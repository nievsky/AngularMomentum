# Orbital Panopticon — Project Architecture & Technical Context

> **Version**: 2.0.0 (3-Scene Architecture, Star Fox Narrative Comms & Pure Mass Progression)  
> **Target Engine**: Unity 6 (6000.4.4f1) / Universal Render Pipeline (URP)  
> **Active Scenes Flow**:
> 1. `Assets/_Project/Scenes/MainMenuScene.unity` (Build Index 0)  
> 2. `Assets/_Project/Scenes/GameplayScene.unity` (Build Index 1 — Primary Game Arena)  
> 3. `Assets/_Project/Scenes/CreditsScene.unity` (Build Index 2 — Star Wars Perspective Crawl)  
> *(Legacy Reference: `Assets/OutdoorsScene.unity`, Build Index 3)*  
> **Balance Asset**: [`GameBalanceConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/GameBalanceConfig.asset)  
> **Audio Configs**: [`FMODAudioConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/FMODAudioConfig.asset) & [`NarrativeDialogueConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/NarrativeDialogueConfig.asset)  
> **Design Theme**: 3D Space Salvage / Incremental Physics / Surveillance Panopticon

---

## 1. Project Overview & Core Gameplay Loop

### 1.1 High Concept
**Orbital Panopticon** is a First-Person (FPV) 6DoF space salvage and incremental physics game set in an orbital surveillance arena dominated by a central supermassive **Black Hole**. 

The player assumes the role of indentured salvage drone **Operator #042** contracted by an ominous corporate entity. Patrolling the outer boundary are **4 Identical Corporate Overseer Eyes** projecting continuous **Containment Beams** straight into the central Black Hole.

> [!IMPORTANT]
> **Containment Beams Interaction**:
> - **For Scrap**: Beams remain 100% visual only (no colliders, no scrap physics triggers, and no scrap multipliers).
> - **For Player Drone**: Active beams act as a physical hazard! Proximity check detects if the drone is within `2.2m` of the 3D line segment between an Eye and the Singularity. 3 hits destroy the drone (signal loss & wreckage spawned into the black hole, no HP bar).
> There is **NO energy battery, NO siphoning from the Black Hole, NO shooting lasers at bosses, and NO boss HP bars**. The game is focused on pure incremental trash collection, exponential mass growth, and narrative radio transmissions.

### 1.2 Core Gameplay Loop
```
                                 [ 1. Orbital Search ]
               Search calm Outer Zone for debris and glowing golden Valuable Scrap (3x->12x)
                                          │
                                          ▼
                                [ 2. Approach & Grab ]
               Fly in 6DoF cockpit view; latch debris using Robo-Arm / Magnet (RMB)
               Valuable scrap triggers: "VALUABLE ANOMALY GRABBED - x3 VALUE!"
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
               Score & Mass = BaseValue × OrbitalBonus × ValuableBonus × Heat × StageMultiplier
               Singularity Mass increases (+12 per mass unit × StageMultiplier × QuantumDecay)
                                          │
                                          ▼
                      [ 6. Pure Mass Stages & Containment Locks ]
               Mass grows through Stages 1–4 with exponential gaps:
               Stage 1: 500 tons   (Scale: 6m,  Mult: 1.0x,  Locks: 4/4)
               Stage 2: 4,000 tons (Scale: 10m, Mult: 2.5x,  Locks: 3/4)
               Stage 3: 25,000 tons(Scale: 15m, Mult: 6.0x,  Locks: 2/4)
               Stage 4: 120,000 t  (Scale: 22m, Mult: 15.0x, Locks: 1/4 -> Critical Mass!)
                                          │
                                          ▼
                     [ 7. Star Fox Narrative Comms & Lock Shatter ]
               Reaching each stage target triggers Russian radio transmission on HUD
               and calls BreakContainmentLock() on an Overseer Eye:
               Plays electrical overload flash, extinguishes beam, dims eye to offline state.
                                          │
                                          ▼
                         [ 8. Suspense Finale & Singularity Pull ]
               Reaching 120,000 tons breaks the 4th lock (0/4 beams active):
               Alarm sounds -> 4 Eyes sucked inward into (0,0,0) -> 
               Player drone locked & pulled across event horizon ->
               Procedural black dissolve transition -> Star Wars Credits Scene!
```

---

## 2. Strict Architectural Rules (Must Follow in All Future Sessions)

Future AI agents and developers **must adhere strictly** to the following core architectural principles:

### Rule 1: The `VisualRoot` Pattern (Strict Separation of Physics and Visuals)
* **Rule**: All GameObjects that interact with physics or gameplay logic (`PlayerDrone`, `ScrapItem`, `DynamicSurveillanceCamera`, `ArenaBoundary`, `OverseerContainmentEye`, `BlackHoleSystem`) must place components (`Rigidbody`, `Colliders`, gameplay scripts) **strictly on the root GameObject**.
* **Visual Children**: All meshes, `MeshFilter`, `MeshRenderer`, and aesthetic primitive shapes must reside inside a dedicated child transform named `VisualRoot`.
* **Why**: Enables immediate, non-destructive replacement of greybox prototyping shapes (cubes, cylinders, spheres) with final 3D production models (`.fbx` / `.obj`) without breaking script references, colliders, hierarchy bindings, or serialized fields.

### Rule 2: Centralized Balance Configuration (`GameBalanceConfig`)
* **Rule**: **Zero hardcoded magic numbers** in gameplay or physics scripts.
* **Mechanism**: Every speed, force, radius, multiplier, cooldown, and timing constant MUST be declared as a serialized field in `GameBalanceConfig.cs` and tuned inside [`Assets/_Project/ScriptableObjects/GameBalanceConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/GameBalanceConfig.asset).
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

## 3. Multi-Scene Architecture & Transitions

The game is structured into a clean 3-scene flow configured in `EditorBuildSettings.scenes`:

```
[ 0: MainMenuScene ] ──[НАЧАТЬ ИГРУ]──► [ 1: GameplayScene ] ──Finale Singularity──► [ 2: CreditsScene ]
         ▲                                                                                   │
         └──────────────────────────[В ГЛАВНОЕ МЕНЮ]────────────────────────────────────────┘
```

### 3.1 Scene Transition Manager (`SceneTransitionManager.cs`)
* **Global Singleton**: Instantiated via `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`, marked with `DontDestroyOnLoad`.
* **Top-Sorting Overlay & URP Bloom Integration**: Dedicated `Transition_Canvas` configured as `RenderMode.ScreenSpaceCamera` bound to `Camera.main` at `planeDistance = 0.35f` (`sortingOrder = 9999`) with fallback to `ScreenSpaceOverlay`. This ensures URP post-processing (Bloom, ACES tonemapping) directly catches the HDR fiery dissolve edge.
* **Fiery Orange HDR Dissolve Shader (`S_ScreenDissolve.shader`)**:
  * Multi-layer burning edge gradient: scorching white-yellow core (`[HDR] _EdgeCoreColor = (8.0, 4.5, 1.0, 1.0)`), fiery orange-red flame (`[HDR] _EdgeOuterColor = (4.5, 0.9, 0.05, 1.0)`), and expanding heat halo (`_SoftGlowWidth = 0.16f`, `_DissolveEdgeWidth = 0.09f`).
  * Driven by `_DissolveProgress` (`0.0` = completely clear/invisible, `1.0` = completely solid black).
  * Automatically pulses post-processing bloom (`bloomBoost: 2.2f`) via `PostProcessingToolkitController.Instance.TriggerImpactPulse()` on transitions.
* **Game Launch Boot Dissolve-In**:
  * On initial boot, starts at `_DissolveProgress = 1.0f` (solid black overlay) and waits **2 settling frames** behind the curtain for cameras, skybox, and post-processing to initialize.
  * Then runs `InitialDissolveInRoutine()` animating `_DissolveProgress` `1.0 -> 0.0` over `0.85s` (`Time.unscaledDeltaTime`) with audio cue, seamlessly revealing `MainMenuScene`.
* **Standard Two-Way Scene Transition (`TransitionRoutine`)**:
  1. **Step 1 (Fade In Black Screen / Dissolve Out Current Scene)**: Animates `_DissolveProgress` `0.0 -> 1.0` over `0.85s` (`Time.unscaledDeltaTime`) while playing `dissolveTransitionOneShot`.
  2. **Step 2 (Background Async Load & Frame Settle)**: Screen is pitch black; loads scene via `SceneManager.LoadSceneAsync(sceneName)` until `op.isDone`, then waits **2 extra frames** (`yield return null; yield return null;`) behind the curtain so the new scene's camera, skybox, and post-processing initialize without frame drops or visual glitches (`Time.timeScale = 1f`).
  3. **Step 3 (Fade Out Black Screen / Dissolve In New Scene)**: Animates `_DissolveProgress` `1.0 -> 0.0` over `0.95s` (`Time.unscaledDeltaTime`) while playing `dissolveTransitionOneShot` and smoothly revealing the active scene.
* **Specialized Finale Transition (`TransitionFromBlackScreenToScene`)**:
  * Used after the CRT camera shutdown in `GameplayScene`: skips Step 1 (avoids redundant fade-in on an already black screen), loads `CreditsScene` asynchronously, and executes ONLY the fiery dissolve fade-out over `1.2s`.

### 3.2 Main Menu Scene (`MainMenuScene.unity`)
* **Live 3D Simulation Background**: Space skybox, `BlackHoleSystem` at `(0,0,0)`, and pre-warmed `ScrapSpawner` (40 debris items orbiting and spiraling inward).
* **Cinematic Orbit Camera**: [`MenuCameraOrbit.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/MenuCameraOrbit.cs) orbiting the singularity at radius `32f` and speed `4.0 deg/s`.
* **Menu Canvas (`MainMenuUIController.cs`)**:
  * Game Title: `"ORBITAL PANOPTICON"` / Subtitle: `"SURVEILLANCE SECTOR #042"`.
  * 4 Navigation Buttons:
    1. **`[НАЧАТЬ ИГРУ]`**: Calls `SceneTransitionManager.Instance.TransitionToScene("GameplayScene")`.
    2. **`[НАСТРОЙКИ]`**: Opens collapsible Settings sub-panel for Master Volume (`AudioListener.volume`) and Mouse Sensitivity saved to `PlayerPrefs`.
    3. **`[АВТОРЫ / КРЕДИТЫ]`**: Calls `SceneTransitionManager.Instance.TransitionToScene("CreditsScene")`.
    4. **`[ВЫЙТИ ИЗ ИГРЫ]`**: Calls `Application.Quit()`.

### 3.3 Gameplay Scene (`GameplayScene.unity`)
* **Active Arena**: Complete FPV drone salvage arena with Black Hole, 4 identical Overseer Containment Eyes, 100-item debris simulation pool, and cockpit HUD.
* **Progressive Stages 1–4**: Pure mass accretion progression driving singularity growth, containment lock breaks, and narrative radio transmissions.

### 3.4 Credits Scene (`CreditsScene.unity`)
* **Star Wars Perspective Crawl**: World-space Canvas tilted backward at `Rotation X = 62.0°` (`Camera.main.transform.position = (0, 0, -28)`).
* **Smooth Text Scroll ([`StarWarsCrawlController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/StarWarsCrawlController.cs))**:
  * Crawl speed: `38 units/sec` (accelerates to `3x` speed while holding `Space` or `LMB`).
  * Narrative epilogue detailing Operator #042's transcendence beyond the event horizon into corporate cyberspace.
  * Return button `[В ГЛАВНОЕ МЕНЮ]` anchored at bottom-right returning to `MainMenuScene`.

---

## 4. Directory Structure & Script Map

```
Assets/
├── _Project/
│   ├── Materials/                         <-- S_ScreenDissolve.shader, S_OuterWildsBlackHole.shader, S_AccretionDisk.shader, S_ValuableLavaLoot.shader, S_LavaEmber.shader, S_ParticleAdditiveGlow.shader, S_VHSMonitorEffect.shader, S_ProceduralSpaceSkybox.shader, M_OuterWildsBlackHole.mat, M_AccretionDisk.mat, M_ValuableLavaLoot.mat, M_LavaEmberParticle.mat, M_ParticleAdditiveGlow.mat, M_VHSMonitorEffect.mat, M_ProceduralSpaceSkybox.mat
│   ├── Prefabs/                           <-- VFX_ValuableLavaEmbers.prefab, VFX_GravityPullStream.prefab, VFX_GravityPuntBlast.prefab, VFX_SpaceFlightDust.prefab, VFX_ShiftDashBurst.prefab, VFX_AccretionSparks.prefab, VFX_SingularityImplosion.prefab, HelperDroneBot.prefab
│   ├── Scenes/                            <-- MainMenuScene.unity, GameplayScene.unity, CreditsScene.unity
│   ├── Settings/                          <-- MainPostProcessProfile.asset
│   └── ScriptableObjects/                 <-- GameBalanceConfig.asset, FMODAudioConfig.asset, NarrativeDialogueConfig.asset, GlobalFontConfig.asset, VisualEnvironmentConfig.asset
├── Scripts/
│   ├── Core/                              <-- Game management, balance, audio, UI, comms
│   ├── Player/                            <-- FPV 6DoF flight and gravity manipulator
│   ├── Physics/                           <-- Black hole orbital mechanics and gravity bodies
│   ├── Gameplay/                          <-- Containment Eyes, stage manager, endgame finale, scrap simulation
│   └── UI/                                <-- Menu controllers, crawl controller, scene transitions
```

