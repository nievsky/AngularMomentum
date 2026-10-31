using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public static class GlobalFontEditorUtility
    {
        public const string ConfigAssetPath = "Assets/_Project/ScriptableObjects/GlobalFontConfig.asset";
        public const string DefaultTMPFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public static readonly string[] ScenePaths = new string[]
        {
            "Assets/_Project/Scenes/MainMenuScene.unity",
            "Assets/_Project/Scenes/GameplayScene.unity",
            "Assets/_Project/Scenes/CreditsScene.unity"
        };

        public static GlobalFontConfigSO GetOrCreateConfig()
        {
            GlobalFontConfigSO config = AssetDatabase.LoadAssetAtPath<GlobalFontConfigSO>(ConfigAssetPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GlobalFontConfigSO>();

                // Attempt to link default TMP font
                TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultTMPFontPath);
                if (defaultFont != null)
                {
                    config.masterTMPFont = defaultFont;
                }

                string dir = Path.GetDirectoryName(ConfigAssetPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                AssetDatabase.CreateAsset(config, ConfigAssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"<color=#00FFCC>[GlobalFontEditorUtility] Created new GlobalFontConfig at {ConfigAssetPath}</color>");
            }

            return config;
        }

        [MenuItem("Tools/Orbital Panopticon/Apply Global Font To All Scenes & Prefabs", priority = 20)]
        public static void ApplyToAllScenesAndPrefabs()
        {
            GlobalFontConfigSO config = GetOrCreateConfig();
            ApplyToAllScenesAndPrefabs(config);
        }

        public static void ApplyToAllScenesAndPrefabs(GlobalFontConfigSO config)
        {
            if (config == null)
            {
                Debug.LogError("[GlobalFontEditorUtility] Config is null!");
                return;
            }

            Debug.Log("<color=#00FFFF>[GlobalFontEditorUtility] Starting Global Font application...</color>");

            // 1. Process all UI prefabs
            int updatedPrefabs = ApplyToPrefabs(config);

            // 2. Process all scenes
            int updatedScenes = 0;
            string currentScenePath = EditorSceneManager.GetActiveScene().path;

            foreach (var scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                ConfigureSceneFontManager(config);

                if (GlobalFontApplier.Instance != null)
                {
                    GlobalFontApplier.Instance.ApplyGlobalFontToEverything();
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                updatedScenes++;
                Debug.Log($"[GlobalFontEditorUtility] Applied font to scene: {scenePath}");
            }

            // Restore previously active scene if different
            if (!string.IsNullOrEmpty(currentScenePath) && File.Exists(currentScenePath))
            {
                EditorSceneManager.OpenScene(currentScenePath, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"<color=#00FFCC>[GlobalFontEditorUtility] Successfully applied font across {updatedScenes} scenes and {updatedPrefabs} prefabs!</color>");
        }

        public static void ConfigureSceneFontManager(GlobalFontConfigSO config)
        {
            const string managerName = "--- FONT MANAGER ---";
            GameObject managerObj = GameObject.Find(managerName);
            if (managerObj == null)
            {
                managerObj = new GameObject(managerName);
                Undo.RegisterCreatedObjectUndo(managerObj, "Create Font Manager");
            }

            var applier = managerObj.GetComponent<GlobalFontApplier>();
            if (applier == null)
            {
                applier = managerObj.AddComponent<GlobalFontApplier>();
            }

            applier.FontConfig = config;
            EditorUtility.SetDirty(managerObj);
        }

        public static int ApplyToPrefabs(GlobalFontConfigSO config)
        {
            if (config == null) return 0;

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new string[] { "Assets/_Project/Prefabs", "Assets/Prefabs" });
            int modifiedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefabContents = null;

                try
                {
                    prefabContents = PrefabUtility.LoadPrefabContents(path);
                    if (prefabContents == null) continue;

                    TMP_Text[] tmpTexts = prefabContents.GetComponentsInChildren<TMP_Text>(true);
                    UnityEngine.UI.Text[] legacyTexts = prefabContents.GetComponentsInChildren<UnityEngine.UI.Text>(true);

                    if (tmpTexts.Length == 0 && legacyTexts.Length == 0)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabContents);
                        continue;
                    }

                    bool modified = false;

                    foreach (var tmp in tmpTexts)
                    {
                        if (config.masterTMPFont != null && tmp.font != config.masterTMPFont)
                        {
                            tmp.font = config.masterTMPFont;
                            modified = true;
                        }

                        if (config.globalCharacterSpacing != 0f)
                        {
                            tmp.characterSpacing = config.globalCharacterSpacing;
                            modified = true;
                        }
                    }

                    foreach (var txt in legacyTexts)
                    {
                        if (config.masterLegacyFont != null && txt.font != config.masterLegacyFont)
                        {
                            txt.font = config.masterLegacyFont;
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        PrefabUtility.SaveAsPrefabAsset(prefabContents, path);
                        modifiedCount++;
                        Debug.Log($"[GlobalFontEditorUtility] Updated font in prefab: {path}");
                    }
                }
                finally
                {
                    if (prefabContents != null)
                    {
                        PrefabUtility.UnloadPrefabContents(prefabContents);
                    }
                }
            }

            return modifiedCount;
        }

        public static void ApplyToActiveScene(GlobalFontConfigSO config)
        {
            if (config == null) return;

            ConfigureSceneFontManager(config);
            if (GlobalFontApplier.Instance != null)
            {
                GlobalFontApplier.Instance.ApplyGlobalFontToEverything();
            }

            var activeScene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(activeScene);
            Debug.Log($"[GlobalFontEditorUtility] Applied font to active scene: {activeScene.name}");
        }
    }
}
