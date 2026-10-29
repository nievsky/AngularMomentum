using UnityEngine;
using UnityEngine.InputSystem;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Player
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class DroneController : MonoBehaviour
    {
        [Header("Tuning Fallbacks (Overrides from GameBalanceConfig)")]
        [Tooltip("Turn rate for aligning drone heading with movement direction.")]
        [SerializeField] private float turnSpeed = 10f;

        [Tooltip("Damping factor for cargo weight penalty on acceleration.")]
        [SerializeField] private float cargoMassPenaltyFactor = 0.05f;

        [Tooltip("Thrust multiplier applied when holding boost.")]
        [SerializeField] private float boostContinuousMultiplier = 1.6f;

        [Header("References")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform tetherPoint;

        [Header("Runtime State")]
        [SerializeField] private float currentCargoMass = 0f;
        [SerializeField] private bool isControlEnabled = true;
        [SerializeField] private bool isBraking = false;

        private Vector2 _moveInput;
        private float _verticalInput;
        private bool _isBoostHeld;
        private bool _burstRequested;
        private float _lastBurstTime = -999f;
        private Vector3 _lastNonZeroVelocityDir = Vector3.forward;

        public float CurrentCargoMass
        {
            get => currentCargoMass;
            set => currentCargoMass = Mathf.Max(0f, value);
        }

        public bool IsControlEnabled => isControlEnabled;
        public bool IsBoosting => _isBoostHeld;
        public bool IsBraking => isBraking;
        public float BoostCooldownTimer
        {
            get
            {
                GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
                float cooldown = config != null ? config.boostCooldown : 2.0f;
                return Mathf.Max(0f, cooldown - (Time.time - _lastBurstTime));
            }
        }
        public Transform TetherPoint => tetherPoint;
        public Rigidbody Rigidbody => rb;

        public void SetControlEnabled(bool enabled)
        {
            isControlEnabled = enabled;
            if (!enabled)
            {
                _moveInput = Vector2.zero;
                _verticalInput = 0f;
                _isBoostHeld = false;
                _burstRequested = false;
                isBraking = false;
            }
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            transform.rotation = rotation;
            if (rb != null)
            {
                rb.position = position;
                rb.rotation = rotation;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void OnValidate()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
        }

        private void Update()
        {
            ReadInput();
        }

        private void FixedUpdate()
        {
            UpdateDampingAndBrakes();
            ApplyMovementPhysics();
            AlignDroneHeading();
        }

        private void ReadInput()
        {
            _moveInput = Vector2.zero;
            _verticalInput = 0f;
            _isBoostHeld = false;
            isBraking = false;

            if (!isControlEnabled) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Horizontal thrust (WASD)
            if (keyboard.wKey.isPressed) _moveInput.y += 1f;
            if (keyboard.sKey.isPressed) _moveInput.y -= 1f;
            if (keyboard.dKey.isPressed) _moveInput.x += 1f;
            if (keyboard.aKey.isPressed) _moveInput.x -= 1f;
            _moveInput = Vector2.ClampMagnitude(_moveInput, 1f);

            // Vertical thrust (Space / Left Ctrl or E / Q)
            bool space = keyboard.spaceKey.isPressed || keyboard.eKey.isPressed;
            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.qKey.isPressed;

            if (space && !ctrl) _verticalInput += 1f;
            if (ctrl && !space) _verticalInput -= 1f;

            // Retro-Braking / Inertia match: X or C or holding both Space+Ctrl
            if (keyboard.xKey.isPressed || keyboard.cKey.isPressed || (space && ctrl))
            {
                isBraking = true;
            }

            // Boost
            _isBoostHeld = keyboard.leftShiftKey.isPressed;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float cooldown = config != null ? config.boostCooldown : 2.0f;

            if (keyboard.leftShiftKey.wasPressedThisFrame && Time.time - _lastBurstTime >= cooldown)
            {
                _burstRequested = true;
                _lastBurstTime = Time.time;
            }
        }

        private void UpdateDampingAndBrakes()
        {
            if (rb == null) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float spaceDamping = config != null ? config.spaceLinearDamping : 0.08f;
            float brakeDamping = config != null ? config.retroBrakeDamping : 3.5f;

            if (isBraking)
            {
                rb.linearDamping = brakeDamping;

                // Active retro-thrust countering remaining velocity
                if (rb.linearVelocity.sqrMagnitude > 0.05f)
                {
                    float baseAcc = config != null ? config.baseThrusterAcceleration : 14f;
                    rb.AddForce(-rb.linearVelocity.normalized * (baseAcc * 0.8f), ForceMode.Acceleration);
                }
            }
            else
            {
                rb.linearDamping = spaceDamping;
            }
        }

        private void ApplyMovementPhysics()
        {
            if (rb == null || !isControlEnabled) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float baseAcc = config != null ? config.baseThrusterAcceleration : 14f;
            float vertAcc = config != null ? config.verticalThrusterAcceleration : 10f;
            float maxSpeed = config != null ? config.maxThrusterSpeed : 18f;
            float burstImpulse = config != null ? config.boostImpulse : 16f;

            Transform camTransform = mainCamera != null ? mainCamera.transform : transform;

            // Camera-relative directions on horizontal XZ plane
            Vector3 camForward = camTransform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.001f)
            {
                camForward = camTransform.up;
                camForward.y = 0f;
            }
            camForward.Normalize();

            Vector3 camRight = camTransform.right;
            camRight.y = 0f;
            camRight.Normalize();

            Vector3 desiredDir = (camForward * _moveInput.y) + (camRight * _moveInput.x);

            // Cargo penalty calculation
            float cargoPenaltyMultiplier = 1f / (1f + (currentCargoMass * cargoMassPenaltyFactor));
            float currentMultiplier = (_isBoostHeld ? boostContinuousMultiplier : 1f) * cargoPenaltyMultiplier;

            Vector3 desiredAccel = (desiredDir * (baseAcc * currentMultiplier)) + (Vector3.up * (_verticalInput * vertAcc * currentMultiplier));

            // Smart Soft Cap:
            // If current speed exceeds maxThrusterSpeed (e.g. from gravity slingshots),
            // engines do not auto-brake, but only allow perpendicular steering or reverse braking!
            float currentSpeed = rb.linearVelocity.magnitude;
            if (currentSpeed > maxSpeed && desiredAccel.sqrMagnitude > 0.001f)
            {
                Vector3 velDir = rb.linearVelocity / currentSpeed;
                float forwardComponent = Vector3.Dot(desiredAccel, velDir);
                if (forwardComponent > 0f)
                {
                    // Subtract component parallel to forward velocity
                    desiredAccel -= velDir * forwardComponent;
                }
            }

            if (desiredAccel.sqrMagnitude > 0.001f && !isBraking)
            {
                rb.AddForce(desiredAccel, ForceMode.Acceleration);
            }

            // Burst impulse
            if (_burstRequested)
            {
                _burstRequested = false;
                Vector3 burstDir = desiredDir.sqrMagnitude > 0.01f ? desiredDir.normalized : transform.forward;
                if (Mathf.Abs(_verticalInput) > 0.01f)
                {
                    burstDir = (burstDir + Vector3.up * Mathf.Sign(_verticalInput)).normalized;
                }
                rb.AddForce(burstDir * (burstImpulse * cargoPenaltyMultiplier), ForceMode.Impulse);
            }
        }

        private void AlignDroneHeading()
        {
            if (rb == null || !isControlEnabled) return;

            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;

            Vector3 targetHeading = vel.sqrMagnitude > 0.25f ? vel.normalized : Vector3.zero;

            if (targetHeading == Vector3.zero && _moveInput.sqrMagnitude > 0.1f)
            {
                Transform camTransform = mainCamera != null ? mainCamera.transform : transform;
                Vector3 camForward = camTransform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                Vector3 camRight = camTransform.right;
                camRight.y = 0f;
                camRight.Normalize();
                targetHeading = ((camForward * _moveInput.y) + (camRight * _moveInput.x)).normalized;
            }

            if (targetHeading.sqrMagnitude > 0.01f)
            {
                _lastNonZeroVelocityDir = targetHeading;
                Quaternion targetRotation = Quaternion.LookRotation(_lastNonZeroVelocityDir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
            }
        }
    }
}
