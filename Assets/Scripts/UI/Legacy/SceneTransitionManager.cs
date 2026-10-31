using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-990)]
[DisallowMultipleComponent]
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Transition Settings")]
    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private float transitionDuration = 0.8f;

    private Canvas _transitionCanvas;
    private Image _dissolveImage;
    private Material _instancedMat;
    private bool _isTransitioning = false;

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
    }

    public void BuildTransitionUI()
    {
        if (_transitionCanvas != null) return;

        // 1. Create Top-Sorting Canvas (sortingOrder = 9999)
        GameObject canvasGo = new GameObject("Transition_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _transitionCanvas = canvasGo.AddComponent<Canvas>();
        _transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _transitionCanvas.sortingOrder = 9999;

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

        imgGo.SetActive(false);
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

        if (_dissolveImage != null)
        {
            _dissolveImage.gameObject.SetActive(true);
            _dissolveImage.raycastTarget = true;
        }

        // -------------------------------------------------------------
        // Step 1 (Dissolve IN to Black):
        // Animate _DissolveProgress from 0.0 to 1.0 over 0.8f seconds (unscaledDeltaTime)
        // -------------------------------------------------------------
        if (OrbitalPanopticon.Core.FMODAudioManager.Instance != null)
        {
            OrbitalPanopticon.Core.FMODAudioManager.Instance.PlayDissolveTransition();
        }

        float elapsed = 0f;
        float dur = Mathf.Max(0.1f, transitionDuration);

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / dur);
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

        yield return null;
        Time.timeScale = 1f;

        // -------------------------------------------------------------
        // Step 3 (Dissolve OUT from Black):
        // Animate _DissolveProgress from 1.0 back down to 0.0 over 0.8f seconds
        // -------------------------------------------------------------
        if (OrbitalPanopticon.Core.FMODAudioManager.Instance != null)
        {
            OrbitalPanopticon.Core.FMODAudioManager.Instance.PlayDissolveTransition();
        }

        elapsed = 0f;
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
}