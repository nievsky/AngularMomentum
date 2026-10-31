using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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
        [SerializeField] private Button btnFlagRU;
        [SerializeField] private Button btnFlagEN;
        [SerializeField] private Image imageFlagRU;
        [SerializeField] private Image imageFlagEN;
        [SerializeField] private Outline outlineFlagRU;
        [SerializeField] private Outline outlineFlagEN;
        [SerializeField] private Sprite flagSpriteRU;
        [SerializeField] private Sprite flagSpriteEN;

        public Button BtnFlagRU => btnFlagRU;
        public Button BtnFlagEN => btnFlagEN;
        public Image ImageFlagRU => imageFlagRU;
        public Image ImageFlagEN => imageFlagEN;

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

        private void SetupButtonHoverAudio(Button btn)
        {
            if (btn == null) return;
            EventTrigger trigger = btn.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerEnter;
            entry.callback.AddListener((data) => { TriggerHover(); });
            trigger.triggers.Add(entry);
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
                languageSwitcherPanel = panelGo;
            }

            RectTransform rt = languageSwitcherPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-40f, 40f);
            rt.sizeDelta = new Vector2(270f, 96f);

            Image bg = languageSwitcherPanel.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.14f, 0.92f);

            Outline outline = languageSwitcherPanel.GetComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.85f, 1f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Clean up legacy small text buttons if present
            Transform oldRu = languageSwitcherPanel.transform.Find("Btn_Lang_RU");
            if (oldRu != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(oldRu.gameObject);
                else Destroy(oldRu.gameObject);
#else
                Destroy(oldRu.gameObject);
#endif
            }
            Transform oldEn = languageSwitcherPanel.transform.Find("Btn_Lang_EN");
            if (oldEn != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(oldEn.gameObject);
                else Destroy(oldEn.gameObject);
#else
                Destroy(oldEn.gameObject);
#endif
            }

#if UNITY_EDITOR
            if (flagSpriteRU == null)
            {
                flagSpriteRU = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Textures/UI/Flag_RU.png");
            }
            if (flagSpriteEN == null)
            {
                flagSpriteEN = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Textures/UI/Flag_EN.png");
            }
