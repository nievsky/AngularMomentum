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

        [Header("Juicy Numbers Go Up Mass Counter")]
        [SerializeField] private TextMeshProUGUI massNumberText;
        public TextMeshProUGUI MassNumberText => massNumberText != null ? massNumberText : blackHoleBarText;

        private float _displayedMassValue = 1f;
        private float _lastKnownTargetMass = -1f;
        private Tweener _massRollTween;
        private Tween _stage4TremorTween;
        private Vector2 _originalMassTextAnchorPos = Vector2.zero;
        private bool _isStage4Tremoring = false;

        [Header("Camera Viewfinder Overlay")]
        [SerializeField] private GameObject cameraViewfinderOverlay;
        [SerializeField] private Image recDotImage;

        public GameObject CameraViewfinderOverlay => cameraViewfinderOverlay;
        public Image RecDotImage => recDotImage;

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
        private bool _isHUDHidden = false;
        private int _lastCoins = -1;

        public bool IsOperatorConnected => _isOperatorConnected;
        public bool IsHUDHidden => _isHUDHidden;
        public event System.Action OnOperatorConnected;

        public void HideAllHUD()
        {
            _isHUDHidden = true;
            if (bottomHUDPanel != null) bottomHUDPanel.SetActive(false);
            if (scrapCoinsBadge != null) scrapCoinsBadge.SetActive(false);
            if (crosshairImage != null) crosshairImage.gameObject.SetActive(false);
            if (cameraViewfinderOverlay != null) cameraViewfinderOverlay.SetActive(false);
            if (warningPanel != null) warningPanel.SetActive(false);
            if (observedNoticePanel != null) observedNoticePanel.SetActive(false);
            if (quotaCelebrationPanel != null) quotaCelebrationPanel.SetActive(false);
            if (startOverlayPanel != null) startOverlayPanel.SetActive(false);
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (beamAlertPanel != null) beamAlertPanel.SetActive(false);
            Canvas parentCanvas = GetComponentInParent<Canvas>() ?? UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (parentCanvas != null)
            {
                Transform p = parentCanvas.transform.Find("Panel_BeamDamageAlert");
                if (p != null) p.gameObject.SetActive(false);
            }
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
            if (beamAlertPanel != null) beamAlertPanel.SetActive(false);
            if (beamAlertCanvasGroup != null) beamAlertCanvasGroup.alpha = 0f;
            if (warningBorderImage != null)
            {
                warningBorderImage.raycastTarget = false;
                Color c = warningBorderPulsingColor;
                c.a = 0f;
                warningBorderImage.color = c;
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

            // Setup Start Overlay (Decommissioned in favor of TutorialSlideshowUI)
            if (startOverlayPanel != null)
            {
                startOverlayPanel.SetActive(false);
            }
            _isOperatorConnected = false;

            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.OnMassChanged += HandleBlackHoleMassChanged;
            }

            EnsureMinimalistHUD();
            EnsureViewfinderOverlay();
        }

        private void OnDestroy()
        {
            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.OnMassChanged -= HandleBlackHoleMassChanged;
            }
            _massRollTween?.Kill();
            _stage4TremorTween?.Kill();
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
            if (TutorialSlideshowUI.IsTutorialOpen) return;

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

            OnOperatorConnected?.Invoke();
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
            if (_isHUDHidden) return;
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

            // 2. Hide HUD Clutter (Stage header and progress bar fill)
            if (stageHeaderText != null && stageHeaderText.gameObject.activeSelf)
            {
                stageHeaderText.gameObject.SetActive(false);
            }
            if (blackHoleBarFill != null && blackHoleBarFill.gameObject.activeSelf)
            {
                blackHoleBarFill.gameObject.SetActive(false);
            }

            // 3. Pure Dynamic Number Starting at 1 (displayMass = 1 + currentMass)
            float targetDisplayMass = 1f + mass;

            if (_lastKnownTargetMass < 0f)
            {
                _lastKnownTargetMass = targetDisplayMass;
                _displayedMassValue = targetDisplayMass;
                UpdateMassTextString();
            }
            else if (targetDisplayMass > _lastKnownTargetMass)
            {
                float gain = targetDisplayMass - _lastKnownTargetMass;
                _lastKnownTargetMass = targetDisplayMass;
                OnMassGainedJuice(gain, targetDisplayMass);
            }
            else if (targetDisplayMass < _lastKnownTargetMass)
            {
                _lastKnownTargetMass = targetDisplayMass;
                _displayedMassValue = targetDisplayMass;
                UpdateMassTextString();
            }

            // 4. Dynamic Font Size & Color Scaling by Mass across Stages 1-4
            ApplyMassVisualStyling(mass, stage, stageIndex, config);
        }

        private void HandleBlackHoleMassChanged(float mass, int stage)
        {
            float targetDisplayMass = 1f + mass;
            if (_lastKnownTargetMass < 0f)
            {
                _lastKnownTargetMass = targetDisplayMass;
                _displayedMassValue = targetDisplayMass;
                UpdateMassTextString();
            }
            else if (targetDisplayMass > _lastKnownTargetMass)
            {
                float gain = targetDisplayMass - _lastKnownTargetMass;
                _lastKnownTargetMass = targetDisplayMass;
                OnMassGainedJuice(gain, targetDisplayMass);
            }
        }

        private void UpdateMassTextString()
        {
            TextMeshProUGUI txt = MassNumberText;
            if (txt == null) return;
            long displayInt = (long)System.Math.Round(_displayedMassValue);
            if (displayInt < 1) displayInt = 1;
            txt.text = displayInt.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private void OnMassGainedJuice(float massGain, float newTargetMass)
        {
            TextMeshProUGUI txt = MassNumberText;
            if (txt == null) return;

            // 1. Smoothly roll displayedMassValue to new target mass over 0.35s
            _massRollTween?.Kill();
            _massRollTween = DOTween.To(() => _displayedMassValue, x =>
            {
                _displayedMassValue = x;
                UpdateMassTextString();
            }, newTargetMass, 0.35f).SetEase(Ease.OutQuad).SetUpdate(true);

            // 2. Snappy DOTween scale punch + random rotational wobble + DOShakeAnchorPos
            RectTransform rt = txt.rectTransform;
            rt.DOKill(complete: false);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = _originalMassTextAnchorPos;

            // Snappy scale punch
            rt.DOPunchScale(Vector3.one * 0.28f, 0.25f, 8, 0.7f).SetUpdate(true);

            // Playful random rotational wobble/shake
            float wobbleZ = UnityEngine.Random.Range(-7f, 7f);
            if (Mathf.Abs(wobbleZ) < 2.0f) wobbleZ = wobbleZ < 0 ? -4.5f : 4.5f;
            rt.DOPunchRotation(new Vector3(0f, 0f, wobbleZ), 0.25f, 10).SetUpdate(true);

            // DOShakeAnchorPos scaled by how big the mass gain was
            float shakeStrength = Mathf.Clamp(massGain * 0.08f, 2.0f, 15f);
            rt.DOShakeAnchorPos(0.25f, shakeStrength, 14, 90f).SetUpdate(true).OnComplete(() =>
            {
                if (!_isStage4Tremoring && rt != null)
                {
                    rt.anchoredPosition = _originalMassTextAnchorPos;
                    rt.localRotation = Quaternion.identity;
                }
            });
        }

        private void ApplyMassVisualStyling(float actualMass, int stage, int stageIndex, GameBalanceConfig config)
        {
            TextMeshProUGUI txt = MassNumberText;
            if (txt == null) return;

            float prevTarget = (stageIndex > 0 && config != null && config.stageMassTargets != null && stageIndex - 1 < config.stageMassTargets.Length)
                ? config.stageMassTargets[stageIndex - 1]
                : 0f;
            float currTarget = (config != null && config.stageMassTargets != null && stageIndex < config.stageMassTargets.Length)
                ? config.stageMassTargets[stageIndex]
                : (prevTarget + 1000f);

            float stageProgress = Mathf.Clamp01((actualMass - prevTarget) / Mathf.Max(1f, currTarget - prevTarget));
            float totalProgress = Mathf.Clamp01((stageIndex + stageProgress) / 4.0f);

            // 1. Dynamic Font Size (48pt at mass 1 up to 92pt at high mass)
            txt.fontSize = Mathf.Lerp(48f, 92f, totalProgress);

            // 2. Dynamic Color Scaling
            // Low Mass (Stage 1): Crisp Ice White (#FFFFFF) -> Soft Cyan (#60F0FF)
            // Medium Mass (Stage 2): Electric Gold (#FFE040)
            // High Mass (Stage 3): Fiery Orange (#FF6A10)
            // Critical Mass (Stage 4): Blazing Crimson-Magenta (#FF1845) with tremor!
            Color white = new Color(1f, 1f, 1f, 1f);
            Color softCyan = new Color(0.376f, 0.941f, 1.0f, 1f);
            Color electricGold = new Color(1.0f, 0.878f, 0.251f, 1f);
            Color fieryOrange = new Color(1.0f, 0.416f, 0.063f, 1f);
            Color blazingCrimson = new Color(1.0f, 0.094f, 0.271f, 1f);

            Color targetColor;
            if (stage <= 1)
            {
                targetColor = Color.Lerp(white, softCyan, stageProgress);
            }
            else if (stage == 2)
            {
                targetColor = Color.Lerp(softCyan, electricGold, stageProgress);
            }
            else if (stage == 3)
            {
                targetColor = Color.Lerp(electricGold, fieryOrange, stageProgress);
            }
            else // Stage 4+
            {
                targetColor = Color.Lerp(fieryOrange, blazingCrimson, stageProgress);
                float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 6f));
                targetColor = Color.Lerp(targetColor, blazingCrimson, pulse * 0.4f);
            }

            txt.color = targetColor;

            // 3. Stage 4 Subtle Continuous DOTween Tremor
            if (stage >= 4)
            {
                if (!_isStage4Tremoring)
                {
                    _isStage4Tremoring = true;
                    _stage4TremorTween?.Kill();
                    _stage4TremorTween = txt.rectTransform.DOShakeAnchorPos(1.0f, 2.2f, 16, 90f, false, false)
                        .SetLoops(-1, LoopType.Restart)
                        .SetUpdate(true);
                }
            }
            else
            {
                if (_isStage4Tremoring)
                {
                    _isStage4Tremoring = false;
                    _stage4TremorTween?.Kill();
                    _stage4TremorTween = null;
                    txt.rectTransform.anchoredPosition = _originalMassTextAnchorPos;
                }
            }
        }

        /// <summary>
        /// Triggers a sharp red punch scale tween on the Scrap Coins HUD counter when coins are lost on death.
        /// </summary>
        public void TriggerScrapCoinsLossPunch()
        {
            if (scrapCoinsBadge != null)
            {
                scrapCoinsBadge.transform.DOKill();
                scrapCoinsBadge.transform.localScale = Vector3.one;
                scrapCoinsBadge.transform.DOPunchScale(Vector3.one * 0.25f, 0.35f, 7, 0.6f).SetUpdate(true);
            }

            if (scrapCoinsText != null)
            {
                scrapCoinsText.DOKill();
                Color normalColor = new Color(1.0f, 0.88f, 0.25f);
                scrapCoinsText.color = new Color(1.0f, 0.15f, 0.15f);
                scrapCoinsText.DOColor(normalColor, 0.5f).SetUpdate(true);
            }
        }

        /// <summary>
        /// Smoothly fades out and deactivates the bottom mass/stage HUD bar for cinematic finale sequences.
        /// </summary>
        public void FadeOutBottomMassBar(float duration = 1.0f)
        {
            if (bottomHUDPanel == null) return;
            CanvasGroup cg = bottomHUDPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = bottomHUDPanel.AddComponent<CanvasGroup>();

            _stage4TremorTween?.Kill();
            _stage4TremorTween = null;
            _isStage4Tremoring = false;

            cg.DOKill();
            cg.DOFade(0f, duration).SetUpdate(true).OnComplete(() =>
            {
                if (bottomHUDPanel != null) bottomHUDPanel.SetActive(false);
            });
        }

        /// <summary>
        /// Immediately hides the bottom mass/stage HUD bar.
        /// </summary>
        public void HideBottomMassBar()
        {
            _stage4TremorTween?.Kill();
            _stage4TremorTween = null;
            _isStage4Tremoring = false;

            if (bottomHUDPanel != null)
            {
                bottomHUDPanel.SetActive(false);
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
            string[] legacyNames = { "SurveillanceMonitorFrame", "Text_REC_Status", "Panel_QuotaAndHeat", "Text_BossTracker", "Panel_CriticalMassWarning", "DamageFlashOverlay", "StageWarningBanner", "BoundaryWarningPanel", "Panel_FinaleAlert" };
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
            bottomRt.anchoredPosition = new Vector2(0f, 30f);
            bottomRt.sizeDelta = new Vector2(900f, 140f);

            // Hide legacy battery bar if present
            Transform batT = bottomHUDPanel.transform.Find("Bar_Battery");
            if (batT != null) SafeDestroy(batT.gameObject);

            // Hide legacy Stage Header
            Transform stageHeaderT = bottomHUDPanel.transform.Find("Text_StageHeader");
            if (stageHeaderT != null)
            {
                stageHeaderT.gameObject.SetActive(false);
            }
            if (stageHeaderText != null)
            {
                stageHeaderText.gameObject.SetActive(false);
            }

            // Setup or re-anchor Bar_BlackHole container
            Transform bhT = bottomHUDPanel.transform.Find("Bar_BlackHole");
            GameObject bhBarGo;
            if (bhT == null)
            {
                bhBarGo = new GameObject("Bar_BlackHole", typeof(RectTransform));
                bhBarGo.transform.SetParent(bottomHUDPanel.transform, false);
            }
            else
            {
                bhBarGo = bhT.gameObject;
            }

            RectTransform bhRt = bhBarGo.GetComponent<RectTransform>();
            bhRt.anchorMin = new Vector2(0.5f, 0.5f);
            bhRt.anchorMax = new Vector2(0.5f, 0.5f);
            bhRt.pivot = new Vector2(0.5f, 0.5f);
            bhRt.sizeDelta = new Vector2(900f, 140f);
            bhRt.anchoredPosition = Vector2.zero;

            // Remove/disable background image and outline on Bar_BlackHole
            Image bhBg = bhBarGo.GetComponent<Image>();
            if (bhBg != null) bhBg.enabled = false;

            Outline bhOutline = bhBarGo.GetComponent<Outline>();
            if (bhOutline != null) bhOutline.enabled = false;

            // Hide horizontal fill image
            Transform fillT = bhBarGo.transform.Find("Fill");
            if (fillT != null)
            {
                fillT.gameObject.SetActive(false);
            }
            if (blackHoleBarFill != null)
            {
                blackHoleBarFill.gameObject.SetActive(false);
            }

            // Setup Mass Number Text
            Transform textT = bhBarGo.transform.Find("Text");
            if (textT == null)
            {
                GameObject tGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.transform.SetParent(bhBarGo.transform, false);
                textT = tGo.transform;
            }
            RectTransform tRt = textT.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0.5f, 0.5f);
            tRt.anchorMax = new Vector2(0.5f, 0.5f);
            tRt.pivot = new Vector2(0.5f, 0.5f);
            tRt.sizeDelta = new Vector2(900f, 140f);
            tRt.anchoredPosition = Vector2.zero;
            _originalMassTextAnchorPos = Vector2.zero;

            blackHoleBarText = textT.GetComponent<TextMeshProUGUI>();
            blackHoleBarText.alignment = TextAlignmentOptions.Center;
            blackHoleBarText.fontSize = 48;
            blackHoleBarText.fontStyle = FontStyles.Bold;
            blackHoleBarText.color = Color.white;
            blackHoleBarText.textWrappingMode = TextWrappingModes.NoWrap;
            blackHoleBarText.overflowMode = TextOverflowModes.Overflow;
            blackHoleBarText.raycastTarget = false;
            massNumberText = blackHoleBarText;

            // 4. Ensure Camera Viewfinder Overlay (Corner Brackets + ● REC)
            EnsureViewfinderOverlay(canvas);
        }

        public void EnsureViewfinderOverlay(Canvas canvas = null)
        {
            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
                if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
                if (canvas == null) return;
            }

            if (cameraViewfinderOverlay == null)
            {
                Transform existing = canvas.transform.Find("CameraViewfinderOverlay");
                if (existing != null) cameraViewfinderOverlay = existing.gameObject;
            }

            if (cameraViewfinderOverlay == null)
            {
                GameObject vfGo = new GameObject("CameraViewfinderOverlay", typeof(RectTransform));
                vfGo.transform.SetParent(canvas.transform, false);
                vfGo.transform.SetAsFirstSibling(); // Behind text HUD, in front of 3D world
                RectTransform rt = vfGo.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;

                Color bracketColor = new Color(0.35f, 0.9f, 1f, 0.35f);

                void CreateBracket(string name, Vector2 anchor, Vector2 offset, bool isRight, bool isBottom)
                {
                    GameObject brGo = new GameObject(name, typeof(RectTransform));
                    brGo.transform.SetParent(vfGo.transform, false);
                    RectTransform brRt = brGo.GetComponent<RectTransform>();
                    brRt.anchorMin = anchor;
                    brRt.anchorMax = anchor;
                    brRt.pivot = anchor;
                    brRt.anchoredPosition = offset;
                    brRt.sizeDelta = new Vector2(36f, 36f);

                    // Horizontal line
                    GameObject hBar = new GameObject("H", typeof(RectTransform), typeof(Image));
                    hBar.transform.SetParent(brGo.transform, false);
                    RectTransform hRt = hBar.GetComponent<RectTransform>();
                    hRt.anchorMin = isBottom ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
                    hRt.anchorMax = isBottom ? new Vector2(1f, 0f) : new Vector2(1f, 1f);
                    hRt.pivot = isBottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
                    hRt.sizeDelta = new Vector2(0f, 2f);
                    hRt.anchoredPosition = Vector2.zero;
                    Image hImg = hBar.GetComponent<Image>();
                    hImg.color = bracketColor;
                    hImg.raycastTarget = false;

                    // Vertical line
                    GameObject vBar = new GameObject("V", typeof(RectTransform), typeof(Image));
                    vBar.transform.SetParent(brGo.transform, false);
                    RectTransform vRt = vBar.GetComponent<RectTransform>();
                    vRt.anchorMin = isRight ? new Vector2(1f, 0f) : new Vector2(0f, 0f);
                    vRt.anchorMax = isRight ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
                    vRt.pivot = isRight ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
                    vRt.sizeDelta = new Vector2(2f, 0f);
                    vRt.anchoredPosition = Vector2.zero;
                    Image vImg = vBar.GetComponent<Image>();
                    vImg.color = bracketColor;
                    vImg.raycastTarget = false;
                }

                CreateBracket("Bracket_TL", new Vector2(0f, 1f), new Vector2(28f, -28f), false, false);
                CreateBracket("Bracket_TR", new Vector2(1f, 1f), new Vector2(-28f, -28f), true, false);
                CreateBracket("Bracket_BL", new Vector2(0f, 0f), new Vector2(28f, 28f), false, true);
                CreateBracket("Bracket_BR", new Vector2(1f, 0f), new Vector2(-28f, 28f), true, true);

                // Top-Left ● REC Indicator
                GameObject recGo = new GameObject("REC_Indicator", typeof(RectTransform));
                recGo.transform.SetParent(vfGo.transform, false);
                RectTransform recRt = recGo.GetComponent<RectTransform>();
                recRt.anchorMin = new Vector2(0f, 1f);
                recRt.anchorMax = new Vector2(0f, 1f);
                recRt.pivot = new Vector2(0f, 1f);
                recRt.anchoredPosition = new Vector2(46f, -38f);
                recRt.sizeDelta = new Vector2(90f, 24f);

                // Red Circle Dot
                GameObject dotGo = new GameObject("Dot_REC", typeof(RectTransform), typeof(Image));
                dotGo.transform.SetParent(recGo.transform, false);
                RectTransform dotRt = dotGo.GetComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(0f, 0.5f);
                dotRt.anchorMax = new Vector2(0f, 0.5f);
                dotRt.pivot = new Vector2(0f, 0.5f);
                dotRt.anchoredPosition = Vector2.zero;
                dotRt.sizeDelta = new Vector2(10f, 10f);
                recDotImage = dotGo.GetComponent<Image>();
                recDotImage.color = new Color(1f, 0.15f, 0.15f, 1f);
                recDotImage.raycastTarget = false;

                // REC text
                GameObject textGo = new GameObject("Text_REC", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(recGo.transform, false);
                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = new Vector2(0f, 0.5f);
                textRt.anchorMax = new Vector2(1f, 0.5f);
                textRt.pivot = new Vector2(0f, 0.5f);
                textRt.anchoredPosition = new Vector2(18f, 0f);
                textRt.sizeDelta = new Vector2(-18f, 20f);
                var tmp = textGo.GetComponent<TextMeshProUGUI>();
                tmp.text = "REC";
                tmp.fontSize = 14;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = new Color(0.9f, 0.95f, 1f, 0.85f);
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.raycastTarget = false;

                cameraViewfinderOverlay = vfGo;
            }

            if (recDotImage == null && cameraViewfinderOverlay != null)
            {
                var dotT = cameraViewfinderOverlay.transform.Find("REC_Indicator/Dot_REC");
                if (dotT != null) recDotImage = dotT.GetComponent<Image>();
            }

            if (recDotImage != null)
            {
                recDotImage.DOKill();
                recDotImage.color = new Color(1f, 0.15f, 0.15f, 1f);
                recDotImage.DOFade(0.15f, 0.65f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
            }
        }

        private void UpdateWarningHUD()
        {
            if (!_isWarningActive || _warningRemainingSeconds <= 0f)
            {
                if (warningPanel != null && warningPanel.activeSelf)
                {
                    warningPanel.SetActive(false);
                }
                if (warningBorderImage != null && warningBorderImage.color.a > 0f)
                {
                    Color c = warningBorderPulsingColor;
                    c.a = 0f;
                    warningBorderImage.color = c;
                }
                _isWarningActive = false;
                return;
            }

            if (warningCountdownText != null)
            {
                warningCountdownText.text = LocalizationManager.Get("HUD_BoundaryWarning", _warningRemainingSeconds);
            }

            if (warningBorderImage != null)
            {
                float pulse = (Mathf.Sin(Time.time * 9f) + 1f) * 0.5f;
                Color c = warningBorderPulsingColor;
                c.a = Mathf.Lerp(0.12f, 0.45f, pulse);
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
            // Legacy quota celebration top banner ("КВОТА #X ВЫПОЛНЕНА") removed per design request
            // Only clean Stage & Mass HUD remains
            if (quotaCelebrationPanel != null)
            {
                quotaCelebrationPanel.SetActive(false);
            }
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
            // Fully disabled per Task 1 Issue #4: No red UI popups/banners on beam hit
            if (_beamAlertCoroutine != null)
            {
                StopCoroutine(_beamAlertCoroutine);
                _beamAlertCoroutine = null;
            }
            if (beamAlertPanel != null)
            {
                beamAlertPanel.SetActive(false);
            }
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
                beamAlertPanel.SetActive(false);
                if (beamAlertCanvasGroup != null) beamAlertCanvasGroup.alpha = 0f;
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

            if (string.IsNullOrWhiteSpace(message) || duration <= 0f)
            {
                if (beamAlertPanel != null) beamAlertPanel.SetActive(false);
                yield break;
            }

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
