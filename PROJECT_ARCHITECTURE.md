# Orbital Panopticon — Project Architecture & Technical Context

> **Version**: 1.0.0 (Post-Refactoring & FPV Migration)  
> **Target Engine**: Unity 6 (6000.4.4f1) / Universal Render Pipeline (URP)  
> **Primary Scene**: `Assets/OutdoorsScene.unity`  
> **Balance Asset**: `Assets/_Project/ScriptableObjects/GameBalanceConfig.asset` (mirror: `Assets/ScriptableObjects/GameBalanceConfig.asset`)  
> **Design Theme**: 3D Space Salvage / Incremental Physics / Surveillance Panopticon

---

## 1. Project Overview & Core Gameplay Loop

### 1.1 High Concept
**Orbital Panopticon** is a First-Person (FPV) 6DoF space salvage and incremental physics game set in an orbital surveillance arena dominated by a central supermassive **Black Hole**. 

The player assumes the role of an indentured salvage drone operator contracted by an ominous corporate entity. Overhead, massive mechanical **Surveillance Cameras** and the cyclopean **Corporate Overseer Eye** patrol the arena sphere, illuminating valuable orbital debris with volumetric searchlight beams.

### 1.2 Core Gameplay Loop
```
                                 [ 1. Orbital Search ]
               Search calm Outer Zone for debris illuminated by Surveillance Beams
                                          │
                                          ▼
                                [ 2. Approach & Grab ]
               Fly in 6DoF cockpit view; latch debris using Robo-Arm / Magnet (RMB)
               Observed scrap triggers: "OBSERVED TARGET GRABBED - x3 VALUE!"
                                          │
                                          ▼
                                [ 3. Aim & Launch ]
               Line up trajectory with the Black Hole; punt scrap across threshold (LMB)
                                          │
                                          ▼
                               [ 4. Accretion Swirl ]
               Scrap accelerates into hot inner accretion disc, multiplying orbital bonus
                                          │
                                          ▼
                             [ 5. Event Horizon Payoff ]
               Scrap consumed at event horizon:
               Score = BaseValue × OrbitalBonus × ObservationBonus(3x) × HeatMultiplier
                                          │
                                          ▼
                              [ 6. Quotas & Upgrades ]
               Complete corporate quotas to earn Scrap Coins; upgrade drone thrusters & magnet
```

---

## 2. Strict Architectural Rules (Must Follow in All Future Sessions)

Future AI agents and developers **must adhere strictly** to the following three core architectural principles to prevent regressions and maintain codebase scalability:

### Rule 1: The `VisualRoot` Pattern (Strict Separation of Physics and Visuals)
* **Rule**: All GameObjects that interact with physics or gameplay logic (`PlayerDrone`, `ScrapItem`, `DynamicSurveillanceCamera`, `ArenaBoundary`, `CorporateOverseerEye`, `BlackHoleSystem`) must place components (`Rigidbody`, `Colliders`, gameplay scripts) **strictly on the root GameObject**.
* **Visual Children**: All meshes, `MeshFilter`, `MeshRenderer`, and aesthetic primitive shapes must reside inside a dedicated child transform named `VisualRoot`.
* **Why**: This enables immediate, non-destructive replacement of greybox prototyping shapes (cubes, cylinders, spheres) with final 3D production models (`.fbx` / `.obj`) without breaking script references, colliders, hierarchy bindings, or serialized fields.

### Rule 2: Centralized Balance Configuration (`GameBalanceConfig`)
* **Rule**: **Zero hardcoded magic numbers** in gameplay or physics scripts.
* **Mechanism**: Every speed, force, radius, multiplier, cooldown, and timing constant MUST be declared as a serialized field in `GameBalanceConfig.cs` and tuned inside `Assets/_Project/ScriptableObjects/GameBalanceConfig.asset`.
* **Runtime Access**: Scripts read values dynamically via `GameManager.GameBalanceConfigInstance`. This enables live-tuning in the Inspector and Play Mode without code recompilation.

### Rule 3: Data-Oriented Simulation Management (No Individual Scrap Loops)
* **Rule**: Individual `ScrapItem` instances **MUST NOT** possess `Update()` or `FixedUpdate()` methods.
* **Mechanism**: `ScrapSimulationManager` owns all active debris (`List<ScrapItem> ActiveScrap`), driving batched physics updates, lifetime tracking, distance checks, and boundary fade-outs in a single centralized loop.
* **Zero Runtime Allocations**: Debris items are prewarmed into a queue-based pool (`Queue<ScrapItem> _scrapPool`, default 100 items). No `Instantiate()` or `Destroy()` calls during active gameplay.