### 4.1 `Core/`
| Script | Key Responsibilities | Key Public API |
| :--- | :--- | :--- |
| [`GameBalanceConfig.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/GameBalanceConfig.cs) | ScriptableObject defining all global simulation, pacing, black hole, drone, manipulator, scrap, and stage parameters. | `stageMassTargets`, `stageBlackHoleScales`, `stageValueMultipliers`, `mouseSensitivity` |
| [`GameManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/GameManager.cs) | Central singleton manager coordinating game state and providing global static access to active configs. | `GameManager.Instance`, `GameManager.GameBalanceConfigInstance` |
| [`QuotaAndScoreManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/QuotaAndScoreManager.cs) | Computes multi-tier scores, manages heat multiplier combos (`1x`→`16x`), and tracks Scrap Coins. | `CurrentQuotaProgress`, `TargetQuota`, `CurrentHeatMultiplier`, `ScrapCoins` |
| [`NarrativeDialogueConfigSO.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/NarrativeDialogueConfigSO.cs) | ScriptableObject containing Russian radio transmission entries (speaker name, portrait, theme color, text, timing) for Stages 1–4 and Finale. | `stage1StartSupervisor`, `stage2FirstBeamOffline`, `stage3SecondBeamOffline`, `stage4ThirdBeamOffline`, `finaleAllBeamsOffline` |
| [`NarrativeCommsUI.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/NarrativeCommsUI.cs) | Star Fox style comms box on HUD Canvas. Slide-in/out tween, portrait frame, colored name header, typewriter text effect (`0.03s`), and FMOD audio blips. | `Instance`, `PlayTransmission()`, `PlayFinaleTransmission()` |
| [`UIManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UIManager.cs) | Cockpit HUD: REC status, Quota bar, active containment beams indicator (`● LOCKS: X / 4`), stage mass telemetry, and dynamic reticle. | `Instance`, `SetCrosshairState()`, `SetObservedNotice()`, `HandleSignalLost()` |
| [`UpgradeTreeManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UpgradeTreeManager.cs) | Radial 5-branch upgrade tree data and purchase manager toggled via TAB with game pause (`Time.timeScale = 0`). 20 upgrade nodes and WOW capstone perks. | `Instance`, `ToggleTree()`, `PurchaseNode()`, `IsNodeUnlocked()` |
| [`UpgradeTreeUI.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UpgradeTreeUI.cs) | DOTween-choreographed skill tree interface with unscaled-time (`.SetUpdate(true)`) outward ripple animations on TAB open, line pulsing on purchase, and docked `"Купить робота"` button. | `Instance`, `ToggleTree()`, `SetTreeOpen()`, `RefreshUI()` |
| [`SkillTreeViewportController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/SkillTreeViewportController.cs) | Fullscreen viewport pan and scroll-wheel zoom controller (`0.45x` to `2.2x`) zooming toward cursor position, drag panning, and Space / `[СБРОСИТЬ КАМЕРУ]` recentering. | `CurrentZoom`, `MapContainer`, `HandleZoom()`, `ResetView()` |
| [`UIButtonTweenAnimator.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UIButtonTweenAnimator.cs) | Reusable juicy procedural button animator (`IPointerEnterHandler`, `IPointerDownHandler`, etc.). Hover scale (`1.08x`), exit scale (`1.0x`), punch click, purchase success green flash, and failure horizontal shake. | `PlayPurchaseSuccessAnimation()`, `PlayInsufficientCoinsAnimation()`, `SetOriginalScale()`, `RefreshBaseColors()` |
| [`MusicPlaylistManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/MusicPlaylistManager.cs) | Manages multi-track sequential music playlists across scenes (MainMenu, Gameplay, Credits) with smooth unscaled-time crossfades, FMOD EventReferences & Unity AudioClip fallbacks, and track completion monitoring. | `Instance`, `PlayPlaylist()`, `CrossfadeToTrack()`, `PlayNextTrack()`, `PlayPreviousTrack()`, `CurrentPlaylistType`, `CurrentTrackIndex` |
| [`VisualEnvironmentConfigSO.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/VisualEnvironmentConfigSO.cs) | Persistent visual environment configuration ScriptableObject holding all persistent settings for Skybox, VHS monitor effect, and URP Post-Processing toolkit. Prevents settings reset across scenes. | [`VisualEnvironmentConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/VisualEnvironmentConfig.asset), `MarkDirty()` |
| [`SpaceSkyboxController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/SpaceSkyboxController.cs) | Live procedural skybox controller (`[ExecuteAlways]`) driving `S_ProceduralSpaceSkybox.shader` and `RenderSettings.skybox`. Exposes full Inspector controls for gradient colors, multi-tier starfields, 3D rotatable Milky Way band with dust lanes, and domain-warped nebulae. Reads/writes directly to `VisualEnvironmentConfig.asset`. | `Instance`, `visualConfig`, `SkyboxMaterial`, `SetMilkyWayRotation()`, `SetStarDensity()`, `UpdateMaterialProperties()`, `LoadFromConfig()`, `SaveToConfig()` |
| [`VHSCameraController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/VHSCameraController.cs) | Live CCTV/VHS surveillance monitor controller (`[ExecuteAlways]`) driving `S_VHSMonitorEffect.shader` properties (scanlines, tracking glitch, RGB shift, tape noise, CRT curvature, vertical jitter, overscan scale). Reads/writes directly to `VisualEnvironmentConfig.asset`. | `Instance`, `visualConfig`, `TriggerGlitchBurst()`, `SetMasterBlend()`, `SetVHSEnabled()`, `UpdateMaterialProperties()`, `LoadFromConfig()`, `SaveToConfig()` |
| [`PostProcessingToolkitController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/PostProcessingToolkitController.cs) | Master Post-Processing Volume controller exposing live Inspector sliders for ACES Tonemapping, Bloom, Vignette, Chromatic Aberration, Lens Distortion (with auto-compensated scale 1.15 to eliminate empty borders), Color Adjustments, Film Grain, and Motion Blur. Coordinates gameplay impact pulses and reads/writes to `VisualEnvironmentConfig.asset`. | `Instance`, `visualConfig`, `TriggerImpactPulse()`, `ApplySettingsToVolume()`, `SetBloomIntensity()`, `SetChromaticAberration()`, `LoadFromConfig()`, `SaveToConfig()` |
| [`GlobalFontConfigSO.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/GlobalFontConfigSO.cs) | Centralized typography ScriptableObject defining master TextMeshPro font asset, master legacy UI font, global scale multiplier, and character spacing. Auto-triggers live scene updates on Inspector modification. | `masterTMPFont`, `masterLegacyFont`, `globalFontScaleMultiplier`, `globalCharacterSpacing` |
| [`GlobalFontApplier.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/GlobalFontApplier.cs) | Project-wide typography singleton and runtime applier (`[ExecuteAlways]`). Traverses active scenes and instantiated UI to apply master fonts and spacing with `GlobalFontTracker` base size protection. | `Instance`, `FontConfig`, `ApplyGlobalFontToEverything()`, `ApplyToHierarchy()`, `GlobalFontApplier.ApplyTo()` |
| [`FMODAudioConfigSO.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/FMODAudioConfigSO.cs) | Centralized configuration for FMOD Studio 2.03.08 exposing background music playlists, 18 sound designer event references, fallback clips, and global automation parameters. | [`FMODAudioConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/FMODAudioConfig.asset) |
| [`FMODAudioManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/FMODAudioManager.cs) | Central FMOD controller managing persistent loops (`spaceAmbienceLoop`, `singularityHumLoop`, `containmentBeamHumLoop`, `thrusterLoop`), global parameters (`BlackHoleStage`, `DistanceToHole`, `DroneSpeed`, `ActiveContainmentBeams`, `ComboLevel`), and null-safe one-shots. | `Instance`, `PlayContainmentLockBreak()`, `PlayCommsOpenBeep()`, `PlayCommsVoiceBlip()`, `PlayFinaleEyesConsumedRumble()`, `PlayPlayerHorizonWarp()`, `PlayDissolveTransition()` |
| [`VFXGraphBridge.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/VFXGraphBridge.cs) | Universal bridge component wrapping `UnityEngine.VFX.VisualEffect` alongside particle systems and ribbon trails. Exposes dynamic parameters (`TargetPosition`, `Intensity`, `Color`, `SpawnRate`) with graceful null-safe fallbacks. | `Instance`, `Play()`, `Stop()`, `SetTarget()`, `SetIntensity()`, `SetColor()` |

