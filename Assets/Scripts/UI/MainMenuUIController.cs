using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.UI
{
    [DisallowMultipleComponent]
    public class MainMenuUIController : MonoBehaviour
    {
        [Header("Main Menu Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Settings Sub-Panel")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private TextMeshProUGUI masterVolumeValueText;
        [SerializeField] private Slider mouseSensSlider;
        [SerializeField] private TextMeshProUGUI mouseSensValueText;
        [SerializeField] private Button closeSettingsButton;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettingsClicked);
            if (creditsButton != null) creditsButton.onClick.AddListener(OnCreditsClicked);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(OnCloseSettingsClicked);

            LoadSettings();

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (mouseSensSlider != null) mouseSensSlider.onValueChanged.AddListener(OnMouseSensChanged);

            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void LoadSettings()
        {
            float volume = PlayerPrefs.GetFloat("Settings_MasterVolume", 0.85f);
            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.value = volume;
            }
            AudioListener.volume = volume;
            if (masterVolumeValueText != null)
            {
                masterVolumeValueText.text = $"{Mathf.RoundToInt(volume * 100f)}%";
            }

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

        private void OnMasterVolumeChanged(float val)
        {
            AudioListener.volume = val;
            PlayerPrefs.SetFloat("Settings_MasterVolume", val);
            PlayerPrefs.Save();
            if (masterVolumeValueText != null)
            {
                masterVolumeValueText.text = $"{Mathf.RoundToInt(val * 100f)}%";
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
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(!settingsPanel.activeSelf);
            }
        }

        public void OnCloseSettingsClicked()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        public void OnCreditsClicked()
        {
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
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}