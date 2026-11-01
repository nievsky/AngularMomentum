using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class PostProcessingToolkitController : MonoBehaviour
    {
        public static PostProcessingToolkitController Instance { get; private set; }

        [Header("Configuration & Volume Target")]
        [SerializeField] public VisualEnvironmentConfigSO visualConfig;
        [SerializeField] private Volume postProcessVolume;

        [Header("1. Tonemapping (ACES)")]
        [SerializeField] private TonemappingMode tonemappingMode = TonemappingMode.ACES;

        [Header("2. Bloom")]
        [Range(0f, 10f)] [SerializeField] private float bloomIntensity = 1.25f;
        [Range(0f, 2f)] [SerializeField] private float bloomThreshold = 1.1f;
        [Range(0f, 1f)] [SerializeField] private float bloomScatter = 0.65f;
        [SerializeField] private bool bloomHighQualityFiltering = true;

        [Header("3. Vignette")]
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensity = 0.28f;
        [Range(0f, 1f)] [SerializeField] private float vignetteSmoothness = 0.4f;

        [Header("4. Chromatic Aberration")]
        [Range(0f, 1f)] [SerializeField] private float chromaticAberrationIntensity = 0.08f;

        [Header("5. Lens Distortion (Fullscreen Overscan)")]
        [Range(-1f, 1f)] [SerializeField] private float lensDistortionIntensity = -0.32f;
        [Range(0.5f, 2f)] [SerializeField] private float lensDistortionScale = 1.15f;
        [SerializeField] private bool autoCompensateDistortionScale = true;

        [Header("6. Color Adjustments")]
        [Range(-100f, 100f)] [SerializeField] private float contrast = 22f;
        [Range(-5f, 5f)] [SerializeField] private float postExposure = -0.15f;
        [Range(-100f, 100f)] [SerializeField] private float saturation = 12f;
        [SerializeField] private Color colorFilter = Color.white;

        [Header("7. Optional Cinematic Overrides")]
        [SerializeField] private bool enableFilmGrain = false;
        [Range(0f, 1f)] [SerializeField] private float filmGrainIntensity = 0.15f;
        [SerializeField] private bool enableMotionBlur = false;
        [Range(0f, 1f)] [SerializeField] private float motionBlurIntensity = 0.25f;
        [SerializeField] private bool enableDepthOfField = false;

        // Cached Volume Components
        private Tonemapping _tonemapping;
        private Bloom _bloom;
        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;
        private LensDistortion _lensDistortion;
        private ColorAdjustments _colorAdjustments;
        private FilmGrain _filmGrain;
        private MotionBlur _motionBlur;
        private DepthOfField _depthOfField;

        // Runtime Pulse Offsets
        private float _pulseBloomBoost = 0f;
        private float _pulseChromaticBoost = 0f;
        private float _pulseLensDistortionBoost = 0f;
        private float _pulseMotionBlurBoost = 0f;
        private Coroutine _pulseRoutine;

        public Volume PostProcessVolume => postProcessVolume;
        public VisualEnvironmentConfigSO VisualConfig => visualConfig;

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

            tonemappingMode = visualConfig.tonemappingMode;

            bloomIntensity = visualConfig.bloomIntensity;
            bloomThreshold = visualConfig.bloomThreshold;
            bloomScatter = visualConfig.bloomScatter;
            bloomHighQualityFiltering = visualConfig.bloomHighQualityFiltering;

            vignetteIntensity = visualConfig.vignetteIntensity;
            vignetteSmoothness = visualConfig.vignetteSmoothness;

            chromaticAberrationIntensity = visualConfig.chromaticAberrationIntensity;

            lensDistortionIntensity = visualConfig.lensDistortionIntensity;
            lensDistortionScale = visualConfig.lensDistortionScale;
            autoCompensateDistortionScale = visualConfig.autoCompensateDistortionScale;

            contrast = visualConfig.contrast;
            postExposure = visualConfig.postExposure;
            saturation = visualConfig.saturation;
            colorFilter = visualConfig.colorFilter;

            enableFilmGrain = visualConfig.enableFilmGrain;
            filmGrainIntensity = visualConfig.filmGrainIntensity;
            enableMotionBlur = visualConfig.enableMotionBlur;
            motionBlurIntensity = visualConfig.motionBlurIntensity;
            enableDepthOfField = visualConfig.enableDepthOfField;

            EnsureVolumeAndOverrides();
            ApplySettingsToVolume();
        }

        public void SaveToConfig()
        {
            EnsureConfig();
            if (visualConfig == null) return;

            visualConfig.tonemappingMode = tonemappingMode;

            visualConfig.bloomIntensity = bloomIntensity;
            visualConfig.bloomThreshold = bloomThreshold;
            visualConfig.bloomScatter = bloomScatter;
            visualConfig.bloomHighQualityFiltering = bloomHighQualityFiltering;

            visualConfig.vignetteIntensity = vignetteIntensity;
            visualConfig.vignetteSmoothness = vignetteSmoothness;

            visualConfig.chromaticAberrationIntensity = chromaticAberrationIntensity;

            visualConfig.lensDistortionIntensity = lensDistortionIntensity;
            visualConfig.lensDistortionScale = lensDistortionScale;
            visualConfig.autoCompensateDistortionScale = autoCompensateDistortionScale;

            visualConfig.contrast = contrast;
            visualConfig.postExposure = postExposure;
            visualConfig.saturation = saturation;
            visualConfig.colorFilter = colorFilter;

            visualConfig.enableFilmGrain = enableFilmGrain;
            visualConfig.filmGrainIntensity = filmGrainIntensity;
            visualConfig.enableMotionBlur = enableMotionBlur;
            visualConfig.motionBlurIntensity = motionBlurIntensity;
            visualConfig.enableDepthOfField = enableDepthOfField;

            visualConfig.MarkDirty();
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            EnsureConfig();
            EnsureCameraSettings();
            EnsureVolumeAndOverrides();
            LoadFromConfig();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Instance = this;
            EnsureConfig();
            EnsureCameraSettings();
            EnsureVolumeAndOverrides();
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

        private void OnDisable()
        {
#if UNITY_EDITOR
            if (visualConfig != null)
            {
                SaveToConfig();
                UnityEditor.EditorUtility.SetDirty(visualConfig);
            }
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.BuildPipeline.isBuildingPlayer) return;
            EnsureConfig();
            SaveToConfig();
            EnsureVolumeAndOverrides();
            ApplySettingsToVolume();
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying) return;

            // 1. Maintain camera post-processing requirements
            EnsureCameraSettings();

            // 2. Synchronize sliders between controller and visualConfig
            SyncConfigAndController();

            // 3. Apply overrides every frame to guarantee instantaneous visual feedback in Play Mode
            ApplySettingsToVolume();
        }

        private void EnsureCameraSettings()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Camera.current;
            if (cam != null)
            {
                UniversalAdditionalCameraData camData = cam.GetUniversalAdditionalCameraData();
                if (camData != null)
                {
                    if (!camData.renderPostProcessing) camData.renderPostProcessing = true;
                    if (camData.volumeLayerMask != -1) camData.volumeLayerMask = -1; // Everything
                }
            }

            var urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null) urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        }

        private void SyncConfigAndController()
        {
            if (visualConfig == null) return;

            bool controllerChanged =
                tonemappingMode != visualConfig.tonemappingMode ||
                !Mathf.Approximately(bloomIntensity, visualConfig.bloomIntensity) ||
                !Mathf.Approximately(bloomThreshold, visualConfig.bloomThreshold) ||
                !Mathf.Approximately(bloomScatter, visualConfig.bloomScatter) ||
                bloomHighQualityFiltering != visualConfig.bloomHighQualityFiltering ||
                !Mathf.Approximately(vignetteIntensity, visualConfig.vignetteIntensity) ||
                !Mathf.Approximately(vignetteSmoothness, visualConfig.vignetteSmoothness) ||
                !Mathf.Approximately(chromaticAberrationIntensity, visualConfig.chromaticAberrationIntensity) ||
                !Mathf.Approximately(lensDistortionIntensity, visualConfig.lensDistortionIntensity) ||
                !Mathf.Approximately(lensDistortionScale, visualConfig.lensDistortionScale) ||
                autoCompensateDistortionScale != visualConfig.autoCompensateDistortionScale ||
                !Mathf.Approximately(contrast, visualConfig.contrast) ||
                !Mathf.Approximately(postExposure, visualConfig.postExposure) ||
                !Mathf.Approximately(saturation, visualConfig.saturation) ||
                colorFilter != visualConfig.colorFilter ||
                enableFilmGrain != visualConfig.enableFilmGrain ||
                !Mathf.Approximately(filmGrainIntensity, visualConfig.filmGrainIntensity) ||
                enableMotionBlur != visualConfig.enableMotionBlur ||
                !Mathf.Approximately(motionBlurIntensity, visualConfig.motionBlurIntensity) ||
                enableDepthOfField != visualConfig.enableDepthOfField;

            if (controllerChanged)
            {
                SaveToConfig();
            }
        }

        public void EnsureVolumeAndOverrides()
        {
            if (postProcessVolume == null)
            {
                postProcessVolume = GetComponent<Volume>();
            }
            if (postProcessVolume == null)
            {
                postProcessVolume = FindAnyObjectByType<Volume>();
            }
            if (postProcessVolume == null) return;

            // Enforce volume requirements
            postProcessVolume.isGlobal = true;
            postProcessVolume.weight = 1.0f;
            postProcessVolume.priority = 100f;

            if (postProcessVolume.sharedProfile == null)
            {
#if UNITY_EDITOR
                postProcessVolume.sharedProfile = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/DefaultVolumeProfile.asset");
#endif
            }

            if (Application.isPlaying)
            {
                if (postProcessVolume.profile == null || postProcessVolume.profile == postProcessVolume.sharedProfile)
                {
                    postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
                }
            }

            VolumeProfile profile = Application.isPlaying ? postProcessVolume.profile : postProcessVolume.sharedProfile;
            if (profile == null) return;

            if (!profile.TryGet(out _tonemapping)) _tonemapping = profile.Add<Tonemapping>(true);
            if (!profile.TryGet(out _bloom)) _bloom = profile.Add<Bloom>(true);
            if (!profile.TryGet(out _vignette)) _vignette = profile.Add<Vignette>(true);
            if (!profile.TryGet(out _chromaticAberration)) _chromaticAberration = profile.Add<ChromaticAberration>(true);
            if (!profile.TryGet(out _lensDistortion)) _lensDistortion = profile.Add<LensDistortion>(true);
            if (!profile.TryGet(out _colorAdjustments)) _colorAdjustments = profile.Add<ColorAdjustments>(true);
            if (!profile.TryGet(out _filmGrain)) _filmGrain = profile.Add<FilmGrain>(true);
            if (!profile.TryGet(out _motionBlur)) _motionBlur = profile.Add<MotionBlur>(true);
            if (!profile.TryGet(out _depthOfField)) _depthOfField = profile.Add<DepthOfField>(true);
        }

        public void ApplySettingsToVolume()
        {
            if (postProcessVolume == null) return;

            if (_tonemapping != null)
            {
                _tonemapping.active = true;
                _tonemapping.mode.Override(tonemappingMode);
            }

            if (_bloom != null)
            {
                _bloom.active = true;
                _bloom.intensity.Override(Mathf.Max(0f, bloomIntensity + _pulseBloomBoost));
                _bloom.threshold.Override(bloomThreshold);
                _bloom.scatter.Override(bloomScatter);
                _bloom.highQualityFiltering.Override(bloomHighQualityFiltering);
            }

            if (_vignette != null)
            {
                _vignette.active = true;
                _vignette.intensity.Override(vignetteIntensity);
                _vignette.smoothness.Override(vignetteSmoothness);
            }

            if (_chromaticAberration != null)
            {
                _chromaticAberration.active = true;
                _chromaticAberration.intensity.Override(Mathf.Clamp01(chromaticAberrationIntensity + _pulseChromaticBoost));
            }

            if (_lensDistortion != null)
            {
                _lensDistortion.active = true;
                float totalIntensity = Mathf.Clamp(lensDistortionIntensity + _pulseLensDistortionBoost, -1f, 1f);
                _lensDistortion.intensity.Override(totalIntensity);

                float effectiveScale = autoCompensateDistortionScale
                    ? Mathf.Max(1.0f, 1.0f + Mathf.Abs(totalIntensity) * 0.45f)
                    : lensDistortionScale;
                _lensDistortion.scale.Override(effectiveScale);
            }

            if (_colorAdjustments != null)
            {
                _colorAdjustments.active = true;
                _colorAdjustments.contrast.Override(contrast);
                _colorAdjustments.postExposure.Override(postExposure);
                _colorAdjustments.saturation.Override(saturation);
                _colorAdjustments.colorFilter.Override(colorFilter);
            }

            if (_filmGrain != null)
            {
                _filmGrain.active = enableFilmGrain;
                _filmGrain.intensity.Override(filmGrainIntensity);
            }

            if (_motionBlur != null)
            {
                _motionBlur.active = enableMotionBlur || (_pulseMotionBlurBoost > 0.001f);
                _motionBlur.intensity.Override(Mathf.Clamp01(motionBlurIntensity + _pulseMotionBlurBoost));
            }

            if (_depthOfField != null)
            {
                _depthOfField.active = enableDepthOfField;
            }
        }

        public void TriggerImpactPulse(float bloomBoost = 1.5f, float chromaticBoost = 0.35f, float duration = 0.4f)
        {
            TriggerImpactPulse(bloomBoost, chromaticBoost, 0f, duration, 0f);
        }

        public void TriggerImpactPulse(float bloomBoost, float chromaticBoost, float lensDistortionBoost, float duration, float motionBlurBoost = 0.5f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }
            _pulseRoutine = StartCoroutine(ImpactPulseRoutine(bloomBoost, chromaticBoost, lensDistortionBoost, duration, motionBlurBoost));
        }

        private IEnumerator ImpactPulseRoutine(float bloomBoost, float chromaticBoost, float lensDistortionBoost, float duration, float motionBlurBoost)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                float decay = Mathf.Pow(1.0f - t, 2.2f);
                _pulseBloomBoost = bloomBoost * decay;
                _pulseChromaticBoost = chromaticBoost * decay;
                _pulseLensDistortionBoost = lensDistortionBoost * decay;
                _pulseMotionBlurBoost = motionBlurBoost * decay;

                ApplySettingsToVolume();
                yield return null;
            }

            _pulseBloomBoost = 0f;
            _pulseChromaticBoost = 0f;
            _pulseLensDistortionBoost = 0f;
            _pulseMotionBlurBoost = 0f;
            ApplySettingsToVolume();
            _pulseRoutine = null;
        }

        public void SetBloomIntensity(float val) { bloomIntensity = val; SaveToConfig(); ApplySettingsToVolume(); }
        public void SetChromaticAberration(float val) { chromaticAberrationIntensity = val; SaveToConfig(); ApplySettingsToVolume(); }
        public void SetVignetteIntensity(float val) { vignetteIntensity = val; SaveToConfig(); ApplySettingsToVolume(); }
        public void SetLensDistortion(float val) { lensDistortionIntensity = val; SaveToConfig(); ApplySettingsToVolume(); }
        public void SetLensDistortionScale(float val) { lensDistortionScale = val; SaveToConfig(); ApplySettingsToVolume(); }
    }
}