### Rule 4: Cockpit-First FPV Experience
* **Rule**: The camera rig is strictly First-Person 6DoF Cockpit view.
* **Hierarchy**: `Main Camera` is a direct child of `PlayerDrone` at local position `(0, 0.15, 0.4)`. The `RoboArmVisual` and `HoldPoint` are children of `Main Camera`.
* **Orientation Constraints**: Pitch is strictly clamped between `[-80°, +80°]`. Roll is strictly locked to `0°` (preventing disorientation and motion sickness). Physics torque is locked (`rb.freezeRotation = true`, `rb.angularVelocity = Vector3.zero`).

---

## 3. Directory Structure & Script Map

The project scripts are located in `Assets/Scripts/` (with configurations in `Assets/_Project/ScriptableObjects/`).

```
Assets/
├── _Project/
│   └── ScriptableObjects/
│       └── GameBalanceConfig.asset        <-- Central balance configuration
├── Scripts/
│   ├── Core/                              <-- Game management, configuration, UI, economy
│   ├── Player/                            <-- FPV 6DoF flight and gravity manipulator
│   ├── Physics/                           <-- Black hole orbital mechanics and gravity bodies
│   ├── Gameplay/                          <-- Surveillance cameras, arena boundaries, scrap simulation
│   └── UI/                                <-- Legacy menus, localization, and UI tweens
```

### 3.1 `Core/`
| Script | Namespace | Key Responsibilities | Key Public API |
| :--- | :--- | :--- | :--- |
| **`GameBalanceConfig.cs`** | `OrbitalPanopticon.Core` | ScriptableObject defining all global simulation, pacing, black hole, drone, manipulator, scrap, camera, and economy parameters. | `globalSimulationSpeed`, `accretionThresholdRadius`, `cameraObservedMultiplier`, `mouseSensitivity` |
| **`GameManager.cs`** | `OrbitalPanopticon.Core` | Central singleton manager coordinating game state and providing global static access to the active `GameBalanceConfig`. | `GameManager.Instance`, `GameManager.GameBalanceConfigInstance`, `SetBalanceConfig()` |
| **`QuotaAndScoreManager.cs`** | `OrbitalPanopticon.Core` | Listens to `BlackHoleGravity.OnObjectConsumed`, computes multi-tier scores, manages heat multiplier combos (`1x`→`16x`), and tracks Scrap Coins. | `CurrentQuotaProgress`, `TargetQuota`, `CurrentHeatMultiplier`, `ScrapCoins`, `OnScoreAdded`, `OnQuotaCompleted` |
| **`UIManager.cs`** | `OrbitalPanopticon.Core` | Drives cockpit HUD: REC indicator, Quota progress bar, Heat multiplier, Scrap Coins counter, Crosshair states, Boundary Warning, and Observed Scrap notice. | `Instance`, `SetCrosshairState()`, `SetObservedNotice()`, `HandleSignalLost()` |

### 3.2 `Player/`
| Script | Namespace | Key Responsibilities | Key Public API |
| :--- | :--- | :--- | :--- |
| **`DroneController.cs`** | `OrbitalPanopticon.Player` | First-Person 6DoF cockpit flight controller (New Input System). Mouse look (Pitch/Yaw, locked Roll), look-directed 3D thrusters (`W`/`S`), strafing (`A`/`D`), vertical lift strictly (`Space`/`LeftControl`/`C`), retro-braking (`X`), and instant Directional Dash (`Left Shift`, impulse 22f, duration 0.22s, cooldown 0.9s) projecting along composite movement input vectors with dynamic speed-cap bypass. | `Teleport()`, `SetControlEnabled()`, `IsControlEnabled`, `IsDashing`, `IsBoosting`, `IsBraking`, `CurrentCargoMass`, `MainCamera` |
| **`GravityManipulatorController.cs`** | `OrbitalPanopticon.Player` | Authentic Half-Life 2 Gravity Gun mechanics. Strict RMB pull/hold, LMB punt launch and dry punt shockwave cast (`Physics.OverlapSphere` at 3.5m ahead, radius 3.8m, `QueryTriggerInteraction.Ignore`), `ballisticLaunchTimer = 0.85s`, `requireRmbRelease` latch lock, `pullImmunityTimer`, robo-arm recoil/vibration animations, `PuntShockwaveRing` expansion. | `HeldItems`, `HeldCount`, `IsHolding`, `HoldPoint`, `ReleaseAllHeld()`, `PlayPuntAnimation()` |
| **`GravityTetherController.cs`** | `OrbitalPanopticon.Player` | *(Deprecated)* Pre-refactoring spring-tail tether system; preserved for backward compatibility. | — |
| **`OrbitalCameraController.cs`** | `OrbitalPanopticon.Player` | *(Deprecated)* Pre-refactoring third-person chase camera; superseded by FPV cockpit hierarchy. | — |

