using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Centralized manager and runtime applier for project-wide typography.
    /// Updates all TextMeshPro and Legacy UI Text elements across active scenes, UI panels, and dynamic popups.
    /// </summary>
    public class GlobalFontApplier : MonoBehaviour
    {
        public static GlobalFontApplier Instance { get; private set; }

        private static readonly Dictionary<TMP_Text, (float size, float spacing)> _tmpBaseValues = new();
        private static readonly Dictionary<UnityEngine.UI.Text, float> _legacyBaseValues = new();

        [Header("Configuration")]
        [SerializeField] private GlobalFontConfigSO fontConfig;

        public GlobalFontConfigSO FontConfig
        {
            get => fontConfig;
            set
            {
                fontConfig = value;
                ApplyGlobalFontToEverything();
            }
        }

        public static void ApplyToAll()
        {
            var applier = Instance ?? Object.FindAnyObjectByType<GlobalFontApplier>();
            if (applier != null)
            {
                applier.ApplyGlobalFontToEverything();
            }
        }

        private void Awake()
        {
            if (!Application.isPlaying) return;

            if (Instance == null)
            {
                Instance = this;
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureConfigLoaded();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;

            if (Instance == null)
            {
                Instance = this;
            }

            EnsureConfigLoaded();
            GlobalFontConfigSO.OnConfigChanged += OnConfigChanged;

            // Apply on enable
            ApplyGlobalFontToEverything();
        }

        private void OnDisable()
        {
            GlobalFontConfigSO.OnConfigChanged -= OnConfigChanged;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyGlobalFontToEverything();
        }

        private void OnConfigChanged()
        {
            ApplyGlobalFontToEverything();
        }

        public void EnsureConfigLoaded()
        {
            if (fontConfig == null)
            {
#if UNITY_EDITOR
                fontConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<GlobalFontConfigSO>(
                    "Assets/_Project/ScriptableObjects/GlobalFontConfig.asset");
#endif
                if (fontConfig == null)
                {
                    fontConfig = Resources.Load<GlobalFontConfigSO>("GlobalFontConfig");
                }
            }
        }

        /// <summary>
        /// Scans the entire active scene hierarchy (including inactive panels) and applies the global font.
        /// </summary>
        [ContextMenu("Apply Global Font Now")]
        public void ApplyGlobalFontToEverything()
        {
#if UNITY_EDITOR
            if (UnityEditor.BuildPipeline.isBuildingPlayer || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
            EnsureConfigLoaded();
            if (fontConfig == null) return;

            // 1. TextMeshPro components (covers TextMeshProUGUI and TextMeshPro 3D)
            TMP_Text[] allTMPTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            foreach (var tmp in allTMPTexts)
            {
                ApplyToTMP(tmp);
            }

            // 2. Standard Legacy UI Text components
            UnityEngine.UI.Text[] allLegacyTexts = FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include);
            foreach (var txt in allLegacyTexts)
            {
                ApplyToLegacy(txt);
            }
        }

        /// <summary>
        /// Applies the global font settings to a specific TMP_Text component.
        /// </summary>
        public void ApplyToTMP(TMP_Text tmp)
        {
            if (tmp == null || fontConfig == null) return;

            // Dedicated tutorial typography check removed so all HUD & canvas elements receive valid font & material

            if (!_tmpBaseValues.TryGetValue(tmp, out var baseVal) || baseVal.size <= 0f)
            {
                baseVal = (tmp.fontSize, tmp.characterSpacing);
                _tmpBaseValues[tmp] = baseVal;
            }

            // Assign master font and enforce valid material & alpha
            if (fontConfig.masterTMPFont != null)
            {
                if (tmp.font != fontConfig.masterTMPFont)
                {
                    tmp.font = fontConfig.masterTMPFont;
                }

                if (fontConfig.masterTMPFont.material != null)
                {
                    tmp.fontSharedMaterial = fontConfig.masterTMPFont.material;
                }
            }

            tmp.enabled = true;
            if (tmp.color.a < 0.05f)
            {
                Color c = tmp.color;
                c.a = 1.0f;
                tmp.color = c;
            }

            // Apply scale multiplier
            if (fontConfig.globalFontScaleMultiplier > 0.01f && baseVal.size > 0f)
            {
                tmp.fontSize = baseVal.size * fontConfig.globalFontScaleMultiplier;
            }

            // Apply character spacing offset
            tmp.characterSpacing = baseVal.spacing + fontConfig.globalCharacterSpacing;
        }

        /// <summary>
        /// Applies the global font settings to a specific Legacy UI Text component.
        /// </summary>
        public void ApplyToLegacy(UnityEngine.UI.Text txt)
        {
            if (txt == null || fontConfig == null) return;

            if (!_legacyBaseValues.TryGetValue(txt, out float baseSize) || baseSize <= 0f)
            {
                baseSize = txt.fontSize;
                _legacyBaseValues[txt] = baseSize;
            }

            // Assign master legacy font
            if (fontConfig.masterLegacyFont != null && txt.font != fontConfig.masterLegacyFont)
            {
                txt.font = fontConfig.masterLegacyFont;
            }

            // Apply scale multiplier
            if (fontConfig.globalFontScaleMultiplier > 0.01f && baseSize > 0f)
            {
                txt.fontSize = Mathf.RoundToInt(baseSize * fontConfig.globalFontScaleMultiplier);
            }
        }

        /// <summary>
        /// Applies the global font settings to all text components within a GameObject hierarchy.
        /// Ideal for dynamically instantiated UI (e.g. Floating Score popups, Upgrade Tree nodes).
        /// </summary>
        public void ApplyToHierarchy(GameObject root)
        {
            if (root == null) return;
            EnsureConfigLoaded();
            if (fontConfig == null) return;

            TMP_Text[] tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in tmpTexts)
            {
                ApplyToTMP(tmp);
            }

            UnityEngine.UI.Text[] legacyTexts = root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
            foreach (var txt in legacyTexts)
            {
                ApplyToLegacy(txt);
            }
        }

        #region Static Convenience API

        /// <summary>
        /// Static helper to apply global fonts to any GameObject and its children.
        /// Safe to call even if GlobalFontApplier is not yet instantiated.
        /// </summary>
        public static void ApplyTo(GameObject root)
        {
            if (root == null) return;

            if (Instance != null)
            {
                Instance.ApplyToHierarchy(root);
            }
            else
            {
                var applier = Object.FindAnyObjectByType<GlobalFontApplier>();
                if (applier != null)
                {
                    applier.ApplyToHierarchy(root);
                }
                else
                {
                    var cfg = Resources.Load<GlobalFontConfigSO>("GlobalFontConfig");
#if UNITY_EDITOR
                    if (cfg == null)
                    {
                        cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GlobalFontConfigSO>("Assets/_Project/ScriptableObjects/GlobalFontConfig.asset");
                    }
#endif
                    if (cfg != null && cfg.masterTMPFont != null)
                    {
                        var tmps = root.GetComponentsInChildren<TMP_Text>(true);
                        foreach (var tmp in tmps)
                        {
                            // All HUD & canvas elements receive valid font & material
                            tmp.font = cfg.masterTMPFont;
                            if (cfg.masterTMPFont.material != null) tmp.fontSharedMaterial = cfg.masterTMPFont.material;
                            tmp.enabled = true;
                            if (tmp.color.a < 0.05f) { Color c = tmp.color; c.a = 1f; tmp.color = c; }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Static helper to apply global fonts to a component's GameObject hierarchy.
        /// </summary>
        public static void ApplyTo(Component comp)
        {
            if (comp != null)
            {
                ApplyTo(comp.gameObject);
            }
        }

        #endregion
    }
}
