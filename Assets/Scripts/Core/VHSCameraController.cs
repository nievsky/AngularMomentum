using System.Collections;
using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class VHSCameraController : MonoBehaviour
    {
        public static VHSCameraController Instance { get; private set; }

        [Header("Configuration & Material References")]
        [SerializeField] public VisualEnvironmentConfigSO visualConfig;
        [SerializeField] private Material vhsMaterial;

        [Header("Master VHS Controls")]
        [SerializeField] private bool enableVHSEffect = true;
        [Range(0f, 1f)] [SerializeField] private float masterBlend = 0.65f;
        [Range(0.5f, 1.0f)] [SerializeField] private float overscanScale = 0.90f;

        [Header("1. CRT Scanlines")]
        [Range(0f, 1f)] [SerializeField] private float scanlineIntensity = 0.15f;
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
        [Range(0f, 1f)] [SerializeField] private float tapeNoiseIntensity = 0.04f;

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
        private static readonly int PropOverscanScale = Shader.PropertyToID("_OverscanScale");
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
        private float _finaleInterference = 0f;
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

        public VisualEnvironmentConfigSO VisualConfig => visualConfig;

        public float OverscanScale
        {
            get => overscanScale;
            set { overscanScale = Mathf.Clamp(value, 0.5f, 1.0f); SaveToConfig(); UpdateMaterialProperties(); }
        }

        public void EnsureConfig()
        {
            if (visualConfig == null)
            {
                string sceneName = gameObject.scene.name;
                string configName = "VisualConfig_Gameplay";
                if (!string.IsNullOrEmpty(sceneName))
                {
                    if (sceneName.Contains("MainMenu")) configName = "VisualConfig_MainMenu";
                    else if (sceneName.Contains("Credits")) configName = "VisualConfig_Credits";
                }

#if UNITY_EDITOR
                visualConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualEnvironmentConfigSO>(
                    $"Assets/_Project/ScriptableObjects/{configName}.asset");
#endif
            }
        }

        public void LoadFromConfig()
        {
            EnsureConfig();
            if (visualConfig == null) return;

            enableVHSEffect = visualConfig.enableVHSEffect;
            masterBlend = visualConfig.masterBlend;
            overscanScale = visualConfig.overscanScale;

            scanlineIntensity = visualConfig.scanlineIntensity;
            scanlineCount = visualConfig.scanlineCount;
            scanlineScrollSpeed = visualConfig.scanlineScrollSpeed;

            trackingBarIntensity = visualConfig.trackingBarIntensity;
            trackingBarSpeed = visualConfig.trackingBarSpeed;
            trackingBarThickness = visualConfig.trackingBarThickness;
            horizontalGlitchStrength = visualConfig.horizontalGlitchStrength;

            rgbShiftAmount = visualConfig.rgbShiftAmount;
            tapeNoiseIntensity = visualConfig.tapeNoiseIntensity;
            screenCurvature = visualConfig.screenCurvature;
            cornerVignette = visualConfig.cornerVignette;
            verticalJitter = visualConfig.verticalJitter;
            cctvTint = visualConfig.cctvTint;
            cctvTintIntensity = visualConfig.cctvTintIntensity;

            EnsureMaterial();
            UpdateMaterialProperties();
        }

        public void SaveToConfig()
        {
            EnsureConfig();
            if (visualConfig == null) return;

            visualConfig.enableVHSEffect = enableVHSEffect;
            visualConfig.masterBlend = masterBlend;
            visualConfig.overscanScale = overscanScale;

            visualConfig.scanlineIntensity = scanlineIntensity;
            visualConfig.scanlineCount = scanlineCount;
            visualConfig.scanlineScrollSpeed = scanlineScrollSpeed;

            visualConfig.trackingBarIntensity = trackingBarIntensity;
            visualConfig.trackingBarSpeed = trackingBarSpeed;
            visualConfig.trackingBarThickness = trackingBarThickness;
            visualConfig.horizontalGlitchStrength = horizontalGlitchStrength;

            visualConfig.rgbShiftAmount = rgbShiftAmount;
            visualConfig.tapeNoiseIntensity = tapeNoiseIntensity;
            visualConfig.screenCurvature = screenCurvature;
            visualConfig.cornerVignette = cornerVignette;
            visualConfig.verticalJitter = verticalJitter;
            visualConfig.cctvTint = cctvTint;
            visualConfig.cctvTintIntensity = cctvTintIntensity;

            visualConfig.MarkDirty();
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            EnsureConfig();
            LoadFromConfig();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
#if UNITY_EDITOR
            if (visualConfig != null)
            {
                SaveToConfig();
                UnityEditor.EditorUtility.SetDirty(visualConfig);
                UnityEditor.AssetDatabase.SaveAssets();
            }
#endif
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            if (Instance == null) Instance = this;
            EnsureConfig();
            LoadFromConfig();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.BuildPipeline.isBuildingPlayer) return;
            EnsureConfig();
            SaveToConfig();
            UpdateMaterialProperties();
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying) return;

            if (_burstGlitchBoost > 0f || _finaleInterference > 0f)
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
                if (vhsMaterial == null)
                {
                    vhsMaterial = Resources.Load<Material>("M_VHSMonitorEffect");
                }
            }
        }

        public void UpdateMaterialProperties()
        {
            if (vhsMaterial == null) return;

            float effectiveBlend = enableVHSEffect ? Mathf.Clamp01(masterBlend + _burstGlitchBoost * 0.35f + _finaleInterference * 0.45f) : 0f;
            float effectiveGlitch = Mathf.Clamp01(trackingBarIntensity + _burstGlitchBoost + _finaleInterference * 0.95f);
            float effectiveHoriz = Mathf.Clamp(horizontalGlitchStrength + _burstGlitchBoost * 0.08f + _finaleInterference * 0.18f, 0f, 0.25f);
            float effectiveNoise = Mathf.Clamp01(tapeNoiseIntensity + _burstGlitchBoost * 0.45f + _finaleInterference * 0.95f);
            float effectiveJitter = Mathf.Clamp(verticalJitter + _burstGlitchBoost * 0.015f + _finaleInterference * 0.025f, 0f, 0.05f);
            float effectiveRGBShift = rgbShiftAmount + _burstGlitchBoost * 0.015f + _finaleInterference * 0.035f;

            vhsMaterial.SetFloat(PropMasterBlend, effectiveBlend);
            vhsMaterial.SetFloat(PropOverscanScale, overscanScale);
            vhsMaterial.SetFloat(PropScanlineIntensity, scanlineIntensity);
            vhsMaterial.SetFloat(PropScanlineCount, scanlineCount);
            vhsMaterial.SetFloat(PropScanlineScrollSpeed, scanlineScrollSpeed);

            vhsMaterial.SetFloat(PropTrackingBarIntensity, effectiveGlitch);
            vhsMaterial.SetFloat(PropTrackingBarSpeed, trackingBarSpeed);
            vhsMaterial.SetFloat(PropTrackingBarThickness, trackingBarThickness);
            vhsMaterial.SetFloat(PropHorizontalGlitchStrength, effectiveHoriz);

            vhsMaterial.SetFloat(PropRGBShiftAmount, effectiveRGBShift);
            vhsMaterial.SetFloat(PropTapeNoiseIntensity, effectiveNoise);

            vhsMaterial.SetFloat(PropScreenCurvature, screenCurvature);
            vhsMaterial.SetFloat(PropCornerVignette, cornerVignette);

            vhsMaterial.SetFloat(PropVerticalJitter, effectiveJitter);
            vhsMaterial.SetColor(PropCCTVTint, cctvTint);
            vhsMaterial.SetFloat(PropCCTVTintIntensity, cctvTintIntensity);
        }

        public void SetFinaleInterference(float progress01)
        {
            _finaleInterference = Mathf.Clamp01(progress01);
            UpdateMaterialProperties();
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
        public void SetTrackingBarIntensity(float val) { trackingBarIntensity = val; SaveToConfig(); UpdateMaterialProperties(); }
        public void SetScanlineIntensity(float val) { scanlineIntensity = val; SaveToConfig(); UpdateMaterialProperties(); }
        public void SetRGBShift(float val) { rgbShiftAmount = val; SaveToConfig(); UpdateMaterialProperties(); }
        public void SetCurvature(float val) { screenCurvature = val; SaveToConfig(); UpdateMaterialProperties(); }
    }
}
