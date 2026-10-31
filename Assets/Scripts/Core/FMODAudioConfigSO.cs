using System;
using UnityEngine;
using FMODUnity;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Centralized ScriptableObject configuration for FMOD Studio 2.03.08 events and parameters.
    /// Exposes background music playlists with crossfade settings, Black Hole ambience, Containment Eyes,
    /// Star Fox style narrative comms, cinematic finale, player drone mechanics, and UI.
    /// </summary>
    [CreateAssetMenu(fileName = "FMODAudioConfig", menuName = "OrbitalPanopticon/Audio/FMOD Audio Config")]
    public class FMODAudioConfigSO : ScriptableObject
    {
        [Header("0. Background Music Playlists")]
        [Tooltip("Sequential or randomized FMOD music event playlist for the Main Menu.")]
        public EventReference[] mainMenuMusicPlaylist;

        [Tooltip("Sequential or randomized FMOD music event playlist for active Gameplay.")]
        public EventReference[] gameplayMusicPlaylist;

        [Tooltip("Sequential or randomized FMOD music event playlist for the Credits/Ending scene.")]
        public EventReference[] creditsMusicPlaylist;

        [Tooltip("Optional standard Unity AudioClip arrays for testing Main Menu music before FMOD banks are built.")]
        public AudioClip[] fallbackMainMenuClips;

        [Tooltip("Optional standard Unity AudioClip arrays for testing Gameplay music before FMOD banks are built.")]
        public AudioClip[] fallbackGameplayClips;

        [Tooltip("Optional standard Unity AudioClip arrays for testing Credits music before FMOD banks are built.")]
        public AudioClip[] fallbackCreditsClips;

        [Tooltip("Smooth crossfade transition duration in seconds between music tracks and scenes.")]
        [Range(0.5f, 10f)]
        public float musicCrossfadeDuration = 2.2f;

        [Header("1. Black Hole & Ambience")]
        [Tooltip("Ambient background drone loop (2D)")]
        public EventReference spaceAmbienceLoop;

        [Tooltip("3D spatial hum loop positioned at Black Hole singularity")]
        public EventReference singularityHumLoop;

        [Tooltip("One-shot played when scrap item is spaghettified into event horizon")]
        public EventReference scrapSpaghettifyOneShot;

        [Tooltip("Tense alarm loop warning of near-critical mass (Stage 4)")]
        public EventReference criticalMassAlarmLoop;

        [Tooltip("Massive collapse failure sound on catastrophic implosion")]
        public EventReference collapseGameOverOneShot;

        [Header("2. Containment Eyes & Narrative Comms")]
        [Tooltip("Ambient hum of active Eye containment beams; modulated by active beams count (4 -> 0)")]
        public EventReference containmentBeamHumLoop;

        [Tooltip("One-shot played when a stage completes and one Eye containment lock breaks")]
        public EventReference containmentLockBreakOneShot;

        [Tooltip("Radio transmission open beep for Star Fox style comms box")]
        public EventReference commsOpenBeepOneShot;

        [Tooltip("Typewriter radio chatter blip for incoming comms characters")]
        public EventReference commsVoiceBlipOneShot;

        [Tooltip("Heavy ominous rumble when Panopticon Eyes are consumed into the Singularity")]
        public EventReference finaleEyesConsumedRumbleOneShot;

        [Tooltip("Relativistic audio warp when the player is pulled into the Event Horizon")]
        public EventReference playerHorizonWarpOneShot;

        [Tooltip("Subtle noise sweep when the screen dissolves between scenes")]
        public EventReference dissolveTransitionOneShot;

        [Header("3. Player Drone & Gravity Gun")]
        [Tooltip("Continuous engine thruster loop modulated by player drone speed")]
        public EventReference thrusterLoop;

        [Tooltip("Short burst whoosh when player activates Shift Dash")]
        public EventReference shiftDashOneShot;

        [Tooltip("Gravitational beam loop active while holding items in tether")]
        public EventReference gravityGunHoldLoop;

        [Tooltip("Heavy kinetic punch sound when punting scrap towards vortex or targets")]
        public EventReference gravityGunPuntOneShot;

        [Tooltip("Warning beep loop/cue when straying near outer arena boundary")]
        public EventReference boundaryWarningBeep;

        [Tooltip("Signal lost / drone respawn sound cue")]
        public EventReference signalLostRespawnOneShot;

        [Header("4. Economy & UI")]
        [Tooltip("Score popup chime on quota deposit or point gain")]
        public EventReference scorePopupOneShot;

        [Tooltip("Distinct resonant chime when valuable glowing scrap is consumed")]
        public EventReference valuableScrapConsumeOneShot;

        [Tooltip("HUD/Terminal toggle click sound (TAB Upgrade Tree)")]
        public EventReference terminalToggleOneShot;

        [Tooltip("Satisfying mechanical confirmation on purchasing an upgrade")]
        public EventReference upgradeBuyOneShot;

        [Header("5. Global FMOD Parameter Names")]
        [Tooltip("Black Hole Evolution Stage (1 to 4)")]
        public string paramBlackHoleStage = "BlackHoleStage";

        [Tooltip("Normalized distance to Singularity (0 = at horizon, 1 = safe outer ring)")]
        public string paramDistanceToHole = "DistanceToHole";

        [Tooltip("Normalized Drone Velocity magnitude (0 = stationary, 1 = max dash)")]
        public string paramDroneSpeed = "DroneSpeed";

        [Tooltip("Active Containment Beams Count (4 to 0)")]
        public string paramActiveBeams = "ActiveContainmentBeams";

        [Tooltip("Player Quota / Combo Level (integer scale)")]
        public string paramComboLevel = "ComboLevel";
    }
}
