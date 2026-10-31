using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public static class SetupSpaceSkybox
    {
        private const string SkyboxMatPath = "Assets/_Project/Materials/M_ProceduralSpaceSkybox.mat";

        [MenuItem("Tools/Orbital Panopticon/Setup Procedural Space Skybox")]
        public static void SetupAll()
        {
            Debug.Log("<color=#00FFFF>[SetupSpaceSkybox] Beginning Skybox setup...</color>");

            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMatPath);
            if (skyboxMat == null)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Materials/S_ProceduralSpaceSkybox.shader");
                if (shader != null)
                {
                    skyboxMat = new Material(shader);
                    AssetDatabase.CreateAsset(skyboxMat, SkyboxMatPath);
                }
            }

            if (skyboxMat == null)
            {
                Debug.LogError("[SetupSpaceSkybox] Failed to load or create M_ProceduralSpaceSkybox.mat!");
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
                ConfigureSceneSkybox(scenePath, skyboxMat);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("<color=#00FFCC>[SetupSpaceSkybox] Successfully configured Procedural Space Skybox across all scenes!</color>");
        }

        private static void ConfigureSceneSkybox(string scenePath, Material skyboxMat)
        {
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Assign skybox to RenderSettings
            RenderSettings.skybox = skyboxMat;

            // Find or create --- ENVIRONMENT --- GameObject
            const string envObjName = "--- ENVIRONMENT ---";
            GameObject envObj = GameObject.Find(envObjName);
            if (envObj == null)
            {
                envObj = new GameObject(envObjName);
                Undo.RegisterCreatedObjectUndo(envObj, "Create Environment GameObject");
            }

            var controller = envObj.GetComponent<SpaceSkyboxController>();
            if (controller == null)
            {
                controller = envObj.AddComponent<SpaceSkyboxController>();
            }

            controller.SkyboxMaterial = skyboxMat;
            controller.UpdateMaterialProperties();

            EditorUtility.SetDirty(envObj);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SetupSpaceSkybox] Configured {envObjName} and RenderSettings.skybox in {scenePath}");
        }
    }
}
