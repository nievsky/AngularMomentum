using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;

public class MenuMusicStarter : MonoBehaviour
{
    [SerializeField] private StudioEventEmitter emitter;
    [SerializeField] private StudioEventEmitter emitter2;
    [SerializeField] private bool stopWhenLeavingInitialScene = true;

    private string _initialSceneName;
    private Coroutine _playRoutine;
    private bool _playRequested;
    private bool _isStopping;

    private void Awake()
    {
        _initialSceneName = gameObject.scene.name;
        DontDestroyOnLoad(gameObject);
        EnsureEmitterReferences();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (!_isStopping)
        {
            StopMenuMusic();
        }
    }

    public void PlayAfterUserGesture()
    {
        EnsureEmitterReferences();
        _playRequested = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        RuntimeManager.CoreSystem.mixerResume();
#endif

        if (RuntimeManager.HaveAllBanksLoaded)
        {
            PlayEmitters();
            return;
        }

        if (_playRoutine == null)
        {
            _playRoutine = StartCoroutine(PlayWhenBanksLoaded());
        }
    }

    public void StopMenuMusic()
    {
        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        StopEmitter(emitter);
        StopEmitter(emitter2);
        _playRequested = false;
    }

    private IEnumerator PlayWhenBanksLoaded()
    {
        yield return new WaitUntil(() => RuntimeManager.HaveAllBanksLoaded);

        if (_playRequested)
        {
            PlayEmitters();
        }

        _playRoutine = null;
    }

    private void PlayEmitters()
    {
        PlayEmitter(emitter);
        PlayEmitter(emitter2);
    }

    private void PlayEmitter(StudioEventEmitter targetEmitter)
    {
        if (targetEmitter != null && !targetEmitter.IsPlaying())
        {
            targetEmitter.Play();
        }
    }

    private void StopEmitter(StudioEventEmitter targetEmitter)
    {
        if (targetEmitter != null && targetEmitter.IsPlaying())
        {
            targetEmitter.Stop();
        }
    }

    private void EnsureEmitterReferences()
    {
        if (emitter != null && emitter2 != null && emitter != emitter2)
        {
            return;
        }

        StudioEventEmitter[] emitters = GetComponents<StudioEventEmitter>();

        if (emitter == null && emitters.Length > 0)
        {
            emitter = emitters[0];
        }

        if ((emitter2 == null || emitter2 == emitter) && emitters.Length > 1)
        {
            emitter2 = GetDifferentEmitter(emitters, emitter);
        }
    }

    private StudioEventEmitter GetDifferentEmitter(StudioEventEmitter[] emitters, StudioEventEmitter currentEmitter)
    {
        foreach (StudioEventEmitter targetEmitter in emitters)
        {
            if (targetEmitter != null && targetEmitter != currentEmitter)
            {
                return targetEmitter;
            }
        }

        return null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!stopWhenLeavingInitialScene || scene.name == _initialSceneName)
        {
            return;
        }

        _isStopping = true;
        StopMenuMusic();
        Destroy(gameObject);
    }
}
