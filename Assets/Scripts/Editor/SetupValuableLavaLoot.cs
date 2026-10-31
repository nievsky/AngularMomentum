using System.IO;
using UnityEngine;
using UnityEditor;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Editor
{
    public static class SetupValuableLavaLoot
    {
        private const string LavaMatPath = "Assets/_Project/Materials/M_ValuableLavaLoot.mat";
        private const string EmberMatPath = "Assets/_Project/Materials/M_LavaEmberParticle.mat";
        private const string EmbersPrefabPath = "Assets/_Project/Prefabs/VFX_ValuableLavaEmbers.prefab";

        private const string ResourceLavaMatPath = "Assets/Resources/M_ValuableLavaLoot.mat";
        private const string ResourceEmbersPrefabPath = "Assets/Resources/VFX_ValuableLavaEmbers.prefab";

        [MenuItem("Tools/Setup Valuable Lava Loot Assets")]
        public static void SetupAll()
        {
            Material lavaMat = CreateOrUpdateLavaMaterial();
            Material emberMat = CreateOrUpdateEmberMaterial();
            GameObject embersPrefab = CreateOrUpdateEmbersPrefab(emberMat);

            // Copy to Resources for standalone build and fallback runtime loading
            EnsureResourceFolder();
            AssetDatabase.CopyAsset(LavaMatPath, ResourceLavaMatPath);
            AssetDatabase.CopyAsset(EmbersPrefabPath, ResourceEmbersPrefabPath);

            // Assign references on all Scrap prefabs
            AssignToScrapPrefabs(lavaMat, embersPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFCC>[SetupValuableLavaLoot] Successfully configured M_ValuableLavaLoot, M_LavaEmberParticle, VFX_ValuableLavaEmbers and assigned to all Scrap prefabs!</color>");
        }

        private static void EnsureResourceFolder()
        {
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }
            if (File.Exists(ResourceLavaMatPath))
            {
                AssetDatabase.DeleteAsset(ResourceLavaMatPath);
            }
            if (File.Exists(ResourceEmbersPrefabPath))
            {
                AssetDatabase.DeleteAsset(ResourceEmbersPrefabPath);
            }
        }

        public static Material CreateOrUpdateLavaMaterial()
        {
            Shader shader = Shader.Find("OrbitalPanopticon/ValuableLavaLoot");
            if (shader == null)
            {
                Debug.LogError("[SetupValuableLavaLoot] Shader 'OrbitalPanopticon/ValuableLavaLoot' not found!");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(LavaMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, LavaMatPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_MagmaCoreColor", new Color(3.8f, 2.8f, 0.4f, 1.0f));
            mat.SetColor("_MagmaMidColor", new Color(2.4f, 0.5f, 0.04f, 1.0f));
            mat.SetFloat("_LavaScale", 5.5f);
            mat.SetFloat("_FlowSpeed", 0.45f);
            mat.SetFloat("_MagmaContrast", 1.8f);
            mat.SetFloat("_MagmaIntensity", 2.2f);

            mat.SetColor("_CrustColor", new Color(0.08f, 0.06f, 0.07f, 1.0f));
            mat.SetFloat("_CrustScale", 4.2f);
            mat.SetFloat("_CrackWidth", 0.42f);
            mat.SetFloat("_CrackSoftness", 0.08f);
            mat.SetFloat("_EdgeGlowIntensity", 2.8f);

            mat.SetColor("_RimColor", new Color(2.8f, 1.3f, 0.1f, 1.0f));
            mat.SetFloat("_RimPower", 2.2f);
            mat.SetFloat("_RimIntensity", 2.0f);
            mat.SetFloat("_PulseSpeed", 4.5f);
            mat.SetFloat("_Alpha", 1.0f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material CreateOrUpdateEmberMaterial()
        {
            Shader shader = Shader.Find("OrbitalPanopticon/LavaEmber");
            if (shader == null)
            {
                Debug.LogError("[SetupValuableLavaLoot] Shader 'OrbitalPanopticon/LavaEmber' not found!");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(EmberMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, EmberMatPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static GameObject CreateOrUpdateEmbersPrefab(Material emberMat)
        {
            string dir = Path.GetDirectoryName(EmbersPrefabPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            GameObject root = new GameObject("VFX_ValuableLavaEmbers");
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer psRenderer = root.GetComponent<ParticleSystemRenderer>();

            // Main module
            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = new Color(2.8f, 1.8f, 0.35f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 60;
            main.playOnAwake = true;

            // Emission module (12 - 18 sparks/sec)
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 15f;

            // Shape module
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.75f;
            shape.radiusThickness = 0.3f;

            // Velocity over Lifetime (orbital sparks)
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
            vel.orbitalX = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            vel.radial = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);

            // Noise module (turbulence)
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.35f;
            noise.frequency = 0.85f;
            noise.scrollSpeed = 0.6f;
            noise.damping = true;

            // Size over Lifetime (shrinking to 0)
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.6f);
            sizeCurve.AddKey(0.2f, 1.0f);
            sizeCurve.AddKey(1.0f, 0.0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over Lifetime (gold -> lava orange -> deep red -> 0 alpha)
            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(2.8f, 2.2f, 0.6f), 0.0f),
                    new GradientColorKey(new Color(2.4f, 0.7f, 0.05f), 0.5f),
                    new GradientColorKey(new Color(1.2f, 0.15f, 0.01f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.15f),
                    new GradientAlphaKey(0.9f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            color.color = new ParticleSystem.MinMaxGradient(grad);

            // Renderer module
            psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (emberMat != null)
            {
                psRenderer.sharedMaterial = emberMat;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, EmbersPrefabPath);
            Object.DestroyImmediate(root);

            EditorUtility.SetDirty(prefab);
            return prefab;
        }

        private static void AssignToScrapPrefabs(Material lavaMat, GameObject embersPrefab)
        {
            string[] guids = AssetDatabase.FindAssets("Scrap_ t:Prefab", new[] { "Assets/Prefabs" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                ScrapItem scrap = prefab.GetComponent<ScrapItem>();
                if (scrap != null)
                {
                    scrap.ValuableLavaMaterial = lavaMat;
                    scrap.ValuableEmbersPrefab = embersPrefab;
                    EditorUtility.SetDirty(prefab);
                    PrefabUtility.SavePrefabAsset(prefab);
                    Debug.Log($"[SetupValuableLavaLoot] Assigned Lava Loot assets to {path}");
                }
            }
        }
    }
}
