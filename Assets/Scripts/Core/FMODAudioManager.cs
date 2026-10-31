using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using FMODUnity;
using FMOD.Studio;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Player;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Central audio controller integrating FMOD Studio 2.03.08.
    /// Manages persistent audio loops, real-time parameter automation (BlackHoleStage, DistanceToHole, DroneSpeed, ActiveContainmentBeams, ComboLevel),
    /// multi-track background music crossfading via MusicPlaylistManager, and provides null-safe one-shot sound playback throughout gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public class FMODAudioManager : MonoBehaviour
    {
        public static FMODAudioManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private FMODAudioConfigSO audioConfig;

        [Header("Subsystems")]
        [SerializeField] private MusicPlaylistManager playlistManager;

        [Header("Scene References (Auto-Resolved if Unassigned)")]
        [SerializeField] private Transform blackHoleTransform;
        [SerializeField] private DroneController playerDrone;
        [SerializeField] private GravityManipulatorController gravityManipulator;
        [SerializeField] private BlackHoleStageManager stageManager;
        [SerializeField] private BlackHoleMassAndEnergyManager massAndEnergyManager;

        // Persistent Event Instances
        private EventInstance _spaceAmbienceInstance;
        private EventInstance _singularityHumInstance;
        private EventInstance _containmentBeamHumInstance;
        private EventInstance _thrusterInstance;
        private EventInstance _gravityGunHoldInstance;
        private EventInstance _criticalMassAlarmInstance;

        // Loop state flags
        private bool _isGravityHoldingActive = false;
        private bool _isAlarmActive = false;

        public FMODAudioConfigSO Config => audioConfig;
        public MusicPlaylistManager PlaylistManager => playlistManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var existing = FindAnyObjectByType<FMODAudioManager>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    GameObject go = new GameObject("[FMOD_AUDIO_MANAGER]");
                    Instance = go.AddComponent<FMODAudioManager>();
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

            if (playlistManager == null) playlistManager = GetComponent<MusicPlaylistManager>();
            if (playlistManager == null) playlistManager = gameObject.AddComponent<MusicPlaylistManager>();

            ResolveReferences();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            InitializeLoops();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (audioConfig == null)
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

            if (blackHoleTransform == null)
            {
                GameObject bh = GameObject.Find("BlackHoleSystem");
                if (bh != null) blackHoleTransform = bh.transform;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam.GetComponent<StudioListener>() == null)
            {
                mainCam.gameObject.AddComponent<StudioListener>();
            }

            if (playerDrone == null) playerDrone = DroneController.Instance;
            if (playerDrone == null) playerDrone = FindAnyObjectByType<DroneController>();

            if (gravityManipulator == null) gravityManipulator = GravityManipulatorController.Instance;
            if (gravityManipulator == null) gravityManipulator = FindAnyObjectByType<GravityManipulatorController>();

            if (stageManager == null) stageManager = BlackHoleStageManager.Instance;
            if (stageManager == null) stageManager = FindAnyObjectByType<BlackHoleStageManager>();

            if (massAndEnergyManager == null) massAndEnergyManager = BlackHoleMassAndEnergyManager.Instance;
            if (massAndEnergyManager == null) massAndEnergyManager = FindAnyObjectByType<BlackHoleMassAndEnergyManager>();
        }

        private void InitializeLoops()
        {
            if (audioConfig == null) return;

            // 1. Space Ambience Loop (2D persistent background)
            if (!audioConfig.spaceAmbienceLoop.IsNull)
            {
                _spaceAmbienceInstance = RuntimeManager.CreateInstance(audioConfig.spaceAmbienceLoop);
                _spaceAmbienceInstance.start();
            }

            // 2. Singularity Hum Loop (3D at Black Hole center)
            if (!audioConfig.singularityHumLoop.IsNull)
            {
                _singularityHumInstance = RuntimeManager.CreateInstance(audioConfig.singularityHumLoop);
                if (blackHoleTransform != null)
                {
                    _singularityHumInstance.set3DAttributes(RuntimeUtils.To3DAttributes(blackHoleTransform.position));
                }
                _singularityHumInstance.start();
            }

            // 3. Containment Beam Hum Loop (Ambient hum of active Eye beams)
            if (!audioConfig.containmentBeamHumLoop.IsNull)
            {
                _containmentBeamHumInstance = RuntimeManager.CreateInstance(audioConfig.containmentBeamHumLoop);
                if (blackHoleTransform != null)
                {
                    _containmentBeamHumInstance.set3DAttributes(RuntimeUtils.To3DAttributes(blackHoleTransform.position));
                }
                _containmentBeamHumInstance.start();
            }

            // 4. Thruster Engine Loop
            if (!audioConfig.thrusterLoop.IsNull)
            {
                _thrusterInstance = RuntimeManager.CreateInstance(audioConfig.thrusterLoop);
                _thrusterInstance.start();
            }

            // 5. Gravity Gun Tether Hold Loop (prepared)
            if (!audioConfig.gravityGunHoldLoop.IsNull)
            {
                _gravityGunHoldInstance = RuntimeManager.CreateInstance(audioConfig.gravityGunHoldLoop);
            }

            // 6. Critical Mass Alarm Loop (prepared)
            if (!audioConfig.criticalMassAlarmLoop.IsNull)
            {
                _criticalMassAlarmInstance = RuntimeManager.CreateInstance(audioConfig.criticalMassAlarmLoop);
            }
        }

        private void Update()
        {
            if (audioConfig == null) return;

            // Re-resolve dynamic references if needed
            if (playerDrone == null) playerDrone = DroneController.Instance;
            if (gravityManipulator == null) gravityManipulator = GravityManipulatorController.Instance;
            if (stageManager == null) stageManager = BlackHoleStageManager.Instance;
            if (massAndEnergyManager == null) massAndEnergyManager = BlackHoleMassAndEnergyManager.Instance;

            UpdateGlobalParameters();
            UpdateDynamicLoops();
        }

        private void UpdateGlobalParameters()
        {
            // 1. BlackHoleStage (1.0 to 4.0)
            float stage = 1.0f;
            if (stageManager != null)
            {
                stage = Mathf.Clamp(stageManager.CurrentStage, 1, 4);
            }
            else if (massAndEnergyManager != null)
            {
                stage = Mathf.Clamp(massAndEnergyManager.CurrentStage, 1, 4);
            }
            SetParameter(audioConfig.paramBlackHoleStage, stage);

            // 2. DistanceToHole (0.0 = at Event Horizon, 1.0 = safe outer orbit / boundary)
            float distRatio = 1.0f;
            if (playerDrone != null && blackHoleTransform != null)
            {
                float dist = Vector3.Distance(playerDrone.transform.position, blackHoleTransform.position);
                GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                float horizonRadius = cfg != null ? cfg.eventHorizonRadius : 3.5f;
                float outerRadius = cfg != null ? cfg.arenaMaxRadius : 48.0f;
                distRatio = Mathf.Clamp01((dist - horizonRadius) / Mathf.Max(1.0f, outerRadius - horizonRadius));
            }
            SetParameter(audioConfig.paramDistanceToHole, distRatio);

            // 3. DroneSpeed (0.0 = idle, 1.0 = max velocity)
            float speedRatio = 0f;
            if (playerDrone != null && playerDrone.Rigidbody != null)
            {
                GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                float maxSpeed = cfg != null ? cfg.maxThrusterSpeed : 12.0f;
                speedRatio = Mathf.Clamp01(playerDrone.Rigidbody.linearVelocity.magnitude / Mathf.Max(1.0f, maxSpeed));
            }
            SetParameter(audioConfig.paramDroneSpeed, speedRatio);

            // 4. ActiveContainmentBeams (4 to 0)
            float activeBeams = 4f;
            if (stageManager != null)
            {
                activeBeams = stageManager.ActiveContainmentLocksCount;
            }
            SetParameter(audioConfig.paramActiveBeams, activeBeams);

            // 5. ComboLevel (Integer / Quota combo level)
            float combo = 0f;
            if (QuotaAndScoreManager.Instance != null)
            {
                combo = QuotaAndScoreManager.Instance.CompletedQuotasCount;
            }
            SetParameter(audioConfig.paramComboLevel, combo);
        }

        private void UpdateDynamicLoops()
        {
            Vector3 playerPos = playerDrone != null ? playerDrone.transform.position : transform.position;

            // 1. Gravity Gun Tether Hold Loop
            bool isHolding = gravityManipulator != null && gravityManipulator.IsHolding;
            if (isHolding && !_isGravityHoldingActive)
            {
                _isGravityHoldingActive = true;
                if (_gravityGunHoldInstance.isValid())
                {
                    _gravityGunHoldInstance.set3DAttributes(RuntimeUtils.To3DAttributes(playerPos));
                    _gravityGunHoldInstance.start();
                }
            }
            else if (!isHolding && _isGravityHoldingActive)
            {
                _isGravityHoldingActive = false;
                if (_gravityGunHoldInstance.isValid())
                {
                    _gravityGunHoldInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                }
            }
            else if (_isGravityHoldingActive && _gravityGunHoldInstance.isValid())
            {
                _gravityGunHoldInstance.set3DAttributes(RuntimeUtils.To3DAttributes(playerPos));
            }

            // 2. Critical Mass Alarm Loop (Stage 4 warning)
            int curStage = stageManager != null ? stageManager.CurrentStage : (massAndEnergyManager != null ? massAndEnergyManager.CurrentStage : 1);
            bool shouldAlarm = curStage >= 4;
            if (shouldAlarm && !_isAlarmActive)
            {
                _isAlarmActive = true;
                if (_criticalMassAlarmInstance.isValid())
                {
                    _criticalMassAlarmInstance.start();
                }
            }
            else if (!shouldAlarm && _isAlarmActive)
            {
                _isAlarmActive = false;
                if (_criticalMassAlarmInstance.isValid())
                {
                    _criticalMassAlarmInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                }
            }

            // 3. Singularity Hum & Containment Beam Hum 3D position refresh
            if (blackHoleTransform != null)
            {
                var bhAttributes = RuntimeUtils.To3DAttributes(blackHoleTransform.position);
                if (_singularityHumInstance.isValid())
                {
                    _singularityHumInstance.set3DAttributes(bhAttributes);
                }
                if (_containmentBeamHumInstance.isValid())
                {
                    _containmentBeamHumInstance.set3DAttributes(bhAttributes);
                }
            }

            // 4. Thruster Loop 3D position
            if (playerDrone != null && _thrusterInstance.isValid())
            {
                _thrusterInstance.set3DAttributes(RuntimeUtils.To3DAttributes(playerPos));
            }
        }

        private void SetParameter(string paramName, float value)
        {
            if (string.IsNullOrEmpty(paramName)) return;

            // Global Studio parameter
            RuntimeManager.StudioSystem.setParameterByName(paramName, value);

            // Per-instance fallback
            if (_spaceAmbienceInstance.isValid()) _spaceAmbienceInstance.setParameterByName(paramName, value);
            if (_singularityHumInstance.isValid()) _singularityHumInstance.setParameterByName(paramName, value);
            if (_containmentBeamHumInstance.isValid()) _containmentBeamHumInstance.setParameterByName(paramName, value);
            if (_thrusterInstance.isValid()) _thrusterInstance.setParameterByName(paramName, value);
            if (_gravityGunHoldInstance.isValid()) _gravityGunHoldInstance.setParameterByName(paramName, value);
            if (_criticalMassAlarmInstance.isValid()) _criticalMassAlarmInstance.setParameterByName(paramName, value);
        }

        #region Public Music Playlist API

        public void CrossfadeToMusicTrack(MusicPlaylistManager.PlaylistType type, int trackIndex)
        {
            if (playlistManager != null) playlistManager.CrossfadeToTrack(type, trackIndex);
        }

        public void PlayNextMusicTrack()
        {
            if (playlistManager != null) playlistManager.PlayNextTrack();
        }

        public void PlayPreviousMusicTrack()
        {
            if (playlistManager != null) playlistManager.PlayPreviousTrack();
        }

        #endregion

        #region Public One-Shot Trigger API

        /// <summary>
        /// Null-safe helper to play any FMOD EventReference at an optional world position.
        /// </summary>
        public void PlayOneShot(EventReference eventRef, Vector3 position = default)
        {
            if (eventRef.IsNull) return;
            RuntimeManager.PlayOneShot(eventRef, position);
        }

        public void PlayScrapSpaghettify(Vector3 position)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.scrapSpaghettifyOneShot, position);
        }

        public void PlayShiftDash(Vector3 position)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.shiftDashOneShot, position);
        }

        public void PlayGravityGunPunt(Vector3 position)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.gravityGunPuntOneShot, position);
        }

        public void PlayContainmentLockBreak(Vector3 position = default)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.containmentLockBreakOneShot, position);
        }

        public void PlayCommsOpenBeep()
        {
            if (audioConfig != null) PlayOneShot(audioConfig.commsOpenBeepOneShot);
        }

        public void PlayCommsVoiceBlip()
        {
            if (audioConfig != null) PlayOneShot(audioConfig.commsVoiceBlipOneShot);
        }

        public void PlayFinaleEyesConsumedRumble(Vector3 position = default)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.finaleEyesConsumedRumbleOneShot, position);
        }

        public void PlayPlayerHorizonWarp(Vector3 position = default)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.playerHorizonWarpOneShot, position);
        }

        public void PlayDissolveTransition()
        {
            if (audioConfig != null) PlayOneShot(audioConfig.dissolveTransitionOneShot);
        }

        public void PlayBoundaryWarning(Vector3 position = default)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.boundaryWarningBeep, position);
        }

        public void PlaySignalLostRespawn(Vector3 position = default)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.signalLostRespawnOneShot, position);
        }

        public void PlayScorePopup(Vector3 position = default)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.scorePopupOneShot, position);
        }

        public void PlayValuableScrapConsume(Vector3 position)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.valuableScrapConsumeOneShot, position);
        }

        public void PlayTerminalToggle()
        {
            if (audioConfig != null) PlayOneShot(audioConfig.terminalToggleOneShot);
        }

        public void PlayUpgradeBuy()
        {
            if (audioConfig != null) PlayOneShot(audioConfig.upgradeBuyOneShot);
        }

        public void PlayCollapseGameOver(Vector3 position = default)
        {
            if (audioConfig != null) PlayOneShot(audioConfig.collapseGameOverOneShot, position);
        }

        /// <summary>
        /// Backward-compatible alias for containment lock destruction.
        /// </summary>
        public void PlayOverseerDestroyed(Vector3 position = default)
        {
            PlayContainmentLockBreak(position);
        }

        #endregion

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            ReleaseInstance(ref _spaceAmbienceInstance);
            ReleaseInstance(ref _singularityHumInstance);
            ReleaseInstance(ref _containmentBeamHumInstance);
            ReleaseInstance(ref _thrusterInstance);
            ReleaseInstance(ref _gravityGunHoldInstance);
            ReleaseInstance(ref _criticalMassAlarmInstance);

            if (Instance == this) Instance = null;
        }

        private void ReleaseInstance(ref EventInstance instance)
        {
            if (instance.isValid())
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
            }
        }
    }
}
