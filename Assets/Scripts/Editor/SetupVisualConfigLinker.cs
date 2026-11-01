using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public static class SetupVisualConfigLinker
    {
        private const string MainMenuConfigPath = "Assets/_Project/ScriptableObjects/VisualConfig_MainMenu.asset";
        private const string GameplayConfigPath = "Assets/_Project/ScriptableObjects/VisualConfig_Gameplay.asset";
        private const string CreditsConfigPath = "Assets/_Project/ScriptableObjects/VisualConfig_Credits.asset";

        [MenuItem("Tools/Orbital Panopticon/Link Visual Environment Config to All Scenes")]
        public static void LinkAllScenes()
        {
            var menuConfig = AssetDatabase.LoadAssetAtPath<VisualEnvironmentConfigSO>(MainMenuConfigPath);
            var gameplayConfig = AssetDatabase.LoadAssetAtPath<VisualEnvironmentConfigSO>(GameplayConfigPath);
            var creditsConfig = AssetDatabase.LoadAssetAtPath<VisualEnvironmentConfigSO>(CreditsConfigPath);

            if (menuConfig == null || gameplayConfig == null || creditsConfig == null)
            {
                Debug.LogError("[SetupVisualConfigLinker] One or more per-scene VisualConfig assets are missing!");
                return;
            }

            LinkScene("Assets/_Project/Scenes/MainMenuScene.unity", menuConfig);
            LinkScene("Assets/_Project/Scenes/GameplayScene.unity", gameplayConfig);
            LinkScene("Assets/_Project/Scenes/CreditsScene.unity", creditsConfig);

            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00FFCC>[SetupVisualConfigLinker] Dedicated per-scene visualConfig assignment complete!</color>");
        }

        private static void LinkScene(string scenePath, VisualEnvironmentConfigSO config)
        {
            bool isCurrent = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == scenePath;
            var scene = isCurrent 
                ? UnityEngine.SceneManagement.SceneManager.GetActiveScene() 
                : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            Debug.Log($"[SetupVisualConfigLinker] Linking scene: {scene.name} -> {config.name}");

            var rootGos = scene.GetRootGameObjects();
            foreach (var go in rootGos)
            {
                var skyboxControllers = go.GetComponentsInChildren<SpaceSkyboxController>(true);
                foreach (var sky in skyboxControllers)
                {
                    sky.visualConfig = config;
                    sky.LoadFromConfig();
                    EditorUtility.SetDirty(sky);
                }

                var vhsControllers = go.GetComponentsInChildren<VHSCameraController>(true);
                foreach (var vhs in vhsControllers)
                {
                    vhs.visualConfig = config;
                    vhs.LoadFromConfig();
                    EditorUtility.SetDirty(vhs);
                }

                var postControllers = go.GetComponentsInChildren<PostProcessingToolkitController>(true);
                foreach (var post in postControllers)
                {
                    post.visualConfig = config;
                    post.EnsureVolumeAndOverrides();
                    post.LoadFromConfig();
                    EditorUtility.SetDirty(post);
                }

                var cameras = go.GetComponentsInChildren<Camera>(true);
                foreach (var cam in cameras)
                {
                    var addCam = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (addCam == null)
                    {
                        addCam = cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    }
                    addCam.renderPostProcessing = true;
                    addCam.volumeLayerMask = -1;
                    EditorUtility.SetDirty(cam);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (!isCurrent)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