### 3.3 `Physics/`
| Script | Namespace | Key Responsibilities | Key Public API |
| :--- | :--- | :--- | :--- |
| **`IGravityAffectable.cs`** | `OrbitalPanopticon.Physics` | Core interface for entities subject to black hole gravitational pull. | `ApplyGravityForce()`, `SetCurrentGravity()`, `OnConsumedByBlackHole()` |
| **`GravityBody.cs`** | `OrbitalPanopticon.Physics` | Monobehaviour bridging Rigidbody physics to `BlackHoleGravity`. Auto-registers with the gravity well and scales mass influence. | `Rigidbody`, `GravityMultiplier`, `ApplyGravityForce()` |
| **`BlackHoleGravity.cs`** | `OrbitalPanopticon.Physics` | Implements True 3D Two-Zone outer orbit vs. inner accretion physics. Clockwise tangential drift, singularity-safe horizontal tangents, 3D center pull (`dirToCenter3D`), equatorial flattening (`verticalFlattenStrength = 6.0f`), spiral drag, direct spherical radius clamping, ballistic launch timer bypass, event horizon consumption trigger, `OnObjectConsumed` event. | `OnObjectConsumed`, `TrackedBodiesCount`, `ConsumeObject()`, `AccretionRadius` |

### 3.4 `Gameplay/`
| Script | Namespace | Key Responsibilities | Key Public API |
| :--- | :--- | :--- | :--- |
| **`ScrapData.cs`** | `OrbitalPanopticon.Gameplay` | ScriptableObject defining scrap tier, base value, physics mass, and visual scale. | `ScrapName`, `Tier`, `BaseValue`, `Mass`, `BaseScale` |
| **`ScrapItem.cs`** | `OrbitalPanopticon.Gameplay` | Lightweight data container for scrap instances. Stores `ScrapState`, mass, observation flag, orbital bonus multiplier, and material property blocks. Has no `Update()` loop! | `CurrentState`, `IsObserved`, `OrbitalBonusMultiplier`, `SetState()`, `SetObserved()`, `ResetToFloating()` |
| **`ScrapSimulationManager.cs`** | `OrbitalPanopticon.Gameplay` | Data-oriented pool and simulation manager. Drives pooled recycling, batched movement/fade updates, and circular orbit spawning. | `Instance`, `ActiveScrap`, `SpawnScrap()`, `ReturnToPool()`, `CalculateTotalWeight()` |
| **`DynamicSurveillanceCamera.cs`** | `OrbitalPanopticon.Gameplay` | Autonomous surveillance cameras located on the sphere boundary. Targets calm Outer Zone scrap, aims volumetric procedural spotlight cone mesh (double-sided tapered cone: tip 0.08m, base 1.0m, unlit translucent yellow `M_CameraBeamYellow.mat`, no collider), zero-lag `LateUpdate` tracking (lock speed 25f, search 4f), awards x3 observation bonus. | `CurrentTarget`, `ReleaseTarget()` |
| **`CorporateOverseerEye.cs`** | `OrbitalPanopticon.Gameplay` | Massive celestial overseer orbiting the outer boundary. Undulating inclination orbit, smooth slow `Quaternion.Slerp` tracking of player drone, pulsing pupil light. | — |
| **`ArenaBoundaryManager.cs`** | `OrbitalPanopticon.Gameplay` | Monitors drone distance from arena center `(0,0,0)`. Triggers warning countdown (`safeRadius=42f`), non-destructive immediate respawn with dummy wreckage spawn for black hole spaghettification (`maxRadius=48f`), and 1.2s input lock. | `Instance`, `OnSignalLost`, `OnBoundaryWarningChanged`, `OnPlayerConsumedByBlackHole()` |

---

