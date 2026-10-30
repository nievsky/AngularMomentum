using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Player
{
    [RequireComponent(typeof(DroneController))]
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class GravityManipulatorController : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private DroneController droneController;
        [SerializeField] private Rigidbody droneRb;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Transform roboArmVisual;
        [SerializeField] private Transform blackHoleCenter;

        [Header("Visual Beam (Optional Feedback)")]
        [SerializeField] private LineRenderer magnetBeamLine;
        [SerializeField] private Color magnetBeamColor = new Color(0.2f, 0.9f, 1f, 0.85f);
        [SerializeField] private Color shockwaveColor = new Color(0.15f, 0.9f, 1f, 0.85f);

        [Header("Punt Shockwave Feedback")]
        [SerializeField] private Transform puntShockwaveRing;

        [Header("Observed Scrap Indicator on Arm")]
        [SerializeField] private Light armIndicatorLight;
        [SerializeField] private Color observedArmGlowColor = new Color(1f, 0.85f, 0.1f, 1f);

        // FPV Robo-Arm Animation State
        private readonly Vector3 _armBaseLocalPos = new Vector3(0.45f, -0.35f, 0.8f);
        private readonly Vector3 _armGrabLocalPos = new Vector3(0.38f, -0.28f, 0.95f);
        private readonly Quaternion _armBaseLocalRot = Quaternion.identity;
        private readonly Quaternion _armGrabLocalRot = Quaternion.Euler(5f, -14f, 0f); // Tilted inward toward HoldPoint
        private Vector3 _armRecoilOffset = Vector3.zero;
        private Quaternion _armRecoilRot = Quaternion.identity;
        private Transform _clawLeft;
        private Transform _clawRight;
        private MeshRenderer _clawLeftRenderer;
        private MeshRenderer _clawRightRenderer;
        private MaterialPropertyBlock _armPropertyBlock;
        private MaterialPropertyBlock _shockwavePropertyBlock;
        private readonly Color _defaultClawColor = new Color(1.0f, 0.85f, 0.10f, 1.0f);

        // Runtime state
        private readonly List<ScrapItem> _heldItems = new List<ScrapItem>();
        private ScrapItem _currentPullTarget;
        private bool _isGrabHeld = false;
        private bool _requireRmbRelease = false;
        private float _beamFadeTimer = 0f;
        private Coroutine _puntAnimCoroutine;

        public IReadOnlyList<ScrapItem> HeldItems => _heldItems;
        public int HeldCount => _heldItems.Count;
        public bool IsHolding => _heldItems.Count > 0;
        public Transform HoldPoint => holdPoint;
        public bool RequireRmbRelease => _requireRmbRelease;

        private void Awake()
        {
            if (droneController == null) droneController = GetComponent<DroneController>();
            if (droneRb == null) droneRb = GetComponent<Rigidbody>();
            if (mainCamera == null) mainCamera = Camera.main;

            if (blackHoleCenter == null)
            {
                GameObject bh = GameObject.Find("BlackHoleSystem");
                if (bh != null) blackHoleCenter = bh.transform;
            }

            if (roboArmVisual != null)
            {
                _clawLeft = roboArmVisual.Find("Claw_Prong_L");
                _clawRight = roboArmVisual.Find("Claw_Prong_R");
                if (_clawLeft != null) _clawLeftRenderer = _clawLeft.GetComponent<MeshRenderer>();
                if (_clawRight != null) _clawRightRenderer = _clawRight.GetComponent<MeshRenderer>();

                if (armIndicatorLight == null)
                {
                    Transform lightT = roboArmVisual.Find("ObservedIndicatorLight");
                    if (lightT != null)
                    {
                        armIndicatorLight = lightT.GetComponent<Light>();
                    }
                    else
                    {
                        GameObject lightGo = new GameObject("ObservedIndicatorLight");
                        lightGo.transform.SetParent(roboArmVisual, false);
                        lightGo.transform.localPosition = new Vector3(0f, 0.15f, 0.45f);
                        armIndicatorLight = lightGo.AddComponent<Light>();
                        armIndicatorLight.type = LightType.Point;
                        armIndicatorLight.range = 3.5f;
                        armIndicatorLight.color = observedArmGlowColor;
                        armIndicatorLight.intensity = 0f;
                        armIndicatorLight.enabled = false;
                    }
                }

                if (puntShockwaveRing == null)
                {
                    puntShockwaveRing = roboArmVisual.Find("PuntShockwaveRing");
                }
            }

            SetupMagnetBeamLine();
        }

        private void SetupMagnetBeamLine()
        {
            if (magnetBeamLine == null)
            {
                GameObject beamGo = new GameObject("MagnetBeamLine");
                beamGo.transform.SetParent(transform, false);
                magnetBeamLine = beamGo.AddComponent<LineRenderer>();
                magnetBeamLine.positionCount = 2;
                magnetBeamLine.startWidth = 0.06f;
                magnetBeamLine.endWidth = 0.03f;
                magnetBeamLine.useWorldSpace = true;
                magnetBeamLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                magnetBeamLine.receiveShadows = false;

                Material lineMat = AssetDatabaseLoadMaterial();
                if (lineMat != null) magnetBeamLine.sharedMaterial = lineMat;
                magnetBeamLine.enabled = false;
            }
        }

        private Material AssetDatabaseLoadMaterial()
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Scrap.mat");
#else
            return null;
#endif
        }

        private void Update()
        {
            // Fix 2: Purge invalid, destroyed, consumed, or fading items
            _heldItems.RemoveAll(x => x == null || !x.gameObject.activeInHierarchy || x.CurrentState == ScrapState.Consumed || x.CurrentState == ScrapState.FadingOut);
            RecalculateCargoMass();

            HandleInputs();
            UpdateCrosshairStatus();
            UpdateRoboArmVisuals();
            UpdateBeamVisuals();
        }

        private void FixedUpdate()
        {
            ApplyHoldingPhysics();
        }

        private void HandleInputs()
        {
            if (droneController != null && !droneController.IsControlEnabled)
            {
                ReleaseAllHeld();
                _requireRmbRelease = false;
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Pulling/Holding uses ONLY Right Mouse Button
            bool isRmbHeld = mouse.rightButton.isPressed;

            // Reset requireRmbRelease whenever !Input.GetMouseButton(1) is true (never rely solely on GetMouseButtonUp)
            if (!isRmbHeld)
            {
                _requireRmbRelease = false;
            }

            // Grab is only active if RMB is held AND the release lock is inactive
            _isGrabHeld = isRmbHeld && !_requireRmbRelease;

            // Punting/Launching uses ONLY Left Mouse Button
            bool isLmbDown = mouse.leftButton.wasPressedThisFrame;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null) return;

            Ray aimRay = GetAimRay();
            Vector3 holdTargetPos = GetHoldTargetPosition(aimRay, config);

            // Position dynamic HoldPoint
            if (holdPoint != null)
            {
                if (mainCamera != null && holdPoint.parent == mainCamera.transform)
                {
                    holdPoint.localPosition = new Vector3(0f, -0.1f, config.holdPointDistance);
                    holdPoint.localRotation = Quaternion.identity;
                }
                else
                {
                    holdPoint.position = holdTargetPos;
                    holdPoint.rotation = mainCamera != null ? mainCamera.transform.rotation : Quaternion.LookRotation(aimRay.direction, Vector3.up);
                }
            }

            // 1. Fire / Punt Logic (LMB)
            if (isLmbDown)
            {
                if (_heldItems.Count > 0)
                {
                    // Rule A: Firing a held object (LMB while holding RMB)
                    PuntHeldItems(aimRay, config);
                }
                else
                {
                    // Rule B: Dry punt / direct push without holding objects (LMB alone)
                    PerformDryPunt(aimRay, config);
                }
            }
            // 2. Pull / Hold Logic (RMB only, when not locked)
            else if (_isGrabHeld)
            {
                if (_heldItems.Count < config.maxHeldObjects)
                {
                    PerformMagnetPull(aimRay, holdTargetPos, config);
                }
            }
            // 3. Release when RMB released
            else
            {
                if (_heldItems.Count > 0)
                {
                    ReleaseAllHeld();
                }
                _currentPullTarget = null;
            }
        }

        private Ray GetAimRay()
        {
            if (mainCamera != null)
            {
                return new Ray(mainCamera.transform.position, mainCamera.transform.forward);
            }
            return new Ray(transform.position, transform.forward);
        }

        private Vector3 GetHoldTargetPosition(Ray aimRay, GameBalanceConfig config)
        {
            if (holdPoint != null)
            {
                return holdPoint.position;
            }
            if (mainCamera != null)
            {
                return mainCamera.transform.position + (mainCamera.transform.forward * config.holdPointDistance) + (mainCamera.transform.up * -0.1f);
            }
            Vector3 dronePos = droneRb != null ? droneRb.position : transform.position;
            return dronePos + (aimRay.direction * config.holdPointDistance);
        }

        private void PerformMagnetPull(Ray aimRay, Vector3 holdTargetPos, GameBalanceConfig config)
        {
            Vector3 dronePos = droneRb != null ? droneRb.position : transform.position;
            float checkRange = config.isMagnetUnlocked ? config.magnetPullRange : config.grabRangeBase;
            Collider[] hits = UnityEngine.Physics.OverlapSphere(dronePos, checkRange, ~0, QueryTriggerInteraction.Ignore);

            ScrapItem bestCandidate = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                ScrapItem scrap = hits[i].GetComponentInParent<ScrapItem>();
                if (scrap == null || _heldItems.Contains(scrap) || scrap.CurrentState == ScrapState.Consumed || scrap.CurrentState == ScrapState.FadingOut)
                    continue;

                // Check pull immunity (e.g. recently punted or during ballistic launch)
                if (scrap.IsPullImmune || scrap.pullImmunityTimer > 0f || scrap.ballisticLaunchTimer > 0f)
                    continue;

                Vector3 toScrap = scrap.transform.position - dronePos;
                float dist = toScrap.magnitude;
                if (dist < 0.001f) continue;

                Vector3 dir = toScrap / dist;
                float angle = Vector3.Angle(aimRay.direction, dir);
                float maxAngle = config.isMagnetUnlocked ? config.magnetConeAngle : 35f;

                if (angle <= maxAngle)
                {
                    Rigidbody sRb = scrap.Rigidbody;
                    if (sRb != null)
                    {
                        Vector3 toHold = holdTargetPos - sRb.position;
                        float distToHold = toHold.magnitude;
                        Vector3 pullDir = distToHold > 0.001f ? toHold / distToHold : Vector3.zero;

                        // Magnetic attraction force
                        float pullForce = config.isMagnetUnlocked ? config.magnetPullForce : config.magnetPullForce * 0.7f;
                        sRb.AddForce(pullDir * pullForce, ForceMode.Acceleration);
                        sRb.linearVelocity = Vector3.Lerp(sRb.linearVelocity, pullDir * 16f, Time.fixedDeltaTime * 4f);

                        // Capture if close enough to HoldPoint
                        if (distToHold <= 1.6f)
                        {
                            AttachHeldItem(scrap);
                            return;
                        }
                    }

                    if (angle + dist < bestScore)
                    {
                        bestScore = angle + dist;
                        bestCandidate = scrap;
                    }
                }
            }

            _currentPullTarget = bestCandidate;
        }

        private void AttachHeldItem(ScrapItem scrap)
        {
            if (scrap == null || _heldItems.Contains(scrap)) return;

            scrap.SetState(ScrapState.Held);
            _heldItems.Add(scrap);
            _currentPullTarget = null;
            RecalculateCargoMass();
        }

        private void ApplyHoldingPhysics()
        {
            if (_heldItems.Count == 0) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null) return;

            Ray aimRay = GetAimRay();
            Vector3 holdTargetPos = GetHoldTargetPosition(aimRay, config);

            for (int i = _heldItems.Count - 1; i >= 0; i--)
            {
                ScrapItem item = _heldItems[i];
                if (item == null)
                {
                    _heldItems.RemoveAt(i);
                    RecalculateCargoMass();
                    continue;
                }

                Rigidbody sRb = item.Rigidbody;
                if (sRb == null) continue;

                // High-precision spring-damper hold
                Vector3 toTarget = holdTargetPos - sRb.position;
                Vector3 targetVelocity = toTarget * config.holdSpringStrength;

                sRb.linearVelocity = targetVelocity;
                sRb.angularVelocity = Vector3.Lerp(sRb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 10f);
            }
        }

        /// <summary>
        /// Rule A — Firing a Held Object (LMB while holding RMB).
        /// </summary>
        private void PuntHeldItems(Ray aimRay, GameBalanceConfig config)
        {
            Vector3 droneVel = droneRb != null ? droneRb.linearVelocity : Vector3.zero;
            Vector3 forwardDir = mainCamera != null ? mainCamera.transform.forward : aimRay.direction;
            float impulse = config != null ? config.puntLaunchImpulse : 20.0f;
            Vector3 launchVel = droneVel + (forwardDir * impulse);

            Vector3 bhPos = blackHoleCenter != null ? blackHoleCenter.position : Vector3.zero;
            float distToHole = Vector3.Distance(transform.position, bhPos);
            bool isHighRisk = distToHole < 16f;

            for (int i = 0; i < _heldItems.Count; i++)
            {
                ScrapItem item = _heldItems[i];
                if (item == null) continue;

                item.SetState(ScrapState.Launched);
                item.pullImmunityTimer = 0.65f;
                item.ballisticLaunchTimer = 0.85f;

                if (item.Rigidbody != null)
                {
                    item.Rigidbody.linearVelocity = launchVel;
                    item.Rigidbody.angularVelocity = Random.insideUnitSphere * 4f;
                }

                if (isHighRisk)
                {
                    item.ApplyRiskBonus(2.5f);
                }
            }

            _heldItems.Clear();
            RecalculateCargoMass();

            // Set lock: ignore RMB until player releases RMB
            _requireRmbRelease = true;
            _isGrabHeld = false;
            _currentPullTarget = null;

            TriggerBeamFlash(shockwaveColor, 0.12f);
            PlayPuntAnimation();
        }

        /// <summary>
        /// Rule B — Dry Punt / Direct Push WITHOUT holding RMB (LMB alone).
        /// </summary>
        private void PerformDryPunt(Ray aimRay, GameBalanceConfig config)
        {
            Vector3 origin = mainCamera != null ? mainCamera.transform.position : transform.position;
            Vector3 direction = mainCamera != null ? mainCamera.transform.forward : aimRay.direction;
            float impulse = config != null ? config.puntLaunchImpulse : 20.0f;

            Vector3 droneVel = droneRb != null ? droneRb.linearVelocity : Vector3.zero;
            Vector3 launchVel = droneVel + (direction * impulse);

            Vector3 bhPos = blackHoleCenter != null ? blackHoleCenter.position : Vector3.zero;
            float distToHole = Vector3.Distance(transform.position, bhPos);
            bool isHighRisk = distToHole < 16f;

            HashSet<ScrapItem> hitScraps = new HashSet<ScrapItem>();

            // Overlap query centered in front of camera
            Vector3 queryCenter = origin + (direction * 3.5f);
            Collider[] overlapHits = UnityEngine.Physics.OverlapSphere(queryCenter, 3.8f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapHits.Length; i++)
            {
                ScrapItem scrap = overlapHits[i].GetComponentInParent<ScrapItem>();
                if (scrap != null && scrap.CurrentState != ScrapState.Consumed && scrap.CurrentState != ScrapState.FadingOut)
                {
                    hitScraps.Add(scrap);
                }
            }

            // Also check along camera ray for distant targets within dry punt range
            float dryRange = config != null && config.dryPuntRange > 0f ? config.dryPuntRange : 8.5f;
            float dryRadius = config != null && config.dryPuntRadius > 0f ? config.dryPuntRadius : 2.2f;
            RaycastHit[] castHits = UnityEngine.Physics.SphereCastAll(origin, dryRadius, direction, dryRange, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < castHits.Length; i++)
            {
                ScrapItem scrap = castHits[i].collider.GetComponentInParent<ScrapItem>();
                if (scrap != null && scrap.CurrentState != ScrapState.Consumed && scrap.CurrentState != ScrapState.FadingOut)
                {
                    hitScraps.Add(scrap);
                }
            }

            foreach (var scrap in hitScraps)
            {
                scrap.SetState(ScrapState.Launched);
                scrap.pullImmunityTimer = 0.5f;
                scrap.ballisticLaunchTimer = 0.85f;

                if (scrap.Rigidbody != null)
                {
                    scrap.Rigidbody.linearVelocity = launchVel;
                    scrap.Rigidbody.angularVelocity = Random.insideUnitSphere * 4f;
                }

                if (isHighRisk)
                {
                    scrap.ApplyRiskBonus(2.5f);
                }
            }

            TriggerBeamFlash(shockwaveColor, 0.12f);
            PlayPuntAnimation();
        }

        public void PlayPuntAnimation()
        {
            if (_puntAnimCoroutine != null)
            {
                StopCoroutine(_puntAnimCoroutine);
            }
            _puntAnimCoroutine = StartCoroutine(PuntAnimationRoutine());
        }

        private IEnumerator PuntAnimationRoutine()
        {
            // 1) Setup RoboArmVisual kick: backward by -0.25f on Z, angle slightly upward (-12f on X)
            Vector3 kickPos = new Vector3(0f, 0.05f, -0.25f);
            Quaternion kickRot = Quaternion.Euler(-12f, 0f, 0f);

            // 2) Setup PuntShockwaveRing
            Vector3 ringStartPos = new Vector3(0f, 0.05f, 0.75f);
            Vector3 ringEndPos = ringStartPos + new Vector3(0f, 0f, 2.5f);
            Vector3 ringStartScale = new Vector3(0.2f, 0.05f, 0.2f);
            Vector3 ringEndScale = new Vector3(2.2f, 0.05f, 2.2f);

            if (puntShockwaveRing == null && roboArmVisual != null)
            {
                puntShockwaveRing = roboArmVisual.Find("PuntShockwaveRing");
            }

            if (puntShockwaveRing != null)
            {
                puntShockwaveRing.gameObject.SetActive(true);
                puntShockwaveRing.localPosition = ringStartPos;
                puntShockwaveRing.localRotation = Quaternion.Euler(90f, 0f, 0f);
                puntShockwaveRing.localScale = ringStartScale;
                SetShockwaveAlpha(0.85f);
            }

            const float kickDuration = 0.22f;
            const float ringDuration = 0.18f;
            float elapsed = 0f;

            while (elapsed < kickDuration)
            {
                elapsed += Time.deltaTime;
                float tKick = Mathf.Clamp01(elapsed / kickDuration);
                float tRing = Mathf.Clamp01(elapsed / ringDuration);

                // Smooth ease out return to rest
                float easeReturn = Mathf.SmoothStep(0f, 1f, tKick);
                _armRecoilOffset = Vector3.Lerp(kickPos, Vector3.zero, easeReturn);
                _armRecoilRot = Quaternion.Slerp(kickRot, Quaternion.identity, easeReturn);

                // Shockwave ring animation
                if (puntShockwaveRing != null && tRing <= 1.0f)
                {
                    puntShockwaveRing.localPosition = Vector3.Lerp(ringStartPos, ringEndPos, tRing);
                    puntShockwaveRing.localScale = Vector3.Lerp(ringStartScale, ringEndScale, tRing);
                    float alpha = Mathf.Lerp(0.85f, 0f, tRing);
                    SetShockwaveAlpha(alpha);

                    if (tRing >= 1.0f)
                    {
                        puntShockwaveRing.gameObject.SetActive(false);
                    }
                }

                yield return null;
            }

            if (puntShockwaveRing != null)
            {
                puntShockwaveRing.gameObject.SetActive(false);
            }

            _armRecoilOffset = Vector3.zero;
            _armRecoilRot = Quaternion.identity;
            _puntAnimCoroutine = null;
        }

        private void SetShockwaveAlpha(float alpha)
        {
            if (puntShockwaveRing == null) return;
            var mr = puntShockwaveRing.GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (_shockwavePropertyBlock == null) _shockwavePropertyBlock = new MaterialPropertyBlock();
            mr.GetPropertyBlock(_shockwavePropertyBlock);
            Color baseColor = shockwaveColor;
            baseColor.a = alpha;
            _shockwavePropertyBlock.SetColor("_BaseColor", baseColor);
            _shockwavePropertyBlock.SetColor("_Color", baseColor);
            _shockwavePropertyBlock.SetColor("_EmissionColor", shockwaveColor * (alpha * 2.5f));
            mr.SetPropertyBlock(_shockwavePropertyBlock);
        }

        private void ReleaseAllHeld()
        {
            for (int i = 0; i < _heldItems.Count; i++)
            {
                if (_heldItems[i] != null)
                {
                    _heldItems[i].SetState(ScrapState.Floating);
                }
            }
            _heldItems.Clear();
            RecalculateCargoMass();
        }

        private void RecalculateCargoMass()
        {
            float totalMass = 0f;
            for (int i = 0; i < _heldItems.Count; i++)
            {
                if (_heldItems[i] != null) totalMass += _heldItems[i].Mass;
            }

            if (droneController != null)
            {
                droneController.CurrentCargoMass = totalMass;
            }
        }

        private void UpdateCrosshairStatus()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null || UIManager.Instance == null) return;

            bool hasTarget = false;
            if (_heldItems.Count > 0)
            {
                hasTarget = true;
            }
            else
            {
                Ray aimRay = GetAimRay();
                Vector3 dronePos = droneRb != null ? droneRb.position : transform.position;
                float checkRange = config.isMagnetUnlocked ? config.magnetPullRange : config.grabRangeBase;

                Collider[] hits = UnityEngine.Physics.OverlapSphere(dronePos, checkRange, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < hits.Length; i++)
                {
                    ScrapItem scrap = hits[i].GetComponentInParent<ScrapItem>();
                    if (scrap != null && scrap.CurrentState != ScrapState.Consumed && scrap.CurrentState != ScrapState.FadingOut && !scrap.IsPullImmune && scrap.pullImmunityTimer <= 0f && scrap.ballisticLaunchTimer <= 0f)
                    {
                        Vector3 toScrap = scrap.transform.position - dronePos;
                        float angle = Vector3.Angle(aimRay.direction, toScrap.normalized);
                        float maxAngle = config.isMagnetUnlocked ? config.magnetConeAngle : 35f;

                        if (angle <= maxAngle)
                        {
                            hasTarget = true;
                            break;
                        }
                    }
                }
            }

            bool isHoldingObserved = false;
            for (int i = 0; i < _heldItems.Count; i++)
            {
                if (_heldItems[i] != null && _heldItems[i].IsObserved)
                {
                    isHoldingObserved = true;
                    break;
                }
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.SetCrosshairState(hasTarget, _heldItems.Count > 0);
                UIManager.Instance.SetObservedNotice(isHoldingObserved, "OBSERVED TARGET GRABBED - x3 VALUE!");
            }
        }

        private void UpdateRoboArmVisuals()
        {
            if (roboArmVisual == null) return;

            bool isHoldingObserved = false;
            for (int i = 0; i < _heldItems.Count; i++)
            {
                if (_heldItems[i] != null && _heldItems[i].IsObserved)
                {
                    isHoldingObserved = true;
                    break;
                }
            }

            // Inward rotation & subtle mechanical vibration toward HoldPoint when RMB pulling/holding
            Vector3 basePos = _armBaseLocalPos;
            Quaternion baseRot = _armBaseLocalRot;

            if (_isGrabHeld)
            {
                basePos = _armGrabLocalPos;
                baseRot = _armGrabLocalRot;

                // High-frequency subtle tractor-beam vibration
                float vibFreq = Time.time * 42f;
                Vector3 vibration = new Vector3(
                    Mathf.Sin(vibFreq) * 0.003f,
                    Mathf.Cos(vibFreq * 1.3f) * 0.002f,
                    Mathf.Sin(vibFreq * 0.7f) * 0.004f
                );
                basePos += vibration;
            }

            Vector3 targetLocalPos = basePos + _armRecoilOffset;
            Quaternion targetLocalRot = baseRot * _armRecoilRot;

            roboArmVisual.localPosition = Vector3.Lerp(roboArmVisual.localPosition, targetLocalPos, Time.deltaTime * 18f);
            roboArmVisual.localRotation = Quaternion.Slerp(roboArmVisual.localRotation, targetLocalRot, Time.deltaTime * 16f);

            // Animate claw prongs
            if (_clawLeft != null && _clawRight != null)
            {
                float pinchAngle = _isGrabHeld ? 18f : 0f;
                _clawLeft.localRotation = Quaternion.Slerp(_clawLeft.localRotation, Quaternion.Euler(0f, -pinchAngle, 0f), Time.deltaTime * 15f);
                _clawRight.localRotation = Quaternion.Slerp(_clawRight.localRotation, Quaternion.Euler(0f, pinchAngle, 0f), Time.deltaTime * 15f);
            }

            // Arm indicator light animation when holding observed scrap
            if (armIndicatorLight != null)
            {
                if (isHoldingObserved)
                {
                    armIndicatorLight.enabled = true;
                    float pulse = (Mathf.Sin(Time.time * 9f) + 1f) * 0.5f;
                    armIndicatorLight.color = observedArmGlowColor;
                    armIndicatorLight.intensity = Mathf.Lerp(2.5f, 5.5f, pulse);
                }
                else
                {
                    armIndicatorLight.enabled = false;
                }
            }

            // Animate claw prongs material property block
            if (_clawLeftRenderer != null || _clawRightRenderer != null)
            {
                if (_armPropertyBlock == null) _armPropertyBlock = new MaterialPropertyBlock();
                if (isHoldingObserved)
                {
                    float pulse = (Mathf.Sin(Time.time * 9f) + 1f) * 0.5f;
                    Color glowColor = Color.Lerp(observedArmGlowColor, Color.white, pulse * 0.4f);
                    _armPropertyBlock.SetColor("_BaseColor", glowColor);
                    _armPropertyBlock.SetColor("_Color", glowColor);
                    _armPropertyBlock.SetColor("_EmissionColor", observedArmGlowColor * 2.5f);
                }
                else
                {
                    _armPropertyBlock.SetColor("_BaseColor", _defaultClawColor);
                    _armPropertyBlock.SetColor("_Color", _defaultClawColor);
                    _armPropertyBlock.SetColor("_EmissionColor", Color.black);
                }
                if (_clawLeftRenderer != null) _clawLeftRenderer.SetPropertyBlock(_armPropertyBlock);
                if (_clawRightRenderer != null) _clawRightRenderer.SetPropertyBlock(_armPropertyBlock);
            }
        }

        private void UpdateBeamVisuals()
        {
            if (magnetBeamLine == null) return;

            if (_beamFadeTimer > 0f)
            {
                _beamFadeTimer -= Time.deltaTime;
                magnetBeamLine.enabled = true;
                return;
            }

            if (_isGrabHeld && _currentPullTarget != null && _heldItems.Count == 0)
            {
                magnetBeamLine.enabled = true;
                Vector3 startPos = roboArmVisual != null ? roboArmVisual.position : transform.position;
                magnetBeamLine.SetPosition(0, startPos);
                magnetBeamLine.SetPosition(1, _currentPullTarget.transform.position);
                magnetBeamLine.startColor = magnetBeamColor;
                magnetBeamLine.endColor = magnetBeamColor;
            }
            else
            {
                magnetBeamLine.enabled = false;
            }
        }

        private void TriggerBeamFlash(Color color, float duration)
        {
            if (magnetBeamLine == null) return;

            Ray aimRay = GetAimRay();
            Vector3 startPos = roboArmVisual != null ? roboArmVisual.position : transform.position;
            Vector3 endPos = startPos + (aimRay.direction * 18f);

            magnetBeamLine.enabled = true;
            magnetBeamLine.startColor = color;
            magnetBeamLine.endColor = color;
            magnetBeamLine.SetPosition(0, startPos);
            magnetBeamLine.SetPosition(1, endPos);
            _beamFadeTimer = duration;
        }

        private void OnDrawGizmosSelected()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float grabRange = config != null ? config.grabRangeBase : 4.5f;
            float magRange = config != null ? config.magnetPullRange : 18f;
            float dryRange = config != null && config.dryPuntRange > 0f ? config.dryPuntRange : 8.5f;

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, grabRange);

            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, magRange);

            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            Ray aimRay = GetAimRay();
            Gizmos.DrawLine(aimRay.origin, aimRay.origin + aimRay.direction * dryRange);
        }
    }
}
