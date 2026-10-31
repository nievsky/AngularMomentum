using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public static class SetupUIButtonAnimators
    {
        public static readonly string[] ScenePaths = new string[]
        {
            "Assets/_Project/Scenes/MainMenuScene.unity",
            "Assets/_Project/Scenes/GameplayScene.unity",
            "Assets/_Project/Scenes/CreditsScene.unity"
        };

        [MenuItem("Tools/Orbital Panopticon/Setup UI Button Animators", priority = 25)]
        public static void SetupAll()
        {
            Debug.Log("<color=#00FFFF>[SetupUIButtonAnimators] Beginning UI Button Animator setup across all scenes and prefabs...</color>");

            string currentScenePath = EditorSceneManager.GetActiveScene().path;
            int totalAttachedButtons = 0;

            // 1. Process Scenes
            foreach (var scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include);
                int sceneAttached = 0;

                foreach (var btn in buttons)
                {
                    if (btn.GetComponent<UIButtonTweenAnimator>() == null)
                    {
                        Undo.AddComponent<UIButtonTweenAnimator>(btn.gameObject);
                        EditorUtility.SetDirty(btn.gameObject);
                        sceneAttached++;
                    }
                }

                if (sceneAttached > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }

                totalAttachedButtons += sceneAttached;
                Debug.Log($"[SetupUIButtonAnimators] Attached {sceneAttached} animators in {scenePath} (Total buttons in scene: {buttons.Length})");
            }

            // 2. Process Prefabs
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/_Project/Prefabs" });
            int prefabAttached = 0;

            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    if (root == null) continue;

                    Button[] buttons = root.GetComponentsInChildren<Button>(true);
                    bool modified = false;

                    foreach (var btn in buttons)
                    {
                        if (btn.GetComponent<UIButtonTweenAnimator>() == null)
                        {
                            btn.gameObject.AddComponent<UIButtonTweenAnimator>();
                            modified = true;
                            prefabAttached++;
                        }
                    }

                    if (modified)
                    {
                        try
                        {
                            PrefabUtility.SaveAsPrefabAsset(root, path);
                            Debug.Log($"[SetupUIButtonAnimators] Attached animators to buttons in prefab: {path}");
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[SetupUIButtonAnimators] Could not save prefab {path}: {ex.Message}");
                        }
                    }
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            // Restore active scene
            if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00FFCC>[SetupUIButtonAnimators] Successfully verified and attached animators across all scenes ({totalAttachedButtons}) and prefabs ({prefabAttached})!</color>");
        }
    }
}