## 4. Scene Hierarchy & Key GameObjects (`OutdoorsScene.unity`)

```
OutdoorsScene
├── StaticLightingSky
├── Directional Light                       <-- Main scene light
│
├── PlayerDrone                             <-- Root: Rigidbody, SphereCollider, GravityBody, DroneController, GravityManipulatorController
│   ├── VisualRoot                          <-- Hull, Canopy, Thrusters (MeshRenderers disabled for cockpit view)
│   └── MainCamera                          <-- Position: (0, 0.15, 0.4), Rotation: (0, 0, 0)
│       ├── RoboArmVisual                   <-- Position: (0.45, -0.35, 0.8), Source-engine style weapon viewmodel
│       │   ├── Arm_Base                    <-- Mechanical base cylinder
│       │   ├── Piston_Left / Piston_Right  <-- Pneumatic actuators
│       │   ├── Claw_Prong_L / Claw_Prong_R <-- Pinching magnetic claws (emits gold on observed grab)
│       │   └── ObservedIndicatorLight      <-- Point light (Range: 3.5, Color: Gold, activates on observed grab)
│       └── HoldPoint                       <-- Position: (0, -0.1, 2.8), Cargo suspension point aligned with crosshair
│
├── ScrapSimulationManager                  <-- Central simulation manager & object pool container
│   └── --- SCRAP_POOL_CONTAINER ---        <-- Prewarmed inactive ScrapItem instances (100 items)
│
├── --- ENVIRONMENT ---                     <-- Static and ambient environment root
│   ├── DroneSpawnPoint                     <-- Teleport coordinates: (0, 10, -35)
│   ├── ArenaBoundary                       <-- ArenaBoundaryManager, safeRadius: 42, maxRadius: 48
│   │   └── VisualRoot/BoundarySphere       <-- Inverted boundary sphere (Scale: 90, 90, 90)
│   ├── SurveillanceCamera_01               <-- Position: (-32, 18, 28), DynamicSurveillanceCamera
│   │   ├── VisualRoot                      <-- CameraHousing, Lens
│   │   ├── BeamMesh                        <-- Volumetric golden searchlight tapered cone (Material: M_CameraBeamYellow.mat, unlit translucent)
│   │   └── SpotLight                       <-- Searchlight (Range: 50, Color: Gold, Intensity: 8)
│   ├── SurveillanceCamera_02               <-- Position: (32, -16, 28), DynamicSurveillanceCamera (mirrored)
│   ├── BlackHoleSystem                     <-- Position: (0, 0, 0), BlackHoleGravity
│   │   ├── VisualRoot/BlackHoleSphere      <-- Event horizon black sphere (Scale: 6, 6, 6)
│   │   ├── EventHorizonTrigger             <-- SphereCollider (isTrigger=true, Radius=3.5)
│   │   └── GravityWellTrigger              <-- SphereCollider (isTrigger=true, Radius=45)
│   └── CorporateOverseerEye                <-- Position: (0, 20, 50), CorporateOverseerEye
│       ├── VisualRoot                      <-- Eye_Hull, Gimbal_Ring_Horizontal, Gimbal_Ring_Vertical, Eye_Pupil
│       └── Pupil_Light                     <-- Menacing red/orange glowing eye pupil light
│
├── --- TEST_STAND ---                      <-- 15 benchmark floating test scrap cubes in stable orbit
│   ├── TestScrapCube_00 ... 14
│
├── GameManager                             <-- GameManager, QuotaAndScoreManager, UpgradeTreeManager, HelperDroneManager, ScorePopupManager
├── EventSystem                             <-- New Input System UI Input Module
│
└── HUD_Canvas                              <-- Screen Space Canvas, UIManager, UpgradeTreeUI
    ├── Text_REC_Status                     <-- Blinking "● REC | DRONE #001" HUD feed indicator
    ├── Panel_QuotaAndHeat                  <-- Quota progress bar & Heat Multiplier (x1, x2, x4, x8, x16)
    ├── Text_ScrapCoins                     <-- Persistent currency counter
    ├── SignalLostOverlay                   <-- Fullscreen static/reconnect splash on boundary breach
    ├── Crosshair                           <-- Dynamic HUD crosshair (turns green on target, blue on hold)
    ├── WarningPanel                        <-- Pulsing red border & countdown on boundary approach
    ├── Panel_ObservedNotice                <-- Radiant gold banner: "OBSERVED TARGET GRABBED - x3 VALUE!"
    └── Panel_UpgradeTree                   <-- Fullscreen modular radial skill tree overlay (TAB toggle, world pause)
```

