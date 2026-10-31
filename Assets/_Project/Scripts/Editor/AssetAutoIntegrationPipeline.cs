using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using OrbitalPanopticon.Player;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Editor
{
    /// <summary>
    /// Automated pipeline for 3D FBX models, PBR materials (with direct Roughness support),
    /// and Robo-Arm LMB/RMB Animation Controller setup.
    /// Menu Item: Tools/Orbital Panopticon/Auto-Bind 3D Assets & Materials
    /// </summary>
    public static class AssetAutoIntegrationPipeline
    {
        private const string RootModelsPath = "Assets/_Project/Art/Models";
        private const string GeneratedMaterialsPath = "Assets/_Project/Materials/Generated";
        private const string RoboArmFolder = "Assets/_Project/Art/Models/RoboArm";
        private const string HelperBotFolder = "Assets/_Project/Art/Models/HelperBot";
        private const string OverseerEyeFolder = "Assets/_Project/Art/Models/OverseerEye";
        private const string ScrapRootFolder = "Assets/_Project/Art/Models/Scrap";

        [MenuItem("Tools/Orbital Panopticon/Auto-Bind 3D Assets & Materials", priority = 10)]
        public static void RunPipelineMenu()
        {
            ExecutePipeline();
        }

        public static void ExecutePipeline()
        {
            Debug.Log("<color=#00FFAA>===============================================================</color>");
            Debug.Log("<color=#00FFAA>[ASSET PIPELINE] Starting 3D Asset, Texture & Animation Integration...</color>");

            try
            {
                // 1. Ensure Target Directory Structure
                EnsureDirectoryStructure();

                // 2. Organize root assets into designated folders if user dropped them in Art/Models
                OrganizeRootAssetsIfNeeded();

                // 3. Generate PBR Materials with Direct Roughness Support
                GeneratePBRMaterials();

                // 4. Setup Robo-Arm Animation Controller and bind to RoboArmVisual
                SetupRoboArmModelAndAnimation();

                // 5. Replace Helper Drone Bot VisualRoot in prefab and scene
                SetupHelperDroneBot();

                // 6. Replace Overseer Eye VisualRoot if eyeball model is present
                SetupOverseerEye();

                // 7. Replace Scrap Models in Tier1..Tier4 prefabs
                SetupScrapPrefabs();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("<color=#00FFAA>[ASSET PIPELINE] All 3D Assets, Materials & Animations successfully integrated!</color>");
                Debug.Log("<color=#00FFAA>===============================================================</color>");
            }
            catch (Exception ex)
            {
                Debug.LogError($"<color=#FF0055>[ASSET PIPELINE] Error during pipeline execution: {ex}</color>");
            }
        }

        #region 1. Directory Structure & Organization

        private static void EnsureDirectoryStructure()
        {
            EnsureFolderExists("Assets/_Project/Art");
            EnsureFolderExists("Assets/_Project/Art/Models");
            EnsureFolderExists("Assets/_Project/Art/Models/RoboArm");
            EnsureFolderExists("Assets/_Project/Art/Models/HelperBot");
            EnsureFolderExists("Assets/_Project/Art/Models/OverseerEye");
            EnsureFolderExists("Assets/_Project/Art/Models/Scrap");
            EnsureFolderExists("Assets/_Project/Art/Models/Scrap/Tier1");
            EnsureFolderExists("Assets/_Project/Art/Models/Scrap/Tier2");
            EnsureFolderExists("Assets/_Project/Art/Models/Scrap/Tier3");
            EnsureFolderExists("Assets/_Project/Art/Models/Scrap/Tier4");

            EnsureFolderExists("Assets/_Project/Materials");
            EnsureFolderExists("Assets/_Project/Materials/Generated");
        }

        private static void EnsureFolderExists(string folderPath)
        {
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
                string folderName = Path.GetFileName(folderPath);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolderExists(parent);
                }
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void OrganizeRootAssetsIfNeeded()
        {
            // Main Gun -> RoboArm
            MoveAssetIfExists("Assets/_Project/Art/Models/MainGun_animated.fbx", $"{RoboArmFolder}/MainGun_animated.fbx");
            MoveAssetIfExists("Assets/_Project/Art/Models/MainGun_textures", $"{RoboArmFolder}/MainGun_textures");

            // Robot Low -> HelperBot
            MoveAssetIfExists("Assets/_Project/Art/Models/robot_low.fbx", $"{HelperBotFolder}/robot_low.fbx");
            MoveAssetIfExists("Assets/_Project/Art/Models/robot_textures", $"{HelperBotFolder}/robot_textures");

            // Eyeball -> OverseerEye
            MoveAssetIfExists("Assets/_Project/Art/Models/eyeball.fbx", $"{OverseerEyeFolder}/eyeball.fbx");
            MoveAssetIfExists("Assets/_Project/Art/Models/eyeball_textures", $"{OverseerEyeFolder}/eyeball_textures");

            // Ariral -> Scrap/Tier4 (Anomaly)
            MoveAssetIfExists("Assets/_Project/Art/Models/ariral.fbx", $"{ScrapRootFolder}/Tier4/ariral.fbx");
            MoveAssetIfExists("Assets/_Project/Art/Models/ariral_textures", $"{ScrapRootFolder}/Tier4/ariral_textures");

            AssetDatabase.Refresh();
        }

        private static void MoveAssetIfExists(string src, string dst)
        {
            if (File.Exists(src) || Directory.Exists(src))
            {
                if (!File.Exists(dst) && !Directory.Exists(dst))
                {
                    string err = AssetDatabase.MoveAsset(src, dst);
                    if (string.IsNullOrEmpty(err))
                    {
                        Debug.Log($"[ASSET PIPELINE] Organized asset: '{src}' -> '{dst}'");
                    }
                    else
                    {
                        Debug.LogWarning($"[ASSET PIPELINE] MoveAsset failed for '{src}': {err}");
                    }
                }
            }
        }

        #endregion

        #region 2. PBR Material Generator (Direct Roughness Map Support)

        public static void GeneratePBRMaterials()
        {
            string[] searchFolders = new string[] {
                RoboArmFolder,
                HelperBotFolder,
                OverseerEyeFolder,
                $"{ScrapRootFolder}/Tier1",
                $"{ScrapRootFolder}/Tier2",
                $"{ScrapRootFolder}/Tier3",
                $"{ScrapRootFolder}/Tier4"
            };

            foreach (var folder in searchFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                ProcessFolderMaterials(folder);
            }
        }

        private static void ProcessFolderMaterials(string folderPath)
        {
            string folderName = Path.GetFileName(folderPath);
            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new string[] { folderPath });
            if (texGuids.Length == 0) return;

            Texture2D baseColorTex = null;
            Texture2D roughnessTex = null;
            Texture2D metallicTex = null;
            Texture2D normalTex = null;
            Texture2D emissiveTex = null;
            Texture2D maskMapTex = null;

            foreach (var guid in texGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string filename = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                // 1. Normal Map
                if (filename.Contains("normal") || filename.Contains("norm") || filename.EndsWith("_n") || filename.Contains("_n."))
                {
                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                    }
                    normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                // 2. Emissive Map
                else if (filename.Contains("emissive") || filename.Contains("emission") || filename.Contains("emit"))
                {
                    emissiveTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                // 3. Roughness Map
                else if (filename.Contains("roughness") || filename.Contains("rough"))
                {
                    roughnessTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                // 4. Metallic Map
                else if (filename.Contains("metallic") || filename.Contains("metal"))
                {
                    metallicTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                // 5. Mask Map (R: Metallic, G: Occlusion, B: Detail, A: Smoothness/Roughness)
                else if (filename.Contains("maskmap") || filename.Contains("mask"))
                {
                    maskMapTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                // 6. Base Color / Albedo / Diffuse
                else if (filename.Contains("basecolor") || filename.Contains("basemap") || filename.Contains("albedo") || filename.Contains("diffuse") || filename.Contains("base_color") || filename.Contains("color"))
                {
                    baseColorTex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            if (baseColorTex == null && roughnessTex == null && normalTex == null) return;

            // Resolve Shader: Prioritize Autodesk Interactive (Direct Roughness), fallback to URP Lit
            Shader shader = Shader.Find("Universal Render Pipeline/Autodesk Interactive");
            if (shader == null) shader = Shader.Find("Autodesk Interactive");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");

            string matPath = $"{GeneratedMaterialsPath}/M_{folderName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            // BaseColor / Albedo
            if (baseColorTex != null)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseColorTex);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseColorTex);
            }

            // Direct Roughness Map (_SpecGlossMap in Autodesk Interactive)
            Texture2D specOrRough = roughnessTex != null ? roughnessTex : maskMapTex;
            if (specOrRough != null)
            {
                if (mat.HasProperty("_SpecGlossMap"))
                {
                    mat.SetTexture("_SpecGlossMap", specOrRough);
                }
                if (mat.HasProperty("_Glossiness"))
                {
                    mat.SetFloat("_Glossiness", 1.0f);
                }
            }

            // Metallic Map
            Texture2D metal = metallicTex != null ? metallicTex : maskMapTex;
            if (metal != null)
            {
                if (mat.HasProperty("_MetallicGlossMap"))
                {
                    mat.SetTexture("_MetallicGlossMap", metal);
                }
                if (mat.HasProperty("_Metallic"))
                {
                    mat.SetFloat("_Metallic", 1.0f);
                }
            }

            // Normal Map
            if (normalTex != null)
            {
                if (mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normalTex);
                    mat.EnableKeyword("_NORMALMAP");
                    if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 1.0f);
                }
            }

            // Emissive Map ([HDR] Color.white * 2.5f)
            if (emissiveTex != null)
            {
                if (mat.HasProperty("_EmissionMap"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetTexture("_EmissionMap", emissiveTex);
                    Color hdrEmission = Color.white * 2.5f;
                    mat.SetColor("_EmissionColor", hdrEmission);
                }
            }
            else
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                }
                mat.DisableKeyword("_EMISSION");
            }

            EditorUtility.SetDirty(mat);
            Debug.Log($"[ASSET PIPELINE] Generated PBR Material: '{matPath}' with shader '{shader.name}' (Roughness: {specOrRough != null}, Emissive: {emissiveTex != null})");
        }

        #endregion

        #region 3. Robo-Arm FBX & Animation Controller Setup

        public static void SetupRoboArmModelAndAnimation()
        {
            string[] fbxGuids = AssetDatabase.FindAssets("t:GameObject", new string[] { RoboArmFolder });
            string fbxPath = null;
            foreach (var guid in fbxGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    fbxPath = p;
                    break;
                }
            }

            if (string.IsNullOrEmpty(fbxPath))
            {
                Debug.LogWarning("[ASSET PIPELINE] No Robo-Arm FBX model found in " + RoboArmFolder);
                return;
            }

            // 1. Scan AnimationClips inside the FBX
            AnimationClip[] allClips = LoadAnimationClipsFromAsset(fbxPath);
            AnimationClip pullClip = null;
            AnimationClip puntClip = null;
            AnimationClip idleClip = null;

            foreach (var clip in allClips)
            {
                string cName = clip.name.ToLowerInvariant();
                if (cName.StartsWith("__preview__")) continue;

                if (cName.Contains("mandibles_base_open"))
                {
                    pullClip = clip;
                }
                else if (pullClip == null && (cName.Contains("open") || cName.Contains("pull") || cName.Contains("hold")))
                {
                    pullClip = clip;
                }
                else if (puntClip == null && (cName.Contains("close") || cName.Contains("punt") || cName.Contains("fire") || cName.Contains("shoot")))
                {
                    puntClip = clip;
                }
                else if (idleClip == null && (cName.Contains("idle") || cName.Contains("rest") || cName.Contains("default")))
                {
                    idleClip = clip;
                }
            }

            // Fallback selections if specific naming differs
            if (idleClip == null && allClips.Length > 0) idleClip = allClips[0];
            if (pullClip == null && allClips.Length > 0) pullClip = allClips[0];
            if (puntClip == null && allClips.Length > 1) puntClip = allClips[1];
            else if (puntClip == null && allClips.Length > 0) puntClip = allClips[0];

            Debug.Log($"[ASSET PIPELINE] Robo-Arm Clips: Idle='{idleClip?.name}', Pull='{pullClip?.name}', Punt='{puntClip?.name}'");

            // 2. Programmatically generate AnimatorController at Assets/_Project/Art/Models/RoboArm/AC_RoboArm.controller
            string controllerPath = $"{RoboArmFolder}/AC_RoboArm.controller";
            AnimatorController controller = CreateRoboArmAnimatorController(controllerPath, idleClip, pullClip, puntClip);

            // 3. Bind to RoboArmVisual in GameplayScene
            BindRoboArmToGameplayScene(fbxPath, controller);
        }

        private static AnimationClip[] LoadAnimationClipsFromAsset(string fbxPath)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            var clips = new List<AnimationClip>();
            foreach (var a in assets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    clips.Add(c);
                }
            }
            return clips.ToArray();
        }

        private static AnimatorController CreateRoboArmAnimatorController(string path, AnimationClip idleClip, AnimationClip pullClip, AnimationClip puntClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            // Clear existing parameters
            while (controller.parameters.Length > 0)
            {
                controller.RemoveParameter(0);
            }

            controller.AddParameter("IsPullingRMB", AnimatorControllerParameterType.Bool);
            controller.AddParameter("FirePuntLMB", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;

            // Clear existing states
            var states = rootStateMachine.states;
            foreach (var s in states)
            {
                rootStateMachine.RemoveState(s.state);
            }
            var anyTransitions = rootStateMachine.anyStateTransitions;
            foreach (var t in anyTransitions)
            {
                rootStateMachine.RemoveAnyStateTransition(t);
            }

            // 1. Idle State (Default)
            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;
            rootStateMachine.defaultState = idleState;

            // 2. PullHold_RMB State
            var pullState = rootStateMachine.AddState("PullHold_RMB");
            pullState.motion = pullClip;

            // 3. Punt_LMB State
            var puntState = rootStateMachine.AddState("Punt_LMB");
            puntState.motion = puntClip;

            // Transition: Idle -> PullHold_RMB (IsPullingRMB == true)
            var tIdleToPull = idleState.AddTransition(pullState);
            tIdleToPull.hasExitTime = false;
            tIdleToPull.duration = 0.15f;
            tIdleToPull.AddCondition(AnimatorConditionMode.If, 0, "IsPullingRMB");

            // Transition: PullHold_RMB -> Idle (IsPullingRMB == false)
            var tPullToIdle = pullState.AddTransition(idleState);
            tPullToIdle.hasExitTime = false;
            tPullToIdle.duration = 0.15f;
            tPullToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsPullingRMB");

            // Transition: Any State -> Punt_LMB (FirePuntLMB trigger)
            var tAnyToPunt = rootStateMachine.AddAnyStateTransition(puntState);
            tAnyToPunt.hasExitTime = false;
            tAnyToPunt.duration = 0.05f;
            tAnyToPunt.AddCondition(AnimatorConditionMode.If, 0, "FirePuntLMB");

            // Transition: Punt_LMB -> Idle (Exit on finish)
            var tPuntToIdle = puntState.AddTransition(idleState);
            tPuntToIdle.hasExitTime = true;
            tPuntToIdle.exitTime = 0.85f;
            tPuntToIdle.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[ASSET PIPELINE] Programmed AnimatorController at '{path}'");
            return controller;
        }

        private static void BindRoboArmToGameplayScene(string fbxPath, AnimatorController controller)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameplayScene.unity");
            var drone = UnityEngine.Object.FindAnyObjectByType<DroneController>();
            if (drone == null)
            {
                Debug.LogWarning("[ASSET PIPELINE] DroneController not found in GameplayScene.");
                return;
            }

            Transform roboArmVisual = drone.transform.Find("MainCamera/RoboArmVisual");
            if (roboArmVisual == null) roboArmVisual = drone.transform.Find("Main Camera/RoboArmVisual");
            if (roboArmVisual == null)
            {
                foreach (Transform t in drone.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "RoboArmVisual") { roboArmVisual = t; break; }
                }
            }

            if (roboArmVisual == null)
            {
                Debug.LogWarning("[ASSET PIPELINE] RoboArmVisual not found under DroneController.");
                return;
            }

            // 1. Disable old primitive cylinder/claw meshes
            string[] primitiveNames = { "Arm_Base", "Piston_Left", "Piston_Right", "Claw_Prong_L", "Claw_Prong_R" };
            foreach (var pName in primitiveNames)
            {
                Transform pt = roboArmVisual.Find(pName);
                if (pt != null)
                {
                    pt.gameObject.SetActive(false);
                }
            }

            // 2. Remove or update existing 3D model under RoboArmVisual
            Transform oldModel = roboArmVisual.Find("Model_RoboArm");
            if (oldModel != null)
            {
                UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
            }

            GameObject fbxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxPrefab == null) return;

            GameObject armModel = (GameObject)PrefabUtility.InstantiatePrefab(fbxPrefab, roboArmVisual);
            armModel.name = "Model_RoboArm";
            armModel.transform.localPosition = new Vector3(0f, -0.05f, -0.1f);
            armModel.transform.localRotation = Quaternion.identity;

            // Calculate auto-scale: Target length along Z ~0.75m
            Bounds b = CalculateBounds(armModel);
            float lengthZ = Mathf.Max(0.01f, b.size.z);
            float scaleFactor = Mathf.Clamp(0.75f / lengthZ, 1.0f, 25.0f);
            armModel.transform.localScale = Vector3.one * scaleFactor;

            // Strip colliders from visual model
            var colliders = armModel.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
            {
                UnityEngine.Object.DestroyImmediate(c);
            }

            // Apply generated material
            Material armMat = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedMaterialsPath}/M_RoboArm.mat");
            if (armMat != null)
            {
                var rends = armModel.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    r.sharedMaterial = armMat;
                }
            }

            // Attach / Configure Animator
            var animator = armModel.GetComponent<Animator>();
            if (animator == null) animator = armModel.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Attach / Configure RoboArmAnimatorHook
            var hook = roboArmVisual.GetComponent<RoboArmAnimatorHook>();
            if (hook == null) hook = roboArmVisual.gameObject.AddComponent<RoboArmAnimatorHook>();
            hook.TargetAnimator = animator;

            // Position MuzzleNozzlePoint at the front tip of the arm's bounds
            Bounds scaledBounds = CalculateBounds(armModel);
            Transform muzzlePoint = roboArmVisual.Find("MuzzleNozzlePoint");
            if (muzzlePoint == null)
            {
                GameObject muzzleGo = new GameObject("MuzzleNozzlePoint");
                muzzleGo.transform.SetParent(roboArmVisual, false);
                muzzlePoint = muzzleGo.transform;
            }
            muzzlePoint.localPosition = new Vector3(0f, 0f, scaledBounds.extents.z * 1.15f);

            // Re-anchor pull stream and punt blast to muzzle point if needed
            Transform pullStream = roboArmVisual.Find("VFX_GravityPullStream");
            if (pullStream != null) pullStream.position = muzzlePoint.position;
            Transform puntBlast = roboArmVisual.Find("VFX_GravityPuntBlast");
            if (puntBlast != null) puntBlast.position = muzzlePoint.position;

            EditorUtility.SetDirty(drone);
            EditorUtility.SetDirty(roboArmVisual);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[ASSET PIPELINE] Successfully bound Robo-Arm 3D Model & AnimatorController to '{roboArmVisual.name}'");
        }

        #endregion

        #region 4. Helper Bot VisualRoot Replacement

        public static void SetupHelperDroneBot()
        {
            string[] fbxGuids = AssetDatabase.FindAssets("t:GameObject", new string[] { HelperBotFolder });
            string fbxPath = null;
            foreach (var guid in fbxGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    fbxPath = p;
                    break;
                }
            }

            if (string.IsNullOrEmpty(fbxPath)) return;

            string prefabPath = "Assets/_Project/Prefabs/HelperDroneBot.prefab";
            if (!File.Exists(prefabPath)) return;

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot == null) return;

            Transform visualRoot = prefabRoot.transform.Find("VisualRoot");
            if (visualRoot != null)
            {
                // Disable old placeholder meshes
                string[] oldParts = { "BodySphere", "SensorVisor", "ThrusterLeft", "ThrusterRight", "TetherNozzle" };
                foreach (var part in oldParts)
                {
                    Transform t = visualRoot.Find(part);
                    if (t != null) t.gameObject.SetActive(false);
                }

                // Remove existing model
                Transform oldM = visualRoot.Find("Model_HelperBot");
                if (oldM != null) UnityEngine.Object.DestroyImmediate(oldM.gameObject);

                GameObject botFBX = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (botFBX != null)
                {
                    GameObject botInst = (GameObject)PrefabUtility.InstantiatePrefab(botFBX, visualRoot);
                    botInst.name = "Model_HelperBot";
                    botInst.transform.localPosition = Vector3.zero;
                    botInst.transform.localRotation = Quaternion.identity;

                    // Auto-fit scale to match original placeholder bounding box (~1.8 units)
                    Bounds b = CalculateBounds(botInst);
                    float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    float scale = maxDim > 0.001f ? (1.8f / maxDim) : 1f;
                    botInst.transform.localScale = Vector3.one * scale;

                    // Strip colliders
                    var cols = botInst.GetComponentsInChildren<Collider>(true);
                    foreach (var c in cols) UnityEngine.Object.DestroyImmediate(c);

                    // Apply generated material
                    Material botMat = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedMaterialsPath}/M_HelperBot.mat");
                    if (botMat != null)
                    {
                        var rends = botInst.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in rends) r.sharedMaterial = botMat;
                    }

                    Debug.Log($"[ASSET PIPELINE] Bound HelperBot 3D model to '{prefabPath}' (Scale={scale:F3})");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        #endregion

        #region 5. Overseer Eye VisualRoot Replacement

        public static void SetupOverseerEye()
        {
            string[] fbxGuids = AssetDatabase.FindAssets("t:GameObject", new string[] { OverseerEyeFolder });
            string fbxPath = null;
            foreach (var guid in fbxGuids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    fbxPath = p;
                    break;
                }
            }

            if (string.IsNullOrEmpty(fbxPath)) return;

            // Update in GameplayScene
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameplayScene.unity");
            var eyes = UnityEngine.Object.FindObjectsByType<OverseerContainmentEye>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject eyeFBX = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            Material eyeMat = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedMaterialsPath}/M_OverseerEye.mat");

            if (eyeFBX != null && eyes.Length > 0)
            {
                foreach (var eye in eyes)
                {
                    Transform visualRoot = eye.transform.Find("VisualRoot");
                    if (visualRoot == null) visualRoot = eye.transform;

                    // Disable old primitive pupil/sclera
                    Transform sclera = visualRoot.Find("Eye_Sclera");
                    if (sclera != null) sclera.gameObject.SetActive(false);
                    Transform pupil = visualRoot.Find("Eye_Pupil");
                    if (pupil != null) pupil.gameObject.SetActive(false);

                    Transform oldM = visualRoot.Find("Model_Eyeball");
                    if (oldM != null) UnityEngine.Object.DestroyImmediate(oldM.gameObject);

                    GameObject eyeInst = (GameObject)PrefabUtility.InstantiatePrefab(eyeFBX, visualRoot);
                    eyeInst.name = "Model_Eyeball";
                    eyeInst.transform.localPosition = Vector3.zero;
                    eyeInst.transform.localRotation = Quaternion.identity;

                    // Match diameter ~3.5 units
                    Bounds b = CalculateBounds(eyeInst);
                    float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                    float scale = maxDim > 0.001f ? (3.5f / maxDim) : 1f;
                    eyeInst.transform.localScale = Vector3.one * scale;

                    var cols = eyeInst.GetComponentsInChildren<Collider>(true);
                    foreach (var c in cols) UnityEngine.Object.DestroyImmediate(c);

                    if (eyeMat != null)
                    {
                        var rends = eyeInst.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in rends) r.sharedMaterial = eyeMat;
                    }

                    EditorUtility.SetDirty(eye);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[ASSET PIPELINE] Bound Overseer Eyeball 3D model to {eyes.Length} OverseerContainmentEye instances in GameplayScene.");
            }
        }

        #endregion

        #region 6. Scrap Prefabs Replacement (Tier1..Tier4)

        public static void SetupScrapPrefabs()
        {
            float[] targetSizes = new float[] { 0.55f, 1.1f, 1.8f, 3.2f };

            for (int tier = 1; tier <= 4; tier++)
            {
                string tierFolder = $"{ScrapRootFolder}/Tier{tier}";
                if (!AssetDatabase.IsValidFolder(tierFolder)) continue;

                string[] fbxGuids = AssetDatabase.FindAssets("t:GameObject", new string[] { tierFolder });
                if (fbxGuids.Length == 0) continue;

                string fbxPath = AssetDatabase.GUIDToAssetPath(fbxGuids[0]);
                GameObject scrapFBX = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (scrapFBX == null) continue;

                Material scrapMat = AssetDatabase.LoadAssetAtPath<Material>($"{GeneratedMaterialsPath}/M_Tier{tier}.mat");

                // Find matching prefab
                string[] prefabGuids = AssetDatabase.FindAssets($"Scrap_Tier{tier} t:Prefab");
                foreach (var pGuid in prefabGuids)
                {
                    string pPath = AssetDatabase.GUIDToAssetPath(pGuid);
                    var pRoot = PrefabUtility.LoadPrefabContents(pPath);
                    if (pRoot == null) continue;

                    Transform vr = pRoot.transform.Find("VisualRoot");
                    if (vr != null)
                    {
                        for (int i = 0; i < vr.childCount; i++)
                        {
                            vr.GetChild(i).gameObject.SetActive(false);
                        }

                        Transform oldM = vr.Find($"Model_Scrap_Tier{tier}");
                        if (oldM != null) UnityEngine.Object.DestroyImmediate(oldM.gameObject);

                        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(scrapFBX, vr);
                        inst.name = $"Model_Scrap_Tier{tier}";
                        inst.transform.localPosition = Vector3.zero;
                        inst.transform.localRotation = Quaternion.identity;

                        Bounds b = CalculateBounds(inst);
                        float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                        float targetSize = targetSizes[tier - 1];
                        float scale = maxDim > 0.001f ? (targetSize / maxDim) : 1f;
                        inst.transform.localScale = Vector3.one * scale;

                        var cols = inst.GetComponentsInChildren<Collider>(true);
                        foreach (var c in cols) UnityEngine.Object.DestroyImmediate(c);

                        if (scrapMat != null)
                        {
                            var rends = inst.GetComponentsInChildren<Renderer>(true);
                            foreach (var r in rends) r.sharedMaterial = scrapMat;
                        }

                        // Update mainRenderer reference on ScrapItem
                        var scrapItem = pRoot.GetComponent<ScrapItem>();
                        if (scrapItem != null)
                        {
                            var rend = inst.GetComponentInChildren<Renderer>();
                            if (rend != null)
                            {
                                var field = typeof(ScrapItem).GetField("mainRenderer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                if (field != null) field.SetValue(scrapItem, rend);
                            }
                        }

                        Debug.Log($"[ASSET PIPELINE] Bound 3D Scrap Model '{scrapFBX.name}' to '{pPath}' (Tier {tier}, Scale={scale:F3})");
                    }

                    PrefabUtility.SaveAsPrefabAsset(pRoot, pPath);
                    PrefabUtility.UnloadPrefabContents(pRoot);
                }
            }
        }

        #endregion

        #region Utility Methods

        private static Bounds CalculateBounds(GameObject obj)
        {
            var rends = obj.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return new Bounds(obj.transform.position, Vector3.one);

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                b.Encapsulate(rends[i].bounds);
            }
            return b;
        }

        #endregion
    }
}
