using System;
using UnityEngine;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Player
{
    /// <summary>
    /// Dedicated controller for smooth trauma-based camera shake and Stage 4 ambient psychological tremor.
    /// Operates strictly in LateUpdate by applying positional and rotational offsets on top of mouse look,
    /// ensuring camera aim and pitch are never overridden or clobbered.
    /// </summary>
    [DisallowMultipleComponent]
    public class CameraShakeController : MonoBehaviour
    {
        public static CameraShakeController Instance { get; private set; }

        [Header("Trauma Impulse Settings")]
        [SerializeField] private float traumaDecayRate = 1.0f;
        [SerializeField] private float maxPositionShake = 0.45f;
        [SerializeField] private float maxRotationShake = 5.0f;
        [SerializeField] private float shakeFrequency = 22.0f;

        [Header("Stage 4 Ambient Psychological Tremor")]
        [SerializeField] private float ambientTremorPositionAmplitude = 0.025f;
        [SerializeField] private float ambientTremorAngleAmplitude = 0.35f;
        [SerializeField] private float ambientTremorFrequency = 9.0f;

        private Vector3 _baseLocalPosition;
        private float _currentTrauma = 0f;
        private float _traumaSeedX;
        private float _traumaSeedY;
        private float _traumaSeedZ;

        public float CurrentTrauma => _currentTrauma;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            _baseLocalPosition = transform.localPosition;
            _traumaSeedX = UnityEngine.Random.Range(0f, 100f);
            _traumaSeedY = UnityEngine.Random.Range(100f, 200f);
            _traumaSeedZ = UnityEngine.Random.Range(200f, 300f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Updates the stored base local position (e.g. if the camera root moves relative to the drone).
        /// </summary>
        public void SetBaseLocalPosition(Vector3 pos)
        {
            _baseLocalPosition = pos;
        }

        /// <summary>
        /// Adds decaying trauma to the camera for an impact or explosion.
        /// </summary>
        /// <param name="duration">Duration in seconds for the shake to decay to zero.</param>
        /// <param name="magnitude">Trauma impulse added (0.0 to 1.0).</param>
        public void TriggerShake(float duration = 0.5f, float magnitude = 0.3f)
        {
            _currentTrauma = Mathf.Clamp01(_currentTrauma + magnitude);
            traumaDecayRate = 1.0f / Mathf.Max(0.05f, duration);
        }

        /// <summary>
        /// Dedicated shockwave shake triggered when a Containment Lock shatters or a stage advances.
        /// </summary>
        public void TriggerStageTransitionShake(float duration = 1.4f, float magnitude = 0.35f)
        {
            TriggerShake(duration, magnitude);
            Debug.Log($"<color=#FF8800>[CAMERA SHAKE] Stage Transition Shockwave! Duration: {duration}s, Magnitude: {magnitude}</color>");
        }

        /// <summary>
        /// Instantly zeroes out all active trauma and restores the base local transform (e.g. on respawn).
        /// </summary>
        public void ResetShake()
        {
            _currentTrauma = 0f;
            transform.localPosition = _baseLocalPosition;
        }

        private void LateUpdate()
        {
            Vector3 posOffset = Vector3.zero;
            Vector3 rotOffset = Vector3.zero;

            // 1. Decaying Trauma Impulse Shake
            if (_currentTrauma > 0f)
            {
                _currentTrauma = Mathf.Max(0f, _currentTrauma - (traumaDecayRate * Time.deltaTime));
                float shakeIntensity = _currentTrauma * _currentTrauma; // Quadratic falloff for snappy feel

                float t = Time.time * shakeFrequency;
                float px = (Mathf.PerlinNoise(_traumaSeedX, t) * 2f - 1f) * maxPositionShake * shakeIntensity;
                float py = (Mathf.PerlinNoise(_traumaSeedY, t) * 2f - 1f) * maxPositionShake * shakeIntensity;
                float pz = (Mathf.PerlinNoise(_traumaSeedZ, t) * 2f - 1f) * (maxPositionShake * 0.5f) * shakeIntensity;

                float rx = (Mathf.PerlinNoise(_traumaSeedX + 15f, t) * 2f - 1f) * maxRotationShake * shakeIntensity;
                float ry = (Mathf.PerlinNoise(_traumaSeedY + 15f, t) * 2f - 1f) * maxRotationShake * shakeIntensity;
                float rz = (Mathf.PerlinNoise(_traumaSeedZ + 15f, t) * 2f - 1f) * maxRotationShake * shakeIntensity;

                posOffset += new Vector3(px, py, pz);
                rotOffset += new Vector3(rx, ry, rz);
            }

            // 2. Stage 4 Continuous Psychological Ambient Tremor
            if (BlackHoleStageManager.Instance != null && BlackHoleStageManager.Instance.CurrentStage == 4)
            {
                float tAmb = Time.time * ambientTremorFrequency;
                float aPx = (Mathf.PerlinNoise(tAmb, 13.5f) * 2f - 1f) * ambientTremorPositionAmplitude;
                float aPy = (Mathf.PerlinNoise(tAmb, 27.8f) * 2f - 1f) * ambientTremorPositionAmplitude;
                float aPz = (Mathf.PerlinNoise(tAmb, 42.1f) * 2f - 1f) * (ambientTremorPositionAmplitude * 0.5f);

                float aRx = (Mathf.PerlinNoise(tAmb, 55.4f) * 2f - 1f) * ambientTremorAngleAmplitude;
                float aRy = (Mathf.PerlinNoise(tAmb, 68.7f) * 2f - 1f) * ambientTremorAngleAmplitude;
                float aRz = (Mathf.PerlinNoise(tAmb, 81.2f) * 2f - 1f) * ambientTremorAngleAmplitude;

                posOffset += new Vector3(aPx, aPy, aPz);
                rotOffset += new Vector3(aRx, aRy, aRz);
            }

            // Apply offsets strictly on top of mouse look
            transform.localPosition = _baseLocalPosition + posOffset;
            if (rotOffset.sqrMagnitude > 0.0001f)
            {
                transform.localRotation = transform.localRotation * Quaternion.Euler(rotOffset);
            }
        }
    }
}
