using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SpaceSkyboxController : MonoBehaviour
    {
        public static SpaceSkyboxController Instance { get; private set; }

        [Header("Skybox Material Reference")]
        [SerializeField] private Material skyboxMaterial;

        [Header("1. Deep Space Gradient")]
        [SerializeField] private Color spaceZenithColor = new Color(0.012f, 0.018f, 0.042f, 1.0f);
        [SerializeField] private Color spaceHorizonColor = new Color(0.004f, 0.007f, 0.016f, 1.0f);
        [SerializeField] private Color spaceNadirColor = new Color(0.001f, 0.002f, 0.006f, 1.0f);
        [Range(0.2f, 5.0f)] [SerializeField] private float gradientExponent = 1.35f;

        [Header("2. Multi-Layered Starfield")]
        [Range(20f, 300f)] [SerializeField] private float starDensity = 110f;
        [Range(0.002f, 0.06f)] [SerializeField] private float starSize = 0.016f;
        [Range(0.1f, 5.0f)] [SerializeField] private float starBrightness = 2.2f;
        [Range(0f, 1f)] [SerializeField] private float starColorVariation = 0.85f;
        [Range(0f, 10f)] [SerializeField] private float starTwinkleSpeed = 2.2f;
        [Range(0f, 1f)] [SerializeField] private float starTwinkleAmount = 0.55f;

        [Header("3. Movable & Rotatable Milky Way Band")]
        [Tooltip("Euler angles (degrees) to rotate the Milky Way equator band across the sky.")]
        [SerializeField] private Vector3 milkyWayRotationAngles = new Vector3(32f, 45f, 18f);
        [Range(0.05f, 0.8f)] [SerializeField] private float milkyWayWidth = 0.32f;
        [Range(0.5f, 6.0f)] [SerializeField] private float milkyWayFalloff = 2.2f;
        [ColorUsage(true, true)] [SerializeField] private Color milkyWayCoreHDRColor = new Color(0.65f, 0.52f, 0.95f, 1.0f);
        [SerializeField] private Color milkyWayOuterColor = new Color(0.12f, 0.18f, 0.38f, 1.0f);
        [Range(0f, 1f)] [SerializeField] private float dustLaneDarkness = 0.18f;
        [Range(1f, 6f)] [SerializeField] private float milkyWayStarBoost = 3.5f;
        [Range(0.5f, 10f)] [SerializeField] private float milkyWayNoiseScale = 2.8f;

        [Header("4. Cosmic Nebulae Clouds")]
        [Range(0f, 3.0f)] [SerializeField] private float nebulaIntensity = 0.95f;
        [Range(0.5f, 8.0f)] [SerializeField] private float nebulaScale = 2.0f;
        [ColorUsage(true, true)] [SerializeField] private Color nebulaColorA = new Color(0.08f, 0.48f, 0.85f, 1.0f);
        [ColorUsage(true, true)] [SerializeField] private Color nebulaColorB = new Color(0.75f, 0.15f, 0.65f, 1.0f);
        [SerializeField] private Vector3 nebulaOffset = new Vector3(1.5f, 2.3f, 3.7f);

        // Shader Property IDs
        private static readonly int PropSpaceZenithColor = Shader.PropertyToID("_SpaceZenithColor");
        private static readonly int PropSpaceHorizonColor = Shader.PropertyToID("_SpaceHorizonColor");
        private static readonly int PropSpaceNadirColor = Shader.PropertyToID("_SpaceNadirColor");
        private static readonly int PropGradientExponent = Shader.PropertyToID("_GradientExponent");

        private static readonly int PropStarDensity = Shader.PropertyToID("_StarDensity");
        private static readonly int PropStarSize = Shader.PropertyToID("_StarSize");
        private static readonly int PropStarBrightness = Shader.PropertyToID("_StarBrightness");
        private static readonly int PropStarColorVariation = Shader.PropertyToID("_StarColorVariation");
        private static readonly int PropStarTwinkleSpeed = Shader.PropertyToID("_StarTwinkleSpeed");
        private static readonly int PropStarTwinkleAmount = Shader.PropertyToID("_StarTwinkleAmount");

        private static readonly int PropMilkyWayWidth = Shader.PropertyToID("_MilkyWayWidth");
        private static readonly int PropMilkyWayFalloff = Shader.PropertyToID("_MilkyWayFalloff");
        private static readonly int PropMilkyWayCoreHDRColor = Shader.PropertyToID("_MilkyWayCoreHDRColor");
        private static readonly int PropMilkyWayOuterColor = Shader.PropertyToID("_MilkyWayOuterColor");
        private static readonly int PropDustLaneDarkness = Shader.PropertyToID("_DustLaneDarkness");
        private static readonly int PropMilkyWayStarBoost = Shader.PropertyToID("_MilkyWayStarBoost");
        private static readonly int PropMilkyWayNoiseScale = Shader.PropertyToID("_MilkyWayNoiseScale");

        private static readonly int PropNebulaIntensity = Shader.PropertyToID("_NebulaIntensity");
        private static readonly int PropNebulaScale = Shader.PropertyToID("_NebulaScale");
        private static readonly int PropNebulaColorA = Shader.PropertyToID("_NebulaColorA");
        private static readonly int PropNebulaColorB = Shader.PropertyToID("_NebulaColorB");
        private static readonly int PropNebulaOffset = Shader.PropertyToID("_NebulaOffset");

        private static readonly int PropMilkyWayRotationEuler = Shader.PropertyToID("_MilkyWayRotationEuler");
        private static readonly int PropMilkyWayRotationMatrix = Shader.PropertyToID("_MilkyWayRotationMatrix");

        public Material SkyboxMaterial
        {
            get => skyboxMaterial;
            set
            {
                skyboxMaterial = value;
                UpdateMaterialProperties();
            }
        }

        public Vector3 MilkyWayRotationAngles
        {
            get => milkyWayRotationAngles;
            set
            {
                milkyWayRotationAngles = value;
                UpdateMaterialProperties();
            }
        }

        public float MilkyWayWidth
        {
            get => milkyWayWidth;
            set
            {
                milkyWayWidth = Mathf.Clamp(value, 0.05f, 0.8f);
                UpdateMaterialProperties();
            }
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
            // Sync in Edit Mode so changes reflect immediately in Scene & Game views
            if (!Application.isPlaying)
            {
                UpdateMaterialProperties();
            }
        }

        public void EnsureMaterial()
        {
            if (skyboxMaterial == null)
            {
                skyboxMaterial = RenderSettings.skybox;
            }

            if (skyboxMaterial == null)
            {
#if UNITY_EDITOR
                skyboxMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ProceduralSpaceSkybox.mat");
#endif
            }

            if (skyboxMaterial != null && RenderSettings.skybox != skyboxMaterial)
            {
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        public void UpdateMaterialProperties()
        {
            if (skyboxMaterial == null) return;

            // 1. Gradient Colors
            skyboxMaterial.SetColor(PropSpaceZenithColor, spaceZenithColor);
            skyboxMaterial.SetColor(PropSpaceHorizonColor, spaceHorizonColor);
            skyboxMaterial.SetColor(PropSpaceNadirColor, spaceNadirColor);
            skyboxMaterial.SetFloat(PropGradientExponent, gradientExponent);

            // 2. Starfield
            skyboxMaterial.SetFloat(PropStarDensity, starDensity);
            skyboxMaterial.SetFloat(PropStarSize, starSize);
            skyboxMaterial.SetFloat(PropStarBrightness, starBrightness);
            skyboxMaterial.SetFloat(PropStarColorVariation, starColorVariation);
            skyboxMaterial.SetFloat(PropStarTwinkleSpeed, starTwinkleSpeed);
            skyboxMaterial.SetFloat(PropStarTwinkleAmount, starTwinkleAmount);

            // 3. Milky Way Band
            skyboxMaterial.SetFloat(PropMilkyWayWidth, milkyWayWidth);
            skyboxMaterial.SetFloat(PropMilkyWayFalloff, milkyWayFalloff);
            skyboxMaterial.SetColor(PropMilkyWayCoreHDRColor, milkyWayCoreHDRColor);
            skyboxMaterial.SetColor(PropMilkyWayOuterColor, milkyWayOuterColor);
            skyboxMaterial.SetFloat(PropDustLaneDarkness, dustLaneDarkness);
            skyboxMaterial.SetFloat(PropMilkyWayStarBoost, milkyWayStarBoost);
            skyboxMaterial.SetFloat(PropMilkyWayNoiseScale, milkyWayNoiseScale);

            // 4. Nebulae
            skyboxMaterial.SetFloat(PropNebulaIntensity, nebulaIntensity);
            skyboxMaterial.SetFloat(PropNebulaScale, nebulaScale);
            skyboxMaterial.SetColor(PropNebulaColorA, nebulaColorA);
            skyboxMaterial.SetColor(PropNebulaColorB, nebulaColorB);
            skyboxMaterial.SetVector(PropNebulaOffset, new Vector4(nebulaOffset.x, nebulaOffset.y, nebulaOffset.z, 0f));

            // 5. Rotation Matrix & Euler Vector
            skyboxMaterial.SetVector(PropMilkyWayRotationEuler, new Vector4(milkyWayRotationAngles.x, milkyWayRotationAngles.y, milkyWayRotationAngles.z, 0f));
            Matrix4x4 rotMatrix = Matrix4x4.Rotate(Quaternion.Euler(milkyWayRotationAngles));
            skyboxMaterial.SetMatrix(PropMilkyWayRotationMatrix, rotMatrix);

            if (RenderSettings.skybox != skyboxMaterial)
            {
                RenderSettings.skybox = skyboxMaterial;
            }
        }

        // Live Controls
        public void SetMilkyWayRotation(Vector3 angles)
        {
            milkyWayRotationAngles = angles;
            UpdateMaterialProperties();
        }

        public void SetStarDensity(float density)
        {
            starDensity = density;
            UpdateMaterialProperties();
        }

        public void SetNebulaIntensity(float intensity)
        {
            nebulaIntensity = intensity;
            UpdateMaterialProperties();
        }
    }
}
