using UnityEngine;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Central audio configuration ScriptableObject.
    /// The Sound Designer can assign audio clips directly into this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Orbital Panopticon/Audio Config")]
    public class AudioConfig : ScriptableObject
    {
        [Header("1. Drone Flight & Thrusters")]
        [Tooltip("Looping hum for active drone movement.")]
        public AudioClip thrusterLoop;

        [Tooltip("Sound played upon executing a directional dash (Left Shift).")]
        public AudioClip dashBoostSfx;

        [Tooltip("Sound played when active retro-braking (X / C) is engaged.")]
        public AudioClip brakeSfx;

        [Header("2. Gravity Manipulator (Gravity Gun)")]
        [Tooltip("Looping hum when gravity tractor beam is active.")]
        public AudioClip magnetPullLoop;

        [Tooltip("Impact / latch sound when a debris item is snapped into the claw.")]
        public AudioClip grabLatchSfx;

        [Tooltip("Ballistic punt launch blast sound (LMB).")]
        public AudioClip puntLaunchSfx;

        [Tooltip("Dry blast shockwave sound when punting with no held cargo.")]
        public AudioClip dryPuntSfx;

        [Tooltip("Special chime played when grabbing a Camera-Observed x3 target.")]
        public AudioClip observedGrabChimeSfx;

        [Header("3. Black Hole & Accretion Dynamics")]
        [Tooltip("Deep sub-bass cosmic hum looping near the black hole.")]
        public AudioClip blackHoleAmbientLoop;

        [Tooltip("Accelerated high-speed vortex swirl whoosh.")]
        public AudioClip accretionSwirlLoop;

        [Tooltip("Heavy gravitational compression / crunch when debris crosses the event horizon.")]
        public AudioClip eventHorizonCrunchSfx;

        [Tooltip("Gravitational shockwave ring explosion for Hawking Pulse.")]
        public AudioClip hawkingPulseSfx;

        [Header("4. Surveillance Panopticon & Cameras")]
        [Tooltip("Mechanical servo beep when a surveillance camera acquires a target.")]
        public AudioClip cameraLockBeepSfx;

        [Tooltip("Gravitational burst sound for Camera Gravi-Flash.")]
        public AudioClip cameraGraviFlashSfx;

        [Tooltip("Pulsing alarm siren when approaching arena boundary.")]
        public AudioClip boundaryWarningSirenSfx;

        [Header("5. Quota & UI Feedback")]
        [Tooltip("Triumphant corporate fanfare when a quota is completed.")]
        public AudioClip quotaCompleteFanfareSfx;

        [Tooltip("Tech sound played when purchasing a skill tree node.")]
        public AudioClip upgradePurchasedSfx;

        [Tooltip("Scrap coin acquisition sound.")]
        public AudioClip coinEarnedSfx;

        [Tooltip("Generic button click SFX.")]
        public AudioClip buttonClickSfx;

        [Header("6. Endgame Finale")]
        [Tooltip("Emergency sector collapse siren.")]
        public AudioClip sectorCollapseAlarmSfx;

        [Tooltip("Massive destruction rumble when Corporate Overseer Eye is swallowed.")]
        public AudioClip overseerDestructionSfx;

        [Tooltip("Victory / Shift Complete summary screen chime.")]
        public AudioClip victoryChimeSfx;
    }
}
