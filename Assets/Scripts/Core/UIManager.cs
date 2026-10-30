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
        public static UIManager Instance { get; private set; }

        [Header("Manager References")]
        [SerializeField] private QuotaAndScoreManager quotaManager;
        [SerializeField] private ArenaBoundaryManager arenaBoundaryManager;

        [Header("HUD Elements")]
        [SerializeField] private TextMeshProUGUI recStatusText;
        [SerializeField] private TextMeshProUGUI quotaText;
        [SerializeField] private Image quotaFillImage;
        [SerializeField] private TextMeshProUGUI heatMultiplierText;
        [SerializeField] private TextMeshProUGUI scrapCoinsText;

        [Header("Crosshair Elements")]
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Color crosshairNormalColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color crosshairTargetInRangeColor = new Color(0.2f, 1f, 0.5f, 0.95f);
        [SerializeField] private Color crosshairHoldingColor = new Color(0.15f, 0.85f, 1f, 0.95f);

        [Header("Boundary Warning Elements")]
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private TextMeshProUGUI warningCountdownText;
        [SerializeField] private Image warningBorderImage;
        [SerializeField] private Color warningBorderPulsingColor = new Color(1f, 0.15f, 0.1f, 0.75f);

        [Header("Signal Lost Overlay")]
        [SerializeField] private GameObject signalLostOverlay;
        [SerializeField] private TextMeshProUGUI signalLostReasonText;

        [Header("Observed Scrap Notification")]
        [SerializeField] private GameObject observedNoticePanel;
        [SerializeField] private TextMeshProUGUI observedNoticeText;
        [SerializeField] private Image observedNoticeBorder;
        [SerializeField] private Color observedGoldColor = new Color(1f, 0.85f, 0.1f, 1f);

        [Header("Heat Colors")]
        [SerializeField] private Color heat1xColor = Color.white;
        [SerializeField] private Color heat2xColor = new Color(1f, 0.9f, 0.2f);
        [SerializeField] private Color heat4xColor = new Color(1f, 0.5f, 0.1f);
        [SerializeField] private Color heat8xColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color heat16xColor = new Color(1f, 0.1f, 0.9f);

        private Coroutine _signalLostCoroutine;
        private bool _isWarningActive = false;
        private float _warningRemainingSeconds = 0f;

        private void Awake()
        {
            Instance = this;
        }

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
                arenaBoundaryManager.OnBoundaryWarningChanged += HandleBoundaryWarning;
            }

            if (signalLostOverlay != null)
            {
                signalLostOverlay.SetActive(false);
            }

            if (warningPanel != null)
            {
                warningPanel.SetActive(false);
            }

            if (observedNoticePanel != null)
            {
                observedNoticePanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (arenaBoundaryManager != null)
            {
                arenaBoundaryManager.OnSignalLost -= HandleSignalLost;
                arenaBoundaryManager.OnBoundaryWarningChanged -= HandleBoundaryWarning;
            }
        }

        private void Update()
        {
            UpdateHUD();
            UpdateWarningHUD();
            UpdateObservedNotice();
        }

        public void SetObservedNotice(bool show, string message = "OBSERVED TARGET GRABBED - x3 VALUE!")
        {
            if (observedNoticePanel != null && observedNoticePanel.activeSelf != show)
            {
                observedNoticePanel.SetActive(show);
            }
            if (show && observedNoticeText != null)
            {
                observedNoticeText.text = message;
            }
        }

        private void UpdateObservedNotice()
        {
            if (observedNoticePanel == null || !observedNoticePanel.activeSelf) return;

            float pulse = (Mathf.Sin(Time.time * 8f) + 1f) * 0.5f;
            if (observedNoticeText != null)
            {
                Color c = observedGoldColor;
                c.a = Mathf.Lerp(0.85f, 1f, pulse);
                observedNoticeText.color = c;
            }
            if (observedNoticeBorder != null)
            {
                Color c = observedGoldColor;
                c.a = Mathf.Lerp(0.35f, 0.85f, pulse);
                observedNoticeBorder.color = c;
            }
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

        private void UpdateWarningHUD()
        {
            if (!_isWarningActive) return;

            if (warningCountdownText != null)
            {
                warningCountdownText.text = $"WARNING: BOUNDARY BREACH DETECTED\nSIGNAL LOSS IN {_warningRemainingSeconds:F1}s";
            }

            if (warningBorderImage != null)
            {
                float pulse = (Mathf.Sin(Time.time * 9f) + 1f) * 0.5f;
                Color c = warningBorderPulsingColor;
                c.a = Mathf.Lerp(0.25f, 0.85f, pulse);
                warningBorderImage.color = c;
            }
        }

        private void HandleBoundaryWarning(bool isWarning, float remainingTime, float normalized)
        {
            _isWarningActive = isWarning;
            _warningRemainingSeconds = remainingTime;

            if (warningPanel != null)
            {
                warningPanel.SetActive(isWarning);
            }

            if (!isWarning && warningBorderImage != null)
            {
                Color c = warningBorderPulsingColor;
                c.a = 0f;
                warningBorderImage.color = c;
            }
        }

        public void SetCrosshairState(bool hasTargetInRange, bool isHolding)
        {
            if (crosshairImage == null) return;

            if (isHolding)
            {
                crosshairImage.color = crosshairHoldingColor;
                crosshairImage.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            }
            else if (hasTargetInRange)
            {
                crosshairImage.color = crosshairTargetInRangeColor;
                crosshairImage.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            }
            else
            {
                crosshairImage.color = crosshairNormalColor;
                crosshairImage.transform.localScale = Vector3.one;
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

            yield return new WaitForSeconds(1.2f);

            if (signalLostOverlay != null)
            {
                signalLostOverlay.SetActive(false);
            }
        }
    }
}
