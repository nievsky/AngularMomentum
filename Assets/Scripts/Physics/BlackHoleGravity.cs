using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Player;

namespace OrbitalPanopticon.Physics
{
    [DisallowMultipleComponent]
    public class BlackHoleGravity : MonoBehaviour
    {
        [Header("Master Balance Configuration")]
        [SerializeField] private GameBalanceConfig balanceConfig;

        [Header("Scene References")]
        [SerializeField] private Transform centerPoint;
        [SerializeField] private SphereCollider eventHorizonTrigger;
        [SerializeField] private SphereCollider gravityWellTrigger;

        [Header("Singularity Implosion VFX")]
        [SerializeField] private GameObject singularityImplosionPrefab;

        public GameObject SingularityImplosionPrefab
        {
            get => singularityImplosionPrefab;
            set => singularityImplosionPrefab = value;
        }

        public Transform CenterPoint => centerPoint != null ? centerPoint : transform;
        public Vector3 BlackHoleCenter => CenterPoint.position;
        public Vector3 blackHoleCenter => BlackHoleCenter;
        public GameBalanceConfig Config => balanceConfig != null ? balanceConfig : (GameManager.GameBalanceConfigInstance ?? GameBalanceConfig.Instance);
        public GameBalanceConfig config => Config;

        // Events
        public event Action<GravityBody> OnObjectConsumed;

        // Internal tracking
        private readonly List<GravityBody> _trackedBodies = new List<GravityBody>();
        private readonly HashSet<GravityBody> _consumingBodies = new HashSet<GravityBody>();
        private float _hawkingPulseTimer = 0f;

        public int TrackedBodiesCount => _trackedBodies.Count;

        private void Awake()
        {
            if (centerPoint == null) centerPoint = transform;
        }

        private void FixedUpdate()
        {
            ApplyBlackHoleForces();
            UpdateHawkingPulse();
        }

