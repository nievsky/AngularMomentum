using UnityEngine;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Gameplay
{
    public enum HelperBotState
    {
        Seek,
        Deliver,
        Kamikaze,
        Respawning
    }

    [DisallowMultipleComponent]
    public class HelperDroneBot : MonoBehaviour
    {
        [Header("Tuning")]
        [SerializeField] private float baseSpeed = 14f;
        [SerializeField] private float turnSpeed = 8f;
        [SerializeField] private float kamikazeSpeed = 34f;
        [SerializeField] private float respawnDelay = 2.0f;

        [Header("References")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private LineRenderer tetherLine;
        [SerializeField] private TrailRenderer engineTrail;
        [SerializeField] private Transform tetherNozzle;

        [Header("Runtime Status")]
        [SerializeField] private HelperBotState currentState = HelperBotState.Seek;
        [SerializeField] private ScrapItem targetScrap;

        public HelperBotState CurrentState => currentState;
        public ScrapItem TargetScrap => targetScrap;

        private float _respawnTimer = 0f;
        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null)
            {
                _rb = gameObject.AddComponent<Rigidbody>();
                _rb.useGravity = false;
                _rb.isKinematic = true;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (visualRoot == null)
            {
                visualRoot = transform.Find("VisualRoot");
            }

            if (tetherLine == null)
            {
                tetherLine = GetComponentInChildren<LineRenderer>();
            }

            if (engineTrail == null)
            {
                engineTrail = GetComponentInChildren<TrailRenderer>();
            }

            if (tetherNozzle == null && visualRoot != null)
            {
                Transform nozzle = visualRoot.Find("TetherNozzle");
                tetherNozzle = nozzle != null ? nozzle : visualRoot;
            }
        }

        private void Update()
        {
            if (currentState == HelperBotState.Respawning)
            {
                UpdateRespawn();
                return;
            }

            UpdateTetherVisual();
        }

        private void FixedUpdate()
        {
            if (currentState == HelperBotState.Respawning) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            Vector3 center = Vector3.zero;

            switch (currentState)
            {
                case HelperBotState.Seek:
                    HandleSeek(config, center);
                    break;
                case HelperBotState.Deliver:
                    HandleDeliver(config, center);
                    break;
                case HelperBotState.Kamikaze:
                    HandleKamikaze(config, center);
                    break;
            }
        }

        private void HandleSeek(GameBalanceConfig config, Vector3 center)
        {
            SetTetherActive(false);

            // Validate target or acquire new one
            if (targetScrap == null || !IsScrapValid(targetScrap, config, center))
            {
                ReleaseTargetScrap();
                targetScrap = FindBestScrap(config, center);
            }

            float configuredSpeed = config != null && config.helperBotSpeed > 0f ? config.helperBotSpeed : baseSpeed;
            float speedMultiplier = config != null ? (config.magnetPullForce / 25f) : 1f;
            float currentSpeed = configuredSpeed * Mathf.Max(1f, speedMultiplier);

            if (targetScrap != null)
            {
                Vector3 toTarget = targetScrap.transform.position - transform.position;
                float dist = toTarget.magnitude;

                if (dist > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
                }

                transform.position += transform.forward * (currentSpeed * Time.fixedDeltaTime);

                // Grab scrap if within range
                if (dist <= 2.8f)
                {
                    currentState = HelperBotState.Deliver;
                    SetTetherActive(true);
                }
            }
            else
            {
                // Idle patrol around outer zone
                Vector3 fromCenter = transform.position - center;
                Vector3 tangent = Vector3.Cross(fromCenter.normalized, Vector3.up).normalized;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(tangent), turnSpeed * Time.fixedDeltaTime);
                transform.position += transform.forward * (currentSpeed * 0.65f * Time.fixedDeltaTime);
            }
        }

        private void HandleDeliver(GameBalanceConfig config, Vector3 center)
        {
            if (targetScrap == null || !targetScrap.gameObject.activeInHierarchy || targetScrap.CurrentState == ScrapState.Consumed)
            {
                ReleaseTargetScrap();
                currentState = HelperBotState.Seek;
                return;
            }

            SetTetherActive(true);

            // Steer towards Black Hole center
            Vector3 toCenter = center - transform.position;
            float distToCenter = toCenter.magnitude;
            if (distToCenter > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toCenter.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.fixedDeltaTime);
            }

            float configuredSpeed = config != null && config.helperBotSpeed > 0f ? config.helperBotSpeed : baseSpeed;
            float speedMultiplier = config != null ? (config.magnetPullForce / 25f) : 1f;
            float currentSpeed = configuredSpeed * Mathf.Max(1f, speedMultiplier);
            transform.position += transform.forward * (currentSpeed * Time.fixedDeltaTime);

            // Drag scrap along behind the bot
            Vector3 desiredScrapPos = transform.position - (transform.forward * 1.6f);
            Rigidbody scrapRb = targetScrap.Rigidbody;
            if (scrapRb != null)
            {
                Vector3 pullForce = (desiredScrapPos - targetScrap.transform.position) * 12f;
                scrapRb.linearVelocity = Vector3.Lerp(scrapRb.linearVelocity, pullForce + (transform.forward * currentSpeed), Time.fixedDeltaTime * 6f);
            }

            float threshold = config != null ? config.accretionThresholdRadius : 15.0f;

            // Check if Kamikaze protocol is unlocked
            if (config != null && config.kamikazeProtocolUnlocked)
            {
                if (distToCenter <= threshold + 1.0f)
                {
                    currentState = HelperBotState.Kamikaze;
                    targetScrap.isKamikazeDelivery = true;
                }
            }
            else
            {
                // Normal release safe boundary
                if (distToCenter <= threshold + 2.5f)
                {
                    // Launch scrap into black hole vortex
                    Vector3 launchDir = (center - targetScrap.transform.position).normalized;
                    targetScrap.TransitionToLaunched(launchDir * 20f);

                    ReleaseTargetScrap();
                    currentState = HelperBotState.Seek;
                }
            }
        }

        private void HandleKamikaze(GameBalanceConfig config, Vector3 center)
        {
            SetTetherActive(true);

            Vector3 toCenter = center - transform.position;
            float distToCenter = toCenter.magnitude;
            if (distToCenter > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(toCenter.normalized);
            }

            // Max speed dive directly into singularity
            transform.position += transform.forward * (kamikazeSpeed * Time.fixedDeltaTime);

            if (targetScrap != null)
            {
                targetScrap.isKamikazeDelivery = true;
                if (targetScrap.Rigidbody != null)
                {
                    targetScrap.transform.position = transform.position - (transform.forward * 1.0f);
                    targetScrap.Rigidbody.linearVelocity = transform.forward * kamikazeSpeed;
                }
            }

            float eventRadius = config != null ? config.eventHorizonRadius : 3.5f;
            if (distToCenter <= eventRadius + 0.8f)
            {
                TriggerKamikazeImpact(config, center);
            }
        }

        private void TriggerKamikazeImpact(GameBalanceConfig config, Vector3 center)
        {
            // Disintegrate bot and start respawn
            if (targetScrap != null)
            {
                targetScrap.isKamikazeDelivery = true;
                var blackHole = Object.FindAnyObjectByType<OrbitalPanopticon.Physics.BlackHoleGravity>();
                if (blackHole != null && targetScrap.GravityBody != null)
                {
                    blackHole.ConsumeObject(targetScrap.GravityBody);
                }
                ReleaseTargetScrap();
            }

            SetTetherActive(false);
            if (visualRoot != null) visualRoot.gameObject.SetActive(false);
            if (engineTrail != null) engineTrail.emitting = false;

            currentState = HelperBotState.Respawning;
            _respawnTimer = respawnDelay;
        }

        private void UpdateRespawn()
        {
            _respawnTimer -= Time.deltaTime;
            if (_respawnTimer <= 0f)
            {
                RespawnAtPerimeter();
            }
        }

        private void RespawnAtPerimeter()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float spawnRadius = config != null ? config.maxSpawnRadius : 38f;

            float phi = Random.Range(0f, Mathf.PI * 2f);
            float y = Random.Range(-4f, 4f);
            Vector3 pos = new Vector3(Mathf.Cos(phi) * spawnRadius, y, Mathf.Sin(phi) * spawnRadius);

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation((Vector3.zero - pos).normalized);

            if (visualRoot != null) visualRoot.gameObject.SetActive(true);
            if (engineTrail != null)
            {
                engineTrail.Clear();
                engineTrail.emitting = true;
            }

            currentState = HelperBotState.Seek;
        }

        private void UpdateTetherVisual()
        {
            if (tetherLine == null || !tetherLine.enabled) return;

            Vector3 startPos = tetherNozzle != null ? tetherNozzle.position : transform.position;
            Vector3 endPos = targetScrap != null ? targetScrap.transform.position : transform.position;

            tetherLine.SetPosition(0, startPos);
            tetherLine.SetPosition(1, endPos);
        }

        private void SetTetherActive(bool active)
        {
            if (tetherLine != null && tetherLine.enabled != active)
            {
                tetherLine.enabled = active;
            }
        }

        private bool IsScrapValid(ScrapItem scrap, GameBalanceConfig config, Vector3 center)
        {
            if (scrap == null || !scrap.gameObject.activeInHierarchy) return false;
            if (scrap.CurrentState != ScrapState.Floating) return false;

            float threshold = config != null ? config.accretionThresholdRadius : 15f;
            return Vector3.Distance(scrap.transform.position, center) > threshold;
        }

        private ScrapItem FindBestScrap(GameBalanceConfig config, Vector3 center)
        {
            var simManager = ScrapSimulationManager.Instance;
            if (simManager == null) return null;

            ScrapItem bestValuable = null;
            float closestValuableDist = float.MaxValue;
            ScrapItem bestRegular = null;
            float closestRegularDist = float.MaxValue;
            var helperManager = HelperDroneManager.Instance;

            foreach (var scrap in simManager.ActiveScrap)
            {
                if (!IsScrapValid(scrap, config, center)) continue;
                if (helperManager != null && helperManager.IsScrapClaimed(scrap)) continue;

                float dist = Vector3.Distance(transform.position, scrap.transform.position);
                if (scrap.isValuableScrap)
                {
                    if (dist < closestValuableDist)
                    {
                        closestValuableDist = dist;
                        bestValuable = scrap;
                    }
                }
                else
                {
                    if (dist < closestRegularDist)
                    {
                        closestRegularDist = dist;
                        bestRegular = scrap;
                    }
                }
            }

            ScrapItem best = bestValuable != null ? bestValuable : bestRegular;

            if (best != null && helperManager != null)
            {
                helperManager.ClaimScrap(best);
            }

            return best;
        }

        private void ReleaseTargetScrap()
        {
            if (targetScrap != null && HelperDroneManager.Instance != null)
            {
                HelperDroneManager.Instance.ReleaseScrap(targetScrap);
            }
            targetScrap = null;
        }

        private void OnDestroy()
        {
            ReleaseTargetScrap();
        }
    }
}
