using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Balance Configuration")]
        [Tooltip("Centralized ScriptableObject holding all tunable gameplay and physics parameters.")]
        [SerializeField] private GameBalanceConfig balanceConfig;

        /// <summary>
        /// Global static accessor for runtime balance settings.
        /// Allows real-time live-tuning in the Inspector and Play Mode.
        /// </summary>
        public static GameBalanceConfig GameBalanceConfigInstance
        {
            get
            {
                if (Instance != null && Instance.balanceConfig != null)
                {
                    return Instance.balanceConfig;
                }
                return GameBalanceConfig.Instance;
            }
        }

        public GameBalanceConfig BalanceConfig => balanceConfig;

        private bool _isClonedForUpgrades = false;

        public void PrepareForUpgradeMutation()
        {
            if (_isClonedForUpgrades || balanceConfig == null) return;

            if (balanceConfig.allowRuntimeUpgradeCloning)
            {
                balanceConfig = Instantiate(balanceConfig);
                GameBalanceConfig.Instance = balanceConfig;
                _isClonedForUpgrades = true;
                Debug.Log("<color=#00FFAA>[GameManager] Cloned GameBalanceConfig for runtime upgrade mutations.</color>");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.runInBackground = true;

            EnsureMasterConfig();
            if (balanceConfig != null)
            {
                GameBalanceConfig.Instance = balanceConfig;
            }
        }

        private void EnsureMasterConfig()
        {
            if (balanceConfig == null)
            {
#if UNITY_EDITOR
                balanceConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBalanceConfig>("Assets/_Project/ScriptableObjects/GameBalanceConfig.asset");
#endif
            }
        }

        private void OnEnable()
        {
            if (balanceConfig != null)
            {
                GameBalanceConfig.Instance = balanceConfig;
            }
        }

        private void OnValidate()
        {
            if (balanceConfig != null && !Application.isPlaying)
            {
                GameBalanceConfig.Instance = balanceConfig;
            }
        }
    }
}