### 4.2 `Player/`
| Script | Key Responsibilities | Key Public API |
| :--- | :--- | :--- |
| [`DroneController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/DroneController.cs) | First-Person 6DoF cockpit flight controller (New Input System). Mouse look (clamped pitch, locked roll), look-directed thrusters (`W`/`S`), strafing (`A`/`D`), vertical lift (`Space`/`LeftCtrl`/`C`), retro-braking (`X`), Directional Dash (`Left Shift`), and containment beam hit state tracking. | `Teleport()`, `SetControlEnabled()`, `SetMovementLocked()`, `CurrentBeamHits`, `BeamHitCooldownTimer`, `IsDashing`, `CurrentCargoMass`, `MainCamera` |
| [`CameraShakeController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/CameraShakeController.cs) | Trauma-based camera shake and psychological ambient tremor system. Operates strictly in `LateUpdate` applying non-destructive positional and rotational offsets on top of mouse look. | `Instance`, `TriggerShake()`, `TriggerStageTransitionShake()`, `ResetShake()`, `CurrentTrauma` |
| [`GravityManipulatorController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/GravityManipulatorController.cs) | Authentic Half-Life 2 Gravity Gun. Strict RMB pull/hold, LMB punt launch and dry punt shockwave cast. Cluster holding (up to 3 items) with semi-transparency. | `HeldItems`, `HeldCount`, `IsHolding`, `HoldPoint`, `ReleaseAllHeld()`, `PlayPuntAnimation()` |

### 4.3 `Gameplay/`
| Script | Key Responsibilities | Key Public API |
| :--- | :--- | :--- |
| [`BlackHoleStageManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/BlackHoleStageManager.cs) | Pure mass progression manager. Interpolates Black Hole visual scale (`6m -> 22m`), event horizon, and accretion vortex across Stages 1–4. Coordinates containment lock shattering, stage transition shockwaves, and narrative transmissions. | `Instance`, `CurrentMass`, `CurrentStage`, `ActiveContainmentLocksCount`, `AddMass()` |
| [`OverseerContainmentEye.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/OverseerContainmentEye.cs) | Four identical Overseer Eyes (live-tunable scale 14m, orbit radius 78m, angular speed 8 deg/s, tilted normal axes) with containment beams (`LineRenderer`). Executes 3D line segment player hazard collision (3 hits = drone destroyed) and `BreakContainmentLock()` overload shutdown. | `IsActiveLock`, `BreakContainmentLock()`, `PupilPosition` |
| [`EndgameFinaleController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/EndgameFinaleController.cs) | Orchestrates the suspense finale sequence when the 4th containment lock shatters: red alert sirens -> 4-Eye inward death spiral -> relativistic player horizon pull (`FOV 60 -> 115`) -> transition to `CreditsScene`. | `TriggerEndgameFinale()`, `IsFinaleTriggered` |
| [`ScrapSimulationManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/ScrapSimulationManager.cs) | Data-oriented pool (100 items) and batched simulation manager for debris orbiting in the arena. | `Instance`, `ActiveScrap`, `SpawnScrap()`, `ReturnToPool()` |

### 4.4 `UI/`
| Script | Key Responsibilities | Key Public API |
| :--- | :--- | :--- |
| [`SceneTransitionManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/Legacy/SceneTransitionManager.cs) | Procedural black screen dissolve screen transitions between MainMenu, Gameplay, and Credits scenes. | `Instance`, `TransitionToScene()`, `LoadScene()` |
| [`MainMenuUIController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/MainMenuUIController.cs) | Main Menu interaction: play button, settings panel (volume slider, mouse sensitivity), credits button, quit button. | `OnPlayClicked()`, `OnCreditsClicked()`, `OnQuitClicked()` |
| [`StarWarsCrawlController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/StarWarsCrawlController.cs) | Tilted perspective Star Wars text crawl with 3x acceleration on Space/LMB and return button to Main Menu. | `Instance`, `SetScrollSpeed()` |
| [`MenuCameraOrbit.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/MenuCameraOrbit.cs) | Smooth 360° circular camera orbit around the Black Hole in the Main Menu background. | `orbitRadius`, `orbitSpeed` |

---

## 5. Narrative Radio Comms & Suspense Finale System

### 5.1 Star Fox Style Narrative Comms UI
During stage advancement, incoming corporate radio transmissions slide in at top-center of the HUD without pausing the game:

| Stage Event | Speaker | Theme Color | Dialogue Line (Russian) |
| :--- | :--- | :--- | :--- |
| **Stage 1 Start** | Корпоративный Куратор | Cyan (`#00FFFF`) | *"Оператор Дрона #042, приступайте к штатной уборке сектора. Четыре Ока Паноптикума удерживают сингулярность в безопасных пределах."* |
| **Stage 2 — 1st Lock Broken** | Корпоративный Куратор | Yellow (`#FFD700`) | *"Дрон #042, вы превысили лимит подачи материи! Сдерживающий луч №1 перегорел. Снизьте темп утилизации!"* |
| **Stage 3 — 2nd Lock Broken** | ИИ Службы Безопасности | Orange (`#FF8C00`) | *"ТРЕВОГА: Отключен второй контур сдерживания! Зафиксирован несанкционированный взлом протоколов прокачки дрона. Оператор, ответьте!"* |
| **Stage 4 — 3rd Lock Broken** | Паникующий Директор | Neon Red (`#FF2020`) | *"Вы что творите?! Остался последний луч! Если он погаснет, дыра поглотит весь надзорный сектор! Отключить его немедленно!.. ОШИБКА ДОСТУПА."* |
| **Finale — All Locks Broken** | Системный Оверрайд | Deep Magenta (`#FF00FF`) | *"ВСЕ ОГРАНИЧИТЕЛИ СНЯТЫ. ПАНОПТИКУМ ОСЛЕПЛЕН. ДОБРО ПОЖАЛОВАТЬ ЗА ГОРИЗОНТ СОБЫТИЙ, ДРОН #042."* |

### 5.2 The Finale Sequence (`EndgameFinaleController`)
When Black Hole mass reaches **120,000 tons** (Stage 4 target), all containment is broken:
1. **Critical Alarm & Transmission (0–4s)**: HUD flashes red klaxons; System Override transmission plays.
2. **Inward Suction of the 4 Eyes (4–10s)**: All 4 deactivated Overseer Eyes are caught in the gravitational vacuum, tumbling and spiraling into `(0,0,0)` with camera shake and massive score popups (`+999,999`).
3. **Player Horizon Pull (10–14s)**:
   * Drone movement controls lock (mouse look stays enabled so the player can watch the singularity expand).
   * Drone is pulled smoothly into `(0,0,0)`.
   * Camera FOV expands from `60°` to `115°` for relativistic optical warping with camera shudder.
4. **Dissolve Transition to Credits**: The instant the drone crosses `(0,0,0)`, `SceneTransitionManager.Instance.TransitionToScene("CreditsScene")` triggers the black dissolve wipe into the Star Wars perspective crawl.

### 5.3 Outer Wilds Gravitational Lensing & Accretion Disk
- **Screen-Space Gravitational Lensing ([`S_OuterWildsBlackHole.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_OuterWildsBlackHole.shader), [`M_OuterWildsBlackHole.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_OuterWildsBlackHole.mat))**:
  - Custom URP transparent shader (`Queue = Transparent-100 / 2900`, `ZWrite Off`, `Cull Front`) sampling `_CameraOpaqueTexture` and `_CameraDepthTexture`.
  - **True Depth Rejection (Fix Issue #1)**: Evaluates `LinearEyeDepth(SampleSceneDepth(screenUV))` against `length(_WorldSpaceCameraPos - centerWS) - coreRadiusWorld * 1.05f`. If an opaque object (such as `RoboArmVisual`, held scrap, or debris in front of the black hole) is closer than the singularity, the shader immediately executes `discard;`, preventing the black hole from ever rendering over foreground objects.
  - Warps background stars, Overseer beams, and debris around the singularity center:
    1. **Singularity Core (`r < _CoreRadius`)**: Pure pitch-black core (`float4(0, 0, 0, 1)`).
    2. **Photon Ring / Einstein Ring (`r ≈ _CoreRadius`)**: Razor-thin warm photon ring where light orbits the event horizon.
    3. **Relativistic Gravitational Lensing (`r >= _CoreRadius`)**: Inverse power curve deflection offsetting screen UV toward the center.
  - Lensing sphere mesh scaled to `2.8x` inside `VisualRoot` so the lens extends wide around the core.
- **Solid Black Event Horizon Opaque Core ([`S_EventHorizonOpaqueCore.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_EventHorizonOpaqueCore.shader), [`M_EventHorizonOpaqueCore.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_EventHorizonOpaqueCore.mat))**:
  - Inner sphere `EventHorizonOpaqueCore` child of `VisualRoot` with pure unlit pitch-black opaque shader (`Queue = Geometry / 2000`, `ZWrite On`, `ZTest LEqual`).
  - Scaled dynamically in `BlackHoleStageManager` to match `eventHorizonRadius * 2f`.
  - Ensures depth buffer writing and solid occlusion for any geometry or line renderers behind the Event Horizon.
- **Optional Toggleable Accretion Disk ([`S_AccretionDisk.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_AccretionDisk.shader), [`M_AccretionDisk.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_AccretionDisk.mat))**:
  - Flat horizontal disk/quad (`AccretionDiskVisual`, scale `7.0, 7.0, 1.0`) inside `VisualRoot` with polar coordinate Keplerian differential swirl, dual-layer procedural FBM noise, and hot-to-cool radial temperature gradient (`_InnerHotColor` to `_OuterCoolColor`).
  - Render queue `Transparent+20` so the disk is drawn before the lensing sphere and dynamically distorted by the gravitational lens.
  - Toggled live anytime via `GameBalanceConfig.showAccretionDisk` (default `false`).

### 5.4 Production-Quality Molten Lava Shader & Ember VFX for Valuable Scrap
- **Procedural 3D Lava/Magma Shader ([`S_ValuableLavaLoot.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_ValuableLavaLoot.shader), [`M_ValuableLavaLoot.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_ValuableLavaLoot.mat))**:
  - Custom URP HLSL shader using 3D Object-Space procedural Voronoi and Simplex FBM noise (seamless and UV-free across primitives and custom 3D FBX models).
  - **Layer 1 — Molten Magma Core**: Dual-octave flowing 3D noise rivers of blinding HDR magma (`_MagmaCoreColor` = HDR golden-yellow, `_MagmaMidColor` = HDR fiery orange-red).
  - **Layer 2 — Dark Basalt Crust**: 3D Voronoi cellular rock plates (`_CrustColor = float4(0.08, 0.06, 0.07, 1)`) with heat-glowing edges at plate fractures.
  - **Layer 3 — Pulsing Fresnel Heat Rim & Vertex Breathing**: Orange-gold Fresnel rim (`pow(1.0 - NdotV, _RimPower)`) modulated by `sin(_Time.y * _PulseSpeed)` and subtle vertex expansion for an active molten appearance.
  - **Alpha & Fade Support**: Supports `_Alpha`, `_BaseColor.a`, and `_Color.a` via `MaterialPropertyBlock`, enabling smooth transparency fading when held by the Gravity Gun (`heldObjectAlpha = 0.42f`) or dissolving at arena boundaries.
- **Orbiting Lava Ember Particle Effect ([`S_LavaEmber.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_LavaEmber.shader), [`M_LavaEmberParticle.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_LavaEmberParticle.mat), [`VFX_ValuableLavaEmbers.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_ValuableLavaEmbers.prefab))**:
  - Lightweight GPU unlit additive shader generating procedural soft circular sparks.
  - Particle system emitting 15 HDR gold-orange sparks/s in world simulation space with orbital angular velocity around the object, 3D noise turbulence, and smooth shrink-to-zero lifetime curve.
- **Object Pool Integration ([`ScrapItem.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/ScrapItem.cs))**:
  - `ScrapItem` automatically caches original renderer materials across all child meshes on awake/pool initialization.
  - When `SetupValuableScrap(true)` is invoked, it swaps all mesh renderers to `M_ValuableLavaLoot` and activates/plays `VFX_ValuableLavaEmbers`.
  - When returned to pool via `ResetForPool()` (`SetupValuableScrap(false)`), it cleanly restores original materials and stops/clears the particle system.

### 5.5 Production Particle Systems & Thermal Spaghettification

Orbital Panopticon uses custom Unity `ParticleSystem` components powered by the procedural soft additive glow material [`M_ParticleAdditiveGlow.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_ParticleAdditiveGlow.mat) and shader [`S_ParticleAdditiveGlow.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_ParticleAdditiveGlow.shader):

#### 1. Universal Particle Glow Shader ([`S_ParticleAdditiveGlow.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_ParticleAdditiveGlow.shader))
- Custom URP Unlit Additive transparent shader (`Queue = Transparent+100`, `ZWrite Off`, `Blend One One`, `Cull Off`).
- Computes procedural soft radial circles with controllable core sharpness (`_CoreSharpness = 2.5`) and outer glow falloff (`_GlowFalloff = 1.8`) modulated by particle vertex color and HDR `_EmissionColor`.
- Requires no external texture assets; perfectly anti-aliased at any camera distance.

#### 2. Gravity Gun Pull Vortex ([`VFX_GravityPullStream.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_GravityPullStream.prefab))
- **Mount**: Child of `RoboArmVisual` at local `(0, 0.05, 0.65)`.
- **Trigger**: Active while holding RMB (`Mouse1`) in [`GravityManipulatorController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/GravityManipulatorController.cs).
- **Behavior**: Emits 45 particles/s in a cone shaped inward spiral. Stretched billboard renderer (`lengthScale = 2.0`, `velocityScale = 0.04`) creates energetic tractor filaments twisting with orbital angular velocity (`4.0 rad/s`) and negative radial velocity (`-8.0 m/s`) pulling scrap into the manipulator claw.

#### 3. Gravity Gun Punt Shockwave Blast ([`VFX_GravityPuntBlast.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_GravityPuntBlast.prefab))
- **Mount**: Child of `RoboArmVisual` at local `(0, 0.05, 0.75)`. Replaces old primitive cylinder ring.
- **Trigger**: Single burst on LMB (`Mouse0`) launch/punt in [`GravityManipulatorController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/GravityManipulatorController.cs).
- **Sub-Emitters**:
  1. **`SparksBurst`**: 30 high-velocity sparks (`Speed: 18–28 m/s`, `Lifetime: 0.35s`, Stretched Billboard `lengthScale = 3.5`) exploding outward in a tight forward cone (`Angle = 22°`). Color transitions from blinding electric cyan (`#4CFFFF`) to hot amber and fiery red.
  2. **`MuzzleFlash`**: Short-lived billboard sphere burst (`Lifetime: 0.12s`, `Size: 0.85m`) directly at the claw tip.

#### 4. Cockpit Space Dust & Dynamic Speed Lines ([`VFX_SpaceFlightDust.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_SpaceFlightDust.prefab))
- **Mount**: Direct child of `MainCamera` on `PlayerDrone`.
- **Simulation**: World-space box volume (`25 x 25 x 25` meters) around the cockpit emitting 60 particles/s with lifetime `2.5s`.
- **Dynamic Velocity Coupling**: In [`DroneController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/DroneController.cs), the `velocityOverLifetime` is dynamically set each frame to `-rb.linearVelocity * 0.45f`.
- **Speed Lines**: Stretched billboard renderer (`velocityScale = 0.025`, `lengthScale = 1.0`). At rest, particles float as gentle ambient space motes; as flight velocity increases, they stretch into authentic sci-fi hyperspace speed lines streaming past the cockpit canopy.

#### 5. Directional Dash Burst & FOV Kick ([`VFX_ShiftDashBurst.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_ShiftDashBurst.prefab))
- **Mount**: Child of `PlayerDrone` root.
- **Trigger**: Fired on `Left Shift` dash in [`DroneController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/DroneController.cs).
- **Behavior**: Burst of 40 plasma trail particles oriented instantly opposite to dash direction (`-dashDir`). Particles fire backward at `14–22 m/s` with rapid size decay over `0.45s`.
- **Camera Kick**: Accompanied by a smooth camera FOV kick (`+8°` instant impulse smoothly recovering over `0.25s` via `DashFovKickRoutine`).

#### 6. Scrap Thermal Blackbody Heating & Spaghettification Trail ([`VFX_AccretionSparks.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_AccretionSparks.prefab))
- **Mount**: Instantiated child on every pooled [`ScrapItem.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/ScrapItem.cs).
- **Accretion Inflow Detection**: As scrap crosses the accretion threshold into the singularity, [`ScrapItem.CentralizedUpdate`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/ScrapItem.cs) computes normalized penetration depth `depth01 = (accretionRadius - dist) / (accretionRadius - horizonRadius)`.
- **Thermal Blackbody Emission**: Smooth interpolation via `MaterialPropertyBlock`:
  - `depth01 = 0.0`: Original cold material.
  - `depth01 = 0.5`: Radiant molten orange (`Color(3.5, 1.2, 0.1) * 3.0`).
  - `depth01 = 0.95`: Superheated relativistic white-blue (`Color(2.0, 3.2, 5.0) * 8.0`).
- **Accretion Sparks Trail**: When `depth01 > 0.25f`, `VFX_AccretionSparks` emits trailing sparks streaming into the hole (`rateOverTime` scales up to 120).
- **Singularity Spaghettification**: When [`BlackHoleGravity.SpaghettifyRoutine`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Physics/BlackHoleGravity.cs) stretches scrap along the radial vector into the Event Horizon, [`ScrapItem.TriggerSpaghettifyBurst`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/ScrapItem.cs) fires an intense 25-particle burst into the singularity core before returning the item to the pool.

### 5.6 Master Post-Processing Pipeline & Controllable VHS/Surveillance Camera Effect

A complete, high-fidelity post-processing and surveillance CCTV camera pipeline is active across all scenes (`MainMenuScene`, `GameplayScene`, `CreditsScene`) driven by the `--- POST PROCESSING & VHS ---` GameObject:

#### 1. Global Post-Processing Toolkit ([`PostProcessingToolkitController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/PostProcessingToolkitController.cs))
- **Persistent Profile ([`MainPostProcessProfile.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Settings/MainPostProcessProfile.asset))**:
  - **ACES Tonemapping**: Rich cinematic HDR color grading, vital for glowing molten scrap and relativistic accretion disks.
  - **HDR Bloom**: `Threshold = 0.85f`, `Intensity = 1.6f`, `Scatter = 0.65f`, `HighQualityFiltering = true`.
  - **Vignette**: `Intensity = 0.28f`, `Smoothness = 0.4f` focusing attention on the singularity.
  - **Chromatic Aberration**: `Intensity = 0.08f` base value.
  - **Lens Distortion**: `Intensity = -0.12f` subtle optical curvature.
  - **Color Adjustments**: Contrast `12f`, Post-Exposure `0.15f`, Saturation `8f`, Color Filter `Color.white`.
  - **Cinematic Overrides**: Optional Film Grain, Motion Blur, and Depth of Field exposed for live toggle.
- **Gameplay Juice Pulses (`TriggerImpactPulse`)**:
  - `TriggerImpactPulse(float bloomBoost, float chromaticBoost, float duration)` triggers an unscaled-time exponential decay spike.
  - Wired to:
    - **LMB Scrap Punt**: (`1.4f` bloom, `0.22f` chromatic aberration, `0.35s` duration).
    - **Overseer Lock Break**: (`2.4f` bloom, `0.4f` chromatic aberration, `0.65s` duration).
    - **Shift Dash Impulse**: (`0.6f` bloom, `0.2f` chromatic aberration, `0.25s` duration).

#### 2. Custom VHS / Surveillance Camera Shader & Live Controller ([`S_VHSMonitorEffect.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_VHSMonitorEffect.shader) & [`VHSCameraController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/VHSCameraController.cs))
- **URP FullScreenPassRendererFeature**:
  - Injected directly into `New Universal Render Pipeline Asset_Renderer.asset` at `AfterRenderingPostProcessing` with material [`M_VHSMonitorEffect.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_VHSMonitorEffect.mat).
- **Procedural HLSL Visual Layers**:
  1. **CRT Barrel Curvature & Bezel**: `_ScreenCurvature` (`0..0.5`) bends the image outwards; pixels outside `[0,1]` are masked with a dark bezel border (`#040404`).
  2. **Signal Interlacing & Vertical Jitter**: `_VerticalJitter` (`0..0.02`) adds sporadic VCR tape tracking jumps and odd/even scanline horizontal offset.
  3. **VHS Tracking Glitch Bars**: `_TrackingBarIntensity` (`0..1`), `_TrackingBarSpeed`, `_TrackingBarThickness`, and `_HorizontalGlitchStrength` generate scrolling bands of high-frequency noise and horizontal pixel displacement.
  4. **RGB Chromatic Shift / Color Bleed**: `_RGBShiftAmount` (`0..0.05`) displaces R, G, and B color channels horizontally and diagonally, amplifying inside tracking glitch bars.
  5. **Tape Noise & Static Snow**: `_TapeNoiseIntensity` (`0..1`) computes procedural GPU hash snow moving at high frequency.
  6. **CRT Scanlines**: `_ScanlineIntensity` (`0..1`), `_ScanlineCount` (`100..1080`), and `_ScanlineScrollSpeed` overlay crisp sinusoidal phosphor scan lines.
  7. **Surveillance CCTV Phosphor Tint**: `_CCTVTint` and `_CCTVTintIntensity` (`0..1`) map luminance into authentic surveillance monitor phosphor tones.
  8. **Corner Vignette**: `_CornerVignette` (`0..1`) darkens CRT monitor corners.
  9. **Master Blend & Zero-Overhead Toggle**: `_MasterBlend` (`0..1`) smoothly blends between raw post-processed scene and full VHS output. When `enableVHSEffect = false`, `_MasterBlend = 0.0f` passes through the raw scene with zero glitch or performance penalty.
- **Dynamic Glitch Bursts (`TriggerGlitchBurst`)**:
  - `TriggerGlitchBurst(float glitchBoost = 0.75f, float duration = 0.5f)` executes when an Overseer Eye's containment lock shatters or during red alarms.

### 5.7 Procedural 3D Space Skybox (Stars, Nebulae & Movable Milky Way Band)

Orbital Panopticon renders a fully procedural 360° deep-space background via [`S_ProceduralSpaceSkybox.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_ProceduralSpaceSkybox.shader) and [`M_ProceduralSpaceSkybox.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_ProceduralSpaceSkybox.mat), controlled live in the editor/runtime by [`SpaceSkyboxController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/SpaceSkyboxController.cs) attached to `--- ENVIRONMENT ---` across all scenes:

#### 1. Procedural 3D Direction-Based Skybox Shader
- **Direction-Based 3D Sampling**:
  - Uses normalized world view direction vector `dir = normalize(IN.worldPos)`.
  - Zero UV seams, zero polar pinching, and isotropic star density across the entire celestial sphere.
- **Visual Layers of the Skybox**:
  1. **Deep Space Background Gradient**:
     - Exponential gradient interpolating from `_SpaceZenithColor` (`#03050D`) to `_SpaceHorizonColor` (`#010207`) and `_SpaceNadirColor` (`#000103`) driven by `_GradientExponent` (`1.8`).
  2. **Multi-Tiered Starfield (Sharp + Twinkling)**:
     - 3D cellular hash grid sampling the 8 nearest voxel cells to eliminate boundary grid lines.
     - **Layer 1 (Dense Background Stars)**: High density (`_StarDensity = 280`), fine sharpness (`_StarSharpness = 380`), temperature color variation (`_StarColorVariation = 0.65`) shifting from blue-white O/B class stars to golden K/M class stars, and subtle temporal twinkling (`_StarTwinkleSpeed = 1.4`, `_StarTwinkleAmount = 0.35`).
     - **Layer 2 (Rare Bright Constellation Anchors)**: Lower density (`_BrightStarDensity = 42`), larger scale (`_BrightStarSize = 0.0048`), high HDR brightness (`_BrightStarBrightness = 3.6`), and 4-point cross diffraction spikes.
  3. **Rotatable Milky Way Galactic Band**:
     - Equatorial planar band falloff `exp(-sqr(dirRot.y * width) * falloff)` transformed by 3D rotation matrix `_MilkyWayRotationMatrix` (or Euler angles `_MilkyWayRotationEuler`).
     - Rotatable on all 3 axes (`milkyWayRotationAngles = (25, 40, -15)`) so artists can position the galactic core behind any focal point in the arena.
     - **3D Turbulent Dust Lanes**: Dual-octave 3D Simplex FBM noise carves deep silhouette dust rifts into the galactic plane (`_DustLaneDarkness = 0.82`, `_MilkyWayNoiseScale = 4.2`).
     - **HDR Color & Star Density Boost**: Blends from vibrant core (`_MilkyWayCoreColor` = HDR `#FFD8A8`, intensity 2.4) to outer halo (`_MilkyWayOuterColor` = `#5870B8`, intensity 0.8) and increases star cluster density within the band (`_MilkyWayStarBoost = 2.2`).
  4. **Domain-Warped Cosmic Nebulae Clouds**:
     - Multi-octave 3D FBM noise with domain warping (`warp = FBM(dir + offset)`) generating organic, ethereal gas clouds.
     - Two-color spectral blending between `_NebulaColorA` (deep celestial violet `#7A2A9A`) and `_NebulaColorB` (luminous teal-cyan `#187E8E`), scaled by `_NebulaIntensity = 0.9` and `_NebulaScale = 2.4`.

#### 2. Live Inspector Controller (`SpaceSkyboxController.cs`)
- `[ExecuteAlways]` MonoBehaviour component attached to `--- ENVIRONMENT ---` in `MainMenuScene`, `GameplayScene`, and `CreditsScene`.
- Automatically assigns `M_ProceduralSpaceSkybox.mat` to `RenderSettings.skybox`.
- Exposes intuitive Inspector sliders for all celestial parameters with property ID caching and instant material updates.
- Computes `Matrix4x4.Rotate(Quaternion.Euler(milkyWayRotationAngles))` on parameter changes, providing real-time interactive rotation of the Milky Way band in both Edit Mode and Play Mode.

### 5.8 One-Click Global Typography & Font System (`GlobalFontManager`)

Orbital Panopticon features a centralized, one-click typography system allowing instant previewing, testing, and unified replacement of fonts across all scenes, UI prefabs, and dynamic runtime panels:

#### 1. Configuration & Asset
- **[`GlobalFontConfigSO.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/GlobalFontConfigSO.cs) & [`GlobalFontConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/GlobalFontConfig.asset)**:
  - `masterTMPFont`: Primary TextMeshPro font asset (defaults to `LiberationSans SDF.asset`).
  - `masterLegacyFont`: Optional standard Unity `UnityEngine.UI.Text` fallback font.
  - `globalFontScaleMultiplier`: Relative multiplier (`0.5x` to `2.5x`) applied to base font sizes without cumulative distortion.
  - `globalCharacterSpacing`: Global tracking/spacing offset (`-10` to `+30`).
  - `autoApplyOnValidate`: Automatically pushes changes to the active scene and prefabs when modified in the Inspector.
  - `[ContextMenu("Apply Font To All Scenes & Prefabs")]`: Instant batch trigger from the asset's context menu.

#### 2. Runtime & Editor Manager (`GlobalFontApplier.cs`)
- Attached to `--- FONT MANAGER ---` across `MainMenuScene`, `GameplayScene`, and `CreditsScene`.
- `[ExecuteAlways]` MonoBehaviour singleton with `DontDestroyOnLoad` in play mode.
- Hooks into `SceneManager.sceneLoaded` to automatically unify typography whenever a scene loads.
- **Base Size Protection (`GlobalFontTracker`)**:
  - Automatically attaches a hidden `GlobalFontTracker` component to text elements to record their original design font sizes and character spacings.
  - Prevents cumulative exponential scaling when modifying `globalFontScaleMultiplier` multiple times.
- **Dynamic UI Instantiation Support**:
  - Static helper `GlobalFontApplier.ApplyTo(GameObject root)` allows runtime systems (such as floating score popups or newly instantiated upgrade tree nodes) to apply the current master font immediately upon spawn.

#### 3. Editor Tooling & Batch Utility
- **[`GlobalFontEditorUtility.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Editor/GlobalFontEditorUtility.cs)**:
  - Traverses and updates all UI prefabs under `Assets/_Project/Prefabs` using `PrefabUtility.LoadPrefabContents` and `SaveAsPrefabAsset`.
  - Batch configures all 3 scenes in Build Settings (`MainMenuScene`, `GameplayScene`, `CreditsScene`).
- **[`GlobalFontEditorWindow.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Editor/GlobalFontEditorWindow.cs)**:
  - Dedicated visual typography control center accessible via `Tools -> Orbital Panopticon -> Global Font Manager Window`.
  - Displays live counts of TextMeshPro and Legacy UI Text elements in the active scene.
  - Provides quick action buttons: `"Apply to Active Scene"`, `"Apply to All Prefabs"`, and `"★ Apply To All Scenes & Prefabs (One-Click)"`.

### 5.9 DOTween UI Animations & Skill Tree Viewport Zoom / Panning

When the Upgrade Tree is opened on `TAB`, `Time.timeScale == 0f` (game pauses). All UI DOTween tweens strictly use `.SetUpdate(true)` to ensure snappy execution in unscaled time:

#### 1. Reusable Juicy Button Animator ([`UIButtonTweenAnimator.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UIButtonTweenAnimator.cs))
- Attached automatically to buttons across all scenes (`MainMenuScene`, `GameplayScene`, `CreditsScene`) and all dynamic skill tree node buttons:
  - **On Pointer Enter (Hover)**: `transform.DOScale(originalScale * 1.08f, 0.16f).SetEase(Ease.OutBack).SetUpdate(true)` plus outline/border glow tween.
  - **On Pointer Exit**: `transform.DOScale(originalScale, 0.14f).SetEase(Ease.OutQuad).SetUpdate(true)` and color restoration.
  - **On Pointer Down (Click)**: `transform.DOPunchScale(-Vector3.one * 0.15f, 0.22f, 8, 0.8f).SetUpdate(true)`.
  - **On Affordable Purchase (`PlayPurchaseSuccessAnimation`)**: `transform.DOPunchScale(Vector3.one * 0.25f, 0.32f, 10, 0.8f).SetUpdate(true)`, outline flashes bright green (`#33FF66`), and incoming connecting lines pulse/punch scale (`1.4x`).
  - **On Insufficient Coins (`PlayInsufficientCoinsAnimation`)**: Horizontal anchor shake `rectTransform.DOShakeAnchorPos(0.25f, new Vector2(12f, 0f), 20).SetUpdate(true)` with bright red outline flash (`#FF3333`).

#### 2. Upgrade Tree Open/Close Choreography ([`UpgradeTreeUI.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UpgradeTreeUI.cs))
- **On `TAB` Open**:
  - `CanvasGroup.DOFade(1f, 0.2f).SetUpdate(true)`.
  - **Outward Ripple Sequence**: Skill tree nodes scale up from `Vector3.zero` to full scale using `Ease.OutBack`, staggered outward based on distance from the center hub `(0,0)` (`delay = dist * 0.0006f`).
- **On `TAB` Close**:
  - `CanvasGroup.DOFade(0f, 0.15f).SetUpdate(true)` with map container scaling down to `0.85f` via `Ease.InQuad` before deactivating the panel.

#### 3. Skill Tree Viewport Zoom & Drag Panning ([`SkillTreeViewportController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/SkillTreeViewportController.cs))
- **Container Separation**:
  - `SkillTreeMapContainer` contains all nodes and connecting lines.
  - Fixed HUD overlay (Title header, Currency counter, `[СБРОСИТЬ КАМЕРУ]` button, and `"Купить робота"` button docked at bottom-right) remains stationary on `panelRoot` at all zoom levels.
- **Mouse Scroll-Wheel Zoom (Toward Cursor)**:
  - Supports zoom range from `0.45x` (broad overview) to `2.2x` (close inspection).
  - Calculates local cursor position in viewport to keep the point under the cursor stationary while zooming (`newPos = mouse - (mouse - currentPos) * scaleRatio`).
- **Drag Panning & Camera Reset**:
  - Dragging with LMB/MMB/RMB across the viewport moves `mapContainer.anchoredPosition` within generous clamped bounds (`±900px * zoom`).
  - Pressing `Space` or clicking `[СБРОСИТЬ КАМЕРУ]` smoothly tweens scale back to `1.0x` and position to `(0,0)` over `0.25s` via `Ease.OutCubic`.

### 5.10 High-Juice VFX Overhaul (VFX Graph Bridge, Helical Beams, Plasma Wakes & Singularity Implosion)

Orbital Panopticon features an overhauled AAA-indie VFX pipeline combining Visual Effect Graph (`UnityEngine.VFX`), ACES HDR Bloom intensity, dynamic point light flashes, ribbon trails, and procedural vertex displacement:

#### 1. VFX Graph Integration Bridge ([`VFXGraphBridge.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/VFXGraphBridge.cs))
- **Universal VFX Bridge Component**:
  - Wraps `UnityEngine.VFX.VisualEffect` components, high-density GPU compute graphs, and standard `ParticleSystem` layers into a single API.
  - Automatically reads asset references exposed in [`GameBalanceConfig.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/GameBalanceConfig.cs) (`vfxPullBeamAsset`, `vfxPuntShockwaveAsset`, `vfxSpaghettifyVortexAsset`, `vfxValuableLootAuraAsset`, `vfxDashThrusterAsset`).
  - Feeds dynamic runtime parameters (`"TargetPosition"`, `"Intensity"`, `"Color"`, `"SpawnRate"`, `"Velocity"`) with null-safe fallback handling if `.vfx` assets are omitted.

#### 2. Procedural 3D Lava Loot Overdrive & Ribbon Embers
- **Lava Shader Vertex Displacement ([`S_ValuableLavaLoot.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_ValuableLavaLoot.shader) & [`M_ValuableLavaLoot.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_ValuableLavaLoot.mat))**:
  - Upgraded with organic 3D Object-Space Simplex noise vertex displacement pulsing along vertex normals (`_VertexDisplacementIntensity = 0.08`, `_VertexPulseSpeed = 2.4`).
  - Magma HDR core emission boosted to `8.0–10.0`, triggering intense ACES Bloom flares across fractured basalt crust plates.
- **Ribbon Aura Embers ([`VFX_ValuableLavaEmbers.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_ValuableLavaEmbers.prefab))**:
  - Enabled **Trails Module** in `Ribbon` mode (`lifetime = 0.35s`, tapering width curve `1.0 -> 0.0`, HDR gold-to-crimson gradient) using [`M_ParticleAdditiveGlow.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_ParticleAdditiveGlow.mat).
  - Enabled **Noise Turbulence Module** (`Strength = 1.8`, `Frequency = 1.2`, `ScrollSpeed = 2.0`), transforming simple sparks into swirling, fiery ribbon tendrils orbiting the loot.

#### 3. Gravity Gun Intertwining Helical Beams & Concussive Punt Blast ([`GravityManipulatorController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/GravityManipulatorController.cs))
- **RMB Tractor Beam (Dual Intertwining Helices)**:
  - Procedural dual `LineRenderer` system calculating 28 parametric 3D vertices per curve in real time between the robo-arm muzzle and the targeted scrap.
  - Envelope tapering: `r = maxRadius * sin(t * PI)` ensures the beam pinches to 0 at both the muzzle and the scrap object.
  - Primary beam: HDR Cyan (`new Color(0.2f, 2.5f, 5.0f, 1f)`).
  - Secondary beam: HDR Electric Violet (`new Color(3.5f, 0.4f, 5.0f, 1f)`), rotated 180° out of phase.
  - Jitter noise: Continuous Perlin noise displacement gives an organic, crackling high-voltage plasma appearance.
