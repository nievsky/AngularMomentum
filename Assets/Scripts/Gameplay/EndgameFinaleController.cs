using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class EndgameFinaleController : MonoBehaviour
    {
        public static EndgameFinaleController Instance { get; private set; }

        [Header("Runtime Status")]
        [SerializeField] private bool isFinaleTriggered = false;
        [SerializeField] private bool isEndlessMode = false;

        private QuotaAndScoreManager _quotaManager;
        private BlackHoleGravity _blackHole;
        private Transform _bhVisualRoot;
        private Vector3 _baseBHScale = Vector3.one * 6f;
        private float _baseEventHorizonRadius = 3.5f;

        private GameObject _finaleAlertPanel;
        private TextMeshProUGUI _finaleAlertText;
        private GameObject _victorySummaryPanel;

        public bool IsFinaleTriggered => isFinaleTriggered;

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
            _quotaManager = FindAnyObjectByType<QuotaAndScoreManager>();
            if (_quotaManager != null)
            {
                _quotaManager.OnQuotaCompleted += HandleQuotaCompleted;
            }

            _blackHole = FindAnyObjectByType<BlackHoleGravity>();
            if (_blackHole != null)
            {
                Transform vr = _blackHole.transform.Find("VisualRoot");
                _bhVisualRoot = vr != null ? vr : _blackHole.transform;
                _baseBHScale = _bhVisualRoot.localScale;
            }

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null)
            {
                _baseEventHorizonRadius = config.eventHorizonRadius;
            }

            BuildFinaleUI();
        }

        private void OnDestroy()
        {
            if (_quotaManager != null)
            {
                _quotaManager.OnQuotaCompleted -= HandleQuotaCompleted;
            }
        }

        private void HandleQuotaCompleted(int quotaLevel, int rewardCoins)
        {
            if (isEndlessMode) return;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null) return;

            int completedCount = _quotaManager != null ? _quotaManager.CompletedQuotasCount : quotaLevel;

            // 1. Smoothly scale Black Hole up with each quota
            float scaleMultiplier = 1f + (completedCount * config.blackHoleScalePerQuota);
            if (_bhVisualRoot != null)
            {
                StopAllCoroutines();
                StartCoroutine(ScaleBlackHoleRoutine(_baseBHScale * scaleMultiplier));
            }

            // Expand event horizon radius
            config.eventHorizonRadius = _baseEventHorizonRadius + (completedCount * config.blackHoleScalePerQuota * 0.75f);

            // 2. Check if endgame sequence should trigger
            if (!isFinaleTriggered && completedCount >= config.quotasToTriggerFinale)
            {
                TriggerEndgameFinale();
            }
        }

        public void TriggerEndgameFinale()
        {
            if (isFinaleTriggered) return;
            isFinaleTriggered = true;
            StopAllCoroutines();
            StartCoroutine(EndgameCollapseSequenceRoutine());
        }

        private IEnumerator ScaleBlackHoleRoutine(Vector3 targetScale)
        {
            if (_bhVisualRoot == null) yield break;

            Vector3 startScale = _bhVisualRoot.localScale;
            float elapsed = 0f;
            const float duration = 1.8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _bhVisualRoot.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            _bhVisualRoot.localScale = targetScale;
        }

        private IEnumerator EndgameCollapseSequenceRoutine()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            Vector3 center = _blackHole != null ? _blackHole.transform.position : Vector3.zero;

            // 1. Display Flashing Alert Banner
            if (_finaleAlertPanel != null)
            {
                _finaleAlertPanel.SetActive(true);
            }

            StartCoroutine(FlashAlertRoutine());

            // -------------------------------------------------------------
            // Phase 1 (0 to 4s): Inward vacuum pulls all Scrap & Helper Bots
            // -------------------------------------------------------------
            float phase1Timer = 0f;
            while (phase1Timer < 4.0f)
            {
                phase1Timer += Time.deltaTime;

                var simManager = ScrapSimulationManager.Instance;
                if (simManager != null)
                {
                    foreach (var scrap in simManager.ActiveScrap)
                    {
                        if (scrap == null || !scrap.gameObject.activeInHierarchy || scrap.CurrentState == ScrapState.Consumed) continue;
                        Vector3 toCenter = (center - scrap.transform.position).normalized;
                        scrap.TransitionToLaunched(toCenter * 45f);
                    }
                }

                var helperMgr = HelperDroneManager.Instance;
                if (helperMgr != null)
                {
                    foreach (var bot in helperMgr.ActiveBots)
                    {
                        if (bot == null || !bot.gameObject.activeInHierarchy || bot.CurrentState == HelperBotState.Respawning) continue;
                        Vector3 toCenter = (center - bot.transform.position).normalized;
                        bot.transform.position += toCenter * (35f * Time.deltaTime);
                        if (Vector3.Distance(bot.transform.position, center) <= (config != null ? config.eventHorizonRadius : 4f))
                        {
                            bot.gameObject.SetActive(false);
                        }
                    }
                }

                yield return null;
            }

            // -------------------------------------------------------------
            // Phase 2 (4 to 8s): Detach all Surveillance Cameras
            // -------------------------------------------------------------
            var cameras = Object.FindObjectsByType<DynamicSurveillanceCamera>(FindObjectsInactive.Exclude);
            float phase2Timer = 0f;

            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                cam.enabled = false;
                var rb = cam.GetComponent<Rigidbody>();
                if (rb == null) rb = cam.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = false;
                rb.linearDamping = 0.05f;

                var gb = cam.GetComponent<GravityBody>();
                if (gb == null) gb = cam.gameObject.AddComponent<GravityBody>();
                if (_blackHole != null) _blackHole.RegisterBody(gb);
            }

            while (phase2Timer < 4.0f)
            {
                phase2Timer += Time.deltaTime;

                for (int i = cameras.Length - 1; i >= 0; i--)
                {
                    var cam = cameras[i];
                    if (cam == null) continue;

                    Vector3 toCenter = (center - cam.transform.position).normalized;
                    var rb = cam.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = toCenter * 32f;
                    }

                    float d = Vector3.Distance(cam.transform.position, center);
                    if (d <= (config != null ? config.eventHorizonRadius + 1.0f : 5.0f))
                    {
                        // Spaghettify and consume camera
                        var gb = cam.GetComponent<GravityBody>();
                        if (_blackHole != null && gb != null)
                        {
                            _blackHole.ConsumeObject(gb);
                        }

                        // Award 5,000 bonus points
                        if (ScorePopupManager.Instance != null)
                        {
                            ScorePopupManager.Instance.SpawnPopup(cam.transform.position, 5000f, false, false);
                        }
                        if (_quotaManager != null)
                        {
                            _quotaManager.AddScrapCoins(50);
                        }

                        cameras[i] = null;
                    }
                }

                yield return null;
            }

            // -------------------------------------------------------------
            // Phase 3 (8 to 12s): Break Overseer Eye Orbit & Consume Boss Eye
            // -------------------------------------------------------------
            var overseerEye = FindAnyObjectByType<CorporateOverseerEye>();
            if (overseerEye != null)
            {
                overseerEye.isConsumedOrCollapsing = true;
            }

            Camera mainCam = Camera.main;
            Vector3 camInitialLocalPos = mainCam != null ? mainCam.transform.localPosition : Vector3.zero;

            float phase3Timer = 0f;
            while (phase3Timer < 4.0f)
            {
                phase3Timer += Time.deltaTime;
                float progress = phase3Timer / 4.0f;

                // Dramatic Camera Shake
                if (mainCam != null)
                {
                    float shake = Mathf.Lerp(0.05f, 0.35f, progress);
                    mainCam.transform.localPosition = camInitialLocalPos + (Random.insideUnitSphere * shake);
                }

                if (overseerEye != null)
                {
                    Vector3 toCenter = (center - overseerEye.transform.position);
                    float d = toCenter.magnitude;
                    overseerEye.transform.position += toCenter.normalized * (28f * Time.deltaTime);

                    // Dramatic spin and shrink
                    overseerEye.transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);
                    overseerEye.transform.localScale = Vector3.Lerp(Vector3.one, new Vector3(0.1f, 0.1f, 3.5f), progress);

                    if (d <= (config != null ? config.eventHorizonRadius + 1.5f : 6.0f))
                    {
                        if (ScorePopupManager.Instance != null)
                        {
                            ScorePopupManager.Instance.SpawnPopup(center, 25000f, true, true);
                        }
                        if (_quotaManager != null)
                        {
                            _quotaManager.AddScrapCoins(250);
                        }

                        Destroy(overseerEye.gameObject);
                        overseerEye = null;
                    }
                }

                yield return null;
            }

            if (mainCam != null)
            {
                mainCam.transform.localPosition = camInitialLocalPos;
            }

            if (_finaleAlertPanel != null)
            {
                _finaleAlertPanel.SetActive(false);
            }

            // -------------------------------------------------------------
            // Phase 4: Victory Summary Panel
            // -------------------------------------------------------------
            yield return new WaitForSeconds(0.6f);
            ShowVictorySummaryPanel();
        }

        private IEnumerator FlashAlertRoutine()
        {
            if (_finaleAlertText == null) yield break;

            Color c1 = new Color(1f, 0.2f, 0.2f, 1f);
            Color c2 = new Color(1f, 0.85f, 0.1f, 1f);

            while (isFinaleTriggered && _finaleAlertPanel != null && _finaleAlertPanel.activeSelf)
            {
                float t = 0.5f + (0.5f * Mathf.Sin(Time.time * 8f));
                _finaleAlertText.color = Color.Lerp(c1, c2, t);
                yield return null;
            }
        }

        private void BuildFinaleUI()
        {
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas == null) return;

            // 1. Alert Banner
            Transform existingAlert = hudCanvas.transform.Find("Panel_FinaleAlert");
            if (existingAlert == null)
            {
                _finaleAlertPanel = new GameObject("Panel_FinaleAlert", typeof(RectTransform), typeof(Image));
                _finaleAlertPanel.transform.SetParent(hudCanvas.transform, false);

                RectTransform rt = _finaleAlertPanel.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -95f);
                rt.sizeDelta = new Vector2(850f, 48f);

                Image bg = _finaleAlertPanel.GetComponent<Image>();
                bg.color = new Color(0.12f, 0.02f, 0.02f, 0.92f);

                GameObject textGo = new GameObject("Text_Alert", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(_finaleAlertPanel.transform, false);
                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(8f, 4f);
                textRt.offsetMax = new Vector2(-8f, -4f);

                _finaleAlertText = textGo.GetComponent<TextMeshProUGUI>();
                _finaleAlertText.alignment = TextAlignmentOptions.Center;
                _finaleAlertText.fontSize = 17;
                _finaleAlertText.fontStyle = FontStyles.Bold;
                _finaleAlertText.text = "CRITICAL MASS REACHED! SINGULARITY OVERFLOW — INITIATING SECTOR COLLAPSE!";
                _finaleAlertText.color = new Color(1f, 0.3f, 0.2f, 1f);

                _finaleAlertPanel.SetActive(false);
            }
            else
            {
                _finaleAlertPanel = existingAlert.gameObject;
                _finaleAlertText = _finaleAlertPanel.GetComponentInChildren<TextMeshProUGUI>();
                _finaleAlertPanel.SetActive(false);
            }

            // 2. Victory Summary Panel
            Transform existingVictory = hudCanvas.transform.Find("Panel_VictorySummary");
            if (existingVictory == null)
            {
                _victorySummaryPanel = new GameObject("Panel_VictorySummary", typeof(RectTransform), typeof(Image));
                _victorySummaryPanel.transform.SetParent(hudCanvas.transform, false);

                RectTransform rt = _victorySummaryPanel.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                Image bg = _victorySummaryPanel.GetComponent<Image>();
                bg.color = new Color(0.04f, 0.06f, 0.1f, 0.95f);

                // Dialog container
                GameObject box = new GameObject("DialogBox", typeof(RectTransform), typeof(Image));
                box.transform.SetParent(_victorySummaryPanel.transform, false);
                RectTransform boxRt = box.GetComponent<RectTransform>();
                boxRt.anchorMin = new Vector2(0.5f, 0.5f);
                boxRt.anchorMax = new Vector2(0.5f, 0.5f);
                boxRt.sizeDelta = new Vector2(600f, 420f);
                Image boxBg = box.GetComponent<Image>();
                boxBg.color = new Color(0.08f, 0.12f, 0.18f, 0.98f);

                // Header
                GameObject titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGo.transform.SetParent(box.transform, false);
                RectTransform titleRt = titleGo.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0.5f, 1f);
                titleRt.anchorMax = new Vector2(0.5f, 1f);
                titleRt.pivot = new Vector2(0.5f, 1f);
                titleRt.anchoredPosition = new Vector2(0f, -25f);
                titleRt.sizeDelta = new Vector2(560f, 40f);
                TextMeshProUGUI titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
                titleTxt.alignment = TextAlignmentOptions.Center;
                titleTxt.fontSize = 24;
                titleTxt.fontStyle = FontStyles.Bold;
                titleTxt.text = "СМЕНА ЗАВЕРШЕНА | SHIFT COMPLETE";
                titleTxt.color = new Color(1f, 0.85f, 0.2f, 1f);

                // Subtitle
                GameObject subGo = new GameObject("Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                subGo.transform.SetParent(box.transform, false);
                RectTransform subRt = subGo.GetComponent<RectTransform>();
                subRt.anchorMin = new Vector2(0.5f, 1f);
                subRt.anchorMax = new Vector2(0.5f, 1f);
                subRt.pivot = new Vector2(0.5f, 1f);
                subRt.anchoredPosition = new Vector2(0f, -65f);
                subRt.sizeDelta = new Vector2(560f, 28f);
                TextMeshProUGUI subTxt = subGo.GetComponent<TextMeshProUGUI>();
                subTxt.alignment = TextAlignmentOptions.Center;
                subTxt.fontSize = 13;
                subTxt.text = "СЕКТОР ПОЛНОСТЬЮ ПОГЛОЩЕН СИНГУЛЯРНОСТЬЮ";
                subTxt.color = new Color(0.4f, 0.9f, 1f, 0.9f);

                // Stats text
                GameObject statsGo = new GameObject("StatsText", typeof(RectTransform), typeof(TextMeshProUGUI));
                statsGo.transform.SetParent(box.transform, false);
                RectTransform statsRt = statsGo.GetComponent<RectTransform>();
                statsRt.anchorMin = new Vector2(0.5f, 0.5f);
                statsRt.anchorMax = new Vector2(0.5f, 0.5f);
                statsRt.anchoredPosition = new Vector2(0f, 20f);
                statsRt.sizeDelta = new Vector2(500f, 160f);
                TextMeshProUGUI statsTxt = statsGo.GetComponent<TextMeshProUGUI>();
                statsTxt.alignment = TextAlignmentOptions.Center;
                statsTxt.fontSize = 17;
                statsTxt.lineSpacing = 24f;

                // Button Continue
                BuildButton(box.transform, new Vector2(-135f, -150f), "ПРОДОЛЖИТЬ (ENDLESS)", new Color(0.2f, 0.8f, 1f), OnContinueEndlessClicked);

                // Button Restart
                BuildButton(box.transform, new Vector2(135f, -150f), "НАЧАТЬ ЗАНОВО", new Color(1f, 0.85f, 0.2f), OnRestartShiftClicked);

                _victorySummaryPanel.SetActive(false);
            }
            else
            {
                _victorySummaryPanel = existingVictory.gameObject;
                _victorySummaryPanel.SetActive(false);
            }
        }

        private void BuildButton(Transform parent, Vector2 pos, string label, Color outlineColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(230f, 48f);

            Image bg = btnGo.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.16f, 0.22f, 0.98f);

            Outline outline = btnGo.GetComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);

            Button btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(onClick);

            GameObject txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            TextMeshProUGUI txt = txtGo.GetComponent<TextMeshProUGUI>();
            txt.alignment = TextAlignmentOptions.Center;
            txt.fontSize = 12;
            txt.fontStyle = FontStyles.Bold;
            txt.text = label;
            txt.color = Color.white;
        }

        private void ShowVictorySummaryPanel()
        {
            if (_victorySummaryPanel == null) return;

            _victorySummaryPanel.SetActive(true);
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Populate stats text
            TextMeshProUGUI statsTxt = _victorySummaryPanel.transform.Find("DialogBox/StatsText")?.GetComponent<TextMeshProUGUI>();
            if (statsTxt != null)
            {
                float totalSeconds = Time.timeSinceLevelLoad;
                int mins = Mathf.FloorToInt(totalSeconds / 60f);
                int secs = Mathf.FloorToInt(totalSeconds % 60f);

                float mass = _quotaManager != null ? _quotaManager.TotalMassConsumed : 0f;
                int peakCombo = _quotaManager != null ? _quotaManager.PeakHeatMultiplier : 1;
                int coins = _quotaManager != null ? _quotaManager.ScrapCoins : 0;

                statsTxt.text = $"<b>Время смены:</b> {mins:D2}:{secs:D2}\n" +
                               $"<b>Поглощено массы:</b> {mass:F1} т\n" +
                               $"<b>Пиковый множитель комбо:</b> x{peakCombo}\n" +
                               $"<b>Награда Scrap Coins:</b> {coins} 🪙";
            }
        }

        private void OnContinueEndlessClicked()
        {
            isEndlessMode = true;
            if (_victorySummaryPanel != null)
            {
                _victorySummaryPanel.SetActive(false);
            }
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnRestartShiftClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
