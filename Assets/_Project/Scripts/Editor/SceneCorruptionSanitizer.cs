using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public static class SceneCorruptionSanitizer
    {
        public static readonly string BootstrapScenePath = "Assets/_Project/Scenes/BootstrapScene.unity";
        public static readonly string MainMenuScenePath = "Assets/_Project/Scenes/MainMenuScene.unity";
        public static readonly string GameplayScenePath = "Assets/_Project/Scenes/GameplayScene.unity";
        public static readonly string CreditsScenePath = "Assets/_Project/Scenes/CreditsScene.unity";

        public static readonly string[] TargetScenePaths = new string[]
        {
            MainMenuScenePath,
            GameplayScenePath,
            CreditsScenePath
        };

        [MenuItem("Tools/Angular Momentum/Sanitize All Scenes & Prefabs", false, 10)]
        public static void SanitizeAllScenesAndPrefabs()
        {
            Debug.Log("<color=#00FFFF><b>[SceneCorruptionSanitizer] Starting Complete Sanitization...</b></color>");

            // 1. Ensure Bootstrap Scene is present and configured
            EnsureBootstrapScene();

            // 2. Sanitize Scenes
            foreach (string scenePath in TargetScenePaths)
            {
                if (File.Exists(scenePath))
                {
                    SanitizeScene(scenePath);
                }
                else
                {
                    Debug.LogWarning($"[SceneCorruptionSanitizer] Scene file not found: {scenePath}");
                }
            }

            // 3. Sanitize Prefabs
            SanitizePrefabsInDirectory("Assets/_Project/Prefabs");
            if (Directory.Exists("Assets/Prefabs"))
            {
                SanitizePrefabsInDirectory("Assets/Prefabs");
            }

            // 4. Update Build Settings
            UpdateBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FF66><b>[SceneCorruptionSanitizer] All Scenes and Prefabs Successfully Sanitized!</b></color>");
        }

        [MenuItem("Tools/Angular Momentum/Create Bootstrap Scene", false, 11)]
        public static void EnsureBootstrapScene()
        {
            string dir = Path.GetDirectoryName(BootstrapScenePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bool sceneExists = File.Exists(BootstrapScenePath);
            if (!sceneExists)
            {
                Debug.Log($"[SceneCorruptionSanitizer] Creating fresh BootstrapScene at {BootstrapScenePath}...");
                Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // 1. Minimal Camera
                GameObject camGo = new GameObject("Main Camera");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0; // Don't render anything
                cam.depth = -100;
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();

                // 2. Bootstrap Loader
                GameObject loaderGo = new GameObject("[BOOTSTRAP_LOADER]");
                loaderGo.AddComponent<BootstrapLoader>();

                EditorSceneManager.SaveScene(newScene, BootstrapScenePath);
                Debug.Log("<color=#00FF66>[SceneCorruptionSanitizer] BootstrapScene created and saved cleanly!</color>");
            }
            else
            {
                // Verify its content
                Scene scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
                bool hasLoader = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.GetComponent<BootstrapLoader>() != null)
                    {
                        hasLoader = true;
                        break;
                    }
                }

                if (!hasLoader)
                {
                    GameObject loaderGo = new GameObject("[BOOTSTRAP_LOADER]");
                    loaderGo.AddComponent<BootstrapLoader>();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }

            UpdateBuildSettings();
        }

        public static void UpdateBuildSettings()
        {
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();

            string[] orderedScenes = new string[]
            {
                BootstrapScenePath,
                MainMenuScenePath,
                GameplayScenePath,
                CreditsScenePath
            };

            foreach (string p in orderedScenes)
            {
                if (File.Exists(p))
                {
                    buildScenes.Add(new EditorBuildSettingsScene(p, true));
                }
            }

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log($"[SceneCorruptionSanitizer] Configured {EditorBuildSettings.scenes.Length} scenes in EditorBuildSettings (Bootstrap is Level0).");
        }

        public static void SanitizeScene(string scenePath)
        {
            Debug.Log($"<color=#FFFF00>[SceneCorruptionSanitizer] Sanitizing Scene: {scenePath}</color>");
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[SceneCorruptionSanitizer] Failed to open scene: {scenePath}");
                return;
            }

            int missingScriptsRemoved = 0;
            int inMemoryMeshesPurged = 0;
            int inMemoryMaterialsPurged = 0;
            int leakedObjectsDestroyed = 0;

            List<GameObject> allObjects = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                CollectGameObjectsRecursive(root, allObjects);
            }

            // 1. Delete Leaked Runtime Clones and Dynamic Singletons accidentally baked into scene
            for (int i = allObjects.Count - 1; i >= 0; i--)
            {
                GameObject go = allObjects[i];
                if (go == null) continue;

                if (IsLeakedRuntimeObject(go))
                {
                    leakedObjectsDestroyed++;
                    allObjects.RemoveAt(i);
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            // 2. Process Remaining Scene Objects
            foreach (GameObject go in allObjects)
            {
                if (go == null) continue;

                // A) Remove Missing Scripts
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                if (removed > 0)
                {
                    missingScriptsRemoved += removed;
                }

                // B) Purge Unsaved In-Memory Meshes on MeshFilter
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    if (IsUnsavedInMemoryMesh(mf.sharedMesh))
                    {
                        Debug.LogWarning($"[Sanitizer] Purging unsaved in-memory mesh '{mf.sharedMesh.name}' on '{go.name}' in {scenePath}");
                        mf.sharedMesh = null;
                        inMemoryMeshesPurged++;
                    }
                }

                // Purge Unsaved In-Memory Meshes on MeshCollider
                MeshCollider mc = go.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh != null)
                {
                    if (IsUnsavedInMemoryMesh(mc.sharedMesh))
                    {
                        Debug.LogWarning($"[Sanitizer] Purging unsaved in-memory collider mesh '{mc.sharedMesh.name}' on '{go.name}' in {scenePath}");
                        mc.sharedMesh = null;
                        inMemoryMeshesPurged++;
                    }
                }

                // C) Purge Unsaved In-Memory Materials on Renderers
                Renderer r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    Material[] sharedMats = r.sharedMaterials;
                    bool matChanged = false;
                    for (int m = 0; m < sharedMats.Length; m++)
                    {
                        Material mat = sharedMats[m];
                        if (mat != null && IsUnsavedInMemoryMaterial(mat))
                        {
                            Material diskMat = FindDiskMaterialReplacement(mat);
                            Debug.LogWarning($"[Sanitizer] Replacing unsaved in-memory material '{mat.name}' on '{go.name}' with '{(diskMat != null ? diskMat.name : "null")}'");
                            sharedMats[m] = diskMat;
                            matChanged = true;
                            inMemoryMaterialsPurged++;
                        }
                    }
                    if (matChanged)
                    {
                        r.sharedMaterials = sharedMats;
                    }
                }

                // Purge Unsaved In-Memory Materials on UI Graphics
                Graphic graphic = go.GetComponent<Graphic>();
                if (graphic != null && graphic.material != null)
                {
                    if (IsUnsavedInMemoryMaterial(graphic.material))
                    {
                        Debug.LogWarning($"[Sanitizer] Resetting unsaved in-memory UI material '{graphic.material.name}' on '{go.name}' to default null");
                        graphic.material = null;
                        inMemoryMaterialsPurged++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"<color=#33FF88>[SceneCorruptionSanitizer] Scene {scene.name} Sanitized: " +
                      $"MissingScripts: {missingScriptsRemoved}, In-Memory Meshes: {inMemoryMeshesPurged}, " +
                      $"In-Memory Materials: {inMemoryMaterialsPurged}, Leaked Objects Destroyed: {leakedObjectsDestroyed}</color>");
        }

        public static void SanitizePrefabsInDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;

            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new string[] { dirPath });
            int sanitizedPrefabs = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
                if (prefabRoot == null) continue;

                bool modified = false;
                List<GameObject> objects = new List<GameObject>();
                CollectGameObjectsRecursive(prefabRoot, objects);

                foreach (GameObject go in objects)
                {
                    if (go == null) continue;

                    int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                    if (removed > 0) modified = true;

                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null && IsUnsavedInMemoryMesh(mf.sharedMesh))
                    {
                        mf.sharedMesh = null;
                        modified = true;
                    }

                    MeshCollider mc = go.GetComponent<MeshCollider>();
                    if (mc != null && mc.sharedMesh != null && IsUnsavedInMemoryMesh(mc.sharedMesh))
                    {
                        mc.sharedMesh = null;
                        modified = true;
                    }

                    Renderer r = go.GetComponent<Renderer>();
                    if (r != null)
                    {
                        Material[] mats = r.sharedMaterials;
                        bool rMod = false;
                        for (int m = 0; m < mats.Length; m++)
                        {
                            if (mats[m] != null && IsUnsavedInMemoryMaterial(mats[m]))
                            {
                                mats[m] = FindDiskMaterialReplacement(mats[m]);
                                rMod = true;
                            }
                        }
                        if (rMod)
                        {
                            r.sharedMaterials = mats;
                            modified = true;
                        }
                    }

                    Graphic g = go.GetComponent<Graphic>();
                    if (g != null && g.material != null && IsUnsavedInMemoryMaterial(g.material))
                    {
                        g.material = null;
                        modified = true;
                    }
                }

                if (modified)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                    sanitizedPrefabs++;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            Debug.Log($"[SceneCorruptionSanitizer] Sanitized {sanitizedPrefabs} prefabs in {dirPath}");
        }

        private static bool IsLeakedRuntimeObject(GameObject go)
        {
            if (go == null) return false;

            string name = go.name;

            // Runtime singletons with [RuntimeInitializeOnLoadMethod] or DontDestroyOnLoad
            if (name.StartsWith("[FMOD_AUDIO_MANAGER]") ||
                name.StartsWith("[SCENE_TRANSITION_MANAGER]") ||
                name.StartsWith("[GLOBAL_FONT_APPLIER]"))
            {
                return true;
            }

            // Runtime instantiate clones
            if (name.Contains("(Clone)"))
            {
                return true;
            }

            // Runtime transient shockwave / debris objects
            if (name.Contains("HawkingPulseShockwave") ||
                name.Contains("OverdriveDashTrail") ||
                name.Contains("M_CameraBeamYellow_Runtime"))
            {
                return true;
            }

            return false;
        }

        private static bool IsUnsavedInMemoryMesh(Mesh mesh)
        {
            if (mesh == null) return false;

            string path = AssetDatabase.GetAssetPath(mesh);
            if (!string.IsNullOrEmpty(path))
            {
                // Has valid disk path or package path
                return false;
            }

            // Check if built-in primitive mesh
            string name = mesh.name;
            if (name == "Cube" || name == "Sphere" || name == "Cylinder" ||
                name == "Plane" || name == "Quad" || name == "Capsule" ||
                name.StartsWith("Unity-") || name.StartsWith("Builtin-"))
            {
                return false;
            }

            // In-memory mesh with empty path
            return true;
        }

        private static bool IsUnsavedInMemoryMaterial(Material mat)
        {
            if (mat == null) return false;

            if (mat.name.Contains("(Instance)") || mat.name.EndsWith(" (Instance)"))
            {
                return true;
            }

            string path = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(path))
            {
                // In-memory material created via new Material() in edit mode
                return true;
            }

            return false;
        }

        private static Material FindDiskMaterialReplacement(Material inMemoryMat)
        {
            if (inMemoryMat == null) return null;

            string rawName = inMemoryMat.name.Replace(" (Instance)", "").Replace("(Instance)", "").Trim();

            // 1. Search by name in AssetDatabase
            string[] guids = AssetDatabase.FindAssets($"{rawName} t:Material");
            if (guids != null && guids.Length > 0)
            {
                foreach (string g in guids)
                {
                    string p = AssetDatabase.GUIDToAssetPath(g);
                    Material m = AssetDatabase.LoadAssetAtPath<Material>(p);
                    if (m != null && m.name == rawName)
                    {
                        return m;
                    }
                }
                // Fallback to first found
                return AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            // 2. Check Resources folder
            Material resMat = Resources.Load<Material>(rawName);
            if (resMat != null) return resMat;

            // 3. Fallback to default URP Lit
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                string defaultMatPath = "Assets/_Project/Materials/M_DefaultFallback.mat";
                Material defMat = AssetDatabase.LoadAssetAtPath<Material>(defaultMatPath);
                if (defMat == null)
                {
                    defMat = new Material(urpLit);
                    AssetDatabase.CreateAsset(defMat, defaultMatPath);
                }
                return defMat;
            }

            return null;
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

        [MenuItem("Tools/Angular Momentum/Sanitize Scenes & Build Clean Windows Release", false, 50)]
        public static void SanitizeScenesAndBuildCleanWindowsRelease()
        {
            SanitizeAllScenesAndPrefabs();

            string buildDir = "Builds/CleanRelease";
            if (Directory.Exists(buildDir))
            {
                try
                {
                    Directory.Delete(buildDir, true);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Build] Could not completely delete {buildDir}: {ex.Message}");
                }
            }
            Directory.CreateDirectory(buildDir);

            string exePath = Path.Combine(buildDir, "AngularMomentum.exe");

            string[] scenes = new string[]
            {
                BootstrapScenePath,
                MainMenuScenePath,
                GameplayScenePath,
                CreditsScenePath
            };

            Debug.Log($"<color=#00FFFF><b>[Build] Building Standalone Windows 64 to {exePath} with CleanBuildCache...</b></color>");

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
                Debug.Log($"<color=#00FF66><b>[Build Succeeded!] Clean build generated at: {exePath} (Size: {report.summary.totalSize / (1024 * 1024)} MB)</b></color>");
            }
            else
            {
                Debug.LogError($"<color=#FF0033><b>[Build Failed!] Result: {report.summary.result}, Errors: {report.summary.totalErrors}</b></color>");
            }
        }
    }
}
