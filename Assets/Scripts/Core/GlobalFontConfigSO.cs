using UnityEngine;
using TMPro;

namespace OrbitalPanopticon.Core
{
    [CreateAssetMenu(fileName = "GlobalFontConfig", menuName = "Orbital Panopticon/Global Font Config")]
    public class GlobalFontConfigSO : ScriptableObject
    {
        [Header("Master Font Assets")]
        [Tooltip("The primary TextMeshPro Font Asset applied to all TMP_Text elements across the project.")]
        public TMP_FontAsset masterTMPFont;

        [Tooltip("Legacy UnityEngine.UI.Text Font fallback in case standard UI Text components are used.")]
        public Font masterLegacyFont;

        [Header("Global Formatting Modifiers")]
        [Range(0.5f, 2.5f)]
        [Tooltip("Relative multiplier applied to the base font size of all text components.")]
        public float globalFontScaleMultiplier = 1.0f;

        [Range(-10f, 30f)]
        [Tooltip("Global character spacing offset applied to all TextMeshPro text components.")]
        public float globalCharacterSpacing = 0.0f;

        [Header("Editor Automation")]
        [Tooltip("Automatically push changes to active scene and prefabs when modified in the Inspector.")]
        public bool autoApplyOnValidate = true;

        public static event System.Action OnConfigChanged;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (globalFontScaleMultiplier < 0.1f)
            {
                globalFontScaleMultiplier = 1.0f;
            }

            OnConfigChanged?.Invoke();

            if (autoApplyOnValidate && !Application.isPlaying)
            {
                // Defer to delayCall to avoid modifying serialized objects during Inspector GUI repaint
                UnityEditor.EditorApplication.delayCall += () =>
                {
                    if (this == null) return;
                    if (GlobalFontApplier.Instance != null)
                    {
                        GlobalFontApplier.Instance.ApplyGlobalFontToEverything();
                    }
                };
            }
        }

        [ContextMenu("Apply Font To All Scenes & Prefabs")]
        public void TriggerApplyToAllScenesAndPrefabs()
        {
            // Call into editor utility via reflection or direct call if available
            UnityEditor.EditorApplication.ExecuteMenuItem("Tools/Orbital Panopticon/Apply Global Font To All Scenes & Prefabs");
        }
#endif
    }
}
