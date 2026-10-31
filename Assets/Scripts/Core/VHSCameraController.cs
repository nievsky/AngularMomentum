using System.Collections;
using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class VHSCameraController : MonoBehaviour
    {
        public static VHSCameraController Instance { get; private set; }

        [Header("Material Reference")]
        [SerializeField] private Material vhsMaterial;

        [Header("Master VHS Controls")]
        [SerializeField] private bool enableVHSEffect = true;
        [Range(0f, 1f)] [SerializeField] private float masterBlend = 0.65f;

        [Header("1. CRT Scanlines")]
        [Range(0f, 1f)] [SerializeField] private float scanlineIntensity = 0.28f;
        [Range(100f, 1080f)] [SerializeField] private float scanlineCount = 640f;
        [Range(-10f, 10f)] [SerializeField] private float scanlineScrollSpeed = 0.8f;

        [Header("2. VHS Tracking Glitch")]
        [Range(0f, 1f)] [SerializeField] private float trackingBarIntensity = 0.45f;
        [Range(0f, 2f)] [SerializeField] private float trackingBarSpeed = 0.18f;
        [Range(0.01f, 0.35f)] [SerializeField] private float trackingBarThickness = 0.08f;
        [Range(0f, 0.15f)] [SerializeField] private float horizontalGlitchStrength = 0.035f;

        [Header("3. RGB Chromatic Shift & Bleed")]
        [Range(0f, 0.05f)] [SerializeField] private float rgbShiftAmount = 0.0055f;

        [Header("4. Tape Noise & Static")]
        [Range(0f, 1f)] [SerializeField] private float tapeNoiseIntensity = 0.18f;

        [Header("5. CRT Curvature & Vignette")]
        [Range(0f, 0.5f)] [SerializeField] private float screenCurvature = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float cornerVignette = 0.45f;

        [Header("6. Signal Interlacing & Jitter")]
        [Range(0f, 0.02f)] [SerializeField] private float verticalJitter = 0.003f;

        [Header("7. Surveillance CCTV Tint")]
        [SerializeField] private Color cctvTint = new Color(0.95f, 1.02f, 0.98f, 1.0f);
        [Range(0f, 1f)] [SerializeField] private float cctvTintIntensity = 0.15f;

        // Shader Property IDs
        private static readonly int PropMasterBlend = Shader.PropertyToID("_MasterBlend");
        private static readonly int PropScanlineIntensity = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int PropScanlineCount = Shader.PropertyToID("_ScanlineCount");
        private static readonly int PropScanlineScrollSpeed = Shader.PropertyToID("_ScanlineScrollSpeed");
        private static readonly int PropTrackingBarIntensity = Shader.PropertyToID("_TrackingBarIntensity");
        private static readonly int PropTrackingBarSpeed = Shader.PropertyToID("_TrackingBarSpeed");
        private static readonly int PropTrackingBarThickness = Shader.PropertyToID("_TrackingBarThickness");
        private static readonly int PropHorizontalGlitchStrength = Shader.PropertyToID("_HorizontalGlitchStrength");
        private static readonly int PropRGBShiftAmount = Shader.PropertyToID("_RGBShiftAmount");
        private static readonly int PropTapeNoiseIntensity = Shader.PropertyToID("_TapeNoiseIntensity");
        private static readonly int PropScreenCurvature = Shader.PropertyToID("_ScreenCurvature");
        private static readonly int PropCornerVignette = Shader.PropertyToID("_CornerVignette");
        private static readonly int PropVerticalJitter = Shader.PropertyToID("_VerticalJitter");
        private static readonly int PropCCTVTint = Shader.PropertyToID("_CCTVTint");
        private static readonly int PropCCTVTintIntensity = Shader.PropertyToID("_CCTVTintIntensity");

        // Runtime Transient Boosts
        private float _burstGlitchBoost = 0f;
        private Coroutine _glitchBurstRoutine;

        public Material VHSMaterial
        {
            get => vhsMaterial;
            set { vhsMaterial = value; UpdateMaterialProperties(); }
        }

        public bool EnableVHSEffect
        {
            get => enableVHSEffect;
            set { enableVHSEffect = value; UpdateMaterialProperties(); }
        }

        public float MasterBlend
        {
            get => masterBlend;
            set { masterBlend = Mathf.Clamp01(value); UpdateMaterialProperties(); }
        }

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance == null) Instance = this;
            }
            EnsureMaterial();
            UpdateMaterialProperties();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            EnsureMaterial();
            UpdateMaterialProperties();
        }

        private void OnValidate()
        {
            EnsureMaterial();
            UpdateMaterialProperties();
        }

        private void Update()
        {
            // Update in edit mode or when dynamic burst animation is running
            if (!Application.isPlaying || _burstGlitchBoost > 0f)
            {
                UpdateMaterialProperties();
            }
        }

        public void EnsureMaterial()
        {
            if (vhsMaterial == null)
            {
#if UNITY_EDITOR
                vhsMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_VHSMonitorEffect.mat");
#endif
            }
        }

        public void UpdateMaterialProperties()
        {
            if (vhsMaterial == null) return;

            float effectiveBlend = enableVHSEffect ? Mathf.Clamp01(masterBlend + _burstGlitchBoost * 0.35f) : 0f;
            float effectiveGlitch = Mathf.Clamp01(trackingBarIntensity + _burstGlitchBoost);
            float effectiveHoriz = Mathf.Clamp(horizontalGlitchStrength + _burstGlitchBoost * 0.08f, 0f, 0.25f);
            float effectiveNoise = Mathf.Clamp01(tapeNoiseIntensity + _burstGlitchBoost * 0.45f);
            float effectiveJitter = Mathf.Clamp(verticalJitter + _burstGlitchBoost * 0.015f, 0f, 0.05f);

            vhsMaterial.SetFloat(PropMasterBlend, effectiveBlend);
            vhsMaterial.SetFloat(PropScanlineIntensity, scanlineIntensity);
            vhsMaterial.SetFloat(PropScanlineCount, scanlineCount);
            vhsMaterial.SetFloat(PropScanlineScrollSpeed, scanlineScrollSpeed);

            vhsMaterial.SetFloat(PropTrackingBarIntensity, effectiveGlitch);
            vhsMaterial.SetFloat(PropTrackingBarSpeed, trackingBarSpeed);
            vhsMaterial.SetFloat(PropTrackingBarThickness, trackingBarThickness);
            vhsMaterial.SetFloat(PropHorizontalGlitchStrength, effectiveHoriz);

            vhsMaterial.SetFloat(PropRGBShiftAmount, rgbShiftAmount + _burstGlitchBoost * 0.015f);
            vhsMaterial.SetFloat(PropTapeNoiseIntensity, effectiveNoise);

            vhsMaterial.SetFloat(PropScreenCurvature, screenCurvature);
            vhsMaterial.SetFloat(PropCornerVignette, cornerVignette);

            vhsMaterial.SetFloat(PropVerticalJitter, effectiveJitter);
            vhsMaterial.SetColor(PropCCTVTint, cctvTint);
            vhsMaterial.SetFloat(PropCCTVTintIntensity, cctvTintIntensity);
        }

        /// <summary>
        /// Triggers a sudden, high-intensity analog VHS tracking/static glitch burst
        /// during red alarms, Eye beam shutoffs, or gravitational horizon collapse.
        /// </summary>
        public void TriggerGlitchBurst(float glitchBoost = 0.8f, float duration = 0.35f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            if (_glitchBurstRoutine != null)
            {
                StopCoroutine(_glitchBurstRoutine);
            }
            _glitchBurstRoutine = StartCoroutine(GlitchBurstRoutine(glitchBoost, duration));
        }

        private IEnumerator GlitchBurstRoutine(float glitchBoost, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Sharp stuttering decay curve
                float noiseFlutter = (Mathf.Sin(elapsed * 45f) * 0.5f + 0.5f);
                float decay = Mathf.Pow(1.0f - t, 1.8f) * (0.6f + 0.4f * noiseFlutter);
                _burstGlitchBoost = glitchBoost * decay;

                UpdateMaterialProperties();
                yield return null;
            }

            _burstGlitchBoost = 0f;
            UpdateMaterialProperties();
            _glitchBurstRoutine = null;
        }

        // Live Setters
        public void SetTrackingBarIntensity(float val) { trackingBarIntensity = val; UpdateMaterialProperties(); }
        public void SetScanlineIntensity(float val) { scanlineIntensity = val; UpdateMaterialProperties(); }
        public void SetRGBShift(float val) { rgbShiftAmount = val; UpdateMaterialProperties(); }
        public void SetCurvature(float val) { screenCurvature = val; UpdateMaterialProperties(); }
    }
}
