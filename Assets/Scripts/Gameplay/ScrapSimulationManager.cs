using System.Collections.Generic;
using UnityEngine;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class ScrapSimulationManager : MonoBehaviour
    {
        public static ScrapSimulationManager Instance { get; private set; }

        [System.Serializable]
        public struct ScrapPoolEntry
        {
            public ScrapItem prefab;
            public ScrapData scrapData;
            [Range(1, 100)] public int weight;
        }

        [Header("Pool Setup")]
        [SerializeField] private int prewarmPoolSize = 100;
        [SerializeField] private Transform poolRoot;
        [SerializeField] private List<ScrapPoolEntry> poolEntries = new List<ScrapPoolEntry>();

        [Header("Spawn Reference")]
        [SerializeField] private Transform centerTransform;

        [Header("Runtime Info (Read Only)")]
        [SerializeField] private int activeCount = 0;
        [SerializeField] private int fadingCount = 0;
        [SerializeField] private int pooledCount = 0;

        private readonly Queue<ScrapItem> _scrapPool = new Queue<ScrapItem>();
        private readonly List<ScrapItem> _activeScrap = new List<ScrapItem>();
        private readonly List<ScrapItem> _fadingScrap = new List<ScrapItem>();

        private float _spawnTimer = 0f;
        private int _totalWeight = 0;

        public IReadOnlyList<ScrapItem> ActiveScrap => _activeScrap;
        public int ActiveScrapCount => _activeScrap.Count;
        public int FadingScrapCount => _fadingScrap.Count;
        public int PooledScrapCount => _scrapPool.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (poolRoot == null)
            {
                GameObject poolObj = new GameObject("--- SCRAP_POOL_CONTAINER ---");
                poolObj.transform.SetParent(transform);
                poolRoot = poolObj.transform;
            }

            CalculateTotalWeight();
            PrewarmPool();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void CalculateTotalWeight()
        {
            _totalWeight = 0;
            if (poolEntries == null) return;

            foreach (var entry in poolEntries)
            {
                if (entry.prefab != null)
                {
                    _totalWeight += entry.weight;
                }
            }
        }

        public void SetPoolEntries(List<ScrapPoolEntry> entries)
        {
            poolEntries = entries;
            CalculateTotalWeight();
        }

        private void PrewarmPool()
        {
            if (poolEntries == null || poolEntries.Count == 0 || _totalWeight <= 0)
            {
                Debug.LogWarning("[ScrapSimulationManager] Cannot prewarm pool: pool entries missing or zero weight.");
                return;
            }

            for (int i = 0; i < prewarmPoolSize; i++)
            {
                ScrapPoolEntry entry = SelectRandomEntry();
                if (entry.prefab == null) continue;

                ScrapItem scrap = Instantiate(entry.prefab, poolRoot);
                scrap.name = $"{entry.prefab.name}_Pooled_{i:D3}";
                if (entry.scrapData != null)
                {
                    scrap.Initialize(entry.scrapData);
                }

                scrap.ResetForPool();
                scrap.gameObject.SetActive(false);
                _scrapPool.Enqueue(scrap);
            }

            UpdateStats();
            Debug.Log($"[ScrapSimulationManager] Pre-warmed pool with {_scrapPool.Count} scrap items.");
        }

        private void Start()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config != null)
            {
                Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;
                int initialSpawn = Mathf.Min(15, config.maxActiveScrap);
                for (int i = 0; i < initialSpawn; i++)
                {
                    SpawnFromPool(config, center);
                }
            }
        }

        private ScrapPoolEntry SelectRandomEntry()
        {
            int roll = Random.Range(0, _totalWeight);
            int accumulated = 0;
            ScrapPoolEntry selected = poolEntries[0];

            foreach (var entry in poolEntries)
            {
                if (entry.prefab == null) continue;
                accumulated += entry.weight;
                if (roll < accumulated)
                {
                    selected = entry;
                    break;
                }
            }

            return selected;
        }

        private void FixedUpdate()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null) return;

            Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;

            // 1. Spawning cycle
            _spawnTimer += Time.fixedDeltaTime;
            if (_spawnTimer >= config.scrapSpawnInterval)
            {
                _spawnTimer = 0f;
                if (_activeScrap.Count < config.maxActiveScrap && _scrapPool.Count > 0)
                {
                    SpawnFromPool(config, center);
                }
            }

            // 2. Centralized batched simulation for active scrap
            float dt = Time.fixedDeltaTime;
            for (int i = _activeScrap.Count - 1; i >= 0; i--)
            {
                ScrapItem scrap = _activeScrap[i];
                if (scrap == null)
                {
                    _activeScrap.RemoveAt(i);
                    continue;
                }

                if (scrap.CurrentState == ScrapState.Consumed)
                {
                    // Consumed by black hole, handled via ReturnToPool()
                    continue;
                }

                if (scrap.CurrentState == ScrapState.Held)
                {
                    // Held by manipulator, skips boundary fade-out
                    continue;
                }

                // Boundary escape check
                float dist = Vector3.Distance(scrap.transform.position, center);
                if (dist > config.arenaMaxRadius)
                {
                    _activeScrap.RemoveAt(i);
                    _fadingScrap.Add(scrap);
                    scrap.StartFadeOut(config.scrapFadeOutDuration);
                    continue;
                }

                // Batch update accretion zone bonuses
                scrap.CentralizedUpdate(dt, 0.5f);
            }

            // 3. Centralized batched update for fading scrap
            for (int i = _fadingScrap.Count - 1; i >= 0; i--)
            {
                ScrapItem scrap = _fadingScrap[i];
                if (scrap == null)
                {
                    _fadingScrap.RemoveAt(i);
                    continue;
                }

                bool isDone = scrap.UpdateFadeOut(dt);
                if (isDone)
                {
                    _fadingScrap.RemoveAt(i);
                    ReturnToPoolInternal(scrap);
                }
            }

            UpdateStats();
        }

        public ScrapItem SpawnFromPool(GameBalanceConfig config, Vector3 center)
        {
            if (_scrapPool.Count == 0) return null;

            ScrapItem scrap = _scrapPool.Dequeue();
            if (scrap == null) return null;

            // Compute random position in spherical shell (mildly flattened on Y for cohesive orbital plane)
            Vector3 radialDir = Random.onUnitSphere;
            radialDir.y = Random.Range(-0.25f, 0.25f);
            radialDir.Normalize();

            float radius = Random.Range(config.minSpawnRadius, config.maxSpawnRadius);
            Vector3 spawnPos = center + (radialDir * radius);

            scrap.transform.position = spawnPos;
            scrap.transform.rotation = Random.rotation;

            scrap.ResetForPool();
            scrap.HomeOrbitRadius = radius;
            scrap.gameObject.SetActive(true);

            // Compute clockwise tangential orbital velocity for Zone A Calm Drift
            Vector3 toCenter = center - spawnPos;
            Vector3 dirToCenter = toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : Vector3.forward;
            Vector3 tangent = Vector3.Cross(dirToCenter, Vector3.up).normalized;
            if (!config.clockwiseOrbit) tangent = -tangent;

            float speed = config.outerZoneDriftSpeed * config.globalSimulationSpeed;
            Vector3 initialVelocity = tangent * speed;

            Rigidbody rb = scrap.Rigidbody;
            if (rb != null)
            {
                rb.linearVelocity = initialVelocity;
                rb.angularVelocity = Random.insideUnitSphere * 0.4f;
            }

            var bh = Object.FindAnyObjectByType<OrbitalPanopticon.Physics.BlackHoleGravity>();
            if (bh != null)
            {
                bh.RegisterBody(scrap.GravityBody);
            }

            _activeScrap.Add(scrap);
            return scrap;
        }

        public void ReturnToPool(ScrapItem scrap)
        {
            if (scrap == null) return;

            _activeScrap.Remove(scrap);
            _fadingScrap.Remove(scrap);
            ReturnToPoolInternal(scrap);
        }

        private void ReturnToPoolInternal(ScrapItem scrap)
        {
            var bh = Object.FindAnyObjectByType<OrbitalPanopticon.Physics.BlackHoleGravity>();
            if (bh != null)
            {
                bh.UnregisterBody(scrap.GravityBody);
            }

            scrap.ResetForPool();
            scrap.gameObject.SetActive(false);

            if (poolRoot != null)
            {
                scrap.transform.SetParent(poolRoot);
            }

            _scrapPool.Enqueue(scrap);
            UpdateStats();
        }

        private void UpdateStats()
        {
            activeCount = _activeScrap.Count;
            fadingCount = _fadingScrap.Count;
            pooledCount = _scrapPool.Count;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float minRadius = config != null ? config.minSpawnRadius : 16f;
            float maxRadius = config != null ? config.maxSpawnRadius : 38f;

            Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, minRadius);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawWireSphere(center, maxRadius);
        }
    }
}
