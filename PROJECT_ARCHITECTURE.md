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
| **`UIManager.cs`** | `OrbitalPanopticon.Core` | Drives cockpit HUD: Surveillance monitor viewfinder frame, blinking REC indicator, Sector telemetry, active Helper Bot counter, Quota progress bar, Heat multiplier, Scrap Coins counter, dynamic Reticle (Cyan in range, Gold on x3 observed target), Operator Start Overlay, Quota Celebration Banner, Boundary Warning, and Observed Scrap notice. | `Instance`, `SetCrosshairState()`, `SetObservedNotice()`, `HandleSignalLost()`, `ConnectOperator()` |
| **`UpgradeTreeManager.cs`** | `OrbitalPanopticon.Core` | Radial 5-branch upgrade tree UI toggled via TAB with game pause (`Time.timeScale = 0`). Manages 20 upgrade nodes, WOW capstone perks, and runtime balance cloning (`RuntimeConfig`). | `Instance`, `ToggleTree()`, `PurchaseNode()`, `IsNodeUnlocked()` |
| **`AudioConfig.cs`** | `OrbitalPanopticon.Core` | ScriptableObject defining categorized audio clips for drone flight, gravity gun, black hole dynamics, surveillance cameras, UI, and endgame finale. | `AudioConfig.asset` |
| **`GameAudioManager.cs`** | `OrbitalPanopticon.Core` | Central audio singleton managing sound sources, pitch jitter, ambient loops, and convenient one-shot audio triggers. | `Instance`, `PlayOneShot()`, `PlayPunt()`, `PlayGrab()`, `PlayQuotaComplete()`, `PlayConsume()` |
| **`DesignerDebugTools.cs`** | `OrbitalPanopticon.Core` | Live developer shortcuts toggled by `GameBalanceConfig.enableDebugShortcuts`. Enables `F1` (+500 Coins), `F2` (Skip Quota), `F3` (Spawn Robot), and `F4` (Trigger Singularity Finale) with on-screen HUD legend. | `Instance` |

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
| **`DynamicSurveillanceCamera.cs`** | `OrbitalPanopticon.Gameplay` | Autonomous surveillance cameras located on the sphere boundary. Targets calm Outer Zone scrap, aims volumetric procedural spotlight cone mesh (double-sided tapered cone: tip 0.08m, base 1.0m, unlit translucent yellow `M_CameraBeamYellow.mat`, no collider), zero-lag `LateUpdate` tracking (lock speed 25f, search 4f), awards x3 observation bonus (x6 with Panopticon Laser Grid), tracks dual targets simultaneously, executes periodic Gravi-Flash shockwaves. | `CurrentTarget`, `ReleaseTarget()` |
| **`HelperDroneBot.cs`** | `OrbitalPanopticon.Gameplay` | Autonomous helper salvager with state machine (`Seek`, `Deliver`, `Release`, `Kamikaze`, `Respawning`). Drags Outer Zone debris to accretion disk; executes high-speed Kamikaze dives into singularity for x2.5 value when unlocked. | `CurrentState`, `SeekTarget()` |
| **`HelperDroneManager.cs`** | `OrbitalPanopticon.Gameplay` | Singleton spawning, coordinating, and managing fleet of active autonomous helper bots. | `Instance`, `SpawnHelperBot()`, `ActiveBots` |
| **`EndgameFinaleController.cs`** | `OrbitalPanopticon.Gameplay` | Controls Black Hole visual and physical scaling per quota completion (`blackHoleScalePerQuota`), orchestrates 4-phase cinematic Sector Collapse finale upon reaching critical quota count, manages camera shake, HUD sirens, and displays victory summary panel. | `TriggerEndgameFinale()`, `OnQuotaCompleted()` |
| **`CorporateOverseerEye.cs`** | `OrbitalPanopticon.Gameplay` | Massive celestial overseer orbiting the outer boundary. Undulating inclination orbit, smooth slow `Quaternion.Slerp` tracking of player drone, pulsing pupil light. Possesses `isConsumedOrCollapsing` flag for finale sequence. | `isConsumedOrCollapsing` |
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