---

## 5. Physics & State Machine Logic

### 5.1 Scrap Lifecycle State Machine (`ScrapState`)
```
    [ POOL ] ──Spawn──► [ Floating ] ──LMB/Magnet──► [ Held ]
                            │                           │
                   Out of Bounds / Fade                 │ LMB Punt / Helper / Flash
                            ▼                           ▼
                     [ FadingOut ]                 [ Launched ]
                            │                           │
                            │                     Accretion Swirl
                            │                           │
                            ▼                           ▼
                        [ POOL ] ◄──Recycle── [ Consumed ]
```

* **`Floating`**:
  * Default state for unharvested debris in orbit.
  * Controlled by central `ScrapSimulationManager`.
  * In the Outer Zone (`dist > 15f`), zero inward gravitational pull is applied; scrap maintains stable, predictable clockwise orbit.
* **`Held`**:
  * Debris captured by the player's gravity manipulator claw.
  * External forces are zeroed (`isKinematic` or damped spring force toward `HoldPoint`).
  * Aligned directly in front of the crosshair at local position `(0, -0.1, 2.8)`.
* **`Launched`**:
  * Debris released or punted with high impulse via LMB, pushed by Camera Gravi-Flash, or flung by Helper Drone Bots.
  * Subject to active black hole gravity and accretion zone acceleration.
  * Accumulates `orbitalBonusMultiplier` while spiraling through the inner disc.
* **`Consumed`**:
  * Crosses the event horizon (`dist <= 3.5f`).
  * Triggers `OnObjectConsumed` event on `BlackHoleGravity`.
  * Computes final score with risk bonuses, observation multipliers (`x3`), and kamikaze bonuses (`x2.5`), triggers floating world-space popup, and resets item into the simulation pool.
* **`FadingOut`**:
  * Triggered if scrap drifts past `arenaMaxRadius` (48f).
  * Central manager shrinks scale and alpha over `scrapFadeOutDuration` (1.8s) before returning to the pool.

### 5.2 Two-Zone Black Hole Physics Model
To eliminate chaotic slingshots and prevent un-thrown scrap from prematurely falling into the black hole, physics operates under a strict two-zone model with explicit tightening spiral dynamics:

1. **Outer Calm Zone (`dist > accretionThresholdRadius` = 15.0m)**:
   * **Inward Gravity Force**: `0 N` (Zero inward radial attraction).
   * **Tangential Orbit**: Predictable clockwise orbital drift (`outerZoneDriftSpeed = 2.2f`).
   * **Purpose**: Allows the player to navigate calmly, locate surveillance spotlight beams, align trajectories, and collect high-value scrap without panic.
2. **Inner Accretion Zone (`dist <= accretionThresholdRadius` = 15.0m)**:
   * **Dynamic Per-Object 3D Orbital Axis (`customOrbitAxis`)**: Eliminates single-axis flattening by computing a personalized 3D tilted orbital plane at the exact moment an object enters Zone B (`entryTangent = (lateralVel * 0.6 + defaultCw * 6.0 + noise * 1.5).normalized`, `customOrbitAxis = Cross(dirToCenter3D, entryTangent)`).
   * **Planar Drift Correction**: Actively keeps each object locked onto its unique 3D tilted orbital plane passing through `(0,0,0)` (`planeDriftCorrection = -Project(pos - center, customOrbitAxis) * 6.0f`), generating rich natural accretion disks from all approach angles.
   * **Direct 3D Spherical Constraint**: When 3D distance exceeds `currentSpiralRadius`, `rb.position` is smoothly pulled onto the shrinking 3D sphere around the singularity (`pullStep = Min(excess, excess * spiralTightnessSpring * dt)`).
   * **Ballistic Punt Flight Window**: Debris punted by the player maintains full ballistic trajectory for `ballisticLaunchTimer = 0.85s` without orbital velocity overwrite until entering the accretion threshold.
   * **Tightening Death Spiral (`currentSpiralRadius`)**: Neutralizes discrete-step centrifugal drift by tracking an explicit shrinking target radius (`spiralRadiusShrinkRate = 5.5m/s`, accelerated by proximity depth `1 + depth01 * 1.5`).
   * **Centrifugal Drift Correction**: Compensates radial error with spring restoration (`spiralTightnessSpring = 8.0f`), pulled inward at `(shrinkRate * (1 + depth01)) + Max(0, radiusError * spring)`.
   * **Accelerated Tangential Swirl**: Exponential clockwise spin from entry (`innerMinSwirlSpeed = 16.0f`) to event horizon threshold (`innerMaxSwirlSpeed = 48.0f`, power `1.8`).
   * **Kinematic Grip**: Target velocity applied with high responsiveness (`12f * Time.fixedDeltaTime`), ensuring 2 to 3 dramatic tightening loops before consumption.
   * **Heat Growth**: Accumulates orbital bonus multiplier at `0.5f/s` (up to `maxOrbitalBonusMultiplier = 4.0x`).
