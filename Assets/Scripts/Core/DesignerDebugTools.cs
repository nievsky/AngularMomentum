using UnityEngine;
using UnityEngine.InputSystem;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Provides live developer shortcuts for game designers to test economy, quotas, helper robots, and the endgame collapse.
    /// Controlled by GameBalanceConfig.enableDebugShortcuts.
    /// </summary>
    [DisallowMultipleComponent]
    public class DesignerDebugTools : MonoBehaviour
    {
        public static DesignerDebugTools Instance { get; private set; }

        private QuotaAndScoreManager _quotaManager;
        private GUIStyle _legendStyle;

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
        }

        private void Update()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null || !config.enableDebugShortcuts) return;

            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            // F1: +500 Scrap Coins
            if (kb.f1Key.wasPressedThisFrame)
            {
                if (_quotaManager == null) _quotaManager = FindAnyObjectByType<QuotaAndScoreManager>();
                if (_quotaManager != null)
                {
                    _quotaManager.AddScrapCoins(500);
                    Debug.Log("<color=#00FFAA>[DEBUG SHORTCUT] F1 pressed: Added +500 Scrap Coins! New Total: " + _quotaManager.ScrapCoins + "</color>");
                }
            }

            // F2: +1,000 Mass
            if (kb.f2Key.wasPressedThisFrame)
            {
                var stageMgr = BlackHoleStageManager.Instance;
                if (stageMgr != null)
                {
                    stageMgr.AddMass(1000f);
                    Debug.Log("<color=#00FFAA>[DEBUG SHORTCUT] F2 pressed: Added +1,000 Singularity Mass! Current Mass: " + stageMgr.CurrentMass + "</color>");
                }
            }

            // F3: Advance Black Hole Stage
            if (kb.f3Key.wasPressedThisFrame)
            {
                var stageMgr = BlackHoleStageManager.Instance;
                if (stageMgr != null)
                {
                    float target = stageMgr.CurrentStageTargetMass;
                    float needed = Mathf.Max(100f, target - stageMgr.CurrentMass);
                    stageMgr.AddMass(needed);
                    Debug.Log($"<color=#FFD700>[DEBUG SHORTCUT] F3 pressed: Advanced Black Hole Stage! Stage: {stageMgr.CurrentStage}, Mass: {stageMgr.CurrentMass:F0}</color>");
                }
            }

            // F4: Break Next Containment Lock
            if (kb.f4Key.wasPressedThisFrame)
            {
                var stageMgr = BlackHoleStageManager.Instance;
                if (stageMgr != null)
                {
                    stageMgr.BreakNextContainmentLock();
                    Debug.Log($"<color=#FF2244>[DEBUG SHORTCUT] F4 pressed: Broke Containment Lock! Active Locks Remaining: {stageMgr.ActiveContainmentLocksCount}/4</color>");
                }
            }
        }

        private void OnGUI()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null || !config.enableDebugShortcuts) return;

            if (_legendStyle == null)
            {
                _legendStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleCenter
                };
                _legendStyle.normal.textColor = new Color(0.2f, 1f, 0.85f, 0.95f);
            }

            string text = "[DEV SHORTCUTS]  F1: +500 Coins  |  F2: +1000 Mass  |  F3: Advance Stage  |  F4: Break Next Lock";
            float width = 680f;
            float height = 22f;
            float x = (Screen.width - width) * 0.5f;
            float y = 4f;

            GUI.Box(new Rect(x, y, width, height), text, _legendStyle);
        }
    }
}
