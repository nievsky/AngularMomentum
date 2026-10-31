using UnityEngine;
using TMPro;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Attach to any UI element with TextMeshProUGUI to enable instant reactive language switching.
    /// Can either use a predefined translationKey from LocalizationManager or direct textRU/textEN overrides.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    [DisallowMultipleComponent]
    public class LocalizedText : MonoBehaviour
    {
        [Header("Localization Setup")]
        [Tooltip("Key defined in LocalizationManager. If specified, overrides textRU/textEN fields.")]
        [SerializeField] private string translationKey = "";

        [Header("Direct Text Fallbacks")]
        [TextArea(1, 4)]
        [SerializeField] private string textRU = "";
        [TextArea(1, 4)]
        [SerializeField] private string textEN = "";

        private TextMeshProUGUI _tmpText;

        public string TranslationKey
        {
            get => translationKey;
            set
            {
                translationKey = value;
                RefreshText();
            }
        }

        public void SetDirectTexts(string ru, string en)
        {
            textRU = ru;
            textEN = en;
            RefreshText();
        }

        private void Awake()
        {
            _tmpText = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
            RefreshText();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged(GameLanguage lang)
        {
            RefreshText();
        }

        public void RefreshText()
        {
            if (_tmpText == null) _tmpText = GetComponent<TextMeshProUGUI>();
            if (_tmpText == null) return;

            if (!string.IsNullOrEmpty(translationKey))
            {
                _tmpText.text = LocalizationManager.Get(translationKey);
            }
            else if (!string.IsNullOrEmpty(textRU) || !string.IsNullOrEmpty(textEN))
            {
                _tmpText.text = LocalizationManager.IsRussian ? textRU : textEN;
            }
        }
    }
}
