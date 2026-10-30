using System.Collections.Generic;
using UnityEngine;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class HelperDroneManager : MonoBehaviour
    {
        public static HelperDroneManager Instance { get; private set; }

        [Header("Prefab Reference")]
        [SerializeField] private GameObject helperDronePrefab;

        private readonly List<HelperDroneBot> _activeBots = new List<HelperDroneBot>();
        private readonly HashSet<ScrapItem> _claimedScrap = new HashSet<ScrapItem>();

        public IReadOnlyList<HelperDroneBot> ActiveBots => _activeBots;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (helperDronePrefab == null)
            {
                LoadPrefabFallback();
            }
        }

        private void Start()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null && config.activeHelperRobotsCount > 0)
            {
                for (int i = 0; i < config.activeHelperRobotsCount; i++)
                {
                    SpawnHelperRobot();
                }
            }
        }

        private void LoadPrefabFallback()
        {
#if UNITY_EDITOR
            helperDronePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/HelperDroneBot.prefab");
#endif
        }

        public HelperDroneBot SpawnHelperRobot()
        {
            if (helperDronePrefab == null)
            {
                LoadPrefabFallback();
            }

            if (helperDronePrefab == null)
            {
                Debug.LogError("[HelperDroneManager] Cannot spawn robot: helperDronePrefab is missing!");
                return null;
            }

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float spawnRadius = config != null ? config.maxSpawnRadius : 38f;

            float phi = Random.Range(0f, Mathf.PI * 2f);
            float y = Random.Range(-4f, 4f);
            Vector3 spawnPos = new Vector3(Mathf.Cos(phi) * spawnRadius, y, Mathf.Sin(phi) * spawnRadius);
            Quaternion spawnRot = Quaternion.LookRotation((Vector3.zero - spawnPos).normalized);

            GameObject botObj = Instantiate(helperDronePrefab, spawnPos, spawnRot);
            botObj.name = $"HelperDroneBot_{_activeBots.Count + 1}";

            HelperDroneBot bot = botObj.GetComponent<HelperDroneBot>();
            if (bot != null)
            {
                _activeBots.Add(bot);
            }

            return bot;
        }

        public bool IsScrapClaimed(ScrapItem scrap)
        {
            if (scrap == null) return false;
            return _claimedScrap.Contains(scrap);
        }

        public void ClaimScrap(ScrapItem scrap)
        {
            if (scrap != null)
            {
                _claimedScrap.Add(scrap);
            }
        }

        public void ReleaseScrap(ScrapItem scrap)
        {
            if (scrap != null)
            {
                _claimedScrap.Remove(scrap);
            }
        }

        private void Update()
        {
            // Clean destroyed or consumed scrap from claimed set
            _claimedScrap.RemoveWhere(s => s == null || !s.gameObject.activeInHierarchy || s.CurrentState == ScrapState.Consumed);
        }
    }
}
