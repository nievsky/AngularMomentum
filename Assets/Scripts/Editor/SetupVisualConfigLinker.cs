using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public static class SetupVisualConfigLinker
    {
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/VisualEnvironmentConfig.asset";

        [MenuItem("Tools/Orbital Panopticon/Link Visual Environment Config to All Scenes")]
        public static void LinkAllScenes()
        {
            var config = AssetDatabase.LoadAssetAtPath<VisualEnvironmentConfigSO>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"[SetupVisualConfigLinker] VisualEnvironmentConfig.asset not found at {ConfigPath}!");
                return;
            }

            string[] scenePaths = new string[]
            {
                "Assets/_Project/Scenes/MainMenuScene.unity",
                "Assets/_Project/Scenes/GameplayScene.unity",
                "Assets/_Project/Scenes/CreditsScene.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Debug.Log($"[SetupVisualConfigLinker] Processing scene: {scene.name}");

                var skyboxControllers = Object.FindObjectsByType<SpaceSkyboxController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var sky in skyboxControllers)
                {
                    sky.visualConfig = config;
                    sky.LoadFromConfig();
                    EditorUtility.SetDirty(sky);
                    Debug.Log($"[SetupVisualConfigLinker] Linked visualConfig to SpaceSkyboxController in {scene.name}");
                }

                var vhsControllers = Object.FindObjectsByType<VHSCameraController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var vhs in vhsControllers)
                {
                    vhs.visualConfig = config;
                    vhs.LoadFromConfig();
                    EditorUtility.SetDirty(vhs);
                    Debug.Log($"[SetupVisualConfigLinker] Linked visualConfig to VHSCameraController in {scene.name}");
                }

                var postControllers = Object.FindObjectsByType<PostProcessingToolkitController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var post in postControllers)
                {
                    post.visualConfig = config;
                    post.LoadFromConfig();
                    EditorUtility.SetDirty(post);
                    Debug.Log($"[SetupVisualConfigLinker] Linked visualConfig to PostProcessingToolkitController in {scene.name}");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[SetupVisualConfigLinker] Saved scene: {scene.name}");
            }

            // Restore GameplayScene as current active scene
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/GameplayScene.unity", OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00FFCC>[SetupVisualConfigLinker] Batch scene visualConfig assignment complete!</color>");
        }
    }
}
