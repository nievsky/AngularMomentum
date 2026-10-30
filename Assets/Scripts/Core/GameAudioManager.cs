using UnityEngine;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Central audio manager coordinating sound playback throughout the game.
    /// Access via GameAudioManager.Instance.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameAudioManager : MonoBehaviour
    {
        public static GameAudioManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private AudioConfig audioConfig;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource ambientSource;
        [SerializeField] private AudioSource thrusterSource;

        public AudioConfig Config => audioConfig;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureAudioSources();
        }

        private void EnsureAudioSources()
        {
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }
            if (ambientSource == null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.playOnAwake = false;
                ambientSource.loop = true;
                ambientSource.spatialBlend = 0f;
                ambientSource.volume = 0.5f;
            }
            if (thrusterSource == null)
            {
                thrusterSource = gameObject.AddComponent<AudioSource>();
                thrusterSource.playOnAwake = false;
                thrusterSource.loop = true;
                thrusterSource.spatialBlend = 0f;
                thrusterSource.volume = 0.4f;
            }
        }

        public void SetAudioConfig(AudioConfig config)
        {
            audioConfig = config;
        }

        public void PlayOneShot(AudioClip clip, float volume = 1.0f, float pitchVariation = 0.05f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
            sfxSource.PlayOneShot(clip, volume);
        }

        public void PlayPunt() => PlayOneShot(audioConfig != null ? audioConfig.puntLaunchSfx : null);
        public void PlayGrab() => PlayOneShot(audioConfig != null ? audioConfig.grabLatchSfx : null);
        public void PlayDash() => PlayOneShot(audioConfig != null ? audioConfig.dashBoostSfx : null);
        public void PlayObservedGrab() => PlayOneShot(audioConfig != null ? audioConfig.observedGrabChimeSfx : null);
        public void PlayQuotaComplete() => PlayOneShot(audioConfig != null ? audioConfig.quotaCompleteFanfareSfx : null);
        public void PlayConsume() => PlayOneShot(audioConfig != null ? audioConfig.eventHorizonCrunchSfx : null);
    }
}
