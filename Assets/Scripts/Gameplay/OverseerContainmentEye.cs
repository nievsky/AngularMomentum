using System;
using System.Collections;
using UnityEngine;
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

        private void Awake()
        {
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            if (visualRoot == null) visualRoot = transform;

            // Hit detection colliders and HP bars must be completely removed
            var colliders = GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                Destroy(col);
            }

            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
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
        }

        private void Start()
        {
            FindSceneReferences();
            ConfigureOrbitGeometry();
            ApplyVisualScale();
        }

        public void InitializeEye(int index)
        {
            eyeIndex = index;
            ConfigureOrbitGeometry();
            SetupContainmentBeam();
            ApplyVisualScale();
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
            if (existingCol != null) Destroy(existingCol);

            containmentBeam.positionCount = 2;
            containmentBeam.startWidth = beamStartWidth;
            containmentBeam.endWidth = beamEndWidth;
            containmentBeam.useWorldSpace = true;
            containmentBeam.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            containmentBeam.receiveShadows = false;

            Material lineMat = null;
#if UNITY_EDITOR
            lineMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ValuableScrapGlow.mat");
#endif
            if (lineMat == null) lineMat = Resources.Load<Material>("M_ValuableScrapGlow");
            if (lineMat != null) containmentBeam.sharedMaterial = lineMat;

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

        private void Update()
        {
            // Dynamically synchronize visual scale every frame from GameBalanceConfig
            ApplyVisualScale();

            if (!isConsumedOrCollapsing && !isLockBroken)
            {
                UpdateOrbitalMovement();
                CheckBeamPlayerHazard();
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
            Vector3 endPos = centerReference != null ? centerReference.position : Vector3.zero;
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
                drone.Rigidbody.AddForce(awayDir * 18f, ForceMode.VelocityChange);
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

            // Audio shock cue
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayContainmentLockBreak(closestPoint);
            }
            else if (GameAudioManager.Instance != null && GameAudioManager.Instance.Config != null)
            {
                GameAudioManager.Instance.PlayOneShot(GameAudioManager.Instance.Config.overseerDestructionSfx, 0.85f);
            }

            // Evaluate Hits (Strict 3-Hit system, no HP bar)
            if (drone.CurrentBeamHits < 3)
            {
                string warningMsg = $"ВНИМАНИЕ: ПОВРЕЖДЕНИЕ ОТ ЛУЧА СДЕРЖИВАНИЯ! [{drone.CurrentBeamHits}/3]";
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowCenterWarningAlert(warningMsg, 2.0f);
                }
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

        private void UpdateOrbitalMovement()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float orbitRadius = config != null ? config.overseerOrbitRadius : 78.0f;
            float angularSpeed = config != null ? config.overseerOrbitAngularSpeed : 8.0f;
            float simSpeed = config != null ? config.globalSimulationSpeed : 1.0f;

            // Increment orbital angle in degrees
            _orbitAngle += angularSpeed * simSpeed * Time.deltaTime;

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

        private void UpdateContainmentBeam()
        {
            if (containmentBeam == null) return;

            if (isLockBroken)
            {
                if (containmentBeam.enabled) containmentBeam.enabled = false;
                return;
            }

            if (!containmentBeam.enabled) containmentBeam.enabled = true;

            Vector3 startPos = PupilPosition;
            Vector3 endPos = centerReference != null ? centerReference.position : Vector3.zero;

            containmentBeam.SetPosition(0, startPos);
            containmentBeam.SetPosition(1, endPos);
        }

        /// <summary>
        /// Breaks this containment lock: plays electrical overload flash, disables beam,
        /// dims materials to dark offline state, and stops orbital rotation.
        /// </summary>
        public void BreakContainmentLock()
        {
            if (isLockBroken) return;
            isLockBroken = true;

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

            // Phase 2: Complete dark offline state
            if (pupilLight != null)
            {
                pupilLight.intensity = 0f;
                pupilLight.enabled = false;
            }

            if (_renderers != null && _propBlock != null)
            {
                _propBlock.SetColor("_EmissionColor", Color.black);
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null) _renderers[i].SetPropertyBlock(_propBlock);
                }
            }

            _overloadFlashCoroutine = null;
            Debug.Log($"<color=#FF5500>[CONTAINMENT BREACH] {eyeName} lock broken and offline!</color>");
        }
    }
}
