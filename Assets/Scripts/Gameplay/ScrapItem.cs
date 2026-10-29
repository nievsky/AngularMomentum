using UnityEngine;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Gameplay
{
    public enum ScrapState
    {
        Floating,  // Freely drifting in space, minimal black hole pull (0.15x)
        Tethered,  // Captured by player tether beam
        Launched,  // Released/launched towards the hole (full 1.0x gravity & swirl)
        Consumed   // Being absorbed by event horizon
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

        [Header("Components")]
        [SerializeField] private Rigidbody rb;
        [SerializeField] private GravityBody gravityBody;

        private MaterialPropertyBlock _propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

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

        public void ApplyObservationBonus(float multiplier)
        {
            if (isObserved) return;
            isObserved = true;
            orbitalBonusMultiplier *= multiplier;

            if (mainRenderer != null)
            {
                if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
                mainRenderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, observedGoldColor);
                _propertyBlock.SetColor(ColorId, observedGoldColor);
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

        private void Update()
        {
            // When launched into the accretion disk, build bonus multiplier over time
            if (currentState == ScrapState.Launched && isInAccretionZone)
            {
                orbitalBonusMultiplier += bonusMultiplierPerSecond * Time.deltaTime;
                UpdateVisualHeat();
            }
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

            if (currentState != ScrapState.Launched)
            {
                orbitalBonusMultiplier = 1.0f;
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
                    // During tethering, external gravity is suppressed so player maintains control
                    gravityBody.GravityMultiplier = 0f;
                    break;
                case ScrapState.Consumed:
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
            _propertyBlock.SetColor(BaseColorId, baseColdColor);
            _propertyBlock.SetColor(ColorId, baseColdColor);
            mainRenderer.SetPropertyBlock(_propertyBlock);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("AccretionZone") || other.gameObject.name.Contains("Accretion"))
            {
                SetInAccretionZone(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("AccretionZone") || other.gameObject.name.Contains("Accretion"))
            {
                SetInAccretionZone(false);
            }
        }
    }
}
