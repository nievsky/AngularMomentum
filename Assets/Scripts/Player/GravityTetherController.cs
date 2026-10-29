using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Player
{
    [RequireComponent(typeof(DroneController))]
    [DisallowMultipleComponent]
    public class GravityTetherController : MonoBehaviour
    {
        [Header("Tether Upgrades & Tuning")]
        [Tooltip("Max radius for detecting and latching onto floating scrap.")]
        [SerializeField] private float captureRadius = 8f;

        [Tooltip("Max scrap items the drone can carry simultaneously.")]
        [SerializeField] private int maxCargoCount = 8;

        [Tooltip("Spring pull force pulling scrap toward the tether point.")]
        [SerializeField] private float pullStrength = 40f;

        [Tooltip("Impulse force added to scrap when slingshot launched.")]
        [SerializeField] private float throwForce = 35f;

        [Tooltip("Damping factor to prevent cargo from violently oscillating.")]
        [SerializeField] private float springDamping = 6f;

        [Tooltip("Desired spacing distance between items in the cargo chain.")]
        [SerializeField] private float chainSpacing = 1.2f;

        [Header("High-Risk Bonus Mechanic")]
        [Tooltip("Distance threshold to the black hole for receiving immediate Risk Bonus.")]
        [SerializeField] private float riskDistanceThreshold = 16f;

        [Tooltip("Immediate multiplier boost awarded when launched close to the event horizon.")]
        [SerializeField] private float riskBonusMultiplier = 2.5f;

        [Header("References")]
        [SerializeField] private DroneController droneController;
        [SerializeField] private Rigidbody droneRb;
        [SerializeField] private Transform tetherPoint;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform blackHoleCenter;
        [SerializeField] private Material lineMaterial;

        [Header("Visual Feedback")]
        [SerializeField] private Color tetherColor = new Color(0.15f, 0.9f, 1f, 0.85f);
        [SerializeField] private float lineStartWidth = 0.08f;
        [SerializeField] private float lineEndWidth = 0.04f;

        // Runtime Cargo
        private readonly List<ScrapItem> _tetheredItems = new List<ScrapItem>();
        private readonly List<LineRenderer> _linePool = new List<LineRenderer>();
        private Transform _linesContainer;
        private bool _wasHoldingTether = false;

        public IReadOnlyList<ScrapItem> TetheredItems => _tetheredItems;
        public int TetheredCount => _tetheredItems.Count;
        public int MaxCargoCount => maxCargoCount;

        private void Awake()
        {
            if (droneController == null) droneController = GetComponent<DroneController>();
            if (droneRb == null) droneRb = GetComponent<Rigidbody>();
            if (tetherPoint == null && droneController != null) tetherPoint = droneController.TetherPoint;
            if (mainCamera == null) mainCamera = Camera.main;

            CreateLinePool();
        }

        private void Update()
        {
            HandleInputs();
        }

        private void FixedUpdate()
        {
            ApplyTetherPhysics();
        }

        private void LateUpdate()
        {
            UpdateLineVisuals();
        }

        private void HandleInputs()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            bool isHoldingLmb = mouse != null && mouse.leftButton.isPressed;
            bool wasLmbReleased = mouse != null && mouse.leftButton.wasReleasedThisFrame;
            bool launchKeyPressed = (keyboard != null && keyboard.fKey.wasPressedThisFrame)
                                 || (mouse != null && mouse.rightButton.wasPressedThisFrame);

            // Capture logic while holding LMB
            if (isHoldingLmb)
            {
                if (_tetheredItems.Count < maxCargoCount)
                {
                    ScanAndCaptureScrap();
                }
                _wasHoldingTether = true;
            }

            // Slingshot / Launch logic
            if ((wasLmbReleased && _wasHoldingTether) || launchKeyPressed)
            {
                _wasHoldingTether = false;
                if (_tetheredItems.Count > 0)
                {
                    LaunchAllScrap();
                }
            }

            if (!isHoldingLmb && wasLmbReleased)
            {
                _wasHoldingTether = false;
            }
        }

        private void ScanAndCaptureScrap()
        {
            Collider[] hits = UnityEngine.Physics.OverlapSphere(transform.position, captureRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                if (_tetheredItems.Count >= maxCargoCount) break;

                ScrapItem scrap = hits[i].GetComponentInParent<ScrapItem>();
                if (scrap != null && scrap.CurrentState == ScrapState.Floating && !_tetheredItems.Contains(scrap))
                {
                    AttachScrap(scrap);
                }
            }
        }

        private void AttachScrap(ScrapItem scrap)
        {
            scrap.SetState(ScrapState.Tethered);
            _tetheredItems.Add(scrap);
            RecalculateCargoWeight();
        }

        private void ApplyTetherPhysics()
        {
            if (_tetheredItems.Count == 0) return;

            Vector3 basePoint = tetherPoint != null ? tetherPoint.position : transform.position;

            for (int i = _tetheredItems.Count - 1; i >= 0; i--)
            {
                ScrapItem item = _tetheredItems[i];
                if (item == null)
                {
                    _tetheredItems.RemoveAt(i);
                    RecalculateCargoWeight();
                    continue;
                }

                Rigidbody itemRb = item.Rigidbody;
                if (itemRb == null) continue;

                // Chain link: Item 0 connects to TetherPoint; subsequent items connect to previous cargo item
                Vector3 targetPos = (i == 0) ? basePoint : _tetheredItems[i - 1].transform.position;
                Vector3 toTarget = targetPos - itemRb.position;
                float distance = toTarget.magnitude;
                Vector3 dir = distance > 0.001f ? toTarget / distance : Vector3.zero;

                float targetSpacing = (i == 0) ? 0.6f : chainSpacing;
                float stretch = Mathf.Max(0f, distance - targetSpacing);

                // Spring-damper physics
                Vector3 relVelocity = itemRb.linearVelocity - droneRb.linearVelocity;
                Vector3 springForce = (dir * (stretch * pullStrength)) - (relVelocity * springDamping);

                itemRb.AddForce(springForce, ForceMode.Acceleration);
            }
        }

        public void LaunchAllScrap()
        {
            if (_tetheredItems.Count == 0) return;

            Transform camTransform = mainCamera != null ? mainCamera.transform : transform;
            Vector3 aimDir = camTransform.forward;

            // Check high-risk bonus (close proximity to black hole)
            Vector3 bhPos = blackHoleCenter != null ? blackHoleCenter.position : Vector3.zero;
            float distToHole = Vector3.Distance(transform.position, bhPos);
            bool isHighRisk = distToHole < riskDistanceThreshold;

            Vector3 baseVelocity = droneRb != null ? droneRb.linearVelocity : Vector3.zero;
            Vector3 throwVelocity = baseVelocity + (aimDir * throwForce);

            for (int i = 0; i < _tetheredItems.Count; i++)
            {
                ScrapItem scrap = _tetheredItems[i];
                if (scrap == null) continue;

                scrap.SetState(ScrapState.Launched);

                Rigidbody sRb = scrap.Rigidbody;
                if (sRb != null)
                {
                    sRb.linearVelocity = throwVelocity + (Random.insideUnitSphere * 1.5f);
                    sRb.angularVelocity = Random.insideUnitSphere * 2.0f;
                }

                if (isHighRisk)
                {
                    scrap.ApplyRiskBonus(riskBonusMultiplier);
                }
            }

            _tetheredItems.Clear();
            RecalculateCargoWeight();
        }

        private void RecalculateCargoWeight()
        {
            float totalMass = 0f;
            for (int i = 0; i < _tetheredItems.Count; i++)
            {
                if (_tetheredItems[i] != null)
                {
                    totalMass += _tetheredItems[i].Mass;
                }
            }

            if (droneController != null)
            {
                droneController.CurrentCargoMass = totalMass;
            }
        }

        private void CreateLinePool()
        {
            GameObject containerGo = new GameObject("TetherLines");
            containerGo.transform.SetParent(transform, false);
            _linesContainer = containerGo.transform;

            for (int i = 0; i < maxCargoCount; i++)
            {
                GameObject lineGo = new GameObject($"Line_{i}");
                lineGo.transform.SetParent(_linesContainer, false);
                LineRenderer lr = lineGo.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = lineStartWidth;
                lr.endWidth = lineEndWidth;
                lr.startColor = tetherColor;
                lr.endColor = tetherColor;
                lr.useWorldSpace = true;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                if (lineMaterial != null)
                {
                    lr.sharedMaterial = lineMaterial;
                }

                lineGo.SetActive(false);
                _linePool.Add(lr);
            }
        }

        private void UpdateLineVisuals()
        {
            Vector3 origin = tetherPoint != null ? tetherPoint.position : transform.position;

            for (int i = 0; i < _linePool.Count; i++)
            {
                if (i < _tetheredItems.Count && _tetheredItems[i] != null)
                {
                    _linePool[i].gameObject.SetActive(true);
                    Vector3 prevPoint = (i == 0) ? origin : _tetheredItems[i - 1].transform.position;
                    _linePool[i].SetPosition(0, prevPoint);
                    _linePool[i].SetPosition(1, _tetheredItems[i].transform.position);
                }
                else
                {
                    if (_linePool[i].gameObject.activeSelf)
                    {
                        _linePool[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, captureRadius);

            Vector3 bhPos = blackHoleCenter != null ? blackHoleCenter.position : Vector3.zero;
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.25f);
            Gizmos.DrawWireSphere(bhPos, riskDistanceThreshold);
        }
    }
}
