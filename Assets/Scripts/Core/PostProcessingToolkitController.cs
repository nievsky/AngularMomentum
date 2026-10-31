using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OrbitalPanopticon.Core
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class PostProcessingToolkitController : MonoBehaviour
    {
        public static PostProcessingToolkitController Instance { get; private set; }

        [Header("Volume Target")]
        [SerializeField] private Volume postProcessVolume;

        [Header("1. Tonemapping (ACES)")]
        [SerializeField] private TonemappingMode tonemappingMode = TonemappingMode.ACES;

        [Header("2. Bloom")]
        [Range(0f, 10f)] [SerializeField] private float bloomIntensity = 1.6f;
        [Range(0f, 2f)] [SerializeField] private float bloomThreshold = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float bloomScatter = 0.65f;
        [SerializeField] private bool bloomHighQualityFiltering = true;

        [Header("3. Vignette")]
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensity = 0.28f;
        [Range(0f, 1f)] [SerializeField] private float vignetteSmoothness = 0.4f;

        [Header("4. Chromatic Aberration")]
        [Range(0f, 1f)] [SerializeField] private float chromaticAberrationIntensity = 0.08f;

        [Header("5. Lens Distortion")]
        [Range(-1f, 1f)] [SerializeField] private float lensDistortionIntensity = -0.12f;

        [Header("6. Color Adjustments")]
        [Range(-100f, 100f)] [SerializeField] private float contrast = 12f;
        [Range(-5f, 5f)] [SerializeField] private float postExposure = 0.15f;
        [Range(-100f, 100f)] [SerializeField] private float saturation = 8f;
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
        private Coroutine _pulseRoutine;

        public Volume PostProcessVolume => postProcessVolume;

        private void Awake()
        {
            if (Application.isPlaying)
            {
                if (Instance == null)
                {
                    Instance = this;
                }
            }
            EnsureVolumeAndOverrides();
            ApplySettingsToVolume();
        }

        private void OnEnable()
        {
            if (Instance == null) Instance = this;
            EnsureVolumeAndOverrides();
            ApplySettingsToVolume();
        }

        private void OnValidate()
        {
            EnsureVolumeAndOverrides();
            ApplySettingsToVolume();
        }

        private void Update()
        {
            // Keep volume updated in editor and runtime
            if (!Application.isPlaying || _pulseBloomBoost > 0f || _pulseChromaticBoost > 0f)
            {
                ApplySettingsToVolume();
            }
        }

        public void EnsureVolumeAndOverrides()
        {
            if (postProcessVolume == null)
            {
                postProcessVolume = GetComponent<Volume>();
            }
            if (postProcessVolume == null) return;

            VolumeProfile profile = postProcessVolume.sharedProfile;
            if (profile == null) profile = postProcessVolume.profile;
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
                _tonemapping.mode.overrideState = true;
                _tonemapping.mode.value = tonemappingMode;
            }

            if (_bloom != null)
            {
                _bloom.intensity.overrideState = true;
                _bloom.intensity.value = Mathf.Max(0f, bloomIntensity + _pulseBloomBoost);

                _bloom.threshold.overrideState = true;
                _bloom.threshold.value = bloomThreshold;

                _bloom.scatter.overrideState = true;
                _bloom.scatter.value = bloomScatter;

                _bloom.highQualityFiltering.overrideState = true;
                _bloom.highQualityFiltering.value = bloomHighQualityFiltering;
            }

            if (_vignette != null)
            {
                _vignette.intensity.overrideState = true;
                _vignette.intensity.value = vignetteIntensity;

                _vignette.smoothness.overrideState = true;
                _vignette.smoothness.value = vignetteSmoothness;
            }

            if (_chromaticAberration != null)
            {
                _chromaticAberration.intensity.overrideState = true;
                _chromaticAberration.intensity.value = Mathf.Clamp01(chromaticAberrationIntensity + _pulseChromaticBoost);
            }

            if (_lensDistortion != null)
            {
                _lensDistortion.intensity.overrideState = true;
                _lensDistortion.intensity.value = lensDistortionIntensity;
            }

            if (_colorAdjustments != null)
            {
                _colorAdjustments.contrast.overrideState = true;
                _colorAdjustments.contrast.value = contrast;

                _colorAdjustments.postExposure.overrideState = true;
                _colorAdjustments.postExposure.value = postExposure;

                _colorAdjustments.saturation.overrideState = true;
                _colorAdjustments.saturation.value = saturation;

                _colorAdjustments.colorFilter.overrideState = true;
                _colorAdjustments.colorFilter.value = colorFilter;
            }

            if (_filmGrain != null)
            {
                _filmGrain.active = enableFilmGrain;
                _filmGrain.intensity.overrideState = true;
                _filmGrain.intensity.value = filmGrainIntensity;
            }

            if (_motionBlur != null)
            {
                _motionBlur.active = enableMotionBlur;
                _motionBlur.intensity.overrideState = true;
                _motionBlur.intensity.value = motionBlurIntensity;
            }

            if (_depthOfField != null)
            {
                _depthOfField.active = enableDepthOfField;
            }
        }

        /// <summary>
        /// Triggers a dynamic high-impact pulse (Bloom flash + Chromatic distortion)
        /// for dramatic gameplay feedback during punt launches, containment lock breaks, or stage shifts.
        /// </summary>
        public void TriggerImpactPulse(float bloomBoost = 1.5f, float chromaticBoost = 0.35f, float duration = 0.4f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;

            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
            }
            _pulseRoutine = StartCoroutine(ImpactPulseRoutine(bloomBoost, chromaticBoost, duration));
        }

        private IEnumerator ImpactPulseRoutine(float bloomBoost, float chromaticBoost, float duration)
        {
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Sharp attack, smooth exponential decay
                float decay = Mathf.Pow(1.0f - t, 2.2f);
                _pulseBloomBoost = bloomBoost * decay;
                _pulseChromaticBoost = chromaticBoost * decay;

                ApplySettingsToVolume();
                yield return null;
            }

            _pulseBloomBoost = 0f;
            _pulseChromaticBoost = 0f;
            ApplySettingsToVolume();
            _pulseRoutine = null;
        }

        // Live Setters for External Controllers
        public void SetBloomIntensity(float val) { bloomIntensity = val; ApplySettingsToVolume(); }
        public void SetChromaticAberration(float val) { chromaticAberrationIntensity = val; ApplySettingsToVolume(); }
        public void SetVignetteIntensity(float val) { vignetteIntensity = val; ApplySettingsToVolume(); }
        public void SetLensDistortion(float val) { lensDistortionIntensity = val; ApplySettingsToVolume(); }
    }
}
