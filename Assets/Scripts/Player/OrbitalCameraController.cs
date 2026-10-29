using UnityEngine;
using UnityEngine.InputSystem;

namespace OrbitalPanopticon.Player
{
    [DisallowMultipleComponent]
    public class OrbitalCameraController : MonoBehaviour
    {
        [Header("Target & Tracking")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float positionSmoothTime = 0.08f;

        [Header("Orbit & Distance")]
        [SerializeField] private float defaultDistance = 9.0f;
        [SerializeField] private float boostDistance = 12.0f;
        [SerializeField] private float minDistance = 3.0f;
        [SerializeField] private float maxDistance = 25.0f;
        [SerializeField] private float distanceSmoothTime = 0.2f;

        [Header("Field of View")]
        [SerializeField] private float defaultFov = 60f;
        [SerializeField] private float boostFov = 72f;
        [SerializeField] private float fovSmoothTime = 0.25f;

        [Header("Mouse Controls")]
        [SerializeField] private float mouseSensitivityX = 0.2f;
        [SerializeField] private float mouseSensitivityY = 0.2f;
        [SerializeField] private float minPitch = -25f;
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private bool requireRightClickToOrbit = false;

        [Header("References")]
        [SerializeField] private Camera cam;
        [SerializeField] private DroneController droneController;

        // Internal State
        private float _yaw = 0f;
        private float _pitch = 20f;
        private float _currentDistance;
        private float _distanceVelocity;
        private float _fovVelocity;
        private Vector3 _currentVelocity;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        private void Awake()
        {
            if (cam == null)
            {
                cam = GetComponent<Camera>();
            }

            _currentDistance = defaultDistance;

            if (target != null)
            {
                Vector3 toCam = transform.position - (target.position + targetOffset);
                if (toCam.sqrMagnitude > 0.01f)
                {
                    _currentDistance = Mathf.Clamp(toCam.magnitude, minDistance, maxDistance);
                    Quaternion initialRot = Quaternion.LookRotation(toCam);
                    _pitch = initialRot.eulerAngles.x;
                    _yaw = initialRot.eulerAngles.y;
                }
            }
        }

        private void Start()
        {
            if (!requireRightClickToOrbit)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void Update()
        {
            HandleMouseInput();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            UpdateCameraTransform();
            UpdateDynamicFovAndDistance();
        }

        private void HandleMouseInput()
        {
            var mouse = Mouse.current;
            var keyboard = Keyboard.current;

            // Optional unlock cursor with Escape or Alt
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Click inside game to lock again if not requiring right-click
            if (mouse != null && !requireRightClickToOrbit && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (mouse == null) return;

            bool canRotate = !requireRightClickToOrbit 
                ? (Cursor.lockState == CursorLockMode.Locked || mouse.rightButton.isPressed)
                : mouse.rightButton.isPressed;

            if (canRotate)
            {
                Vector2 mouseDelta = mouse.delta.ReadValue();
                _yaw += mouseDelta.x * mouseSensitivityX;
                _pitch -= mouseDelta.y * mouseSensitivityY;
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }

            // Optional zoom with scroll wheel
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                defaultDistance = Mathf.Clamp(defaultDistance - (scroll * 0.005f), minDistance, maxDistance);
            }
        }

        private void UpdateCameraTransform()
        {
            Vector3 focusPoint = target.position + targetOffset;
            Quaternion orbitRotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // Compute ideal position behind target
            Vector3 targetPosition = focusPoint - (orbitRotation * Vector3.forward * _currentDistance);

            // Smooth position tracking
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref _currentVelocity, positionSmoothTime);
            transform.LookAt(focusPoint);
        }

        private void UpdateDynamicFovAndDistance()
        {
            bool isBoosting = droneController != null && droneController.IsBoosting;

            // Dynamic distance
            float targetDist = isBoosting ? boostDistance : defaultDistance;
            _currentDistance = Mathf.SmoothDamp(_currentDistance, targetDist, ref _distanceVelocity, distanceSmoothTime);

            // Dynamic FOV
            if (cam != null)
            {
                float targetFov = isBoosting ? boostFov : defaultFov;
                cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref _fovVelocity, fovSmoothTime);
            }
        }
    }
}
