using System.Collections;
using UnityEngine;
using DG.Tweening;
using UnityEngine.SceneManagement;
using FMODUnity;
using FMOD.Studio;

public class UIPopWindow : MonoBehaviour
{
    [Header("Animation")]
    public float animationDuration = 0.3f;
    public Ease ease = Ease.OutBack; // nice bounce effect

    private RectTransform rectTransform;
    private Vector3 originalScale;

    // --- FMOD snapshot instance ---
    private static EventInstance pausedSnapshot;
    private static bool snapshotInitialized = false;

    void Awake()
    {
        InitializeRectTransform();

        // Initialize FMOD snapshot once
        if (!snapshotInitialized)
        {
            pausedSnapshot = RuntimeManager.CreateInstance("snapshot:/Paused");
            snapshotInitialized = true;
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);

        if (!InitializeRectTransform()) return;

        rectTransform.DOKill();
        rectTransform.localScale = Vector3.zero;
        rectTransform.DOScale(originalScale, animationDuration).SetEase(ease);
    }

    public void Hide()
    {
        if (!InitializeRectTransform()) return;

        rectTransform.DOKill();
        rectTransform.DOScale(Vector3.zero, animationDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => gameObject.SetActive(false));
    }

    public void HideWithCursorLock()
    {
        Hide();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void ContinueGame()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Hide();

        PauseAudioManager.StopSnapshot();
    }


    // public void RestartLevel()
    // {
    //     PrepareForSceneLoad();
    //     Cursor.lockState = CursorLockMode.Locked;
    //     Cursor.visible = false;
    //
    //     // FMOD cleanup before reload
    //     FMOD.Studio.System fmodSystem = FMODUnity.RuntimeManager.StudioSystem;
    //     fmodSystem.flushCommands();
    //     FMODUnity.RuntimeManager.GetBus("bus:/").stopAllEvents(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    //
    //     SceneTransitionManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    // }

    // public void StartGame()
    // {
    //     PrepareForSceneLoad();
    //     SceneTransitionManager.LoadScene(0);
    // }

    // public void StartNextScene()
    // {
    //     PrepareForSceneLoad();
    //     int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
    //     if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
    //     {
    //         if (SceneManager.GetActiveScene().buildIndex == 0)
    //         {
    //             GameController.TargetStartLevelIndex = -1;
    //         }
    //
    //         SceneTransitionManager.LoadScene(nextSceneIndex);
    //     }
    //     else
    //     {
    //         Debug.LogWarning("No next scene found in Build Settings!");
    //     }
    // }
    //
    // public void LoadMainMenu()
    // {
    //     PrepareForSceneLoad();
    //     SceneTransitionManager.LoadScene(0);
    // }
    
    // public void LoadNextScene()
    // {
    //     PrepareForSceneLoad();
    //     int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
    //     if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
    //     {
    //         if (SceneManager.GetActiveScene().buildIndex == 0)
    //         {
    //             GameController.TargetStartLevelIndex = -1;
    //         }
    //
    //         SceneTransitionManager.LoadScene(nextSceneIndex);
    //     }
    //     else
    //     {
    //         Debug.LogWarning("No next scene found in Build Settings!");
    //     }
    // }

    // private void PrepareForSceneLoad()
    // {
    //     Time.timeScale = 1f;
    //     PauseAudioManager.StopSnapshot();
    //     AudioManager.StopAllPersistentAudio();
    // }

    private bool InitializeRectTransform()
    {
        if (rectTransform != null)
        {
            return true;
        }

        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return false;
        }

        originalScale = rectTransform.localScale;
        rectTransform.localScale = Vector3.zero;
        return true;
    }
}
