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
        [Tooltip("Damping factor for cargo weight penalty on acceleration.")]
        [SerializeField] private float cargoMassPenaltyFactor = 0.05f;

        [Header("References")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform tetherPoint;

        [Header("Runtime State")]
        [SerializeField] private float currentCargoMass = 0f;
        [SerializeField] private bool isControlEnabled = true;
        [SerializeField] private bool isBraking = false;
        [SerializeField] private float currentPitch = 0f;
        [SerializeField] private float currentYaw = 0f;

        private Vector2 _moveInput;
        private float _verticalInput;
        private float _dashTimer = 0f;
        private float _lastDashTime = -999f;

        public float CurrentCargoMass
        {
            get => currentCargoMass;
            set => currentCargoMass = Mathf.Max(0f, value);
        }

        public bool IsControlEnabled => isControlEnabled;
        public bool IsDashing => _dashTimer > 0f;
        public bool IsBoosting => IsDashing;
        public bool IsBraking => isBraking;
        public float Pitch => currentPitch;
        public float Yaw => currentYaw;

        public float DashCooldownTimer
        {
            get
            {
                GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
                float cooldown = config != null ? config.dashCooldown : 0.9f;
                return Mathf.Max(0f, cooldown - (Time.time - _lastDashTime));
            }
        }
        public float BoostCooldownTimer => DashCooldownTimer;
        public Transform TetherPoint => tetherPoint;
        public Rigidbody Rigidbody => rb;
        public Camera MainCamera => mainCamera;

        public void SetControlEnabled(bool enabled)
        {
            isControlEnabled = enabled;
            if (!enabled)
            {
                _moveInput = Vector2.zero;
                _verticalInput = 0f;
                _dashTimer = 0f;
                isBraking = false;
            }
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.position = position;
            currentYaw = rotation.eulerAngles.y;
            currentPitch = 0f;
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            if (mainCamera != null)
            {
                mainCamera.transform.localRotation = Quaternion.identity;
            }

            if (rb != null)
            {
                rb.position = position;
                rb.rotation = Quaternion.Euler(0f, currentYaw, 0f);
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

            // Freeze physics torque to keep Roll and Pitch strictly stabilized under player control
            if (rb != null)
            {
                rb.freezeRotation = true;
            }

            if (mainCamera == null)
            {
                mainCamera = GetComponentInChildren<Camera>();
            }
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Start()
        {
            currentYaw = transform.eulerAngles.y;
            currentPitch = 0f;

            if (mainCamera != null)
            {
                mainCamera.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }

            LockCursor();
        }

        private void OnEnable()
        {
            LockCursor();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && isControlEnabled)
            {
                LockCursor();
            }
        }

        private void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (OrbitalPanopticon.Core.UpgradeTreeManager.IsTreeOpen)
            {
                _moveInput = Vector2.zero;
                _verticalInput = 0f;
                isBraking = false;
                return;
            }

            if (_dashTimer > 0f)
            {
                _dashTimer -= Time.deltaTime;
            }

            HandleMouseLook();
            ReadInput();
        }

        private void FixedUpdate()
        {
            if (OrbitalPanopticon.Core.UpgradeTreeManager.IsTreeOpen) return;

            UpdateDampingAndBrakes();
            ApplyMovementPhysics();

            // Enforce zero physics angular velocity to eliminate collision tumble
            if (rb != null && !rb.isKinematic)
            {
                rb.angularVelocity = Vector3.zero;
            }
        }

        private void HandleMouseLook()
        {
            if (!isControlEnabled || OrbitalPanopticon.Core.UpgradeTreeManager.IsTreeOpen) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            // Re-acquire cursor lock on click if lost
            if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            {
                LockCursor();
            }

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float sens = config != null ? config.mouseSensitivity : 1.8f;

            Vector2 mouseDelta = mouse.delta.ReadValue();
            float mouseX = mouseDelta.x * sens * 0.1f;
            float mouseY = mouseDelta.y * sens * 0.1f;

            // Yaw: Horizontal turn around world Up
            currentYaw += mouseX;
            transform.rotation = Quaternion.Euler(0f, currentYaw, 0f);

            // Pitch: Vertical look clamped [-80, +80], Roll strictly zero
            currentPitch = Mathf.Clamp(currentPitch - mouseY, -80f, 80f);
            if (mainCamera != null)
            {
                mainCamera.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
            }
        }

        private void ReadInput()
        {
            _moveInput = Vector2.zero;
            _verticalInput = 0f;
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

            // Vertical thrust UP is strictly Space; Vertical thrust DOWN is strictly LeftControl (or C).
            // KeyCode.E and KeyCode.Q are strictly removed to eliminate input conflicts.
            // Using New Input System (Keyboard.current) to avoid InvalidOperationException.
            bool up = keyboard.spaceKey.isPressed;
            bool down = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;

            if (up && !down) _verticalInput += 1f;
            if (down && !up) _verticalInput -= 1f;

            // Retro-Braking / Inertia match: X or holding both Up+Down
            bool xBrake = keyboard.xKey.isPressed;
            if (xBrake || (up && down))
            {
                isBraking = true;
            }

            // Directional Dash on Left Shift
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float cooldown = config != null ? config.dashCooldown : 0.9f;
            float impulse = config != null ? config.dashImpulse : 22f;
            float duration = config != null ? config.dashDuration : 0.22f;

            if (keyboard.leftShiftKey.wasPressedThisFrame && Time.time - _lastDashTime >= cooldown)
            {
                Transform camTransform = mainCamera != null ? mainCamera.transform : transform;
                Vector3 camForward = camTransform.forward;
                Vector3 camRight = camTransform.right;

                // Calculate desired direction from active movement keys
                Vector3 inputDir = (camForward * _moveInput.y) + (camRight * _moveInput.x) + (Vector3.up * _verticalInput);
                Vector3 dashDir = inputDir.sqrMagnitude > 0.01f ? inputDir.normalized : camForward;

                _lastDashTime = Time.time;
                _dashTimer = duration;

                if (rb != null)
                {
                    rb.linearVelocity = dashDir * impulse;
                }
            }
        }

        private void UpdateDampingAndBrakes()
        {
            if (rb == null || rb.isKinematic) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float spaceDamping = config != null ? config.spaceLinearDamping : 0.08f;
            float brakeDamping = config != null ? config.retroBrakeDamping : 3.5f;
            float pacing = config != null ? (config.globalSimulationSpeed / 0.5f) : 1.0f;
            float baseAcc = (config != null ? config.baseThrusterAcceleration : 7.5f) * pacing;

            if (isBraking)
            {
                rb.linearDamping = brakeDamping;

                // Active retro-thrust countering remaining velocity for a crisp stop
                if (rb.linearVelocity.sqrMagnitude > 0.05f)
                {
                    rb.AddForce(-rb.linearVelocity.normalized * (baseAcc * 1.5f), ForceMode.Acceleration);
                }
                else
                {
                    rb.linearVelocity = Vector3.zero;
                }
            }
            else
            {
                rb.linearDamping = spaceDamping;
            }
        }

        private void ApplyMovementPhysics()
        {
            if (rb == null || rb.isKinematic || !isControlEnabled) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float pacing = config != null ? (config.globalSimulationSpeed / 0.5f) : 1.0f;
            float baseAcc = (config != null ? config.baseThrusterAcceleration : 7.5f) * pacing;
            float vertAcc = (config != null ? config.verticalThrusterAcceleration : 6.0f) * pacing;
            float maxSpeed = (config != null ? config.maxThrusterSpeed : 9.0f) * pacing;
            float burstImpulse = (config != null ? config.boostImpulse : 16f) * pacing;

            Transform camTransform = mainCamera != null ? mainCamera.transform : transform;

            // Fly Where You Look: Forward is directly along camera aim vector (including vertical pitch)
            Vector3 camForward = camTransform.forward;
            Vector3 camRight = camTransform.right;

            Vector3 desiredDir = (camForward * _moveInput.y) + (camRight * _moveInput.x);
            Vector3 verticalThrust = Vector3.up * _verticalInput;

            // Cargo penalty calculation
            float cargoPenaltyMultiplier = 1f / (1f + (currentCargoMass * cargoMassPenaltyFactor));
            Vector3 desiredAccel = ((desiredDir * baseAcc) + (verticalThrust * vertAcc)) * cargoPenaltyMultiplier;

            // Smart Soft Cap: bypass while dashing so directional dash jump feels snappy and unrestrained
            if (_dashTimer <= 0f)
            {
                float currentSpeed = rb.linearVelocity.magnitude;
                if (currentSpeed > maxSpeed && desiredAccel.sqrMagnitude > 0.001f)
                {
                    Vector3 velDir = rb.linearVelocity / currentSpeed;
                    float forwardComponent = Vector3.Dot(desiredAccel, velDir);
                    if (forwardComponent > 0f)
                    {
                        desiredAccel -= velDir * forwardComponent;
                    }
                }
            }

            if (desiredAccel.sqrMagnitude > 0.001f && !isBraking)
            {
                rb.AddForce(desiredAccel, ForceMode.Acceleration);
            }
        }
    }
}