3. **Event Horizon (`dist <= eventHorizonRadius` = 3.5m)**:
   * **Payoff Trigger**: Instantly consumes scrap, calculates combo scores, awards Scrap Coins, spawns 3D score popups, and recycles object.
   * **Player Protection**: Player drone is strictly immune to consumption and destruction (protecting child `Main Camera`), instantly respawning while dummy wreckage is spaghettified.

### 5.3 Autonomous Helper Drone Bot State Machine (`HelperDroneBot`)
```
    [ Respawning ] ──(2.0s delay)──► [ Seek ] ──(Target < 2.8m)──► [ Deliver ]
           ▲                                                            │
           │                                                            ├─► [ Normal Release ] ──► [ Seek ]
           │                                                            │
           └────────────── [ Kamikaze Dive ] ◄──(Threshold Entry)───────┘
```
1. **Seek**: Scans outer zone for unharvested floating debris (`dist > 15m`, unclaimed), turns smoothly, and flies toward it. Speed scales directly with `"Мощность трала"` (`config.magnetPullForce`).
2. **Deliver**: Attaches a dynamic `LineRenderer` tether beam and drags the scrap toward the black hole center.
3. **Release (Normal Mode, `!kamikazeProtocolUnlocked`)**: Halts outside the threshold (17.5m), launches the scrap into the black hole vortex with high velocity (`TransitionToLaunched`), releases tether, and immediately returns to `Seek`.
4. **Kamikaze Protocol (`kamikazeProtocolUnlocked == true`)**: Accelerates into a terminal dive directly into the singularity (`kamikazeSpeed = 34m/s`). When crossing the event horizon, it disintegrates, consumes the scrap with a **x2.5 Kamikaze Multiplier**, spawns a world-space popup, and resets into `Respawning` for 2 seconds.

### 5.4 Camera "Грави-вспышка" (Gravi-Flash)
* **Periodic Pulse**: Every `config.cameraGraviFlashInterval` (10s), active cameras tracking a target in the outer zone activate a gravitational shockwave.
* **Visual Flash**: Spotlight flashes cyan with intensity spike (12f) and the procedural cone beam flashes white/cyan for 0.25 seconds.
* **Dynamic Impulse**: Pushes the current target and all nearby scrap inside a 6m cone directly into the accretion threshold (`TransitionToLaunched(dirToCenter * 26f)`).

---

## 6. Current Project Status & Future Roadmap

