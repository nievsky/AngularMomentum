using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Player;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Minimalist HUD Controller for Orbital Panopticon.
    /// Manages strictly 3 core gameplay metrics (Scrap Coins Badge, Mass Bar, and Stage Indicator),
    /// the central aiming crosshair, and transient overlay notifications.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Manager References")]
        [SerializeField] private QuotaAndScoreManager quotaManager;
        [SerializeField] private ArenaBoundaryManager arenaBoundaryManager;
        [SerializeField] private DroneController droneController;

        [Header("Minimalist Core HUD Elements")]
        [SerializeField] private GameObject scrapCoinsBadge;
        [SerializeField] private TextMeshProUGUI scrapCoinsText;
        [SerializeField] private GameObject bottomHUDPanel;
        [SerializeField] private Image blackHoleBarFill;
        [SerializeField] private TextMeshProUGUI blackHoleBarText;
        [SerializeField] private TextMeshProUGUI stageHeaderText;

        public GameObject ScrapCoinsBadge => scrapCoinsBadge;
        public TextMeshProUGUI ScrapCoinsText => scrapCoinsText;
        public GameObject BottomHUDPanel => bottomHUDPanel;
        public Image BlackHoleBarFill => blackHoleBarFill;
        public TextMeshProUGUI BlackHoleBarText => blackHoleBarText;
        public TextMeshProUGUI StageHeaderText => stageHeaderText;

        [Header("Crosshair Elements")]
        [SerializeField] private Image crosshairImage;
        [SerializeField] private Image[] crosshairTickImages;
        [SerializeField] private Color crosshairNormalColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color crosshairTargetInRangeColor = new Color(0.2f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color crosshairObservedTargetColor = new Color(1.0f, 0.85f, 0.15f, 1f);
        [SerializeField] private Color crosshairHoldingColor = new Color(0.15f, 0.85f, 1f, 0.95f);

        [Header("Operator Start Screen")]
        [SerializeField] private GameObject startOverlayPanel;
        [SerializeField] private CanvasGroup startOverlayCanvasGroup;
        [SerializeField] private TextMeshProUGUI startPromptText;

        [Header("Transient Notifications")]
        [SerializeField] private GameObject warningPanel;
        [SerializeField] private TextMeshProUGUI warningCountdownText;
        [SerializeField] private Image warningBorderImage;
        [SerializeField] private Color warningBorderPulsingColor = new Color(1f, 0.15f, 0.1f, 0.75f);

        [SerializeField] private GameObject beamAlertPanel;
        [SerializeField] private TextMeshProUGUI beamAlertText;
        [SerializeField] private CanvasGroup beamAlertCanvasGroup;
        private Coroutine _beamAlertCoroutine;

        [SerializeField] private GameObject signalLostOverlay;
        [SerializeField] private TextMeshProUGUI signalLostReasonText;

        [SerializeField] private GameObject observedNoticePanel;
        [SerializeField] private TextMeshProUGUI observedNoticeText;
        [SerializeField] private Image observedNoticeBorder;
        [SerializeField] private Color observedGoldColor = new Color(1f, 0.85f, 0.1f, 1f);

        [SerializeField] private GameObject quotaCelebrationPanel;
        [SerializeField] private CanvasGroup quotaCelebrationCanvasGroup;
        [SerializeField] private TextMeshProUGUI quotaCelebrationText;

        private Coroutine _signalLostCoroutine;
        private Coroutine _celebrationCoroutine;
        private bool _isWarningActive = false;
        private float _warningRemainingSeconds = 0f;
        private bool _isOperatorConnected = false;
        private int _lastCoins = -1;

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

            if (signalLostOverlay != null) signalLostOverlay.SetActive(false);
            if (warningPanel != null) warningPanel.SetActive(false);
            if (observedNoticePanel != null) observedNoticePanel.SetActive(false);
            if (quotaCelebrationPanel != null) quotaCelebrationPanel.SetActive(false);

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
                if (startOverlayCanvasGroup != null) startOverlayCanvasGroup.alpha = 1f;
                if (startPromptText != null) startPromptText.text = LocalizationManager.Get("HUD_StartPrompt");

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _isOperatorConnected = false;
            }
            else
            {
                _isOperatorConnected = true;
            }

            EnsureMinimalistHUD();
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

        public void SetObservedNotice(bool show, string message = null)
        {
            if (observedNoticePanel != null && observedNoticePanel.activeSelf != show)
            {
                observedNoticePanel.SetActive(show);
            }
            if (show && observedNoticeText != null)
            {
                observedNoticeText.text = !string.IsNullOrEmpty(message) ? message : LocalizationManager.Get("HUD_ObservedNotice");
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
            var stageMgr = BlackHoleStageManager.Instance;
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float mass = stageMgr != null ? stageMgr.CurrentMass : (BlackHoleMassAndEnergyManager.Instance != null ? BlackHoleMassAndEnergyManager.Instance.CurrentBlackHoleMass : 0f);
            int stage = stageMgr != null ? stageMgr.CurrentStage : (BlackHoleMassAndEnergyManager.Instance != null ? BlackHoleMassAndEnergyManager.Instance.CurrentStage : 1);
            int stageIndex = stageMgr != null ? stageMgr.CurrentStageIndex : (stage - 1);
            float targetMass = stageMgr != null ? stageMgr.CurrentStageTargetMass : (config != null && config.stageMassTargets != null && stageIndex < config.stageMassTargets.Length ? config.stageMassTargets[stageIndex] : 500f);

            // 1. Scrap Coins Counter (Top-Right Badge)
            int coins = quotaManager != null ? quotaManager.ScrapCoins : 0;
            if (_lastCoins != -1 && coins > _lastCoins && scrapCoinsBadge != null)
            {
                scrapCoinsBadge.transform.DOKill();
                scrapCoinsBadge.transform.localScale = Vector3.one;
                scrapCoinsBadge.transform.DOPunchScale(Vector3.one * 0.18f, 0.22f, 6, 0.5f).SetUpdate(true);
            }
            _lastCoins = coins;

            if (scrapCoinsText != null)
            {
                scrapCoinsText.text = LocalizationManager.Get("HUD_ScrapCoins", coins);
            }

            // 2. Black Hole Mass Progress Bar (Bottom-Center)
            float prevTarget = (stageIndex > 0 && config != null && config.stageMassTargets != null && stageIndex - 1 < config.stageMassTargets.Length)
                ? config.stageMassTargets[stageIndex - 1]
                : 0f;
            float fillNorm = Mathf.Clamp01((mass - prevTarget) / Mathf.Max(1f, targetMass - prevTarget));

            if (blackHoleBarFill != null)
            {
                blackHoleBarFill.fillAmount = fillNorm;

                if (stage == 1) blackHoleBarFill.color = new Color(0.15f, 0.9f, 1.0f, 0.95f);
                else if (stage == 2) blackHoleBarFill.color = new Color(1.0f, 0.88f, 0.2f, 0.95f);
                else if (stage == 3) blackHoleBarFill.color = new Color(1.0f, 0.55f, 0.1f, 0.95f);
                else
                {
                    float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 8f));
                    blackHoleBarFill.color = Color.Lerp(new Color(1f, 0.15f, 0.15f, 0.95f), new Color(1f, 0.85f, 0.15f, 0.95f), pulse);
                }
            }

            if (blackHoleBarText != null)
            {
                blackHoleBarText.text = LocalizationManager.Get("HUD_MassBar", mass, targetMass);
            }

            // 3. Stage Indicator (Directly above the Mass Bar)
            if (stageHeaderText != null)
            {
                if (stage >= 4)
                {
                    stageHeaderText.text = LocalizationManager.Get("HUD_Stage4_Critical");
                    float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 10f));
                    stageHeaderText.color = Color.Lerp(new Color(1f, 0.2f, 0.15f), new Color(1f, 0.9f, 0.2f), pulse);
                }
                else
                {
                    stageHeaderText.text = LocalizationManager.Get("HUD_Stage", stage);
                    if (stage == 1) stageHeaderText.color = new Color(0.2f, 0.92f, 1.0f);
                    else if (stage == 2) stageHeaderText.color = new Color(1.0f, 0.88f, 0.2f);
                    else if (stage == 3) stageHeaderText.color = new Color(1.0f, 0.55f, 0.1f);
                }
            }
        }

        private static void SafeDestroy(GameObject go)
        {
            if (go == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(go);
            else
                Destroy(go);
#else
            Destroy(go);
#endif
        }

        public void EnsureMinimalistHUD()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // 1. Permanently remove all legacy clutter elements
            string[] legacyNames = { "SurveillanceMonitorFrame", "Text_REC_Status", "Panel_QuotaAndHeat", "Text_BossTracker", "Panel_CriticalMassWarning" };
            foreach (var legName in legacyNames)
            {
                Transform t = canvas.transform.Find(legName);
                if (t != null)
                {
                    SafeDestroy(t.gameObject);
                }
            }

            // 2. Setup or find Scrap Coins Counter Badge (Top-Right)
            if (scrapCoinsBadge == null)
            {
                Transform existingBadge = canvas.transform.Find("Panel_ScrapCoins");
                if (existingBadge != null)
                {
                    scrapCoinsBadge = existingBadge.gameObject;
                    scrapCoinsText = scrapCoinsBadge.GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            if (scrapCoinsBadge == null)
            {
                GameObject badgeGo = new GameObject("Panel_ScrapCoins", typeof(RectTransform), typeof(Image), typeof(Outline));
                badgeGo.transform.SetParent(canvas.transform, false);

                RectTransform badgeRt = badgeGo.GetComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(1f, 1f);
                badgeRt.anchorMax = new Vector2(1f, 1f);
                badgeRt.pivot = new Vector2(1f, 1f);
                badgeRt.anchoredPosition = new Vector2(-40f, -36f);
                badgeRt.sizeDelta = new Vector2(250f, 44f);

                Image bg = badgeGo.GetComponent<Image>();
                bg.color = new Color(0.04f, 0.07f, 0.12f, 0.90f);

                Outline outline = badgeGo.GetComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.85f, 1f, 0.8f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                GameObject textGo = new GameObject("Text_Coins", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(badgeGo.transform, false);
                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.sizeDelta = Vector2.zero;

                scrapCoinsText = textGo.GetComponent<TextMeshProUGUI>();
                scrapCoinsText.alignment = TextAlignmentOptions.Center;
                scrapCoinsText.fontSize = 15;
                scrapCoinsText.fontStyle = FontStyles.Bold;
                scrapCoinsText.color = new Color(1.0f, 0.88f, 0.25f);

                scrapCoinsBadge = badgeGo;
            }

            // Remove standalone legacy Text_ScrapCoins if present
            Transform oldCoins = canvas.transform.Find("Text_ScrapCoins");
            if (oldCoins != null && oldCoins.gameObject != scrapCoinsBadge && oldCoins.parent == canvas.transform)
            {
                SafeDestroy(oldCoins.gameObject);
            }

            // 3. Setup or find Bottom HUD (Mass Bar & Stage Header)
            if (bottomHUDPanel == null)
            {
                Transform existingBottom = canvas.transform.Find("Panel_BottomHUD");
                if (existingBottom != null)
                {
                    bottomHUDPanel = existingBottom.gameObject;
                }
            }

            if (bottomHUDPanel == null)
            {
                GameObject hudGo = new GameObject("Panel_BottomHUD", typeof(RectTransform));
                hudGo.transform.SetParent(canvas.transform, false);
                bottomHUDPanel = hudGo;
            }

            RectTransform bottomRt = bottomHUDPanel.GetComponent<RectTransform>();
            bottomRt.anchorMin = new Vector2(0.5f, 0f);
            bottomRt.anchorMax = new Vector2(0.5f, 0f);
            bottomRt.pivot = new Vector2(0.5f, 0f);
            bottomRt.anchoredPosition = new Vector2(0f, 32f);
            bottomRt.sizeDelta = new Vector2(520f, 22f);

            // Hide legacy battery bar if present
            Transform batT = bottomHUDPanel.transform.Find("Bar_Battery");
            if (batT != null) SafeDestroy(batT.gameObject);

            // Re-anchor Bar_BlackHole
            Transform bhT = bottomHUDPanel.transform.Find("Bar_BlackHole");
            GameObject bhBarGo;
            if (bhT == null)
            {
                bhBarGo = new GameObject("Bar_BlackHole", typeof(RectTransform), typeof(Image), typeof(Outline));
                bhBarGo.transform.SetParent(bottomHUDPanel.transform, false);
            }
            else
            {
                bhBarGo = bhT.gameObject;
            }

            RectTransform bhRt = bhBarGo.GetComponent<RectTransform>();
            bhRt.anchorMin = Vector2.zero;
            bhRt.anchorMax = Vector2.one;
            bhRt.sizeDelta = Vector2.zero;
            bhRt.anchoredPosition = Vector2.zero;

            Image bhBg = bhBarGo.GetComponent<Image>();
            bhBg.color = new Color(0.04f, 0.07f, 0.12f, 0.92f);

            Outline bhOutline = bhBarGo.GetComponent<Outline>();
            bhOutline.effectColor = new Color(0.2f, 0.85f, 1f, 0.75f);
            bhOutline.effectDistance = new Vector2(1.5f, -1.5f);

            Transform fillT = bhBarGo.transform.Find("Fill");
            if (fillT == null)
            {
                GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fillGo.transform.SetParent(bhBarGo.transform, false);
                fillT = fillGo.transform;
            }
            RectTransform fillRt = fillT.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.sizeDelta = Vector2.zero;

            blackHoleBarFill = fillT.GetComponent<Image>();
            blackHoleBarFill.type = Image.Type.Filled;
            blackHoleBarFill.fillMethod = Image.FillMethod.Horizontal;
            blackHoleBarFill.color = new Color(0.2f, 0.9f, 1f, 0.95f);

            Transform textT = bhBarGo.transform.Find("Text");
            if (textT == null)
            {
                GameObject tGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.transform.SetParent(bhBarGo.transform, false);
                textT = tGo.transform;
            }
            RectTransform tRt = textT.GetComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero;
            tRt.anchorMax = Vector2.one;
            tRt.sizeDelta = Vector2.zero;

            blackHoleBarText = textT.GetComponent<TextMeshProUGUI>();
            blackHoleBarText.alignment = TextAlignmentOptions.Center;
            blackHoleBarText.fontSize = 13;
            blackHoleBarText.fontStyle = FontStyles.Bold;
            blackHoleBarText.color = Color.white;

            // Stage Header (directly above the Mass Bar)
            Transform stageHeaderT = bottomHUDPanel.transform.Find("Text_StageHeader");
            if (stageHeaderT == null)
            {
                GameObject shGo = new GameObject("Text_StageHeader", typeof(RectTransform), typeof(TextMeshProUGUI));
                shGo.transform.SetParent(bottomHUDPanel.transform, false);
                stageHeaderT = shGo.transform;
            }
            RectTransform shRt = stageHeaderT.GetComponent<RectTransform>();
            shRt.anchorMin = new Vector2(0.5f, 1f);
            shRt.anchorMax = new Vector2(0.5f, 1f);
            shRt.pivot = new Vector2(0.5f, 0f);
            shRt.anchoredPosition = new Vector2(0f, 6f);
            shRt.sizeDelta = new Vector2(520f, 26f);

            stageHeaderText = stageHeaderT.GetComponent<TextMeshProUGUI>();
            stageHeaderText.alignment = TextAlignmentOptions.Center;
            stageHeaderText.fontSize = 14;
            stageHeaderText.fontStyle = FontStyles.Bold;
            stageHeaderText.color = new Color(0.2f, 0.92f, 1f);
        }

        private void UpdateWarningHUD()
        {
            if (!_isWarningActive) return;

            if (warningCountdownText != null)
            {
                warningCountdownText.text = LocalizationManager.Get("HUD_BoundaryWarning", _warningRemainingSeconds);
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
                quotaCelebrationText.text = LocalizationManager.Get("HUD_QuotaCelebration", quotaLevel, rewardCoins, speedBonus);
            }

            quotaCelebrationPanel.SetActive(true);
            Transform bannerT = quotaCelebrationPanel.transform;

            if (GameAudioManager.Instance != null)
            {
                GameAudioManager.Instance.PlayQuotaComplete();
            }

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

            yield return new WaitForSecondsRealtime(2.5f);

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

        public void ShowCenterWarningAlert(string message, float duration = 2.0f)
        {
            if (_beamAlertCoroutine != null)
            {
                StopCoroutine(_beamAlertCoroutine);
            }
            _beamAlertCoroutine = StartCoroutine(ShowCenterWarningAlertRoutine(message, duration));
        }

        private void EnsureBeamAlertPanel()
        {
            if (beamAlertPanel != null && beamAlertText != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Transform existing = canvas.transform.Find("Panel_BeamDamageAlert");
            if (existing != null)
            {
                beamAlertPanel = existing.gameObject;
                beamAlertText = beamAlertPanel.GetComponentInChildren<TextMeshProUGUI>();
                beamAlertCanvasGroup = beamAlertPanel.GetComponent<CanvasGroup>();
                return;
            }

            GameObject panelGo = new GameObject("Panel_BeamDamageAlert");
            panelGo.transform.SetParent(canvas.transform, false);
            RectTransform rt = panelGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.72f);
            rt.anchorMax = new Vector2(0.5f, 0.72f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(720f, 52f);

            Image bg = panelGo.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.02f, 0.03f, 0.88f);

            Outline outline = panelGo.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.25f, 0.1f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            beamAlertCanvasGroup = panelGo.AddComponent<CanvasGroup>();

            GameObject textGo = new GameObject("Text_Alert");
            textGo.transform.SetParent(panelGo.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            beamAlertText = textGo.AddComponent<TextMeshProUGUI>();
            beamAlertText.alignment = TextAlignmentOptions.Center;
            beamAlertText.fontSize = 17;
            beamAlertText.fontStyle = FontStyles.Bold;
            beamAlertText.color = new Color(1f, 0.35f, 0.15f);

            beamAlertPanel = panelGo;
            beamAlertPanel.SetActive(false);
        }

        private IEnumerator ShowCenterWarningAlertRoutine(string message, float duration)
        {
            EnsureBeamAlertPanel();

            if (beamAlertPanel != null && beamAlertText != null)
            {
                beamAlertText.text = message;
                beamAlertPanel.SetActive(true);
                if (beamAlertCanvasGroup != null) beamAlertCanvasGroup.alpha = 1f;

                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float pulse = (Mathf.Sin(elapsed * 20f) + 1f) * 0.5f;
                    beamAlertText.color = Color.Lerp(new Color(1f, 0.2f, 0.1f), new Color(1f, 0.95f, 0.2f), pulse);
                    yield return null;
                }

                float fadeElapsed = 0f;
                const float fadeDuration = 0.35f;
                while (fadeElapsed < fadeDuration)
                {
                    fadeElapsed += Time.unscaledDeltaTime;
                    if (beamAlertCanvasGroup != null)
                    {
                        beamAlertCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeElapsed / fadeDuration);
                    }
                    yield return null;
                }

                beamAlertPanel.SetActive(false);
            }
            _beamAlertCoroutine = null;
        }
    }
}