- **LMB Punt Concussive Blast**:
  - **Dynamic Point Light**: Muzzle flash Point Light (`PuntMuzzleFlashLight`, `Intensity = 8.0f`, `Range = 8.0m`) flashing and decaying exponentially to 0 in `0.12s`.
  - **Ribbon Sparks ([`VFX_GravityPuntBlast.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_GravityPuntBlast.prefab))**: 60 high-velocity sparks (`25–35 m/s`) in a forward cone with Ribbon Trails and turbulence noise.
  - **Camera & Post-Process Punch**: Unscaled DOTween camera shake on `MainCamera` (`DOShakePosition` + `DOShakeRotation`) coupled with [`PostProcessingToolkitController.Instance.TriggerImpactPulse(1.5f, 0.35f, 0.18f)`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/PostProcessingToolkitController.cs).

#### 4. Superheated Spaghettification & Accretion Vortex ([`ScrapItem.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/ScrapItem.cs) & [`BlackHoleGravity.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Physics/BlackHoleGravity.cs))
- **Curved HDR Plasma Wake Trail (`VFX_PlasmaWakeTrail`)**:
  - Procedural `TrailRenderer` instantiated on every pooled debris item.
  - Parameters: `time = 0.45s`, tapering width (`0.35m -> 0.0m`), material [`M_ParticleAdditiveGlow.mat`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/M_ParticleAdditiveGlow.mat).
  - Multi-stop HDR gradient: Head = electric blue-white (`Color(6.0, 7.0, 8.0)`), Mid = fiery orange (`Color(3.5, 1.2, 0.1)`), Tail = deep crimson.
  - Automatically emits in Zone B / Launched state or when crossing the accretion threshold (`depth01 > 0.15f`); cleanly cleared upon return to pool.
- **Singularity Implosion Burst ([`VFX_SingularityImplosion.prefab`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Prefabs/VFX_SingularityImplosion.prefab))**:
  - Instantiated at the singularity center `(0,0,0)` whenever an object crosses the event horizon and completes spaghettification.
  - 45 stretched billboard particles (`lengthScale = 2.8`) with HDR White-Gold gradient (`Color(4.5, 3.8, 1.8)`), particle trails, and negative radial velocity (`-8.0 m/s`) sucked inward into the singularity.
  - Accompanied by a dramatic Post-Processing bloom flash via `TriggerImpactPulse(1.8f, 0.25f, 0.25f)`.

### 5.11 Persistent Visual Config Asset, Fullscreen Lens Overscan & Two-Way Dissolve Transition

#### 1. Persistent Visual Environment Config Asset ([`VisualEnvironmentConfigSO.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/VisualEnvironmentConfigSO.cs))
- **Single Source of Truth**: Persistent ScriptableObject asset [`VisualEnvironmentConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/VisualEnvironmentConfig.asset) storing unified parameters across:
  - **Skybox**: `spaceZenithColor`, `spaceHorizonColor`, `spaceNadirColor`, `starDensity` (`110`), `starSize` (`0.016`), `starBrightness` (`2.2`), `starTwinkleSpeed`, `milkyWayRotationAngles` (`(32, 45, 18)`), `milkyWayWidth` (`0.32`), `nebulaIntensity` (`0.95`), and nebula colors.
  - **VHS Camera Effect**: `masterBlend` (`0.65`), `overscanScale` (`0.90`), `scanlineIntensity` (`0.28`), `trackingBarIntensity` (`0.45`), `rgbShiftAmount` (`0.0035`), `tapeNoiseIntensity` (`0.14`), `screenCurvature` (`0.12`), `verticalJitter` (`0.0018`), and CRT screen tint.
  - **Post-Processing Volume**: `tonemappingMode` (`ACES`), `bloomIntensity` (`1.6`), `bloomThreshold` (`0.85`), `vignetteIntensity` (`0.28`), `chromaticAberrationIntensity` (`0.22`), `lensDistortionIntensity` (`-0.32`), `lensDistortionScale` (`1.15`), `autoCompensateDistortionScale` (`true`), `filmGrainIntensity` (`0.18`), and `motionBlurIntensity` (`0.25`).
- **Zero Accidental Cloning**: Never instantiated with `Instantiate()`. Tweaks in the Inspector during Edit Mode or Play Mode synchronize bidirectionally and mark the asset dirty via `EditorUtility.SetDirty(config)`.
- **Linked Across All Scenes**: `SpaceSkyboxController`, `VHSCameraController`, and `PostProcessingToolkitController` across `MainMenuScene`, `GameplayScene`, and `CreditsScene` link directly to this asset in `OnEnable()` and `Start()`.

#### 2. Fullscreen Lens Overscan & Elimination of Gray Borders
- **VHS CRT Curvature Border Fix ([`S_VHSMonitorEffect.shader`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Materials/S_VHSMonitorEffect.shader))**:
  - Removed old hard cutoff logic `if (isOutside) vhsColor = float3(0.015, 0.015, 0.015);` that generated ugly gray/black border lines around curved screen edges.
  - Added `_OverscanScale` (default `0.90f`): Zooms input UV inward around `(0.5, 0.5)` before CRT curvature distortion (`(uv - 0.5) * _OverscanScale + 0.5`), clamped to `[0.001, 0.999]`. This fills the entire viewport edge-to-edge with rendered space graphics while maintaining authentic CRT tube curvature.
- **URP Lens Distortion Scale Compensation ([`PostProcessingToolkitController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/PostProcessingToolkitController.cs))**:
  - Sets `lensDistortionIntensity = -0.32f` for strong cockpit fisheye curvature.
  - When `autoCompensateDistortionScale = true`, computes `LensDistortion.scale = 1.0f + Mathf.Abs(intensity) * 0.45f` (e.g. `1.15f`), scaling the camera frame outward so barrel-distorted corners never reveal black or empty screen margins.

