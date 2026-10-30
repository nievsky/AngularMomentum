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

            // F2: Instantly Complete Current Quota
            if (kb.f2Key.wasPressedThisFrame)
            {
                if (_quotaManager == null) _quotaManager = FindAnyObjectByType<QuotaAndScoreManager>();
                if (_quotaManager != null)
                {
                    Debug.Log("<color=#FFD700>[DEBUG SHORTCUT] F2 pressed: Instantly completing current quota!</color>");
                    _quotaManager.ForceCompleteQuota();
                }
            }

            // F3: Spawn 1 Helper Robot immediately
            if (kb.f3Key.wasPressedThisFrame)
            {
                if (HelperDroneManager.Instance != null)
                {
                    HelperDroneBot bot = HelperDroneManager.Instance.SpawnHelperRobot();
                    Debug.Log("<color=#00D8FF>[DEBUG SHORTCUT] F3 pressed: Spawned Helper Robot! Total active: " + HelperDroneManager.Instance.ActiveBots.Count + "</color>");
                }
                else
                {
                    Debug.LogWarning("[DEBUG SHORTCUT] HelperDroneManager instance not found!");
                }
            }

            // F4: Trigger the Endgame Collapse Finale immediately
            if (kb.f4Key.wasPressedThisFrame)
            {
                if (EndgameFinaleController.Instance != null)
                {
                    Debug.Log("<color=#FF2244>[DEBUG SHORTCUT] F4 pressed: Triggering Singularity Sector Collapse Finale!</color>");
                    EndgameFinaleController.Instance.TriggerEndgameFinale();
                }
                else
                {
                    Debug.LogWarning("[DEBUG SHORTCUT] EndgameFinaleController instance not found!");
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

            string text = "[DEV SHORTCUTS]  F1: +500 Coins  |  F2: Skip Quota  |  F3: Spawn Bot  |  F4: Singularity Finale";
            float width = 640f;
            float height = 22f;
            float x = (Screen.width - width) * 0.5f;
            float y = 4f;

            GUI.Box(new Rect(x, y, width, height), text, _legendStyle);
        }
    }
}
