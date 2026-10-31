using System.Collections.Generic;
using UnityEngine;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class ScrapSpawner : MonoBehaviour
    {
        [System.Serializable]
        public struct ScrapSpawnEntry
        {
            public ScrapItem prefab;
            public ScrapData scrapData;
            [Range(1, 100)] public int weight;
        }

        [Header("Spawn Boundaries")]
        [Tooltip("Center of the orbital arena (black hole center). Defaults to Vector3.zero.")]
        [SerializeField] private Transform centerTransform;
        [SerializeField] private float minSpawnRadius = 18f;
        [SerializeField] private float maxSpawnRadius = 38f;

        [Header("Spawn Dynamics")]
        [SerializeField] private int maxActiveScrap = 40;
        [SerializeField] private int prewarmCount = 0;
        [SerializeField] private float spawnInterval = 1.2f;
        [SerializeField] private float minInitialSpeed = 2.5f;
        [SerializeField] private float maxInitialSpeed = 6.0f;
        [SerializeField] private float maxAngularSpeed = 1.2f;

        [Header("Available Prefabs & Weights")]
        [SerializeField] private List<ScrapSpawnEntry> spawnEntries = new List<ScrapSpawnEntry>();

        [Header("Runtime Status")]
        [SerializeField] private int currentActiveCount = 0;

        private readonly List<ScrapItem> _activeScrap = new List<ScrapItem>();
        private float _spawnTimer = 0f;
        private int _totalWeight = 0;

        public int ActiveScrapCount => _activeScrap.Count;

        private void Awake()
        {
            CalculateTotalWeight();
        }

        private void Start()
        {
            if (prewarmCount > 0)
            {
                PrewarmSpawns(prewarmCount);
            }
        }

        public void PrewarmSpawns(int count)
        {
            CalculateTotalWeight();
            int toSpawn = Mathf.Min(count, maxActiveScrap);
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnRandomScrap();
            }
        }

        private void OnValidate()
        {
            CalculateTotalWeight();
        }

        private void Update()
        {
            CleanupInactiveScrap();
            currentActiveCount = _activeScrap.Count;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= spawnInterval)
            {
                _spawnTimer = 0f;
                if (_activeScrap.Count < maxActiveScrap)
                {
                    SpawnRandomScrap();
                }
            }
        }

        public void CalculateTotalWeight()
        {
            _totalWeight = 0;
            if (spawnEntries == null) return;

            foreach (var entry in spawnEntries)
            {
                if (entry.prefab != null)
                {
                    _totalWeight += entry.weight;
                }
            }
        }

        public void SetSpawnEntries(List<ScrapSpawnEntry> entries)
        {
            spawnEntries = entries;
            CalculateTotalWeight();
        }

        public ScrapItem SpawnRandomScrap()
        {
            if (spawnEntries == null || spawnEntries.Count == 0)
            {
                return null;
            }

            OrbitalPanopticon.Core.GameBalanceConfig config = OrbitalPanopticon.Core.GameManager.GameBalanceConfigInstance;
            int maxTier = config != null ? config.unlockedScrapTier : 1;

            int validWeight = 0;
            foreach (var entry in spawnEntries)
            {
                if (entry.prefab == null) continue;
                int tier = entry.scrapData != null ? entry.scrapData.Tier : 1;
                if (tier <= maxTier)
                {
                    validWeight += entry.weight;
                }
            }

            if (validWeight <= 0) return null;

            // Weighted selection among unlocked tiers
            int roll = Random.Range(0, validWeight);
            int accumulated = 0;
            ScrapSpawnEntry selected = spawnEntries[0];

            foreach (var entry in spawnEntries)
            {
                if (entry.prefab == null) continue;
                int tier = entry.scrapData != null ? entry.scrapData.Tier : 1;
                if (tier > maxTier) continue;

                accumulated += entry.weight;
                if (roll < accumulated)
                {
                    selected = entry;
                    break;
                }
            }

            // Calculate position in spherical shell
            Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;
            Vector3 radialDir = Random.onUnitSphere;
            float spawnRadius = Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector3 spawnPosition = center + (radialDir * spawnRadius);

            // Compute tangential orbital velocity
            Vector3 randomAxis = Random.insideUnitSphere;
            if (randomAxis.sqrMagnitude < 0.001f) randomAxis = Vector3.up;
            Vector3 tangent = Vector3.Cross(radialDir, randomAxis).normalized;
            float initialSpeed = Random.Range(minInitialSpeed, maxInitialSpeed);
            Vector3 initialVelocity = tangent * initialSpeed;

            // Instantiate scrap
            Quaternion randomRot = Random.rotation;
            ScrapItem scrap = Instantiate(selected.prefab, spawnPosition, randomRot, transform);

            if (selected.scrapData != null)
            {
                scrap.Initialize(selected.scrapData);
            }

            float chance = config != null ? config.valuableScrapSpawnChance : 0.25f;
            bool isValuable = Random.value < chance;
            scrap.SetupValuableScrap(isValuable);

            // Apply tangential orbital impulse and spin
            Rigidbody rb = scrap.Rigidbody;
            if (rb != null)
            {
                rb.linearVelocity = initialVelocity;
                rb.angularVelocity = Random.insideUnitSphere * maxAngularSpeed;
            }

            _activeScrap.Add(scrap);
            return scrap;
        }

        public void UnregisterScrap(ScrapItem scrap)
        {
            if (scrap != null)
            {
                _activeScrap.Remove(scrap);
            }
        }

        private void CleanupInactiveScrap()
        {
            for (int i = _activeScrap.Count - 1; i >= 0; i--)
            {
                if (_activeScrap[i] == null || _activeScrap[i].CurrentState == ScrapState.Consumed)
                {
                    _activeScrap.RemoveAt(i);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;
            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, minSpawnRadius);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, maxSpawnRadius);
        }
    }
}