### 6.1 Completed Systems `[x]`
- [x] **Autonomous Helper Robots & Kamikaze Protocol**: Procedural `HelperDroneBot.prefab` (spherical body, cyan visor, engine trail, tether `LineRenderer`), autonomous state machine (`Seek`, `Deliver`, `Release`, `Kamikaze`, `Respawning`), +100% fixed-cost purchase in Skill Tree, and x2.5 Kamikaze Event Horizon score bonus.
- [x] **Camera Gravi-Flash Pulse**: 10-second timer on `DynamicSurveillanceCamera`, white/cyan cone flash, and physical impulse pushing targeted Outer Zone scrap straight into the Black Hole accretion vortex.
- [x] **Tier 4 Scrap "Космическая аномалия" & Gated Spawning**: `SO_Scrap_Tier4_Anomaly.asset` (baseValue = 600, mass = 15) and monolithic golden torus prefab `Scrap_Tier4_Anomaly.prefab`. Dynamic pool filtering in `ScrapSimulationManager` & `ScrapSpawner` strictly enforcing `tier <= config.unlockedScrapTier`.
- [x] **World-Space Floating Score Popups**: Camera-facing billboard 3D text showing `"+Score"`, with special gold styling for `"OBSERVED x3!"` and cyan styling for `"KAMIKAZE x2.5!"`.
- [x] **Radial Upgrade Tree UI (TAB with World Pause) & Stat Hooks**: Modular, data-driven skill tree with 20 designer upgrade nodes across 5 branches (Player, Black Hole, Scrap, Cameras, Robots) converging into WOW perks, plus standalone Helper Robot purchase. Runtime configuration cloning (`RuntimeConfig` pattern) preventing editor asset mutation, procedural UI lines, color-coded node states, +10% compound quota reward scaling, and complete player flight/manipulator input suppression during skill tree navigation.
- [x] **Dynamic Per-Object 3D Orbital Plane (Vortex Accretion)**: Each object establishes a unique, locked 3D orbital plane passing through `(0,0,0)` based on approach velocity and organic tilt, avoiding flat single-plane snapping.
- [x] **Multi-Object Semi-Transparent Gravity Gun**: Simultaneous capture and holding of up to 3 objects (`maxHeldObjects = 3`), non-overlapping slowly rotating ring formation (`heldClusterRadius = 0.85f`), mutual held collision suppression with 0.9m separation spring, and dynamic FPV semi-transparency (`heldObjectAlpha = 0.42f`).
- [x] **Directional Dash on Shift & Ballistic Punt**: 6DoF composite movement vector dash (`Left Shift`, impulse 22f, duration 0.22s, cooldown 0.9s) with speed-cap bypass, and `ballisticLaunchTimer = 0.85s` preserving player shot trajectories outside the accretion well.
- [x] **Unbreakable Gravity Gun & Trigger Collision Fix**: Overlap queries ignore trigger colliders (`QueryTriggerInteraction.Ignore`), stale item auto-purging in `Update()`, automatic RMB release latching, and reliable dry punt blast (range 8.5m, radius 3.8m).
- [x] **Volumetric Procedural Tapered Cone & Unlit Material**: Runtime `CreateConeMesh(segments = 24, tipRadius = 0.08f, baseRadius = 1.0f, length = 1.0f)` with double-sided faces, vertex colors `(1, 0.92, 0.15, 0.25)`, and bright unlit translucent warm yellow material (`M_CameraBeamYellow.mat`).
- [x] **FPV 6DoF Cockpit Experience**: Mouse-look (clamped pitch, roll strictly locked at 0°), look-directed 3D flight (`W`/`S`), vertical thrust strictly (`Space`/`LeftCtrl`/`C`), retro-braking (`X`).
- [x] **Dynamic Surveillance Cameras**: Automatic targeting of calm Outer Zone debris, zero-lag `LateUpdate` tracking (lock speed 25f, search 4f), and x3 score multiplier.
- [x] **Non-Destructive Player Respawn**: Event horizon fatal check triggers dummy wreckage spaghettification while immediately teleporting `PlayerDrone` to safe spawn point with 1.2s input lock and signal lost HUD splash.
- [x] **Corporate Overseer Eye**: Heavy, slow-tracking `Quaternion.Slerp` gaze following player drone with undulating outer boundary orbit.
- [x] **Observed Target Feedback**: Cockpit claw indicator light and `"OBSERVED TARGET GRABBED - x3 VALUE!"` HUD banner.
- [x] **Arena Boundary Enforcement**: Warning buffer at 42m, countdown timer, signal loss teleportation back to `DroneSpawnPoint` at 48m.
- [x] **Data-Oriented Scrap Simulation**: Central manager `ScrapSimulationManager` with prewarmed 100-item pool and batched lifecycle updates.
- [x] **Centralized Balance Configuration**: `GameBalanceConfig` scriptable object powering real-time inspector tuning.

### 6.2 Upcoming Development Roadmap `[ ]`
- [ ] **Phase 1: 3D Art Asset Polish**
  - Replace prototype cylinders on `RoboArmVisual` with high-detail cockpit mechanical arm mesh.
  - Replace greybox sphere on `BlackHoleSphere` with animated accretion disc shader (event horizon distortion / gravitational lensing).
  - Replace boundary wireframe sphere with holographic boundary grid shader.
- [ ] **Phase 2: Audio-Visual Juice & Feedback**
  - Thruster hum and boost burst SFX.
  - Black hole low-frequency rumble and event horizon consumption crunch.
  - Surveillance camera mechanical servo tracking whine.
  - Particle effects: thruster plumes, accretion disc sparkles, suction vortex trails.
