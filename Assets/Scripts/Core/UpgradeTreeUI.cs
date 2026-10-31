using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class UpgradeTreeUI : MonoBehaviour
    {
        public static UpgradeTreeUI Instance { get; private set; }

        [Header("Hierarchy References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform mapContainer;
        [SerializeField] private RectTransform nodesContainer;
        [SerializeField] private RectTransform linesContainer;
        [SerializeField] private TextMeshProUGUI currencyText;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private Button resetCameraButton;
        [SerializeField] private SkillTreeViewportController viewportController;

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
        private CanvasGroup _canvasGroup;
        private Tweener _fadeTween;
        private bool _isInitialized = false;

        private class NodeUIElement
        {
            public UpgradeNodeData node;
            public GameObject gameObject;
            public Button button;
            public Image background;
            public Outline outline;
            public TextMeshProUGUI label;
            public UIButtonTweenAnimator animator;
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

            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayTerminalToggle();
            }

            EnsureCanvasGroup();

            if (open)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (panelRoot != null)
                {
                    panelRoot.SetActive(true);
                }

                _fadeTween?.Kill();
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.blocksRaycasts = true;
                    _canvasGroup.interactable = true;
                    _fadeTween = _canvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
                }

                if (viewportController != null)
                {
                    viewportController.ResetView();
                }

                RefreshUI();
                AnimateNodesRippleOutward();
            }
            else
            {
                Time.timeScale = 1f;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                if (_canvasGroup != null)
                {
                    _canvasGroup.blocksRaycasts = false;
                    _canvasGroup.interactable = false;

                    _fadeTween?.Kill();
                    _fadeTween = _canvasGroup.DOFade(0f, 0.15f)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            if (panelRoot != null && !UpgradeTreeManager.IsTreeOpen)
                            {
                                panelRoot.SetActive(false);
                            }
                        });
                }
                else if (panelRoot != null)
                {
                    panelRoot.SetActive(false);
                }

                if (mapContainer != null)
                {
                    mapContainer.DOKill();
                    mapContainer.DOScale(Vector3.one * 0.85f, 0.15f)
                        .SetEase(Ease.InQuad)
                        .SetUpdate(true);
                }
            }
        }

        private void EnsureCanvasGroup()
        {
            if (panelRoot != null && _canvasGroup == null)
            {
                _canvasGroup = panelRoot.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = panelRoot.AddComponent<CanvasGroup>();
                }
            }
        }

        /// <summary>
        /// Choreographs an outward ripple animation where skill tree nodes scale up (0 -> 1)
        /// with a delay proportional to their distance from the center hub (0,0).
        /// </summary>
        private void AnimateNodesRippleOutward()
        {
            foreach (var kvp in _nodeUIElements)
            {
                NodeUIElement elem = kvp.Value;
                if (elem.gameObject == null) continue;

                Vector3 targetScale = elem.animator != null ? elem.animator.OriginalScale : Vector3.one;
                float dist = elem.node != null ? elem.node.uiPosition.magnitude : 0f;
                float delay = Mathf.Clamp(dist * 0.0006f, 0.02f, 0.22f);

                elem.gameObject.transform.DOKill();
                elem.gameObject.transform.localScale = Vector3.zero;
                elem.gameObject.transform.DOScale(targetScale, 0.28f)
                    .SetEase(Ease.OutBack)
                    .SetDelay(delay)
                    .SetUpdate(true);
            }

            // Also ripple lines
            for (int i = 0; i < _lineUIElements.Count; i++)
            {
                LineUIElement line = _lineUIElements[i];
                if (line.lineImage == null) continue;

                line.lineImage.rectTransform.DOKill();
                line.lineImage.rectTransform.localScale = new Vector3(0f, 1f, 1f);
                line.lineImage.rectTransform.DOScale(Vector3.one, 0.25f)
                    .SetEase(Ease.OutQuad)
                    .SetDelay(0.08f)
                    .SetUpdate(true);
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

            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            EnsureCanvasGroup();
            BuildUIHierarchy();
            BuildConnectingLines();
            BuildNodeButtons();
            BuildRobotPurchaseButton();
            BuildResetCameraButton();

            // Wire font applier to new hierarchy
            GlobalFontApplier.ApplyTo(panelRoot);

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

            // 1. Skill Tree Viewport (captures pan and scroll input)
            if (viewportRect == null)
            {
                Transform existingVp = panelRoot.transform.Find("SkillTreeViewport");
                if (existingVp != null)
                {
                    viewportRect = existingVp.GetComponent<RectTransform>();
                }
                else
                {
                    GameObject vpGo = new GameObject("SkillTreeViewport", typeof(RectTransform), typeof(Image));
                    vpGo.transform.SetParent(panelRoot.transform, false);
                    viewportRect = vpGo.GetComponent<RectTransform>();
                    viewportRect.anchorMin = Vector2.zero;
                    viewportRect.anchorMax = Vector2.one;
                    viewportRect.sizeDelta = Vector2.zero;

                    Image vpImg = vpGo.GetComponent<Image>();
                    vpImg.color = new Color(0f, 0f, 0f, 0.01f); // Near transparent raycast target
                    vpImg.raycastTarget = true;
                }
            }

            // 2. Viewport Controller
            if (viewportController == null && viewportRect != null)
            {
                viewportController = viewportRect.GetComponent<SkillTreeViewportController>();
                if (viewportController == null)
                {
                    viewportController = viewportRect.gameObject.AddComponent<SkillTreeViewportController>();
                }
            }

            // 3. Map Container (scaled and panned)
            if (mapContainer == null)
            {
                Transform existingMap = viewportRect.transform.Find("SkillTreeMapContainer");
                if (existingMap != null)
                {
                    mapContainer = existingMap.GetComponent<RectTransform>();
                }
                else
                {
                    GameObject mapGo = new GameObject("SkillTreeMapContainer", typeof(RectTransform));
                    mapGo.transform.SetParent(viewportRect, false);
                    mapContainer = mapGo.GetComponent<RectTransform>();
                    mapContainer.anchorMin = new Vector2(0.5f, 0.5f);
                    mapContainer.anchorMax = new Vector2(0.5f, 0.5f);
                    mapContainer.pivot = new Vector2(0.5f, 0.5f);
                    mapContainer.anchoredPosition = Vector2.zero;
                    mapContainer.sizeDelta = Vector2.zero;
                }
            }

            if (viewportController != null)
            {
                viewportController.Initialize(viewportRect, mapContainer);
            }

            // 4. Lines Container (inside Map Container)
            if (linesContainer == null)
            {
                Transform existingLines = mapContainer.Find("LinesContainer");
                if (existingLines != null)
                {
                    linesContainer = existingLines.GetComponent<RectTransform>();
                }
                else
                {
                    GameObject linesGo = new GameObject("LinesContainer", typeof(RectTransform));
                    linesGo.transform.SetParent(mapContainer, false);
                    linesContainer = linesGo.GetComponent<RectTransform>();
                    linesContainer.anchorMin = new Vector2(0.5f, 0.5f);
                    linesContainer.anchorMax = new Vector2(0.5f, 0.5f);
                    linesContainer.sizeDelta = Vector2.zero;
                }
            }

            // 5. Nodes Container (inside Map Container, above Lines)
            if (nodesContainer == null)
            {
                Transform existingNodes = mapContainer.Find("NodesContainer");
                if (existingNodes != null)
                {
                    nodesContainer = existingNodes.GetComponent<RectTransform>();
                }
                else
                {
                    GameObject nodesGo = new GameObject("NodesContainer", typeof(RectTransform));
                    nodesGo.transform.SetParent(mapContainer, false);
                    nodesContainer = nodesGo.GetComponent<RectTransform>();
                    nodesContainer.anchorMin = new Vector2(0.5f, 0.5f);
                    nodesContainer.anchorMax = new Vector2(0.5f, 0.5f);
                    nodesContainer.sizeDelta = Vector2.zero;
                }
            }

            // 6. Fixed Header Text (on panelRoot directly)
            if (headerText == null)
            {
                Transform existingHeader = panelRoot.transform.Find("Text_Header");
                if (existingHeader != null)
                {
                    headerText = existingHeader.GetComponent<TextMeshProUGUI>();
                }
                else
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
            }

            // 7. Fixed Currency Text (on panelRoot directly)
            if (currencyText == null)
            {
                Transform existingCurr = panelRoot.transform.Find("Text_Currency");
                if (existingCurr != null)
                {
                    currencyText = existingCurr.GetComponent<TextMeshProUGUI>();
                }
                else
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
        }

        private void BuildConnectingLines()
        {
            if (UpgradeTreeManager.Instance == null || linesContainer == null) return;
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
            if (UpgradeTreeManager.Instance == null || nodesContainer == null) return;
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

            GameObject nodeGo = new GameObject($"Node_{node.nodeId}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
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

            UIButtonTweenAnimator animator = nodeGo.GetComponent<UIButtonTweenAnimator>();
            animator.SetOriginalScale(Vector3.one);

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
                label = label,
                animator = animator
            };
        }

        private void BuildRobotPurchaseButton()
        {
            // Docked fixed at bottom-right of panelRoot so it's always accessible at any zoom level
            Vector2 size = new Vector2(160f, 60f);

            Transform existing = panelRoot.transform.Find("Node_BuyHelperRobot");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            GameObject robotGo = new GameObject("Node_BuyHelperRobot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
            robotGo.transform.SetParent(panelRoot.transform, false);

            RectTransform rt = robotGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-25f, 25f);
            rt.sizeDelta = size;

            Image bg = robotGo.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.10f, 0.05f, 0.95f);

            Outline outline = robotGo.GetComponent<Outline>();
            outline.effectDistance = new Vector2(2f, -2f);
            outline.effectColor = new Color(1f, 0.85f, 0.2f, 1f);

            UIButtonTweenAnimator animator = robotGo.GetComponent<UIButtonTweenAnimator>();
            animator.SetOriginalScale(Vector3.one);

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
                label = label,
                animator = animator
            };
        }

        private void BuildResetCameraButton()
        {
            Transform existing = panelRoot.transform.Find("Button_ResetCamera");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }

            GameObject resetGo = new GameObject("Button_ResetCamera", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
            resetGo.transform.SetParent(panelRoot.transform, false);

            RectTransform rt = resetGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(25f, 25f);
            rt.sizeDelta = new Vector2(175f, 38f);

            Image bg = resetGo.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.95f);

            Outline outline = resetGo.GetComponent<Outline>();
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.effectColor = new Color(0.3f, 0.8f, 1f, 0.8f);

            UIButtonTweenAnimator animator = resetGo.GetComponent<UIButtonTweenAnimator>();
            animator.SetOriginalScale(Vector3.one);

            resetCameraButton = resetGo.GetComponent<Button>();
            resetCameraButton.onClick.AddListener(() =>
            {
                if (viewportController != null)
                {
                    viewportController.ResetView();
                }
            });

            GameObject labelGo = new GameObject("Text_Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(resetGo.transform, false);

            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(4f, 2f);
            labelRt.offsetMax = new Vector2(-4f, -2f);

            TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 11;
            label.fontStyle = FontStyles.Bold;
            label.color = new Color(0.7f, 0.95f, 1f, 1f);
            label.text = "[СБРОСИТЬ КАМЕРУ]";
            label.raycastTarget = false;
        }

        private void OnNodeClicked(string nodeId)
        {
            if (UpgradeTreeManager.Instance == null) return;
            bool success = UpgradeTreeManager.Instance.PurchaseNode(nodeId);

            if (_nodeUIElements.TryGetValue(nodeId, out var elem))
            {
                if (success)
                {
                    elem.animator?.PlayPurchaseSuccessAnimation();

                    // Animate incoming lines from parent
                    for (int i = 0; i < _lineUIElements.Count; i++)
                    {
                        var line = _lineUIElements[i];
                        if (line.toId == nodeId && line.lineImage != null)
                        {
                            line.lineImage.DOKill();
                            line.lineImage.color = new Color(0.2f, 1f, 0.5f, 1f);
                            line.lineImage.rectTransform.DOKill();
                            line.lineImage.rectTransform.DOPunchScale(new Vector3(0.1f, 1.4f, 0f), 0.35f, 8, 0.8f).SetUpdate(true);
                        }
                    }

                    RefreshUI();
                }
                else
                {
                    elem.animator?.PlayInsufficientCoinsAnimation();
                }
            }
            else if (success)
            {
                RefreshUI();
            }
        }

        private void OnBuyRobotClicked()
        {
            if (UpgradeTreeManager.Instance == null) return;
            bool success = UpgradeTreeManager.Instance.PurchaseHelperRobot();

            if (_robotButtonElement != null)
            {
                if (success)
                {
                    _robotButtonElement.animator?.PlayPurchaseSuccessAnimation();
                    RefreshUI();
                }
                else
                {
                    _robotButtonElement.animator?.PlayInsufficientCoinsAnimation();
                }
            }
            else if (success)
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

                elem.animator?.RefreshBaseColors();
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
            _robotButtonElement.animator?.RefreshBaseColors();
        }
    }
}
