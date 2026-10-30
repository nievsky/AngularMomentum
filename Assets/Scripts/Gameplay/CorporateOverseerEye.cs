using UnityEngine;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class CorporateOverseerEye : MonoBehaviour
    {
        [Header("Target & References")]
        [SerializeField] private Transform targetDrone;
        [SerializeField] private Transform centerReference;
        [SerializeField] private Light pupilLight;

        [Header("Orbit Geometry")]
        [Tooltip("Additional distance beyond arenaMaxRadius for the outer orbital track.")]
        [SerializeField] private float radiusOffset = 6.0f;

        [Tooltip("Inclination frequency and amplitude factor.")]
        [SerializeField] private float inclinationFrequency = 0.65f;
        [SerializeField] private float inclinationAmplitude = 0.45f;

        // Internal orbit progress
        private float _orbitAngle = 0f;

        [Header("Finale Status")]
        public bool isConsumedOrCollapsing = false;

        private void Awake()
        {
            if (targetDrone == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null) player = GameObject.Find("PlayerDrone");
                if (player != null) targetDrone = player.transform;
            }

            if (centerReference == null)
            {
                GameObject bh = GameObject.Find("BlackHoleSystem");
                if (bh != null) centerReference = bh.transform;
            }

            if (pupilLight == null)
            {
                pupilLight = GetComponentInChildren<Light>();
            }

            // Randomize starting orbit angle
            _orbitAngle = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!isConsumedOrCollapsing)
            {
                UpdateOrbitalPosition();
            }
            TrackPlayerDrone();
            AnimatePupilGlow();
        }

        private void UpdateOrbitalPosition()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float baseRadius = config != null ? config.arenaMaxRadius : 48f;
            float speedMult = config != null ? config.globalSimulationSpeed : 0.5f;
            float orbitSpeed = (config != null ? config.overseerOrbitSpeed : 2.0f) * speedMult;
            float totalRadius = baseRadius + radiusOffset;

            // Advance orbital traversal
            _orbitAngle += orbitSpeed * 0.035f * Time.deltaTime;

            // Compute spherical coordinates with undulating inclination
            float x = Mathf.Cos(_orbitAngle);
            float z = Mathf.Sin(_orbitAngle);
            float y = Mathf.Sin(_orbitAngle * inclinationFrequency) * inclinationAmplitude;

            Vector3 sphereDir = new Vector3(x, y, z).normalized;
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;

            transform.position = center + (sphereDir * totalRadius);
        }

        private void TrackPlayerDrone()
        {
            if (targetDrone == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null) player = GameObject.Find("PlayerDrone");
                if (player != null) targetDrone = player.transform;
            }

            if (targetDrone != null)
            {
                Vector3 toDrone = targetDrone.position - transform.position;
                if (toDrone.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(toDrone.normalized, Vector3.up);
                    GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
                    float rotSpeed = config != null ? config.overseerTrackingSpeed : 1.5f;
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
                }
            }
        }

        private void AnimatePupilGlow()
        {
            if (pupilLight != null)
            {
                // Menacing pulsing pupil
                float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 2.5f));
                pupilLight.intensity = Mathf.Lerp(4f, 8f, pulse);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float totalRadius = (config != null ? config.arenaMaxRadius : 48f) + radiusOffset;

            Gizmos.color = new Color(1f, 0.1f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(center, totalRadius);
        }
    }
}
