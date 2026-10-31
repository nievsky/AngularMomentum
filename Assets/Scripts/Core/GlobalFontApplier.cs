using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Helper component attached to Text components to track original base font size and character spacing,
    /// preventing cumulative exponential scaling when font scale multipliers are re-applied.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class GlobalFontTracker : MonoBehaviour
    {
        [HideInInspector] public float originalFontSize = -1f;
        [HideInInspector] public float originalCharacterSpacing = 0f;
        [HideInInspector] public bool isInitialized = false;
    }

    /// <summary>
    /// Centralized manager and runtime applier for project-wide typography.
    /// Updates all TextMeshPro and Legacy UI Text elements across active scenes, UI panels, and dynamic popups.
    /// </summary>
    [ExecuteAlways]
    public class GlobalFontApplier : MonoBehaviour
    {
        public static GlobalFontApplier Instance { get; private set; }

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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (Application.isPlaying)
                {
                    transform.SetParent(null);
                    DontDestroyOnLoad(gameObject);
                }
            }
            else if (Instance != this)
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                    return;
                }
                else
                {
                    Instance = this;
                }
            }

            EnsureConfigLoaded();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnEnable()
        {
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
            }
        }

        /// <summary>
        /// Scans the entire active scene hierarchy (including inactive panels) and applies the global font.
        /// </summary>
        [ContextMenu("Apply Global Font Now")]
        public void ApplyGlobalFontToEverything()
        {
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

            GlobalFontTracker tracker = tmp.GetComponent<GlobalFontTracker>();
            if (tracker == null)
            {
                tracker = tmp.gameObject.AddComponent<GlobalFontTracker>();
                tracker.originalFontSize = tmp.fontSize;
                tracker.originalCharacterSpacing = tmp.characterSpacing;
                tracker.isInitialized = true;
            }
            else if (!tracker.isInitialized || tracker.originalFontSize <= 0f)
            {
                tracker.originalFontSize = tmp.fontSize;
                tracker.originalCharacterSpacing = tmp.characterSpacing;
                tracker.isInitialized = true;
            }

            // Assign master font
            if (fontConfig.masterTMPFont != null && tmp.font != fontConfig.masterTMPFont)
            {
                tmp.font = fontConfig.masterTMPFont;
            }

            // Apply scale multiplier
            if (fontConfig.globalFontScaleMultiplier > 0.01f && tracker.originalFontSize > 0f)
            {
                tmp.fontSize = tracker.originalFontSize * fontConfig.globalFontScaleMultiplier;
            }

            // Apply character spacing offset
            tmp.characterSpacing = tracker.originalCharacterSpacing + fontConfig.globalCharacterSpacing;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(tmp);
                if (tracker != null) UnityEditor.EditorUtility.SetDirty(tracker);
            }
#endif
        }

        /// <summary>
        /// Applies the global font settings to a specific Legacy UI Text component.
        /// </summary>
        public void ApplyToLegacy(UnityEngine.UI.Text txt)
        {
            if (txt == null || fontConfig == null) return;

            GlobalFontTracker tracker = txt.GetComponent<GlobalFontTracker>();
            if (tracker == null)
            {
                tracker = txt.gameObject.AddComponent<GlobalFontTracker>();
                tracker.originalFontSize = txt.fontSize;
                tracker.isInitialized = true;
            }
            else if (!tracker.isInitialized || tracker.originalFontSize <= 0f)
            {
                tracker.originalFontSize = txt.fontSize;
                tracker.isInitialized = true;
            }

            // Assign master legacy font
            if (fontConfig.masterLegacyFont != null && txt.font != fontConfig.masterLegacyFont)
            {
                txt.font = fontConfig.masterLegacyFont;
            }

            // Apply scale multiplier
            if (fontConfig.globalFontScaleMultiplier > 0.01f && tracker.originalFontSize > 0f)
            {
                txt.fontSize = Mathf.RoundToInt(tracker.originalFontSize * fontConfig.globalFontScaleMultiplier);
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(txt);
                if (tracker != null) UnityEditor.EditorUtility.SetDirty(tracker);
            }
#endif
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
