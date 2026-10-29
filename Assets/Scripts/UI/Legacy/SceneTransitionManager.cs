using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-900)]
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }
    public static bool IsTransitioning => Instance != null && Instance._isTransitioning;
    public static event Action<SceneTransitionPhase> PhaseChanged;

    [Header("Default Config")]
    [SerializeField] private SceneTransitionConfig _defaultConfig;

    [Header("Transition Events")]
    [SerializeField] private UnityEvent _coverStarted = new UnityEvent();
    [SerializeField] private UnityEvent _coverCompleted = new UnityEvent();
    [SerializeField] private UnityEvent _sceneActivationStarted = new UnityEvent();
    [SerializeField] private UnityEvent _sceneActivated = new UnityEvent();
    [SerializeField] private UnityEvent _revealStarted = new UnityEvent();
    [SerializeField] private UnityEvent _revealCompleted = new UnityEvent();

    private SceneTransitionView _view;
    private SceneTransitionConfig _runtimeDefaultConfig;
    private Coroutine _transitionRoutine;
    private bool _isTransitioning;

    public static void LoadScene(string sceneName)
    {
        LoadScene(sceneName, null);
    }

    public static void LoadScene(string sceneName, SceneTransitionConfig config)
    {
        EnsureInstance().StartSceneLoad(sceneName, -1, false, config);
    }

    public static void LoadScene(int buildIndex)
    {
        LoadScene(buildIndex, null);
    }

    public static void LoadScene(int buildIndex, SceneTransitionConfig config)
    {
        EnsureInstance().StartSceneLoad(null, buildIndex, true, config);
    }

    public static void ReloadActiveScene(SceneTransitionConfig config = null)
    {
        LoadScene(SceneManager.GetActiveScene().buildIndex, config);
    }

    private static SceneTransitionManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

#if UNITY_2023_1_OR_NEWER
        SceneTransitionManager existing = FindFirstObjectByType<SceneTransitionManager>();
#else
        SceneTransitionManager existing = FindObjectOfType<SceneTransitionManager>();
