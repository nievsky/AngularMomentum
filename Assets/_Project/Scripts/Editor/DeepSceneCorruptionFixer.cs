using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace OrbitalPanopticon.Editor
{
    public static class DeepSceneCorruptionFixer
    {
        public static readonly string BootstrapScenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
        public static readonly string MainMenuScenePath = "Assets/_Project/Scenes/MainMenuScene.unity";
        public static readonly string GameplayScenePath = "Assets/_Project/Scenes/GameplayScene.unity";
        public static readonly string CreditsScenePath = "Assets/_Project/Scenes/CreditsScene.unity";

        public static readonly string[] ContentScenes = new string[]
        {
            MainMenuScenePath,
            GameplayScenePath,
            CreditsScenePath
        };

        [MenuItem("Tools/Angular Momentum/Deep Clean & Rebuild All Scenes", false, 20)]
        public static void DeepCleanAndRebuildAllScenes()
        {
            Debug.Log("<color=#00FFFF><b>[DeepFixer] === Starting Deep SerializedProperty Purge & Scene Reconstruction ===</b></color>");

            // 1. Purge ScriptableObjects and Prefabs
            PurgeScriptableObjects();
            PurgePrefabs();

            // 2. Process each content scene
            foreach (string scenePath in ContentScenes)
            {
                if (!File.Exists(scenePath))
                {
                    Debug.LogWarning($"[DeepFixer] Scene file not found: {scenePath}");
                    continue;
                }

                PurgeSceneSerializedProperties(scenePath);
                ReconstructSceneFile(scenePath);
            }

            // 3. Ensure Bootstrap Scene & Build Settings
            SceneCorruptionSanitizer.EnsureBootstrapScene();
            SceneCorruptionSanitizer.UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FF66><b>[DeepFixer] === All Scenes Successfully Purged & Reconstructed! ===</b></color>");
        }

        public static void PurgeSceneSerializedProperties(string scenePath)
        {
            Debug.Log($"<color=#FFFF00>[DeepFixer] Purging Serialized Properties in: {scenePath}</color>");
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid()) return;

            int purgedPropsCount = 0;
            List<GameObject> allObjects = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CollectGameObjectsRecursive(root, allObjects);
            }

            foreach (GameObject go in allObjects)
            {
                if (go == null) continue;

                // Clear any sharedProfile on Volume components to prevent sub-asset corruption
                Volume vol = go.GetComponent<Volume>();
                if (vol != null)
                {
                    if (vol.sharedProfile != null)
                    {
                        Debug.LogWarning($"[DeepFixer] Cleared sharedProfile '{vol.sharedProfile.name}' on {go.name} (will be instantiated purely at runtime)");
                        vol.sharedProfile = null;
                        EditorUtility.SetDirty(vol);
                        purgedPropsCount++;
                    }
                }

                Component[] components = go.GetComponents<Component>();
                foreach (Component comp in components)
                {
                    if (comp == null) continue;

                    purgedPropsCount += PurgeSerializedObjectProperties(comp, go.name);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=#33FF88>[DeepFixer] Scene {scene.name} property purge complete. Total purged properties: {purgedPropsCount}</color>");
        }

        public static void ReconstructSceneFile(string scenePath)
        {
            Debug.Log($"<color=#00E5FF>[DeepFixer] Reconstructing clean scene YAML for: {scenePath}</color>");
            Scene sourceScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!sourceScene.IsValid()) return;

            GameObject[] roots = sourceScene.GetRootGameObjects();

            // Create temporary clean additive scene
            Scene cleanScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // Move all root GameObjects into cleanScene
            foreach (GameObject r in roots)
            {
                if (r != null)
                {
                    SceneManager.MoveGameObjectToScene(r, cleanScene);
                }
            }

            // Close old sourceScene
            EditorSceneManager.CloseScene(sourceScene, true);

            // Save cleanScene directly to target scenePath (wiping all orphaned YAML blocks)
            EditorSceneManager.SaveScene(cleanScene, scenePath);
            Debug.Log($"<color=#00FF66>[DeepFixer] Scene {Path.GetFileName(scenePath)} cleanly reconstructed and saved!</color>");
        }

        private static int PurgeSerializedObjectProperties(UnityEngine.Object targetObj, string contextName)
        {
            if (targetObj == null) return 0;

            int purged = 0;
            SerializedObject so = new SerializedObject(targetObj);
            SerializedProperty prop = so.GetIterator();

            while (prop.Next(true))
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference && prop.objectReferenceValue != null)
                {
                    UnityEngine.Object obj = prop.objectReferenceValue;

                    // Do not purge scene hierarchy references (GameObjects or Components attached to GameObjects)
                    if (obj is GameObject || obj is Component)
                    {
                        continue;
                    }

                    // Check if object is an asset on disk
                    string assetPath = AssetDatabase.GetAssetPath(obj);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        // Check if built-in Unity primitive asset
                        string typeName = obj.GetType().Name;
                        string objName = obj.name;

                        bool isBuiltin = objName.StartsWith("Unity-") || objName.StartsWith("Builtin-") ||
                                         objName == "Cube" || objName == "Sphere" || objName == "Cylinder" ||
                                         objName == "Quad" || objName == "Plane";

                        if (!isBuiltin)
                        {
                            Debug.LogWarning($"<color=#FF9900>[DeepFixer] Purging corrupted in-memory {typeName} ('{objName}') on {contextName}.{prop.propertyPath}</color>");
                            prop.objectReferenceValue = null;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            purged++;
                        }
                    }
                }
            }

            return purged;
        }

        private static void PurgeScriptableObjects()
        {
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new string[] { "Assets/_Project/ScriptableObjects" });
            int count = 0;
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                ScriptableObject soAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (soAsset != null)
                {
                    count += PurgeSerializedObjectProperties(soAsset, soAsset.name);
                    EditorUtility.SetDirty(soAsset);
                }
            }
            Debug.Log($"[DeepFixer] Purged {count} in-memory object references across ScriptableObjects.");
        }

        private static void PurgePrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/_Project/Prefabs" });
            int count = 0;
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root != null)
                {
                    List<GameObject> all = new List<GameObject>();
                    CollectGameObjectsRecursive(root, all);
                    bool modified = false;

                    foreach (GameObject go in all)
                    {
                        foreach (Component c in go.GetComponents<Component>())
                        {
                            if (c == null) continue;
                            int p = PurgeSerializedObjectProperties(c, $"{root.name}->{go.name}");
                            if (p > 0)
                            {
                                count += p;
                                modified = true;
                            }
                        }
                    }

                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Debug.Log($"[DeepFixer] Purged {count} in-memory object references across Prefabs.");
        }

        private static void CollectGameObjectsRecursive(GameObject current, List<GameObject> list)
        {
            if (current == null) return;
            list.Add(current);
            for (int i = 0; i < current.transform.childCount; i++)
            {
                CollectGameObjectsRecursive(current.transform.GetChild(i).gameObject, list);
            }
        }

        [MenuItem("Tools/Angular Momentum/Build Clean Release (All Steps)", false, 100)]
        public static void BuildCleanReleaseAllSteps()
        {
            DeepCleanAndRebuildAllScenes();

            // Clear old build folders
            string oldPrototype = @"D:\GameDev\Builds\Angular Momentum\Prototype6(PC)";
            if (Directory.Exists(oldPrototype))
            {
                try { Directory.Delete(oldPrototype, true); } catch { }
            }

            string cleanReleaseDir = @"D:\GameDev\Builds\Angular Momentum\ReleaseBuild_Clean";
            if (Directory.Exists(cleanReleaseDir))
            {
                try { Directory.Delete(cleanReleaseDir, true); } catch { }
            }
            Directory.CreateDirectory(cleanReleaseDir);

            string exePath = Path.Combine(cleanReleaseDir, "AngularMomentum.exe");

            string[] scenes = new string[]
            {
                BootstrapScenePath,
                MainMenuScenePath,
                GameplayScenePath,
                CreditsScenePath
            };

            Debug.Log($"<color=#00FFFF><b>[Build] Building Standalone Windows 64 with CleanBuildCache to: {exePath}</b></color>");

            BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            };

            var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"<color=#00FF66><b>[Build Succeeded!] Clean Release successfully built at: {exePath} ({report.summary.totalSize / (1024 * 1024)} MB)</b></color>");
            }
            else
            {
                Debug.LogError($"<color=#FF0033><b>[Build Failed!] Result: {report.summary.result}, Errors: {report.summary.totalErrors}</b></color>");
            }
        }
    }
}
