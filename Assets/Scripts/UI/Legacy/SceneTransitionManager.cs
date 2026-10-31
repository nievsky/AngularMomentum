using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Player;

[DefaultExecutionOrder(-990)]
[DisallowMultipleComponent]
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private float fadeOutDuration = 0.85f;
    [SerializeField] private float fadeInDuration = 0.95f;

    private Canvas _transitionCanvas;
    private Image _dissolveImage;
    private Material _instancedMat;
    private bool _isTransitioning = false;
    private static bool _hasPlayedBootDissolve = false;

    private static readonly int DissolveProgressID = Shader.PropertyToID("_DissolveProgress");

    public bool IsTransitioning => _isTransitioning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (Instance == null)
        {
            var existing = FindAnyObjectByType<SceneTransitionManager>();
            if (existing != null)
            {
                Instance = existing;
            }
            else
            {
                GameObject go = new GameObject("[SCENE_TRANSITION_MANAGER]");
                Instance = go.AddComponent<SceneTransitionManager>();
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildTransitionUI();

        if (!_hasPlayedBootDissolve && Application.isPlaying)
        {
            _hasPlayedBootDissolve = true;
            StartCoroutine(InitialDissolveInRoutine());
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateCanvasCamera();
    }

    /// <summary>
    /// Enforces ScreenSpaceOverlay with maximum sorting order (32767) so the dissolve transition
    /// renders above all other canvases and UI elements in all scenes.
    /// </summary>
    public void UpdateCanvasCamera()
    {
        if (_transitionCanvas == null) return;
        _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _transitionCanvas.sortingOrder = 32767;
    }

    public void BuildTransitionUI()
    {
        if (_transitionCanvas != null)
        {
            UpdateCanvasCamera();
            return;
        }

        // 1. Create Top-Sorting Canvas (ScreenSpaceOverlay with sortingOrder 32767)
        GameObject canvasGo = new GameObject("Transition_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _transitionCanvas = canvasGo.AddComponent<Canvas>();
        _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _transitionCanvas.sortingOrder = 32767;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // 2. Full-screen BlackDissolveOverlay Image
        GameObject imgGo = new GameObject("BlackDissolveOverlay");
        imgGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _dissolveImage = imgGo.AddComponent<Image>();
        _dissolveImage.color = Color.white;
        _dissolveImage.raycastTarget = false;

        if (dissolveMaterial == null)
        {
#if UNITY_EDITOR
            dissolveMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ScreenDissolve.mat");
#endif
            if (dissolveMaterial == null)
            {
                Shader s = Shader.Find("UI/ScreenDissolve");
                if (s != null) dissolveMaterial = new Material(s);
            }
        }

        if (dissolveMaterial != null)
        {
            _instancedMat = new Material(dissolveMaterial);
            _dissolveImage.material = _instancedMat;
            _instancedMat.SetFloat(DissolveProgressID, 0f);
        }

        if (!_hasPlayedBootDissolve && Application.isPlaying)
        {
            if (_instancedMat != null)
            {
                _instancedMat.SetFloat(DissolveProgressID, 1.0f);
            }
            _dissolveImage.raycastTarget = true;
            imgGo.SetActive(true);
        }
        else
        {
            imgGo.SetActive(false);
        }
    }

    private IEnumerator InitialDissolveInRoutine()
    {
        _isTransitioning = true;
        BuildTransitionUI();
        UpdateCanvasCamera();

        if (_dissolveImage != null)
        {
            _dissolveImage.gameObject.SetActive(true);
            _dissolveImage.raycastTarget = true;
        }

        if (_instancedMat != null)
        {
            _instancedMat.SetFloat(DissolveProgressID, 1.0f);
        }

        // Wait 2 settling frames for scene camera, skybox, post-processing to initialize behind black curtain
        yield return null;
        yield return null;
        UpdateCanvasCamera();

        if (PostProcessingToolkitController.Instance != null)
        {
            PostProcessingToolkitController.Instance.TriggerImpactPulse(bloomBoost: 2.2f, chromaticBoost: 0.2f, lensDistortionBoost: -0.05f, duration: 0.6f);
        }

        float elapsed = 0f;
        float dur = Mathf.Max(0.1f, fadeInDuration);

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = 1.0f - Mathf.Clamp01(elapsed / dur);
            if (_instancedMat != null)
            {
                _instancedMat.SetFloat(DissolveProgressID, progress);
            }
            yield return null;
        }

        if (_instancedMat != null)
        {
            _instancedMat.SetFloat(DissolveProgressID, 0.0f);
        }

        if (_dissolveImage != null)
        {
            _dissolveImage.raycastTarget = false;
            _dissolveImage.gameObject.SetActive(false);
        }

        _isTransitioning = false;
    }

    public void TransitionToScene(string sceneName)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[SceneTransitionManager] Transition to '{sceneName}' requested while already transitioning. Ignored.");
            return;
        }

        StartCoroutine(TransitionRoutine(sceneName));
    }

    /// <summary>
    /// Specialized scene transition for post-CRT blackout / finale.
    /// Skips Step 1 (no redundant fade-in when screen is already black),
    /// loads scene asynchronously, and executes ONLY the fiery dissolve fade-out over 1.2s.
    /// </summary>
    public void TransitionFromBlackScreenToScene(string sceneName)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[SceneTransitionManager] TransitionFromBlackScreen to '{sceneName}' requested while already transitioning. Ignored.");
            return;
        }

        StartCoroutine(TransitionFromBlackRoutine(sceneName));
    }

    public static void LoadScene(string sceneName)
    {
        if (Instance != null)
        {
            Instance.TransitionToScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    public static void LoadScene(int buildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(path);
        if (!string.IsNullOrEmpty(sceneName))
        {
            LoadScene(sceneName);
        }
        else
        {
            SceneManager.LoadScene(buildIndex);
        }
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        _isTransitioning = true;
        BuildTransitionUI();
        UpdateCanvasCamera();

        if (_dissolveImage != null)
        {
            _dissolveImage.gameObject.SetActive(true);
            _dissolveImage.raycastTarget = true;
        }

        // -------------------------------------------------------------
        // Step 1 (Dissolve IN to Black / Fade Out Current Scene):
        // Animate _DissolveProgress from 0.0 to 1.0 over fadeOutDuration (unscaledDeltaTime)
        // -------------------------------------------------------------
        if (OrbitalPanopticon.Core.FMODAudioManager.Instance != null)
        {
            OrbitalPanopticon.Core.FMODAudioManager.Instance.PlayDissolveTransition();
        }
        if (PostProcessingToolkitController.Instance != null)
        {
            PostProcessingToolkitController.Instance.TriggerImpactPulse(bloomBoost: 2.2f, chromaticBoost: 0.2f, lensDistortionBoost: -0.05f, duration: 0.6f);
        }

        float elapsed = 0f;
        float fadeOutDur = Mathf.Max(0.1f, fadeOutDuration);

        while (elapsed < fadeOutDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / fadeOutDur);
            if (_instancedMat != null)
            {
                _instancedMat.SetFloat(DissolveProgressID, progress);
            }
            yield return null;
        }

        if (_instancedMat != null)
        {
            _instancedMat.SetFloat(DissolveProgressID, 1.0f);
        }

        // -------------------------------------------------------------
        // Step 2 (Load Scene in Background while Screen is Black):
        // AsyncOperation op = SceneManager.LoadSceneAsync(sceneName); wait until op.isDone
        // Then wait 2 settling frames for scene camera, skybox, post-processing to initialize
        // -------------------------------------------------------------
        Time.timeScale = 1f;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone)
            {
                yield return null;
            }
        }
        else
        {
            Debug.LogError($"[SceneTransitionManager] Failed to start async load for scene '{sceneName}'.");
        }

        // Wait 2 settling frames for scene initialization behind the black curtain
        yield return null;
        yield return null;
        Time.timeScale = 1f;
        UpdateCanvasCamera();

        // -------------------------------------------------------------
        // Step 3 (Dissolve OUT from Black / Fade In New Scene):
        // Animate _DissolveProgress from 1.0 back down to 0.0 over fadeInDuration (unscaledDeltaTime)
        // -------------------------------------------------------------
        if (OrbitalPanopticon.Core.FMODAudioManager.Instance != null)
        {
            OrbitalPanopticon.Core.FMODAudioManager.Instance.PlayDissolveTransition();
        }
        if (PostProcessingToolkitController.Instance != null)
        {
            PostProcessingToolkitController.Instance.TriggerImpactPulse(bloomBoost: 2.2f, chromaticBoost: 0.2f, lensDistortionBoost: -0.05f, duration: 0.6f);
        }

        elapsed = 0f;
        float fadeInDur = Mathf.Max(0.1f, fadeInDuration);

        while (elapsed < fadeInDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = 1.0f - Mathf.Clamp01(elapsed / fadeInDur);
            if (_instancedMat != null)
            {
                _instancedMat.SetFloat(DissolveProgressID, progress);
            }
            yield return null;
        }

        if (_instancedMat != null)
        {
            _instancedMat.SetFloat(DissolveProgressID, 0.0f);
        }

        if (_dissolveImage != null)
        {
            _dissolveImage.raycastTarget = false;
            _dissolveImage.gameObject.SetActive(false);
        }

        _isTransitioning = false;
    }

    private IEnumerator TransitionFromBlackRoutine(string sceneName)
    {
        _isTransitioning = true;
        BuildTransitionUI();
        UpdateCanvasCamera();

        if (_dissolveImage != null)
        {
            _dissolveImage.gameObject.SetActive(true);
            _dissolveImage.raycastTarget = true;
        }

        // Step 1: Immediately snap _DissolveProgress = 1.0f in 0s (no redundant fade-in!)
        if (_instancedMat != null)
        {
            _instancedMat.SetFloat(DissolveProgressID, 1.0f);
        }

        // Step 2: Async background load while screen is solidly black
        Time.timeScale = 1f;
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op != null)
        {
            while (!op.isDone)
            {
                yield return null;
            }
        }
        else
        {
            Debug.LogError($"[SceneTransitionManager] Failed to start async load for scene '{sceneName}'.");
        }

        // Wait 2 settling frames for scene camera, skybox, post-processing to initialize
        yield return null;
        yield return null;
        Time.timeScale = 1f;
        UpdateCanvasCamera();

        // Step 3: Fiery Dissolve Fade-Out ONLY (1.0f down to 0.0f over 1.2s)
        if (OrbitalPanopticon.Core.FMODAudioManager.Instance != null)
        {
            OrbitalPanopticon.Core.FMODAudioManager.Instance.PlayDissolveTransition();
        }
        if (PostProcessingToolkitController.Instance != null)
        {
            PostProcessingToolkitController.Instance.TriggerImpactPulse(bloomBoost: 2.2f, chromaticBoost: 0.2f, lensDistortionBoost: -0.05f, duration: 0.8f);
        }

        float elapsed = 0f;
        const float fadeOutDur = 1.2f;

        while (elapsed < fadeOutDur)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = 1.0f - Mathf.Clamp01(elapsed / fadeOutDur);
            if (_instancedMat != null)
            {
                _instancedMat.SetFloat(DissolveProgressID, progress);
            }
            yield return null;
        }

        if (_instancedMat != null)
        {
            _instancedMat.SetFloat(DissolveProgressID, 0.0f);
        }

        if (_dissolveImage != null)
        {
            _dissolveImage.raycastTarget = false;
            _dissolveImage.gameObject.SetActive(false);
        }

        _isTransitioning = false;
    }
}
