using System;
using UnityEngine;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class QuotaAndScoreManager : MonoBehaviour
    {
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
        [SerializeField] private float speedBonusPercent = 100f;
        [SerializeField] private int currentHeatMultiplier = 1;
        [SerializeField] private int currentHeatStreak = 0;
        [SerializeField] private float lastConsumptionTime = -999f;

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
        public int CurrentHeatMultiplier => currentHeatMultiplier;

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

            // 2. Calculate Final Score
            float baseValue = 10f;
            float orbitalMultiplier = 1f;
            bool isObserved = false;

            if (scrap != null)
            {
                baseValue = scrap.BaseValue;
                orbitalMultiplier = scrap.OrbitalBonusMultiplier;
                isObserved = scrap.IsObserved;
            }

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float obsMultiplier = config != null ? config.cameraObservedMultiplier : 3f;
            float observationMultiplier = isObserved ? obsMultiplier : 1f;
            int effectiveHeatMultiplier = playerInvolved ? currentHeatMultiplier : 1;
            float finalScore = baseValue * orbitalMultiplier * observationMultiplier * effectiveHeatMultiplier;

            currentQuotaProgress += finalScore;
            OnScoreAdded?.Invoke(finalScore, scrapCoins);

            // 3. Check Quota Completion
            if (currentQuotaProgress >= targetQuota)
            {
                CompleteCurrentQuota();
            }
        }

        private void CompleteCurrentQuota()
        {
            int reward = Mathf.RoundToInt(baseQuotaReward * (1f + (speedBonusPercent / 100f)));
            scrapCoins += reward;

            OnQuotaCompleted?.Invoke(currentQuotaLevel, reward);

            currentQuotaLevel++;
            currentQuotaProgress = Mathf.Max(0f, currentQuotaProgress - targetQuota);
            targetQuota = Mathf.Round(targetQuota * quotaScalingFactor);
            speedBonusPercent = 100f;
        }
    }
}
