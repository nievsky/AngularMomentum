using System.IO;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    [InitializeOnLoad]
    public static class SetupTutorialUI
    {
        private const string OtfPath = "Assets/TextMesh Pro/Fonts/Mayak Ultra Extended Medium.otf";
        private const string SdfPath = "Assets/TextMesh Pro/Fonts/Mayak SDF.asset";
        private const string ResourcesSdfPath = "Assets/Resources/Mayak SDF.asset";
        private const string ScenePath = "Assets/_Project/Scenes/GameplayScene.unity";

        static SetupTutorialUI()
        {
            EditorApplication.delayCall += Initialize;
        }

        [MenuItem("Tools/Orbital Panopticon/Setup New Tutorial UI (Mayak & 4 Slides)")]
        public static void Initialize()
        {
            Debug.Log("<color=#FFBA00>[SetupTutorialUI] Starting Tutorial UI setup...</color>");

            // 1. Ensure Mayak SDF font asset exists
            TMP_FontAsset fontAsset = EnsureMayakFontAsset();

            // 2. Load the 4 slide sprites
            Sprite[] slideSprites = new Sprite[4];
            for (int i = 0; i < 4; i++)
            {
                string slidePath = $"Assets/_Project/Textures/UI/Tutorial/Tutorial_Slide_{i + 1}.png";
                slideSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(slidePath);
                if (slideSprites[i] == null)
                {
                    TextureImporter ti = AssetImporter.GetAtPath(slidePath) as TextureImporter;
                    if (ti != null && ti.textureType != TextureImporterType.Sprite)
                    {
                        ti.textureType = TextureImporterType.Sprite;
                        ti.spriteImportMode = SpriteImportMode.Single;
                        ti.alphaIsTransparency = true;
                        ti.maxTextureSize = 2048;
                        ti.SaveAndReimport();
                    }
                    slideSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(slidePath);
                }
                if (slideSprites[i] == null)
                {
                    slideSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Resources/Tutorial/Tutorial_Slide_{i + 1}.png");
                }
                Debug.Log($"[SetupTutorialUI] Slide {i + 1}: {(slideSprites[i] != null ? slideSprites[i].name : "NULL")}");
            }

            // 3. Update GameplayScene
            if (File.Exists(ScenePath))
            {
                bool isCurrent = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == ScenePath;
                var scene = isCurrent
                    ? UnityEngine.SceneManagement.SceneManager.GetActiveScene()
                    : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                TutorialSlideshowUI tutorialUI = Object.FindAnyObjectByType<TutorialSlideshowUI>();
                if (tutorialUI != null)
                {
                    tutorialUI.ConfigureNewTutorial(slideSprites, fontAsset);
                    EditorUtility.SetDirty(tutorialUI);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("<color=#FFBA00>[SetupTutorialUI] Successfully configured TutorialSlideshowUI in GameplayScene!</color>");
                }
                else
                {
                    Debug.LogWarning("[SetupTutorialUI] TutorialSlideshowUI component not found in GameplayScene.");
                }
            }

            AssetDatabase.SaveAssets();
        }

        public static TMP_FontAsset EnsureMayakFontAsset()
        {
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfPath);
            if (fontAsset == null)
            {
                fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ResourcesSdfPath);
            }

            if (fontAsset != null)
            {
                Debug.Log($"[SetupTutorialUI] Found existing Mayak SDF font asset at {fontAsset.name}");
                return fontAsset;
            }

            Font font = AssetDatabase.LoadAssetAtPath<Font>(OtfPath);
            if (font == null)
            {
                Debug.LogError($"[SetupTutorialUI] Could not load Font from {OtfPath}");
                return null;
            }

            Debug.Log("[SetupTutorialUI] Creating new Mayak SDF font asset...");
            fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset == null)
            {
                Debug.LogError("[SetupTutorialUI] TMP_FontAsset.CreateFontAsset returned null!");
                return null;
            }

            AssetDatabase.CreateAsset(fontAsset, SdfPath);
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            if (fontAsset.material != null)
            {
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(SdfPath);

            try
            {
                AssetDatabase.CopyAsset(SdfPath, ResourcesSdfPath);
                AssetDatabase.SaveAssets();
            }
            catch { }

            Debug.Log($"<color=#FFBA00>[SetupTutorialUI] Successfully created and saved Mayak SDF at {SdfPath}</color>");

            return fontAsset;
        }
    }
}
