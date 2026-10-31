using UnityEngine;
using UnityEditor;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Editor
{
    public class GlobalFontEditorWindow : EditorWindow
    {
        private GlobalFontConfigSO _config;
        private Vector2 _scrollPos;

        [MenuItem("Tools/Orbital Panopticon/Global Font Manager Window", priority = 19)]
        public static void OpenWindow()
        {
            var window = GetWindow<GlobalFontEditorWindow>("Font Manager");
            window.minSize = new Vector2(400, 480);
            window.Show();
        }

        private void OnEnable()
        {
            _config = GlobalFontEditorUtility.GetOrCreateConfig();
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Orbital Panopticon — Typography Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "One-click typography manager. Changes here or in GlobalFontConfig.asset automatically update all TextMeshPro and UI Text components across all scenes and UI prefabs.",
                MessageType.Info);

            EditorGUILayout.Space(10);

            _config = (GlobalFontConfigSO)EditorGUILayout.ObjectField("Font Config Asset", _config, typeof(GlobalFontConfigSO), false);

            if (_config == null)
            {
                if (GUILayout.Button("Create / Locate GlobalFontConfig"))
                {
                    _config = GlobalFontEditorUtility.GetOrCreateConfig();
                }
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Font Assets", EditorStyles.boldLabel);
            _config.masterTMPFont = (TMP_FontAsset)EditorGUILayout.ObjectField("Master TMP Font", _config.masterTMPFont, typeof(TMP_FontAsset), false);
            _config.masterLegacyFont = (Font)EditorGUILayout.ObjectField("Master Legacy Font", _config.masterLegacyFont, typeof(Font), false);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Formatting Settings", EditorStyles.boldLabel);
            _config.globalFontScaleMultiplier = EditorGUILayout.Slider("Font Scale Multiplier", _config.globalFontScaleMultiplier, 0.5f, 2.5f);
            _config.globalCharacterSpacing = EditorGUILayout.Slider("Character Spacing", _config.globalCharacterSpacing, -10f, 30f);
            _config.autoApplyOnValidate = EditorGUILayout.Toggle("Auto Apply In Editor", _config.autoApplyOnValidate);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_config);
                if (_config.autoApplyOnValidate)
                {
                    GlobalFontEditorUtility.ApplyToActiveScene(_config);
                }
            }

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Batch Actions", EditorStyles.boldLabel);

            if (GUILayout.Button("Apply To Active Scene", GUILayout.Height(32)))
            {
                GlobalFontEditorUtility.ApplyToActiveScene(_config);
            }

            EditorGUILayout.Space(3);

            if (GUILayout.Button("Apply To All Prefabs", GUILayout.Height(32)))
            {
                int count = GlobalFontEditorUtility.ApplyToPrefabs(_config);
                EditorUtility.DisplayDialog("Font Manager", $"Applied global font to {count} prefabs.", "OK");
            }

            EditorGUILayout.Space(5);

            GUI.backgroundColor = new Color(0.2f, 0.9f, 0.8f);
            if (GUILayout.Button("★ Apply To All Scenes & Prefabs (One-Click)", GUILayout.Height(40)))
            {
                GlobalFontEditorUtility.ApplyToAllScenesAndPrefabs(_config);
                EditorUtility.DisplayDialog("Font Manager", "Successfully applied global font to all scenes and prefabs!", "OK");
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(15);
            EditorGUILayout.LabelField("Active Scene Inspection", EditorStyles.boldLabel);

            TMP_Text[] activeTMPs = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            UnityEngine.UI.Text[] activeLegacies = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include);

            EditorGUILayout.LabelField($"Active Scene TMP_Text Elements: {activeTMPs.Length}");
            EditorGUILayout.LabelField($"Active Scene Legacy Text Elements: {activeLegacies.Length}");

            EditorGUILayout.EndScrollView();
        }
    }
}
