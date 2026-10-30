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
        [SerializeField] private Color shockwaveColor = new Color(1f, 0.8f, 0.1f, 1f);

        // Runtime state
        private readonly List<ScrapItem> _heldItems = new List<ScrapItem>();
        private ScrapItem _currentPullTarget;
        private bool _isGrabHeld = false;
        private float _beamFadeTimer = 0f;

        public IReadOnlyList<ScrapItem> HeldItems => _heldItems;
        public int HeldCount => _heldItems.Count;
        public bool IsHolding => _heldItems.Count > 0;
        public Transform HoldPoint => holdPoint;

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
                return;
            }

            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            _isGrabHeld = (mouse != null && mouse.rightButton.isPressed)
                       || (keyboard != null && keyboard.eKey.isPressed);

            bool isFirePressed = mouse != null && mouse.leftButton.wasPressedThisFrame;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null) return;

            Ray aimRay = GetAimRay();
            Vector3 holdTargetPos = GetHoldTargetPosition(aimRay, config);

            // Position dynamic HoldPoint
            if (holdPoint != null)
            {
                holdPoint.position = holdTargetPos;
                holdPoint.rotation = Quaternion.LookRotation(aimRay.direction, Vector3.up);
            }

            // 1. Fire / Punt Logic (LMB)
            if (isFirePressed)
            {
                if (_heldItems.Count > 0)
                {
                    PuntHeldItems(aimRay, config);
                }
                else if (config.isMagnetUnlocked)
                {
                    PerformShockwavePunt(aimRay, config);
                }
            }
            // 2. Grab / Reel Logic (RMB or E)
            else if (_isGrabHeld)
            {
                if (_heldItems.Count < config.maxHeldObjects)
                {
                    if (!config.isMagnetUnlocked)
                    {
                        PerformBaseRoboArmGrab(aimRay, holdTargetPos, config);
                    }
                    else
                    {
                        PerformMagnetPull(aimRay, holdTargetPos, config);
                    }
                }
            }
            // 3. Release when button released
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
                return mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            return new Ray(transform.position, transform.forward);
        }

        private Vector3 GetHoldTargetPosition(Ray aimRay, GameBalanceConfig config)
        {
            Vector3 dronePos = droneRb != null ? droneRb.position : transform.position;
            return dronePos + (aimRay.direction * config.holdPointDistance);
        }

        private void PerformBaseRoboArmGrab(Ray aimRay, Vector3 holdTargetPos, GameBalanceConfig config)
        {
            Vector3 dronePos = droneRb != null ? droneRb.position : transform.position;
            Collider[] hits = UnityEngine.Physics.OverlapSphere(dronePos, config.grabRangeBase);

            ScrapItem bestCandidate = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                ScrapItem scrap = hits[i].GetComponentInParent<ScrapItem>();
                if (scrap == null || _heldItems.Contains(scrap) || scrap.CurrentState == ScrapState.Consumed)
                    continue;

                Vector3 toScrap = scrap.transform.position - dronePos;
                // Check if in front of camera aim
                if (Vector3.Dot(aimRay.direction, toScrap.normalized) > 0.35f)
                {
                    float dist = toScrap.sqrMagnitude;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestCandidate = scrap;
                    }
                }
            }

            if (bestCandidate != null)
            {
                AttachHeldItem(bestCandidate);
            }
        }

        private void PerformMagnetPull(Ray aimRay, Vector3 holdTargetPos, GameBalanceConfig config)
        {
            Vector3 dronePos = droneRb != null ? droneRb.position : transform.position;
            Collider[] hits = UnityEngine.Physics.OverlapSphere(dronePos, config.magnetPullRange);

            ScrapItem bestCandidate = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                ScrapItem scrap = hits[i].GetComponentInParent<ScrapItem>();
                if (scrap == null || _heldItems.Contains(scrap) || scrap.CurrentState == ScrapState.Consumed)
                    continue;

                Vector3 toScrap = scrap.transform.position - dronePos;
                float dist = toScrap.magnitude;
                if (dist < 0.001f) continue;

                Vector3 dir = toScrap / dist;
                float angle = Vector3.Angle(aimRay.direction, dir);

                if (angle <= config.magnetConeAngle)
                {
                    // Pull candidate towards HoldPoint
                    Rigidbody sRb = scrap.Rigidbody;
                    if (sRb != null)
                    {
                        Vector3 toHold = holdTargetPos - sRb.position;
                        float distToHold = toHold.magnitude;
                        Vector3 pullDir = distToHold > 0.001f ? toHold / distToHold : Vector3.zero;

                        // Magnetic attraction force
                        sRb.AddForce(pullDir * config.magnetPullForce, ForceMode.Acceleration);
                        sRb.linearVelocity = Vector3.Lerp(sRb.linearVelocity, pullDir * 16f, Time.fixedDeltaTime * 4f);

                        // Capture if close enough to HoldPoint
                        if (distToHold <= 1.5f)
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

        private void PuntHeldItems(Ray aimRay, GameBalanceConfig config)
        {
            Vector3 droneVel = droneRb != null ? droneRb.linearVelocity : Vector3.zero;
            Vector3 launchVel = droneVel + (aimRay.direction * config.puntLaunchImpulse);

            Vector3 bhPos = blackHoleCenter != null ? blackHoleCenter.position : Vector3.zero;
            float distToHole = Vector3.Distance(transform.position, bhPos);
            bool isHighRisk = distToHole < 16f;

            for (int i = 0; i < _heldItems.Count; i++)
            {
                ScrapItem item = _heldItems[i];
                if (item == null) continue;

                item.SetState(ScrapState.Launched);

                if (item.Rigidbody != null)
                {
                    item.Rigidbody.linearVelocity = launchVel;
                    item.Rigidbody.angularVelocity = Random.insideUnitSphere * 3f;
                }

                if (isHighRisk)
                {
                    item.ApplyRiskBonus(2.5f);
                }
            }

            _heldItems.Clear();
            RecalculateCargoMass();
            TriggerBeamFlash(shockwaveColor, 0.12f);
        }

        private void PerformShockwavePunt(Ray aimRay, GameBalanceConfig config)
        {
            Vector3 dronePos = droneRb != null ? droneRb.position : transform.position;
            Vector3 droneVel = droneRb != null ? droneRb.linearVelocity : Vector3.zero;
            Vector3 launchVel = droneVel + (aimRay.direction * config.puntLaunchImpulse);

            Vector3 bhPos = blackHoleCenter != null ? blackHoleCenter.position : Vector3.zero;
            float distToHole = Vector3.Distance(transform.position, bhPos);
            bool isHighRisk = distToHole < 16f;

            Collider[] hits = UnityEngine.Physics.OverlapSphere(dronePos, config.magnetPullRange * 0.75f);
            int puntedCount = 0;

            for (int i = 0; i < hits.Length; i++)
            {
                ScrapItem scrap = hits[i].GetComponentInParent<ScrapItem>();
                if (scrap == null || scrap.CurrentState == ScrapState.Consumed) continue;

                Vector3 toScrap = scrap.transform.position - dronePos;
                float angle = Vector3.Angle(aimRay.direction, toScrap.normalized);

                if (angle <= config.magnetConeAngle * 1.5f)
                {
                    scrap.SetState(ScrapState.Launched);
                    if (scrap.Rigidbody != null)
                    {
                        scrap.Rigidbody.linearVelocity = launchVel;
                        scrap.Rigidbody.angularVelocity = Random.insideUnitSphere * 4f;
                    }
                    if (isHighRisk)
                    {
                        scrap.ApplyRiskBonus(2.5f);
                    }
                    puntedCount++;
                }
            }

            TriggerBeamFlash(shockwaveColor, 0.15f);
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

                Collider[] hits = UnityEngine.Physics.OverlapSphere(dronePos, checkRange);
                for (int i = 0; i < hits.Length; i++)
                {
                    ScrapItem scrap = hits[i].GetComponentInParent<ScrapItem>();
                    if (scrap != null && scrap.CurrentState != ScrapState.Consumed)
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

            UIManager.Instance.SetCrosshairState(hasTarget, _heldItems.Count > 0);
        }

        private void UpdateRoboArmVisuals()
        {
            if (roboArmVisual == null) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            Vector3 targetLookPos = holdPoint != null 
                ? holdPoint.position 
                : transform.position + (transform.forward * (config != null ? config.holdPointDistance : 3.2f));

            roboArmVisual.LookAt(targetLookPos);
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

            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, grabRange);

            Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, magRange);
        }
    }
}