#### 3. Two-Way Procedural Dissolve Transition ([`SceneTransitionManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/Legacy/SceneTransitionManager.cs))
- **Boot Dissolve-In**:
  - On game launch, the fullscreen overlay starts at `_DissolveProgress = 1.0f` (solid black) and waits **2 settling frames** behind the curtain for cameras, skybox, and shaders to compile and initialize.
  - Smoothly animates `_DissolveProgress` from `1.0` down to `0.0` over `0.85s` (`Time.unscaledDeltaTime`) with audio cue, presenting a polished entry into `MainMenuScene`.
- **Two-Way Scene Transitions**:
  - **Step 1 (Fade Out Current Scene to Black)**: Animates `_DissolveProgress` `0.0 -> 1.0` over `0.85s` (`Time.unscaledDeltaTime`) with FMOD audio cue.
  - **Step 2 (Async Scene Load & 2-Frame Settle)**: Loads scene asynchronously via `SceneManager.LoadSceneAsync(sceneName)`; once complete, waits **2 extra settling frames** (`yield return null; yield return null;`) behind the curtain so the incoming scene's lighting, skybox, and post-processing initialize before the veil lifts.
  - **Step 3 (Fade In New Scene from Black)**: Smoothly animates `_DissolveProgress` `1.0 -> 0.0` over `0.95s` (`Time.unscaledDeltaTime`) with FMOD audio cue.

