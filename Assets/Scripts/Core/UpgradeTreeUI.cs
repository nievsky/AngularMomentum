using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class UpgradeTreeUI : MonoBehaviour
    {
        public static UpgradeTreeUI Instance { get; private set; }

        [Header("Hierarchy References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform nodesContainer;
        [SerializeField] private RectTransform linesContainer;
        [SerializeField] private TextMeshProUGUI currencyText;
        [SerializeField] private TextMeshProUGUI headerText;

        [Header("Visual Colors")]
        [SerializeField] private Color standardOutlineColor = new Color(0.2f, 0.9f, 1f, 1f); // Cyan
        [SerializeField] private Color wowOutlineColor = new Color(1f, 0.2f, 0.2f, 1f);      // Bright Red
        [SerializeField] private Color maxedOutlineColor = new Color(0.2f, 1f, 0.4f, 1f);    // Bright Green
        [SerializeField] private Color lockedOutlineColor = new Color(0.35f, 0.4f, 0.5f, 0.4f); // Dim Gray
        [SerializeField] private Color nodeBgColor = new Color(0.06f, 0.09f, 0.14f, 0.95f);
        [SerializeField] private Color wowBgColor = new Color(0.22f, 0.05f, 0.05f, 0.95f);

        private readonly Dictionary<string, NodeUIElement> _nodeUIElements = new Dictionary<string, NodeUIElement>();
        private readonly List<LineUIElement> _lineUIElements = new List<LineUIElement>();
        private NodeUIElement _robotButtonElement;
        private bool _isInitialized = false;

        private class NodeUIElement
        {
            public UpgradeNodeData node;
            public GameObject gameObject;
            public Button button;
            public Image background;
            public Outline outline;
            public TextMeshProUGUI label;
        }

        private class LineUIElement
        {
            public string fromId;
            public string toId;
            public Image lineImage;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            InitializeUI();
            SetTreeOpen(false);

            if (UpgradeTreeManager.Instance != null)
            {
                UpgradeTreeManager.Instance.OnUpgradeTreeChanged += RefreshUI;
            }

            var quotaManager = Object.FindAnyObjectByType<QuotaAndScoreManager>();
            if (quotaManager != null)
            {
                quotaManager.OnScoreAdded += HandleScoreAdded;
            }
        }

        private void OnDestroy()
        {
            if (UpgradeTreeManager.Instance != null)
            {
                UpgradeTreeManager.Instance.OnUpgradeTreeChanged -= RefreshUI;
            }

            var quotaManager = Object.FindAnyObjectByType<QuotaAndScoreManager>();
            if (quotaManager != null)
            {
                quotaManager.OnScoreAdded -= HandleScoreAdded;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame)
                {
                    ToggleTree();
                }
                else if (UpgradeTreeManager.IsTreeOpen && keyboard.escapeKey.wasPressedThisFrame)
                {
                    SetTreeOpen(false);
                }
            }
        }

        public void ToggleTree()
        {
            SetTreeOpen(!UpgradeTreeManager.IsTreeOpen);
        }

        public void SetTreeOpen(bool open)
        {
            UpgradeTreeManager.IsTreeOpen = open;

            if (panelRoot != null)
            {
                if (panelRoot == gameObject)
                {
                    var cg = panelRoot.GetComponent<CanvasGroup>();
                    if (cg == null) cg = panelRoot.AddComponent<CanvasGroup>();
                    cg.alpha = open ? 1f : 0f;
                    cg.blocksRaycasts = open;
                    cg.interactable = open;
                }
                else
                {
                    panelRoot.SetActive(open);
                }
            }

            if (open)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                RefreshUI();
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void HandleScoreAdded(float scoreGained, int totalCoins)
        {
            if (UpgradeTreeManager.IsTreeOpen)
            {
                RefreshCurrencyText();
                RefreshNodeVisuals();
            }
        }

        public void InitializeUI()
        {
            if (_isInitialized) return;

            // Ensure containers exist
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            BuildUIHierarchy();
            BuildConnectingLines();
            BuildNodeButtons();
            BuildRobotPurchaseButton();

            _isInitialized = true;
            RefreshUI();
        }

        private void BuildUIHierarchy()
        {
            RectTransform rootRect = panelRoot.GetComponent<RectTransform>();
            if (rootRect == null) rootRect = panelRoot.AddComponent<RectTransform>();

            // Dark full-screen translucent overlay
            var bgImg = panelRoot.GetComponent<Image>();
            if (bgImg == null) bgImg = panelRoot.AddComponent<Image>();
            bgImg.color = new Color(0.04f, 0.06f, 0.09f, 0.94f);

            // Containers
            if (linesContainer == null)
            {
                GameObject linesGo = new GameObject("LinesContainer", typeof(RectTransform));
                linesGo.transform.SetParent(panelRoot.transform, false);
                linesContainer = linesGo.GetComponent<RectTransform>();
                linesContainer.anchorMin = new Vector2(0.5f, 0.5f);
                linesContainer.anchorMax = new Vector2(0.5f, 0.5f);
                linesContainer.sizeDelta = Vector2.zero;
            }

            if (nodesContainer == null)
            {
                GameObject nodesGo = new GameObject("NodesContainer", typeof(RectTransform));
                nodesGo.transform.SetParent(panelRoot.transform, false);
                nodesContainer = nodesGo.GetComponent<RectTransform>();
                nodesContainer.anchorMin = new Vector2(0.5f, 0.5f);
                nodesContainer.anchorMax = new Vector2(0.5f, 0.5f);
                nodesContainer.sizeDelta = Vector2.zero;
            }

            // Header and Currency Texts
            if (headerText == null)
            {
                GameObject headerGo = new GameObject("Text_Header", typeof(RectTransform), typeof(TextMeshProUGUI));
                headerGo.transform.SetParent(panelRoot.transform, false);
                RectTransform rt = headerGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -25f);
                rt.sizeDelta = new Vector2(800f, 40f);

                headerText = headerGo.GetComponent<TextMeshProUGUI>();
                headerText.alignment = TextAlignmentOptions.Center;
                headerText.fontSize = 24;
                headerText.fontStyle = FontStyles.Bold;
                headerText.color = new Color(0.9f, 0.95f, 1f, 1f);
                headerText.text = "ДЕРЕВО НАВЫКОВ | SKILL TREE (TAB - Закрыть)";
            }

            if (currencyText == null)
            {
                GameObject currGo = new GameObject("Text_Currency", typeof(RectTransform), typeof(TextMeshProUGUI));
                currGo.transform.SetParent(panelRoot.transform, false);
                RectTransform rt = currGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -65f);
                rt.sizeDelta = new Vector2(500f, 32f);

                currencyText = currGo.GetComponent<TextMeshProUGUI>();
                currencyText.alignment = TextAlignmentOptions.Center;
                currencyText.fontSize = 18;
                currencyText.color = new Color(1f, 0.85f, 0.2f, 1f);
            }
        }

        private void BuildConnectingLines()
        {
            if (UpgradeTreeManager.Instance == null) return;
            var nodes = UpgradeTreeManager.Instance.Nodes;

            _lineUIElements.Clear();

            // Clear previous line children
            for (int i = linesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(linesContainer.GetChild(i).gameObject);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                UpgradeNodeData childNode = nodes[i];
                if (childNode.prerequisiteNodeIds == null) continue;

                for (int j = 0; j < childNode.prerequisiteNodeIds.Length; j++)
                {
                    string parentId = childNode.prerequisiteNodeIds[j];
                    UpgradeNodeData parentNode = UpgradeTreeManager.Instance.GetNode(parentId);
                    if (parentNode == null) continue;

                    Vector2 posA = parentNode.uiPosition;
                    Vector2 posB = childNode.uiPosition;
                    Vector2 diff = posB - posA;
                    float dist = diff.magnitude;
                    float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

                    GameObject lineGo = new GameObject($"Line_{parentId}_to_{childNode.nodeId}", typeof(RectTransform), typeof(Image));
                    lineGo.transform.SetParent(linesContainer, false);

                    RectTransform rt = lineGo.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = (posA + posB) * 0.5f;
                    rt.sizeDelta = new Vector2(dist, 3f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, angle);

                    Image img = lineGo.GetComponent<Image>();
                    img.color = new Color(0.3f, 0.35f, 0.45f, 0.25f);

                    _lineUIElements.Add(new LineUIElement
                    {
                        fromId = parentId,
                        toId = childNode.nodeId,
                        lineImage = img
                    });
                }
            }
        }

        private void BuildNodeButtons()
        {
            if (UpgradeTreeManager.Instance == null) return;
            var nodes = UpgradeTreeManager.Instance.Nodes;

            _nodeUIElements.Clear();

            // Clear previous node children
            for (int i = nodesContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(nodesContainer.GetChild(i).gameObject);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                UpgradeNodeData node = nodes[i];
                CreateNodeButton(node);
            }
        }

        private void CreateNodeButton(UpgradeNodeData node)
        {
            Vector2 size = node.isWowPerk ? new Vector2(130f, 62f) : (node.nodeId == "hub" ? new Vector2(110f, 60f) : new Vector2(115f, 54f));

            GameObject nodeGo = new GameObject($"Node_{node.nodeId}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            nodeGo.transform.SetParent(nodesContainer, false);

            RectTransform rt = nodeGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = node.uiPosition;
            rt.sizeDelta = size;

            Image bg = nodeGo.GetComponent<Image>();
            bg.color = node.isWowPerk ? wowBgColor : nodeBgColor;

            Outline outline = nodeGo.GetComponent<Outline>();
            outline.effectDistance = node.isWowPerk ? new Vector2(3f, -3f) : new Vector2(2f, -2f);
            outline.effectColor = node.isWowPerk ? wowOutlineColor : standardOutlineColor;

            Button btn = nodeGo.GetComponent<Button>();
            string capturedId = node.nodeId;
            btn.onClick.AddListener(() => OnNodeClicked(capturedId));

            // Label
            GameObject labelGo = new GameObject("Text_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(nodeGo.transform, false);

            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(4f, 4f);
            labelRt.offsetMax = new Vector2(-4f, -4f);

            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = node.isWowPerk ? 12 : 11;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;

            _nodeUIElements[node.nodeId] = new NodeUIElement
            {
                node = node,
                gameObject = nodeGo,
                button = btn,
                background = bg,
                outline = outline,
                label = label
            };
        }

        private void BuildRobotPurchaseButton()
        {
            Vector2 pos = new Vector2(350f, -220f);
            Vector2 size = new Vector2(150f, 58f);

            GameObject robotGo = new GameObject("Node_BuyHelperRobot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            robotGo.transform.SetParent(nodesContainer, false);

            RectTransform rt = robotGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image bg = robotGo.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.10f, 0.05f, 0.95f);

            Outline outline = robotGo.GetComponent<Outline>();
            outline.effectDistance = new Vector2(2f, -2f);
            outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);

            Button btn = robotGo.GetComponent<Button>();
            btn.onClick.AddListener(OnBuyRobotClicked);

            GameObject labelGo = new GameObject("Text_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(robotGo.transform, false);

            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(4f, 4f);
            labelRt.offsetMax = new Vector2(-4f, -4f);

            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 11;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.raycastTarget = false;

            _robotButtonElement = new NodeUIElement
            {
                gameObject = robotGo,
                button = btn,
                background = bg,
                outline = outline,
                label = label
            };
        }

        private void OnNodeClicked(string nodeId)
        {
            if (UpgradeTreeManager.Instance == null) return;
            bool success = UpgradeTreeManager.Instance.PurchaseNode(nodeId);
            if (success)
            {
                RefreshUI();
            }
        }

        private void OnBuyRobotClicked()
        {
            if (UpgradeTreeManager.Instance == null) return;
            bool success = UpgradeTreeManager.Instance.PurchaseHelperRobot();
            if (success)
            {
                RefreshUI();
            }
        }

        public void RefreshUI()
        {
            RefreshCurrencyText();
            RefreshNodeVisuals();
            RefreshLineVisuals();
            RefreshRobotButton();
        }

        private void RefreshCurrencyText()
        {
            if (currencyText == null) return;
            var quotaManager = Object.FindAnyObjectByType<QuotaAndScoreManager>();
            int coins = quotaManager != null ? quotaManager.ScrapCoins : 0;
            currencyText.text = $"Баланс: <color=#FFE640>{coins}</color> Scrap Coins";
        }

        private void RefreshNodeVisuals()
        {
            if (UpgradeTreeManager.Instance == null) return;
            var quotaManager = Object.FindAnyObjectByType<QuotaAndScoreManager>();
            int coins = quotaManager != null ? quotaManager.ScrapCoins : 0;

            foreach (var kvp in _nodeUIElements)
            {
                NodeUIElement elem = kvp.Value;
                UpgradeNodeData node = elem.node;

                bool prereqsMet = UpgradeTreeManager.Instance.ArePrerequisitesMet(node);
                bool canAfford = coins >= node.CurrentCost;
                bool isMax = node.IsMaxLevel;
                bool canPurchase = prereqsMet && canAfford && !isMax;

                // Color coding
                if (node.nodeId == "hub")
                {
                    elem.outline.effectColor = standardOutlineColor;
                    elem.background.color = nodeBgColor;
                    elem.label.text = $"<b>{node.titleRU}</b>\n<size=10><color=#00FF88>[АКТИВНО]</color></size>";
                    elem.button.interactable = false;
                }
                else if (isMax)
                {
                    elem.outline.effectColor = maxedOutlineColor;
                    elem.background.color = new Color(0.05f, 0.16f, 0.08f, 0.95f);
                    elem.label.text = $"<b>{node.titleRU}</b>\n<size=10><color=#00FF88>[МАКС. УР.]</color></size>";
                    elem.button.interactable = false;
                }
                else if (prereqsMet)
                {
                    if (node.isWowPerk)
                    {
                        elem.outline.effectColor = wowOutlineColor;
                        elem.background.color = canAfford ? wowBgColor : new Color(0.14f, 0.05f, 0.05f, 0.95f);
                    }
                    else
                    {
                        elem.outline.effectColor = canAfford ? standardOutlineColor : new Color(0.2f, 0.5f, 0.6f, 0.8f);
                        elem.background.color = nodeBgColor;
                    }

                    string costColor = canAfford ? "#FFD700" : "#FF5555";
                    string levelStr = node.maxLevel > 1 ? $"Lv.{node.currentLevel}/{node.maxLevel}" : "";
                    elem.label.text = $"<b>{node.titleRU}</b>\n<size=10><color={costColor}>{node.CurrentCost}©</color>  {levelStr}</size>";
                    elem.button.interactable = canPurchase;
                }
                else
                {
                    // Locked
                    elem.outline.effectColor = lockedOutlineColor;
                    elem.background.color = new Color(0.04f, 0.05f, 0.07f, 0.85f);
                    elem.label.text = $"<color=#778899><b>{node.titleRU}</b>\n<size=10>[ЗАКРЫТО]</size></color>";
                    elem.button.interactable = false;
                }
            }
        }

        private void RefreshLineVisuals()
        {
            if (UpgradeTreeManager.Instance == null) return;

            for (int i = 0; i < _lineUIElements.Count; i++)
            {
                LineUIElement line = _lineUIElements[i];
                UpgradeNodeData toNode = UpgradeTreeManager.Instance.GetNode(line.toId);

                if (toNode != null)
                {
                    if (toNode.IsUnlocked)
                    {
                        line.lineImage.color = new Color(0.2f, 1f, 0.5f, 0.85f); // Bright green
                    }
                    else if (UpgradeTreeManager.Instance.ArePrerequisitesMet(toNode))
                    {
                        line.lineImage.color = new Color(0.2f, 0.9f, 1f, 0.6f); // Cyan
                    }
                    else
                    {
                        line.lineImage.color = new Color(0.3f, 0.35f, 0.45f, 0.20f); // Dim gray
                    }
                }
            }
        }

        private void RefreshRobotButton()
        {
            if (_robotButtonElement == null) return;
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            int cost = config != null ? config.fixedHelperRobotPrice : 50;
            int robots = config != null ? config.activeHelperRobotsCount : 0;

            var quotaManager = Object.FindAnyObjectByType<QuotaAndScoreManager>();
            int coins = quotaManager != null ? quotaManager.ScrapCoins : 0;
            bool canAfford = coins >= cost;

            string costColor = canAfford ? "#FFD700" : "#FF5555";
            _robotButtonElement.label.text = $"<b>Купить робота</b>\n<size=10><color={costColor}>{cost}©</color> (В строю: {robots})</size>";
            _robotButtonElement.button.interactable = canAfford;
        }
    }
}
