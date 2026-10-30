using UnityEngine;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Gameplay
{
    public enum ScrapState
    {
        Floating,  // Freely drifting in space, minimal black hole pull (0.15x)
        Tethered,  // Legacy tethered / captured
        Held,      // Captured and held by gravity manipulator / robo-arm
        Launched,  // Released/launched towards the hole (full 1.0x gravity & swirl)
        Consumed,  // Being absorbed by event horizon
        FadingOut  // Escaped beyond max boundary, smoothly dissolving
    }

    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(GravityBody))]
    [DisallowMultipleComponent]
    public class ScrapItem : MonoBehaviour
    {
        [Header("Data Configuration")]
        [SerializeField] private ScrapData scrapData;

        [Header("State")]
        [SerializeField] private ScrapState currentState = ScrapState.Floating;
        [SerializeField] private bool isInAccretionZone = false;

        [Header("Gravity Multipliers")]
        [SerializeField] private float floatingGravityMultiplier = 0.15f;
        [SerializeField] private float launchedGravityMultiplier = 1.0f;

        [Header("Orbital Value Multiplier")]
        [SerializeField] private float orbitalBonusMultiplier = 1.0f;
        [SerializeField] private float bonusMultiplierPerSecond = 0.5f;

        [Header("Visual Feedback")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Renderer mainRenderer;
        [SerializeField] private Color baseColdColor = new Color(0.15f, 0.85f, 1f, 1f); // Cyan
        [SerializeField] private Color hotAccretionColor = new Color(1f, 0.45f, 0.05f, 1f); // Hot orange

        [Header("Observation Feedback")]
        [SerializeField] private bool isObserved = false;
        [SerializeField] private Color observedGoldColor = new Color(1f, 0.84f, 0.05f, 1f); // Radiant Gold

        [Header("Tractor Immunity")]
        [SerializeField] public float pullImmunityTimer = 0f;

        public float PullImmunityTimer
        {
            get => pullImmunityTimer;
            set => pullImmunityTimer = Mathf.Max(0f, value);
        }

        public bool IsPullImmune => pullImmunityTimer > 0f;

        [Header("Accretion Spiral State")]
        [SerializeField] public float currentSpiralRadius = 0f;

        [Header("Ballistic Launch State")]
        [SerializeField] public float ballisticLaunchTimer = 0f;

        public float BallisticLaunchTimer
        {
            get => ballisticLaunchTimer;
            set => ballisticLaunchTimer = Mathf.Max(0f, value);
        }

        public bool IsBallisticFlight => ballisticLaunchTimer > 0f;

        [Header("Components")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private GravityBody gravityBody;

        private MaterialPropertyBlock _propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private float _fadeDuration = 1.8f;
        private float _fadeElapsed = 0f;
        private float _currentAlpha = 1f;

        [Header("Two-Zone Orbit State")]
        [SerializeField] private float homeOrbitRadius = 25f;
        [SerializeField] private bool wasInteractedByPlayer = false;

        public float HomeOrbitRadius
        {
            get => homeOrbitRadius;
            set => homeOrbitRadius = value;
        }

        public bool WasInteractedByPlayer
        {
            get => wasInteractedByPlayer;
            set => wasInteractedByPlayer = value;
        }

        public ScrapState CurrentState => currentState;
        public ScrapData Data => scrapData;
        public float OrbitalBonusMultiplier
        {
            get => orbitalBonusMultiplier;
            set
            {
                orbitalBonusMultiplier = Mathf.Max(1f, value);
                UpdateVisualHeat();
            }
        }

        public void ApplyRiskBonus(float multiplier)
        {
            orbitalBonusMultiplier = Mathf.Max(orbitalBonusMultiplier, orbitalBonusMultiplier * multiplier);
            UpdateVisualHeat();
        }
        public bool IsObserved => isObserved;
        public bool IsTargetedByCamera { get; set; }

        public void SetObserved(bool observed)
        {
            isObserved = observed;
            if (mainRenderer != null)
            {
                if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
                mainRenderer.GetPropertyBlock(_propertyBlock);
                Color c = observed ? observedGoldColor : baseColdColor;
                c.a = _currentAlpha;
                _propertyBlock.SetColor(BaseColorId, c);
                _propertyBlock.SetColor(ColorId, c);
                mainRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        public void ApplyObservationBonus(float multiplier)
        {
            if (isObserved) return;
            isObserved = true;
            orbitalBonusMultiplier *= multiplier;

            if (mainRenderer != null)
            {
                if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
                mainRenderer.GetPropertyBlock(_propertyBlock);
                Color c = observedGoldColor;
                c.a = _currentAlpha;
                _propertyBlock.SetColor(BaseColorId, c);
                _propertyBlock.SetColor(ColorId, c);
                mainRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        public float BaseValue => scrapData != null ? scrapData.BaseValue : 0f;
        public float TotalValue => BaseValue * orbitalBonusMultiplier;
        public float Mass => rb != null ? rb.mass : (scrapData != null ? scrapData.Mass : 1f);
        public bool IsInAccretionZone => isInAccretionZone;
        public Rigidbody Rigidbody => rb;
        public GravityBody GravityBody => gravityBody;

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (gravityBody == null) gravityBody = GetComponent<GravityBody>();
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            if (mainRenderer == null && visualRoot != null)
            {
                mainRenderer = visualRoot.GetComponentInChildren<Renderer>();
            }

            _propertyBlock = new MaterialPropertyBlock();

            if (scrapData != null)
            {
                ApplyScrapData(scrapData);
            }
            else
            {
                UpdateGravityForState();
            }
        }

        /// <summary>
        /// Centralized simulation step invoked exclusively by ScrapSimulationManager.
        /// No individual Update() on ScrapItem instances.
        /// </summary>
        public void CentralizedUpdate(float dt, float bonusPerSec = -1f)
        {
            if (pullImmunityTimer > 0f)
            {
                pullImmunityTimer = Mathf.Max(0f, pullImmunityTimer - dt);
            }

            if (ballisticLaunchTimer > 0f)
            {
                ballisticLaunchTimer = Mathf.Max(0f, ballisticLaunchTimer - dt);
            }

            float rate = bonusPerSec >= 0f ? bonusPerSec : bonusMultiplierPerSecond;
            // When launched into the accretion disk, build bonus multiplier over time
            if (currentState == ScrapState.Launched && isInAccretionZone)
            {
                orbitalBonusMultiplier += rate * dt;
                UpdateVisualHeat();
            }
        }

        public void StartFadeOut(float fadeDuration)
        {
            currentState = ScrapState.FadingOut;
            _fadeDuration = Mathf.Max(0.1f, fadeDuration);
            _fadeElapsed = 0f;
            _currentAlpha = 1f;

            if (gravityBody != null)
            {
                gravityBody.GravityMultiplier = 0f;
            }

            if (rb != null)
            {
                rb.linearDamping = 3f;
                rb.angularDamping = 3f;
            }
        }

        public bool UpdateFadeOut(float dt)
        {
            _fadeElapsed += dt;
            float t = Mathf.Clamp01(_fadeElapsed / _fadeDuration);
            _currentAlpha = Mathf.Lerp(1f, 0f, t);
            ApplyAlpha(_currentAlpha);

            return t >= 1f;
        }

        public void SetAlpha(float alpha)
        {
            _currentAlpha = Mathf.Clamp01(alpha);
            ApplyAlpha(_currentAlpha);
        }

        private void ApplyAlpha(float alpha)
        {
            if (mainRenderer == null) return;
            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            mainRenderer.GetPropertyBlock(_propertyBlock);

            Color currentColor = isObserved ? observedGoldColor : baseColdColor;
            if (currentState == ScrapState.Launched && orbitalBonusMultiplier > 1f)
            {
                float heat = Mathf.Clamp01((orbitalBonusMultiplier - 1.0f) / 3.0f);
                currentColor = Color.Lerp(baseColdColor, hotAccretionColor, heat);
            }

            currentColor.a = alpha;
            _propertyBlock.SetColor(BaseColorId, currentColor);
            _propertyBlock.SetColor(ColorId, currentColor);
            mainRenderer.SetPropertyBlock(_propertyBlock);
        }

        public void ResetForPool()
        {
            currentState = ScrapState.Floating;
            isInAccretionZone = false;
            isObserved = false;
            IsTargetedByCamera = false;
            pullImmunityTimer = 0f;
            currentSpiralRadius = 0f;
            ballisticLaunchTimer = 0f;
            orbitalBonusMultiplier = 1.0f;
            _fadeElapsed = 0f;
            _currentAlpha = 1.0f;
            wasInteractedByPlayer = false;
            homeOrbitRadius = 0f;

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.linearDamping = 0.05f;
                rb.angularDamping = 0.05f;
            }

            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = true;
            }

            if (visualRoot != null)
            {
                visualRoot.localPosition = Vector3.zero;
                visualRoot.localRotation = Quaternion.identity;
                if (scrapData != null)
                {
                    visualRoot.localScale = scrapData.BaseScale;
                }
            }

            ResetVisualHeat();
            UpdateGravityForState();
        }

        public void Initialize(ScrapData data)
        {
            ApplyScrapData(data);
            SetState(ScrapState.Floating);
        }

        public void ApplyScrapData(ScrapData data)
        {
            scrapData = data;
            if (scrapData == null) return;

            if (rb != null)
            {
                rb.mass = scrapData.Mass;
            }

            if (visualRoot != null)
            {
                visualRoot.localScale = scrapData.BaseScale;
            }

            UpdateGravityForState();
        }

        public void SetState(ScrapState newState)
        {
            currentState = newState;
            UpdateGravityForState();

            if (currentState == ScrapState.Held || currentState == ScrapState.Launched)
            {
                wasInteractedByPlayer = true;
            }

            if (currentState != ScrapState.Launched)
            {
                orbitalBonusMultiplier = 1.0f;
                currentSpiralRadius = 0f;
                ballisticLaunchTimer = 0f;
                ResetVisualHeat();
            }
        }

        public void SetInAccretionZone(bool inZone)
        {
            isInAccretionZone = inZone;
        }

        private void UpdateGravityForState()
        {
            if (gravityBody == null) return;

            switch (currentState)
            {
                case ScrapState.Floating:
                    gravityBody.GravityMultiplier = floatingGravityMultiplier;
                    break;
                case ScrapState.Launched:
                    gravityBody.GravityMultiplier = launchedGravityMultiplier;
                    break;
                case ScrapState.Tethered:
                case ScrapState.Held:
                case ScrapState.Consumed:
                case ScrapState.FadingOut:
                    // During tethering / holding / fading / consumption, black hole gravity is suppressed
                    gravityBody.GravityMultiplier = 0f;
                    break;
            }
        }

        private void UpdateVisualHeat()
        {
            if (mainRenderer == null) return;

            // Heat factor scales from 0 (multiplier 1.0) to 1.0 (multiplier >= 4.0)
            float heat = Mathf.Clamp01((orbitalBonusMultiplier - 1.0f) / 3.0f);
            Color currentColor = Color.Lerp(baseColdColor, hotAccretionColor, heat);
            currentColor.a = _currentAlpha;

            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            mainRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BaseColorId, currentColor);
            _propertyBlock.SetColor(ColorId, currentColor);
            mainRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void ResetVisualHeat()
        {
            if (mainRenderer == null) return;

            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
            mainRenderer.GetPropertyBlock(_propertyBlock);
            Color c = baseColdColor;
            c.a = _currentAlpha;
            _propertyBlock.SetColor(BaseColorId, c);
            _propertyBlock.SetColor(ColorId, c);
            mainRenderer.SetPropertyBlock(_propertyBlock);
        }

        private bool IsAccretionTrigger(Collider other)
        {
            if (other == null) return false;
            return other.gameObject.name.IndexOf("Accretion", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsAccretionTrigger(other))
            {
                SetInAccretionZone(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsAccretionTrigger(other))
            {
                SetInAccretionZone(false);
            }
        }
    }
}
