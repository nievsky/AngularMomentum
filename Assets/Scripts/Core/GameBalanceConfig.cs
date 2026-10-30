using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [CreateAssetMenu(fileName = "GameBalanceConfig", menuName = "Orbital Panopticon/Game Balance Config")]
    public class GameBalanceConfig : ScriptableObject
    {
        private static GameBalanceConfig _instance;

        public static GameBalanceConfig Instance
        {
            get
            {
                if (_instance == null)
                {
#if UNITY_EDITOR
                    string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameBalanceConfig");
                    if (guids.Length > 0)
                    {
                        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBalanceConfig>(path);
                    }
#endif
                }
                return _instance;
            }
            set => _instance = value;
        }

        // =========================================================================
        // 0. Global Pacing
        // =========================================================================
        [Header("0. Global Pacing")]
        [Tooltip("Master global speed multiplier for pacing all physics and entity speeds (defaults to 0.5 for calm, deliberate pacing).")]
        [Range(0.1f, 2.0f)]
        public float globalSimulationSpeed = 0.5f;

        // =========================================================================
        // 1. Black Hole & Two-Zone Physics
        // =========================================================================
        [Header("1. Black Hole & Two-Zone Physics")]
        [Tooltip("Orbit direction: true for clockwise, false for counter-clockwise.")]
        public bool clockwiseOrbit = true;

        [Tooltip("The accretion threshold radius. Outside this radius, floating scrap NEVER falls inward.")]
        [Range(5f, 35f)]
        public float accretionThresholdRadius = 15.0f;

        [Tooltip("Lethal radius where spaghettification and consumption occur.")]
        [Range(1f, 10f)]
        public float eventHorizonRadius = 3.5f;

        [Tooltip("Very slow, predictable clockwise orbit speed for floating scrap outside the threshold.")]
        [Range(0.5f, 10f)]
        public float outerZoneDriftSpeed = 2.2f;

        [Tooltip("Gently keeps floating scrap at its initial spawn radius so orbits don't shift chaotically.")]
        [Range(0.1f, 5f)]
        public float outerZoneOrbitSpring = 1.5f;

        [Tooltip("Tangential speed right at the threshold.")]
        [Range(1f, 40f)]
        public float innerMinSwirlSpeed = 16.0f;

        [Tooltip("Max tangential speed near the Event Horizon.")]
        [Range(10f, 100f)]
        public float innerMaxSwirlSpeed = 48.0f;

        [Tooltip("Controls the exponential acceleration curve inside the threshold.")]
        [Range(0.5f, 5f)]
        public float swirlExponentialPower = 1.8f;

        [Tooltip("Slow inward pull inside the threshold so objects MUST complete 2-4 full rotations before reaching the center!")]
        [Range(0.5f, 10f)]
        public float innerRadialSinkSpeed = 2.0f;

        [Tooltip("Meters per second that the target orbital radius actively shrinks inside the threshold.")]
        [Range(0.5f, 20f)]
        public float spiralRadiusShrinkRate = 5.5f;

        [Tooltip("How strongly the object is pulled onto its shrinking spiral radius.")]
        [Range(1f, 30f)]
        public float spiralTightnessSpring = 8.0f;

        [Tooltip("How fast objects above/below the Black Hole get pulled toward the Y = 0 plane.")]
        [Range(0.5f, 20f)]
        public float verticalFlattenStrength = 6.0f;

        [Tooltip("Outer reach radius of the gravitational well trigger.")]
        [Range(20f, 80f)]
        public float gravityWellRadius = 45f;

        [Tooltip("Gravity multiplier applied to the player drone.")]
        [Range(0.1f, 2.0f)]
        public float playerGravityMultiplier = 0.65f;

        // Legacy / Compatibility fields
        [HideInInspector] public float gravityConstantG = 1200f;
        [HideInInspector] public float minGravityDistance = 4.5f;
        [HideInInspector] public float orbitalGuidanceStrength = 2.5f;
        [HideInInspector] public float inwardSpiralDecay = 0.85f;
        [HideInInspector] public float maxSafeVelocity = 45f;

        // =========================================================================
        // 2. Drone Inertia & Movement (Outer Wilds Feel)
        // =========================================================================
        [Header("2. Drone Inertia & Movement (Outer Wilds Feel)")]
        [Tooltip("Mouse look sensitivity for FPV pitch and yaw control.")]
        [Range(0.2f, 5.0f)]
        public float mouseSensitivity = 1.8f;

        [Tooltip("Base horizontal thruster acceleration force.")]
        [Range(2f, 30f)]
        public float baseThrusterAcceleration = 7.5f;

        [Tooltip("Vertical thruster acceleration force (Space / Ctrl / Q / E).")]
        [Range(2f, 25f)]
        public float verticalThrusterAcceleration = 6.0f;

        [Tooltip("Space linear damping (near zero for authentic zero-G inertia).")]
        [Range(0f, 1f)]
        public float spaceLinearDamping = 0.08f;

        [Tooltip("Active retro-brake damping when holding speed kill key (X or C).")]
        [Range(1f, 10f)]
        public float retroBrakeDamping = 3.5f;

        [Tooltip("Max linear speed achievable solely from onboard drone thrusters (gravity slingshots exceed this!).")]
        [Range(5f, 30f)]
        public float maxThrusterSpeed = 9.0f;

        [Tooltip("Instant burst impulse applied when pressing boost (Shift) - Legacy.")]
        [Range(5f, 50f)]
        public float boostImpulse = 16f;

        [Tooltip("Cooldown in seconds between boost burst impulses - Legacy.")]
        [Range(0.5f, 5f)]
        public float boostCooldown = 2.0f;

        [Tooltip("Instant velocity impulse applied when performing a directional dash (Shift).")]
        [Range(10f, 60f)]
        public float dashImpulse = 22f;

        [Tooltip("Duration in seconds during which normal thruster speed clamping is bypassed after a dash.")]
        [Range(0.05f, 1f)]
        public float dashDuration = 0.22f;

        [Tooltip("Cooldown in seconds between directional dashes.")]
        [Range(0.2f, 3f)]
        public float dashCooldown = 0.9f;

        // =========================================================================
        // 3. Robo-Arm & Gravity Gun (HL2 Style)
        // =========================================================================
        [Header("3. Robo-Arm & Gravity Gun (HL2 Style)")]
        [Tooltip("Unlock state: false = short-range mechanical arm, true = long-range gravity gun.")]
        public bool isMagnetUnlocked = false;

        [Tooltip("Base grab range for the mechanical robo-arm.")]
        [Range(2f, 10f)]
        public float grabRangeBase = 4.5f;

        [Tooltip("Max magnetic beam pull range for the gravity gun.")]
        [Range(10f, 40f)]
        public float magnetPullRange = 18f;

        [Tooltip("Cone angle in degrees in front of crosshair for magnet beam target acquisition.")]
        [Range(10f, 60f)]
        public float magnetConeAngle = 25f;

        [Tooltip("Continuous pull force reeling scrap toward the drone.")]
        [Range(20f, 120f)]
        public float magnetPullForce = 50f;

        [Tooltip("Distance in front of the drone where captured cargo is held suspended.")]
        [Range(1.5f, 8f)]
        public float holdPointDistance = 2.8f;

        [Tooltip("Spring tension maintaining cargo stability at the hold point.")]
        [Range(20f, 200f)]
        public float holdSpringStrength = 80f;

        [Tooltip("Launch impulse when punting/firing held scrap toward targets or the black hole.")]
        [Range(10f, 100f)]
        public float puntLaunchImpulse = 20.0f;

        [Tooltip("Max reach distance for forward dry punt without holding objects.")]
        [Range(3f, 20f)]
        public float dryPuntRange = 8.5f;

        [Tooltip("Cone/sphere radius for forward dry punt.")]
        [Range(0.5f, 5f)]
        public float dryPuntRadius = 2.2f;

        [Tooltip("Max scrap items the drone can simultaneously hold.")]
        [Range(1, 10)]
        public int maxHeldObjects = 3;

        [Tooltip("Transparency while held in front of the camera.")]
        [Range(0.15f, 1.0f)]
        public float heldObjectAlpha = 0.42f;

        [Tooltip("Spacing radius so multiple held objects don't clip into each other.")]
        [Range(0.2f, 3.0f)]
        public float heldClusterRadius = 0.85f;

        // =========================================================================
        // 4. Arena Boundary & Scrap Spawning
        // =========================================================================
        [Header("4. Arena Boundary & Scrap Spawning")]
        [Tooltip("Max concurrent active scrap pieces in the arena.")]
        [Range(10, 150)]
        public int maxActiveScrap = 50;

        [Tooltip("Interval in seconds between scrap spawn cycles.")]
        [Range(0.2f, 5f)]
        public float scrapSpawnInterval = 1.0f;

        [Tooltip("Inner radius of the scrap spawning spherical shell.")]
        [Range(10f, 30f)]
        public float minSpawnRadius = 16f;

        [Tooltip("Outer radius of the scrap spawning spherical shell.")]
        [Range(25f, 60f)]
        public float maxSpawnRadius = 38f;

        [Tooltip("Radius where signal degradation warning triggers.")]
        [Range(30f, 60f)]
        public float arenaSafeRadius = 42f;

        [Tooltip("Absolute outer radius of the orbital arena dome.")]
        [Range(35f, 70f)]
        public float arenaMaxRadius = 48f;

        [Tooltip("Grace period in seconds outside safe radius before emergency telemetry disconnect.")]
        [Range(1f, 10f)]
        public float outOfBoundsWarningTime = 3.0f;

        [Tooltip("Duration over which escaped scrap smoothly dissolves/fades out.")]
        [Range(0.5f, 5f)]
        public float scrapFadeOutDuration = 1.8f;

        // =========================================================================
        // 5. Surveillance Cameras & Multipliers
        // =========================================================================
        [Header("5. Surveillance Cameras & Multipliers")]
        [Tooltip("Score multiplier awarded to scrap passing through camera observation beam.")]
        [Range(1.5f, 10f)]
        public float cameraObservedMultiplier = 3.0f;

        [Tooltip("Rotational tracking speed for surveillance camera pan & tilt.")]
        [Range(0.5f, 10f)]
        public float cameraRotationSpeed = 1.5f;

        [Tooltip("Rotational tracking speed for surveillance camera when locked onto a target.")]
        [Range(5f, 50f)]
        public float cameraLockRotationSpeed = 25f;

        [Tooltip("Rotational tracking speed for surveillance camera when searching or switching targets.")]
        [Range(1f, 15f)]
        public float cameraSearchRotationSpeed = 4f;

        [Tooltip("Spotlight beam radius at the target object.")]
        [Range(0.5f, 10f)]
        public float cameraSpotRadiusAtTarget = 2.5f;

        [Tooltip("Orbital traversal speed for mobile overseer cameras.")]
        [Range(0.5f, 20f)]
        public float overseerOrbitSpeed = 2.0f;

        [Tooltip("Smooth tracking rotation speed for Corporate Overseer Eye looking at player.")]
        [Range(0.2f, 5.0f)]
        public float overseerTrackingSpeed = 1.5f;

        [Tooltip("Baseline target score for the first quota.")]
        [Range(100f, 2000f)]
        public float baseQuotaTarget = 500f;

        [Tooltip("Quota threshold scaling multiplier per quota level.")]
        [Range(1.2f, 3.0f)]
        public float quotaGrowthMultiplier = 1.75f;

        [Tooltip("Idle seconds before heat multiplier combo starts decaying.")]
        [Range(1f, 10f)]
        public float heatDecayDelay = 4.5f;

        // =========================================================================
        // 6. Skill Tree & Progression
        // =========================================================================
        [Header("6. Skill Tree & Progression")]
        [Tooltip("Robot purchase price is ALWAYS fixed and never scales.")]
        public int fixedHelperRobotPrice = 50;

        [Tooltip("Heat multiplier build rate (upgrades by +10% per level up to +50% max).")]
        [Range(1.0f, 2.0f)]
        public float heatFillRateMultiplier = 1.0f;

        [Tooltip("Highest scrap tier currently unlocked (1 to 4, where 4 is Cosmic Anomaly).")]
        [Range(1, 4)]
        public int unlockedScrapTier = 1;

        [Tooltip("Whether the periodic gravitational flash pulse from cameras is unlocked.")]
        public bool cameraGraviFlashUnlocked = false;

        [Tooltip("Interval in seconds between camera gravitational flashes.")]
        public float cameraGraviFlashInterval = 10.0f;

        [Tooltip("Whether helper bots are equipped with the Kamikaze sacrificial protocol.")]
        public bool kamikazeProtocolUnlocked = false;

        [Tooltip("Active count of purchased helper robots in the arena.")]
        public int activeHelperRobotsCount = 0;

        private void OnEnable()
        {
            _instance = this;
        }

        private void OnValidate()
        {
            _instance = this;
        }
    }
}