        private void UpdateHawkingPulse()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null && config.blackHoleHawkingPulseUnlocked)
            {
                _hawkingPulseTimer += Time.fixedDeltaTime;
                float interval = config.blackHoleWowPulseInterval > 0f ? config.blackHoleWowPulseInterval : 12f;
                if (_hawkingPulseTimer >= interval)
                {
                    _hawkingPulseTimer = 0f;
                    TriggerHawkingPulse(config);
                }
            }
        }

        private void TriggerHawkingPulse(GameBalanceConfig config)
        {
            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;

            StartCoroutine(HawkingShockwaveRoutine(center));

            var simManager = OrbitalPanopticon.Gameplay.ScrapSimulationManager.Instance;
            if (simManager == null) return;

            var candidates = new List<(OrbitalPanopticon.Gameplay.ScrapItem scrap, float dist)>();
            float threshold = config.accretionThresholdRadius;

            foreach (var scrap in simManager.ActiveScrap)
            {
                if (scrap == null || !scrap.gameObject.activeInHierarchy || scrap.CurrentState != OrbitalPanopticon.Gameplay.ScrapState.Floating) continue;
                float d = Vector3.Distance(scrap.transform.position, center);
                if (d > threshold)
                {
                    candidates.Add((scrap, d));
                }
            }

            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));

            int count = Mathf.Min(8, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                OrbitalPanopticon.Gameplay.ScrapItem scrap = candidates[i].scrap;
                Vector3 toCenter = (center - scrap.transform.position).normalized;
                scrap.TransitionToLaunched(toCenter * 26f);
            }
        }

        private System.Collections.IEnumerator HawkingShockwaveRoutine(Vector3 center)
        {
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "HawkingPulseShockwave";
            ring.transform.position = center;
            Destroy(ring.GetComponent<Collider>());

            Material pulseMat = null;
            Color c = new Color(0.2f, 0.85f, 1f, 0.5f);
            Shader pulseShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (pulseShader != null)
            {
                pulseMat = new Material(pulseShader);
                pulseMat.SetColor("_BaseColor", c);
                if (pulseMat.HasProperty("_Color")) pulseMat.SetColor("_Color", c);
                var mr = ring.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = pulseMat;
            }

            float elapsed = 0f;
            const float duration = 0.65f;

            while (elapsed < duration && ring != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(4f, 42f, t);
                ring.transform.localScale = new Vector3(radius, 0.1f, radius);

                if (pulseMat != null)
                {
                    c.a = Mathf.Lerp(0.6f, 0f, t);
                    pulseMat.SetColor("_BaseColor", c);
                    if (pulseMat.HasProperty("_Color")) pulseMat.SetColor("_Color", c);
                }

                yield return null;
            }

            if (ring != null) Destroy(ring);
        }

        public void ApplyBlackHoleForces()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null) return;

            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;

            for (int i = _trackedBodies.Count - 1; i >= 0; i--)
            {
                GravityBody body = _trackedBodies[i];
                if (body == null || _consumingBodies.Contains(body))
                {
                    _trackedBodies.RemoveAt(i);
                    continue;
                }

                Rigidbody rb = body.Rigidbody;
                if (rb == null) continue;

                Vector3 toCenter3D = center - rb.position;
                float dist3D = toCenter3D.magnitude;

                bool isPlayer = body.CompareTag("Player") || body.GetComponent<DroneController>() != null;

                // Event horizon crossing (consumption)
                if (dist3D <= config.eventHorizonRadius)
                {
                    if (isPlayer)
                    {
                        if (ArenaBoundaryManager.Instance != null && ArenaBoundaryManager.Instance.IsBoundaryActive)
                        {
                            ArenaBoundaryManager.Instance.OnPlayerConsumedByBlackHole(body.transform.position);
                        }
                        return;
                    }

                    ConsumeObject(body);
                    continue;
                }

                Vector3 dirToCenter3D = dist3D > 0.001f ? toCenter3D / dist3D : Vector3.zero;

                // Horizontal direction for the tangent so it never degenerates when an object is directly above/below (0, Y, 0)
                Vector3 horizontalDir = new Vector3(toCenter3D.x, 0f, toCenter3D.z).normalized;
                if (horizontalDir.sqrMagnitude < 0.01f) horizontalDir = Vector3.forward;
                Vector3 clockwiseTangent = Vector3.Cross(horizontalDir, Vector3.up).normalized;
                if (!config.clockwiseOrbit)
                {
                    clockwiseTangent = -clockwiseTangent;
                }

                if (isPlayer)
                {
                    // Player Two-Zone Physics
                    if (dist3D > config.accretionThresholdRadius)
                    {
                        // Outer calm zone: gentle clockwise drift with matching centripetal hold (almost zero inward pull, prevents centrifugal drift)
                        float driftSpeed = 0.6f * config.globalSimulationSpeed;
                        Vector3 playerDrift = clockwiseTangent * driftSpeed;
                        float centripetalAccel = (driftSpeed * driftSpeed) / Mathf.Max(1f, dist3D);
                        Vector3 centripetal = dirToCenter3D * centripetalAccel;
                        rb.AddForce(playerDrift + centripetal, ForceMode.Acceleration);
                    }
                    else
                    {
                        // Inside accretion threshold: exponential ramp up allowing diving & slingshot maneuvers
                        float stageGravityMult = (BlackHoleStageManager.Instance != null) ? BlackHoleStageManager.Instance.CurrentStageGravityMultiplier : ((BlackHoleMassAndEnergyManager.Instance != null) ? BlackHoleMassAndEnergyManager.Instance.CurrentStageGravityMultiplier : 1.0f);
                        float depth01 = 1f - Mathf.Clamp01((dist3D - config.eventHorizonRadius) / Mathf.Max(0.1f, config.accretionThresholdRadius - config.eventHorizonRadius));
                        float playerPull = Mathf.Lerp(2.0f, 25.0f, Mathf.Pow(depth01, 2.0f)) * config.globalSimulationSpeed * config.playerGravityMultiplier * stageGravityMult;
                        float playerSwirl = Mathf.Lerp(4.0f, 18.0f, Mathf.Pow(depth01, 1.5f)) * config.globalSimulationSpeed * config.playerGravityMultiplier * stageGravityMult;

                        Vector3 radialForce = dirToCenter3D * playerPull;
                        Vector3 swirlForce = clockwiseTangent * playerSwirl;
                        rb.AddForce(radialForce + swirlForce, ForceMode.Acceleration);
                    }
                }
                else
                {
                    ScrapItem scrap = body.GetComponent<ScrapItem>();

                    // Skip physics if held by manipulator
                    if (scrap != null && scrap.CurrentState == ScrapState.Held)
                    {
                        continue;
                    }

                    // Skip physics if fading out
                    if (scrap != null && scrap.CurrentState == ScrapState.FadingOut)
                    {
                        continue;
                    }

                    // Check if object is outside the Accretion Threshold (and hasn't entered vortex)
                    if (dist3D > config.accretionThresholdRadius && (scrap == null || !scrap.hasEnteredVortex))
                    {
                        // Launched scrap outside threshold: flies purely on ballistic velocity with gentle natural drag
                        if (scrap != null && scrap.CurrentState == ScrapState.Launched)
                        {
                            if (scrap.ballisticLaunchTimer > 0f)
                            {
                                scrap.ballisticLaunchTimer = Mathf.Max(0f, scrap.ballisticLaunchTimer - Time.fixedDeltaTime);
                            }

                            rb.linearDamping = 0.25f;

                            // Once speed drops below outerZoneDriftSpeed * 1.3f while still outside threshold,
                            // smoothly transition back to Floating at its new radius
                            if (rb.linearVelocity.magnitude <= config.outerZoneDriftSpeed * 1.3f)
                            {
                                scrap.SetState(ScrapState.Floating);
                                scrap.HomeOrbitRadius = dist3D;
                                scrap.hasEnteredVortex = false;
                                scrap.timeInVortex = 0f;
                                rb.linearDamping = 0f;
                            }

                            continue;
                        }

                        // ZONE A: Outer Calm Zone (Floating scrap)
                        // Scrap MUST NOT fall into the black hole on its own!
                        rb.linearDamping = 0f;
                        float homeRadius = scrap != null && scrap.HomeOrbitRadius > 0.1f ? scrap.HomeOrbitRadius : dist3D;
                        float radialOffset = dist3D - homeRadius;

                        // Radial stabilization keeping it on its home circular orbit ring
                        Vector3 radialStabVel = dirToCenter3D * (radialOffset * config.outerZoneOrbitSpring);
                        // Tangential clockwise drift
                        Vector3 tangentDriftVel = clockwiseTangent * (config.outerZoneDriftSpeed * config.globalSimulationSpeed);

                        Vector3 targetVel = tangentDriftVel + radialStabVel;
                        // Gently damp vertical plane movement to maintain disk stability
                        float relY = rb.position.y - center.y;
                        targetVel.y = Mathf.Lerp(rb.linearVelocity.y, -relY * 2.0f, Time.fixedDeltaTime * 2.0f);

                        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, Time.fixedDeltaTime * 3.5f);
                    }
                    else
                    {
                        // ZONE B: Inner Accretion Threshold (dist3D <= accretionThresholdRadius)
                        // Only enters the vortex when 3D distance actually crosses inside the threshold!
                        rb.linearDamping = 0f;
                        if (scrap != null && scrap.CurrentState == ScrapState.Floating)
                        {
                            scrap.SetState(ScrapState.Launched);
                        }

                        // Calculate dynamic per-object 3D orbital axis on threshold entry (No single-plane snapping)
                        if (scrap != null && !scrap.hasEnteredVortex)
                        {
                            scrap.hasEnteredVortex = true;
                            scrap.timeInVortex = 0f;

                            // Calculate customOrbitAxis
                            Vector3 lateralVel = Vector3.ProjectOnPlane(rb.linearVelocity, dirToCenter3D);
                            Vector3 defaultCw = Vector3.Cross(dirToCenter3D, Vector3.up);
                            if (defaultCw.sqrMagnitude < 0.01f)
                            {
                                defaultCw = Vector3.Cross(dirToCenter3D, Vector3.right);
                            }
                            if (!config.clockwiseOrbit)
                            {
                                defaultCw = -defaultCw;
                            }

                            Vector3 entryTangent = (lateralVel * 0.6f + defaultCw.normalized * 6.0f + UnityEngine.Random.insideUnitSphere * 1.5f).normalized;
                            scrap.customOrbitAxis = Vector3.Cross(dirToCenter3D, entryTangent).normalized;
                            if (scrap.customOrbitAxis.sqrMagnitude < 0.01f)
                            {
                                scrap.customOrbitAxis = Vector3.up;
                            }

                            // Convert direct radial entry velocity into orbital swirl:
                            float entrySpeed = Mathf.Max(rb.linearVelocity.magnitude, 14f);
                            Vector3 initialTangent3D = Vector3.Cross(dirToCenter3D, scrap.customOrbitAxis).normalized;
                            if (Vector3.Dot(initialTangent3D, entryTangent) < 0f)
                            {
                                scrap.customOrbitAxis = -scrap.customOrbitAxis;
                                initialTangent3D = -initialTangent3D;
                            }

                            // Set initial spiral radius clamped outside event horizon
                            scrap.currentSpiralRadius = Mathf.Clamp(dist3D, config.eventHorizonRadius + 1f, config.accretionThresholdRadius);

                            // Redirect inward momentum directly into tangential orbital momentum!
                            rb.linearVelocity = initialTangent3D * entrySpeed;
                        }
                        else if (scrap != null && scrap.currentSpiralRadius <= 0.01f)
                        {
                            scrap.currentSpiralRadius = Mathf.Clamp(dist3D, config.eventHorizonRadius + 2f, config.accretionThresholdRadius);
                        }

                        if (scrap != null)
                        {
                            scrap.timeInVortex += Time.fixedDeltaTime;
                        }

                        // Normalized depth along the vortex span (0 at threshold, 1 at event horizon)
                        float totalVortexSpan = Mathf.Max(1f, config.accretionThresholdRadius - config.eventHorizonRadius);
                        float currentRadius = scrap != null ? scrap.currentSpiralRadius : dist3D;
                        float depth01 = 1f - Mathf.Clamp01((currentRadius - config.eventHorizonRadius) / totalVortexSpan);

                        float baseAngularSpeed = config.baseOrbitalAngularSpeedDeg > 10f ? config.baseOrbitalAngularSpeedDeg : 260f;
                        float targetOrbits = config.targetOrbitsBeforeConsumption > 0.5f ? config.targetOrbitsBeforeConsumption : 3.5f;

                        // Angular speed in radians/sec: accelerates up to 2.2x as radius shrinks
                        float angularSpeedRad = Mathf.Lerp(baseAngularSpeed, baseAngularSpeed * 2.2f, depth01) * Mathf.Deg2Rad;

                        // Tangential linear speed:
                        float swirlSpeed = angularSpeedRad * currentRadius;

                        // Exact radial shrink rate derived from targetOrbitsBeforeConsumption:
                        float timePerOrbit = (2f * Mathf.PI) / angularSpeedRad;
                        float radialShrinkSpeed = totalVortexSpan / Mathf.Max(0.1f, targetOrbits * timePerOrbit);

                        // Advance spiral radius
                        if (scrap != null)
                        {
                            scrap.currentSpiralRadius -= radialShrinkSpeed * Time.fixedDeltaTime;
                        }

                        // 3D Spiral vectors strictly along item's customOrbitAxis
                        Vector3 orbitAxis = (scrap != null && scrap.customOrbitAxis.sqrMagnitude > 0.01f) ? scrap.customOrbitAxis : Vector3.up;
                        Vector3 tangent3D = Vector3.Cross(dirToCenter3D, orbitAxis).normalized;

                        // Keep object locked onto its tilted orbital plane passing through center (0,0,0)
                        Vector3 planeDriftCorrection = -Vector3.Project(rb.position - center, orbitAxis) * 8.0f;

                        // Smoothly guide object onto the shrinking sphere of radius currentSpiralRadius
                        Vector3 targetPosOnSphere = center - dirToCenter3D * (scrap != null ? scrap.currentSpiralRadius : dist3D);
                        Vector3 radialCorrectionVel = (targetPosOnSphere - rb.position) * 8.0f;

                        rb.linearVelocity = (tangent3D * swirlSpeed) + radialCorrectionVel + planeDriftCorrection;
                    }
                }
            }
        }

        public void RegisterBody(GravityBody body)
        {
            if (body != null && !_trackedBodies.Contains(body) && !_consumingBodies.Contains(body))
            {
                _trackedBodies.Add(body);
            }
        }

        public void UnregisterBody(GravityBody body)
        {
            if (body != null)
            {
                _trackedBodies.Remove(body);
            }
        }

        public void ConsumeObject(GravityBody body)
        {
            if (body == null || _consumingBodies.Contains(body)) return;

            bool isPlayer = body.CompareTag("Player") || body.GetComponent<DroneController>() != null;
            if (isPlayer)
            {
                if (ArenaBoundaryManager.Instance != null)
                {
                    ArenaBoundaryManager.Instance.OnPlayerConsumedByBlackHole(body.transform.position);
                }
                return;
            }

            Vector3 itemPos = body.transform.position;
            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;
            Camera mainCam = Camera.main;
            Vector3 toCam = mainCam != null ? (mainCam.transform.position - center).normalized : Vector3.up;

            GameBalanceConfig config = balanceConfig != null ? balanceConfig : (GameManager.GameBalanceConfigInstance ?? GameBalanceConfig.Instance);
            float ehRadius = config != null ? config.eventHorizonRadius : 5.8f;

            // Offset the particle burst position slightly toward Camera.main along the surface of the Event Horizon
            Vector3 vfxSpawnPos = itemPos + toCam * (ehRadius * 0.6f);

            // Spawn the consumption sparks overlay burst
            SpawnConsumptionBurst(vfxSpawnPos);

            _consumingBodies.Add(body);

            // Notify scrap system if applicable & record horizon entry position
            ScrapItem scrap = body.GetComponent<ScrapItem>();
            if (scrap != null)
            {
                scrap.consumptionPosition = itemPos;
                scrap.SetState(ScrapState.Consumed);
            }

            StartCoroutine(SpaghettifyRoutine(body, vfxSpawnPos));
        }

        private void SpawnConsumptionBurst(Vector3 spawnPos)
        {
            GameObject prefab = singularityImplosionPrefab;
            if (prefab == null)
            {
#if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX_SingularityImplosion.prefab");
                if (prefab == null)
                {
                    prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX_AccretionSparks.prefab");
                }
#endif
                if (prefab == null)
                {
                    prefab = Resources.Load<GameObject>("VFX_SingularityImplosion");
                    if (prefab == null)
                    {
                        prefab = Resources.Load<GameObject>("VFX_AccretionSparks");
                    }
                }
            }

            if (prefab != null)
            {
                GameObject burst = Instantiate(prefab, spawnPos, Quaternion.identity);
                ParticleSystem ps = burst.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    var em = ps.emission;
                    em.enabled = true;
                    ps.Play();
                }
                if (Application.isPlaying) Destroy(burst, 0.85f); else DestroyImmediate(burst);
            }
        }

        private IEnumerator SpaghettifyRoutine(GravityBody body, Vector3 vfxSpawnPos)
        {
            if (body == null) yield break;

            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;
            Vector3 startPos = body.transform.position;

            // Disable physical colliders
            Collider[] colliders = body.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = false;
            }

            // Lock physics
            if (body.Rigidbody != null)
            {
                body.Rigidbody.linearVelocity = Vector3.zero;
                body.Rigidbody.angularVelocity = Vector3.zero;
                body.Rigidbody.isKinematic = true;
            }

            // Find visual root for scaling and initial visual scale
            Transform visualTransform = body.transform.Find("VisualRoot");
            if (visualTransform == null) visualTransform = body.transform;

            ScrapItem scrap = body.GetComponent<ScrapItem>();
            Vector3 initialVisualScale = (scrap != null && scrap.InitialVisualScale != Vector3.zero)
                ? scrap.InitialVisualScale
                : visualTransform.localScale;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance ?? GameBalanceConfig.Instance;
            float stretchMult = config != null ? config.spaghettifyStretchMultiplier : 1.65f;
            float thinnessMult = config != null ? config.spaghettifyThinnessMultiplier : 0.55f;

            Vector3 targetScale = new Vector3(
                initialVisualScale.x * thinnessMult,
                initialVisualScale.y * thinnessMult,
                initialVisualScale.z * stretchMult
            );

            const float duration = 0.32f;
            float elapsed = 0f;

            while (elapsed < duration && body != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth inward pull
                body.transform.position = Vector3.Lerp(startPos, center, Mathf.SmoothStep(0f, 1f, t));

                // Continuously compute direction straight toward Black Hole center
                Vector3 toHole = center - body.transform.position;
                if (toHole.sqrMagnitude > 0.0001f)
                {
                    Vector3 dirToHole = toHole.normalized;
                    body.transform.rotation = Quaternion.Slerp(
                        body.transform.rotation,
                        Quaternion.LookRotation(dirToHole),
                        Mathf.Clamp01(t * 15f * Time.deltaTime + 0.05f)
                    );
                }

                // Interpolate visualRoot.localScale relative to initialVisualScale
                Vector3 currentStretchedScale = Vector3.Lerp(initialVisualScale, targetScale, Mathf.SmoothStep(0f, 1f, t));
                if (elapsed >= (duration - 0.08f))
                {
                    float fadeT = Mathf.Clamp01((elapsed - (duration - 0.08f)) / 0.08f);
                    visualTransform.localScale = Vector3.Lerp(currentStretchedScale, Vector3.zero, fadeT);
                }
                else
                {
                    visualTransform.localScale = currentStretchedScale;
                }

                if (scrap != null)
                {
                    scrap.TriggerSpaghettifyBurst(t);
                }

                yield return null;
            }

            if (body != null)
            {
                bool isPlayer = body.CompareTag("Player") || body.GetComponent<DroneController>() != null;
                if (isPlayer)
                {
                    _trackedBodies.Remove(body);
                    _consumingBodies.Remove(body);
                    yield break;
                }

                TriggerSingularityImplosion(vfxSpawnPos);

                OnObjectConsumed?.Invoke(body);
                _trackedBodies.Remove(body);
                _consumingBodies.Remove(body);

                if (scrap != null && ScrapSimulationManager.Instance != null)
                {
                    ScrapSimulationManager.Instance.ReturnToPool(scrap);
                }
                else
                {
                    Destroy(body.gameObject);
                }
            }
        }

        private void TriggerSingularityImplosion(Vector3 center)
        {
            if (singularityImplosionPrefab == null)
            {
#if UNITY_EDITOR
                singularityImplosionPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX_SingularityImplosion.prefab");
#endif
                if (singularityImplosionPrefab == null)
                {
                    singularityImplosionPrefab = Resources.Load<GameObject>("VFX_SingularityImplosion");
                }
            }

            if (singularityImplosionPrefab != null)
            {
                GameObject implosion = Instantiate(singularityImplosionPrefab, center, Quaternion.identity);
                if (Application.isPlaying) Destroy(implosion, 0.6f); else DestroyImmediate(implosion);
            }

            if (PostProcessingToolkitController.Instance != null)
            {
                PostProcessingToolkitController.Instance.TriggerImpactPulse(1.8f, 0.25f, 0.25f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            GravityBody body = other.GetComponentInParent<GravityBody>();
            if (body != null)
            {
                RegisterBody(body);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            GravityBody body = other.GetComponentInParent<GravityBody>();
            if (body != null)
            {
                UnregisterBody(body);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float eventRadius = config != null ? config.eventHorizonRadius : 3.5f;
            float thresholdRadius = config != null ? config.accretionThresholdRadius : 15.0f;
            float wellRadius = config != null ? config.gravityWellRadius : 45f;

            // Event Horizon Sphere (Red)
            Gizmos.color = new Color(1f, 0f, 0f, 0.7f);
            Gizmos.DrawWireSphere(center, eventRadius);

            // Accretion Threshold (Orange/Gold)
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.6f);
            Gizmos.DrawWireSphere(center, thresholdRadius);

            // Gravity Well Sphere (Purple)
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, wellRadius);
        }
    }
}