### 5.12 Physics & VFX Enhancements: Stage 1 Vortex Clamping, Vacuum Muzzle Nozzle, Heavy Dash & Total Finale Consumption

#### 1. Stage 1 Black Hole Slingshot Elimination & Hard Spherical Clamping ([`BlackHoleGravity.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Physics/BlackHoleGravity.cs))
- **Permanent Vortex Lock**: Once a `ScrapItem` enters the accretion zone (`item.hasEnteredVortex == true`), it is permanently locked into Zone B physics (unless grabbed by the player's tractor beam). It can never transition back to `Floating` or trigger boundary escape in `ScrapSimulationManager`.
- **Radius-Proportional Swirl Speed**:
  `float maxSafeTangentialSpeed = Mathf.Min(swirlSpeed, scrap.currentSpiralRadius * 3.8f);`
  Prevents discrete `FixedUpdate` time-step overshoot and eliminates centrifugal slingshot flinging on Stage 1.
- **Hard Spherical Clamping & Outward Velocity Stripping**:
  - `scrap.currentSpiralRadius` shrinks steadily toward `eventHorizonRadius * 0.8f`.
  - If `dist3D > scrap.currentSpiralRadius`, position is clamped directly onto the shrinking sphere:
    `rb.position = center - dirToCenter3D * scrap.currentSpiralRadius;`
  - Any outward radial velocity is stripped:
    `if (Vector3.Dot(rb.linearVelocity, dirToCenter3D) < 0f) rb.linearVelocity -= dirToCenter3D * radialDot;`
  - 100% of thrown scrap reliably spirals inward and gets consumed on Stage 1.

#### 2. Vacuum Nozzle Pull VFX & Muzzle Swirl Ring ([`GravityManipulatorController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/GravityManipulatorController.cs))
- **`MuzzleNozzlePoint` Anchor**: Located at the exact front tip of `RoboArmVisual` in front of the camera (`(0, 0.05, 0.78)`).
- **Vacuum Pull Stream Overhaul (`VFX_GravityPullStream`)**:
  - `Simulation Space = Local` parented to `MuzzleNozzlePoint`.
  - Spawns in front at `z = 2.8m` (`HoldPoint`) with linear Z velocity `-10.0 m/s`, inward radial velocity `-4.5 m/s`, and orbital Z velocity `6.0 rad/s`.
  - Timed with `startLifetime = 0.28s` and tapering size so **100% of particles converge to radius 0 and terminate directly inside `MuzzleNozzlePoint` (`z = 0`)** without ever flying behind the camera.
- **Spinning Vortex Intake Ring (`VFX_VacuumNozzleSwirl`)**:
  - Emits tightly at `z = 0`, rotating at `720°/s` with HDR cyan/violet glow at the weapon nozzle orifice while RMB is held.
- **Continuous Suction Filaments**:
  - Helical dual-stream lines originate at `MuzzleNozzlePoint.position` and stretch directly to `_heldItems[0]` or the pull target, maintaining visual suction feedback while holding scrap.

#### 3. Heavy Physical Shift Dash Feedback ([`DroneController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/DroneController.cs) & [`PostProcessingToolkitController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/PostProcessingToolkitController.cs))
- **Camera Weight & Tilts**:
  - Dashing right: DOTween roll tilt `-5.5°` on Z returning over `0.3s` via `Ease.OutBack`.
  - Dashing left: DOTween roll tilt `+5.5°` on Z returning over `0.3s` via `Ease.OutBack`.
  - Forward/Back dash: `-4°` pitch kick with spring recovery.
- **Weapon Physical Inertia**:
  - Dips `RoboArmVisual` backward and downward (`DOPunchPosition(new Vector3(0, -0.06f, -0.08f), 0.32f, 7, 0.6f)`) simulating heavy mechanical suspension.
- **Visual Impact Pulse**:
  - Calls `PostProcessingToolkitController.Instance.TriggerImpactPulse(bloomBoost: 1.8f, chromaticBoost: 0.45f, lensDistortionBoost: -0.14f, duration: 0.25f, motionBlurBoost: 0.65f)`.

#### 4. Total Scene Consumption in Finale ([`EndgameFinaleController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/EndgameFinaleController.cs))
- When the 4th Containment Lock breaks:
  - Disables AI steering on **ALL active `HelperDroneBot` instances**, attaches kinematic `GravityBody`, and spirals them rapidly into the Black Hole with spaghettification and score popups (`+50,000`).
  - Converts **ALL active `ScrapItem` instances** into kinematic vortex spiral, spaghettifying and consuming all debris.
  - Sucks in and destroys all 4 `OverseerContainmentEye` bosses with massive rumbles and `+999,999` score popups.
  - Leaves the sector completely cleared of entities before initiating the final relativistic player horizon pull.

### 5.13 Lethal Containment Beams & Dynamic Camera Shake Pipeline (Issues #4 & #3)

#### 1. Lethal Containment Beams for Player Drone ([`OverseerContainmentEye.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/OverseerContainmentEye.cs) & [`DroneController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/DroneController.cs))
- **Strict Separation of Entity Interaction**:
  - **Scrap**: Beams remain 100% visual with zero colliders, triggers, or scoring multipliers.
  - **Player Drone**: Active beams (`!isLockBroken && !isConsumedOrCollapsing`) project an impassable energy hazard.
- **3D Line Segment Proximity Hazard Check**:
  - Every frame, each active Eye calculates the shortest 3D distance from `PlayerDrone.transform.position` to the line segment between `PupilPosition` and the surface of the event horizon `blackHoleCenter + dirFromCenterToEye * (eventHorizonRadius * 0.98f)`:
    $$\vec{AB} = B - A, \quad t = \text{clamp01}\left(\frac{(\vec{P} - A) \cdot \vec{AB}}{|\vec{AB}|^2}\right), \quad \text{closestPoint} = A + t \cdot \vec{AB}$$
  - Hazard threshold radius: `2.2m`.
- **Hit Feedback & Knockback**:
  - When `dist < 2.2f` and `drone.BeamHitCooldownTimer <= 0f`:
    - Increments `drone.CurrentBeamHits++` and applies a `1.1s` cooldown timer (preventing multi-hit clipping during a single pass).
    - Applies a gentle push away from the beam: `rb.linearVelocity = rb.linearVelocity * 0.5f + awayDir * cfg.beamKnockbackImpulse` (`beamKnockbackImpulse = 4.5f`, reduced from `18f`).
    - Triggers full sensory shock:
      - `PostProcessingToolkitController.Instance.TriggerImpactPulse(2.2f, 0.65f, -0.18f, 0.35f, 0.7f)`
      - `VHSCameraController.Instance.TriggerGlitchBurst(0.85f, 0.35f)`
      - `CameraShakeController.Instance.TriggerShake(0.45f, 0.28f)`
      - Audio shock one-shot via `FMODAudioManager.Instance.PlayContainmentLockBreak(closestPoint)`.
- **Strict 3-Hit System (No Player HP Bar)**:
  - **Hits 1 and 2**: Displays a center screen warning alert via [`UIManager.ShowCenterWarningAlert`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UIManager.cs) (`"ВНИМАНИЕ: ПОВРЕЖДЕНИЕ ОТ ЛУЧА СДЕРЖИВАНИЯ! [1/3]"` / `"[2/3]"`).
  - **Hit 3**: Calls [`ArenaBoundaryManager.Instance.TriggerSignalLoss("КРИТИЧЕСКАЯ ПЕРЕГРУЗКА ЛУЧОМ — ДРОН УНИЧТОЖЕН", true)`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/ArenaBoundaryManager.cs), destroying the drone, spawning physical wreckage into the Black Hole, resetting `CurrentBeamHits = 0`, and respawning the drone at a safe distance after `1.2s`.

#### 2. Trauma-Based Camera Shake & Stage 4 Ambient Tremor ([`CameraShakeController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/CameraShakeController.cs))
- **Architecture**:
  - Dedicated controller attached to `Main Camera` running strictly in `LateUpdate`.
  - Appends positional (`maxPositionShake = 0.45m`) and rotational (`maxRotationShake = 5.0°`) offsets on top of mouse look, completely eliminating transform snapping, mouse jitter, or aiming conflicts.
- **Stage Transition Shockwave**:
  - Whenever a Stage completes and a Containment Lock shatters in [`BlackHoleStageManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/BlackHoleStageManager.cs), calls `CameraShakeController.Instance.TriggerStageTransitionShake(duration: 1.4f, magnitude: 0.35f)` with decaying quadratic trauma (`trauma * trauma`).
- **Stage 4 Continuous Psychological Ambient Tremor**:
  - While `BlackHoleStageManager.Instance.CurrentStage == 4`, automatically blends in continuous low-frequency 3D Perlin noise tremor:
    - `ambientTremorPositionAmplitude = 0.025m`
    - `ambientTremorAngleAmplitude = 0.35°`
    - `ambientTremorFrequency = 9.0Hz`
  - Generates an eerie, immersive rumble building suspense before the critical mass finale without impeding player aim or navigation.

### 5.14 Minimalist In-Game HUD, Star Wars Crawl Top-Center Pivot & Full Dual-Language RU/EN Localization (Issues #11, #2, #12)

#### 1. Complete Minimalist HUD Overhaul ([`UIManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UIManager.cs))
- **Permanent Clutter Removal**:
  - Permanently removed legacy prototyping clutter: `SurveillanceMonitorFrame`, `Text_REC_Status`, `Panel_QuotaAndHeat`, `Text_BossTracker`, `Panel_CriticalMassWarning`, and `Bar_Battery`.
  - Preserved `Panel_UpgradeTree` (TAB) and `Panel_NarrativeComms` (Star Fox comms) without alteration.
- **Strictly 3 Core Gameplay Metrics**:
  1. **Scrap Coins Counter (Top-Right)**: Sleek glass badge `Panel_ScrapCoins` (anchored top-right at `-40, -36`) displaying `"ЛОМ-МОНЕТЫ: 1,250"` (or `"SCRAP COINS: 1,250"` in EN). Punches scale via `DOTween` whenever scrap coins increase.
  2. **Black Hole Mass Progress Bar (Bottom-Center)**: Minimalist `520x22` horizontal bar `Bar_BlackHole` (anchored bottom-center at `0, 32`) displaying `"МАССА ДЫРЫ: 420 / 500 ТОНН"` (or `"BLACK HOLE MASS: 420 / 500 TONS"` in EN) with dynamic cyan/blue fill.
  3. **Stage Indicator (Directly above Mass Bar)**: Clean header `Text_StageHeader` (anchored bottom-center at `0, 6`) displaying `"СТАДИЯ 1 / 4"` (or `"STAGE 1 / 4"` in EN), shifting to a dramatic pulsing neon-red warning on Stage 4: `"СТАДИЯ 4 / 4 — КРИТИЧЕСКАЯ МАССА"` (`"STAGE 4 / 4 — CRITICAL MASS"`).
- **Retained Overlays**: Center aiming crosshair dot, Star Fox radio comms (`NarrativeCommsUI`), transient warnings (Boundary warning, Beam alert, Start overlay, Signal lost overlay, Quota celebration, Observed scrap).

#### 2. Star Wars Crawl Top-Center Pivot Fix ([`StarWarsCrawlController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/StarWarsCrawlController.cs))
- **Top-Center Pivot Enforcement**:
  - `CreditsTextContainer` RectTransform pivot is strictly enforced to `(0.5f, 1.0f)` (top-center pivot).
  - `initialYOffset = -280f` guarantees that line 1 (`"ЭПИЗОД #042: ЗА ГОРИЗОНТОМ СОБЫТИЙ"` / `"EPISODE #042: BEYOND THE EVENT HORIZON"`) begins below the screen edge and enters cleanly from the bottom in proper reading order.
  - `endYThreshold = 3400f` and `crawlSpeed = 38f` (with 3x speedup on Space/LMB) ensures the entire credits roll completes seamlessly.
  - Credits role headers follow standard industry practice in English (`Lead Programmer`, `Game Designers`, `3D Artists`, `2D Artist`, `Sound Designer (FMOD)`), while narrative intro, synopsis, and thank-you footer are localized.

#### 3. Full Russian Default Localization & Main Menu Flag Switcher ([`LocalizationManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/LocalizationManager.cs))
- **Architecture**:
  - `LocalizationManager` persistent singleton (`DontDestroyOnLoad`) with PlayerPrefs persistence (`OrbitalPanopticon_Language`), defaulting to Russian (`GameLanguage.RU`).
  - `LocalizedText.cs` component automatically updates attached `TextMeshProUGUI` whenever language changes.
  - Dual-language support across all dialogues (`NarrativeDialogueConfigSO`), Upgrade Tree nodes (`UpgradeTreeManager`), Main Menu labels, HUD metrics, and Credits crawl.
