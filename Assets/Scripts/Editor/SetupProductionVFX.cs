using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Player;

namespace OrbitalPanopticon.Editor
{
    public static class SetupProductionVFX
    {
        public const string GlowShaderName = "OrbitalPanopticon/ParticleAdditiveGlow";
        public const string GlowMatPath = "Assets/_Project/Materials/M_ParticleAdditiveGlow.mat";
        public const string ResourceGlowMatPath = "Assets/Resources/M_ParticleAdditiveGlow.mat";

        public const string PullStreamPrefabPath = "Assets/_Project/Prefabs/VFX_GravityPullStream.prefab";
        public const string PuntBlastPrefabPath = "Assets/_Project/Prefabs/VFX_GravityPuntBlast.prefab";
        public const string SpaceDustPrefabPath = "Assets/_Project/Prefabs/VFX_SpaceFlightDust.prefab";
        public const string ShiftDashPrefabPath = "Assets/_Project/Prefabs/VFX_ShiftDashBurst.prefab";
        public const string AccretionSparksPrefabPath = "Assets/_Project/Prefabs/VFX_AccretionSparks.prefab";

        [MenuItem("Tools/Setup Production Particle Systems")]
        public static void SetupAll()
        {
            Material glowMat = CreateOrUpdateGlowMaterial();

            GameObject pullPrefab = CreatePullStreamPrefab(glowMat);
            GameObject puntPrefab = CreatePuntBlastPrefab(glowMat);
            GameObject dustPrefab = CreateSpaceDustPrefab(glowMat);
            GameObject dashPrefab = CreateShiftDashPrefab(glowMat);
            GameObject sparksPrefab = CreateAccretionSparksPrefab(glowMat);

            CopyPrefabsToResources();

            AssignToScrapPrefabs(sparksPrefab);
            ConfigurePlayerDronePrefab(pullPrefab, puntPrefab, dustPrefab, dashPrefab);
            ConfigureScenePlayerDrone("Assets/_Project/Scenes/GameplayScene.unity", pullPrefab, puntPrefab, dustPrefab, dashPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFCC>[SetupProductionVFX] Successfully generated all 5 production Particle Systems, M_ParticleAdditiveGlow, and wired Drone & Scrap prefabs/scenes!</color>");
        }

        public static Material CreateOrUpdateGlowMaterial()
        {
            Shader shader = Shader.Find(GlowShaderName);
            if (shader == null)
            {
                Debug.LogError($"[SetupProductionVFX] Shader '{GlowShaderName}' not found!");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(GlowMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, GlowMatPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_CoreSharpness", 4.0f);
            mat.SetFloat("_GlowFalloff", 2.0f);
            EditorUtility.SetDirty(mat);

            EnsureDirectory("Assets/Resources");
            if (File.Exists(ResourceGlowMatPath))
            {
                AssetDatabase.DeleteAsset(ResourceGlowMatPath);
            }
            AssetDatabase.CopyAsset(GlowMatPath, ResourceGlowMatPath);

            return mat;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static GameObject CreatePullStreamPrefab(Material mat)
        {
            EnsureDirectory("Assets/_Project/Prefabs");
            GameObject root = new GameObject("VFX_GravityPullStream");
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer rend = root.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.1f);
            main.startColor = new Color(0.2f, 0.9f, 3.5f, 0.9f); // HDR Cyan/Electric Blue
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 90;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 55f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.45f;
            shape.position = new Vector3(0f, 0f, 2.2f);
            shape.rotation = new Vector3(0f, 180f, 0f); // Inward facing toward muzzle

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalZ = new ParticleSystem.MinMaxCurve(5.0f, 7.5f); // Inward vortex
            vel.radial = new ParticleSystem.MinMaxCurve(-3.5f, -5.0f);
            vel.z = new ParticleSystem.MinMaxCurve(-4.0f, -6.5f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.15f, 0.85f, 3.2f), 0.0f),
                    new GradientColorKey(new Color(0.3f, 1.4f, 4.0f), 0.5f),
                    new GradientColorKey(new Color(0.05f, 0.3f, 1.5f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.2f),
                    new GradientAlphaKey(0.85f, 0.8f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 0.4f);
            sizeCurve.AddKey(0.3f, 1.0f);
            sizeCurve.AddKey(1f, 0.2f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.cameraVelocityScale = 0f;
            rend.velocityScale = 0.06f;
            rend.lengthScale = 2.8f;
            if (mat != null) rend.sharedMaterial = mat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PullStreamPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject CreatePuntBlastPrefab(Material mat)
        {
            EnsureDirectory("Assets/_Project/Prefabs");
            GameObject root = new GameObject("VFX_GravityPuntBlast");

            // Sub-Emitter 1: SparksBurst (Ring / tight cone of sparks)
            GameObject sparksObj = new GameObject("SparksBurst");
            sparksObj.transform.SetParent(root.transform, false);
            ParticleSystem psSparks = sparksObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer rendSparks = sparksObj.GetComponent<ParticleSystemRenderer>();

            var mainS = psSparks.main;
            mainS.duration = 0.4f;
            mainS.loop = false;
            mainS.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.35f);
            mainS.startSpeed = new ParticleSystem.MinMaxCurve(18f, 28f);
            mainS.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            mainS.startColor = new Color(0.3f, 1.8f, 3.5f, 1.0f);
            mainS.simulationSpace = ParticleSystemSimulationSpace.Local;
            mainS.maxParticles = 60;
            mainS.playOnAwake = false;

            var emS = psSparks.emission;
            emS.enabled = true;
            emS.rateOverTime = 0;
            emS.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 30) });

            var shapeS = psSparks.shape;
            shapeS.enabled = true;
            shapeS.shapeType = ParticleSystemShapeType.Cone;
            shapeS.angle = 14f;
            shapeS.radius = 0.08f;

            var colorS = psSparks.colorOverLifetime;
            colorS.enabled = true;
            Gradient gradS = new Gradient();
            gradS.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.3f, 2.0f, 3.5f), 0.0f),
                    new GradientColorKey(new Color(3.5f, 1.4f, 0.1f), 0.6f),
                    new GradientColorKey(new Color(2.5f, 0.3f, 0.02f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.9f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorS.color = new ParticleSystem.MinMaxGradient(gradS);

            var sizeS = psSparks.sizeOverLifetime;
            sizeS.enabled = true;
            AnimationCurve sizeCurveS = new AnimationCurve();
            sizeCurveS.AddKey(0f, 1.0f);
            sizeCurveS.AddKey(1f, 0.0f);
            sizeS.size = new ParticleSystem.MinMaxCurve(1f, sizeCurveS);

            rendSparks.renderMode = ParticleSystemRenderMode.Stretch;
            rendSparks.cameraVelocityScale = 0f;
            rendSparks.velocityScale = 0.035f;
            rendSparks.lengthScale = 3.5f;
            if (mat != null) rendSparks.sharedMaterial = mat;

            // Sub-Emitter 2: MuzzleFlash
            GameObject flashObj = new GameObject("MuzzleFlash");
            flashObj.transform.SetParent(root.transform, false);
            ParticleSystem psFlash = flashObj.AddComponent<ParticleSystem>();
            ParticleSystemRenderer rendFlash = flashObj.GetComponent<ParticleSystemRenderer>();

            var mainF = psFlash.main;
            mainF.duration = 0.2f;
            mainF.loop = false;
            mainF.startLifetime = 0.12f;
            mainF.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.5f);
            mainF.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            mainF.startColor = new Color(2.5f, 3.2f, 4.0f, 1.0f);
            mainF.simulationSpace = ParticleSystemSimulationSpace.Local;
            mainF.maxParticles = 12;
            mainF.playOnAwake = false;

            var emF = psFlash.emission;
            emF.enabled = true;
            emF.rateOverTime = 0;
            emF.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 6) });

            var shapeF = psFlash.shape;
            shapeF.enabled = true;
            shapeF.shapeType = ParticleSystemShapeType.Sphere;
            shapeF.radius = 0.15f;

            var colorF = psFlash.colorOverLifetime;
            colorF.enabled = true;
            Gradient gradF = new Gradient();
            gradF.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(2.5f, 3.2f, 4.0f), 0.0f),
                    new GradientColorKey(new Color(0.4f, 1.5f, 3.5f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorF.color = new ParticleSystem.MinMaxGradient(gradF);

            rendFlash.renderMode = ParticleSystemRenderMode.Billboard;
            if (mat != null) rendFlash.sharedMaterial = mat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PuntBlastPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject CreateSpaceDustPrefab(Material mat)
        {
            EnsureDirectory("Assets/_Project/Prefabs");
            GameObject root = new GameObject("VFX_SpaceFlightDust");
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer rend = root.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
            main.startColor = new Color(0.85f, 0.95f, 1.3f, 0.45f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 350;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 75f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(25f, 25f, 25f);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.85f, 0.95f, 1.3f), 0.0f),
                    new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.6f, 0.15f),
                    new GradientAlphaKey(0.6f, 0.85f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            color.color = new ParticleSystem.MinMaxGradient(grad);

            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.cameraVelocityScale = 0f;
            rend.velocityScale = 0.045f;
            rend.lengthScale = 1.0f;
            if (mat != null) rend.sharedMaterial = mat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SpaceDustPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject CreateShiftDashPrefab(Material mat)
        {
            EnsureDirectory("Assets/_Project/Prefabs");
            GameObject root = new GameObject("VFX_ShiftDashBurst");
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer rend = root.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(16f, 26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.18f);
            main.startColor = new Color(0.4f, 1.6f, 4.0f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 60;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 40) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 16f;
            shape.radius = 0.25f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.4f, 1.8f, 4.0f), 0.0f),
                    new GradientColorKey(new Color(2.8f, 0.3f, 2.2f), 0.6f),
                    new GradientColorKey(new Color(1.2f, 0.05f, 0.9f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1.0f);
            sizeCurve.AddKey(1f, 0.0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.cameraVelocityScale = 0f;
            rend.velocityScale = 0.04f;
            rend.lengthScale = 3.2f;
            if (mat != null) rend.sharedMaterial = mat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ShiftDashPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static GameObject CreateAccretionSparksPrefab(Material mat)
        {
            EnsureDirectory("Assets/_Project/Prefabs");
            GameObject root = new GameObject("VFX_AccretionSparks");
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ParticleSystemRenderer rend = root.GetComponent<ParticleSystemRenderer>();

            var main = ps.main;
            main.duration = 1.0f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startColor = new Color(3.5f, 1.8f, 0.3f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = 120;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // Controlled dynamically by depth01

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(3.5f, 2.5f, 1.2f), 0.0f),
                    new GradientColorKey(new Color(2.8f, 0.6f, 0.05f), 0.6f),
                    new GradientColorKey(new Color(1.0f, 0.1f, 0.01f), 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.7f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            color.color = new ParticleSystem.MinMaxGradient(grad);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1.0f);
            sizeCurve.AddKey(1f, 0.0f);
            size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.cameraVelocityScale = 0f;
            rend.velocityScale = 0.04f;
            rend.lengthScale = 2.5f;
            if (mat != null) rend.sharedMaterial = mat;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AccretionSparksPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CopyPrefabsToResources()
        {
            EnsureDirectory("Assets/Resources");
            string[] prefabs = new string[]
            {
                PullStreamPrefabPath,
                PuntBlastPrefabPath,
                SpaceDustPrefabPath,
                ShiftDashPrefabPath,
                AccretionSparksPrefabPath
            };

            foreach (string p in prefabs)
            {
                string resPath = "Assets/Resources/" + Path.GetFileName(p);
                if (File.Exists(resPath))
                {
                    AssetDatabase.DeleteAsset(resPath);
                }
                AssetDatabase.CopyAsset(p, resPath);
            }
        }

        private static void AssignToScrapPrefabs(GameObject sparksPrefab)
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
                    scrap.AccretionSparksPrefab = sparksPrefab;
                    EditorUtility.SetDirty(prefab);
                    PrefabUtility.SavePrefabAsset(prefab);
                    Debug.Log($"[SetupProductionVFX] Assigned AccretionSparks to {path}");
                }
            }
        }

        private static Transform FindCameraTransform(Transform root)
        {
            Transform cam = root.Find("MainCamera");
            if (cam == null) cam = root.Find("Main Camera");
            if (cam == null)
            {
                var c = root.GetComponentInChildren<Camera>(true);
                if (c != null) cam = c.transform;
            }
            return cam;
        }

        private static Transform FindRoboArmVisual(Transform root)
        {
            Transform arm = root.Find("MainCamera/RoboArmVisual");
            if (arm == null) arm = root.Find("Main Camera/RoboArmVisual");
            if (arm == null) arm = root.Find("VisualRoot/RoboArmVisual");
            if (arm == null)
            {
                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in all)
                {
                    if (t.name == "RoboArmVisual") return t;
                }
            }
            return arm;
        }

        private static void ConfigurePlayerDronePrefab(GameObject pullPrefab, GameObject puntPrefab, GameObject dustPrefab, GameObject dashPrefab)
        {
            const string playerPrefabPath = "Assets/Prefabs/PlayerDrone.prefab";
            if (!File.Exists(playerPrefabPath)) return;

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(playerPrefabPath);
            try
            {
                DroneController drone = prefabRoot.GetComponent<DroneController>();
                GravityManipulatorController manip = prefabRoot.GetComponentInChildren<GravityManipulatorController>();

                if (drone != null)
                {
                    Transform cam = FindCameraTransform(prefabRoot.transform);
                    Transform dustParent = cam != null ? cam : prefabRoot.transform;
                    Transform existingDust = dustParent.Find("VFX_SpaceFlightDust");
                    if (existingDust == null && dustPrefab != null)
                    {
                        GameObject dustObj = Object.Instantiate(dustPrefab, dustParent);
                        dustObj.name = "VFX_SpaceFlightDust";
                        drone.SpaceDustParticles = dustObj.GetComponent<ParticleSystem>();
                    }
                    else if (existingDust != null)
                    {
                        drone.SpaceDustParticles = existingDust.GetComponent<ParticleSystem>();
                    }

                    Transform existingDash = prefabRoot.transform.Find("VFX_ShiftDashBurst");
                    if (existingDash == null && dashPrefab != null)
                    {
                        GameObject dashObj = Object.Instantiate(dashPrefab, prefabRoot.transform);
                        dashObj.name = "VFX_ShiftDashBurst";
                        drone.ShiftDashBurstParticles = dashObj.GetComponent<ParticleSystem>();
                    }
                    else if (existingDash != null)
                    {
                        drone.ShiftDashBurstParticles = existingDash.GetComponent<ParticleSystem>();
                    }
                }

                if (manip != null)
                {
                    Transform arm = FindRoboArmVisual(prefabRoot.transform);
                    if (arm != null)
                    {
                        Transform oldRing = arm.Find("PuntShockwaveRing");
                        if (oldRing != null) Object.DestroyImmediate(oldRing.gameObject);

                        Transform existingPull = arm.Find("VFX_GravityPullStream");
                        if (existingPull == null && pullPrefab != null)
                        {
                            GameObject pullObj = Object.Instantiate(pullPrefab, arm);
                            pullObj.name = "VFX_GravityPullStream";
                            pullObj.transform.localPosition = new Vector3(0f, 0.05f, 0.65f);
                            pullObj.transform.localRotation = Quaternion.identity;
                            manip.PullVortexParticles = pullObj.GetComponent<ParticleSystem>();
                        }
                        else if (existingPull != null)
                        {
                            manip.PullVortexParticles = existingPull.GetComponent<ParticleSystem>();
                        }

                        Transform existingPunt = arm.Find("VFX_GravityPuntBlast");
                        if (existingPunt == null && puntPrefab != null)
                        {
                            GameObject puntObj = Object.Instantiate(puntPrefab, arm);
                            puntObj.name = "VFX_GravityPuntBlast";
                            puntObj.transform.localPosition = new Vector3(0f, 0.05f, 0.75f);
                            puntObj.transform.localRotation = Quaternion.identity;
                            manip.PuntBlastParticles = puntObj.GetComponentInChildren<ParticleSystem>();
                        }
                        else if (existingPunt != null)
                        {
                            manip.PuntBlastParticles = existingPunt.GetComponentInChildren<ParticleSystem>();
                        }
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, playerPrefabPath);
                Debug.Log($"[SetupProductionVFX] Configured PlayerDrone prefab with all production VFX.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureScenePlayerDrone(string scenePath, GameObject pullPrefab, GameObject puntPrefab, GameObject dustPrefab, GameObject dashPrefab)
        {
            if (!File.Exists(scenePath)) return;

            string currentScene = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject playerObj = GameObject.Find("PlayerDrone");
            if (playerObj != null)
            {
                DroneController drone = playerObj.GetComponent<DroneController>();
                GravityManipulatorController manip = playerObj.GetComponentInChildren<GravityManipulatorController>();

                Transform cam = FindCameraTransform(playerObj.transform);
                if (cam != null && drone != null)
                {
                    Transform existingDust = cam.Find("VFX_SpaceFlightDust");
                    if (existingDust == null && dustPrefab != null)
                    {
                        GameObject dustObj = (GameObject)PrefabUtility.InstantiatePrefab(dustPrefab, cam);
                        dustObj.name = "VFX_SpaceFlightDust";
                        drone.SpaceDustParticles = dustObj.GetComponent<ParticleSystem>();
                    }
                    else if (existingDust != null)
                    {
                        drone.SpaceDustParticles = existingDust.GetComponent<ParticleSystem>();
                    }
                }

                Transform existingDash = playerObj.transform.Find("VFX_ShiftDashBurst");
                if (existingDash == null && dashPrefab != null && drone != null)
                {
                    GameObject dashObj = (GameObject)PrefabUtility.InstantiatePrefab(dashPrefab, playerObj.transform);
                    dashObj.name = "VFX_ShiftDashBurst";
                    drone.ShiftDashBurstParticles = dashObj.GetComponent<ParticleSystem>();
                }
                else if (existingDash != null && drone != null)
                {
                    drone.ShiftDashBurstParticles = existingDash.GetComponent<ParticleSystem>();
                }

                if (manip != null)
                {
                    Transform arm = FindRoboArmVisual(playerObj.transform);
                    if (arm != null)
                    {
                        Transform oldRing = arm.Find("PuntShockwaveRing");
                        if (oldRing != null) Object.DestroyImmediate(oldRing.gameObject);

                        Transform existingPull = arm.Find("VFX_GravityPullStream");
                        if (existingPull == null && pullPrefab != null)
                        {
                            GameObject pullObj = (GameObject)PrefabUtility.InstantiatePrefab(pullPrefab, arm);
                            pullObj.name = "VFX_GravityPullStream";
                            pullObj.transform.localPosition = new Vector3(0f, 0.05f, 0.65f);
                            pullObj.transform.localRotation = Quaternion.identity;
                            manip.PullVortexParticles = pullObj.GetComponent<ParticleSystem>();
                        }
                        else if (existingPull != null)
                        {
                            manip.PullVortexParticles = existingPull.GetComponent<ParticleSystem>();
                        }

                        Transform existingPunt = arm.Find("VFX_GravityPuntBlast");
                        if (existingPunt == null && puntPrefab != null)
                        {
                            GameObject puntObj = (GameObject)PrefabUtility.InstantiatePrefab(puntPrefab, arm);
                            puntObj.name = "VFX_GravityPuntBlast";
                            puntObj.transform.localPosition = new Vector3(0f, 0.05f, 0.75f);
                            puntObj.transform.localRotation = Quaternion.identity;
                            manip.PuntBlastParticles = puntObj.GetComponentInChildren<ParticleSystem>();
                        }
                        else if (existingPunt != null)
                        {
                            manip.PuntBlastParticles = existingPunt.GetComponentInChildren<ParticleSystem>();
                        }
                    }
                }

                EditorUtility.SetDirty(playerObj);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[SetupProductionVFX] Configured PlayerDrone in {scenePath}.");
            }
        }
    }
}