### 5.5 Black Hole Evolution, WOW-Perks & Sector Collapse Finale
* **Progressive Black Hole Growth**:
  * With every completed quota, `EndgameFinaleController` scales up `BlackHoleSystem/VisualRoot` and expands `eventHorizonRadius` by `config.blackHoleScalePerQuota` (`+0.35m` per quota).
  * The singularity becomes visually and physically more threatening as the shift progresses.
* **Four Red "ВАУ-перки" (WOW Capstones)**:
  1. **Player WOW — Relativistic Overdrive (`player_wow`)**:
     * Instant 6m auto-magnetization into `HoldPoint` with zero drone recoil or deceleration (`PerformRelativisticAutoMagnet`).
     * Doubles punt impulse (`impulse * 2.0f`).
     * Activates an emissive magenta/cyan hyperspace `TrailRenderer` on the player drone during dashes.
  2. **Black Hole WOW — Hawking Singularity Pulse (`hole_wow`)**:
     * Accretion orbital bonus multiplier growth rate is doubled (`1.0x/s` instead of `0.5x/s`).
     * Every 12 seconds, a cylindrical shockwave ring expands outward from the singularity, capturing the 8 nearest Outer Zone floating debris items and launching them inward.
  3. **Cameras WOW — Panopticon Laser Grid (`camera_wow`)**:
     * Beam visuals and searchlights shift to bright neon magenta, elevating observation multiplier to `x6.0` (`config.cameraWowChainLaserMultiplier`).
     * Cameras acquire and track two targets simultaneously (`secondaryTarget`, `_secondaryBeamLine`), observing and gravi-flashing both debris items.
  4. **Scrap WOW — Singularity Resonance (`scrap_wow`)**:
     * Consuming Tier 4 "Космическая аномалия" items instantly advances the Heat Multiplier combo stage by a full tier (`+1.0x` stage jump).
* **Endgame Sector Collapse Finale**:
  * Triggered upon reaching `completedQuotasCount >= config.quotasToTriggerFinale` (default: 8 quotas).
  * Plays red emergency siren HUD warning: *"CRITICAL MASS REACHED! SINGULARITY OVERFLOW — INITIATING SECTOR COLLAPSE!"*.
  * **Phase 1 (0–4s)**: Singularity vacuum pulls all remaining scrap and autonomous helper bots into the event horizon.
  * **Phase 2 (4–8s)**: Detaches all surveillance camera turrets from the outer arena shell via dynamic `GravityBody` physics, consuming them for `+5,000` bonus points each.
  * **Phase 3 (8–12s)**: Overrides Corporate Overseer Eye orbital trajectory (`isConsumedOrCollapsing = true`), pulling the cyclopean eye into the black hole with intense camera shake for `+25,000` bonus points.
  * **Phase 4**: Displays the Shift Complete Victory Summary Panel (Total Time, Total Mass Consumed, Peak Heat Multiplier, Final Scrap Coins, `[CONTINUE ENDLESS MODE]` and `[RESTART SHIFT]`).

---

## 6. Current Project Status & Future Roadmap