#endif
        if (existing != null)
        {
            existing.EnsureInitialized();
            return existing;
        }

        GameObject managerObject = new GameObject("Scene Transition Manager");
        SceneTransitionManager manager = managerObject.AddComponent<SceneTransitionManager>();
        manager.EnsureInitialized();
        return manager;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (Instance._defaultConfig == null && _defaultConfig != null)
            {
                Instance._defaultConfig = _defaultConfig;
            }

            // DestroyDuplicateTransitionComponents();
            return;
        }

        EnsureInitialized();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureInitialized()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        if (_view != null)
        {
            return;
        }

        GameObject viewObject = new GameObject("Scene Transition View");
        viewObject.transform.SetParent(transform, false);
        _view = viewObject.AddComponent<SceneTransitionView>();
        _view.Initialize();
    }

    private void StartSceneLoad(string sceneName, int buildIndex, bool useBuildIndex, SceneTransitionConfig config)
    {
        EnsureInitialized();

        if (_isTransitioning)
        {
            Debug.LogWarning("Scene transition already running. Ignoring new scene load request.");
            return;
        }

        _transitionRoutine = StartCoroutine(LoadSceneRoutine(sceneName, buildIndex, useBuildIndex, GetConfig(config)));
    }

    private IEnumerator LoadSceneRoutine(string sceneName, int buildIndex, bool useBuildIndex, SceneTransitionConfig config)
    {
        _isTransitioning = true;
        _view.Prepare(config);

        AsyncOperation operation = null;
        bool loadAttempted = false;

        RaisePhase(SceneTransitionPhase.CoverStarted);

        if (config.StartLoadingDuringCoverAnimation)
        {
            operation = BeginSceneLoad(sceneName, buildIndex, useBuildIndex);
            loadAttempted = true;
        }

        yield return AnimateProgress(0f, 1f, config.CoverDuration, config.CoverCurve, config.UseUnscaledTime);
        _view.SetCovered();
        RaisePhase(SceneTransitionPhase.CoverCompleted);

        if (!loadAttempted)
        {
            operation = BeginSceneLoad(sceneName, buildIndex, useBuildIndex);
            loadAttempted = true;
        }

        if (operation == null)
        {
            RaisePhase(SceneTransitionPhase.RevealStarted);
            yield return AnimateProgress(1f, 0f, config.RevealDuration, config.RevealCurve, config.UseUnscaledTime);
            RaisePhase(SceneTransitionPhase.RevealCompleted);
            FinishTransition();
            yield break;
        }

        float coveredUntil = Time.realtimeSinceStartup + config.MinimumCoveredDuration;
        while (operation.progress < 0.9f || Time.realtimeSinceStartup < coveredUntil)
        {
            yield return null;
        }

        RaisePhase(SceneTransitionPhase.SceneActivationStarted);
        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }

        RaisePhase(SceneTransitionPhase.SceneActivated);

        yield return null;
        yield return null;

        RaisePhase(SceneTransitionPhase.RevealStarted);
        yield return AnimateProgress(1f, 0f, config.RevealDuration, config.RevealCurve, config.UseUnscaledTime);
        RaisePhase(SceneTransitionPhase.RevealCompleted);

        FinishTransition();
    }

    private AsyncOperation BeginSceneLoad(string sceneName, int buildIndex, bool useBuildIndex)
    {
        if (useBuildIndex)
        {
            if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"Cannot load scene build index {buildIndex}. It is not in Build Settings.");
                return null;
            }

            AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);
            operation.allowSceneActivation = false;
            return operation;
        }

        if (string.IsNullOrWhiteSpace(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"Cannot load scene '{sceneName}'. It is not in Build Settings or is not loadable.");
            return null;
        }

        AsyncOperation namedOperation = SceneManager.LoadSceneAsync(sceneName);
        namedOperation.allowSceneActivation = false;
        return namedOperation;
    }

    private IEnumerator AnimateProgress(float from, float to, float duration, AnimationCurve curve, bool useUnscaledTime)
    {
        if (duration <= 0f)
        {
            _view.SetProgress(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float curvedTime = curve != null ? curve.Evaluate(normalizedTime) : normalizedTime;
            _view.SetProgress(Mathf.LerpUnclamped(from, to, curvedTime));
            yield return null;
        }

        _view.SetProgress(to);
    }

    private SceneTransitionConfig GetConfig(SceneTransitionConfig overrideConfig)
    {
        if (overrideConfig != null)
        {
            return overrideConfig;
        }

        if (_defaultConfig != null)
        {
            return _defaultConfig;
        }

        if (_runtimeDefaultConfig == null)
        {
            _runtimeDefaultConfig = ScriptableObject.CreateInstance<SceneTransitionConfig>();
            _runtimeDefaultConfig.hideFlags = HideFlags.HideAndDontSave;
        }

        return _runtimeDefaultConfig;
    }

    private void RaisePhase(SceneTransitionPhase phase)
    {
        PhaseChanged?.Invoke(phase);

        switch (phase)
        {
            case SceneTransitionPhase.CoverStarted:
                _coverStarted?.Invoke();
                break;
            case SceneTransitionPhase.CoverCompleted:
                _coverCompleted?.Invoke();
                break;
            case SceneTransitionPhase.SceneActivationStarted:
                _sceneActivationStarted?.Invoke();
                break;
            case SceneTransitionPhase.SceneActivated:
                _sceneActivated?.Invoke();
                break;
            case SceneTransitionPhase.RevealStarted:
                _revealStarted?.Invoke();
                break;
            case SceneTransitionPhase.RevealCompleted:
                _revealCompleted?.Invoke();
                break;
        }
    }

    private void FinishTransition()
    {
        _view.Hide();
        _transitionRoutine = null;
        _isTransitioning = false;
    }

    // private void DestroyDuplicateTransitionComponents()
    // {
    //     SceneTransitionFmodAudio[] audioAdapters = GetComponents<SceneTransitionFmodAudio>();
    //     for (int i = 0; i < audioAdapters.Length; i++)
    //     {
    //         Destroy(audioAdapters[i]);
    //     }
    //
    //     Destroy(this);
    // }
}
