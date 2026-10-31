using System;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.UI
{
    [DisallowMultipleComponent]
    public class MainMenuUIController : MonoBehaviour
    {
        public static MainMenuUIController Instance { get; private set; }

        // UI Audio Delegates
        public static event Action OnButtonHovered;
        public static event Action OnButtonClicked;
        public static event Action OnStartGameClicked;

        public static void TriggerHover() => OnButtonHovered?.Invoke();
        public static void TriggerClick() => OnButtonClicked?.Invoke();
        public static void TriggerStartGame() => OnStartGameClicked?.Invoke();

        [Header("Main Menu Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Settings Sub-Panel")]
        [SerializeField] private GameObject settingsPanel;
        [FormerlySerializedAs("masterVolumeSlider")]
        [SerializeField] private Slider musicVolumeSlider;
        [FormerlySerializedAs("masterVolumeValueText")]
        [SerializeField] private TextMeshProUGUI musicVolumeValueText;

        [SerializeField] private Slider soundsVolumeSlider;
        [SerializeField] private TextMeshProUGUI soundsVolumeValueText;
        [SerializeField] private Slider mouseSensSlider;
        [SerializeField] private TextMeshProUGUI mouseSensValueText;
        [SerializeField] private Button closeSettingsButton;

        [Header("Language Switcher")]
        [SerializeField] private GameObject languageSwitcherPanel;
        [SerializeField] private Button btnLangRU;
        [SerializeField] private Button btnLangEN;
        [SerializeField] private Outline outlineLangRU;
        [SerializeField] private Outline outlineLangEN;

        [Header("Localized Labels (Auto-Resolved if Unassigned)")]
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private TextMeshProUGUI startButtonText;
        [SerializeField] private TextMeshProUGUI settingsButtonText;
        [SerializeField] private TextMeshProUGUI creditsButtonText;
        [SerializeField] private TextMeshProUGUI quitButtonText;
        [SerializeField] private TextMeshProUGUI settingsTitleText;
        [FormerlySerializedAs("masterVolumeLabelText")]
        [SerializeField] private TextMeshProUGUI musicVolumeLabelText;
        [SerializeField] private TextMeshProUGUI soundsVolumeLabelText;

        public Slider MasterVolumeSlider => musicVolumeSlider;
        public Slider MusicVolumeSlider => musicVolumeSlider;
        public Slider SoundsVolumeSlider => soundsVolumeSlider;
        [SerializeField] private TextMeshProUGUI mouseSensLabelText;
        [SerializeField] private TextMeshProUGUI closeSettingsButtonText;

        private void Awake()
        {
            Instance = this;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Hook up audio hover triggers on all menu buttons
            SetupButtonHoverAudio(startButton);
            SetupButtonHoverAudio(settingsButton);
            SetupButtonHoverAudio(creditsButton);
            SetupButtonHoverAudio(quitButton);
            SetupButtonHoverAudio(closeSettingsButton);

            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (creditsButton != null) creditsButton.onClick.AddListener(OnCreditsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnCloseSettingsClicked);

            LoadSettings();

            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.onValueChanged.RemoveAllListeners();
                musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
            }
            if (soundsVolumeSlider != null)
            {
                soundsVolumeSlider.onValueChanged.RemoveAllListeners();
                soundsVolumeSlider.onValueChanged.AddListener(OnSoundsVolumeChanged);
            }
            if (mouseSensSlider != null) mouseSensSlider.onValueChanged.AddListener(OnMouseSensChanged);

            if (settingsPanel != null) settingsPanel.SetActive(false);

            EnsureLanguageSwitcher();
            ResolveLabelReferences();
            UpdateLocalizedTexts();

            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            if (Instance == this) Instance = null;
        }

        private void HandleLanguageChanged(GameLanguage lang)
        {
            UpdateLocalizedTexts();
            UpdateLanguageSwitcherVisuals();
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void ResolveLabelReferences()
        {
            if (subtitleText == null)
            {
                var go = GameObject.Find("Text_Subtitle");
                if (go != null) subtitleText = go.GetComponent<TextMeshProUGUI>();
            }
            if (startButtonText == null && startButton != null) startButtonText = startButton.GetComponentInChildren<TextMeshProUGUI>();
            if (settingsButtonText == null && settingsButton != null) settingsButtonText = settingsButton.GetComponentInChildren<TextMeshProUGUI>();
            if (creditsButtonText == null && creditsButton != null) creditsButtonText = creditsButton.GetComponentInChildren<TextMeshProUGUI>();
            if (quitButtonText == null && quitButton != null) quitButtonText = quitButton.GetComponentInChildren<TextMeshProUGUI>();

            if (settingsPanel != null)
            {
                if (settingsTitleText == null)
                {
                    var t = settingsPanel.transform.Find("Title");
                    if (t != null) settingsTitleText = t.GetComponent<TextMeshProUGUI>();
                }
                if (closeSettingsButtonText == null && closeSettingsButton != null)
                {
                    closeSettingsButtonText = closeSettingsButton.GetComponentInChildren<TextMeshProUGUI>();
                }
            }
        }

        public void UpdateLocalizedTexts()
        {
            if (subtitleText != null) subtitleText.text = LocalizationManager.Get("MM_Subtitle");
            if (startButtonText != null) startButtonText.text = LocalizationManager.Get("MM_Start");
            if (settingsButtonText != null) settingsButtonText.text = LocalizationManager.Get("MM_Settings");
            if (creditsButtonText != null) creditsButtonText.text = LocalizationManager.Get("MM_Credits");
            if (quitButtonText != null) quitButtonText.text = LocalizationManager.Get("MM_Quit");

            if (settingsTitleText != null) settingsTitleText.text = LocalizationManager.Get("MM_Settings_Title");
            if (musicVolumeLabelText != null) musicVolumeLabelText.text = LocalizationManager.Get("MM_Settings_MusicVolume");
            if (soundsVolumeLabelText != null) soundsVolumeLabelText.text = LocalizationManager.Get("MM_Settings_SoundsVolume");
            if (mouseSensLabelText != null) mouseSensLabelText.text = LocalizationManager.Get("MM_Settings_Sens");
            if (closeSettingsButtonText != null) closeSettingsButtonText.text = LocalizationManager.Get("MM_Settings_Close");
        }

        public void EnsureLanguageSwitcher()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            if (languageSwitcherPanel == null)
            {
                Transform existing = canvas.transform.Find("LanguageSwitcherPanel");
                if (existing != null) languageSwitcherPanel = existing.gameObject;
            }

            if (languageSwitcherPanel == null)
            {
                GameObject panelGo = new GameObject("LanguageSwitcherPanel", typeof(RectTransform), typeof(Image), typeof(Outline));
                panelGo.transform.SetParent(canvas.transform, false);

                RectTransform rt = panelGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-40f, 40f);
                rt.sizeDelta = new Vector2(175f, 48f);

                Image bg = panelGo.GetComponent<Image>();
                bg.color = new Color(0.04f, 0.08f, 0.14f, 0.92f);

                Outline outline = panelGo.GetComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.85f, 1f, 0.8f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                languageSwitcherPanel = panelGo;

                // RU Flag Button
                btnLangRU = CreateFlagButton(panelGo.transform, "Btn_Lang_RU", new Vector2(-42f, 0f), "RU", new Color(0.2f, 0.55f, 1f));
                outlineLangRU = btnLangRU.GetComponent<Outline>();

                // EN Flag Button
                btnLangEN = CreateFlagButton(panelGo.transform, "Btn_Lang_EN", new Vector2(42f, 0f), "EN", new Color(1f, 0.35f, 0.35f));
                outlineLangEN = btnLangEN.GetComponent<Outline>();
            }

            if (btnLangRU != null)
            {
                btnLangRU.onClick.RemoveAllListeners();
                btnLangRU.onClick.AddListener(() => OnLanguageButtonClicked(GameLanguage.RU));
            }
            if (btnLangEN != null)
            {
                btnLangEN.onClick.RemoveAllListeners();
                btnLangEN.onClick.AddListener(() => OnLanguageButtonClicked(GameLanguage.EN));
            }

            UpdateLanguageSwitcherVisuals();
        }

        private Button CreateFlagButton(Transform parent, string name, Vector2 pos, string code, Color accent)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
            btnGo.transform.SetParent(parent, false);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(72f, 32f);

            Image img = btnGo.GetComponent<Image>();
            img.color = new Color(0.08f, 0.13f, 0.20f, 0.95f);

            Outline outline = btnGo.GetComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.effectColor = accent;

            var animator = btnGo.GetComponent<UIButtonTweenAnimator>();
            animator.SetOriginalScale(Vector3.one);

            Button btn = btnGo.GetComponent<Button>();

            GameObject textGo = new GameObject("Text_Code", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);

            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;

            TextMeshProUGUI tmp = textGo.GetComponent<TextMeshProUGUI>();
            tmp.text = $"<b>[{code}]</b>";
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            SetupButtonHoverAudio(btn);
            return btn;
        }

        public static void SetupButtonHoverAudio(Button btn)
        {
            if (btn == null) return;

            EventTrigger trigger = btn.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) =>
            {
                TriggerHover();
            });
            trigger.triggers.Add(enterEntry);
        }

        private void OnLanguageButtonClicked(GameLanguage lang)
        {
            TriggerClick();
            LocalizationManager.SetLanguage(lang);
            Button activeBtn = (lang == GameLanguage.RU) ? btnLangRU : btnLangEN;
            if (activeBtn != null)
            {
                activeBtn.transform.DOKill();
                activeBtn.transform.localScale = Vector3.one;
                activeBtn.transform.DOPunchScale(Vector3.one * 0.18f, 0.22f, 6, 0.5f).SetUpdate(true);
            }
        }

        public void UpdateLanguageSwitcherVisuals()
        {
            bool isRU = LocalizationManager.IsRussian;

            if (outlineLangRU != null)
            {
                outlineLangRU.effectColor = isRU ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.3f, 0.4f, 0.5f, 0.4f);
                outlineLangRU.effectDistance = isRU ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
            if (btnLangRU != null)
            {
                btnLangRU.transform.localScale = isRU ? Vector3.one * 1.05f : Vector3.one * 0.95f;
            }

            if (outlineLangEN != null)
            {
                outlineLangEN.effectColor = !isRU ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.3f, 0.4f, 0.5f, 0.4f);
                outlineLangEN.effectDistance = !isRU ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
            }
            if (btnLangEN != null)
            {
                btnLangEN.transform.localScale = !isRU ? Vector3.one * 1.05f : Vector3.one * 0.95f;
            }
        }

        public void EnsureVolumeSliders()
        {
            if (settingsPanel == null) return;

            // 1. Expand settings panel height if needed so 3 rows + close button breathe comfortably
            RectTransform panelRt = settingsPanel.GetComponent<RectTransform>();
            if (panelRt != null && panelRt.sizeDelta.y < 380f)
            {
                panelRt.sizeDelta = new Vector2(panelRt.sizeDelta.x, 380f);
            }

            // 2. Identify music row (the existing volume row)
            Transform musicRow = (musicVolumeSlider != null) ? musicVolumeSlider.transform.parent : null;
            if (musicRow == null)
            {
                musicRow = settingsPanel.transform.Find("Row_MusicVolume");
                if (musicRow == null)
                {
                    for (int i = 0; i < settingsPanel.transform.childCount; i++)
                    {
                        var child = settingsPanel.transform.GetChild(i);
                        if (child.name.IndexOf("Volume", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            child.name.IndexOf("Громкость", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            musicRow = child;
                            break;
                        }
                    }
                }
            }

            if (musicRow != null)
            {
                musicRow.name = "Row_MusicVolume";
                RectTransform mRt = musicRow.GetComponent<RectTransform>();
                if (mRt != null) mRt.anchoredPosition = new Vector2(0f, 55f);

                if (musicVolumeSlider == null) musicVolumeSlider = musicRow.GetComponentInChildren<Slider>();
                if (musicVolumeValueText == null)
                {
                    var valTr = musicRow.Find("Value");
                    if (valTr != null) musicVolumeValueText = valTr.GetComponent<TextMeshProUGUI>();
                }
                if (musicVolumeLabelText == null)
                {
                    var lblTr = musicRow.Find("Label");
                    if (lblTr != null) musicVolumeLabelText = lblTr.GetComponent<TextMeshProUGUI>();
                }
                if (musicVolumeLabelText != null)
                {
                    musicVolumeLabelText.text = LocalizationManager.Get("MM_Settings_MusicVolume");
                }
            }

            // 3. Find or instantiate sounds row
            Transform soundsRow = (soundsVolumeSlider != null) ? soundsVolumeSlider.transform.parent : null;
            if (soundsRow == null)
            {
                soundsRow = settingsPanel.transform.Find("Row_SoundsVolume");
            }

            if (soundsRow == null && musicRow != null)
            {
                GameObject soundsGo = Instantiate(musicRow.gameObject, settingsPanel.transform, false);
                soundsGo.name = "Row_SoundsVolume";
                soundsRow = soundsGo.transform;

                // Re-bind components on newly created row
                soundsVolumeSlider = soundsRow.GetComponentInChildren<Slider>();
                var valTr = soundsRow.Find("Value");
                if (valTr != null) soundsVolumeValueText = valTr.GetComponent<TextMeshProUGUI>();
                var lblTr = soundsRow.Find("Label");
                if (lblTr != null) soundsVolumeLabelText = lblTr.GetComponent<TextMeshProUGUI>();
            }

            if (soundsRow != null)
            {
                RectTransform sRt = soundsRow.GetComponent<RectTransform>();
                if (sRt != null) sRt.anchoredPosition = new Vector2(0f, -5f);

                if (soundsVolumeSlider == null) soundsVolumeSlider = soundsRow.GetComponentInChildren<Slider>();
                if (soundsVolumeValueText == null)
                {
                    var valTr = soundsRow.Find("Value");
                    if (valTr != null) soundsVolumeValueText = valTr.GetComponent<TextMeshProUGUI>();
                }
                if (soundsVolumeLabelText == null)
                {
                    var lblTr = soundsRow.Find("Label");
                    if (lblTr != null) soundsVolumeLabelText = lblTr.GetComponent<TextMeshProUGUI>();
                }
                if (soundsVolumeLabelText != null)
                {
                    soundsVolumeLabelText.text = LocalizationManager.Get("MM_Settings_SoundsVolume");
                }
            }

            // 4. Move Mouse Sens Row down
            if (mouseSensSlider != null && mouseSensSlider.transform.parent != null)
            {
                RectTransform sensRt = mouseSensSlider.transform.parent.GetComponent<RectTransform>();
                if (sensRt != null) sensRt.anchoredPosition = new Vector2(0f, -65f);
            }

            // 5. Move Close Settings Button down
            if (closeSettingsButton != null)
            {
                RectTransform closeRt = closeSettingsButton.GetComponent<RectTransform>();
                if (closeRt != null) closeRt.anchoredPosition = new Vector2(0f, -135f);
            }
        }

        private void LoadSettings()
        {
            EnsureVolumeSliders();

            // 1. Music Volume (Default: 100% / 1.0)
            float musicVol = PlayerPrefs.GetFloat("Settings_MusicVolume", 1.0f);
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.minValue = 0f;
                musicVolumeSlider.maxValue = 1f;
                musicVolumeSlider.value = musicVol;
            }
            if (musicVolumeValueText != null)
            {
                musicVolumeValueText.text = $"{Mathf.RoundToInt(musicVol * 100f)}%";
            }
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.SetMusicVolume(musicVol);
            }

            // 2. Sounds Volume (Default: 100% / 1.0)
            float soundsVol = PlayerPrefs.GetFloat("Settings_SoundsVolume", 1.0f);
            if (soundsVolumeSlider != null)
            {
                soundsVolumeSlider.minValue = 0f;
                soundsVolumeSlider.maxValue = 1f;
                soundsVolumeSlider.value = soundsVol;
            }
            if (soundsVolumeValueText != null)
            {
                soundsVolumeValueText.text = $"{Mathf.RoundToInt(soundsVol * 100f)}%";
            }
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.SetSoundsVolume(soundsVol);
            }
            AudioListener.volume = soundsVol;

            // 3. Mouse Sensitivity
            float sens = PlayerPrefs.GetFloat("Settings_MouseSens", 1.8f);
            if (mouseSensSlider != null)
            {
                mouseSensSlider.value = sens;
            }
            if (mouseSensValueText != null)
            {
                mouseSensValueText.text = sens.ToString("F1");
            }

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null)
            {
                config.mouseSensitivity = sens;
            }
        }

        private void OnMusicVolumeChanged(float val)
        {
            val = Mathf.Clamp01(val);
            PlayerPrefs.SetFloat("Settings_MusicVolume", val);
            PlayerPrefs.Save();
            if (musicVolumeValueText != null)
            {
                musicVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            }
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.SetMusicVolume(val);
            }
        }

        private void OnSoundsVolumeChanged(float val)
        {
            val = Mathf.Clamp01(val);
            PlayerPrefs.SetFloat("Settings_SoundsVolume", val);
            PlayerPrefs.Save();
            AudioListener.volume = val;
            if (soundsVolumeValueText != null)
            {
                soundsVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            }
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.SetSoundsVolume(val);
            }
        }
        private void OnMouseSensChanged(float val)
        {
            PlayerPrefs.SetFloat("Settings_MouseSens", val);
            PlayerPrefs.Save();
            if (mouseSensValueText != null)
            {
                mouseSensValueText.text = val.ToString("F1");
            }

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null)
            {
                config.mouseSensitivity = val;
            }
        }

        public void OnStartClicked()
        {
            TriggerStartGame();

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("GameplayScene");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("GameplayScene");
            }
        }

        public void OnSettingsClicked()
        {
            TriggerClick();

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(!settingsPanel.activeSelf);
            }
        }

        public void OnCloseSettingsClicked()
        {
            TriggerClick();

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        public void OnCreditsClicked()
        {
            TriggerClick();

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("CreditsScene");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("CreditsScene");
            }
        }

        public void OnQuitClicked()
        {
            TriggerClick();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
