using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Player;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Manager References")]
        [SerializeField] private QuotaAndScoreManager quotaManager;
        [SerializeField] private ArenaBoundaryManager arenaBoundaryManager;
        [SerializeField] private DroneController droneController;

        [Header("Surveillance Monitor Overlay")]
        [SerializeField] private GameObject surveillanceFramePanel;
        [SerializeField] private TextMeshProUGUI recStatusText;
        [SerializeField] private TextMeshProUGUI sectorInfoText;
        [SerializeField] private TextMeshProUGUI telemetryRightText;
        [SerializeField] private TextMeshProUGUI controlHintsText;

        [Header("HUD Core Elements")]
        [SerializeField] private TextMeshProUGUI quotaText;
        [SerializeField] private Image quotaFillImage;
        [SerializeField] private TextMeshProUGUI heatMultiplierText;
        [SerializeField] private TextMeshProUGUI scrapCoinsText;

        [Header("Crosshair Elements")]
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Image[] crosshairTickImages;
        [SerializeField] private Color crosshairNormalColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color crosshairTargetInRangeColor = new Color(0.2f, 0.95f, 1f, 0.95f); // Bright Cyan
        [SerializeField] private Color crosshairObservedTargetColor = new Color(1.0f, 0.85f, 0.15f, 1f); // Rich Gold
        [SerializeField] private Color crosshairHoldingColor = new Color(0.15f, 0.85f, 1f, 0.95f);

        [Header("Operator Start Screen")]
        [SerializeField] private GameObject startOverlayPanel;
        [SerializeField] private CanvasGroup startOverlayCanvasGroup;
        [SerializeField] private TextMeshProUGUI startPromptText;

        [Header("Quota Celebration Banner")]
        [SerializeField] private GameObject quotaCelebrationPanel;
        [SerializeField] private CanvasGroup quotaCelebrationCanvasGroup;
        [SerializeField] private TextMeshProUGUI quotaCelebrationText;

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
        private Coroutine _celebrationCoroutine;
        private bool _isWarningActive = false;
        private float _warningRemainingSeconds = 0f;
        private bool _isOperatorConnected = false;

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

            if (droneController == null)
            {
                droneController = UnityEngine.Object.FindAnyObjectByType<DroneController>();
            }

            if (quotaManager != null)
            {
                quotaManager.OnQuotaCompleted += HandleQuotaCelebration;
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

            if (quotaCelebrationPanel != null)
            {
                quotaCelebrationPanel.SetActive(false);
            }

            // Auto-detect crosshair ticks if needed
            if (crosshairImage == null)
            {
                var ch = GameObject.Find("Crosshair");
                if (ch != null)
                {
                    crosshairImage = ch.GetComponent<Image>();
                    crosshairTickImages = ch.GetComponentsInChildren<Image>(true);
                }
            }

            // Setup Start Overlay
            if (startOverlayPanel != null)
            {
                startOverlayPanel.SetActive(true);
                if (startOverlayCanvasGroup != null)
                {
                    startOverlayCanvasGroup.alpha = 1f;
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _isOperatorConnected = false;
            }
            else
            {
                _isOperatorConnected = true;
            }
        }

        private void OnDestroy()
        {
            if (quotaManager != null)
            {
                quotaManager.OnQuotaCompleted -= HandleQuotaCelebration;
            }

            if (arenaBoundaryManager != null)
            {
                arenaBoundaryManager.OnSignalLost -= HandleSignalLost;
                arenaBoundaryManager.OnBoundaryWarningChanged -= HandleBoundaryWarning;
            }
        }

        private void Update()
        {
            HandleStartOverlayInput();
            UpdateHUD();
            UpdateWarningHUD();
            UpdateObservedNotice();
        }

        private void HandleStartOverlayInput()
        {
            if (_isOperatorConnected) return;

            // Pulse the click prompt
            if (startPromptText != null)
            {
                float pulse = 0.5f + (Mathf.Sin(Time.unscaledTime * 5f) * 0.5f);
                Color c = startPromptText.color;
                c.a = Mathf.Lerp(0.35f, 1f, pulse);
                startPromptText.color = c;
            }

            bool clicked = false;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                clicked = true;
            }
            else if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                clicked = true;
            }

            if (clicked)
            {
                ConnectOperator();
            }
        }

        public void ConnectOperator()
        {
            if (_isOperatorConnected) return;
            _isOperatorConnected = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (droneController != null)
            {
                droneController.SetControlEnabled(true);
            }

            if (startOverlayCanvasGroup != null)
            {
                StartCoroutine(FadeOutStartOverlayRoutine());
            }
            else if (startOverlayPanel != null)
            {
                startOverlayPanel.SetActive(false);
            }
        }

        private IEnumerator FadeOutStartOverlayRoutine()
        {
            const float duration = 0.45f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (startOverlayCanvasGroup != null)
                {
                    startOverlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                }
                yield return null;
            }

            if (startOverlayPanel != null)
            {
                startOverlayPanel.SetActive(false);
            }
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

            // 4. REC Status & Sector Telemetry (Top-Left)
            if (recStatusText != null)
            {
                bool blink = (Time.time % 1.2f) < 0.8f;
                recStatusText.text = blink
                    ? "<color=#FF2233>●</color> REC | CAM_FEED: DRONE #001"
                    : "<color=#551111>○</color> REC | CAM_FEED: DRONE #001";
            }

            if (sectorInfoText != null)
            {
                sectorInfoText.text = "SECTOR 7-G // ORBITAL PANOPTICON";
            }

            // 5. Telemetry (Top-Right)
            if (telemetryRightText != null)
            {
                int activeBots = 0;
                if (HelperDroneManager.Instance != null)
                {
                    activeBots = HelperDroneManager.Instance.ActiveBots.Count;
                }
                else
                {
                    GameBalanceConfig cfg = GameManager.GameBalanceConfigInstance;
                    if (cfg != null) activeBots = cfg.activeHelperRobotsCount;
                }

                telemetryRightText.text = $"HULL INTEGRITY: 100%\nHELPER BOTS: {activeBots}";
            }

            // 6. Context Control Hints (Bottom-Left)
            if (controlHintsText != null && string.IsNullOrEmpty(controlHintsText.text))
            {
                controlHintsText.text = "[RMB] Pull/Hold  |  [LMB] Launch/Punt  |  [SHIFT] Directional Dash  |  [X] Brake  |  [TAB] Upgrades";
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
            SetCrosshairState(hasTargetInRange, isHolding, false);
        }

        public void SetCrosshairState(bool hasTargetInRange, bool isHolding, bool isTargetObserved)
        {
            if (crosshairImage == null)
            {
                var ch = GameObject.Find("Crosshair");
                if (ch != null)
                {
                    crosshairImage = ch.GetComponent<Image>();
                    crosshairTickImages = ch.GetComponentsInChildren<Image>(true);
                }
            }
            if (crosshairImage == null) return;

            Color targetColor;
            float targetScale;

            if (isHolding)
            {
                targetColor = crosshairHoldingColor;
                targetScale = 1.15f;
            }
            else if (hasTargetInRange)
            {
                if (isTargetObserved)
                {
                    targetColor = crosshairObservedTargetColor;
                    targetScale = 1.45f;
                }
                else
                {
                    targetColor = crosshairTargetInRangeColor;
                    targetScale = 1.30f;
                }
            }
            else
            {
                targetColor = crosshairNormalColor;
                targetScale = 1.0f;
            }

            crosshairImage.color = targetColor;
            crosshairImage.transform.localScale = new Vector3(targetScale, targetScale, 1f);

            if (crosshairTickImages != null)
            {
                for (int i = 0; i < crosshairTickImages.Length; i++)
                {
                    if (crosshairTickImages[i] != null)
                    {
                        crosshairTickImages[i].color = targetColor;
                    }
                }
            }
        }

        private void HandleQuotaCelebration(int quotaLevel, int rewardCoins)
        {
            if (quotaCelebrationPanel == null) return;

            if (_celebrationCoroutine != null)
            {
                StopCoroutine(_celebrationCoroutine);
            }
            _celebrationCoroutine = StartCoroutine(ShowQuotaCelebrationRoutine(quotaLevel, rewardCoins));
        }

        private IEnumerator ShowQuotaCelebrationRoutine(int quotaLevel, int rewardCoins)
        {
            int speedBonus = quotaManager != null ? Mathf.CeilToInt(quotaManager.SpeedBonusPercent) : 0;
            if (quotaCelebrationText != null)
            {
                quotaCelebrationText.text = $"<color=#00FFAA>QUOTA #{quotaLevel} COMPLETED!</color>\n<size=80%>+{rewardCoins} SCRAP COINS  <color=#FFD700>(SPEED BONUS +{speedBonus}%)</color></size>";
            }

            quotaCelebrationPanel.SetActive(true);
            Transform bannerT = quotaCelebrationPanel.transform;

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlayQuotaComplete();
            }

            // Animate In (Bounce Scale + Fade In)
            const float inDuration = 0.35f;
            float elapsed = 0f;
            while (elapsed < inDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / inDuration;
                float scale = Mathf.Lerp(0.7f, 1.05f, Mathf.Sin(t * Mathf.PI * 0.5f));
                bannerT.localScale = new Vector3(scale, scale, 1f);

                if (quotaCelebrationCanvasGroup != null)
                {
                    quotaCelebrationCanvasGroup.alpha = Mathf.Clamp01(t * 1.5f);
                }
                yield return null;
            }

            bannerT.localScale = Vector3.one;
            if (quotaCelebrationCanvasGroup != null) quotaCelebrationCanvasGroup.alpha = 1f;

            // Hold on screen
            yield return new WaitForSecondsRealtime(2.5f);

            // Animate Out (Fade Out)
            const float outDuration = 0.45f;
            elapsed = 0f;
            while (elapsed < outDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / outDuration);
                if (quotaCelebrationCanvasGroup != null)
                {
                    quotaCelebrationCanvasGroup.alpha = alpha;
                }
                yield return null;
            }

            quotaCelebrationPanel.SetActive(false);
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
