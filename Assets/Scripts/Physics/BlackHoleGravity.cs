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

        public int TrackedBodiesCount => _trackedBodies.Count;

        private void Awake()
        {
            if (centerPoint == null) centerPoint = transform;
        }

        private void FixedUpdate()
        {
            ApplyBlackHoleForces();
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

                Vector3 toCenter = center - rb.position;
                float dist = toCenter.magnitude;

                // Event horizon crossing (consumption)
                if (dist <= config.eventHorizonRadius)
                {
                    ConsumeObject(body);
                    continue;
                }

                Vector3 dirToCenter = dist > 0.001f ? toCenter / dist : Vector3.zero;

                // Clockwise tangent vector when looking down +Y axis
                // Cross(dirToCenter, up) yields clockwise tangent
                Vector3 clockwiseTangent = Vector3.Cross(dirToCenter, Vector3.up).normalized;
                if (!config.clockwiseOrbit)
                {
                    clockwiseTangent = -clockwiseTangent;
                }

                // Clamped distance to prevent singularity slingshot glitch
                float clampedDist = Mathf.Max(dist, config.minGravityDistance);

                // Inverse-square law acceleration
                float inwardAccel = config.gravityConstantG / (clampedDist * clampedDist);

                // Target ideal orbital velocity at this radius: sqrt(G / r)
                float targetOrbitalSpeed = Mathf.Sqrt(config.gravityConstantG / clampedDist);

                bool isPlayer = body.CompareTag("Player") || body.GetComponent<DroneController>() != null;

                if (isPlayer)
                {
                    // Honest Newtonian acceleration for PlayerDrone (enables slingshot gravitational assists)
                    float playerMult = config.playerGravityMultiplier;
                    Vector3 radialForce = dirToCenter * (inwardAccel * playerMult);
                    Vector3 swirlForce = clockwiseTangent * (inwardAccel * 0.35f * playerMult);

                    body.Rigidbody.AddForce(radialForce + swirlForce, ForceMode.Acceleration);
                }
                else
                {
                    // Controlled converging spiral orbit for Scrap / debris
                    Vector3 currentVel = rb.linearVelocity;

                    // Decompose velocity into radial (toward center) and tangential components
                    Vector3 currentRadialVel = Vector3.Dot(currentVel, dirToCenter) * dirToCenter;
                    Vector3 currentTangentVel = currentVel - currentRadialVel;

                    // Desired orbital tangential velocity with spiral decay factor
                    Vector3 desiredTangentVel = clockwiseTangent * (targetOrbitalSpeed * config.inwardSpiralDecay);

                    // Smoothly steer tangential velocity toward ideal clockwise orbit
                    float guidanceStep = config.orbitalGuidanceStrength * inwardAccel * Time.fixedDeltaTime;
                    Vector3 steeredTangentVel = Vector3.MoveTowards(currentTangentVel, desiredTangentVel, guidanceStep);

                    // Compute inward approach speed (gradually accelerating toward singularity)
                    float currentInwardSpeed = Mathf.Max(0f, Vector3.Dot(currentVel, dirToCenter));
                    float targetInwardSpeed = Mathf.Min(currentInwardSpeed + (inwardAccel * Time.fixedDeltaTime), targetOrbitalSpeed * 0.75f);
                    Vector3 newRadialVel = dirToCenter * targetInwardSpeed;

                    // Reconstruct velocity and clamp to maxSafeVelocity
                    Vector3 finalVel = newRadialVel + steeredTangentVel;
                    if (finalVel.magnitude > config.maxSafeVelocity)
                    {
                        finalVel = finalVel.normalized * config.maxSafeVelocity;
                    }

                    rb.linearVelocity = finalVel;
                }
            }
        }

        public void ConsumeObject(GravityBody body)
        {
            if (body == null || _consumingBodies.Contains(body)) return;

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
                body.Rigidbody.isKinematic = true;
                body.Rigidbody.linearVelocity = Vector3.zero;
                body.Rigidbody.angularVelocity = Vector3.zero;
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
                OnObjectConsumed?.Invoke(body);
                _trackedBodies.Remove(body);
                _consumingBodies.Remove(body);
                Destroy(body.gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            GravityBody body = other.GetComponentInParent<GravityBody>();
            if (body != null && !_trackedBodies.Contains(body) && !_consumingBodies.Contains(body))
            {
                _trackedBodies.Add(body);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            GravityBody body = other.GetComponentInParent<GravityBody>();
            if (body != null)
            {
                _trackedBodies.Remove(body);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float eventRadius = config != null ? config.eventHorizonRadius : 3.5f;
            float wellRadius = config != null ? config.gravityWellRadius : 45f;

            // Event Horizon Sphere (Red)
            Gizmos.color = new Color(1f, 0f, 0f, 0.7f);
            Gizmos.DrawWireSphere(center, eventRadius);

            // Gravity Well Sphere (Purple)
            Gizmos.color = new Color(0.6f, 0.2f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, wellRadius);
        }
    }
}
