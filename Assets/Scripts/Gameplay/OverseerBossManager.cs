using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class OverseerBossManager : MonoBehaviour
    {
        public static OverseerBossManager Instance { get; private set; }

        [Header("Runtime State")]
        [SerializeField] private List<OverseerBossController> activeBosses = new List<OverseerBossController>();
        [SerializeField] private int defeatedBossesCount = 0;
        [SerializeField] private bool isEndlessMode = false;
        [SerializeField] private bool isVictoryPanelOpen = false;

        [Header("UI Victory Panel")]
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TextMeshProUGUI victoryTitleText;
        [SerializeField] private TextMeshProUGUI victoryStatsText;

        public IReadOnlyList<OverseerBossController> ActiveBosses => activeBosses;
        public int DefeatedBossesCount => defeatedBossesCount;
        public int TotalBossesCount => activeBosses != null ? activeBosses.Count : 4;
        public bool IsAllBossesDefeated => defeatedBossesCount >= 4;
        public bool IsEndlessMode => isEndlessMode;

        public event Action<OverseerBossController> OnBossKilled;
        public event Action OnAllBossesKilled;

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
            DiscoverOrInitializeBosses();
            EnsureVictoryPanel();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void RegisterBoss(OverseerBossController boss)
        {
            if (boss != null && !activeBosses.Contains(boss))
            {
                activeBosses.Add(boss);
            }
        }

        private void DiscoverOrInitializeBosses()
        {
            activeBosses.Clear();
            var found = FindObjectsByType<OverseerBossController>();
            if (found != null && found.Length > 0)
            {
                // Sort by bossIndex
                Array.Sort(found, (a, b) => a.BossIndex.CompareTo(b.BossIndex));
                foreach (var b in found)
                {
                    if (b != null) activeBosses.Add(b);
                }
            }

            Debug.Log($"<color=#00FFCC>[OverseerBossManager] Initialized with {activeBosses.Count} active Overseer Bosses.</color>");
        }

        public void OnBossDefeated(OverseerBossController boss)
        {
            if (boss == null) return;

            defeatedBossesCount++;
            OnBossKilled?.Invoke(boss);

            Debug.Log($"<color=#FFCC00>[OVERSEER PROGRESS] Bosses Defeated: {defeatedBossesCount} / {Mathf.Max(4, activeBosses.Count)}</color>");

            if (defeatedBossesCount >= Mathf.Max(4, activeBosses.Count) && !isEndlessMode)
            {
                TriggerBossVictory();
            }
        }

        public void TriggerBossVictory()
        {
            if (isEndlessMode || isVictoryPanelOpen) return;
            isVictoryPanelOpen = true;
            OnAllBossesKilled?.Invoke();

            StartCoroutine(ShowVictoryRoutine());
        }

        private IEnumerator ShowVictoryRoutine()
        {
            yield return new WaitForSeconds(1.2f);

            // Play Victory Chime
            if (GameAudioManager.Instance != null && GameAudioManager.Instance.Config != null)
            {
                GameAudioManager.Instance.PlayOneShot(GameAudioManager.Instance.Config.victoryChimeSfx, 1.0f);
            }

            EnsureVictoryPanel();
            if (victoryPanel != null)
            {
                victoryPanel.SetActive(true);
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (victoryStatsText != null)
                {
                    float totalSeconds = Time.timeSinceLevelLoad;
                    int mins = Mathf.FloorToInt(totalSeconds / 60f);
                    int secs = Mathf.FloorToInt(totalSeconds % 60f);

                    var quotaMgr = FindAnyObjectByType<QuotaAndScoreManager>();
                    int coins = quotaMgr != null ? quotaMgr.ScrapCoins : 0;
                    var massMgr = BlackHoleMassAndEnergyManager.Instance;
                    float mass = massMgr != null ? massMgr.CurrentBlackHoleMass : 0f;

                    victoryStatsText.text = 
                        $"<color=#00FFCC>MISSION DURATION:</color> {mins:D2}:{secs:D2}\n" +
                        $"<color=#FFD700>OVERSEERS NEUTRALIZED:</color> {defeatedBossesCount} / 4\n" +
                        $"<color=#00FF88>SCRAP COINS ACCUMULATED:</color> {coins:N0}\n" +
                        $"<color=#FF5500>FINAL SINGULARITY MASS:</color> {mass:F0} / 1000\n\n" +
                        $"<color=#AAAAAA>The Panopticon's gaze has been extinguished.\nYou are free to salvage this sector uninterrupted.</color>";
                }
            }
        }

        private void EnsureVictoryPanel()
        {
            if (victoryPanel != null) return;

            Canvas hudCanvas = null;
            var canvasGo = GameObject.Find("HUD_Canvas");
            if (canvasGo != null) hudCanvas = canvasGo.GetComponent<Canvas>();
            if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();
            if (hudCanvas == null) return;

            Transform existing = hudCanvas.transform.Find("Panel_BossVictory");
            if (existing != null)
            {
                victoryPanel = existing.gameObject;
                victoryTitleText = victoryPanel.transform.Find("DialogBox/TitleText")?.GetComponent<TextMeshProUGUI>();
                victoryStatsText = victoryPanel.transform.Find("DialogBox/StatsText")?.GetComponent<TextMeshProUGUI>();
                return;
            }

            // Create Procedural Panel
            GameObject panelGo = new GameObject("Panel_BossVictory");
            panelGo.transform.SetParent(hudCanvas.transform, false);
            RectTransform panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.sizeDelta = Vector2.zero;

            Image panelBg = panelGo.AddComponent<Image>();
            panelBg.color = new Color(0.02f, 0.04f, 0.08f, 0.94f);

            // Dialog Box
            GameObject dialogGo = new GameObject("DialogBox");
            dialogGo.transform.SetParent(panelGo.transform, false);
            RectTransform dialogRect = dialogGo.AddComponent<RectTransform>();
            dialogRect.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRect.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRect.sizeDelta = new Vector2(720, 480);
            Image dialogBg = dialogGo.AddComponent<Image>();
            dialogBg.color = new Color(0.06f, 0.1f, 0.18f, 0.96f);

            Outline dialogOutline = dialogGo.AddComponent<Outline>();
            dialogOutline.effectColor = new Color(0.1f, 0.95f, 1f, 0.8f);
            dialogOutline.effectDistance = new Vector2(3, -3);

            // Title Text
            GameObject titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(dialogGo.transform, false);
            RectTransform titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.05f, 0.78f);
            titleRect.anchorMax = new Vector2(0.95f, 0.96f);
            titleRect.sizeDelta = Vector2.zero;
            victoryTitleText = titleGo.AddComponent<TextMeshProUGUI>();
            victoryTitleText.text = "PANOPTICON BLINDED — ALL OVERSEERS DESTROYED!";
            victoryTitleText.fontSize = 24;
            victoryTitleText.fontStyle = FontStyles.Bold;
            victoryTitleText.alignment = TextAlignmentOptions.Center;
            victoryTitleText.color = new Color(1f, 0.88f, 0.2f);

            // Stats Text
            GameObject statsGo = new GameObject("StatsText");
            statsGo.transform.SetParent(dialogGo.transform, false);
            RectTransform statsRect = statsGo.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.08f, 0.26f);
            statsRect.anchorMax = new Vector2(0.92f, 0.76f);
            statsRect.sizeDelta = Vector2.zero;
            victoryStatsText = statsGo.AddComponent<TextMeshProUGUI>();
            victoryStatsText.fontSize = 17;
            victoryStatsText.lineSpacing = 16;
            victoryStatsText.alignment = TextAlignmentOptions.Center;
            victoryStatsText.color = Color.white;

            // Button 1: [CONTINUE IN FREE MODE]
            BuildButton(dialogGo.transform, new Vector2(-160, -180), new Vector2(280, 52), "[CONTINUE IN FREE MODE]", new Color(0.1f, 0.9f, 0.4f), OnContinueFreeModeClicked);

            // Button 2: [START NEW GAME]
            BuildButton(dialogGo.transform, new Vector2(160, -180), new Vector2(280, 52), "[START NEW GAME]", new Color(0.2f, 0.8f, 1.0f), OnRestartGameClicked);

            victoryPanel = panelGo;
            victoryPanel.SetActive(false);
        }

        private void BuildButton(Transform parent, Vector2 pos, Vector2 size, string label, Color outlineColor, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnGo = new GameObject(label);
            btnGo.transform.SetParent(parent, false);
            RectTransform btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.5f);
            btnRect.anchorMax = new Vector2(0.5f, 0.5f);
            btnRect.anchoredPosition = pos;
            btnRect.sizeDelta = size;

            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.12f, 0.16f, 0.25f, 0.95f);

            Outline btnOutline = btnGo.AddComponent<Outline>();
            btnOutline.effectColor = outlineColor;
            btnOutline.effectDistance = new Vector2(2, -2);

            Button btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.onClick.AddListener(onClick);

            GameObject textGo = new GameObject("Text");
            textGo.transform.SetParent(btnGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            TextMeshProUGUI txt = textGo.AddComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 16;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;
        }

        private void OnContinueFreeModeClicked()
        {
            isEndlessMode = true;
            isVictoryPanelOpen = false;

            if (victoryPanel != null)
            {
                victoryPanel.SetActive(false);
            }

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnRestartGameClicked()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