- **Main Menu Language Switcher**:
  - `LanguageSwitcherPanel` anchored bottom-right `(-40, 40)` in `MainMenuScene` with `Btn_Lang_RU` (`[RU]`) and `Btn_Lang_EN` (`[EN]`).
  - Active button highlighted with an accent outline and DOTween punch scale on click.
  - Instant live update of all scene text upon switching without scene reloading.
- **Unified Font Rendering**:
  - `GlobalFontApplier.ApplyToAll()` ensures all localized Cyrillic and Latin characters render cleanly using the master font assets.

### 5.15 Black Hole Depth Sorting, Ballistic Scrap Coasting & Smooth Vortex Capture (Issues #1, #2, #8)

#### 1. Black Hole Foreground Depth Rejection & Event Horizon Opaque Core (Issue #1)
- **Problem Solved**: `S_OuterWildsBlackHole.shader` previously rendered over foreground elements (held scrap, `RoboArmVisual`), and containment beams pierced through the singularity core.
- **True Depth Rejection**:
  - `S_OuterWildsBlackHole.shader` queue set to `"Transparent-100"` (`renderQueue = 2900`).
  - Frag pass samples `_CameraDepthTexture` via `SampleSceneDepth(screenUV)` and converts to linear eye depth.
  - If `sceneLinearDepth < bhCenterDepth - coreRadiusWorld * 1.05f`, fragment immediately discards. Foreground objects are never covered by black hole lensing or core.
- **Event Horizon Opaque Core**:
  - `EventHorizonOpaqueCore` sphere inside `BlackHoleSystem/VisualRoot` with dedicated pure black shader `S_EventHorizonOpaqueCore.shader` (`Queue = Geometry / 2000`, `ZWrite On`, `ZTest LEqual`, `#000000`).
  - Scaled dynamically by `BlackHoleStageManager` to match `eventHorizonRadius * 2f`.
  - Terminates all 4 Overseer `ContainmentBeam` endpoints at `blackHoleCenter + dirFromCenterToEye * (eventHorizonRadius * 0.98f)`. Beams terminate cleanly on the event horizon surface without piercing into the core.

#### 2. Ballistic Scrap Coasting & Smooth Vortex Capture (Issue #2)
- **Problem Solved**: Thrown scrap outside the accretion threshold was previously captured into Zone B and redirected into the black hole; crossing the threshold previously snapped velocity and position abruptly.
- **Ballistic Coasting Outside Threshold (`dist3D > accretionThresholdRadius`)**:
  - Scrap in `ScrapState.Launched` outside threshold flies purely on its ballistic trajectory with gentle natural drag (`linearDamping = 0.25f`).
  - When velocity drops below `outerZoneDriftSpeed * 1.3f`, smoothly transitions back to `ScrapState.Floating` at its new orbital radius (`homeOrbitRadius = dist3D`), restoring calm circular drift without reverse course snapping.
- **Smooth Vortex Capture & Momentum Blending (`dist3D <= accretionThresholdRadius`)**:
  - Entry tracks `timeInVortex += Time.fixedDeltaTime`.
  - Calculates smooth blend weight `entryBlend = Mathf.SmoothStep(0f, 1f, timeInVortex / 0.85f)`.
  - Smoothly blends from incoming velocity to spiral orbital velocity over `0.85s`.
  - Removes hard position clamping on threshold entry. Hard radial clamping is strictly reserved for deep accretion (`depth01 > 0.65f`) near the Event Horizon, completely eliminating initial trajectory jerk.

#### 3. Gentle Containment Beam Knockback (Issue #8)
- Configured `beamKnockbackImpulse = 4.5f` in `GameBalanceConfig` (reduced from `18f`).
- Applied soft velocity damping: `rb.linearVelocity = rb.linearVelocity * 0.5f + awayDir * config.beamKnockbackImpulse`.
- Touching a beam now delivers a tactile nudge instead of violently flinging the player across the arena.

### 5.14 UI Polish, Minimalist Camera Viewfinder, Large Flag Switchers & In-Game ESC Pause Menu

#### 1. Elimination of Stuck Red UI Panels (Issue #6)
- **Problem**: Blank red rectangle alert overlay remained stuck in `GameplayScene` when warnings had expired or lacked text.
- **Solution**:
  - `UIManager.Awake()` and `Start()` explicitly initialize all alert and warning panels (`WarningPanel`, `SignalLostOverlay`, `Panel_ObservedNotice`, `Panel_QuotaCelebration`, `Panel_NarrativeComms`, `Panel_BeamDamageAlert`) to `SetActive(false)` and `alpha = 0f`.
  - `UpdateWarningHUD()` automatically deactivates `warningPanel` and resets `warningBorderImage.color.a = 0f` immediately whenever `_warningRemainingSeconds <= 0f`.
  - Removed all orphan legacy red panels (`DamageFlashOverlay`, `BoundaryWarningPanel`, `StageWarningBanner`) from `GameplayScene.unity`.

#### 2. Minimalist Camera Viewfinder Overlay (`● REC` & Framing Brackets) (Issue #4)
- **Visual Presentation**:
  - Four delicate corner brackets (`┌  ┐`, `└  ┘`) framing the camera feed (`alpha = 0.35`, cyan `#59E6FF`).
  - Top-left recording status badge: pulsing red circular indicator dot (`●`) paired with bold `"REC"` text label.
  - Pulsing red dot uses `DOTween.DOFade(0.15f, 0.65f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true)` to pulse continuously in unscaled time during pauses or tree viewing.
  - All viewfinder elements have `raycastTarget = false` to prevent mouse hit blockage.
- **Minimalist Gameplay HUD**:
  - In-game HUD strictly limited to 3 essential metrics:
    1. **Scrap Coins Counter** (Top-Right): Sleek badge with scale pop animation on coin pickup.
    2. **Black Hole Mass Bar & Stage** (Bottom-Center): Dynamic stage-colored bar with target mass and stage number indicator.
    3. **Center Crosshair** (Center): Dynamic ticks indicating tractor beam lock states.
    4. **Camera Viewfinder Overlay** (Perimeter): Corner brackets and `● REC` indicator.

#### 3. Large Flag Buttons in Main Menu (Issue #3)
- **Hierarchy & Visuals**:
  - In `MainMenuScene.unity`, replaced small textual language toggle buttons with two large flag buttons (`Btn_Flag_RU` and `Btn_Flag_EN`).
  - Button dimensions: `110x74` px, positioned in the bottom-right corner (`anchoredPosition = new Vector2(-40, 40)`), spaced `20` px apart.
  - Full-size child `Image_Flag` component (`preserveAspect = true`, `color = Color.white`) displaying high-resolution procedural flag textures [`Flag_RU.png`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Textures/UI/Flag_RU.png) and [`Flag_EN.png`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Textures/UI/Flag_EN.png).
  - Selected language displays a vivid cyan outline border (`#33D9FF`, `thickness = 3px`) and scale emphasis (`1.05x`), while inactive language displays a subtle muted outline (`#4D5973`, `thickness = 1.5px`) and scaled down (`0.95x`).
  - Integrated with `UIButtonTweenAnimator` for punch-scale juice on click and hover.

#### 4. In-Game ESC Pause Menu & System Settings (Issue #7)
- **Architecture ([`PauseMenuController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/PauseMenuController.cs))**:
  - Centrally manages game pausing via `ESC` key while prioritizing gameplay overlays:
    - **`ESC` Priority**: If `TAB` Upgrade Tree is open, pressing `ESC` closes the Upgrade Tree first; otherwise, toggles the Pause Menu.
    - **Race-Condition Safety**: `UpgradeTreeManager.LastClosedFrame` tracks when the tree closes, preventing the Pause Menu from opening in the same frame that the Upgrade Tree was closed.
  - When paused:
    - Sets `Time.timeScale = 0f` and unlocks cursor (`CursorLockMode.None`, `Cursor.visible = true`).
    - Blocks input to both [`DroneController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/DroneController.cs) and [`GravityManipulatorController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/GravityManipulatorController.cs).
  - **Menu Buttons**:
    1. **`[ПРОДОЛЖИТЬ] / [RESUME]`**: Unpauses the game, restores `Time.timeScale = 1f`, relocks cursor.
    2. **`[НАСТРОЙКИ] / [SETTINGS]`**: Opens system settings sub-panel (`Panel_SettingsSub`) with Master Volume and Mouse Sensitivity sliders.
    3. **`[В ГЛАВНОЕ МЕНЮ] / [MAIN MENU]`**: Transitions smoothly to `MainMenuScene` using `SceneTransitionManager`.
    4. **`[ВЫЙТИ ИЗ ИГРЫ] / [QUIT GAME]`**: Exits application (`Application.Quit()` / stops play mode in editor).
  - All UI buttons use `UIButtonTweenAnimator` with `.SetUpdate(true)` to provide hover glow and punch feedback in unscaled time.
  - Real-time localization support dynamically refreshes all Pause Menu labels when language changes.

### 5.15 Finale Event Horizon Camera Shutdown (CRT TV Turn-Off + Static)

#### 1. CRT Camera Shutdown Overlay ([`CameraShutdownEffect.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/CameraShutdownEffect.cs))
- Located at the highest sorting order in `HUD_Canvas` (`CameraShutdownOverlay` is set to `transform.SetAsLastSibling()`).
- **Composed of 3 Layers**:
  1. `BlackoutCurtain`: Fullscreen pitch black (`#000000`) background covering the entire screen.
  2. `StaticBurstImage`: Fullscreen high-contrast white noise / analog static overlay using a 256x256 procedural noise sprite, dynamically jittering with violent positional offsets during trigger.
  3. `CRTCollapseLine`: Glowing white/cyan phosphorus flare line (`new Color(0.92f, 0.98f, 1f, 1f)`) with electric cyan outline (`#33D9FF`), anchored across the horizontal center of the screen (`1080px` height initially).

#### 2. Finale Player Pull & Horizon Crossing Cutoff ([`EndgameFinaleController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Gameplay/EndgameFinaleController.cs))
- **Step 1: Relativistic Approach & VHS Glitch Ramp**:
  - As the player drone is pulled toward singularity `(0,0,0)`, [`VHSCameraController.Instance.SetFinaleInterference(t)`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/VHSCameraController.cs) progressively ramps tape noise, tracking bar glitch, horizontal tearing, jitter, and chromatic aberration from `0.0 -> 1.0`.
  - Camera FOV expands smoothly from `60°` to `115°` with exponential camera shudder.
- **Step 2: Instant Camera Cutoff (`dist <= eventHorizonRadius * 1.15f`)**:
  - The exact moment the event horizon threshold is crossed:
    1. Immediately deactivates `RoboArmVisual` (`SetActive(false)`).
    2. Hides all HUD elements via [`UIManager.Instance.HideAllHUD()`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UIManager.cs) and disables finale banners.
    3. Stops rendering the 3D world (`Camera.main.cullingMask = 0`, `clearFlags = SolidColor`, `backgroundColor = Color.black`).
    4. Mutes/stops all ongoing gameplay loops and background music via [`FMODAudioManager.Instance.StopAllGameplayLoops()`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/FMODAudioManager.cs) and [`MusicPlaylistManager.Instance.StopAllMusicImmediate()`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/MusicPlaylistManager.cs).
    5. Executes **CRT TV Turn-Off Sequence** with unscaled DOTween:
       - **Phase 1**: Flashes `StaticBurstImage` at `100%` intensity for `0.18s` with rapid violent screen jitter.
       - **Phase 2**: Enables solid `BlackoutCurtain` and squashes `CRTCollapseLine` vertically (`DOScaleY(0.008f, 0.16f).SetEase(Ease.InExpo)`) into a razor-thin luminous line across the middle of the screen.
       - **Phase 3**: Collapses the line's width into a bright center dot (`DOScaleX(0.0f, 0.18f).SetEase(Ease.InQuad)`) while fading out image alpha to 0.
- **Step 3: Dramatic Silence Hold**:
  - Holds in total pitch-black silence for **`1.8s`** (`WaitForSecondsRealtime(1.8f)`).
- **Step 4: Transition to Credits Crawl**:
  - Calls `SceneTransitionManager.Instance.TransitionFromBlackScreenToScene("CreditsScene")`, which skips redundant fade-in on the black screen and executes only the fiery dissolve fade-out into the Star Wars style Credits crawl.

### 5.16 Automated 3D Asset, PBR Material & Animation Pipeline (`AssetAutoIntegrationPipeline`)

