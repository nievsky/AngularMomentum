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
        // 1. Black Hole & Outer Wilds Physics
        // =========================================================================
        [Header("1. Black Hole & Outer Wilds Physics")]
        [Tooltip("Orbit direction: true for clockwise, false for counter-clockwise.")]
        public bool clockwiseOrbit = true;

        [Tooltip("Gravitational attraction strength (inverse-square force magnitude).")]
        [Range(100f, 5000f)]
        public float gravityConstantG = 1200f;

        [Tooltip("Inner radius threshold preventing infinite gravitational acceleration.")]
        [Range(1f, 15f)]
        public float minGravityDistance = 4.5f;

        [Tooltip("Force multiplier aligning object velocity into stable circular/spiral orbits instead of direct radial plunge.")]
        [Range(0.5f, 10f)]
        public float orbitalGuidanceStrength = 2.5f;

        [Tooltip("Inward drag factor gradually tightening orbits toward the singularity.")]
        [Range(0.1f, 1.5f)]
        public float inwardSpiralDecay = 0.85f;

        [Tooltip("Hard velocity cap inside the gravity well preventing physics tunneling or orbital escape spikes.")]
        [Range(20f, 120f)]
        public float maxSafeVelocity = 45f;

        [Tooltip("Lethal radius where spaghettification and consumption occur.")]
        [Range(1f, 10f)]
        public float eventHorizonRadius = 3.5f;

        [Tooltip("Outer reach radius of the gravitational well.")]
        [Range(20f, 80f)]
        public float gravityWellRadius = 45f;

        // =========================================================================
        // 2. Drone Inertia & Movement (Outer Wilds Feel)
        // =========================================================================
        [Header("2. Drone Inertia & Movement (Outer Wilds Feel)")]
        [Tooltip("Base horizontal thruster acceleration force.")]
        [Range(5f, 50f)]
        public float baseThrusterAcceleration = 14f;

        [Tooltip("Vertical thruster acceleration force (Space / Ctrl / Q / E).")]
        [Range(5f, 40f)]
        public float verticalThrusterAcceleration = 10f;

        [Tooltip("Space linear damping (near zero for authentic zero-G inertia).")]
        [Range(0f, 1f)]
        public float spaceLinearDamping = 0.08f;

        [Tooltip("Active retro-brake damping when holding speed kill key (X or C).")]
        [Range(1f, 10f)]
        public float retroBrakeDamping = 3.5f;

        [Tooltip("Max linear speed achievable solely from onboard drone thrusters (gravity slingshots exceed this!).")]
        [Range(10f, 50f)]
        public float maxThrusterSpeed = 18f;

        [Tooltip("Instant burst impulse applied when pressing boost (Shift).")]
        [Range(5f, 50f)]
        public float boostImpulse = 16f;

        [Tooltip("Cooldown in seconds between boost burst impulses.")]
        [Range(0.5f, 5f)]
        public float boostCooldown = 2.0f;

        [Tooltip("Gravity multiplier applied to the player drone (allowing escape from the black hole).")]
        [Range(0.1f, 1.5f)]
        public float playerGravityMultiplier = 0.65f;

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
        public float holdPointDistance = 3.2f;

        [Tooltip("Spring tension maintaining cargo stability at the hold point.")]
        [Range(20f, 200f)]
        public float holdSpringStrength = 80f;

        [Tooltip("Launch impulse when punting/firing held scrap toward targets or the black hole.")]
        [Range(15f, 100f)]
        public float puntLaunchImpulse = 38f;

        [Tooltip("Max scrap items the drone can simultaneously hold.")]
        [Range(1, 10)]
        public int maxHeldObjects = 1;

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
        [Range(1f, 20f)]
        public float cameraRotationSpeed = 4.0f;

        [Tooltip("Orbital traversal speed for mobile overseer cameras.")]
        [Range(1f, 30f)]
        public float overseerOrbitSpeed = 8.0f;

        [Tooltip("Baseline target score for the first quota.")]
        [Range(100f, 2000f)]
        public float baseQuotaTarget = 500f;

        [Tooltip("Quota threshold scaling multiplier per quota level.")]
        [Range(1.2f, 3.0f)]
        public float quotaGrowthMultiplier = 1.75f;

        [Tooltip("Idle seconds before heat multiplier combo starts decaying.")]
        [Range(1f, 10f)]
        public float heatDecayDelay = 4.5f;

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
