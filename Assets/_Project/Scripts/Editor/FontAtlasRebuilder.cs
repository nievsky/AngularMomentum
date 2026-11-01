using UnityEngine;
using UnityEditor;
using TMPro;

namespace OrbitalPanopticon.Editor
{
    public static class FontAtlasRebuilder
    {
        public const string SourceFontPath = "Assets/_Project/Fonts/MasterCyrillicFont.ttf";
        public const string SdfFontPath = "Assets/_Project/Fonts/MasterCyrillicFont_SDF.asset";

        public const string PreloadCharacters = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя№—–«»•●<>[]()_+#%";

        [MenuItem("Tools/Angular Momentum/Rebuild Master Cyrillic Font SDF Asset", false, 10)]
        public static TMP_FontAsset RebuildFontAsset()
        {
            AssetDatabase.ImportAsset(SourceFontPath, ImportAssetOptions.ForceUpdate);
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[FontAtlasRebuilder] Source font not found at: {SourceFontPath}");
                return null;
            }

            TMP_FontAsset existingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SdfFontPath);
            TMP_FontAsset sdfFont;

            if (existingFont != null)
            {
                sdfFont = existingFont;
            }
            else
            {
                sdfFont = TMP_FontAsset.CreateFontAsset(
                    sourceFont, 44, 5,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                    1024, 1024,
                    AtlasPopulationMode.Dynamic
                );
                AssetDatabase.CreateAsset(sdfFont, SdfFontPath);
            }

            if (sdfFont.atlasTexture != null && !AssetDatabase.IsSubAsset(sdfFont.atlasTexture))
            {
                sdfFont.atlasTexture.name = "MasterCyrillicFont_SDF Atlas";
                AssetDatabase.AddObjectToAsset(sdfFont.atlasTexture, sdfFont);
            }

            if (sdfFont.material != null && !AssetDatabase.IsSubAsset(sdfFont.material))
            {
                sdfFont.material.name = "MasterCyrillicFont_SDF Material";
                AssetDatabase.AddObjectToAsset(sdfFont.material, sdfFont);
            }

            sdfFont.TryAddCharacters(PreloadCharacters, out string missingChars);
            if (!string.IsNullOrEmpty(missingChars))
            {
                Debug.LogWarning($"[FontAtlasRebuilder] Missing characters: {missingChars}");
            }

            EditorUtility.SetDirty(sdfFont);
            if (sdfFont.atlasTexture != null) EditorUtility.SetDirty(sdfFont.atlasTexture);
            if (sdfFont.material != null) EditorUtility.SetDirty(sdfFont.material);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(SdfFontPath, ImportAssetOptions.ForceUpdate);

            Debug.Log($"<color=#00FF88>[FontAtlasRebuilder] Master Cyrillic SDF Font successfully baked! Characters: {sdfFont.characterTable.Count}</color>");
            return sdfFont;
        }
    }
}