Located at [`Assets/_Project/Scripts/Editor/AssetAutoIntegrationPipeline.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/Scripts/Editor/AssetAutoIntegrationPipeline.cs), this automated pipeline scans incoming art assets and wires them directly into gameplay prefabs, scenes, materials, and animator controllers.

- **Menu Item**: `Tools/Orbital Panopticon/Auto-Bind 3D Assets & Materials`.
- **Directory Structure & Auto-Organization**:
  - Automatically sorts incoming raw models and texture folders into structured subdirectories:
    - `Assets/_Project/Art/Models/RoboArm/` (`MainGun_animated.fbx`, `MainGun_textures`)
    - `Assets/_Project/Art/Models/HelperBot/` (`robot_low.fbx`, `robot_textures`)
    - `Assets/_Project/Art/Models/OverseerEye/` (`eyeball.fbx`, `eyeball_textures`)
    - `Assets/_Project/Art/Models/Scrap/Tier1..Tier4/` (`ariral.fbx`, `ariral_textures` bound to `Scrap/Tier4/`)
- **Direct Roughness PBR Material Generation**:
  - Automatically generates materials in `Assets/_Project/Materials/Generated/` (`M_RoboArm.mat`, `M_HelperBot.mat`, `M_OverseerEye.mat`, `M_Tier4.mat`).
  - Utilizes the `Autodesk Interactive` shader (or URP Lit fallback) which natively supports direct **Roughness Maps** in `_SpecGlossMap` without requiring manual texture inversion or RGB channel packing.
  - Automatically flags normal maps as `TextureImporterType.NormalMap`, assigns `_BumpMap`, and enables HDR emissive emission (`[HDR] Color.white * 2.5f`).
- **Robo-Arm LMB/RMB Animation Controller Setup**:
  - Programmatically generates `Assets/_Project/Art/Models/RoboArm/AC_RoboArm.controller` with:
    - Parameters: `bool IsPullingRMB`, `Trigger FirePuntLMB`.
    - States: `Idle` (default pose), `PullHold_RMB` (driven by `Mandibles_base_open` clip when `IsPullingRMB == true`), and `Punt_LMB` (violent muzzle snap and recoil on `FirePuntLMB` trigger).
  - Automatically attaches [`RoboArmAnimatorHook`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/RoboArmAnimatorHook.cs) to `PlayerDrone/MainCamera/RoboArmVisual`, synchronizing player input in [`GravityManipulatorController`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Player/GravityManipulatorController.cs) with arm animations.
  - Automatically aligns `MuzzleNozzlePoint` at the front tip of the arm model's bounding box.
- **Prefab & VisualRoot Replacement**:
  - **Helper Drone Bot**: Automatically instantiates `robot_low.fbx` under `HelperDroneBot.prefab`'s `VisualRoot`, disables primitive placeholder meshes, strips visual colliders, and normalizes scale to `~1.8` units.
  - **Overseer Containment Eye**: Replaces placeholder pupil spheres across all 4 `OverseerContainmentEye` instances in `GameplayScene.unity` with the high-poly eyeball model and generated material.
  - **Scrap Prefabs (`Tier1..Tier4`)**: Replaces visual geometry in `Scrap_Tier1_Bolt.prefab` through `Scrap_Tier4_Anomaly.prefab`, while preserving the root rigidbody and colliders, and updating the cached `mainRenderer` on `ScrapItem` for dynamic dissolve and valuable lava glow highlights.

### 5.15 Rebranding to "Angular Momentum" & Multi-Slide Tutorial Window

#### 1. Global Rebranding & Subtitle Clutter Elimination
- **Title Updates**: Replaced `"ORBITAL PANOPTICON"` with **`"ANGULAR MOMENTUM"`** across `MainMenuScene.unity`, `GameplayScene.unity`, `CreditsScene.unity`, and [`LocalizationManager.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/LocalizationManager.cs).
- **Crawl & Narrative Updates**: In [`StarWarsCrawlController.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/UI/StarWarsCrawlController.cs), credits line `"Panopticon Systems Architecture"` updated to `"Angular Momentum Systems Architecture"`.
- **Clutter Elimination**: Subtitle labels displaying legacy strings like `"SURVEILLANCE SECTOR #042"` or `"CORPORATE SURVEILLANCE SECTOR 7-G // DRONE #001"` have been deactivated and cleared from `MainMenuScene` and `GameplayScene`.

#### 2. Multi-Slide Tutorial Modal Window ([`TutorialSlideshowUI.cs`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/TutorialSlideshowUI.cs))
- **Decommission of Single-Click Start Banner**: Replaced the legacy `Panel_OperatorStart` (`[ CLICK LMB TO CONNECT TO DRONE #042 ]`) in `GameplayScene.unity` with `TutorialWindowPanel`.
- **Simulation Pause & Cursor Rules**:
  - Automatically displayed on `GameplayScene` start with `IsTutorialOpen = true`.
  - Sets `Time.timeScale = 0f` (pausing game physics and simulation) and unlocks cursor (`Cursor.lockState = CursorLockMode.None`, `Cursor.visible = true`).
  - Fullscreen dark backdrop (`Color(0.02, 0.04, 0.08, 0.88)`, `raycastTarget = true`) intercepts clicks to guarantee background mouse clicks cannot dismiss the window.
- **Clean Responsive Layout**:
  - **Top Header**: Title `"ANGULAR MOMENTUM"` in pixel sci-fi typography.
  - **Visual Frame**: `560 x 300 px` container with cyber border, wired to `Sprite[] slideIllustrations` array in Inspector with a stylish placeholder container when unassigned.
  - **Localized Slide Text Area**: Supports real-time RU/EN switching via `LocalizationManager`:
    - *Slide 1/4 (Flight & Momentum)*: WASD thrusters, Space/Ctrl ascend/descend, Shift directional impulse dash.
    - *Slide 2/4 (Gravity Gun & Singularity)*: Hold RMB to vacuum debris, LMB to launch into Black Hole; valuable magma debris bonus.
    - *Slide 3/4 (Overseer Eyes & Stages)*: 4 containment beams holding Black Hole; 3 beam hits destroy drone.
    - *Slide 4/4 (Upgrade Tree & Automation)*: Press TAB to pause, upgrade specs, and purchase autonomous helper bots.
  - **Bottom Navigation Bar**:
    - `[ < НАЗАД ]` (`PreviousSlide()`, hidden on Slide 1).
    - Center page counter (`"1 / 4"` .. `"4 / 4"`).
    - `[ ДАЛЕЕ > ]` (`NextSlide()`, visible on Slides 1–3).
    - `[ НАЧАТЬ ИГРУ ]` (`StartGameFromTutorial()`, visible on Slide 4 with green/gold accent styling).
- **Game Start Transition**:
  - Closes modal with unscaled DOTween fade (`0.35s`), restores `Time.timeScale = 1f`, locks cursor (`Cursor.lockState = CursorLockMode.Locked`), and automatically triggers [`UIManager.Instance.ConnectOperator()`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/UIManager.cs) to initialize drone flight.

---

## 6. Sound Designer Guide: FMOD Studio 2.03.08

Orbital Panopticon natively integrates **FMOD Studio 2.03.08** via [`FMODAudioManager`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/FMODAudioManager.cs) and [`FMODAudioConfigSO`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/Scripts/Core/FMODAudioConfigSO.cs).

All FMOD event references and parameter names are configured in:  
[`Assets/_Project/ScriptableObjects/FMODAudioConfig.asset`](file:///D:/GameDev/UnityGames/AngularMomentum/Assets/_Project/ScriptableObjects/FMODAudioConfig.asset)

### 6.1 FMOD Event Reference Matrix (Music Playlists & 18 Sound Events)

| Category | Field Name | Type | Description | Trigger Hookup |
| :--- | :--- | :--- | :--- | :--- |
| **0. Background Music** | `mainMenuMusicPlaylist` | Playlist (2D) | Sequential music tracks for the Main Menu | Played automatically in `MainMenuScene` via `MusicPlaylistManager` |
| | `gameplayMusicPlaylist` | Playlist (2D) | Sequential music tracks for active Gameplay | Played automatically in `GameplayScene` via `MusicPlaylistManager` |
| | `creditsMusicPlaylist` | Playlist (2D) | Sequential music tracks for the Credits scene | Played automatically in `CreditsScene` via `MusicPlaylistManager` |
| **1. Black Hole & Ambience** | `spaceAmbienceLoop` | Loop (2D) | Ambient background cosmic drone loop | Started automatically on scene load |
| | `singularityHumLoop` | Loop (3D) | Deep gravitational hum at Black Hole center | Started on scene load, positioned at `BlackHoleSystem` |
| | `scrapSpaghettifyOneShot` | One-Shot (3D) | Gravitational tearing/implosion cue | Played on `BlackHoleGravity.OnObjectConsumed` (standard scrap) |
| | `criticalMassAlarmLoop` | Loop (2D) | Tense siren warning of Stage 4 critical mass | Starts when Black Hole enters Stage 4 |
| | `collapseGameOverOneShot` | One-Shot (3D) | Catastrophic collapse game over impact | Played on catastrophic implosion |
| **2. Containment & Comms** | `containmentBeamHumLoop` | Loop (3D) | Ambient hum of active Eye beams (scales with active beams) | Positioned at Black Hole center; volume modulated by active beams count |
| | `containmentLockBreakOneShot` | One-Shot (3D) | Electrical overload explosion when an Eye beam turns off | Played in `OverseerContainmentEye.BreakContainmentLock()` |
| | `commsOpenBeepOneShot` | One-Shot (2D) | Star Fox radio transmission chirp on slide-in | Played in `NarrativeCommsUI.PlayTransmission()` |
| | `commsVoiceBlipOneShot` | One-Shot (2D) | Typewriter chatter audio blip per character | Played in `NarrativeCommsUI.PlayTypewriterChirp()` |
| | `finaleEyesConsumedRumbleOneShot` | One-Shot (3D) | Ominous deep rumble as Eyes spiral into singularity | Played during finale eye suction in `EndgameFinaleController` |
| | `playerHorizonWarpOneShot` | One-Shot (3D) | Relativistic audio warp as player is pulled into singularity | Played at start of player horizon pull in `EndgameFinaleController` |
| | `dissolveTransitionOneShot` | One-Shot (2D) | Subtle transition noise when black dissolve curtain closes/opens | Played in `SceneTransitionManager.TransitionRoutine()` |
| **3. Player Drone & Manipulator** | `thrusterLoop` | Loop (3D) | Engine thruster loop modulated by velocity | Started on scene load, positioned at `PlayerDrone` |
| | `shiftDashOneShot` | One-Shot (3D) | High-energy dash whoosh | Played on `Left Shift` in `DroneController` |
| | `gravityGunHoldLoop` | Loop (3D) | Magnetic tractor beam loop | Active while holding scrap (`GravityManipulatorController.IsHolding`) |
| | `gravityGunPuntOneShot` | One-Shot (3D) | Kinetic punch/shockwave sound | Played on LMB punt in `GravityManipulatorController` |
| | `boundaryWarningBeep` | One-Shot / Cue | Boundary proximity alert | Played when crossing 42m safe radius |
| | `signalLostRespawnOneShot` | One-Shot (3D) | Telemetry loss & respawn cue | Played on boundary signal loss |
| **4. Economy & UI** | `scorePopupOneShot` | One-Shot (2D) | Score chime on scrap delivery | Played on quota addition in `QuotaAndScoreManager` |
| | `valuableScrapConsumeOneShot` | One-Shot (3D) | Resonant golden chime for valuable scrap | Played when valuable glowing scrap is consumed |
| | `terminalToggleOneShot` | One-Shot (2D) | Cockpit terminal toggle sound | Played when opening/closing Upgrade Tree (`TAB`) |
| | `upgradeBuyOneShot` | One-Shot (2D) | Upgrade confirmation sound | Played on purchasing nodes in Upgrade Tree |

### 6.2 Global Parameter Automation

`FMODAudioManager` updates the following parameters globally and on all active instances every frame:

| Parameter Name | Range | Driving Game Metric | Description |
| :--- | :--- | :--- | :--- |
| **`BlackHoleStage`** | `1.0` – `4.0` | `BlackHoleStageManager.Instance.CurrentStage` | Drives musical tension and singularity distortion across the 4 growth stages. |
| **`DistanceToHole`** | `0.0` – `1.0` | `(dist - eventHorizon) / (arenaMax - eventHorizon)` | `0.0` = at Event Horizon, `1.0` = safe outer perimeter. Modulates low-pass filters and gravitational bass. |
| **`DroneSpeed`** | `0.0` – `1.0` | `playerRigidbody.linearVelocity.magnitude / maxThrusterSpeed` | `0.0` = stationary, `1.0` = maximum speed. Drives thruster pitch and engine volume. |
| **`ActiveContainmentBeams`** | `4.0` – `0.0` | `BlackHoleStageManager.Instance.ActiveContainmentLocksCount` | Modulates the intensity and sub-harmonics of `containmentBeamHumLoop` as locks break. |
| **`ComboLevel`** | `0.0`+ | `QuotaAndScoreManager.Instance.CompletedQuotasCount` | Quota milestone level for dynamic musical intensity scaling. |
