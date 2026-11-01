using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FMODUnity;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Player;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class OverseerContainmentEye : MonoBehaviour
    {
        [Header("Eye Identity")]
        [SerializeField] private int eyeIndex = 0;
        [SerializeField] private string eyeName = "CONTAINMENT OVERSEER EYE";

        [Header("Target & References")]
        [SerializeField] private Transform targetDrone;
        [SerializeField] private Transform centerReference;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Light pupilLight;
        [SerializeField] private Renderer pupilRenderer;

        [Header("Containment Beam (100% Visual Only)")]
        [SerializeField] private LineRenderer containmentBeam;
        [SerializeField] private Color beamColorCore = new Color(0.15f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color beamColorGlow = new Color(0.05f, 0.7f, 1f, 0.75f);
        [SerializeField] private float beamStartWidth = 0.55f;
        [SerializeField] private float beamEndWidth = 0.2f;

        [Header("Lock Status")]
        [SerializeField] private bool isLockBroken = false;
        public bool isConsumedOrCollapsing = false;

        [Header("Overloaded Broken Eye Red Laser")]
        [SerializeField] private LineRenderer redSweepingLaserBeam;
        [SerializeField] private Material redLaserMaterial;
        [SerializeField] private BeamSplineAudioEmitter beamAudioEmitter;
        [Tooltip("If true, Overseer_BeamAttack plays at telegraph start (t=0); if false, plays at active beam sweep start.")]
        [SerializeField] private bool triggerBeamAttackSoundAtTelegraph = false;
        private float _redLaserTimer = 0f;
        private Coroutine _redLaserSweepCoroutine;
        private bool _isSweepingLaserActive = false;

        [Header("Idle Audio Settings")]
        [Tooltip("Periodicity slider in seconds between Overseer Idle sounds across active eyes. Default: 10s (rare).")]
        [Range(2f, 60f)]
        [SerializeField] private float idleSoundInterval = 10.0f;

        [Tooltip("Random variance (+/- seconds) applied to idle interval so vocalizations sound organic.")]
        [Range(0f, 10f)]
        [SerializeField] private float idleSoundJitter = 2.5f;

        /// <summary>
        /// Static delegate invoked when any active Overseer Containment Eye emits an idle vocalization cue.
        /// Passes emitting eye instance and 3D world position of pupil.
        /// </summary>
        public static event Action<OverseerContainmentEye, Vector3> OnOverseerIdleEmitted;

        /// <summary>
        /// Instance delegate invoked when this specific Overseer Containment Eye emits an idle vocalization cue.
        /// Passes 3D world position of pupil.
        /// </summary>
        public event Action<Vector3> OnIdleEmitted;

        /// <summary>
        /// Delegate invoked when player is struck or damaged by an active containment beam.
        /// Passes player drone and world collision point.
        /// </summary>
        public static event Action<DroneController, Vector3> OnPlayerBeamShock;

        public float IdleSoundInterval
        {
            get => idleSoundInterval;
            set => idleSoundInterval = Mathf.Clamp(value, 2f, 60f);
        }

        public float IdleSoundJitter
        {
            get => idleSoundJitter;
            set => idleSoundJitter = Mathf.Clamp(value, 0f, 10f);
        }

        private static readonly List<OverseerContainmentEye> _activeEyes = new List<OverseerContainmentEye>();
        private static Coroutine _globalIdleCoroutine;
        private static OverseerContainmentEye _coroutineRunner;

        private float _orbitAngle = 0f;
        private MaterialPropertyBlock _propBlock;
        private Renderer[] _renderers;
        private Coroutine _overloadFlashCoroutine;

        public int EyeIndex => eyeIndex;
        public string EyeName => eyeName;
        public bool IsLockBroken => isLockBroken;
        public bool IsActiveLock => !isLockBroken;

        public Vector3 PupilPosition
        {
            get
            {
                if (pupilLight != null) return pupilLight.transform.position;
                if (pupilRenderer != null) return pupilRenderer.transform.position;
                return transform.position + (transform.forward * 4.2f);
            }
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
                Destroy(obj);
#else
            Destroy(obj);
#endif
        }

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            if (visualRoot == null) visualRoot = transform;

            // Hit detection colliders and HP bars must be completely removed
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                SafeDestroy(col);
            }

            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                SafeDestroy(canvas.gameObject);
            }

            if (pupilLight == null) pupilLight = GetComponentInChildren<Light>(true);
            if (pupilRenderer == null && visualRoot != null)
            {
                Transform pupilTransform = visualRoot.Find("Eye_Pupil");
                if (pupilTransform != null) pupilRenderer = pupilTransform.GetComponent<Renderer>();
            }

            _propBlock = new MaterialPropertyBlock();
            _renderers = visualRoot.GetComponentsInChildren<Renderer>(true);

            SetupContainmentBeam();
            SetupRedSweepingLaser();
        }

        private void OnEnable()
        {
            _activeEyes.RemoveAll(e => e == null);
            if (!_activeEyes.Contains(this))
            {
                _activeEyes.Add(this);
            }
            EnsureIdleCoordinatorRunning();
        }

        private void OnDisable()
        {
            if (_redLaserSweepCoroutine != null)
            {
                StopCoroutine(_redLaserSweepCoroutine);
                _redLaserSweepCoroutine = null;
            }
            if (redSweepingLaserBeam != null)
            {
                redSweepingLaserBeam.enabled = false;
            }
            if (beamAudioEmitter != null)
            {
                beamAudioEmitter.StopLoop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            }

            _activeEyes.Remove(this);
            if (_coroutineRunner == this)
            {
                if (_globalIdleCoroutine != null)
                {
                    StopCoroutine(_globalIdleCoroutine);
                    _globalIdleCoroutine = null;
                }
                _coroutineRunner = null;
                HandoverIdleCoordinator();
            }
        }

        private void OnDestroy()
        {
            if (_redLaserSweepCoroutine != null)
            {
                StopCoroutine(_redLaserSweepCoroutine);
                _redLaserSweepCoroutine = null;
            }
            if (redSweepingLaserBeam != null)
            {
                redSweepingLaserBeam.enabled = false;
            }
            if (beamAudioEmitter != null)
            {
                beamAudioEmitter.StopLoop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            }

            _activeEyes.Remove(this);
            if (_coroutineRunner == this)
            {
                if (_globalIdleCoroutine != null)
                {
                    StopCoroutine(_globalIdleCoroutine);
                    _globalIdleCoroutine = null;
                }
                _coroutineRunner = null;
                HandoverIdleCoordinator();
            }
        }

        private static void HandoverIdleCoordinator()
        {
            for (int i = 0; i < _activeEyes.Count; i++)
            {
                var candidate = _activeEyes[i];
                if (candidate != null && candidate.isActiveAndEnabled && !candidate.isLockBroken && !candidate.isConsumedOrCollapsing)
                {
                    candidate.EnsureIdleCoordinatorRunning();
                    break;
                }
            }
        }

        public void EnsureIdleCoordinatorRunning()
        {
            if (isLockBroken || isConsumedOrCollapsing || !isActiveAndEnabled) return;

            if (_globalIdleCoroutine == null || _coroutineRunner == null || !_coroutineRunner.isActiveAndEnabled)
            {
                _coroutineRunner = this;
                _globalIdleCoroutine = StartCoroutine(GlobalIdleRoutine());
            }
        }

        private IEnumerator GlobalIdleRoutine()
        {
            // Initial delay so background ambience and gameplay music establish first
            float baseInitial = idleSoundInterval;
            if (FMODAudioManager.Instance != null && FMODAudioManager.Instance.Config != null && FMODAudioManager.Instance.Config.overseerIdleInterval > 0f)
            {
                baseInitial = FMODAudioManager.Instance.Config.overseerIdleInterval;
            }
            float initialDelay = Mathf.Max(3f, baseInitial * 0.5f);
            yield return new WaitForSeconds(initialDelay);

            while (true)
            {
                List<OverseerContainmentEye> candidates = new List<OverseerContainmentEye>();
                for (int i = 0; i < _activeEyes.Count; i++)
                {
                    var eye = _activeEyes[i];
                    if (eye != null && eye.isActiveAndEnabled && !eye.isLockBroken && !eye.isConsumedOrCollapsing)
                    {
                        candidates.Add(eye);
                    }
                }

                if (candidates.Count > 0)
                {
                    int chosenIdx = UnityEngine.Random.Range(0, candidates.Count);
                    OverseerContainmentEye chosenEye = candidates[chosenIdx];
                    if (chosenEye != null)
                    {
                        chosenEye.EmitIdleSound();
                    }
                }

                float baseInterval = idleSoundInterval;
                float jitter = idleSoundJitter;

                if (FMODAudioManager.Instance != null && FMODAudioManager.Instance.Config != null && FMODAudioManager.Instance.Config.overseerIdleInterval > 0f)
                {
                    baseInterval = FMODAudioManager.Instance.Config.overseerIdleInterval;
                    jitter = FMODAudioManager.Instance.Config.overseerIdleJitter;
                }
                else
                {
                    GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                    if (cfg != null && cfg.overseerIdleInterval > 0f)
                    {
                        baseInterval = cfg.overseerIdleInterval;
                    }
                }

                float waitTime = Mathf.Max(2.5f, baseInterval + UnityEngine.Random.Range(-jitter, jitter));
                yield return new WaitForSeconds(waitTime);
            }
        }

        /// <summary>
        /// Emits an idle vocalization cue from this Overseer Eye, invoking delegates and triggering 3D FMOD audio.
        /// </summary>
        public void EmitIdleSound()
        {
            if (isLockBroken || isConsumedOrCollapsing) return;

            Vector3 emitPos = PupilPosition;

            OnIdleEmitted?.Invoke(emitPos);
            OnOverseerIdleEmitted?.Invoke(this, emitPos);

            if (OnOverseerIdleEmitted == null && FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayOverseerIdle(emitPos);
            }
        }

        private void Start()
        {
            FindSceneReferences();
            ConfigureOrbitGeometry();
            ApplyVisualScale();
            SetupRedSweepingLaser();
            _redLaserTimer = (eyeIndex * 7.5f) % 30f;
        }

        public void InitializeEye(int index)
        {
            eyeIndex = index;
            ConfigureOrbitGeometry();
            SetupContainmentBeam();
            SetupRedSweepingLaser();
            ApplyVisualScale();
            _redLaserTimer = (eyeIndex * 7.5f) % 30f;
        }

        private void FindSceneReferences()
        {
            if (targetDrone == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player == null) player = GameObject.Find("PlayerDrone");
                if (player != null) targetDrone = player.transform;
            }

            if (centerReference == null)
            {
                GameObject bh = GameObject.Find("BlackHoleSystem");
                if (bh != null) centerReference = bh.transform;
            }
        }

        private void ConfigureOrbitGeometry()
        {
            eyeName = $"CONTAINMENT LOCK EYE-0{eyeIndex + 1}";
            _orbitAngle = eyeIndex * 90f;
        }

        private void ApplyVisualScale()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float scale = config != null ? config.overseerVisualScale : 14.0f;
            if (visualRoot != null)
            {
                visualRoot.localScale = Vector3.one * scale;
            }
        }

        private void SetupContainmentBeam()
        {
            if (containmentBeam == null)
            {
                Transform beamT = transform.Find("ContainmentBeam");
                if (beamT != null)
                {
                    containmentBeam = beamT.GetComponent<LineRenderer>();
                }
            }

            if (containmentBeam == null)
            {
                GameObject beamGo = new GameObject("ContainmentBeam");
                beamGo.transform.SetParent(transform, false);
                containmentBeam = beamGo.AddComponent<LineRenderer>();
            }

            // Beam is strictly visual: ensure NO collider or physics trigger exists
            var existingCol = containmentBeam.GetComponent<Collider>();
            if (existingCol != null) SafeDestroy(existingCol);

            containmentBeam.positionCount = 2;
            containmentBeam.startWidth = beamStartWidth;
            containmentBeam.endWidth = beamEndWidth;
            containmentBeam.useWorldSpace = true;
            containmentBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            containmentBeam.receiveShadows = false;

            Material lineMat = null;
#if UNITY_EDITOR
            lineMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ContainmentBeam.mat");
            if (lineMat == null)
            {
                lineMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ValuableScrapGlow.mat");
            }
#endif
            if (lineMat == null) lineMat = Resources.Load<Material>("M_ContainmentBeam");
            if (lineMat == null) lineMat = Resources.Load<Material>("M_ValuableScrapGlow");
            if (lineMat != null)
            {
                lineMat.renderQueue = 2450;
                containmentBeam.sharedMaterial = lineMat;
            }

            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(beamColorCore, 0.0f),
                    new GradientColorKey(beamColorGlow, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.95f, 0.0f),
                    new GradientAlphaKey(0.85f, 1.0f)
                }
            );
            containmentBeam.colorGradient = grad;
            containmentBeam.enabled = !isLockBroken;
        }

        public void SetupRedSweepingLaser()
        {
            if (redSweepingLaserBeam == null)
            {
                Transform beamT = transform.Find("RedSweepingLaserBeam");
                if (beamT != null)
                {
                    redSweepingLaserBeam = beamT.GetComponent<LineRenderer>();
                }
            }

            if (redSweepingLaserBeam == null)
            {
                GameObject beamGo = new GameObject("RedSweepingLaserBeam");
                beamGo.transform.SetParent(transform, false);
                redSweepingLaserBeam = beamGo.AddComponent<LineRenderer>();
            }

            var existingCol = redSweepingLaserBeam.GetComponent<Collider>();
            if (existingCol != null) SafeDestroy(existingCol);

            redSweepingLaserBeam.positionCount = 2;
            redSweepingLaserBeam.startWidth = 1.4f;
            redSweepingLaserBeam.endWidth = 1.4f;
            redSweepingLaserBeam.useWorldSpace = true;
            redSweepingLaserBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            redSweepingLaserBeam.receiveShadows = false;

            if (redLaserMaterial == null)
            {
#if UNITY_EDITOR
                redLaserMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_RedSweepingLaser.mat");
#endif
                if (redLaserMaterial == null) redLaserMaterial = Resources.Load<Material>("M_RedSweepingLaser");
                if (redLaserMaterial == null)
                {
                    Shader s = Shader.Find("OrbitalPanopticon/RedSweepingLaser") ?? Shader.Find("Universal Render Pipeline/Unlit");
                    if (s != null)
                    {
                        redLaserMaterial = new Material(s);
                        redLaserMaterial.name = "M_RedSweepingLaser";
                    }
                }
            }

            redSweepingLaserBeam.sharedMaterial = redLaserMaterial;
            redSweepingLaserBeam.enabled = false;

            if (beamAudioEmitter == null)
            {
                beamAudioEmitter = GetComponent<BeamSplineAudioEmitter>();
                if (beamAudioEmitter == null)
                {
                    beamAudioEmitter = gameObject.AddComponent<BeamSplineAudioEmitter>();
                }
            }
        }

        private void Update()
        {
            // Dynamically synchronize visual scale every frame from GameBalanceConfig
            ApplyVisualScale();

            if (!isConsumedOrCollapsing)
            {
                if (!isLockBroken)
                {
                    UpdateOrbitalMovement(1.0f);
                    CheckBeamPlayerHazard();
                }
                else
                {
                    GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
                    float brokenSpeedMult = config != null ? config.brokenEyeOrbitSpeedMultiplier : 1.45f;
                    UpdateOrbitalMovement(brokenSpeedMult);
                    UpdateOverloadedRedLaser();
                }
            }

            LookTowardCenterAndPlayer();
            AnimatePupil();
        }

        private void CheckBeamPlayerHazard()
        {
            if (isLockBroken || isConsumedOrCollapsing) return;

            DroneController drone = DroneController.Instance;
            if (drone == null || !drone.IsControlEnabled) return;

            if (drone.BeamHitCooldownTimer > 0f) return;

            Vector3 startPos = PupilPosition;
            Vector3 blackHoleCenter = centerReference != null ? centerReference.position : Vector3.zero;

            GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
            float currentEventHorizonRadius = (cfg != null) ? cfg.eventHorizonRadius : 3.5f;

            Vector3 dirFromCenterToEye = (startPos - blackHoleCenter).normalized;
            Vector3 endPos = blackHoleCenter + dirFromCenterToEye * (currentEventHorizonRadius * 0.98f);
            Vector3 dronePos = drone.transform.position;

            Vector3 closestPoint = GetClosestPointOnSegment(dronePos, startPos, endPos, out float t);
            float dist = Vector3.Distance(dronePos, closestPoint);

            const float beamHazardRadius = 2.2f;
            if (dist < beamHazardRadius)
            {
                OnPlayerHitByBeam(drone, closestPoint, startPos, endPos);
            }
        }

        private static Vector3 GetClosestPointOnSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd, out float t)
        {
            Vector3 ab = segmentEnd - segmentStart;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.0001f)
            {
                t = 0f;
                return segmentStart;
            }
            t = Mathf.Clamp01(Vector3.Dot(point - segmentStart, ab) / abLenSq);
            return segmentStart + (ab * t);
        }

        private void OnPlayerHitByBeam(DroneController drone, Vector3 closestPoint, Vector3 startPos, Vector3 endPos)
        {
            drone.CurrentBeamHits++;
            drone.BeamHitCooldownTimer = 1.1f; // Prevents multi-hit during single pass

            // Knock player away from the beam segment
            Vector3 awayDir = (drone.transform.position - closestPoint);
            if (awayDir.sqrMagnitude < 0.001f)
            {
                Vector3 beamDir = (endPos - startPos).normalized;
                awayDir = Vector3.Cross(beamDir, Vector3.up);
                if (awayDir.sqrMagnitude < 0.001f) awayDir = Vector3.up;
            }
            awayDir.Normalize();

            if (drone.Rigidbody != null)
            {
                GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                float impulse = (cfg != null) ? cfg.beamKnockbackImpulse : 4.5f;
                drone.Rigidbody.linearVelocity = drone.Rigidbody.linearVelocity * 0.5f + awayDir * impulse;
            }

            // High-impact visual / post-processing / camera glitch cues
            if (PostProcessingToolkitController.Instance != null)
            {
                PostProcessingToolkitController.Instance.TriggerImpactPulse(bloomBoost: 2.2f, chromaticBoost: 0.65f, lensDistortionBoost: -0.18f, duration: 0.35f, motionBlurBoost: 0.7f);
            }
            if (VHSCameraController.Instance != null)
            {
                VHSCameraController.Instance.TriggerGlitchBurst(0.85f, 0.35f);
            }
            if (CameraShakeController.Instance != null)
            {
                CameraShakeController.Instance.TriggerShake(duration: 0.45f, magnitude: 0.28f);
            }

            OnPlayerBeamShock?.Invoke(drone, closestPoint);

            // Audio shock cue
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayContainmentBeamShock(closestPoint);
            }

            // Evaluate Hits (Strict 3-Hit system, no HP bar)
            if (drone.CurrentBeamHits < 3)
            {
                Debug.LogWarning($"<color=#FF5500>[CONTAINMENT BEAM HAZARD] Drone struck by beam! Hits: {drone.CurrentBeamHits}/3</color>");
            }
            else
            {
                drone.CurrentBeamHits = 0;
                Debug.LogError("<color=#FF0033>[CONTAINMENT BEAM HAZARD] 3rd Hit sustained: Critical beam overload! Drone destroyed!</color>");
                if (ArenaBoundaryManager.Instance != null)
                {
                    ArenaBoundaryManager.Instance.TriggerSignalLoss("КРИТИЧЕСКАЯ ПЕРЕГРУЗКА ЛУЧОМ — ДРОН УНИЧТОЖЕН", true);
                }
            }
        }

        private void LateUpdate()
        {
            UpdateContainmentBeam();
        }

        private void UpdateOrbitalMovement(float speedMultiplier = 1.0f)
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float orbitRadius = config != null ? config.overseerOrbitRadius : 78.0f;
            float angularSpeed = config != null ? config.overseerOrbitAngularSpeed : 8.0f;
            float simSpeed = config != null ? config.globalSimulationSpeed : 1.0f;

            // Increment orbital angle in degrees
            _orbitAngle += angularSpeed * speedMultiplier * simSpeed * Time.deltaTime;

            // Fetch orbital normal axis from config
            Vector3 normalAxis = Vector3.up;
            if (config != null && config.overseerOrbitAxes != null && config.overseerOrbitAxes.Length > 0)
            {
                normalAxis = config.overseerOrbitAxes[eyeIndex % config.overseerOrbitAxes.Length].normalized;
            }

            // Construct orthonormal basis vectors on plane perpendicular to normalAxis
            Vector3 refVector = Mathf.Abs(Vector3.Dot(normalAxis, Vector3.up)) < 0.95f ? Vector3.up : Vector3.right;
            Vector3 u = Vector3.Cross(normalAxis, refVector).normalized;
            Vector3 v = Vector3.Cross(normalAxis, u).normalized;

            float rad = _orbitAngle * Mathf.Deg2Rad;
            Vector3 orbitDir = (u * Mathf.Cos(rad) + v * Mathf.Sin(rad)).normalized;

            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;
            transform.position = center + (orbitDir * orbitRadius);
        }

        private void UpdateOverloadedRedLaser()
        {
            if (!isLockBroken || isConsumedOrCollapsing || _isSweepingLaserActive) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float interval = config != null ? config.redLaserSweepInterval : 30.0f;

            _redLaserTimer += Time.deltaTime;
            if (_redLaserTimer >= interval)
            {
                _redLaserTimer = 0f;
                if (_redLaserSweepCoroutine != null) StopCoroutine(_redLaserSweepCoroutine);
                _redLaserSweepCoroutine = StartCoroutine(ExecuteRedLaserDiameterSweep());
            }
        }

        private IEnumerator ExecuteRedLaserDiameterSweep()
        {
            _isSweepingLaserActive = true;
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float telegraphDuration = config != null ? config.redLaserTelegraphDuration : 2.0f;
            float sweepDuration = config != null ? config.redLaserSweepDuration : 6.5f;
            float hitRadius = config != null ? config.redLaserHitRadius : 2.0f;
            float shakeIntensity = config != null ? config.redLaserCameraShakeIntensity : 0.08f;

            Vector3 blackHoleCenter = centerReference != null ? centerReference.position : Vector3.zero;
            Vector3 eyeToCenter = (blackHoleCenter - transform.position).normalized;

            // 1) Choose Sweep Plane: Randomly pick Horizontal or Vertical diameter sweep
            bool isHorizontalSweep = UnityEngine.Random.value > 0.5f;
            Vector3 sweepAxis;
            if (isHorizontalSweep)
            {
                sweepAxis = Vector3.up;
            }
            else
            {
                Vector3 rightDir = Vector3.Cross(Vector3.up, eyeToCenter).normalized;
                sweepAxis = rightDir.sqrMagnitude > 0.001f ? rightDir : Vector3.right;
            }

            bool reverseDirection = UnityEngine.Random.value > 0.5f;
            float startAngle = reverseDirection ? 55f : -55f;
            float endAngle = reverseDirection ? -55f : 55f;

            if (redSweepingLaserBeam == null) SetupRedSweepingLaser();

            // 2) Telegraph Phase (redLaserTelegraphDuration = 2.0s):
            // Pulse Eye's red emissive core brightly and display thin faint warning laser line along start angle
            if (triggerBeamAttackSoundAtTelegraph && FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayOverseerBeamAttack(PupilPosition);
            }

            if (redSweepingLaserBeam != null)
            {
                redSweepingLaserBeam.startWidth = 0.15f;
                redSweepingLaserBeam.endWidth = 0.15f;
                redSweepingLaserBeam.startColor = new Color(1f, 0.15f, 0.15f, 0.35f);
                redSweepingLaserBeam.endColor = new Color(1f, 0.15f, 0.15f, 0.35f);
                Vector3 startSweepDir = Quaternion.AngleAxis(startAngle, sweepAxis) * eyeToCenter;
                redSweepingLaserBeam.SetPosition(0, PupilPosition);
                redSweepingLaserBeam.SetPosition(1, PupilPosition + (startSweepDir * 160f));
                redSweepingLaserBeam.enabled = true;
            }

            float elapsed = 0f;
            while (elapsed < telegraphDuration && !isConsumedOrCollapsing)
            {
                elapsed += Time.deltaTime;
                float tTele = elapsed / telegraphDuration;

                if (pupilLight != null)
                {
                    float strobe = (Mathf.Sin(elapsed * 28f) > 0f) ? 35f : 12f;
                    pupilLight.intensity = Mathf.Lerp(16f, 42f, tTele) + strobe * 0.25f;
                    pupilLight.color = Color.red;
                }

                if (redSweepingLaserBeam != null)
                {
                    eyeToCenter = (blackHoleCenter - transform.position).normalized;
                    Vector3 currentStartDir = Quaternion.AngleAxis(startAngle, sweepAxis) * eyeToCenter;
                    redSweepingLaserBeam.SetPosition(0, PupilPosition);
                    redSweepingLaserBeam.SetPosition(1, PupilPosition + (currentStartDir * 160f));
                }

                yield return null;
            }

            // 3) Active Sweep Phase (redLaserSweepDuration = 6.5s):
            // High-intensity wide plasma beam sweeping across sphere diameter
            if (redSweepingLaserBeam != null)
            {
                redSweepingLaserBeam.startWidth = 1.8f;
                redSweepingLaserBeam.endWidth = 1.8f;
                redSweepingLaserBeam.startColor = Color.white;
                redSweepingLaserBeam.endColor = Color.white;
            }

            Vector3 initSweepDir = Quaternion.AngleAxis(startAngle, sweepAxis) * eyeToCenter;
            Vector3 initBeamStart = PupilPosition;
            Vector3 initBeamEnd = initBeamStart + (initSweepDir * 160f);

            // 1. Play 3D attack sound on the Overseer
            if (!triggerBeamAttackSoundAtTelegraph && FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayOverseerBeamAttack(initBeamStart);
            }

            // 2. Start continuous 3D beam loop tracking along the beam rail
            if (beamAudioEmitter == null) beamAudioEmitter = GetComponent<BeamSplineAudioEmitter>() ?? gameObject.AddComponent<BeamSplineAudioEmitter>();
            if (beamAudioEmitter != null)
            {
                EventReference loopRef = (FMODAudioManager.Instance != null && FMODAudioManager.Instance.Config != null)
                    ? FMODAudioManager.Instance.Config.overseerBeamLoop
                    : FMODUnity.RuntimeManager.PathToEventReference("event:/Boss/Overseer_BeamLoop");

                beamAudioEmitter.StartLoop(loopRef, initBeamStart, initBeamEnd);
            }

            elapsed = 0f;
            while (elapsed < sweepDuration && !isConsumedOrCollapsing)
            {
                elapsed += Time.deltaTime;
                float tSweep = Mathf.Clamp01(elapsed / sweepDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, tSweep);

                eyeToCenter = (blackHoleCenter - transform.position).normalized;
                float currentAngle = Mathf.Lerp(startAngle, endAngle, smoothT);
                Vector3 currentSweepDir = Quaternion.AngleAxis(currentAngle, sweepAxis) * eyeToCenter;

                Vector3 beamStart = PupilPosition;
                Vector3 beamEnd = beamStart + (currentSweepDir * 160f);

                if (redSweepingLaserBeam != null)
                {
                    redSweepingLaserBeam.SetPosition(0, beamStart);
                    redSweepingLaserBeam.SetPosition(1, beamEnd);
                }

                // Update continuous 3D beam audio emitter along beam rail at closest point to player
                if (beamAudioEmitter != null)
                {
                    beamAudioEmitter.UpdatePosition(redSweepingLaserBeam);
                }

                if (CameraShakeController.Instance != null)
                {
                    CameraShakeController.Instance.TriggerShake(Time.deltaTime * 1.5f, shakeIntensity);
                }

                // Check distance from PlayerDrone to active red laser line segment
                DroneController drone = DroneController.Instance;
                if (drone != null && drone.IsControlEnabled && drone.BeamHitCooldownTimer <= 0f)
                {
                    Vector3 dronePos = drone.transform.position;
                    Vector3 closestPoint = GetClosestPointOnSegment(dronePos, beamStart, beamEnd, out float segT);
                    float dist = Vector3.Distance(dronePos, closestPoint);
                    if (dist < hitRadius)
                    {
                        OnPlayerHitByBeam(drone, closestPoint, beamStart, beamEnd);
                    }
                }

                yield return null;
            }

            if (beamAudioEmitter != null)
            {
                beamAudioEmitter.StopLoop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            }

            if (redSweepingLaserBeam != null)
            {
                redSweepingLaserBeam.enabled = false;
            }

            if (pupilLight != null)
            {
                pupilLight.intensity = 18f;
                pupilLight.color = Color.red;
            }

            _isSweepingLaserActive = false;
            _redLaserSweepCoroutine = null;
        }

        private void LookTowardCenterAndPlayer()
        {
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;
            Vector3 lookDir = (center - transform.position).normalized;

            // Slight dynamic tracking of player drone if available
            if (targetDrone != null)
            {
                Vector3 toPlayer = (targetDrone.position - transform.position).normalized;
                lookDir = Vector3.Lerp(lookDir, toPlayer, 0.15f).normalized;
            }

            if (lookDir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 3.0f);
            }
        }

        private void AnimatePupil()
        {
            if (isLockBroken) return;

            if (pupilLight != null)
            {
                float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 3f + (eyeIndex * 1.5f)));
                pupilLight.intensity = Mathf.Lerp(4f, 8f, pulse);
            }
        }

        public void UpdateContainmentBeam()
        {
            if (containmentBeam == null) return;

            if (isLockBroken)
            {
                if (containmentBeam.enabled) containmentBeam.enabled = false;
                return;
            }

            if (!containmentBeam.enabled) containmentBeam.enabled = true;

            Vector3 eyeLensPos = PupilPosition;
            Vector3 blackHoleCenter = centerReference != null ? centerReference.position : Vector3.zero;

            containmentBeam.SetPosition(0, eyeLensPos);
            containmentBeam.SetPosition(1, blackHoleCenter);
        }

        /// <summary>
        /// Breaks this containment lock: plays electrical overload flash, disables beam,
        /// dims materials to dark offline state, and stops orbital rotation.
        /// </summary>
        public void BreakContainmentLock()
        {
            if (isLockBroken) return;
            isLockBroken = true;
            _redLaserTimer = (eyeIndex * 7.5f) % 30f;

            if (_coroutineRunner == this)
            {
                if (_globalIdleCoroutine != null)
                {
                    StopCoroutine(_globalIdleCoroutine);
                    _globalIdleCoroutine = null;
                }
                _coroutineRunner = null;
                HandoverIdleCoordinator();
            }

            if (containmentBeam != null)
            {
                containmentBeam.enabled = false;
            }

            if (_overloadFlashCoroutine != null)
            {
                StopCoroutine(_overloadFlashCoroutine);
            }
            _overloadFlashCoroutine = StartCoroutine(OverloadFlashAndShutdownRoutine());
        }

        private IEnumerator OverloadFlashAndShutdownRoutine()
        {
            // Play audio cues
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayContainmentLockBreak(transform.position);
            }
            else if (GameAudioManager.Instance != null && GameAudioManager.Instance.Config != null)
            {
                GameAudioManager.Instance.PlayOneShot(GameAudioManager.Instance.Config.overseerDestructionSfx, 1.0f);
            }

            // Post-Processing Bloom Flash & VHS Tracking Glitch Burst
            if (PostProcessingToolkitController.Instance != null)
            {
                PostProcessingToolkitController.Instance.TriggerImpactPulse(2.4f, 0.4f, 0.65f);
            }
            if (VHSCameraController.Instance != null)
            {
                VHSCameraController.Instance.TriggerGlitchBurst(0.75f, 0.5f);
            }
            if (CameraShakeController.Instance != null)
            {
                CameraShakeController.Instance.TriggerStageTransitionShake(1.4f, 0.35f);
            }

            // Phase 1: High-intensity electrical overload flash (0.55s)
            float elapsed = 0f;
            const float flashDuration = 0.55f;

            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float flicker = (Mathf.Sin(elapsed * 45f) > 0f) ? 1f : 0.15f;

                if (pupilLight != null)
                {
                    pupilLight.enabled = true;
                    pupilLight.intensity = flicker * 28f;
                    pupilLight.color = Color.Lerp(Color.white, new Color(0.2f, 0.9f, 1f), flicker);
                }

                if (_renderers != null && _propBlock != null)
                {
                    Color emissive = Color.Lerp(Color.black, Color.white * 3.5f, flicker);
                    _propBlock.SetColor("_EmissionColor", emissive);
                    for (int i = 0; i < _renderers.Length; i++)
                    {
                        if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_propBlock);
                    }
                }

                yield return null;
            }

            // Phase 2: Menacing glowing RED broken eye state ([HDR] new Color(5.0f, 0.08f, 0.08f, 1.0f))
            Color brokenRedEmission = new Color(5.0f, 0.08f, 0.08f, 1.0f);
            Color brokenRedBase = new Color(1.0f, 0.15f, 0.15f, 1.0f);

            if (pupilLight != null)
            {
                pupilLight.enabled = true;
                pupilLight.color = Color.red;
                pupilLight.intensity = 18f;
            }

            if (_renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] == null) continue;

                    if (_renderers[i].material != null)
                    {
                        _renderers[i].material.EnableKeyword("_EMISSION");
                        _renderers[i].material.SetColor("_EmissionColor", brokenRedEmission);
                        if (_renderers[i].material.HasProperty("_BaseColor"))
                        {
                            _renderers[i].material.SetColor("_BaseColor", brokenRedBase);
                        }
                    }

                    if (_propBlock != null)
                    {
                        _propBlock.SetColor("_EmissionColor", brokenRedEmission);
                        _propBlock.SetColor("_BaseColor", brokenRedBase);
                        _renderers[i].SetPropertyBlock(_propBlock);
                    }
                }
            }

            _overloadFlashCoroutine = null;
            Debug.Log($"<color=#FF5500>[CONTAINMENT BREACH] {eyeName} lock broken and offline!</color>");
        }
    }
}
