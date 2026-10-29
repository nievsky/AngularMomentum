using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using FMOD.Studio;

public class SceneManagerUI : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionReference pauseAction;

    [Header("UI References")]
    [SerializeField] private Canvas pauseMenuCanvas;
    [SerializeField] private GameObject pauseMenuImage;
    [SerializeField] private GameObject settingMenuImage;

    private UIPopWindow uiPopWindow;
    private UIPopWindow uiPopWindow2;
    public bool isPaused = false;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pauseMenuImage != null)
        {
            pauseMenuImage.SetActive(true);
            uiPopWindow = pauseMenuImage.GetComponent<UIPopWindow>();
        }

        if (settingMenuImage != null)
        {
            uiPopWindow2 = settingMenuImage.GetComponent<UIPopWindow>();
        }
    }

    private void OnEnable()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed += OnPausePerformed;
            pauseAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (pauseAction != null && pauseAction.action != null)
        {
            pauseAction.action.performed -= OnPausePerformed;
            pauseAction.action.Disable();
        }
    }

    private void OnPausePerformed(InputAction.CallbackContext context)
    {
        isPaused = !isPaused;

        if (isPaused)
        {
            // --- PAUSE ---
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            uiPopWindow?.Show();

            PauseAudioManager.StartSnapshot();
        }
        else
        {
            // --- UNPAUSE ---
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            uiPopWindow?.Hide();
            uiPopWindow2?.Hide();

            PauseAudioManager.StopSnapshot();
        }
    }
}
