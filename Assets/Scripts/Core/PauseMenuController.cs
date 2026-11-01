using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using OrbitalPanopticon.UI;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class PauseMenuController : MonoBehaviour
    {
        public static PauseMenuController Instance { get; private set; }

        [Header("Root Panels")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private GameObject settingsSubPanel;

        [Header("Pause Buttons")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("Pause Texts")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI resumeText;
        [SerializeField] private TextMeshProUGUI settingsText;
        [SerializeField] private TextMeshProUGUI mainMenuText;
        [SerializeField] private TextMeshProUGUI quitText;

        [Header("Settings Controls")]
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private TextMeshProUGUI musicVolumeValueText;
        [SerializeField] private Slider soundsVolumeSlider;
        [SerializeField] private TextMeshProUGUI soundsVolumeValueText;
        [SerializeField] private Slider mouseSensSlider;
        [SerializeField] private TextMeshProUGUI mouseSensValueText;
        [SerializeField] private Button closeSettingsButton;

        [Header("Settings Texts")]
        [SerializeField] private TextMeshProUGUI settingsTitleText;
        [SerializeField] private TextMeshProUGUI musicVolumeLabelText;
        [SerializeField] private TextMeshProUGUI soundsVolumeLabelText;
        [SerializeField] private TextMeshProUGUI mouseSensLabelText;
        [SerializeField] private TextMeshProUGUI closeSettingsText;

        private bool _isPaused = false;
        public bool IsPausedInstance => _isPaused;
        public static bool IsGamePaused => Instance != null && Instance._isPaused;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsurePauseMenuHierarchy();
            EnsureSettingsSubPanelControls();
            SetupButtonListeners();
            LoadSettings();

            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (settingsSubPanel != null) settingsSubPanel.SetActive(false);

            LocalizationManager.OnLanguageChanged += UpdateLocalizedTexts;
            UpdateLocalizedTexts();
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= UpdateLocalizedTexts;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // If Upgrade Tree is open, or was closed this exact frame via ESC, don't open Pause Menu
                if (UpgradeTreeManager.IsTreeOpen || UpgradeTreeManager.LastClosedFrame == Time.frameCount)
                {
                    var treeUI = FindAnyObjectByType<UpgradeTreeUI>();
                    if (treeUI != null && UpgradeTreeManager.IsTreeOpen)
                    {
                        treeUI.SetTreeOpen(false);
                    }
                    return;
                }

                if (settingsSubPanel != null && settingsSubPanel.activeSelf)
                {
                    OpenSettings(false);
                }
                else
                {
                    TogglePause();
                }
            }
        }

        public void TogglePause()
        {
            PauseGame(!_isPaused);
        }

        public void PauseGame(bool pause)
        {
            _isPaused = pause;

            if (_isPaused)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (pauseMenuPanel != null)
                {
                    pauseMenuPanel.SetActive(true);
                }
            }
            else
            {
                if (settingsSubPanel != null) settingsSubPanel.SetActive(false);
                if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);

                bool operatorConnected = (UIManager.Instance == null || UIManager.Instance.IsOperatorConnected);
                if (!UpgradeTreeManager.IsTreeOpen)
                {
                    Time.timeScale = 1f;
                    if (operatorConnected)
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                    }
                    else
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                    }
                }
            }
        }

        public void OpenSettings(bool open)
        {
            if (open)
            {
                EnsureSettingsSubPanelControls();
                LoadSettings();
                UpdateLocalizedTexts();
            }
            if (settingsSubPanel != null)
            {
                settingsSubPanel.SetActive(open);
            }
        }

        private void SetupButtonListeners()
        {
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveAllListeners();
                resumeButton.onClick.AddListener(() => PauseGame(false));
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveAllListeners();
                settingsButton.onClick.AddListener(() => OpenSettings(true));
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.RemoveAllListeners();
                mainMenuButton.onClick.AddListener(() =>
                {
                    PauseGame(false);
                    if (SceneTransitionManager.Instance != null)
                    {
                        SceneTransitionManager.Instance.TransitionToScene("MainMenuScene");
                    }
                    else
                    {
                        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
                    }
                });
            }

            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(() =>
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
            }

            if (closeSettingsButton != null)
            {
                closeSettingsButton.onClick.RemoveAllListeners();
                closeSettingsButton.onClick.AddListener(() => OpenSettings(false));
            }

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

            if (mouseSensSlider != null)
            {
                mouseSensSlider.onValueChanged.RemoveAllListeners();
                mouseSensSlider.onValueChanged.AddListener(OnMouseSensChanged);
            }
        }

        private void LoadSettings()
        {
            // 1. Music Volume (Default: 100% / 1.0)
            float musicVol = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_MusicVolume", 1.0f));
            if (musicVolumeSlider != null)
            {
                musicVolumeSlider.minValue = 0f;
                musicVolumeSlider.maxValue = 1f;
                musicVolumeSlider.wholeNumbers = false;
                musicVolumeSlider.value = musicVol;
            }
            if (musicVolumeValueText != null) musicVolumeValueText.text = $"{Mathf.RoundToInt(musicVol * 100f)}%";
            if (FMODAudioManager.Instance != null) FMODAudioManager.Instance.SetMusicVolume(musicVol);

            // 2. Sounds Volume (Default: 100% / 1.0)
            float soundsVol = Mathf.Clamp01(PlayerPrefs.GetFloat("Settings_SoundsVolume", 1.0f));
            if (soundsVolumeSlider != null)
            {
                soundsVolumeSlider.minValue = 0f;
                soundsVolumeSlider.maxValue = 1f;
                soundsVolumeSlider.wholeNumbers = false;
                soundsVolumeSlider.value = soundsVol;
            }
            AudioListener.volume = soundsVol;
            if (soundsVolumeValueText != null) soundsVolumeValueText.text = $"{Mathf.RoundToInt(soundsVol * 100f)}%";
            if (FMODAudioManager.Instance != null) FMODAudioManager.Instance.SetSoundsVolume(soundsVol);

            // 3. Mouse Sensitivity
            float sens = PlayerPrefs.GetFloat("Settings_MouseSens", 1.8f);
            if (mouseSensSlider != null) mouseSensSlider.value = sens;
            if (mouseSensValueText != null) mouseSensValueText.text = sens.ToString("F1");

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null) config.mouseSensitivity = sens;
        }

        private void OnMusicVolumeChanged(float val)
        {
            val = Mathf.Clamp01(val);
            PlayerPrefs.SetFloat("Settings_MusicVolume", val);
            PlayerPrefs.Save();
            if (musicVolumeValueText != null) musicVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            if (FMODAudioManager.Instance != null) FMODAudioManager.Instance.SetMusicVolume(val);
        }

        private void OnSoundsVolumeChanged(float val)
        {
            val = Mathf.Clamp01(val);
            AudioListener.volume = val;
            PlayerPrefs.SetFloat("Settings_SoundsVolume", val);
            PlayerPrefs.Save();
            if (soundsVolumeValueText != null) soundsVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
            if (FMODAudioManager.Instance != null) FMODAudioManager.Instance.SetSoundsVolume(val);
        }

        private void OnMouseSensChanged(float val)
        {
            PlayerPrefs.SetFloat("Settings_MouseSens", val);
            if (mouseSensValueText != null) mouseSensValueText.text = val.ToString("F1");

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null) config.mouseSensitivity = val;
        }

        public void UpdateLocalizedTexts(GameLanguage lang = GameLanguage.RU)
        {
            if (titleText != null) titleText.text = LocalizationManager.Get("PAUSE_Title");
            if (resumeText != null) resumeText.text = LocalizationManager.Get("PAUSE_Resume");
            if (settingsText != null) settingsText.text = LocalizationManager.Get("PAUSE_Settings");
            if (mainMenuText != null) mainMenuText.text = LocalizationManager.Get("PAUSE_MainMenu");
            if (quitText != null) quitText.text = LocalizationManager.Get("PAUSE_Quit");

            if (settingsTitleText != null) settingsTitleText.text = LocalizationManager.Get("MM_Settings_Title");
            if (musicVolumeLabelText != null) musicVolumeLabelText.text = LocalizationManager.Get("MM_Settings_MusicVolume");
            if (soundsVolumeLabelText != null) soundsVolumeLabelText.text = LocalizationManager.Get("MM_Settings_SoundsVolume");
            if (mouseSensLabelText != null) mouseSensLabelText.text = LocalizationManager.Get("MM_Settings_Sens");
            if (closeSettingsText != null) closeSettingsText.text = LocalizationManager.Get("MM_Settings_Close");
        }

        public void EnsurePauseMenuHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            if (pauseMenuPanel == null)
            {
                var existing = canvas.transform.Find("Panel_PauseMenu");
                if (existing != null) pauseMenuPanel = existing.gameObject;
            }

            if (pauseMenuPanel == null)
            {
                GameObject panelGo = new GameObject("Panel_PauseMenu", typeof(RectTransform), typeof(Image));
                panelGo.transform.SetParent(canvas.transform, false);

                RectTransform rt = panelGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                Image bg = panelGo.GetComponent<Image>();
                bg.color = new Color(0.02f, 0.02f, 0.03f, 0.90f);

                // Central Box
                GameObject boxGo = new GameObject("Box_Menu", typeof(RectTransform), typeof(Image), typeof(Outline));
                boxGo.transform.SetParent(panelGo.transform, false);

                RectTransform boxRt = boxGo.GetComponent<RectTransform>();
                boxRt.anchorMin = new Vector2(0.5f, 0.5f);
                boxRt.anchorMax = new Vector2(0.5f, 0.5f);
                boxRt.pivot = new Vector2(0.5f, 0.5f);
                boxRt.anchoredPosition = Vector2.zero;
                boxRt.sizeDelta = new Vector2(380f, 440f);

                Image boxBg = boxGo.GetComponent<Image>();
                boxBg.color = new Color(0.08f, 0.07f, 0.07f, 0.96f);

                Outline boxOutline = boxGo.GetComponent<Outline>();
                boxOutline.effectColor = new Color(1f, 0.73f, 0f, 1f);
                boxOutline.effectDistance = new Vector2(2f, -2f);

                // Title
                GameObject titleGo = new GameObject("Text_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(boxGo.transform, false);
                RectTransform titleRt = titleGo.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 1f);
                titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -28f);
                titleRt.sizeDelta = new Vector2(0f, 36f);

                titleText = titleGo.GetComponent<TextMeshProUGUI>();
                titleText.text = "ПАУЗА СИСТЕМЫ";
                titleText.alignment = TextAlignmentOptions.Center;
                titleText.fontSize = 20;
                titleText.fontStyle = FontStyles.Bold;
                titleText.color = new Color(1f, 0.73f, 0f, 1f);

                // Buttons container
                float btnStartY = -100f;
                float btnSpacing = 68f;

                Button CreateMenuButton(string name, string label, float yPos, out TextMeshProUGUI btnLabel)
                {
                    GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
                    btnGo.transform.SetParent(boxGo.transform, false);

                    RectTransform bRt = btnGo.GetComponent<RectTransform>();
                    bRt.anchorMin = new Vector2(0.5f, 1f);
                    bRt.anchorMax = new Vector2(0.5f, 1f);
                    bRt.pivot = new Vector2(0.5f, 1f);
                    bRt.anchoredPosition = new Vector2(0f, yPos);
                    bRt.sizeDelta = new Vector2(300f, 48f);

                    Image bImg = btnGo.GetComponent<Image>();
                    bImg.color = new Color(0.08f, 0.07f, 0.07f, 0.96f);

                    Outline bOut = btnGo.GetComponent<Outline>();
                    bOut.effectColor = new Color(1f, 0.73f, 0f, 1f);
                    bOut.effectDistance = new Vector2(1.5f, -1.5f);

                    var anim = btnGo.GetComponent<UIButtonTweenAnimator>();
                    anim.SetOriginalScale(Vector3.one);

                    GameObject tGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    tGo.transform.SetParent(btnGo.transform, false);
                    RectTransform tRt = tGo.GetComponent<RectTransform>();
                    tRt.anchorMin = Vector2.zero;
                    tRt.anchorMax = Vector2.one;
                    tRt.sizeDelta = Vector2.zero;

                    btnLabel = tGo.GetComponent<TextMeshProUGUI>();
                    btnLabel.text = label;
                    btnLabel.alignment = TextAlignmentOptions.Center;
                    btnLabel.fontSize = 15;
                    btnLabel.fontStyle = FontStyles.Bold;
                    btnLabel.color = Color.white;

                    return btnGo.GetComponent<Button>();
                }

                resumeButton = CreateMenuButton("Btn_Resume", "ПРОДОЛЖИТЬ", btnStartY, out resumeText);
                settingsButton = CreateMenuButton("Btn_Settings", "НАСТРОЙКИ", btnStartY - btnSpacing, out settingsText);
                mainMenuButton = CreateMenuButton("Btn_MainMenu", "В ГЛАВНОЕ МЕНЮ", btnStartY - btnSpacing * 2f, out mainMenuText);
                quitButton = CreateMenuButton("Btn_Quit", "ВЫЙТИ ИЗ ИГРЫ", btnStartY - btnSpacing * 3f, out quitText);

                pauseMenuPanel = panelGo;
            }

            // Settings Sub-Panel
            if (settingsSubPanel == null && pauseMenuPanel != null)
            {
                var existing = pauseMenuPanel.transform.Find("Panel_SettingsSub");
                if (existing != null) settingsSubPanel = existing.gameObject;
            }

            if (settingsSubPanel == null && pauseMenuPanel != null)
            {
                GameObject sGo = new GameObject("Panel_SettingsSub", typeof(RectTransform), typeof(Image), typeof(Outline));
                sGo.transform.SetParent(pauseMenuPanel.transform, false);

                RectTransform sRt = sGo.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0.5f, 0.5f);
                sRt.anchorMax = new Vector2(0.5f, 0.5f);
                sRt.pivot = new Vector2(0.5f, 0.5f);
                sRt.anchoredPosition = Vector2.zero;
                sRt.sizeDelta = new Vector2(460f, 380f);

                Image sBg = sGo.GetComponent<Image>();
                sBg.color = new Color(0.08f, 0.07f, 0.07f, 0.98f);

                Outline sOut = sGo.GetComponent<Outline>();
                sOut.effectColor = new Color(1f, 0.73f, 0f, 1f);
                sOut.effectDistance = new Vector2(2f, -2f);

                // Settings Title
                GameObject stGo = new GameObject("Text_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                stGo.transform.SetParent(sGo.transform, false);
                RectTransform stRt = stGo.GetComponent<RectTransform>();
                stRt.anchorMin = new Vector2(0f, 1f);
                stRt.anchorMax = new Vector2(1f, 1f);
                stRt.pivot = new Vector2(0.5f, 1f);
                stRt.anchoredPosition = new Vector2(0f, -24f);
                stRt.sizeDelta = new Vector2(0f, 32f);

                settingsTitleText = stGo.GetComponent<TextMeshProUGUI>();
                settingsTitleText.text = "НАСТРОЙКИ СИСТЕМЫ";
                settingsTitleText.alignment = TextAlignmentOptions.Center;
                settingsTitleText.fontSize = 18;
                settingsTitleText.fontStyle = FontStyles.Bold;
                settingsTitleText.color = new Color(1f, 0.73f, 0f, 1f);

                // Music Volume Row
                GameObject musicLabelGo = new GameObject("Label_MusicVolume", typeof(RectTransform), typeof(TextMeshProUGUI));
                musicLabelGo.transform.SetParent(sGo.transform, false);
                RectTransform mlRt = musicLabelGo.GetComponent<RectTransform>();
                mlRt.anchorMin = new Vector2(0f, 1f);
                mlRt.anchorMax = new Vector2(1f, 1f);
                mlRt.pivot = new Vector2(0f, 1f);
                mlRt.anchoredPosition = new Vector2(40f, -65f);
                mlRt.sizeDelta = new Vector2(-80f, 22f);

                musicVolumeLabelText = musicLabelGo.GetComponent<TextMeshProUGUI>();
                musicVolumeLabelText.text = LocalizationManager.Get("MM_Settings_MusicVolume");
                musicVolumeLabelText.fontSize = 12;
                musicVolumeLabelText.color = Color.white;

                // Slider Music
                GameObject msGo = CreateSimpleSlider(sGo.transform, "Slider_MusicVolume", new Vector2(0f, -90f), new Vector2(300f, 18f), 0f, 1f, out musicVolumeSlider, out musicVolumeValueText);

                // Sounds Volume Row
                GameObject soundsLabelGo = new GameObject("Label_SoundsVolume", typeof(RectTransform), typeof(TextMeshProUGUI));
                soundsLabelGo.transform.SetParent(sGo.transform, false);
                RectTransform slRt = soundsLabelGo.GetComponent<RectTransform>();
                slRt.anchorMin = new Vector2(0f, 1f);
                slRt.anchorMax = new Vector2(1f, 1f);
                slRt.pivot = new Vector2(0f, 1f);
                slRt.anchoredPosition = new Vector2(40f, -122f);
                slRt.sizeDelta = new Vector2(-80f, 22f);

                soundsVolumeLabelText = soundsLabelGo.GetComponent<TextMeshProUGUI>();
                soundsVolumeLabelText.text = LocalizationManager.Get("MM_Settings_SoundsVolume");
                soundsVolumeLabelText.fontSize = 12;
                soundsVolumeLabelText.color = Color.white;

                // Slider Sounds
                GameObject ssGo = CreateSimpleSlider(sGo.transform, "Slider_SoundsVolume", new Vector2(0f, -147f), new Vector2(300f, 18f), 0f, 1f, out soundsVolumeSlider, out soundsVolumeValueText);

                // Mouse Sens Row
                GameObject sensLabelGo = new GameObject("Label_Sens", typeof(RectTransform), typeof(TextMeshProUGUI));
                sensLabelGo.transform.SetParent(sGo.transform, false);
                RectTransform senslRt = sensLabelGo.GetComponent<RectTransform>();
                senslRt.anchorMin = new Vector2(0f, 1f);
                senslRt.anchorMax = new Vector2(1f, 1f);
                senslRt.pivot = new Vector2(0f, 1f);
                senslRt.anchoredPosition = new Vector2(40f, -179f);
                senslRt.sizeDelta = new Vector2(-80f, 22f);

                mouseSensLabelText = sensLabelGo.GetComponent<TextMeshProUGUI>();
                mouseSensLabelText.text = LocalizationManager.Get("MM_Settings_Sens");
                mouseSensLabelText.fontSize = 12;
                mouseSensLabelText.color = Color.white;

                // Slider Sens
                GameObject sensSliderGo = CreateSimpleSlider(sGo.transform, "Slider_Sens", new Vector2(0f, -204f), new Vector2(300f, 18f), 0.2f, 5f, out mouseSensSlider, out mouseSensValueText);

                // Close Settings Button
                GameObject closeBtnGo = new GameObject("Btn_CloseSettings", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
                closeBtnGo.transform.SetParent(sGo.transform, false);
                RectTransform cbRt = closeBtnGo.GetComponent<RectTransform>();
                cbRt.anchorMin = new Vector2(0.5f, 0f);
                cbRt.anchorMax = new Vector2(0.5f, 0f);
                cbRt.pivot = new Vector2(0.5f, 0f);
                cbRt.anchoredPosition = new Vector2(0f, 28f);
                cbRt.sizeDelta = new Vector2(220f, 42f);

                Image cbImg = closeBtnGo.GetComponent<Image>();
                cbImg.color = new Color(0.12f, 0.20f, 0.30f, 0.95f);

                Outline cbOut = closeBtnGo.GetComponent<Outline>();
                cbOut.effectColor = new Color(0.2f, 0.85f, 1f, 0.85f);
                cbOut.effectDistance = new Vector2(1.5f, -1.5f);

                var cbAnim = closeBtnGo.GetComponent<UIButtonTweenAnimator>();
                cbAnim.SetOriginalScale(Vector3.one);

                closeSettingsButton = closeBtnGo.GetComponent<Button>();

                GameObject cbTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                cbTextGo.transform.SetParent(closeBtnGo.transform, false);
                RectTransform cbtRt = cbTextGo.GetComponent<RectTransform>();
                cbtRt.anchorMin = Vector2.zero;
                cbtRt.anchorMax = Vector2.one;
                cbtRt.sizeDelta = Vector2.zero;

                closeSettingsText = cbTextGo.GetComponent<TextMeshProUGUI>();
                closeSettingsText.text = "ЗАКРЫТЬ";
                closeSettingsText.alignment = TextAlignmentOptions.Center;
                closeSettingsText.fontSize = 14;
                closeSettingsText.fontStyle = FontStyles.Bold;
                closeSettingsText.color = Color.white;

                settingsSubPanel = sGo;
            }

            // Fallback resolution if panel already existed
            if (pauseMenuPanel != null)
            {
                Transform box = pauseMenuPanel.transform.Find("Box_Menu");
                if (box != null)
                {
                    if (titleText == null)
                    {
                        var tTr = box.Find("Text_Title");
                        if (tTr != null) titleText = tTr.GetComponent<TextMeshProUGUI>();
                    }
                    if (resumeButton == null)
                    {
                        var btnTr = box.Find("Btn_Resume");
                        if (btnTr != null)
                        {
                            resumeButton = btnTr.GetComponent<Button>();
                            resumeText = btnTr.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }
                    if (settingsButton == null)
                    {
                        var btnTr = box.Find("Btn_Settings");
                        if (btnTr != null)
                        {
                            settingsButton = btnTr.GetComponent<Button>();
                            settingsText = btnTr.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }
                    if (mainMenuButton == null)
                    {
                        var btnTr = box.Find("Btn_MainMenu");
                        if (btnTr != null)
                        {
                            mainMenuButton = btnTr.GetComponent<Button>();
                            mainMenuText = btnTr.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }
                    if (quitButton == null)
                    {
                        var btnTr = box.Find("Btn_Quit");
                        if (btnTr != null)
                        {
                            quitButton = btnTr.GetComponent<Button>();
                            quitText = btnTr.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }
                }

                if (settingsSubPanel == null)
                {
                    Transform sTr = pauseMenuPanel.transform.Find("Panel_SettingsSub");
                    if (sTr != null) settingsSubPanel = sTr.gameObject;
                }

                if (settingsSubPanel != null)
                {
                    EnsureSettingsSubPanelControls();
                }
            }
        }

        [ContextMenu("Setup In-Game Dual Sliders")]
        public void EnsureSettingsSubPanelControls()
        {
            if (settingsSubPanel == null && pauseMenuPanel != null)
            {
                Transform sTr = pauseMenuPanel.transform.Find("Panel_SettingsSub");
                if (sTr != null) settingsSubPanel = sTr.gameObject;
            }
            if (settingsSubPanel == null) return;

            Transform sPanelTr = settingsSubPanel.transform;

            // 1. Settings Title
            Transform titleTr = sPanelTr.Find("Text_Title");
            if (titleTr == null) titleTr = sPanelTr.Find("Title");
            if (titleTr != null)
            {
                settingsTitleText = titleTr.GetComponent<TextMeshProUGUI>();
                if (settingsTitleText != null) settingsTitleText.text = LocalizationManager.Get("MM_Settings_Title");
            }

            // 2. Identify / Convert Music Volume Row (replacing old Master Volume)
            Transform musicSliderTr = sPanelTr.Find("Slider_MusicVolume");
            if (musicSliderTr == null) musicSliderTr = sPanelTr.Find("Slider_Volume");
            if (musicSliderTr != null)
            {
                musicSliderTr.name = "Slider_MusicVolume";
                RectTransform rt = musicSliderTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -90f);
                    rt.sizeDelta = new Vector2(300f, 18f);
                }

                musicVolumeSlider = musicSliderTr.GetComponent<Slider>();
                if (musicVolumeSlider != null)
                {
                    musicVolumeSlider.minValue = 0f;
                    musicVolumeSlider.maxValue = 1f;
                    musicVolumeSlider.wholeNumbers = false;
                }
                musicVolumeValueText = musicSliderTr.GetComponentInChildren<TextMeshProUGUI>();
            }

            Transform musicLabelTr = sPanelTr.Find("Label_MusicVolume");
            if (musicLabelTr == null) musicLabelTr = sPanelTr.Find("Label_Volume");
            if (musicLabelTr != null)
            {
                musicLabelTr.name = "Label_MusicVolume";
                RectTransform rt = musicLabelTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(40f, -65f);
                    rt.sizeDelta = new Vector2(-80f, 22f);
                }

                musicVolumeLabelText = musicLabelTr.GetComponent<TextMeshProUGUI>();
                if (musicVolumeLabelText != null)
                {
                    musicVolumeLabelText.text = LocalizationManager.Get("MM_Settings_MusicVolume");
                }
            }

            // 3. Identify / Create Sounds Volume Row
            Transform soundsSliderTr = sPanelTr.Find("Slider_SoundsVolume");
            Transform soundsLabelTr = sPanelTr.Find("Label_SoundsVolume");

            if (soundsSliderTr == null && musicSliderTr != null)
            {
                GameObject ssGo = Instantiate(musicSliderTr.gameObject, sPanelTr);
                ssGo.name = "Slider_SoundsVolume";
                soundsSliderTr = ssGo.transform;
            }

            if (soundsSliderTr != null)
            {
                RectTransform rt = soundsSliderTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -147f);
                    rt.sizeDelta = new Vector2(300f, 18f);
                }

                soundsVolumeSlider = soundsSliderTr.GetComponent<Slider>();
                if (soundsVolumeSlider != null)
                {
                    soundsVolumeSlider.minValue = 0f;
                    soundsVolumeSlider.maxValue = 1f;
                    soundsVolumeSlider.wholeNumbers = false;
                }
                soundsVolumeValueText = soundsSliderTr.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (soundsLabelTr == null && musicLabelTr != null)
            {
                GameObject slGo = Instantiate(musicLabelTr.gameObject, sPanelTr);
                slGo.name = "Label_SoundsVolume";
                soundsLabelTr = slGo.transform;
            }

            if (soundsLabelTr != null)
            {
                RectTransform rt = soundsLabelTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(40f, -122f);
                    rt.sizeDelta = new Vector2(-80f, 22f);
                }

                soundsVolumeLabelText = soundsLabelTr.GetComponent<TextMeshProUGUI>();
                if (soundsVolumeLabelText != null)
                {
                    soundsVolumeLabelText.text = LocalizationManager.Get("MM_Settings_SoundsVolume");
                }
            }

            // 4. Reposition Mouse Sens Row
            Transform sensLabelTr = sPanelTr.Find("Label_Sens");
            if (sensLabelTr != null)
            {
                RectTransform rt = sensLabelTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(40f, -179f);
                    rt.sizeDelta = new Vector2(-80f, 22f);
                }

                mouseSensLabelText = sensLabelTr.GetComponent<TextMeshProUGUI>();
                if (mouseSensLabelText != null)
                {
                    mouseSensLabelText.text = LocalizationManager.Get("MM_Settings_Sens");
                }
            }

            Transform sensSliderTr = sPanelTr.Find("Slider_Sens");
            if (sensSliderTr != null)
            {
                RectTransform rt = sensSliderTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.anchoredPosition = new Vector2(0f, -204f);
                    rt.sizeDelta = new Vector2(300f, 18f);
                }

                mouseSensSlider = sensSliderTr.GetComponent<Slider>();
                if (mouseSensSlider != null)
                {
                    mouseSensSlider.minValue = 0.2f;
                    mouseSensSlider.maxValue = 5.0f;
                    mouseSensSlider.wholeNumbers = false;
                }
                mouseSensValueText = sensSliderTr.GetComponentInChildren<TextMeshProUGUI>();
            }

            // 5. Close Button
            Transform closeBtnTr = sPanelTr.Find("Btn_CloseSettings");
            if (closeBtnTr != null)
            {
                RectTransform rt = closeBtnTr.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(0f, 28f);
                }
                closeSettingsButton = closeBtnTr.GetComponent<Button>();
                closeSettingsText = closeBtnTr.GetComponentInChildren<TextMeshProUGUI>();
            }

            // 6. Bind Listeners
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
            if (mouseSensSlider != null)
            {
                mouseSensSlider.onValueChanged.RemoveAllListeners();
                mouseSensSlider.onValueChanged.AddListener(OnMouseSensChanged);
            }
        }

        private GameObject CreateSimpleSlider(Transform parent, string name, Vector2 pos, Vector2 size, float min, float max, out Slider slider, out TextMeshProUGUI valText)
        {
            GameObject sliderGo = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(parent, false);

            RectTransform rt = sliderGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            slider = sliderGo.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;

            // Background
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.1f, 0.15f, 0.22f, 0.9f);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = Vector2.zero;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;
            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.2f, 0.85f, 1f, 0.95f);

            slider.fillRect = fillRt;

            // Value text
            GameObject valGo = new GameObject("Text_Val", typeof(RectTransform), typeof(TextMeshProUGUI));
            valGo.transform.SetParent(sliderGo.transform, false);
            RectTransform valRt = valGo.GetComponent<RectTransform>();
            valRt.anchorMin = new Vector2(1f, 0.5f);
            valRt.anchorMax = new Vector2(1f, 0.5f);
            valRt.pivot = new Vector2(0f, 0.5f);
            valRt.anchoredPosition = new Vector2(16f, 0f);
            valRt.sizeDelta = new Vector2(60f, 20f);

            valText = valGo.GetComponent<TextMeshProUGUI>();
            valText.fontSize = 13;
            valText.fontStyle = FontStyles.Bold;
            valText.color = new Color(0.2f, 0.9f, 1f);
            valText.alignment = TextAlignmentOptions.Left;

            return sliderGo;
        }
    }
}
