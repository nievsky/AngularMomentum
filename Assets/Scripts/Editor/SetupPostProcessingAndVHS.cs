using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public static class SetupPostProcessingAndVHS
    {
        private const string ProfilePath = "Assets/_Project/Settings/MainPostProcessProfile.asset";
        private const string VhsMatPath = "Assets/_Project/Materials/M_VHSMonitorEffect.mat";
        private const string RendererDataPath = "Assets/Shaders/New Universal Render Pipeline Asset_Renderer.asset";

        [MenuItem("Tools/Orbital Panopticon/Setup Post-Processing & VHS")]
        public static void SetupAll()
        {
            Debug.Log("<color=#00FFFF>[SetupPostProcessingAndVHS] Beginning setup...</color>");

            // 1. Ensure Directories
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Settings"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Settings");
            }

            // 2. Setup Volume Profile
            VolumeProfile profile = SetupVolumeProfile();

            // 3. Setup VHS Material
            Material vhsMat = AssetDatabase.LoadAssetAtPath<Material>(VhsMatPath);
            if (vhsMat == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Materials/S_VHSMonitorEffect.shader");
                if (shader != null)
                {
                    vhsMat = new Material(shader);
                    AssetDatabase.CreateAsset(vhsMat, VhsMatPath);
                }
            }

            // 4. Setup URP Renderer Feature
            SetupRendererFeature(vhsMat);

            // 5. Configure Scenes
            string[] scenePaths = new string[]
            {
                "Assets/_Project/Scenes/MainMenuScene.unity",
                "Assets/_Project/Scenes/GameplayScene.unity",
                "Assets/_Project/Scenes/CreditsScene.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                ConfigureScene(scenePath, profile, vhsMat);
            }

            // 6. Configure Prefab
            ConfigurePlayerPrefab();

            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00FFCC>[SetupPostProcessingAndVHS] Successfully configured Post-Processing Toolkit & VHS Monitor Effect across all scenes!</color>");
        }

        private static VolumeProfile SetupVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            // 1. Tonemapping (ACES)
            Tonemapping tm;
            if (!profile.TryGet(out tm)) tm = profile.Add<Tonemapping>(true);
            tm.mode.overrideState = true;
            tm.mode.value = TonemappingMode.ACES;

            // 2. Bloom
            Bloom bloom;
            if (!profile.TryGet(out bloom)) bloom = profile.Add<Bloom>(true);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.85f;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 1.6f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.65f;
            bloom.highQualityFiltering.overrideState = true;
            bloom.highQualityFiltering.value = true;

            // 3. Vignette
            Vignette vig;
            if (!profile.TryGet(out vig)) vig = profile.Add<Vignette>(true);
            vig.intensity.overrideState = true;
            vig.intensity.value = 0.28f;
            vig.smoothness.overrideState = true;
            vig.smoothness.value = 0.4f;

            // 4. Chromatic Aberration
            ChromaticAberration ca;
            if (!profile.TryGet(out ca)) ca = profile.Add<ChromaticAberration>(true);
            ca.intensity.overrideState = true;
            ca.intensity.value = 0.08f;

            // 5. Lens Distortion
            LensDistortion ld;
            if (!profile.TryGet(out ld)) ld = profile.Add<LensDistortion>(true);
            ld.intensity.overrideState = true;
            ld.intensity.value = -0.12f;

            // 6. Color Adjustments
            ColorAdjustments caAdj;
            if (!profile.TryGet(out caAdj)) caAdj = profile.Add<ColorAdjustments>(true);
            caAdj.contrast.overrideState = true;
            caAdj.contrast.value = 12f;
            caAdj.postExposure.overrideState = true;
            caAdj.postExposure.value = 0.15f;
            caAdj.saturation.overrideState = true;
            caAdj.saturation.value = 8f;
            caAdj.colorFilter.overrideState = true;
            caAdj.colorFilter.value = Color.white;

            // 7. Film Grain
            FilmGrain fg;
            if (!profile.TryGet(out fg)) fg = profile.Add<FilmGrain>(true);
            fg.active = false;
            fg.type.overrideState = true;
            fg.type.value = FilmGrainLookup.Thin1;
            fg.intensity.overrideState = true;
            fg.intensity.value = 0.15f;

            // 8. Motion Blur
            MotionBlur mb;
            if (!profile.TryGet(out mb)) mb = profile.Add<MotionBlur>(true);
            mb.active = false;
            mb.mode.overrideState = true;
            mb.mode.value = MotionBlurMode.CameraOnly;
            mb.intensity.overrideState = true;
            mb.intensity.value = 0.25f;

            // 9. Depth Of Field
            DepthOfField dof;
            if (!profile.TryGet(out dof)) dof = profile.Add<DepthOfField>(true);
            dof.active = false;

            EditorUtility.SetDirty(profile);
            Debug.Log($"[SetupPostProcessingAndVHS] Created/Updated VolumeProfile at {ProfilePath}");
            return profile;
        }

        private static void SetupRendererFeature(Material vhsMat)
        {
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);
            if (rendererData == null)
            {
                Debug.LogWarning($"[SetupPostProcessingAndVHS] RendererData not found at {RendererDataPath}");
                return;
            }

            SerializedObject so = new SerializedObject(rendererData);
            SerializedProperty featuresProp = so.FindProperty("m_RendererFeatures");
            SerializedProperty featureMapProp = so.FindProperty("m_RendererFeatureMap");

            FullScreenPassRendererFeature existingFeature = null;
            for (int i = 0; i < featuresProp.arraySize; i++)
            {
                var item = featuresProp.GetArrayElementAtIndex(i).objectReferenceValue as FullScreenPassRendererFeature;
                if (item != null && item.name == "VHS_CCTV_MonitorEffect")
                {
                    existingFeature = item;
                    break;
                }
            }

            if (existingFeature == null)
            {
                existingFeature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                existingFeature.name = "VHS_CCTV_MonitorEffect";
                AssetDatabase.AddObjectToAsset(existingFeature, rendererData);

                featuresProp.arraySize++;
                featuresProp.GetArrayElementAtIndex(featuresProp.arraySize - 1).objectReferenceValue = existingFeature;

                featureMapProp.arraySize++;
#pragma warning disable CS0618
                featureMapProp.GetArrayElementAtIndex(featureMapProp.arraySize - 1).longValue = (long)existingFeature.GetInstanceID();
#pragma warning restore CS0618

                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(rendererData);
                Debug.Log($"[SetupPostProcessingAndVHS] Added FullScreenPassRendererFeature to {RendererDataPath}");
            }

            // Configure Feature Serialized Properties
            SerializedObject featSo = new SerializedObject(existingFeature);
            var passMatProp = featSo.FindProperty("passMaterial");
            if (passMatProp != null) passMatProp.objectReferenceValue = vhsMat;

            var injProp = featSo.FindProperty("injectionPoint");
            if (injProp != null) injProp.enumValueIndex = 2; // AfterRenderingPostProcessing

            var reqProp = featSo.FindProperty("requirements");
            if (reqProp != null) reqProp.enumValueIndex = 3; // Color

            var fetchProp = featSo.FindProperty("fetchColorBuffer");
            if (fetchProp != null) fetchProp.boolValue = true;

            featSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(existingFeature);
        }

        private static void ConfigureScene(string scenePath, VolumeProfile profile, Material vhsMat)
        {
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Enable post processing on all cameras in the scene
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (var cam in cameras)
            {
                var addCam = cam.GetComponent<UniversalAdditionalCameraData>();
                if (addCam == null)
                {
                    addCam = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                }
                addCam.renderPostProcessing = true;
                addCam.requiresColorOption = CameraOverrideOption.On;
                addCam.requiresDepthOption = CameraOverrideOption.On;
                EditorUtility.SetDirty(cam.gameObject);
                Debug.Log($"[SetupPostProcessingAndVHS] Enabled post-processing on camera: {cam.name} in {scene.name}");
            }

            // Setup --- POST PROCESSING & VHS --- GameObject
            const string volumeObjName = "--- POST PROCESSING & VHS ---";
            GameObject volumeObj = GameObject.Find(volumeObjName);
            if (volumeObj == null)
            {
                volumeObj = new GameObject(volumeObjName);
                Undo.RegisterCreatedObjectUndo(volumeObj, "Create Post Processing Volume Object");
            }

            Volume volume = volumeObj.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObj.AddComponent<Volume>();
            }
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.sharedProfile = profile;

            var ppToolkit = volumeObj.GetComponent<PostProcessingToolkitController>();
            if (ppToolkit == null)
            {
                ppToolkit = volumeObj.AddComponent<PostProcessingToolkitController>();
            }
            ppToolkit.EnsureVolumeAndOverrides();
            ppToolkit.ApplySettingsToVolume();

            var vhsController = volumeObj.GetComponent<VHSCameraController>();
            if (vhsController == null)
            {
                vhsController = volumeObj.AddComponent<VHSCameraController>();
            }
            vhsController.VHSMaterial = vhsMat;
            vhsController.UpdateMaterialProperties();

            EditorUtility.SetDirty(volumeObj);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SetupPostProcessingAndVHS] Configured {volumeObjName} in {scenePath}");
        }

        private static void ConfigurePlayerPrefab()
        {
            const string prefabPath = "Assets/Prefabs/PlayerDrone.prefab";
            if (!File.Exists(prefabPath)) return;

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
                foreach (var cam in cameras)
                {
                    var addCam = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (addCam == null) addCam = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    addCam.renderPostProcessing = true;
                    addCam.requiresColorOption = CameraOverrideOption.On;
                    addCam.requiresDepthOption = CameraOverrideOption.On;
                    Debug.Log($"[SetupPostProcessingAndVHS] Enabled post-processing on prefab camera: {cam.name}");
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
