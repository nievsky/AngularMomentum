using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;
using FMOD.Studio;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Manages multi-track sequential music playlists for Main Menu, Gameplay, and Credits scenes.
    /// Handles FMOD EventReferences with fallback Unity AudioClips, smooth unscaled-time crossfades,
    /// track end detection (PLAYBACK_STATE.STOPPED / !isPlaying), and automatic scene-based transitions.
    /// </summary>
    [DisallowMultipleComponent]
    public class MusicPlaylistManager : MonoBehaviour
    {
        public static MusicPlaylistManager Instance { get; private set; }

        public enum PlaylistType
        {
            MainMenu,
            Gameplay,
            Credits
        }

        [Header("Configuration Reference")]
        [SerializeField] private FMODAudioConfigSO audioConfig;

        [Header("Runtime State")]
        [SerializeField] private PlaylistType currentPlaylistType = PlaylistType.MainMenu;
        [SerializeField] private int currentTrackIndex = 0;
        [SerializeField] private bool isCrossfading = false;

        // FMOD Event Instances
        private EventInstance _activeFmodInstance;
        private EventInstance _fadingOutFmodInstance;

        // Unity AudioSource fallbacks
        private AudioSource _audioSourceA;
        private AudioSource _audioSourceB;
        private AudioSource _activeAudioSource;
        private AudioSource _fadingOutAudioSource;

        private Coroutine _crossfadeCoroutine;
        private bool _isInitialized = false;
        private bool _isMusicStopped = false;

        public PlaylistType CurrentPlaylistType => currentPlaylistType;
        public int CurrentTrackIndex => currentTrackIndex;
        public bool IsCrossfading => isCrossfading;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            EnsureAudioSources();
            ResolveConfig();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            if (!_isInitialized)
            {
                _isInitialized = true;
                string activeSceneName = SceneManager.GetActiveScene().name;
                PlaylistType sceneType = GetPlaylistTypeForScene(activeSceneName);
                PlayPlaylist(sceneType, 0, false);
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            StopAllMusicImmediate();

            if (Instance == this) Instance = null;
        }

        private void EnsureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length >= 2)
            {
                _audioSourceA = sources[0];
                _audioSourceB = sources[1];
            }
            else
            {
                if (_audioSourceA == null) _audioSourceA = gameObject.AddComponent<AudioSource>();
                if (_audioSourceB == null) _audioSourceB = gameObject.AddComponent<AudioSource>();
            }

            ConfigureAudioSource(_audioSourceA);
            ConfigureAudioSource(_audioSourceB);
            _activeAudioSource = _audioSourceA;
        }

        private void ConfigureAudioSource(AudioSource src)
        {
            if (src == null) return;
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D stereo background music
            src.bypassEffects = true;
            src.bypassListenerEffects = true;
            src.bypassReverbZones = true;
            src.volume = 0f;
        }

        public void ResolveConfig()
        {
            if (audioConfig == null)
            {
                if (FMODAudioManager.Instance != null && FMODAudioManager.Instance.Config != null)
                {
                    audioConfig = FMODAudioManager.Instance.Config;
                }
                else
                {
                    audioConfig = Resources.Load<FMODAudioConfigSO>("FMODAudioConfig");
#if UNITY_EDITOR
                    if (audioConfig == null)
                    {
                        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:FMODAudioConfigSO");
                        if (guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            audioConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<FMODAudioConfigSO>(path);
                        }
                    }
#endif
                }
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveConfig();
            PlaylistType targetType = GetPlaylistTypeForScene(scene.name);

            // If switching to a new scene playlist type, crossfade to track 0
            if (targetType != currentPlaylistType || !_isInitialized)
            {
                _isInitialized = true;
                CrossfadeToTrack(targetType, 0);
            }
        }

        public PlaylistType GetPlaylistTypeForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return PlaylistType.MainMenu;

            if (sceneName.IndexOf("MainMenu", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PlaylistType.MainMenu;
            }
            if (sceneName.IndexOf("Credit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return PlaylistType.Credits;
            }
            return PlaylistType.Gameplay;
        }

        private void Update()
        {
            if (isCrossfading) return;

            MonitorTrackCompletion();
        }

        private void MonitorTrackCompletion()
        {
            if (_isMusicStopped) return;

            // 1. Check FMOD Event playback state
            if (_activeFmodInstance.isValid())
            {
                _activeFmodInstance.getPlaybackState(out PLAYBACK_STATE state);
                if (state == PLAYBACK_STATE.STOPPED)
                {
                    PlayNextTrack();
                }
                return;
            }

            // 2. Check Fallback Unity AudioSource
            if (_activeAudioSource != null && _activeAudioSource.clip != null)
            {
                if (!_activeAudioSource.isPlaying)
                {
                    PlayNextTrack();
                }
            }
        }

        public void PlayPlaylist(PlaylistType type, int startTrack = 0, bool instant = false)
        {
            currentPlaylistType = type;
            currentTrackIndex = startTrack;

            float crossfadeDur = instant ? 0.05f : (audioConfig != null ? audioConfig.musicCrossfadeDuration : 2.2f);
            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(type, startTrack, crossfadeDur));
        }

        public void CrossfadeToTrack(PlaylistType type, int trackIndex)
        {
            _isMusicStopped = false;
            currentPlaylistType = type;
            currentTrackIndex = trackIndex;

            float crossfadeDur = audioConfig != null ? audioConfig.musicCrossfadeDuration : 2.2f;
            if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
            _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(type, trackIndex, crossfadeDur));
        }

        public void PlayNextTrack()
        {
            int totalTracks = GetTrackCount(currentPlaylistType);
            if (totalTracks <= 0) return;

            int nextIndex = (currentTrackIndex + 1) % totalTracks;
            CrossfadeToTrack(currentPlaylistType, nextIndex);
        }

        public void PlayPreviousTrack()
        {
            int totalTracks = GetTrackCount(currentPlaylistType);
            if (totalTracks <= 0) return;

            int prevIndex = (currentTrackIndex - 1 + totalTracks) % totalTracks;
            CrossfadeToTrack(currentPlaylistType, prevIndex);
        }

        private int GetTrackCount(PlaylistType type)
        {
            ResolveConfig();
            if (audioConfig == null) return 0;

            EventReference[] fmodList = GetFmodPlaylist(type);
            if (fmodList != null && fmodList.Length > 0 && HasValidFmodEvent(fmodList))
            {
                return fmodList.Length;
            }

            AudioClip[] fallbackList = GetFallbackPlaylist(type);
            if (fallbackList != null && fallbackList.Length > 0)
            {
                return fallbackList.Length;
            }

            return 0;
        }

        private EventReference[] GetFmodPlaylist(PlaylistType type)
        {
            if (audioConfig == null) return null;
            switch (type)
            {
                case PlaylistType.MainMenu: return audioConfig.mainMenuMusicPlaylist;
                case PlaylistType.Credits: return audioConfig.creditsMusicPlaylist;
                case PlaylistType.Gameplay:
                default:
                    return audioConfig.gameplayMusicPlaylist;
            }
        }

        private AudioClip[] GetFallbackPlaylist(PlaylistType type)
        {
            if (audioConfig == null) return null;
            switch (type)
            {
                case PlaylistType.MainMenu: return audioConfig.fallbackMainMenuClips;
                case PlaylistType.Credits: return audioConfig.fallbackCreditsClips;
                case PlaylistType.Gameplay:
                default:
                    return audioConfig.fallbackGameplayClips;
            }
        }

        private bool HasValidFmodEvent(EventReference[] list)
        {
            if (list == null) return false;
            for (int i = 0; i < list.Length; i++)
            {
                if (!list[i].IsNull) return true;
            }
            return false;
        }

        private IEnumerator CrossfadeRoutine(PlaylistType type, int trackIndex, float duration)
        {
            isCrossfading = true;
            duration = Mathf.Max(0.05f, duration);

            // 1. Wait for scene dissolve transition to complete so music doesn't play in black screen
            while (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
            {
                yield return null;
            }

            // 2. For gameplay music: wait until the player actually connects / starts the game
            if (type == PlaylistType.Gameplay)
            {
                while (UIManager.Instance != null && !UIManager.Instance.IsOperatorConnected)
                {
                    yield return null;
                }
            }

            EventReference[] fmodList = GetFmodPlaylist(type);
            bool useFmod = fmodList != null && fmodList.Length > 0 && HasValidFmodEvent(fmodList);
            bool hasOutgoing = (_activeFmodInstance.isValid()) || (_activeAudioSource != null && _activeAudioSource.isPlaying);

            if (useFmod)
            {
                // ---------------- FMOD STUDIO CROSSFADE ----------------
                EventReference targetRef = fmodList[trackIndex % fmodList.Length];
                EventInstance incomingInstance = default;

                if (!targetRef.IsNull)
                {
                    incomingInstance = RuntimeManager.CreateInstance(targetRef);
                    // If crossfading from another track, start volume at 0.
                    // If starting fresh (no previous music), start at full volume (1f) so FMOD's internal fade / intro plays unhindered!
                    incomingInstance.setVolume(hasOutgoing ? 0f : 1f);
                    incomingInstance.start();
                }

                _fadingOutFmodInstance = _activeFmodInstance;
                _activeFmodInstance = incomingInstance;

                // Stop any playing fallback AudioSources
                if (_activeAudioSource != null && _activeAudioSource.isPlaying)
                {
                    _fadingOutAudioSource = _activeAudioSource;
                }

                if (hasOutgoing)
                {
                    float elapsed = 0f;
                    while (elapsed < duration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float t = Mathf.Clamp01(elapsed / duration);
                        // Equal-power / sinusoidal perceptual audio curve (prevents abrupt linear volume jump)
                        float inVol = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 0.5f));
                        float outVol = Mathf.Clamp01(Mathf.Cos(t * Mathf.PI * 0.5f));

                        if (_fadingOutFmodInstance.isValid())
                        {
                            _fadingOutFmodInstance.setVolume(outVol);
                        }
                        if (_activeFmodInstance.isValid())
                        {
                            _activeFmodInstance.setVolume(inVol);
                        }
                        if (_fadingOutAudioSource != null)
                        {
                            _fadingOutAudioSource.volume = outVol;
                        }

                        yield return null;
                    }

                    // Clean up outgoing FMOD instance
                    if (_fadingOutFmodInstance.isValid())
                    {
                        _fadingOutFmodInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                        _fadingOutFmodInstance.release();
                    }

                    if (_fadingOutAudioSource != null)
                    {
                        _fadingOutAudioSource.Stop();
                        _fadingOutAudioSource.volume = 0f;
                        _fadingOutAudioSource = null;
                    }
                }

                if (_activeFmodInstance.isValid())
                {
                    _activeFmodInstance.setVolume(1f);
                }
            }
            else
            {
                // ---------------- FALLBACK AUDIOCLIP CROSSFADE ----------------
                AudioClip[] fallbackList = GetFallbackPlaylist(type);
                AudioClip targetClip = null;

                if (fallbackList != null && fallbackList.Length > 0)
                {
                    targetClip = fallbackList[trackIndex % fallbackList.Length];
                }

                // Release outgoing FMOD instance if active
                if (_activeFmodInstance.isValid())
                {
                    _fadingOutFmodInstance = _activeFmodInstance;
                    _activeFmodInstance = default;
                }

                AudioSource incomingSource = (_activeAudioSource == _audioSourceA) ? _audioSourceB : _audioSourceA;
                _fadingOutAudioSource = _activeAudioSource;
                _activeAudioSource = incomingSource;

                if (targetClip != null)
                {
                    _activeAudioSource.clip = targetClip;
                    _activeAudioSource.volume = 0f;
                    _activeAudioSource.loop = (fallbackList.Length == 1); // Single track loops automatically
                    _activeAudioSource.Play();
                }

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float inVol = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 0.5f));
                    float outVol = Mathf.Clamp01(Mathf.Cos(t * Mathf.PI * 0.5f));

                    if (_fadingOutAudioSource != null && _fadingOutAudioSource.isPlaying)
                    {
                        _fadingOutAudioSource.volume = outVol;
                    }
                    if (_fadingOutFmodInstance.isValid())
                    {
                        _fadingOutFmodInstance.setVolume(outVol);
                    }
                    if (_activeAudioSource != null && targetClip != null)
                    {
                        _activeAudioSource.volume = inVol;
                    }

                    yield return null;
                }

                if (_fadingOutAudioSource != null)
                {
                    _fadingOutAudioSource.Stop();
                    _fadingOutAudioSource.volume = 0f;
                    _fadingOutAudioSource = null;
                }

                if (_fadingOutFmodInstance.isValid())
                {
                    _fadingOutFmodInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    _fadingOutFmodInstance.release();
                }

                if (_activeAudioSource != null && targetClip != null)
                {
                    _activeAudioSource.volume = 1f;
                }
            }

            isCrossfading = false;
            _crossfadeCoroutine = null;
            Debug.Log($"<color=#33FF88>[MusicPlaylistManager] Crossfaded to {type} [Track #{trackIndex + 1}]</color>");
        }

        /// <summary>
        /// Stops the current music track with the specified FMOD stop mode (e.g. ALLOWFADEOUT).
        /// Disables automatic track progression until a new track is explicitly requested.
        /// </summary>
        public void StopMusic(FMOD.Studio.STOP_MODE stopMode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
        {
            _isMusicStopped = true;

            if (_crossfadeCoroutine != null)
            {
                StopCoroutine(_crossfadeCoroutine);
                _crossfadeCoroutine = null;
            }

            if (_activeFmodInstance.isValid())
            {
                _activeFmodInstance.stop(stopMode);
                _activeFmodInstance.release();
                _activeFmodInstance.clearHandle();
            }

            if (_fadingOutFmodInstance.isValid())
            {
                _fadingOutFmodInstance.stop(stopMode);
                _fadingOutFmodInstance.release();
                _fadingOutFmodInstance.clearHandle();
            }

            if (_audioSourceA != null)
            {
                _audioSourceA.Stop();
                _audioSourceA.volume = 0f;
            }

            if (_audioSourceB != null)
            {
                _audioSourceB.Stop();
                _audioSourceB.volume = 0f;
            }

            isCrossfading = false;
        }

        public void StopAllMusicImmediate()
        {
            StopMusic(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }
    }
}
