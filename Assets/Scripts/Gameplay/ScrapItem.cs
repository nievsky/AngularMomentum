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

        [Header("Valuable Scrap (Shader Highlight)")]
        [SerializeField] public bool isValuableScrap = false;
        public bool IsValuableScrap => isValuableScrap;
        [SerializeField] private GameObject valuableRingObject;

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
        [SerializeField] public Vector3 customOrbitAxis = Vector3.up;
        [SerializeField] public bool hasEnteredVortex = false;
        [SerializeField] public bool isKamikazeDelivery = false;

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

        [Header("Valuable Lava Loot VFX")]
        [SerializeField] private Material valuableLavaMaterial;
        [SerializeField] private GameObject valuableEmbersPrefab;

        private Renderer[] _cachedRenderers;
        private Material[] _cachedDefaultMaterials;
        private ParticleSystem _valuableEmbersInstance;

        private MaterialPropertyBlock _propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

        public Material ValuableLavaMaterial
        {
            get => valuableLavaMaterial;
            set => valuableLavaMaterial = value;
        }

        public GameObject ValuableEmbersPrefab
        {
            get => valuableEmbersPrefab;
            set => valuableEmbersPrefab = value;
        }

        private float _fadeDuration = 1.8f;
        private float _fadeElapsed = 0f;
        private float _currentAlpha = 1f;

        [Header("Accretion & Spaghettification VFX")]
        [SerializeField] private ParticleSystem accretionSparksParticles;
        [SerializeField] private GameObject accretionSparksPrefab;

        private float _currentAccretionDepth = 0f;

        public ParticleSystem AccretionSparksParticles
        {
            get => accretionSparksParticles;
            set => accretionSparksParticles = value;
        }

        public GameObject AccretionSparksPrefab
        {
            get => accretionSparksPrefab;
            set => accretionSparksPrefab = value;
        }

        [Header("Superheated Plasma Wake Trail")]
        [SerializeField] private TrailRenderer plasmaWakeTrail;
        [SerializeField] private Material plasmaWakeTrailMaterial;

        public TrailRenderer PlasmaWakeTrail
        {
            get => plasmaWakeTrail;
            set => plasmaWakeTrail = value;
        }

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
        public int Tier => scrapData != null ? scrapData.Tier : 1;
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

        public float BaseValue
        {
            get
            {
                float val = scrapData != null ? scrapData.BaseValue : 10f;
                if (isValuableScrap)
                {
                    var cfg = OrbitalPanopticon.Core.GameManager.GameBalanceConfigInstance;
                    val *= (cfg != null ? cfg.valuableScrapMultiplier : 3.0f);
                }
                return val;
            }
        }

        public float TotalValue => BaseValue * orbitalBonusMultiplier;

        public float Mass
        {
            get
            {
                float m = scrapData != null ? scrapData.Mass : (rb != null ? rb.mass : 1f);
                if (isValuableScrap)
                {
                    var cfg = OrbitalPanopticon.Core.GameManager.GameBalanceConfigInstance;
                    m *= (cfg != null ? cfg.valuableScrapMultiplier : 3.0f);
                }
                return m;
            }
        }

        public bool IsInAccretionZone => isInAccretionZone;
        public Rigidbody Rigidbody => rb;
        public GravityBody GravityBody => gravityBody;

        private void CacheOriginalMaterials()
        {
            if (_cachedDefaultMaterials != null && _cachedDefaultMaterials.Length > 0) return;

            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            Transform root = visualRoot != null ? visualRoot : transform;

            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            var listRends = new System.Collections.Generic.List<Renderer>();
            var listMats = new System.Collections.Generic.List<Material>();

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || r is ParticleSystemRenderer || r is LineRenderer) continue;
                listRends.Add(r);
                listMats.Add(r.sharedMaterial);
            }

            _cachedRenderers = listRends.ToArray();
            _cachedDefaultMaterials = listMats.ToArray();

            if (mainRenderer == null && _cachedRenderers.Length > 0)
            {
                mainRenderer = _cachedRenderers[0];
            }
        }

        public void SetupValuableScrap(bool valuable)
        {
            isValuableScrap = valuable;
            CacheOriginalMaterials();

            if (valuable)
            {
                if (valuableLavaMaterial == null)
                {
                    valuableLavaMaterial = Resources.Load<Material>("M_ValuableLavaLoot");
#if UNITY_EDITOR
                    if (valuableLavaMaterial == null)
                    {
                        valuableLavaMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ValuableLavaLoot.mat");
                    }
#endif
                }

                if (valuableLavaMaterial != null && _cachedRenderers != null)
                {
                    for (int i = 0; i < _cachedRenderers.Length; i++)
                    {
                        if (_cachedRenderers[i] != null)
                        {
                            _cachedRenderers[i].sharedMaterial = valuableLavaMaterial;
                        }
                    }
                }

                EnsureValuableEmbers();
                if (_valuableEmbersInstance != null)
                {
                    _valuableEmbersInstance.gameObject.SetActive(true);
                    _valuableEmbersInstance.Play();
                }

                EnsureValuableRing();
                if (valuableRingObject != null)
                {
                    valuableRingObject.SetActive(true);
                }
            }
            else
            {
                if (_cachedRenderers != null && _cachedDefaultMaterials != null)
                {
                    for (int i = 0; i < _cachedRenderers.Length; i++)
                    {
                        if (_cachedRenderers[i] != null && i < _cachedDefaultMaterials.Length && _cachedDefaultMaterials[i] != null)
                        {
                            _cachedRenderers[i].sharedMaterial = _cachedDefaultMaterials[i];
                        }
                    }
                }

                if (_valuableEmbersInstance != null)
                {
                    _valuableEmbersInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _valuableEmbersInstance.gameObject.SetActive(false);
                }

                if (valuableRingObject != null)
                {
                    valuableRingObject.SetActive(false);
                }
            }

            SetVisualAlpha(_currentAlpha);
        }

        private void EnsureValuableEmbers()
        {
            if (_valuableEmbersInstance != null) return;

            if (valuableEmbersPrefab == null)
            {
                valuableEmbersPrefab = Resources.Load<GameObject>("VFX_ValuableLavaEmbers");
#if UNITY_EDITOR
                if (valuableEmbersPrefab == null)
                {
                    valuableEmbersPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX_ValuableLavaEmbers.prefab");
                }
#endif
            }

            if (valuableEmbersPrefab != null)
            {
                Transform parent = visualRoot != null ? visualRoot : transform;
                GameObject go = Instantiate(valuableEmbersPrefab, parent);
                go.name = "VFX_ValuableLavaEmbers";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                _valuableEmbersInstance = go.GetComponent<ParticleSystem>();
                if (_valuableEmbersInstance == null)
                {
                    _valuableEmbersInstance = go.GetComponentInChildren<ParticleSystem>();
                }
            }
        }

        private void EnsureValuableRing()
        {
            if (valuableRingObject != null) return;

            Transform parent = visualRoot != null ? visualRoot : transform;
            valuableRingObject = new GameObject("ValuableRing");
            valuableRingObject.transform.SetParent(parent, false);
            valuableRingObject.transform.localPosition = Vector3.zero;

            var lr = valuableRingObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 28;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;

#if UNITY_EDITOR
            lr.material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ValuableScrapGlow.mat");
#endif
            if (lr.material == null)
            {
                lr.material = Resources.Load<Material>("M_ValuableScrapGlow");
            }

            float radius = 1.35f;
            if (scrapData != null) radius *= Mathf.Max(0.5f, scrapData.BaseScale.x);

            for (int i = 0; i < 28; i++)
            {
                float angle = (i / 28f) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        private void Awake()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (gravityBody == null) gravityBody = GetComponent<GravityBody>();
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            if (mainRenderer == null && visualRoot != null)
            {
                mainRenderer = visualRoot.GetComponentInChildren<Renderer>();
            }

            CacheOriginalMaterials();

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
                OrbitalPanopticon.Core.GameBalanceConfig config = OrbitalPanopticon.Core.GameManager.GameBalanceConfigInstance;
                float wowMultiplier = (config != null && config.blackHoleHawkingPulseUnlocked) ? 2.0f : 1.0f;
                orbitalBonusMultiplier += rate * wowMultiplier * dt;
                UpdateVisualHeat();
            }

            if (isValuableScrap)
            {
                UpdateValuableVisuals(dt);
            }

            // Superheated Plasma Wake Trail management:
            // Enabled in Zone B / Launched state, or when heating up in accretion zone
            bool shouldEmitTrail = (currentState == ScrapState.Launched) || isInAccretionZone || (_currentAccretionDepth > 0.15f);
            if (shouldEmitTrail)
            {
                EnsurePlasmaWakeTrail();
                if (plasmaWakeTrail != null && !plasmaWakeTrail.emitting)
                {
                    plasmaWakeTrail.emitting = true;
                }
            }
            else
            {
                if (plasmaWakeTrail != null && plasmaWakeTrail.emitting)
                {
                    plasmaWakeTrail.emitting = false;
                }
            }

            // Thermal Blackbody Heating & Accretion Vortex check
            OrbitalPanopticon.Core.GameBalanceConfig balanceConfig = OrbitalPanopticon.Core.GameManager.GameBalanceConfigInstance;
            if (balanceConfig != null && (currentState == ScrapState.Launched || currentState == ScrapState.Floating))
            {
                float distToHole = transform.position.magnitude;
                float thresh = balanceConfig.accretionThresholdRadius;
                float horizon = balanceConfig.eventHorizonRadius;
                if (distToHole < thresh && thresh > horizon)
                {
                    float depth01 = Mathf.Clamp01((thresh - distToHole) / (thresh - horizon));
                    UpdateAccretionThermal(depth01);
                }
                else if (_currentAccretionDepth > 0f)
                {
                    ResetAccretionThermal();
                }
            }
        }

        public void UpdateAccretionThermal(float depth01)
        {
            _currentAccretionDepth = Mathf.Clamp01(depth01);

            if (_currentAccretionDepth <= 0.001f)
            {
                ResetAccretionThermal();
                return;
            }

            // Smooth blackbody gradient:
            // depth 0.0 -> normal
            // depth 0.5 -> radiant fiery orange (3.2, 1.2, 0.1)
            // depth 0.95 -> superheated white-blue (2.5, 3.2, 4.5)
            Color thermalColor;
            if (_currentAccretionDepth < 0.5f)
            {
                float t = _currentAccretionDepth / 0.5f;
                thermalColor = Color.Lerp(new Color(0.8f, 0.2f, 0.02f), new Color(3.2f, 1.2f, 0.1f), t);
            }
            else
            {
                float t = (_currentAccretionDepth - 0.5f) / 0.5f;
                thermalColor = Color.Lerp(new Color(3.2f, 1.2f, 0.1f), new Color(2.5f, 3.2f, 4.5f), t);
            }

            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            Renderer[] rends = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || r is ParticleSystemRenderer || r is LineRenderer) continue;
                r.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(EmissionColorId, thermalColor * (_currentAccretionDepth * 3.0f));
                _propertyBlock.SetColor(BaseColorId, Color.Lerp(isObserved ? observedGoldColor : baseColdColor, thermalColor, _currentAccretionDepth * 0.75f));
                r.SetPropertyBlock(_propertyBlock);
            }

            // Spaghettification Plasma Trail (VFX_AccretionSparks)
            if (_currentAccretionDepth > 0.25f)
            {
                EnsureAccretionSparks();
                if (accretionSparksParticles != null)
                {
                    if (!accretionSparksParticles.isPlaying)
                    {
                        accretionSparksParticles.gameObject.SetActive(true);
                        accretionSparksParticles.Play();
                    }
                    var em = accretionSparksParticles.emission;
                    em.rateOverTime = Mathf.Lerp(25f, 90f, _currentAccretionDepth);
                }
            }
            else
            {
                if (accretionSparksParticles != null && accretionSparksParticles.isPlaying)
                {
                    accretionSparksParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public void ResetAccretionThermal()
        {
            _currentAccretionDepth = 0f;
            if (accretionSparksParticles != null)
            {
                accretionSparksParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                accretionSparksParticles.gameObject.SetActive(false);
            }
            ResetVisualHeat();
        }

        public void TriggerSpaghettifyBurst(float t)
        {
            EnsureAccretionSparks();
            if (accretionSparksParticles != null)
            {
                if (!accretionSparksParticles.isPlaying)
                {
                    accretionSparksParticles.gameObject.SetActive(true);
                    accretionSparksParticles.Play();
                }
                var em = accretionSparksParticles.emission;
                em.rateOverTime = 120f;
            }
            UpdateAccretionThermal(Mathf.Lerp(0.85f, 1.0f, t));
        }

        private void EnsureAccretionSparks()
        {
            if (accretionSparksParticles != null) return;

            if (accretionSparksPrefab == null)
            {
                accretionSparksPrefab = Resources.Load<GameObject>("VFX_AccretionSparks");
#if UNITY_EDITOR
                if (accretionSparksPrefab == null)
                {
                    accretionSparksPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/VFX_AccretionSparks.prefab");
                }
#endif
            }

            if (accretionSparksPrefab != null)
            {
                Transform parent = visualRoot != null ? visualRoot : transform;
                GameObject go = Instantiate(accretionSparksPrefab, parent);
                go.name = "VFX_AccretionSparks";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                accretionSparksParticles = go.GetComponent<ParticleSystem>();
                if (accretionSparksParticles == null)
                {
                    accretionSparksParticles = go.GetComponentInChildren<ParticleSystem>();
                }
            }
        }

        private void EnsurePlasmaWakeTrail()
        {
            if (plasmaWakeTrail != null) return;

            Transform existing = transform.Find("VFX_PlasmaWakeTrail");
            if (existing != null)
            {
                plasmaWakeTrail = existing.GetComponent<TrailRenderer>();
            }

            if (plasmaWakeTrail == null)
            {
                GameObject trailGo = new GameObject("VFX_PlasmaWakeTrail");
                trailGo.transform.SetParent(transform, false);
                trailGo.transform.localPosition = Vector3.zero;

                plasmaWakeTrail = trailGo.AddComponent<TrailRenderer>();
                plasmaWakeTrail.time = 0.45f;
                plasmaWakeTrail.minVertexDistance = 0.1f;
                plasmaWakeTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                plasmaWakeTrail.receiveShadows = false;

                // Tapering width curve
                AnimationCurve widthCurve = new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(1f, 0f)
                );
                plasmaWakeTrail.widthCurve = widthCurve;

                // Gradient transparent -> orange -> blue-white (Intensity = 8.0f)
                Gradient grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] {
                        new GradientColorKey(new Color(6.0f, 7.0f, 8.0f), 0f),     // Intense electric blue-white (head)
                        new GradientColorKey(new Color(3.5f, 1.2f, 0.1f), 0.55f),  // Fiery orange HDR (mid)
                        new GradientColorKey(new Color(1.2f, 0.15f, 0.02f), 0.85f),// Deep red
                        new GradientColorKey(new Color(0.1f, 0.05f, 0.05f), 1f)    // Tail fade
                    },
                    new GradientAlphaKey[] {
                        new GradientAlphaKey(0.95f, 0f),
                        new GradientAlphaKey(0.85f, 0.4f),
                        new GradientAlphaKey(0.5f, 0.75f),
                        new GradientAlphaKey(0.0f, 1f)
                    }
                );
                plasmaWakeTrail.colorGradient = grad;

                if (plasmaWakeTrailMaterial == null)
                {
#if UNITY_EDITOR
                    plasmaWakeTrailMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ParticleAdditiveGlow.mat");
#endif
                    if (plasmaWakeTrailMaterial == null)
                    {
                        plasmaWakeTrailMaterial = Resources.Load<Material>("M_ParticleAdditiveGlow");
                    }
                }

                if (plasmaWakeTrailMaterial != null)
                {
                    plasmaWakeTrail.sharedMaterial = plasmaWakeTrailMaterial;
                }

                plasmaWakeTrail.emitting = false;
            }
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void UpdateValuableVisuals(float dt)
        {
            if (valuableRingObject != null && valuableRingObject.activeSelf)
            {
                valuableRingObject.transform.Rotate(new Vector3(35f, 55f, 20f) * dt, Space.Self);
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
            SetVisualAlpha(alpha);
        }

        public void SetVisualAlpha(float alpha)
        {
            _currentAlpha = Mathf.Clamp01(alpha);

            Color currentColor = isObserved ? observedGoldColor : baseColdColor;
            if (currentState == ScrapState.Launched && orbitalBonusMultiplier > 1f)
            {
                float heat = Mathf.Clamp01((orbitalBonusMultiplier - 1.0f) / 3.0f);
                currentColor = Color.Lerp(baseColdColor, hotAccretionColor, heat);
            }
            currentColor.a = _currentAlpha;

            if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();

            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            Renderer[] rends = visualRoot != null ? visualRoot.GetComponentsInChildren<Renderer>(true) : GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || r is ParticleSystemRenderer || r is LineRenderer) continue;
                r.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, currentColor);
                _propertyBlock.SetColor(ColorId, currentColor);
                _propertyBlock.SetFloat(AlphaId, _currentAlpha);
                r.SetPropertyBlock(_propertyBlock);
            }
        }

        private void ApplyAlpha(float alpha)
        {
            SetVisualAlpha(alpha);
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
            isKamikazeDelivery = false;
            _fadeElapsed = 0f;
            _currentAlpha = 1.0f;
            wasInteractedByPlayer = false;
            homeOrbitRadius = 0f;
            hasEnteredVortex = false;
            customOrbitAxis = Vector3.up;
            SetupValuableScrap(false);
            ResetAccretionThermal();
            SetVisualAlpha(1.0f);

            if (plasmaWakeTrail != null)
            {
                plasmaWakeTrail.Clear();
                plasmaWakeTrail.emitting = false;
            }

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

            if (currentState == ScrapState.Held)
            {
                hasEnteredVortex = false;
            }
            else if (currentState == ScrapState.Floating || currentState == ScrapState.Launched)
            {
                SetVisualAlpha(1.0f);
            }

            if (currentState == ScrapState.Held || currentState == ScrapState.Consumed || currentState == ScrapState.FadingOut)
            {
                if (plasmaWakeTrail != null)
                {
                    plasmaWakeTrail.emitting = false;
                }
            }

            if (currentState != ScrapState.Launched)
            {
                orbitalBonusMultiplier = 1.0f;
                currentSpiralRadius = 0f;
                ballisticLaunchTimer = 0f;
                ResetVisualHeat();
            }
        }

        public void TransitionToLaunched(Vector3 launchVelocity, float ballisticDuration = 0.85f)
        {
            SetState(ScrapState.Launched);
            pullImmunityTimer = 0.65f;
            ballisticLaunchTimer = ballisticDuration;
            SetVisualAlpha(1.0f);
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = launchVelocity;
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
