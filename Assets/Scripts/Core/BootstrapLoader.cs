using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrbitalPanopticon.Core
{
    [DefaultExecutionOrder(-1000)]
    public class BootstrapLoader : MonoBehaviour
    {
        [Header("Master Balance Configuration")]
        [SerializeField] private GameBalanceConfig balanceConfig;

        private void Awake()
        {
            if (balanceConfig == null)
            {
#if UNITY_EDITOR
                balanceConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<GameBalanceConfig>("Assets/_Project/ScriptableObjects/GameBalanceConfig.asset");
#endif
            }
            if (balanceConfig != null)
            {
                GameBalanceConfig.Instance = balanceConfig;
            }
        }

        private void Start()
        {
            SceneManager.LoadScene("MainMenuScene");
        }
    }
}