### 6.1 Completed Systems `[x]`
- [x] **Surveillance Monitor HUD Overlay & Operator Start Terminal**: Non-blocking `SurveillanceMonitorFrame` overlay with 4 corner viewfinder brackets (`┌ ┐ └ ┘`), animated blinking REC status, `SECTOR 7-G` telemetry, active Helper Bot counter (`"HELPER BOTS: X"`), contextual keybind hints, dynamic Crosshair reticle (Cyan when scrap is in grab range, Gold when aiming at x3 Observed targets), Operator Start Screen (`Panel_OperatorStart`) with smooth click-to-connect fade out, and celebratory Quota Completed banner (`Panel_QuotaCelebration`).
- [x] **Game Designer Live Debug & Cheat Shortcuts (`DesignerDebugTools`)**: Function key shortcuts toggled via `enableDebugShortcuts` in `GameBalanceConfig`: `F1` (+500 Scrap Coins), `F2` (Instantly complete Quota), `F3` (Spawn 1 Helper Bot), `F4` (Trigger Singularity Sector Collapse Finale), and on-screen HUD developer legend.
- [x] **Centralized Game Audio Architecture (`GameAudioManager` & `AudioConfig`)**: `AudioConfig.asset` ScriptableObject defining categorized clips for thrusters, gravity manipulator, black hole accretion, surveillance cameras, UI fanfare, and endgame finale with pitch-jittered one-shot triggers on `GameAudioManager`.
- [x] **Black Hole Evolution & Sector Collapse Finale**: `EndgameFinaleController` scaling singularity on quota completion, 4-phase cinematic arena collapse (scrap/bots vacuum -> camera turret detachment -> Corporate Overseer Eye consumption), camera shake, and interactive Shift Complete Victory Summary Panel with Endless Mode continuation.
- [x] **Complete Red "ВАУ-перки" (WOW Capstone Perks)**:
  - *Player WOW*: 6m relativistic auto-magnetization, double punt impulse, emissive dash hyperspace trail.
  - *Black Hole WOW*: 2x orbital heat multiplier rate + 12s expanding Hawking shockwave ring flinging 8 outer zone debris items inward.
  - *Cameras WOW*: Panopticon Laser Grid (neon magenta beams, x6 observation multiplier, simultaneous dual-target tracking and dual gravi-flash).
  - *Scrap WOW*: Tier 4 Anomaly consumption instantly advances Heat Multiplier combo stage by +1.0x.
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
- [ ] **Phase 1: 3D Art Asset Production**
  - Integrate production FBX meshes using the `VisualRoot` pattern.
  - Custom event horizon distortion / gravitational lensing shader on `BlackHoleSphere`.
  - Holographic arena boundary grid shader.
- [ ] **Phase 2: Audio Clip Integration**
  - Populate `AudioConfig.asset` with custom sound effects and ambient hums.

---

## 7. 3D & Audio Asset Replacement Guide

This section is dedicated to 3D Artists and Sound Designers joining the project.

### 7.1 Guide for 3D Artists (The `VisualRoot` Workflow)
Every physical entity in **Orbital Panopticon** strictly isolates physics and scripts on the GameObject root, while all visual representations live under a child transform named **`VisualRoot`**. 

**Strict Rules for 3D Artists:**
1. **Never touch root components**: Do NOT add, remove, or modify `Rigidbody`, `Collider`, or gameplay scripts on root GameObjects.
2. **Drop meshes inside `VisualRoot`**: To replace prototype greybox meshes with production `.fbx` or `.obj` models, place your model directly inside `VisualRoot` and delete the old primitive children.
3. **Pivots and Scale**: Ensure FBX models are exported with uniform scale (`1, 1, 1`) and the pivot point centered at `(0, 0, 0)`.

| Entity | Scene / Prefab Location | Where to Drop FBX Models | Notes |
| :--- | :--- | :--- | :--- |
| **Player Drone** | `PlayerDrone` in `OutdoorsScene.unity` | `PlayerDrone/VisualRoot` | Visible in reflections or external cameras. Cockpit view disables these renderers so your camera view remains unobstructed. |
| **FPV Cockpit Robo-Arm** | `PlayerDrone/MainCamera/RoboArmVisual` | `RoboArmVisual` | Source-engine style manipulator viewmodel. Claws point along local `+Z`. |
| **Scrap Items (Tiers 1–3)** | `Assets/_Project/Prefabs/Scrap_TierX.prefab` | `Scrap_TierX/VisualRoot` | Can be mechanical debris, broken solar panels, satellite hulls. Root collider bounds must approximate the mesh. |
| **Tier 4 Anomaly** | `Assets/_Project/Prefabs/Scrap_Tier4_Anomaly.prefab` | `Scrap_Tier4_Anomaly/VisualRoot` | Monolithic ancient alien artifact or glowing hyper-dense relic. |
| **Helper Robot** | `Assets/_Project/Prefabs/HelperDroneBot.prefab` | `HelperDroneBot/VisualRoot` | Small autonomous salvage worker with cyan visor/light. Tether connects at bot center. |
| **Surveillance Camera** | `DynamicSurveillanceCamera` instances | `DynamicSurveillanceCamera/VisualRoot` | Heavy industrial panopticon turret mounted on outer shell. Forward vector is local `+Z`. |
| **Corporate Overseer Eye** | `CorporateOverseerEye` in `OutdoorsScene` | `CorporateOverseerEye/VisualRoot` | Cyclopean celestial eye observing the arena. Front pupil looks along local `+Z`. |
| **Black Hole System** | `BlackHoleSystem` in `OutdoorsScene` | `BlackHoleSystem/VisualRoot` | Singularity core and accretion disk visual geometry. |

