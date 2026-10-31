using System;
using UnityEngine;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class QuotaAndScoreManager : MonoBehaviour
    {
        public static QuotaAndScoreManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private BlackHoleGravity blackHoleGravity;

        [Header("Quota Settings")]
        [Tooltip("Initial quota target for level 1.")]
        [SerializeField] private float targetQuota = 500f;

        [Tooltip("Growth multiplier for quota each time it is completed.")]
        [SerializeField] private float quotaScalingFactor = 1.8f;

        [Tooltip("Base currency reward upon quota completion.")]
        [SerializeField] private int baseQuotaReward = 150;

        [Tooltip("Speed bonus percentage lost per second.")]
        [SerializeField] private float speedBonusDecayRate = 2.0f; // -2%/sec

        [Header("Heat Combo Settings")]
        [Tooltip("Idle seconds before heat multiplier steps down.")]
        [SerializeField] private float heatDecayTimeout = 4.0f;

        [Tooltip("Number of absorbed scrap items needed to step up to next heat multiplier.")]
        [SerializeField] private int itemsPerHeatLevel = 3;

        [Header("Runtime Status")]
        [SerializeField] private int scrapCoins = 0;
        [SerializeField] private float currentQuotaProgress = 0f;
        [SerializeField] private int currentQuotaLevel = 1;
        [SerializeField] private int completedQuotasCount = 0;
        [SerializeField] private float speedBonusPercent = 100f;
        [SerializeField] private int currentHeatMultiplier = 1;
        [SerializeField] private int currentHeatStreak = 0;
        [SerializeField] private float lastConsumptionTime = -999f;
        [SerializeField] private float totalMassConsumed = 0f;
        [SerializeField] private int peakHeatMultiplier = 1;

        private readonly int[] _heatStages = new int[] { 1, 2, 4, 8, 16 };
        private int _currentStageIndex = 0;

        // Public Events
        public event Action<float, int> OnScoreAdded; // (scoreGained, totalCoins)
        public event Action<int, int> OnQuotaCompleted; // (quotaLevel, rewardCoins)
        public event Action<int> OnHeatMultiplierChanged; // (newMultiplier)

        public int ScrapCoins => scrapCoins;
        public float CurrentQuotaProgress => currentQuotaProgress;
        public float TargetQuota => targetQuota;
        public float SpeedBonusPercent => speedBonusPercent;
        public int CurrentQuotaLevel => currentQuotaLevel;
        public int CompletedQuotasCount => completedQuotasCount;
        public int CurrentHeatMultiplier => currentHeatMultiplier;
        public float TotalMassConsumed => totalMassConsumed;
        public int PeakHeatMultiplier => peakHeatMultiplier;

        public bool TrySpendScrapCoins(int amount)
        {
            if (amount <= 0) return true;
            if (scrapCoins >= amount)
            {
                scrapCoins -= amount;
                OnScoreAdded?.Invoke(0f, scrapCoins);
                return true;
            }
            return false;
        }

        public void AddScrapCoins(int amount)
        {
            if (amount > 0)
            {
                scrapCoins += amount;
                OnScoreAdded?.Invoke(0f, scrapCoins);
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
        }

        private void Start()
        {
            if (blackHoleGravity == null)
            {
                blackHoleGravity = UnityEngine.Object.FindAnyObjectByType<BlackHoleGravity>();
            }

            if (blackHoleGravity != null)
            {
                blackHoleGravity.OnObjectConsumed += HandleObjectConsumed;
            }
        }

        private void OnDestroy()
        {
            if (blackHoleGravity != null)
            {
                blackHoleGravity.OnObjectConsumed -= HandleObjectConsumed;
            }
        }

        private void Update()
        {
            UpdateSpeedBonus();
            UpdateHeatDecay();
        }

        private void UpdateSpeedBonus()
        {
            if (speedBonusPercent > 0f)
            {
                speedBonusPercent = Mathf.Max(0f, speedBonusPercent - (speedBonusDecayRate * Time.deltaTime));
            }
        }

        private void UpdateHeatDecay()
        {
            if (_currentStageIndex > 0 && Time.time - lastConsumptionTime >= heatDecayTimeout)
            {
                _currentStageIndex--;
                currentHeatMultiplier = _heatStages[_currentStageIndex];
                currentHeatStreak = 0;
                lastConsumptionTime = Time.time;
                OnHeatMultiplierChanged?.Invoke(currentHeatMultiplier);
            }
        }

        public void HandleObjectConsumed(GravityBody body)
        {
            if (body == null) return;

            ScrapItem scrap = body.GetComponent<ScrapItem>();
            bool isScrap = scrap != null;
            bool playerInvolved = isScrap && (scrap.WasInteractedByPlayer || scrap.CurrentState == ScrapState.Launched);

            lastConsumptionTime = Time.time;

            // 1. Advance Heat Streak only for player-interacted scrap
            if (playerInvolved)
            {
                currentHeatStreak++;
                if (currentHeatStreak >= itemsPerHeatLevel && _currentStageIndex < _heatStages.Length - 1)
                {
                    _currentStageIndex++;
                    currentHeatStreak = 0;
                    currentHeatMultiplier = _heatStages[_currentStageIndex];
                    OnHeatMultiplierChanged?.Invoke(currentHeatMultiplier);
                }
            }

            // Tier 4 Cosmic Anomaly instant heat boost
            if (scrap != null && scrap.Tier == 4)
            {
                if (_currentStageIndex < _heatStages.Length - 1)
                {
                    _currentStageIndex++;
                    currentHeatStreak = 0;
                    currentHeatMultiplier = _heatStages[_currentStageIndex];
                    OnHeatMultiplierChanged?.Invoke(currentHeatMultiplier);
                }
            }

            if (currentHeatMultiplier > peakHeatMultiplier)
            {
                peakHeatMultiplier = currentHeatMultiplier;
            }

            if (scrap != null)
            {
                totalMassConsumed += scrap.Mass;
            }

            // 2. Calculate Final Score
            float baseValue = 10f;
            float orbitalMultiplier = 1f;
            bool isObserved = false;
            bool isKamikaze = false;

            if (scrap != null)
            {
                baseValue = scrap.BaseValue;
                orbitalMultiplier = scrap.OrbitalBonusMultiplier;
                isObserved = scrap.IsObserved;
                isKamikaze = scrap.isKamikazeDelivery;
            }

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float obsMultiplier = config != null ? config.cameraObservedMultiplier : 3f;
            float observationMultiplier = isObserved ? obsMultiplier : 1f;
            float kamikazeMultiplier = isKamikaze ? 2.5f : 1f;
            int effectiveHeatMultiplier = (playerInvolved || isKamikaze) ? currentHeatMultiplier : 1;

            float stageMultiplier = 1.0f;
            if (BlackHoleStageManager.Instance != null)
            {
                stageMultiplier = BlackHoleStageManager.Instance.CurrentStageValueMultiplier;
            }

            float finalScore = baseValue * orbitalMultiplier * observationMultiplier * effectiveHeatMultiplier * kamikazeMultiplier * stageMultiplier;
            if (config != null && config.quantumDecayUnlocked)
            {
                finalScore *= 2.0f;
            }

            currentQuotaProgress += finalScore;

            // Direct coins reward from every piece of scrap (doubled by Quantum Decay)
            int coinsGained = Mathf.Max(1, Mathf.RoundToInt(baseValue * orbitalMultiplier * stageMultiplier * 0.15f));
            if (config != null && config.quantumDecayUnlocked)
            {
                coinsGained *= 2;
            }
            AddScrapCoins(coinsGained);

            OnScoreAdded?.Invoke(finalScore, scrapCoins);

            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayScorePopup(body != null ? body.transform.position : Vector3.zero);
            }

            // World-space floating score popup
            if (ScorePopupManager.Instance != null)
            {
                ScorePopupManager.Instance.SpawnPopup(body.transform.position, finalScore, isKamikaze, isObserved);
            }

            // 3. Check Quota Completion
            if (currentQuotaProgress >= targetQuota)
            {
                CompleteCurrentQuota();
            }
        }

        public void ForceCompleteQuota()
        {
            CompleteCurrentQuota();
        }

        private void CompleteCurrentQuota()
        {
            // Designer rule: each new mission/quota reward increases by +10%
            float currentQuotaReward = baseQuotaReward * Mathf.Pow(1.10f, completedQuotasCount);
            int reward = Mathf.RoundToInt(currentQuotaReward * (1f + (speedBonusPercent / 100f)));
            if (GameManager.GameBalanceConfigInstance != null && GameManager.GameBalanceConfigInstance.quantumDecayUnlocked)
            {
                reward *= 2;
            }
            scrapCoins += reward;

            OnQuotaCompleted?.Invoke(currentQuotaLevel, reward);

            completedQuotasCount++;
            currentQuotaLevel++;
            currentQuotaProgress = Mathf.Max(0f, currentQuotaProgress - targetQuota);
            targetQuota = Mathf.Round(targetQuota * quotaScalingFactor);
            speedBonusPercent = 100f;
        }
    }
}
