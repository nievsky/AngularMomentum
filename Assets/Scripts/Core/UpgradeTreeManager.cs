using System;
using System.Collections.Generic;
using UnityEngine;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Core
{
    public enum UpgradeBranch
    {
        Hub,
        Player,
        BlackHole,
        Scrap,
        Energy,
        Cameras = Energy,
        Robots,
        Special
    }

    [System.Serializable]
    public class UpgradeNodeData
    {
        public string nodeId;
        public string titleRU;
        public UpgradeBranch branch;
        public int currentLevel;
        public int maxLevel = 1;
        public int[] levelCosts;
        public string[] prerequisiteNodeIds;
        public bool isWowPerk;
        public Vector2 uiPosition;
        [TextArea(1, 2)] public string descriptionRU;

        public bool IsUnlocked => currentLevel > 0;
        public bool IsMaxLevel => currentLevel >= maxLevel;

        public int CurrentCost
        {
            get
            {
                if (IsMaxLevel || levelCosts == null || levelCosts.Length == 0) return 0;
                int idx = Mathf.Clamp(currentLevel, 0, levelCosts.Length - 1);
                return levelCosts[idx];
            }
        }
    }

    [DisallowMultipleComponent]
    public class UpgradeTreeManager : MonoBehaviour
    {
        public static UpgradeTreeManager Instance { get; private set; }
        public static bool IsTreeOpen { get; set; } = false;

        public event Action OnUpgradeTreeChanged;

        [Header("Upgrade Nodes (Designer Data)")]
        [SerializeField] private List<UpgradeNodeData> nodes = new List<UpgradeNodeData>();

        public IReadOnlyList<UpgradeNodeData> Nodes => nodes;

        private readonly Dictionary<string, UpgradeNodeData> _nodeMap = new Dictionary<string, UpgradeNodeData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (nodes == null || nodes.Count == 0 || !HasEnergyBranch())
            {
                PopulateDefaultNodes();
            }

            BuildNodeMap();
        }

        private bool HasEnergyBranch()
        {
            if (nodes == null) return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].nodeId == "valuable_scrap_chance") return true;
            }
            return false;
        }

        private void BuildNodeMap()
        {
            _nodeMap.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && !string.IsNullOrEmpty(nodes[i].nodeId))
                {
                    _nodeMap[nodes[i].nodeId] = nodes[i];
                }
            }
        }

        public UpgradeNodeData GetNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;
            if (_nodeMap.Count == 0 && nodes != null && nodes.Count > 0)
            {
                BuildNodeMap();
            }
            if (_nodeMap.TryGetValue(nodeId, out var node)) return node;
            return null;
        }

        public bool ArePrerequisitesMet(UpgradeNodeData node)
        {
            if (node == null) return false;
            if (node.nodeId == "hub") return true;
            if (node.prerequisiteNodeIds == null || node.prerequisiteNodeIds.Length == 0) return true;

            for (int i = 0; i < node.prerequisiteNodeIds.Length; i++)
            {
                string prereqId = node.prerequisiteNodeIds[i];
                UpgradeNodeData prereq = GetNode(prereqId);
                if (prereq == null) continue;

                // Prerequisite must be unlocked / maxed to unlock next step in chain
                if (prereq.currentLevel < prereq.maxLevel)
                {
                    return false;
                }
            }
            return true;
        }

        public bool CanPurchaseNode(UpgradeNodeData node)
        {
            if (node == null || node.IsMaxLevel) return false;
            if (!ArePrerequisitesMet(node)) return false;

            var quotaManager = UnityEngine.Object.FindAnyObjectByType<QuotaAndScoreManager>();
            int coins = quotaManager != null ? quotaManager.ScrapCoins : 0;
            return coins >= node.CurrentCost;
        }

        public bool PurchaseNode(string nodeId)
        {
            UpgradeNodeData node = GetNode(nodeId);
            if (node == null || node.IsMaxLevel) return false;
            if (!ArePrerequisitesMet(node)) return false;

            int cost = node.CurrentCost;
            var quotaManager = UnityEngine.Object.FindAnyObjectByType<QuotaAndScoreManager>();
            if (quotaManager != null)
            {
                if (!quotaManager.TrySpendScrapCoins(cost))
                {
                    return false;
                }
            }

            node.currentLevel++;
            ApplyNodeEffect(node);
            OnUpgradeTreeChanged?.Invoke();

            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayUpgradeBuy();
            }

            return true;
        }

        public bool PurchaseHelperRobot()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            int cost = config != null ? config.fixedHelperRobotPrice : 50;

            var quotaManager = UnityEngine.Object.FindAnyObjectByType<QuotaAndScoreManager>();
            if (quotaManager != null)
            {
                if (!quotaManager.TrySpendScrapCoins(cost))
                {
                    return false;
                }
            }

            if (config != null)
            {
                config.activeHelperRobotsCount++;
            }

            if (HelperDroneManager.Instance != null)
            {
                HelperDroneManager.Instance.SpawnHelperRobot();
            }

            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayUpgradeBuy();
            }

            OnUpgradeTreeChanged?.Invoke();
            return true;
        }

        private void ApplyNodeEffect(UpgradeNodeData node)
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            if (config == null) return;

            switch (node.nodeId)
            {
                case "player_speed_1":
                case "player_speed_2":
                case "player_speed_3":
                    config.baseThrusterAcceleration *= 1.22f;
                    config.maxThrusterSpeed *= 1.22f;
                    break;

                case "player_handling_1":
                case "player_handling_2":
                case "player_handling_3":
                    config.verticalThrusterAcceleration *= 1.20f;
                    config.spaceLinearDamping *= 1.25f;
                    config.dashCooldown *= 0.82f;
                    break;

                case "player_cargo_1":
                    config.maxHeldObjects = 2;
                    break;
                case "player_cargo_2":
                    config.maxHeldObjects = 4;
                    break;
                case "player_cargo_3":
                    config.maxHeldObjects = 7;
                    break;

                case "player_wow":
                    config.puntLaunchImpulse *= 2.0f;
                    config.dashImpulse *= 2.0f;
                    config.playerRelativisticOverdriveUnlocked = true;
                    break;

                case "hole_heat_fill":
                    config.heatFillRateMultiplier = 1.0f + (node.currentLevel * 0.10f);
                    config.heatDecayDelay += 1.0f;
                    break;

                case "hole_wow":
                    config.orbitalGuidanceStrength *= 2.0f;
                    config.blackHoleHawkingPulseUnlocked = true;
                    break;

                case "scrap_spawn_freq":
                    config.scrapSpawnInterval = Mathf.Max(0.25f, config.scrapSpawnInterval * 0.80f);
                    config.maxActiveScrap += 15;
                    break;

                case "scrap_tier_plus":
                    config.unlockedScrapTier = Mathf.Max(config.unlockedScrapTier, 1 + node.currentLevel);
                    break;

                case "scrap_wow":
                    config.unlockedScrapTier = 4;
                    break;

                case "valuable_scrap_chance":
                case "energy_siphon_speed":
                case "camera_add":
                    if (node.currentLevel == 1) config.valuableScrapSpawnChance = 0.25f;
                    else if (node.currentLevel == 2) config.valuableScrapSpawnChance = 0.35f;
                    else if (node.currentLevel >= 3) config.valuableScrapSpawnChance = 0.50f;
                    break;

                case "valuable_scrap_mult":
                case "energy_battery_cap":
                case "camera_flash":
                    if (node.currentLevel == 1) config.valuableScrapMultiplier = 5.0f;
                    else if (node.currentLevel == 2) config.valuableScrapMultiplier = 8.0f;
                    else if (node.currentLevel >= 3) config.valuableScrapMultiplier = 12.0f;
                    break;

                case "quantum_decay_wow":
                case "energy_wow":
                case "camera_wow":
                    config.quantumDecayUnlocked = true;
                    break;

                case "robot_trawl":
                    config.magnetPullForce *= 1.25f;
                    break;

                case "robot_speed":
                    config.helperBotSpeed *= 1.30f;
                    break;

                case "robot_magnet_range":
                    config.isMagnetUnlocked = true;
                    config.magnetPullRange = 22f + (node.currentLevel * 4f);
                    config.magnetConeAngle = 30f + (node.currentLevel * 5f);
                    break;

                case "robot_wow":
                    config.kamikazeProtocolUnlocked = true;
                    break;
            }
        }

        private void ActivateExtraSurveillanceCameras(int level)
        {
            var existing = UnityEngine.Object.FindObjectsByType<DynamicSurveillanceCamera>(FindObjectsInactive.Include);
            if (existing != null && existing.Length > 0)
            {
                int targetTotal = Mathf.Min(6, 2 + level);
                for (int i = existing.Length; i < targetTotal; i++)
                {
                    float phi = (i * 137.5f) * Mathf.Deg2Rad;
                    float cosTheta = 1f - (2f * i + 1f) / (4f + level);
                    float sinTheta = Mathf.Sqrt(Mathf.Max(0f, 1f - cosTheta * cosTheta));
                    Vector3 pos = new Vector3(Mathf.Cos(phi) * sinTheta, cosTheta, Mathf.Sin(phi) * sinTheta) * 36f;

                    var newCam = UnityEngine.Object.Instantiate(existing[0].gameObject, pos, Quaternion.identity);
                    newCam.name = $"SurveillanceCamera_0{i + 1}";
                    newCam.SetActive(true);
                }
            }
        }

        [ContextMenu("Reset to Default Nodes")]
        public void PopulateDefaultNodes()
        {
            nodes.Clear();

            // Center Hub
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "hub",
                titleRU = "Навыки",
                branch = UpgradeBranch.Hub,
                currentLevel = 1,
                maxLevel = 1,
                levelCosts = new int[] { 0 },
                prerequisiteNodeIds = new string[0],
                isWowPerk = false,
                uiPosition = new Vector2(0f, 0f),
                descriptionRU = "Центральный узел дерева"
            });

            // Left Branch: Player Track 1 - Speed
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_speed_1",
                titleRU = "Скорость 1",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 25 },
                prerequisiteNodeIds = new string[] { "hub" },
                isWowPerk = false,
                uiPosition = new Vector2(-150f, 130f),
                descriptionRU = "+22% тяга и скорость"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_speed_2",
                titleRU = "Скорость 2",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 60 },
                prerequisiteNodeIds = new string[] { "player_speed_1" },
                isWowPerk = false,
                uiPosition = new Vector2(-280f, 160f),
                descriptionRU = "+22% тяга и скорость"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_speed_3",
                titleRU = "Скорость 3",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 120 },
                prerequisiteNodeIds = new string[] { "player_speed_2" },
                isWowPerk = false,
                uiPosition = new Vector2(-410f, 180f),
                descriptionRU = "+22% тяга и скорость"
            });

            // Left Branch: Player Track 2 - Handling
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_handling_1",
                titleRU = "Маневр 1",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 25 },
                prerequisiteNodeIds = new string[] { "hub" },
                isWowPerk = false,
                uiPosition = new Vector2(-160f, 25f),
                descriptionRU = "+20% маневр, -18% откат"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_handling_2",
                titleRU = "Маневр 2",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 60 },
                prerequisiteNodeIds = new string[] { "player_handling_1" },
                isWowPerk = false,
                uiPosition = new Vector2(-290f, 25f),
                descriptionRU = "+20% маневр, -18% откат"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_handling_3",
                titleRU = "Маневр 3",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 120 },
                prerequisiteNodeIds = new string[] { "player_handling_2" },
                isWowPerk = false,
                uiPosition = new Vector2(-420f, 25f),
                descriptionRU = "+20% маневр, -18% откат"
            });

            // Left Branch: Player Track 3 - Cargo
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_cargo_1",
                titleRU = "Объем 1",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 20 },
                prerequisiteNodeIds = new string[] { "hub" },
                isWowPerk = false,
                uiPosition = new Vector2(-150f, -80f),
                descriptionRU = "Вместимость: 2 объекта"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_cargo_2",
                titleRU = "Объем 2",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 55 },
                prerequisiteNodeIds = new string[] { "player_cargo_1" },
                isWowPerk = false,
                uiPosition = new Vector2(-280f, -110f),
                descriptionRU = "Вместимость: 4 объекта"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_cargo_3",
                titleRU = "Объем 3",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 110 },
                prerequisiteNodeIds = new string[] { "player_cargo_2" },
                isWowPerk = false,
                uiPosition = new Vector2(-410f, -135f),
                descriptionRU = "Вместимость: 7 объектов"
            });

            // Left WOW Capstone
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "player_wow",
                titleRU = "ВАУ: Игрок",
                branch = UpgradeBranch.Player,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 250 },
                prerequisiteNodeIds = new string[] { "player_speed_3", "player_handling_3", "player_cargo_3" },
                isWowPerk = true,
                uiPosition = new Vector2(-545f, 25f),
                descriptionRU = "x2 импульс удара и сверх-рывок"
            });

            // Top Branch: Black Hole
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "hole_heat_fill",
                titleRU = "КПД воронки",
                branch = UpgradeBranch.BlackHole,
                currentLevel = 0,
                maxLevel = 5,
                levelCosts = new int[] { 25, 45, 75, 120, 180 },
                prerequisiteNodeIds = new string[] { "hub" },
                isWowPerk = false,
                uiPosition = new Vector2(0f, 150f),
                descriptionRU = "+15% КПД энергии, +10% макс. масса"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "hole_wow",
                titleRU = "ВАУ: Дыра",
                branch = UpgradeBranch.BlackHole,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 250 },
                prerequisiteNodeIds = new string[] { "hole_heat_fill" },
                isWowPerk = true,
                uiPosition = new Vector2(0f, 280f),
                descriptionRU = "x2 множитель в воронке"
            });

            // Right Branch: Scrap
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "scrap_spawn_freq",
                titleRU = "Частота мусора",
                branch = UpgradeBranch.Scrap,
                currentLevel = 0,
                maxLevel = 3,
                levelCosts = new int[] { 25, 50, 100 },
                prerequisiteNodeIds = new string[] { "hub" },
                isWowPerk = false,
                uiPosition = new Vector2(170f, 85f),
                descriptionRU = "-20% интервал, +15 макс. мусора"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "scrap_tier_plus",
                titleRU = "Тир мусора +1",
                branch = UpgradeBranch.Scrap,
                currentLevel = 0,
                maxLevel = 2,
                levelCosts = new int[] { 35, 80 },
                prerequisiteNodeIds = new string[] { "scrap_spawn_freq" },
                isWowPerk = false,
                uiPosition = new Vector2(310f, 85f),
                descriptionRU = "Разблокирует Тир 2 и Тир 3 мусор"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "scrap_wow",
                titleRU = "Аномалия Т4",
                branch = UpgradeBranch.Scrap,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 250 },
                prerequisiteNodeIds = new string[] { "scrap_tier_plus" },
                isWowPerk = true,
                uiPosition = new Vector2(460f, 85f),
                descriptionRU = "Космическая аномалия: ценный мусор"
            });

            // Bottom-Right Branch: Valuable Glowing Scrap & Matter Enrichment
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "valuable_scrap_chance",
                titleRU = "Шанс ценного мусора",
                branch = UpgradeBranch.Energy,
                currentLevel = 0,
                maxLevel = 3,
                levelCosts = new int[] { 30, 65, 130 },
                prerequisiteNodeIds = new string[] { "hub" },
                isWowPerk = false,
                uiPosition = new Vector2(170f, -65f),
                descriptionRU = "Шанс ценного мусора: 15% -> 25% -> 35% -> 50%"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "valuable_scrap_mult",
                titleRU = "Обогащение материи",
                branch = UpgradeBranch.Energy,
                currentLevel = 0,
                maxLevel = 3,
                levelCosts = new int[] { 45, 95, 190 },
                prerequisiteNodeIds = new string[] { "valuable_scrap_chance" },
                isWowPerk = false,
                uiPosition = new Vector2(310f, -65f),
                descriptionRU = "Множитель ценного мусора: x3 -> x5 -> x8 -> x12"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "quantum_decay_wow",
                titleRU = "ВАУ: Квантовый распад",
                branch = UpgradeBranch.Energy,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 250 },
                prerequisiteNodeIds = new string[] { "valuable_scrap_mult" },
                isWowPerk = true,
                uiPosition = new Vector2(460f, -65f),
                descriptionRU = "ВАУ-перк: Квантовый распад\nУдваивает массу и монеты со всего мусора"
            });

            // Bottom Branch: Robots
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "robot_trawl",
                titleRU = "Мощность трала",
                branch = UpgradeBranch.Robots,
                currentLevel = 0,
                maxLevel = 3,
                levelCosts = new int[] { 25, 55, 110 },
                prerequisiteNodeIds = new string[] { "hub" },
                isWowPerk = false,
                uiPosition = new Vector2(-60f, -145f),
                descriptionRU = "+25% сила захвата магнита"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "robot_speed",
                titleRU = "Скорость роботов",
                branch = UpgradeBranch.Robots,
                currentLevel = 0,
                maxLevel = 3,
                levelCosts = new int[] { 30, 65, 120 },
                prerequisiteNodeIds = new string[] { "robot_trawl" },
                isWowPerk = false,
                uiPosition = new Vector2(-155f, -265f),
                descriptionRU = "+30% скорость роботов за ур."
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "robot_magnet_range",
                titleRU = "Радиус захвата",
                branch = UpgradeBranch.Robots,
                currentLevel = 0,
                maxLevel = 3,
                levelCosts = new int[] { 35, 70, 130 },
                prerequisiteNodeIds = new string[] { "robot_trawl" },
                isWowPerk = false,
                uiPosition = new Vector2(35f, -265f),
                descriptionRU = "Магнит захватывает дальше и шире"
            });
            nodes.Add(new UpgradeNodeData
            {
                nodeId = "robot_wow",
                titleRU = "Камикадзе",
                branch = UpgradeBranch.Robots,
                currentLevel = 0,
                maxLevel = 1,
                levelCosts = new int[] { 250 },
                prerequisiteNodeIds = new string[] { "robot_speed", "robot_magnet_range" },
                isWowPerk = true,
                uiPosition = new Vector2(-60f, -385f),
                descriptionRU = "Протокол Камикадзе для роботов"
            });

            BuildNodeMap();
        }
    }
}