### 7.2 Guide for Sound Designers (`AudioConfig.asset`)
All gameplay sound effects and ambient sound loops are managed centrally via [`AudioConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/AudioConfig.asset) and played via [`GameAudioManager.Instance`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/GameAudioManager.cs).

**Instructions for Sound Designers:**
1. Open [`Assets/_Project/ScriptableObjects/AudioConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/AudioConfig.asset) (or the mirror in `Assets/ScriptableObjects/`) in the Unity Inspector.
2. Drag and drop your `.wav` or `.ogg` audio clips into the appropriate serialized slots:
   * **1. Drone Flight & Thrusters**:
     * `thrusterLoop`: Looping low hum for active flight.
     * `dashBoostSfx`: Punchy burst sound for directional dash (`Left Shift`).
     * `brakeSfx`: Retro-thruster hiss for active braking (`X`).
   * **2. Gravity Manipulator (Gravity Gun)**:
     * `magnetPullLoop`: Electric tractor beam loop when holding `RMB`.
     * `grabLatchSfx`: Satisfying mechanical clamp/latch when scrap snaps into the claw.
     * `puntLaunchSfx`: Half-Life 2 style pneumatic launch blast (`LMB`).
     * `dryPuntSfx`: Dry shockwave blast when clicking `LMB` with no held cargo.
     * `observedGrabChimeSfx`: High-value reward chime when grabbing a Camera-Observed (`x3`) target.
   * **3. Black Hole & Accretion Dynamics**:
     * `blackHoleAmbientLoop`: Deep sub-bass cosmic rumble for the singularity.
     * `accretionSwirlLoop`: High-velocity rotational whoosh for the accretion disk.
     * `eventHorizonCrunchSfx`: Gravitational crunch / spaghettification when debris is consumed.
     * `hawkingPulseSfx`: Gravitational shockwave explosion sound for Hawking Pulse.
   * **4. Surveillance Panopticon & Cameras**:
     * `cameraLockBeepSfx`: Mechanical servo lock chirp when a camera acquires a target.
     * `cameraGraviFlashSfx`: Intense gravimetric burst sound for camera Gravi-Flash.
     * `boundaryWarningSirenSfx`: Pulsing siren when approaching the 42m arena boundary.
   * **5. Quota & UI Feedback**:
     * `quotaCompleteFanfareSfx`: Rewarding corporate fanfare played when a quota is completed.
     * `upgradePurchasedSfx`: High-tech interface chime when purchasing an upgrade in the Skill Tree (`TAB`).
     * `coinEarnedSfx`: Digital coin acquisition sound.
     * `buttonClickSfx`: Standard UI interaction click.
   * **6. Endgame Finale**:
     * `sectorCollapseAlarmSfx`: Urgent red alert klaxon for Singularity Sector Collapse.
     * `overseerDestructionSfx`: Cataclysmic explosion/implosion when Corporate Overseer Eye is consumed.
     * `victoryChimeSfx`: Triumphant victory summary sound on Shift Complete screen.
3. **Automatic Hookup**: All events (grabbing, punting, completing quotas, dashing, consumption) already invoke `GameAudioManager.Instance` methods in code; once clips are assigned to the asset, they play immediately in-game with natural pitch jitter.
