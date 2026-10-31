using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class BlackHoleStageManager : MonoBehaviour
    {
        public static BlackHoleStageManager Instance { get; private set; }

        [Header("Runtime State")]
        [SerializeField] private float currentMass = 0f;
        [SerializeField] private int currentStageIndex = 0; // 0, 1, 2, 3 -> Stages 1, 2, 3, 4

        [Header("Scene References")]
        [SerializeField] private BlackHoleGravity blackHoleGravity;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject accretionDiskVisual;
        [SerializeField] private SphereCollider eventHorizonCollider;
        [SerializeField] private SphereCollider gravityWellCollider;
        [SerializeField] private List<OverseerContainmentEye> containmentEyes = new List<OverseerContainmentEye>();

        // Public Events
        public event Action<float, int> OnMassChanged; // (mass, stage 1-4)
        public event Action<int> OnStageAdvanced; // (stage 1-4)
        public event Action<int> OnContainmentLockBroken; // (lockIndex)

        public float CurrentMass => currentMass;
        public int CurrentStageIndex => currentStageIndex;
        public int CurrentStage => currentStageIndex + 1; // 1, 2, 3, 4
        public int TotalStages => 4;
        public IReadOnlyList<OverseerContainmentEye> ContainmentEyes => containmentEyes;

        public float CurrentStageTargetMass
        {
            get
            {
                GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                if (cfg != null && cfg.stageMassTargets != null && cfg.stageMassTargets.Length > 0)
                {
                    int idx = Mathf.Clamp(currentStageIndex, 0, cfg.stageMassTargets.Length - 1);
                    return cfg.stageMassTargets[idx];
                }
                return 120000f;
            }
        }

        public float CurrentStageValueMultiplier
        {
            get
            {
                GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                if (cfg != null && cfg.stageValueMultipliers != null && cfg.stageValueMultipliers.Length > 0)
                {
                    int idx = Mathf.Clamp(currentStageIndex, 0, cfg.stageValueMultipliers.Length - 1);
                    return cfg.stageValueMultipliers[idx];
                }
                return 1.0f;
            }
        }

        public float CurrentStageGravityMultiplier
        {
            get
            {
                GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                if (cfg != null && cfg.stageGravityMultipliers != null && cfg.stageGravityMultipliers.Length > 0)
                {
                    int idx = Mathf.Clamp(currentStageIndex, 0, cfg.stageGravityMultipliers.Length - 1);
                    return cfg.stageGravityMultipliers[idx];
                }
                return 1.0f;
            }
        }

        public int ActiveContainmentLocksCount
        {
            get
            {
                if (containmentEyes == null || containmentEyes.Count == 0)
                {
                    return Mathf.Max(0, 4 - currentStageIndex);
                }
                int count = 0;
                for (int i = 0; i < containmentEyes.Count; i++)
                {
                    if (containmentEyes[i] != null && containmentEyes[i].IsActiveLock)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (blackHoleGravity == null) blackHoleGravity = GetComponent<BlackHoleGravity>();
            if (visualRoot == null) visualRoot = transform.Find("VisualRoot");
            if (visualRoot == null) visualRoot = transform;

            if (accretionDiskVisual == null && visualRoot != null)
            {
                var ad = visualRoot.Find("AccretionDiskVisual");
                if (ad != null) accretionDiskVisual = ad.gameObject;
            }

            if (eventHorizonCollider == null)
            {
                var tr = transform.Find("EventHorizonTrigger");
                if (tr != null) eventHorizonCollider = tr.GetComponent<SphereCollider>();
            }

            if (gravityWellCollider == null)
            {
                var gw = transform.Find("GravityWellTrigger");
                if (gw != null) gravityWellCollider = gw.GetComponent<SphereCollider>();
            }
        }

        private void Start()
        {
            if (blackHoleGravity != null)
            {
                blackHoleGravity.OnObjectConsumed += HandleObjectConsumed;
            }

            DiscoverContainmentEyes();
            UpdateVisualsAndRadii(true);
        }

        private void OnDestroy()
        {
            if (blackHoleGravity != null)
            {
                blackHoleGravity.OnObjectConsumed -= HandleObjectConsumed;
            }
            if (Instance == this) Instance = null;
        }

        public void DiscoverContainmentEyes()
        {
            containmentEyes.Clear();
            var found = FindObjectsByType<OverseerContainmentEye>(FindObjectsInactive.Exclude);
            if (found != null && found.Length > 0)
            {
                Array.Sort(found, (a, b) => a.EyeIndex.CompareTo(b.EyeIndex));
                foreach (var eye in found)
                {
                    if (eye != null) containmentEyes.Add(eye);
                }
            }
        }

        private void Update()
        {
            GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
            if (accretionDiskVisual != null && cfg != null)
            {
                if (accretionDiskVisual.activeSelf != cfg.showAccretionDisk)
                {
                    accretionDiskVisual.SetActive(cfg.showAccretionDisk);
                }
            }

            UpdateVisualsAndRadii(false);
        }

        private void HandleObjectConsumed(GravityBody body)
        {
            if (body == null) return;

            ScrapItem scrap = body.GetComponent<ScrapItem>();
            float scrapMass = scrap != null ? scrap.Mass : 2f;
            float stageMultiplier = CurrentStageValueMultiplier;
            float addedMass = scrapMass * 12f * stageMultiplier;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null && config.quantumDecayUnlocked)
            {
                addedMass *= 2.0f;
            }

            AddMass(addedMass);

            if (FMODAudioManager.Instance != null)
            {
                if (scrap != null && scrap.IsValuableScrap)
                {
                    FMODAudioManager.Instance.PlayValuableScrapConsume(body.transform.position);
                }
                else
                {
                    FMODAudioManager.Instance.PlayScrapSpaghettify(body.transform.position);
                }
            }
        }

        public void AddMass(float addedMass)
        {
            if (addedMass <= 0f) return;

            currentMass += addedMass;
            CheckStageProgression();
            OnMassChanged?.Invoke(currentMass, CurrentStage);
        }

        private void CheckStageProgression()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null || config.stageMassTargets == null) return;

            while (currentStageIndex < config.stageMassTargets.Length && currentMass >= config.stageMassTargets[currentStageIndex])
            {
                // Break containment lock for the completed stage
                BreakLockForStage(currentStageIndex);

                if (currentStageIndex < 3)
                {
                    currentStageIndex++;
                    OnStageAdvanced?.Invoke(CurrentStage);
                    Debug.Log($"<color=#00FFCC>[STAGE PROGRESSION] Advanced to STAGE {CurrentStage} / 4! Active Containment Beams: {ActiveContainmentLocksCount}/4</color>");
                }
                else
                {
                    // Reached target of Stage 4 (120,000 tons) -> All 4 containment locks broken!
                    Debug.Log("<color=#FFD700>[CRITICAL MASS] All 4 Containment Locks shattered! Singularity unconstrained!</color>");
                    EndgameFinaleController.Instance?.TriggerEndgameFinale();
                    break;
                }
            }
        }

        public void BreakLockForStage(int stageIdx)
        {
            if (containmentEyes != null && stageIdx < containmentEyes.Count && containmentEyes[stageIdx] != null)
            {
                containmentEyes[stageIdx].BreakContainmentLock();
                OnContainmentLockBroken?.Invoke(stageIdx);
            }
        }

        public void BreakNextContainmentLock()
        {
            for (int i = 0; i < containmentEyes.Count; i++)
            {
                if (containmentEyes[i] != null && containmentEyes[i].IsActiveLock)
                {
                    containmentEyes[i].BreakContainmentLock();
                    OnContainmentLockBroken?.Invoke(i);
                    return;
                }
            }
        }

        private void UpdateVisualsAndRadii(bool forceInstant)
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null || config.stageMassTargets == null || config.stageBlackHoleScales == null) return;

            float prevTarget = (currentStageIndex > 0) ? config.stageMassTargets[currentStageIndex - 1] : 0f;
            float nextTarget = config.stageMassTargets[Mathf.Clamp(currentStageIndex, 0, config.stageMassTargets.Length - 1)];
            float t = Mathf.Clamp01((currentMass - prevTarget) / Mathf.Max(1f, nextTarget - prevTarget));

            int scaleIdx = Mathf.Clamp(currentStageIndex, 0, config.stageBlackHoleScales.Length - 1);
            float baseScale = config.stageBlackHoleScales[scaleIdx];
            float nextScale = (scaleIdx < config.stageBlackHoleScales.Length - 1)
                ? config.stageBlackHoleScales[scaleIdx + 1]
                : (baseScale * 1.35f);

            float targetScaleVal = Mathf.Lerp(baseScale, nextScale, t);

            if (visualRoot != null)
            {
                Vector3 targetScale = Vector3.one * targetScaleVal;
                visualRoot.localScale = forceInstant ? targetScale : Vector3.Lerp(visualRoot.localScale, targetScale, Time.deltaTime * 3.0f);
            }

            // Smooth physical radius expansion
            float targetHorizon = targetScaleVal * 0.58f;
            float targetAccretion = 15.0f + (targetScaleVal - 6.0f) * 1.15f;

            config.eventHorizonRadius = targetHorizon;
            config.accretionThresholdRadius = targetAccretion;

            if (eventHorizonCollider != null) eventHorizonCollider.radius = targetHorizon;
            if (gravityWellCollider != null) gravityWellCollider.radius = targetAccretion;
        }
    }
}
