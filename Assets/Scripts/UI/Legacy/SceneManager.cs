using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class VideoEndToNextScene : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private int nextSceneBuildIndex = -1; // optional override
    [SerializeField] private float delayAfterVideo = 0.5f; // small delay before load

    private void Awake()
    {
        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();
    }

    private void OnEnable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached += OnVideoFinished;
    }

    private void OnDisable()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnVideoFinished;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        // Delay slightly before changing scenes
        Invoke(nameof(LoadNextScene), delayAfterVideo);
    }

    private void LoadNextScene()
    {
        int indexToLoad = nextSceneBuildIndex >= 0
            ? nextSceneBuildIndex
            : SceneManager.GetActiveScene().buildIndex + 1;

        if (indexToLoad < SceneManager.sceneCountInBuildSettings)
            SceneTransitionManager.LoadScene(indexToLoad);
        else
            Debug.LogWarning("No next scene found in Build Settings!");
    }
}
