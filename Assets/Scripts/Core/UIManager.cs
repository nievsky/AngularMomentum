using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        [Header("Manager References")]
        [SerializeField] private QuotaAndScoreManager quotaManager;
        [SerializeField] private ArenaBoundaryManager arenaBoundaryManager;

        [Header("HUD Elements")]
        [SerializeField] private TextMeshProUGUI recStatusText;
        [SerializeField] private TextMeshProUGUI quotaText;
        [SerializeField] private Image quotaFillImage;
        [SerializeField] private TextMeshProUGUI heatMultiplierText;
        [SerializeField] private TextMeshProUGUI scrapCoinsText;

        [Header("Signal Lost Overlay")]
        [SerializeField] private GameObject signalLostOverlay;
        [SerializeField] private TextMeshProUGUI signalLostReasonText;

        [Header("Heat Colors")]
        [SerializeField] private Color heat1xColor = Color.white;
        [SerializeField] private Color heat2xColor = new Color(1f, 0.9f, 0.2f);
        [SerializeField] private Color heat4xColor = new Color(1f, 0.5f, 0.1f);
        [SerializeField] private Color heat8xColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color heat16xColor = new Color(1f, 0.1f, 0.9f);

        private Coroutine _signalLostCoroutine;

        private void Start()
        {
            if (quotaManager == null)
            {
                quotaManager = UnityEngine.Object.FindAnyObjectByType<QuotaAndScoreManager>();
            }

            if (arenaBoundaryManager == null)
            {
                arenaBoundaryManager = UnityEngine.Object.FindAnyObjectByType<ArenaBoundaryManager>();
            }

            if (arenaBoundaryManager != null)
            {
                arenaBoundaryManager.OnSignalLost += HandleSignalLost;
            }

            if (signalLostOverlay != null)
            {
                signalLostOverlay.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (arenaBoundaryManager != null)
            {
                arenaBoundaryManager.OnSignalLost -= HandleSignalLost;
            }
        }

        private void Update()
        {
            UpdateHUD();
        }

        private void UpdateHUD()
        {
            if (quotaManager == null) return;

            // 1. Quota Text & Progress
            if (quotaText != null)
            {
                int currentProg = Mathf.FloorToInt(quotaManager.CurrentQuotaProgress);
                int target = Mathf.FloorToInt(quotaManager.TargetQuota);
                int speedBonus = Mathf.CeilToInt(quotaManager.SpeedBonusPercent);
                quotaText.text = $"QUOTA: {currentProg} / {target} (Speed Bonus: +{speedBonus}%)";
            }

            if (quotaFillImage != null && quotaManager.TargetQuota > 0f)
            {
                quotaFillImage.fillAmount = Mathf.Clamp01(quotaManager.CurrentQuotaProgress / quotaManager.TargetQuota);
            }

            // 2. Heat Multiplier & Color
            if (heatMultiplierText != null)
            {
                int multiplier = quotaManager.CurrentHeatMultiplier;
                heatMultiplierText.text = $"HEAT MULTIPLIER: x{multiplier}";
                heatMultiplierText.color = GetHeatColor(multiplier);
            }

            // 3. Scrap Coins
            if (scrapCoinsText != null)
            {
                scrapCoinsText.text = $"SCRAP COINS: {quotaManager.ScrapCoins}";
            }

            // 4. REC Status blinking
            if (recStatusText != null)
            {
                bool blink = (Time.time % 1.2f) < 0.8f;
                recStatusText.text = blink ? "● REC | DRONE #001" : "  REC | DRONE #001";
            }
        }

        private Color GetHeatColor(int multiplier)
        {
            if (multiplier >= 16) return heat16xColor;
            if (multiplier >= 8) return heat8xColor;
            if (multiplier >= 4) return heat4xColor;
            if (multiplier >= 2) return heat2xColor;
            return heat1xColor;
        }

        private void HandleSignalLost(string reason)
        {
            if (_signalLostCoroutine != null)
            {
                StopCoroutine(_signalLostCoroutine);
            }
            _signalLostCoroutine = StartCoroutine(ShowSignalLostRoutine(reason));
        }

        private IEnumerator ShowSignalLostRoutine(string reason)
        {
            if (signalLostOverlay != null)
            {
                signalLostOverlay.SetActive(true);
                if (signalLostReasonText != null)
                {
                    signalLostReasonText.text = reason;
                }
            }

            yield return new WaitForSeconds(1.5f);

            if (signalLostOverlay != null)
            {
                signalLostOverlay.SetActive(false);
            }
        }
    }
}
