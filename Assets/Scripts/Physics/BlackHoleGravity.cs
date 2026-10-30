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
        [Header("Scene References")]
        [SerializeField] private Transform centerPoint;
        [SerializeField] private SphereCollider eventHorizonTrigger;
        [SerializeField] private SphereCollider gravityWellTrigger;

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

            Material pulseMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            Color c = new Color(0.2f, 0.85f, 1f, 0.5f);
            pulseMat.SetColor("_BaseColor", c);
            if (pulseMat.HasProperty("_Color")) pulseMat.SetColor("_Color", c);
            ring.GetComponent<MeshRenderer>().sharedMaterial = pulseMat;

            float elapsed = 0f;
            const float duration = 0.65f;

            while (elapsed < duration && ring != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float radius = Mathf.Lerp(4f, 42f, t);
                ring.transform.localScale = new Vector3(radius, 0.1f, radius);

                c.a = Mathf.Lerp(0.6f, 0f, t);
                pulseMat.SetColor("_BaseColor", c);
                if (pulseMat.HasProperty("_Color")) pulseMat.SetColor("_Color", c);

                yield return null;
            }

            if (ring != null) Destroy(ring);
        }

        private void ApplyBlackHoleForces()
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
                        if (ArenaBoundaryManager.Instance != null)
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
                        float depth01 = 1f - Mathf.Clamp01((dist3D - config.eventHorizonRadius) / Mathf.Max(0.1f, config.accretionThresholdRadius - config.eventHorizonRadius));
                        float playerPull = Mathf.Lerp(2.0f, 25.0f, Mathf.Pow(depth01, 2.0f)) * config.globalSimulationSpeed * config.playerGravityMultiplier;
                        float playerSwirl = Mathf.Lerp(4.0f, 18.0f, Mathf.Pow(depth01, 1.5f)) * config.globalSimulationSpeed * config.playerGravityMultiplier;

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

                    // Ballistic Launch Phase: Let punted scrap fly purely on its ballistic shot trajectory without interference
                    if (scrap != null && scrap.ballisticLaunchTimer > 0f && dist3D > config.accretionThresholdRadius)
                    {
                        scrap.ballisticLaunchTimer = Mathf.Max(0f, scrap.ballisticLaunchTimer - Time.fixedDeltaTime);
                        continue;
                    }

                    // ZONE A: Outer Calm Zone (dist3D > accretionThresholdRadius AND Floating)
                    // Scrap MUST NOT fall into the black hole on its own!
                    bool isFloating = (scrap == null || scrap.CurrentState == ScrapState.Floating);
                    if (dist3D > config.accretionThresholdRadius && isFloating)
                    {
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
                        // ZONE B: Inner Accretion Threshold (dist3D <= accretionThresholdRadius OR Launched)
                        // Once launched or pushed into threshold, it commits to active inward swirl
                        if (scrap != null && scrap.CurrentState == ScrapState.Floating)
                        {
                            scrap.SetState(ScrapState.Launched);
                        }

                        // Calculate dynamic per-object 3D orbital axis on threshold entry (No single-plane snapping)
                        if (scrap != null && !scrap.hasEnteredVortex)
                        {
                            scrap.hasEnteredVortex = true;
                            scrap.currentSpiralRadius = Mathf.Min(dist3D, config.accretionThresholdRadius);

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
                        }
                        else if (scrap != null && (scrap.currentSpiralRadius <= 0.01f || scrap.currentSpiralRadius > config.accretionThresholdRadius))
                        {
                            scrap.currentSpiralRadius = Mathf.Min(dist3D, config.accretionThresholdRadius);
                        }

                        float thresholdRange = Mathf.Max(0.1f, config.accretionThresholdRadius - config.eventHorizonRadius);
                        float depth01 = 1f - Mathf.Clamp01((dist3D - config.eventHorizonRadius) / thresholdRange);

                        // Step 1 — Shrink the target radius continuously
                        if (scrap != null)
                        {
                            scrap.currentSpiralRadius -= config.spiralRadiusShrinkRate * (1f + depth01 * 1.5f) * config.globalSimulationSpeed * Time.fixedDeltaTime;
                        }

                        // Step 2 — Fast Exponential Clockwise Swirl
                        float swirlSpeed = Mathf.Lerp(config.innerMinSwirlSpeed, config.innerMaxSwirlSpeed, Mathf.Pow(depth01, config.swirlExponentialPower)) * config.globalSimulationSpeed;

                        // Step 3 — Compensate Centrifugal Drift & Pull Inward strictly along dirToCenter3D (True 3D Center Pull)
                        float targetRadius = scrap != null ? scrap.currentSpiralRadius : Mathf.Min(dist3D, config.accretionThresholdRadius);
                        float radiusError = dist3D - targetRadius;
                        float inwardSpeed = (config.spiralRadiusShrinkRate * (1f + depth01)) + Mathf.Max(0f, radiusError * config.spiralTightnessSpring);

                        // 3D Spiral strictly along the item's customOrbitAxis
                        Vector3 orbitAxis = (scrap != null && scrap.customOrbitAxis.sqrMagnitude > 0.01f) ? scrap.customOrbitAxis : Vector3.up;
                        Vector3 tangent3D = Vector3.Cross(dirToCenter3D, orbitAxis).normalized;

                        // Keep object locked onto its tilted orbital plane passing through center (0,0,0)
                        Vector3 planeDriftCorrection = -Vector3.Project(rb.position - center, orbitAxis) * 6.0f;

                        Vector3 targetVel = (tangent3D * swirlSpeed) + (dirToCenter3D * inwardSpeed) + planeDriftCorrection;

                        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, 12f * Time.fixedDeltaTime);

                        // Directly constrain the 3D distance so it cannot exceed currentSpiralRadius:
                        // If dist3D > targetRadius, smoothly pull toward the shrinking 3D sphere around center
                        if (dist3D > targetRadius)
                        {
                            float excess = dist3D - targetRadius;
                            float pullStep = Mathf.Min(excess, excess * config.spiralTightnessSpring * Time.fixedDeltaTime);
                            rb.position += dirToCenter3D * pullStep;
                        }
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

            _consumingBodies.Add(body);

            // Notify scrap system if applicable
            ScrapItem scrap = body.GetComponent<ScrapItem>();
            if (scrap != null)
            {
                scrap.SetState(ScrapState.Consumed);
            }

            StartCoroutine(SpaghettifyRoutine(body));
        }

        private IEnumerator SpaghettifyRoutine(GravityBody body)
        {
            if (body == null) yield break;

            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;
            Vector3 startPos = body.transform.position;
            Vector3 targetScale = new Vector3(0.2f, 0.2f, 4.5f);

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

            // Find visual root for scaling
            Transform visualTransform = body.transform.Find("VisualRoot");
            if (visualTransform == null) visualTransform = body.transform;
            Vector3 startScale = visualTransform.localScale;

            const float duration = 0.35f;
            float elapsed = 0f;

            while (elapsed < duration && body != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Smooth inward pull
                body.transform.position = Vector3.Lerp(startPos, center, t);

                // Look toward black hole singularity
                Vector3 toHole = center - body.transform.position;
                if (toHole.sqrMagnitude > 0.001f)
                {
                    body.transform.rotation = Quaternion.LookRotation(toHole.normalized);
                }

                // Spaghettification stretch
                visualTransform.localScale = Vector3.Lerp(startScale, targetScale, t);

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

                OnObjectConsumed?.Invoke(body);
                _trackedBodies.Remove(body);
                _consumingBodies.Remove(body);

                ScrapItem scrap = body.GetComponent<ScrapItem>();
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