#endif

            // RU Flag Button
            Transform ruTr = languageSwitcherPanel.transform.Find("Btn_Flag_RU");
            if (ruTr == null)
            {
                btnFlagRU = CreateLargeFlagButton(languageSwitcherPanel.transform, "Btn_Flag_RU", new Vector2(-65f, 0f), flagSpriteRU, out imageFlagRU, out outlineFlagRU);
            }
            else
            {
                btnFlagRU = ruTr.GetComponent<Button>();
                outlineFlagRU = ruTr.GetComponent<Outline>();
                var imgTr = ruTr.Find("Image_Flag");
                if (imgTr != null) imageFlagRU = imgTr.GetComponent<Image>();
            }

            // EN Flag Button
            Transform enTr = languageSwitcherPanel.transform.Find("Btn_Flag_EN");
            if (enTr == null)
            {
                btnFlagEN = CreateLargeFlagButton(languageSwitcherPanel.transform, "Btn_Flag_EN", new Vector2(65f, 0f), flagSpriteEN, out imageFlagEN, out outlineFlagEN);
            }
            else
            {
                btnFlagEN = enTr.GetComponent<Button>();
                outlineFlagEN = enTr.GetComponent<Outline>();
                var imgTr = enTr.Find("Image_Flag");
                if (imgTr != null) imageFlagEN = imgTr.GetComponent<Image>();
            }

            if (btnFlagRU != null)
            {
                btnFlagRU.onClick.RemoveAllListeners();
                btnFlagRU.onClick.AddListener(() => OnLanguageButtonClicked(GameLanguage.RU));
                SetupButtonHoverAudio(btnFlagRU);
            }
            if (btnFlagEN != null)
            {
                btnFlagEN.onClick.RemoveAllListeners();
                btnFlagEN.onClick.AddListener(() => OnLanguageButtonClicked(GameLanguage.EN));
                SetupButtonHoverAudio(btnFlagEN);
            }

            UpdateLanguageSwitcherVisuals();
        }

        private Button CreateLargeFlagButton(Transform parent, string name, Vector2 pos, Sprite flagSprite, out Image flagImage, out Outline borderOutline)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
            btnGo.transform.SetParent(parent, false);

            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(110f, 74f);

            Image bg = btnGo.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);

            borderOutline = btnGo.GetComponent<Outline>();
            borderOutline.effectDistance = new Vector2(2f, -2f);
            borderOutline.effectColor = new Color(0.3f, 0.4f, 0.5f, 0.4f);

            var animator = btnGo.GetComponent<UIButtonTweenAnimator>();
            animator.SetOriginalScale(Vector3.one);

            Button btn = btnGo.GetComponent<Button>();

            // Full-size Flag Image child
            GameObject flagGo = new GameObject("Image_Flag", typeof(RectTransform), typeof(Image));
            flagGo.transform.SetParent(btnGo.transform, false);

            RectTransform flagRt = flagGo.GetComponent<RectTransform>();
            flagRt.anchorMin = Vector2.zero;
            flagRt.anchorMax = Vector2.one;
            flagRt.pivot = new Vector2(0.5f, 0.5f);
            flagRt.sizeDelta = new Vector2(-8f, -8f);
            flagRt.anchoredPosition = Vector2.zero;

            flagImage = flagGo.GetComponent<Image>();
            flagImage.color = Color.white;
            flagImage.preserveAspect = true;
            if (flagSprite != null)
            {
                flagImage.sprite = flagSprite;
            }

            return btn;
        }

        private void OnLanguageButtonClicked(GameLanguage lang)
        {
            TriggerClick();
            LocalizationManager.SetLanguage(lang);
            Button activeBtn = (lang == GameLanguage.RU) ? btnFlagRU : btnFlagEN;
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

            if (outlineFlagRU != null)
            {
                outlineFlagRU.effectColor = isRU ? new Color(0.2f, 0.85f, 1f, 1f) : new Color(0.3f, 0.35f, 0.45f, 0.4f);
                outlineFlagRU.effectDistance = isRU ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
            }
            if (btnFlagRU != null)
            {
                btnFlagRU.transform.localScale = isRU ? Vector3.one * 1.05f : Vector3.one * 0.95f;
            }

            if (outlineFlagEN != null)
            {
                outlineFlagEN.effectColor = !isRU ? new Color(0.2f, 0.85f, 1f, 1f) : new Color(0.3f, 0.35f, 0.45f, 0.4f);
                outlineFlagEN.effectDistance = !isRU ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);
            }
            if (btnFlagEN != null)
            {
                btnFlagEN.transform.localScale = !isRU ? Vector3.one * 1.05f : Vector3.one * 0.95f;
            }
        }

        public void EnsureVolumeSliders()
        {
            if (settingsPanel == null) return;

            Transform musicRow = settingsPanel.transform.Find("Row_MusicVolume");
            if (musicRow == null) musicRow = settingsPanel.transform.Find("Row_MasterVolume");
            Transform soundsRow = settingsPanel.transform.Find("Row_SoundsVolume");

            if (musicRow != null)
            {
                musicRow.name = "Row_MusicVolume";
                RectTransform rtMusic = musicRow.GetComponent<RectTransform>();
                if (rtMusic != null) rtMusic.anchoredPosition = new Vector2(0f, 60f);

                if (musicVolumeSlider == null) musicVolumeSlider = musicRow.GetComponentInChildren<Slider>();
                if (musicVolumeValueText == null)
                {
                    Transform valTr = musicRow.Find("ValueText");
                    if (valTr != null) musicVolumeValueText = valTr.GetComponent<TextMeshProUGUI>();
                }
                if (musicVolumeLabelText == null)
                {
                    Transform lblTr = musicRow.Find("Label");
                    if (lblTr != null) musicVolumeLabelText = lblTr.GetComponent<TextMeshProUGUI>();
                }
            }

            if (soundsRow == null && musicRow != null)
            {
                GameObject soundsGo = Instantiate(musicRow.gameObject, settingsPanel.transform);
                soundsGo.name = "Row_SoundsVolume";
                soundsRow = soundsGo.transform;

                RectTransform rtSounds = soundsRow.GetComponent<RectTransform>();
                if (rtSounds != null) rtSounds.anchoredPosition = new Vector2(0f, 0f);

                soundsVolumeSlider = soundsGo.GetComponentInChildren<Slider>();
                Transform valTr = soundsGo.transform.Find("ValueText");
                if (valTr != null) soundsVolumeValueText = valTr.GetComponent<TextMeshProUGUI>();
                Transform lblTr = soundsGo.transform.Find("Label");
                if (lblTr != null) soundsVolumeLabelText = lblTr.GetComponent<TextMeshProUGUI>();

                Transform mouseRow = settingsPanel.transform.Find("Row_MouseSens");
                if (mouseRow != null)
                {
                    RectTransform rtMouse = mouseRow.GetComponent<RectTransform>();
                    if (rtMouse != null) rtMouse.anchoredPosition = new Vector2(0f, -60f);
                }
            }
            else if (soundsRow != null)
            {
                if (soundsVolumeSlider == null) soundsVolumeSlider = soundsRow.GetComponentInChildren<Slider>();
                if (soundsVolumeValueText == null)
                {
                    Transform valTr = soundsRow.Find("ValueText");
                    if (valTr != null) soundsVolumeValueText = valTr.GetComponent<TextMeshProUGUI>();
                }
                if (soundsVolumeLabelText == null)
                {
                    Transform lblTr = soundsRow.Find("Label");
                    if (lblTr != null) soundsVolumeLabelText = lblTr.GetComponent<TextMeshProUGUI>();
                }
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
