using System.Collections;
using UnityEngine;
using OrbitalPanopticon.Player;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class ArenaBoundaryManager : MonoBehaviour
    {
        [Header("Boundary Radii")]
        [Tooltip("Max outer radius of the orbital arena. Beyond this distance, connection is lost.")]
        [SerializeField] private float maxArenaRadius = 45f;

        [Tooltip("Inner radius representing the lethal event horizon of the black hole.")]
        [SerializeField] private float eventHorizonRadius = 4.5f;

        [Tooltip("Time in seconds controls are locked while drone is reconnected/teleported.")]
        [SerializeField] private float respawnDelay = 1.5f;

        [Header("Locations & Center")]
        [SerializeField] private Transform centerTransform;
        [SerializeField] private Transform droneSpawnPoint;

        [Header("Drone & Wreckage References")]
        [SerializeField] private DroneController playerDrone;
        [SerializeField] private ScrapItem droneWreckagePrefab;
        [SerializeField] private ScrapData wreckageData;

        [Header("Status")]
        [SerializeField] private bool isRespawning = false;

        // Events for UI HUD notifications
        public event System.Action<string> OnSignalLost;

        public float MaxArenaRadius => maxArenaRadius;
        public float EventHorizonRadius => eventHorizonRadius;
        public bool IsRespawning => isRespawning;

        private void Awake()
        {
            if (playerDrone == null)
            {
                playerDrone = Object.FindAnyObjectByType<DroneController>();
            }
        }

        private void Update()
        {
            if (isRespawning || playerDrone == null) return;

            Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;
            float distance = Vector3.Distance(playerDrone.transform.position, center);

            if (distance > maxArenaRadius)
            {
                TriggerSignalLoss("OUT OF RANGE - SIGNAL LOST", false);
            }
            else if (distance < eventHorizonRadius)
            {
                TriggerSignalLoss("CRITICAL MASS - DRONE CONSUMED", true);
            }
        }

        public void TriggerSignalLoss(string reason, bool spawnWreckage)
        {
            if (isRespawning) return;
            isRespawning = true;

            Debug.LogWarning($"[ArenaBoundary] Signal Lost: {reason}");
            OnSignalLost?.Invoke(reason);

            if (playerDrone != null)
            {
                playerDrone.SetControlEnabled(false);

                if (spawnWreckage)
                {
                    SpawnWreckage(playerDrone.transform.position);
                }

                StartCoroutine(RespawnRoutine());
            }
        }

        private void SpawnWreckage(Vector3 position)
        {
            if (droneWreckagePrefab != null)
            {
                ScrapItem wreckage = Instantiate(droneWreckagePrefab, position, Random.rotation);
                if (wreckageData != null)
                {
                    wreckage.Initialize(wreckageData);
                }
                wreckage.SetState(ScrapState.Launched);

                // Add slight inward spiral impulse toward the black hole center
                Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;
                Vector3 inwardDir = (center - position).normalized;
                if (wreckage.Rigidbody != null)
                {
                    wreckage.Rigidbody.linearVelocity = inwardDir * 5f;
                }
            }
        }

        private IEnumerator RespawnRoutine()
        {
            Vector3 spawnPos = droneSpawnPoint != null ? droneSpawnPoint.position : new Vector3(0f, 10f, -35f);
            Quaternion spawnRot = droneSpawnPoint != null ? droneSpawnPoint.rotation : Quaternion.identity;

            // Wait for signal loss cooldown
            yield return new WaitForSeconds(respawnDelay);

            if (playerDrone != null)
            {
                playerDrone.Teleport(spawnPos, spawnRot);
                playerDrone.SetControlEnabled(true);
            }

            isRespawning = false;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;

            // Outer Arena Sphere (Cyan)
            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(center, maxArenaRadius);

            // Event Horizon (Red)
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.45f);
            Gizmos.DrawWireSphere(center, eventHorizonRadius);
        }
    }
}
