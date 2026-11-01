using System.Collections.Generic;
using UnityEngine;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Gameplay
{
    [System.Serializable]
    public struct CustomScrapEntry
    {
        [Tooltip("Drag your 3D model, FBX, or prefab (with materials already assigned) here.")]
        public GameObject modelOrPrefab;

        [Range(1, 4), Tooltip("Scrap Tier (1: Bolt, 2: Satellite, 3: Container/Fridge, 4: Relic/Anomaly)")]
        public int scrapTier;

        [Range(0.1f, 5.0f), Tooltip("Fine-tuning scale slider for this model")]
        public float customScaleMultiplier;

        public CustomScrapEntry(GameObject prefab, int tier, float scale = 1.0f)
        {
            modelOrPrefab = prefab;
            scrapTier = Mathf.Clamp(tier, 1, 4);
            customScaleMultiplier = scale <= 0.001f ? 1.0f : scale;
        }
    }

    /// <summary>
    /// Frictionless drag-and-drop catalog for custom 3D scrap models.
    /// Artists/designers drag raw model prefabs or FBXs into slots, pick a Tier (1-4),
    /// and the game automatically builds physics roots, colliders, gravity bodies,
    /// and molten lava shaders at runtime, falling back to primitive prefabs if empty.
    /// </summary>
    [CreateAssetMenu(fileName = "ScrapAssetCatalog", menuName = "Orbital Panopticon/Scrap Asset Catalog")]
    public class ScrapAssetCatalogSO : ScriptableObject
    {
        private static ScrapAssetCatalogSO _instance;

        public static ScrapAssetCatalogSO Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<ScrapAssetCatalogSO>("ScrapAssetCatalog");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<ScrapAssetCatalogSO>(
                            "Assets/_Project/ScriptableObjects/ScrapAssetCatalog.asset");
                    }
#endif
                }
                return _instance;
            }
            set => _instance = value;
        }

        [Header("Drag 3D Prefabs/Models Here & Pick Tier (1-4)")]
        [Tooltip("Add custom 3D models or prefabs here. When scrap of a matching tier spawns, one is chosen at random.")]
        [SerializeField] private List<CustomScrapEntry> customScrapAssets = new List<CustomScrapEntry>();
        public List<CustomScrapEntry> CustomScrapAssets => customScrapAssets;

        [Header("Default Primitive Fallbacks (Tiers 1-4)")]
        [Tooltip("Used when no custom scrap assets are defined for a tier.")]
        [SerializeField] private ScrapItem defaultFallbackTier1;
        [SerializeField] private ScrapItem defaultFallbackTier2;
        [SerializeField] private ScrapItem defaultFallbackTier3;
        [SerializeField] private ScrapItem defaultFallbackTier4;

        [Header("Tier Scrap Data Definitions")]
        [SerializeField] private ScrapData scrapDataTier1;
        [SerializeField] private ScrapData scrapDataTier2;
        [SerializeField] private ScrapData scrapDataTier3;
        [SerializeField] private ScrapData scrapDataTier4;

        [Header("Valuable Lava Loot VFX")]
        [SerializeField] private Material valuableLavaMaterial;
        [SerializeField] private GameObject valuableEmbersPrefab;

        public ScrapItem DefaultFallbackTier1 => defaultFallbackTier1;
        public ScrapItem DefaultFallbackTier2 => defaultFallbackTier2;
        public ScrapItem DefaultFallbackTier3 => defaultFallbackTier3;
        public ScrapItem DefaultFallbackTier4 => defaultFallbackTier4;

        public ScrapData ScrapDataTier1 => scrapDataTier1;
        public ScrapData ScrapDataTier2 => scrapDataTier2;
        public ScrapData ScrapDataTier3 => scrapDataTier3;
        public ScrapData ScrapDataTier4 => scrapDataTier4;

        public Material ValuableLavaMaterial => valuableLavaMaterial;
        public GameObject ValuableEmbersPrefab => valuableEmbersPrefab;

        /// <summary>
        /// Checks whether at least one custom model is registered for the specified tier.
        /// </summary>
        public bool HasCustomScrapForTier(int tier)
        {
            if (customScrapAssets == null || customScrapAssets.Count == 0) return false;
            for (int i = 0; i < customScrapAssets.Count; i++)
            {
                var entry = customScrapAssets[i];
                if (entry.scrapTier == tier && entry.modelOrPrefab != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns all custom entries matching the specified tier.
        /// </summary>
        public List<CustomScrapEntry> GetCustomEntriesForTier(int tier)
        {
            var list = new List<CustomScrapEntry>();
            if (customScrapAssets == null) return list;

            for (int i = 0; i < customScrapAssets.Count; i++)
            {
                var entry = customScrapAssets[i];
                if (entry.scrapTier == tier && entry.modelOrPrefab != null)
                {
                    list.Add(entry);
                }
            }
            return list;
        }

        /// <summary>
        /// Retrieves the fallback primitive prefab for a given tier.
        /// </summary>
        public ScrapItem GetFallbackPrefab(int tier)
        {
            return tier switch
            {
                1 => defaultFallbackTier1,
                2 => defaultFallbackTier2,
                3 => defaultFallbackTier3,
                4 => defaultFallbackTier4,
                _ => defaultFallbackTier1
            };
        }

        /// <summary>
        /// Retrieves the ScrapData configuration for a given tier.
        /// </summary>
        public ScrapData GetScrapData(int tier)
        {
            return tier switch
            {
                1 => scrapDataTier1,
                2 => scrapDataTier2,
                3 => scrapDataTier3,
                4 => scrapDataTier4,
                _ => scrapDataTier1
            };
        }

        /// <summary>
        /// Instantiates and configures a piece of scrap for the specified tier.
        /// If a custom 3D model is registered for that tier, wraps it automatically.
        /// Otherwise falls back to the default primitive placeholder without any errors.
        /// </summary>
        public ScrapItem CreateScrapInstanceForTier(int tier, Transform parent = null)
        {
            var customEntries = GetCustomEntriesForTier(tier);
            if (customEntries.Count > 0)
            {
                var chosen = customEntries[Random.Range(0, customEntries.Count)];
                return ConfigureScrapInstance(chosen.modelOrPrefab, tier, chosen.customScaleMultiplier, parent);
            }

            // Fallback to default primitive prefab
            ScrapItem fallbackPrefab = GetFallbackPrefab(tier);
            if (fallbackPrefab != null)
            {
                ScrapItem fallbackInstance = Instantiate(fallbackPrefab, parent);
                ScrapData data = GetScrapData(tier);
                if (data != null)
                {
                    fallbackInstance.Initialize(data);
                }
                if (valuableLavaMaterial != null) fallbackInstance.ValuableLavaMaterial = valuableLavaMaterial;
                if (valuableEmbersPrefab != null) fallbackInstance.ValuableEmbersPrefab = valuableEmbersPrefab;
                return fallbackInstance;
            }

            Debug.LogError($"[ScrapAssetCatalogSO] No custom scrap entry AND no fallback prefab found for Tier {tier}!");
            return null;
        }

        /// <summary>
        /// Wraps any raw 3D model, FBX, or prefab into a fully functional ScrapItem.
        /// Automatically creates Rigidbody, GravityBody, fitted SphereCollider, VisualRoot,
        /// and wires materials so normal PBR and valuable lava shaders work seamlessly.
        /// </summary>
        public ScrapItem ConfigureScrapInstance(GameObject rawModelOrPrefab, int tier, float scaleMultiplier = 1.0f, Transform parent = null)
        {
            if (rawModelOrPrefab == null)
            {
                return CreateScrapInstanceForTier(tier, parent);
            }

            // If the prefab already has a ScrapItem component on root, instantiate directly
            var existingScrapItem = rawModelOrPrefab.GetComponent<ScrapItem>();
            if (existingScrapItem != null)
            {
                ScrapItem inst = Instantiate(existingScrapItem, parent);
                ScrapData d = GetScrapData(tier);
                if (d != null) inst.Initialize(d);
                if (scaleMultiplier > 0.001f && Mathf.Abs(scaleMultiplier - 1.0f) > 0.01f)
                {
                    inst.transform.localScale *= scaleMultiplier;
                }
                return inst;
            }

            // 1. Create clean physics root GameObject
            GameObject root = new GameObject($"Scrap_Tier{tier}_{rawModelOrPrefab.name}");
            if (parent != null) root.transform.SetParent(parent, false);

            // 2. Setup Rigidbody (Zero-G orbital dynamics)
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.05f;

            // 3. Setup GravityBody
            GravityBody gb = root.AddComponent<GravityBody>();

            // 4. Create VisualRoot child
            GameObject visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(root.transform, false);
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.transform.localScale = Vector3.one;

            // 5. Instantiate user's raw model inside VisualRoot
            GameObject modelInstance = Instantiate(rawModelOrPrefab, visualRoot.transform);
            modelInstance.name = rawModelOrPrefab.name;
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;

            // 6. Strip any static colliders from inside the child model to prevent collision conflicts
            Collider[] childColliders = visualRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < childColliders.Length; i++)
            {
                if (Application.isPlaying)
                    Destroy(childColliders[i]);
                else
                    DestroyImmediate(childColliders[i]);
            }

            // 7. Calculate combined bounds across all child renderers
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            Bounds combinedBounds = new Bounds(visualRoot.transform.position, Vector3.zero);
            bool hasBounds = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!hasBounds)
                {
                    combinedBounds = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(r.bounds);
                }
            }

            // 8. Auto-normalize scale if model has extreme FBX import disparity, then apply customScaleMultiplier
            float finalScale = (scaleMultiplier > 0.001f) ? scaleMultiplier : 1.0f;
            if (hasBounds)
            {
                float maxDim = Mathf.Max(combinedBounds.size.x, combinedBounds.size.y, combinedBounds.size.z);
                if (maxDim > 10f || maxDim < 0.1f)
                {
                    float targetDim = tier switch
                    {
                        1 => 0.6f,
                        2 => 1.2f,
                        3 => 1.8f,
                        4 => 2.6f,
                        _ => 1.0f
                    };
                    finalScale *= (targetDim / maxDim);
                }
            }
            visualRoot.transform.localScale = Vector3.one * finalScale;

            // 9. Fit a smooth SphereCollider on root
            SphereCollider sc = root.AddComponent<SphereCollider>();
            if (hasBounds)
            {
                Vector3 localCenter = root.transform.InverseTransformPoint(combinedBounds.center);
                sc.center = localCenter;
                float radius = Mathf.Max(combinedBounds.extents.x, combinedBounds.extents.y, combinedBounds.extents.z) * finalScale;
                sc.radius = Mathf.Clamp(radius, 0.25f, 6.0f);
            }
            else
            {
                sc.center = Vector3.zero;
                sc.radius = 0.5f;
            }

            // 10. Attach ScrapItem and configure
            ScrapItem scrap = root.AddComponent<ScrapItem>();
            scrap.InitialVisualScale = visualRoot.transform.localScale;
            ScrapData data = GetScrapData(tier);
            if (data != null)
            {
                scrap.ApplyScrapData(data);
            }
            scrap.InitialVisualScale = visualRoot.transform.localScale;

            if (valuableLavaMaterial != null) scrap.ValuableLavaMaterial = valuableLavaMaterial;
            if (valuableEmbersPrefab != null) scrap.ValuableEmbersPrefab = valuableEmbersPrefab;

            // Auto-illuminate materials if needed for dark space visibility
            EnsureMaterialsIlluminated(visualRoot);

            // SetupValuableScrap(false) caches all original PBR materials on child renderers
            scrap.SetupValuableScrap(false);
            scrap.IgnorePlayerCollision();

            return scrap;
        }

        private static void EnsureMaterialsIlluminated(GameObject visualRoot)
        {
            if (visualRoot == null) return;
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            Shader scrapShader = null;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;

                Material[] mats = Application.isPlaying ? r.materials : r.sharedMaterials;
                bool changed = false;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;

                    if (mat.shader != null && mat.shader.name == "OrbitalPanopticon/DeformingLavaAnomaly")
                    {
                        continue;
                    }

                    if (mat.shader == null || mat.shader.name != "OrbitalPanopticon/ScrapSpaceLit")
                    {
                        if (scrapShader == null) scrapShader = Shader.Find("OrbitalPanopticon/ScrapSpaceLit");
                        if (scrapShader != null)
                        {
                            Texture baseMap = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : (mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null);
                            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : (mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white);
                            Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                            Texture specMap = mat.HasProperty("_SpecGlossMap") ? mat.GetTexture("_SpecGlossMap") : (mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null);

                            mat.shader = scrapShader;
                            if (baseMap != null) { mat.SetTexture("_BaseMap", baseMap); mat.SetTexture("_MainTex", baseMap); }
                            mat.SetColor("_BaseColor", baseColor);
                            mat.SetColor("_Color", baseColor);
                            if (bumpMap != null) mat.SetTexture("_BumpMap", bumpMap);
                            if (specMap != null) mat.SetTexture("_SpecGlossMap", specMap);
                            mat.SetFloat("_AmbientFloor", 0.38f);
                            mat.SetColor("_RimColor", new Color(0.7f, 0.88f, 1.0f, 1.0f));
                            mat.SetFloat("_RimPower", 3.0f);
                            mat.SetFloat("_RimIntensity", 0.45f);
                            changed = true;
                        }
                    }
                    else
                    {
                        if (mat.HasProperty("_AmbientFloor") && mat.GetFloat("_AmbientFloor") < 0.2f)
                        {
                            mat.SetFloat("_AmbientFloor", 0.38f);
                            changed = true;
                        }
                        if (mat.HasProperty("_RimIntensity") && mat.GetFloat("_RimIntensity") < 0.2f)
                        {
                            mat.SetFloat("_RimIntensity", 0.45f);
                            changed = true;
                        }
                    }
                }
                if (changed)
                {
                    if (Application.isPlaying)
                        r.materials = mats;
                    else
                        r.sharedMaterials = mats;
                }
            }
        }
    }
}
