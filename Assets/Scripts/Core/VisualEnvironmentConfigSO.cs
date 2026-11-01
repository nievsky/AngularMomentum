using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OrbitalPanopticon.Core
{
    [CreateAssetMenu(fileName = "VisualEnvironmentConfig", menuName = "Orbital Panopticon/Visual Environment Config")]
    public class VisualEnvironmentConfigSO : ScriptableObject
    {
        [Header("=== 1. PROCEDURAL 3D SPACE SKYBOX ===")]
        [Header("Deep Space Gradient")]
        public Color spaceZenithColor = new Color(0.047f, 0.035f, 0.094f, 1.0f);
        public Color spaceHorizonColor = new Color(0.020f, 0.016f, 0.039f, 1.0f);
        public Color spaceNadirColor = new Color(0.001f, 0.001f, 0.002f, 1.0f);
        [Range(0.2f, 5.0f)] public float gradientExponent = 1.35f;

        [Header("Multi-Layered Starfield")]
        [Range(20f, 300f)] public float starDensity = 110f;
        [Range(0.002f, 0.06f)] public float starSize = 0.016f;
        [Range(0.1f, 5.0f)] public float starBrightness = 2.2f;
        [Range(0f, 1f)] public float starColorVariation = 0.85f;
        [Range(0f, 10f)] public float starTwinkleSpeed = 2.2f;
        [Range(0f, 1f)] public float starTwinkleAmount = 0.55f;

        [Header("Movable Milky Way Band")]
        public Vector3 milkyWayRotationAngles = new Vector3(32f, 45f, 18f);
        [Range(0.05f, 0.8f)] public float milkyWayWidth = 0.32f;
        [Range(0.5f, 6.0f)] public float milkyWayFalloff = 2.2f;
        [ColorUsage(true, true)] public Color milkyWayCoreHDRColor = new Color(0.65f, 0.52f, 0.95f, 1.0f);
        public Color milkyWayOuterColor = new Color(0.12f, 0.18f, 0.38f, 1.0f);
        [Range(0f, 1f)] public float dustLaneDarkness = 0.18f;
        [Range(1f, 6f)] public float milkyWayStarBoost = 3.5f;
        [Range(0.5f, 10f)] public float milkyWayNoiseScale = 2.8f;

        [Header("Cosmic Nebulae Clouds")]
        [Range(0f, 3.0f)] public float nebulaIntensity = 0.95f;
        [Range(0.5f, 8.0f)] public float nebulaScale = 2.0f;
        [ColorUsage(true, true)] public Color nebulaColorA = new Color(0.08f, 0.48f, 0.85f, 1.0f);
        [ColorUsage(true, true)] public Color nebulaColorB = new Color(0.75f, 0.15f, 0.65f, 1.0f);
        public Vector3 nebulaOffset = new Vector3(1.5f, 2.3f, 3.7f);

        [Header("=== 2. VHS / SURVEILLANCE CAMERA EFFECT ===")]
        public bool enableVHSEffect = true;
        [Range(0f, 1f)] public float masterBlend = 0.65f;
        [Range(0.5f, 1.0f)] public float overscanScale = 0.90f;

        [Header("Scanlines")]
        [Range(0f, 1f)] public float scanlineIntensity = 0.15f;
        [Range(100f, 1080f)] public float scanlineCount = 640f;
        [Range(-10f, 10f)] public float scanlineScrollSpeed = 0.8f;

        [Header("VHS Tracking Glitch")]
        [Range(0f, 1f)] public float trackingBarIntensity = 0.45f;
        [Range(0f, 2f)] public float trackingBarSpeed = 0.18f;
        [Range(0.01f, 0.35f)] public float trackingBarThickness = 0.08f;
        [Range(0f, 0.15f)] public float horizontalGlitchStrength = 0.035f;

        [Header("Color Shift, Noise & Optics")]
        [Range(0f, 0.05f)] public float rgbShiftAmount = 0.0055f;
        [Range(0f, 1f)] public float tapeNoiseIntensity = 0.04f;
        [Range(0f, 0.5f)] public float screenCurvature = 0.12f;
        [Range(0f, 1f)] public float cornerVignette = 0.45f;
        [Range(0f, 0.02f)] public float verticalJitter = 0.003f;
        public Color cctvTint = new Color(0.95f, 1.02f, 0.98f, 1.0f);
        [Range(0f, 1f)] public float cctvTintIntensity = 0.15f;

        [Header("=== 3. MASTER POST-PROCESSING (URP VOLUME) ===")]
        public TonemappingMode tonemappingMode = TonemappingMode.ACES;

        [Header("Bloom")]
        [Range(0f, 10f)] public float bloomIntensity = 1.25f;
        [Range(0f, 2f)] public float bloomThreshold = 1.1f;
        [Range(0f, 1f)] public float bloomScatter = 0.65f;
        public bool bloomHighQualityFiltering = true;

        [Header("Vignette")]
        [Range(0f, 1f)] public float vignetteIntensity = 0.28f;
        [Range(0f, 1f)] public float vignetteSmoothness = 0.4f;

        [Header("Chromatic Aberration")]
        [Range(0f, 1f)] public float chromaticAberrationIntensity = 0.08f;

        [Header("Lens Distortion (Fullscreen Overscan)")]
        [Range(-1f, 1f)] public float lensDistortionIntensity = -0.32f;
        [Range(0.5f, 2f)] public float lensDistortionScale = 1.15f;
        public bool autoCompensateDistortionScale = true;

        [Header("Color Adjustments")]
        [Range(-100f, 100f)] public float contrast = 22f;
        [Range(-5f, 5f)] public float postExposure = -0.15f;
        [Range(-100f, 100f)] public float saturation = 12f;
        public Color colorFilter = Color.white;

        [Header("Optional Cinematic Overrides")]
        public bool enableFilmGrain = false;
        [Range(0f, 1f)] public float filmGrainIntensity = 0.15f;
        public bool enableMotionBlur = false;
        [Range(0f, 1f)] public float motionBlurIntensity = 0.25f;
        public bool enableDepthOfField = false;

        public void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private void OnValidate()
        {
            MarkDirty();

            // Notify active scene controllers in Edit Mode and Play Mode
            var postControllers = UnityEngine.Object.FindObjectsByType<PostProcessingToolkitController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var post in postControllers)
            {
                if (post != null && (post.visualConfig == this || post.visualConfig == null))
                {
                    if (post.visualConfig == null) post.visualConfig = this;
                    post.LoadFromConfig();
                }
            }

            var skyControllers = UnityEngine.Object.FindObjectsByType<SpaceSkyboxController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var sky in skyControllers)
            {
                if (sky != null && (sky.visualConfig == this || sky.visualConfig == null))
                {
                    if (sky.visualConfig == null) sky.visualConfig = this;
                    sky.LoadFromConfig();
                }
            }

            var vhsControllers = UnityEngine.Object.FindObjectsByType<VHSCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var vhs in vhsControllers)
            {
                if (vhs != null && (vhs.visualConfig == this || vhs.visualConfig == null))
                {
                    if (vhs.visualConfig == null) vhs.visualConfig = this;
                    vhs.LoadFromConfig();
                }
            }
        }
    }
}
