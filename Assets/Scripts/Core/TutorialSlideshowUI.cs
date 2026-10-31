using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Multi-slide tutorial modal window displayed at the start of GameplayScene.
    /// Manages slides, pauses simulation (Time.timeScale = 0), unlocks cursor,
    /// blocks background inputs, and connects the player operator once completed.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialSlideshowUI : MonoBehaviour
    {
        public static TutorialSlideshowUI Instance { get; private set; }
        public static bool IsTutorialOpen { get; set; } = false;

        [Header("Root Panel")]
        [SerializeField] private GameObject tutorialWindowPanel;
        [SerializeField] private CanvasGroup tutorialCanvasGroup;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI headerText;

        [Header("Visual Frame")]
        [SerializeField] private Image slideImage;
        [SerializeField] private GameObject placeholderContainer;
        [SerializeField] private TextMeshProUGUI placeholderLabelText;
        [SerializeField] private Sprite[] slideIllustrations = new Sprite[4];

        [Header("Slide Content")]
        [SerializeField] private TextMeshProUGUI slideTitleText;
        [SerializeField] private TextMeshProUGUI slideDescriptionText;

        [Header("Navigation Bar")]
        [SerializeField] private Button prevButton;
        [SerializeField] private TextMeshProUGUI prevButtonText;
        [SerializeField] private TextMeshProUGUI pageIndicatorText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextButtonText;
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI startButtonText;

        private int _currentSlideIndex = 0;
        private const int TotalSlides = 4;

        public int CurrentSlideIndex => _currentSlideIndex;
        public Sprite[] SlideIllustrations => slideIllustrations;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureTutorialHierarchy();
            SetupButtonListeners();

            LocalizationManager.OnLanguageChanged += HandleLanguageChanged;
        }

        private void Start()
        {
            // On GameplayScene start, automatically display the tutorial window
            ShowTutorial();
        }

        private void OnDestroy()
        {
            LocalizationManager.OnLanguageChanged -= HandleLanguageChanged;
            if (Instance == this)
            {
                Instance = null;
                IsTutorialOpen = false;
            }
        }

        private void LateUpdate()
        {
            if (IsTutorialOpen)
            {
                Time.timeScale = 0f;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                if (tutorialWindowPanel != null)
                {
                    tutorialWindowPanel.transform.SetAsLastSibling();
                }
            }
        }

        private void HandleLanguageChanged(GameLanguage lang)
        {
            UpdateSlideUI();
        }

        private void SetupButtonListeners()
        {
            if (prevButton != null)
            {
                prevButton.onClick.RemoveAllListeners();
                prevButton.onClick.AddListener(PreviousSlide);
            }

            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(NextSlide);
            }

            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(StartGameFromTutorial);
            }
        }

        /// <summary>
        /// Opens the tutorial modal, pauses game world, and unlocks cursor.
        /// </summary>
        public void ShowTutorial()
        {
            IsTutorialOpen = true;
            Time.timeScale = 0f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _currentSlideIndex = 0;

            if (tutorialWindowPanel != null)
            {
                tutorialWindowPanel.SetActive(true);
                tutorialWindowPanel.transform.SetAsLastSibling();
            }

            if (tutorialCanvasGroup != null)
            {
                tutorialCanvasGroup.alpha = 1f;
                tutorialCanvasGroup.interactable = true;
                tutorialCanvasGroup.blocksRaycasts = true;
            }

            UpdateSlideUI();
        }

        /// <summary>
        /// Moves to the next tutorial slide.
        /// </summary>
        public void NextSlide()
        {
            if (_currentSlideIndex < TotalSlides - 1)
            {
                _currentSlideIndex++;
                PlayClickSound();
                UpdateSlideUI();
            }
        }

        /// <summary>
        /// Moves to the previous tutorial slide.
        /// </summary>
        public void PreviousSlide()
        {
            if (_currentSlideIndex > 0)
            {
                _currentSlideIndex--;
                PlayClickSound();
                UpdateSlideUI();
            }
        }

        /// <summary>
        /// Finishes the tutorial, restores timeScale = 1, locks cursor, and connects operator.
        /// </summary>
        public void StartGameFromTutorial()
        {
            IsTutorialOpen = false;
            Time.timeScale = 1f;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            PlayStartSound();

            if (tutorialCanvasGroup != null)
            {
                tutorialCanvasGroup.alpha = 0f;
                tutorialCanvasGroup.interactable = false;
                tutorialCanvasGroup.blocksRaycasts = false;
            }

            if (tutorialWindowPanel != null)
            {
                tutorialWindowPanel.SetActive(false);
            }

            // Connect operator drone
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ConnectOperator();
            }
        }

        /// <summary>
        /// Updates all text and elements for the active slide index.
        /// </summary>
        public void UpdateSlideUI()
        {
            if (headerText != null)
            {
                headerText.text = LocalizationManager.Get("TUT_Header");
            }

            if (placeholderLabelText != null)
            {
                placeholderLabelText.text = LocalizationManager.Get("TUT_Placeholder");
            }

            if (prevButtonText != null)
            {
                prevButtonText.text = LocalizationManager.Get("TUT_Prev");
            }

            if (nextButtonText != null)
            {
                nextButtonText.text = LocalizationManager.Get("TUT_Next");
            }

            if (startButtonText != null)
            {
                startButtonText.text = LocalizationManager.Get("TUT_Start");
            }

            if (pageIndicatorText != null)
            {
                pageIndicatorText.text = $"{_currentSlideIndex + 1} / {TotalSlides}";
            }

            // Localized slide title & description
            string titleKey = $"TUT_Slide{_currentSlideIndex + 1}_Title";
            string descKey = $"TUT_Slide{_currentSlideIndex + 1}_Desc";

            if (slideTitleText != null)
            {
                slideTitleText.text = LocalizationManager.Get(titleKey);
            }

            if (slideDescriptionText != null)
            {
                slideDescriptionText.text = LocalizationManager.Get(descKey);
            }

            // Illustration or placeholder
            Sprite currentSprite = null;
            if (slideIllustrations != null && _currentSlideIndex < slideIllustrations.Length)
            {
                currentSprite = slideIllustrations[_currentSlideIndex];
            }

            if (currentSprite != null)
            {
                if (slideImage != null)
                {
                    slideImage.sprite = currentSprite;
                    slideImage.color = Color.white;
                }
                if (placeholderContainer != null)
                {
                    placeholderContainer.SetActive(false);
                }
            }
            else
            {
                if (slideImage != null)
                {
                    slideImage.sprite = null;
                    slideImage.color = new Color(0.04f, 0.08f, 0.14f, 0.95f);
                }
                if (placeholderContainer != null)
                {
                    placeholderContainer.SetActive(true);
                }
            }

            // Button visibility states
            if (prevButton != null)
            {
                prevButton.gameObject.SetActive(_currentSlideIndex > 0);
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(_currentSlideIndex < TotalSlides - 1);
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(_currentSlideIndex == TotalSlides - 1);
            }
        }

        private void PlayClickSound()
        {
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayUiClick();
            }
        }

        private void PlayStartSound()
        {
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayUiStartGame();
            }
        }

        /// <summary>
        /// Programmatically creates or resolves the Tutorial Modal Window inside Canvas.
        /// </summary>
        public void EnsureTutorialHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            if (tutorialWindowPanel == null)
            {
                Transform existing = canvas.transform.Find("TutorialWindowPanel");
                if (existing != null)
                {
                    tutorialWindowPanel = existing.gameObject;
                }
            }

            if (tutorialWindowPanel == null)
            {
                // 1. Fullscreen Dimmer Backdrop (Blocks clicks from passing behind modal)
                GameObject modalRoot = new GameObject("TutorialWindowPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                modalRoot.transform.SetParent(canvas.transform, false);

                RectTransform mrRt = modalRoot.GetComponent<RectTransform>();
                mrRt.anchorMin = Vector2.zero;
                mrRt.anchorMax = Vector2.one;
                mrRt.sizeDelta = Vector2.zero;
                mrRt.anchoredPosition = Vector2.zero;

                Image mrImg = modalRoot.GetComponent<Image>();
                mrImg.color = new Color(0.02f, 0.04f, 0.08f, 0.88f);
                mrImg.raycastTarget = true; // Crucial: Intercepts all clicks

                tutorialCanvasGroup = modalRoot.GetComponent<CanvasGroup>();
                tutorialWindowPanel = modalRoot;

                // 2. Centered Window Box (720 x 620)
                GameObject boxGo = new GameObject("Box_Modal", typeof(RectTransform), typeof(Image), typeof(Outline));
                boxGo.transform.SetParent(modalRoot.transform, false);

                RectTransform boxRt = boxGo.GetComponent<RectTransform>();
                boxRt.anchorMin = new Vector2(0.5f, 0.5f);
                boxRt.anchorMax = new Vector2(0.5f, 0.5f);
                boxRt.pivot = new Vector2(0.5f, 0.5f);
                boxRt.anchoredPosition = Vector2.zero;
                boxRt.sizeDelta = new Vector2(720f, 620f);

                Image boxImg = boxGo.GetComponent<Image>();
                boxImg.color = new Color(0.05f, 0.08f, 0.15f, 0.98f);
                boxImg.raycastTarget = true;

                Outline boxOutline = boxGo.GetComponent<Outline>();
                boxOutline.effectColor = new Color(0.2f, 0.85f, 1f, 0.85f);
                boxOutline.effectDistance = new Vector2(2f, -2f);

                // 3. Top Header Title
                GameObject headerGo = new GameObject("Text_Header", typeof(RectTransform), typeof(TextMeshProUGUI));
                headerGo.transform.SetParent(boxGo.transform, false);
                RectTransform headerRt = headerGo.GetComponent<RectTransform>();
                headerRt.anchorMin = new Vector2(0f, 1f);
                headerRt.anchorMax = new Vector2(1f, 1f);
                headerRt.pivot = new Vector2(0.5f, 1f);
                headerRt.anchoredPosition = new Vector2(0f, -20f);
                headerRt.sizeDelta = new Vector2(0f, 38f);

                headerText = headerGo.GetComponent<TextMeshProUGUI>();
                headerText.text = "ANGULAR MOMENTUM";
                headerText.alignment = TextAlignmentOptions.Center;
                headerText.fontSize = 22;
                headerText.fontStyle = FontStyles.Bold;
                headerText.characterSpacing = 3f;
                headerText.color = new Color(0.3f, 0.95f, 1f);
                headerText.raycastTarget = false;

                // 4. Center Visual Frame (560 x 300)
                GameObject frameGo = new GameObject("SlideImageFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
                frameGo.transform.SetParent(boxGo.transform, false);
                RectTransform frameRt = frameGo.GetComponent<RectTransform>();
                frameRt.anchorMin = new Vector2(0.5f, 1f);
                frameRt.anchorMax = new Vector2(0.5f, 1f);
                frameRt.pivot = new Vector2(0.5f, 1f);
                frameRt.anchoredPosition = new Vector2(0f, -68f);
                frameRt.sizeDelta = new Vector2(560f, 300f);

                slideImage = frameGo.GetComponent<Image>();
                slideImage.color = new Color(0.04f, 0.08f, 0.14f, 0.95f);
                slideImage.raycastTarget = false;

                Outline frameOutline = frameGo.GetComponent<Outline>();
                frameOutline.effectColor = new Color(0.18f, 0.6f, 0.85f, 0.65f);
                frameOutline.effectDistance = new Vector2(1.5f, -1.5f);

                // Placeholder Container inside Visual Frame
                GameObject phGo = new GameObject("PlaceholderContainer", typeof(RectTransform));
                phGo.transform.SetParent(frameGo.transform, false);
                RectTransform phRt = phGo.GetComponent<RectTransform>();
                phRt.anchorMin = Vector2.zero;
                phRt.anchorMax = Vector2.one;
                phRt.sizeDelta = Vector2.zero;
                placeholderContainer = phGo;

                GameObject phLabelGo = new GameObject("Text_Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
                phLabelGo.transform.SetParent(phGo.transform, false);
                RectTransform phlRt = phLabelGo.GetComponent<RectTransform>();
                phlRt.anchorMin = Vector2.zero;
                phlRt.anchorMax = Vector2.one;
                phlRt.sizeDelta = Vector2.zero;

                placeholderLabelText = phLabelGo.GetComponent<TextMeshProUGUI>();
                placeholderLabelText.text = "МЕСТО ДЛЯ ИЛЛЮСТРАЦИИ / СКЕТЧБУКА";
                placeholderLabelText.alignment = TextAlignmentOptions.Center;
                placeholderLabelText.fontSize = 14;
                placeholderLabelText.fontStyle = FontStyles.Bold;
                placeholderLabelText.color = new Color(0.4f, 0.7f, 0.9f, 0.65f);
                placeholderLabelText.raycastTarget = false;

                // 5. Slide Text Area (Title & Description)
                GameObject slideTitleGo = new GameObject("Text_SlideTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                slideTitleGo.transform.SetParent(boxGo.transform, false);
                RectTransform stRt = slideTitleGo.GetComponent<RectTransform>();
                stRt.anchorMin = new Vector2(0.5f, 1f);
                stRt.anchorMax = new Vector2(0.5f, 1f);
                stRt.pivot = new Vector2(0.5f, 1f);
                stRt.anchoredPosition = new Vector2(0f, -384f);
                stRt.sizeDelta = new Vector2(580f, 32f);

                slideTitleText = slideTitleGo.GetComponent<TextMeshProUGUI>();
                slideTitleText.alignment = TextAlignmentOptions.Center;
                slideTitleText.fontSize = 18;
                slideTitleText.fontStyle = FontStyles.Bold;
                slideTitleText.color = new Color(1f, 0.9f, 0.35f);
                slideTitleText.raycastTarget = false;

                GameObject slideDescGo = new GameObject("Text_SlideDescription", typeof(RectTransform), typeof(TextMeshProUGUI));
                slideDescGo.transform.SetParent(boxGo.transform, false);
                RectTransform sdRt = slideDescGo.GetComponent<RectTransform>();
                sdRt.anchorMin = new Vector2(0.5f, 1f);
                sdRt.anchorMax = new Vector2(0.5f, 1f);
                sdRt.pivot = new Vector2(0.5f, 1f);
                sdRt.anchoredPosition = new Vector2(0f, -422f);
                sdRt.sizeDelta = new Vector2(580f, 95f);

                slideDescriptionText = slideDescGo.GetComponent<TextMeshProUGUI>();
                slideDescriptionText.alignment = TextAlignmentOptions.Top;
                slideDescriptionText.fontSize = 15;
                slideDescriptionText.textWrappingMode = TextWrappingModes.Normal;
                slideDescriptionText.color = new Color(0.92f, 0.96f, 1f);
                slideDescriptionText.raycastTarget = false;

                // 6. Bottom Navigation Bar
                GameObject navBarGo = new GameObject("NavBar", typeof(RectTransform));
                navBarGo.transform.SetParent(boxGo.transform, false);
                RectTransform navRt = navBarGo.GetComponent<RectTransform>();
                navRt.anchorMin = new Vector2(0.5f, 0f);
                navRt.anchorMax = new Vector2(0.5f, 0f);
                navRt.pivot = new Vector2(0.5f, 0f);
                navRt.anchoredPosition = new Vector2(0f, 24f);
                navRt.sizeDelta = new Vector2(600f, 48f);

                // Helper to build stylish retro buttons
                Button CreateRetroButton(string name, string label, Vector2 pos, Vector2 size, Color bgColor, Color outlineColor, out TextMeshProUGUI btnTxt)
                {
                    GameObject bGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
                    bGo.transform.SetParent(navBarGo.transform, false);

                    RectTransform bRt = bGo.GetComponent<RectTransform>();
                    bRt.anchorMin = new Vector2(0.5f, 0.5f);
                    bRt.anchorMax = new Vector2(0.5f, 0.5f);
                    bRt.pivot = new Vector2(0.5f, 0.5f);
                    bRt.anchoredPosition = pos;
                    bRt.sizeDelta = size;

                    Image bImg = bGo.GetComponent<Image>();
                    bImg.color = bgColor;
                    bImg.raycastTarget = true;

                    Outline bOut = bGo.GetComponent<Outline>();
                    bOut.effectColor = outlineColor;
                    bOut.effectDistance = new Vector2(1.5f, -1.5f);

                    var anim = bGo.GetComponent<UIButtonTweenAnimator>();
                    anim.SetOriginalScale(Vector3.one);

                    GameObject tGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                    tGo.transform.SetParent(bGo.transform, false);
                    RectTransform tRt = tGo.GetComponent<RectTransform>();
                    tRt.anchorMin = Vector2.zero;
                    tRt.anchorMax = Vector2.one;
                    tRt.sizeDelta = Vector2.zero;

                    btnTxt = tGo.GetComponent<TextMeshProUGUI>();
                    btnTxt.text = label;
                    btnTxt.alignment = TextAlignmentOptions.Center;
                    btnTxt.fontSize = 15;
                    btnTxt.fontStyle = FontStyles.Bold;
                    btnTxt.color = Color.white;
                    btnTxt.raycastTarget = false;

                    return bGo.GetComponent<Button>();
                }

                // Prev button (Left)
                prevButton = CreateRetroButton("Btn_Prev", "< НАЗАД", new Vector2(-190f, 0f), new Vector2(140f, 44f),
                    new Color(0.08f, 0.14f, 0.22f, 0.95f), new Color(0.2f, 0.85f, 1f, 0.75f), out prevButtonText);

                // Page Indicator (Center)
                GameObject piGo = new GameObject("Text_PageIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
                piGo.transform.SetParent(navBarGo.transform, false);
                RectTransform piRt = piGo.GetComponent<RectTransform>();
                piRt.anchorMin = new Vector2(0.5f, 0.5f);
                piRt.anchorMax = new Vector2(0.5f, 0.5f);
                piRt.pivot = new Vector2(0.5f, 0.5f);
                piRt.anchoredPosition = Vector2.zero;
                piRt.sizeDelta = new Vector2(120f, 44f);

                pageIndicatorText = piGo.GetComponent<TextMeshProUGUI>();
                pageIndicatorText.text = "1 / 4";
                pageIndicatorText.alignment = TextAlignmentOptions.Center;
                pageIndicatorText.fontSize = 17;
                pageIndicatorText.fontStyle = FontStyles.Bold;
                pageIndicatorText.color = new Color(0.3f, 0.9f, 1f);
                pageIndicatorText.raycastTarget = false;

                // Next button (Right)
                nextButton = CreateRetroButton("Btn_Next", "ДАЛЕЕ >", new Vector2(190f, 0f), new Vector2(140f, 44f),
                    new Color(0.08f, 0.14f, 0.22f, 0.95f), new Color(0.2f, 0.85f, 1f, 0.75f), out nextButtonText);

                // Start button (Right, replaces Next on slide 4) - green/gold accent
                startButton = CreateRetroButton("Btn_Start", "НАЧАТЬ ИГРУ", new Vector2(190f, 0f), new Vector2(180f, 44f),
                    new Color(0.08f, 0.32f, 0.18f, 0.95f), new Color(0.3f, 1f, 0.5f, 0.95f), out startButtonText);
                startButton.gameObject.SetActive(false);

                SetupButtonListeners();
            }
            else
            {
                // Resolve existing children if already created in scene
                ResolveExistingReferences();
            }
        }

        private void ResolveExistingReferences()
        {
            if (tutorialWindowPanel == null) return;

            if (tutorialCanvasGroup == null)
            {
                tutorialCanvasGroup = tutorialWindowPanel.GetComponent<CanvasGroup>();
            }

            Transform box = tutorialWindowPanel.transform.Find("Box_Modal");
            if (box != null)
            {
                if (headerText == null)
                {
                    Transform h = box.Find("Text_Header");
                    if (h != null) headerText = h.GetComponent<TextMeshProUGUI>();
                }

                Transform frame = box.Find("SlideImageFrame");
                if (frame != null)
                {
                    if (slideImage == null) slideImage = frame.GetComponent<Image>();
                    Transform ph = frame.Find("PlaceholderContainer");
                    if (ph != null)
                    {
                        placeholderContainer = ph.gameObject;
                        Transform pht = ph.Find("Text_Placeholder");
                        if (pht != null) placeholderLabelText = pht.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (slideTitleText == null)
                {
                    Transform st = box.Find("Text_SlideTitle");
                    if (st != null) slideTitleText = st.GetComponent<TextMeshProUGUI>();
                }

                if (slideDescriptionText == null)
                {
                    Transform sd = box.Find("Text_SlideDescription");
                    if (sd != null) slideDescriptionText = sd.GetComponent<TextMeshProUGUI>();
                }

                Transform nav = box.Find("NavBar");
                if (nav != null)
                {
                    if (prevButton == null)
                    {
                        Transform pb = nav.Find("Btn_Prev");
                        if (pb != null)
                        {
                            prevButton = pb.GetComponent<Button>();
                            prevButtonText = pb.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }

                    if (pageIndicatorText == null)
                    {
                        Transform pi = nav.Find("Text_PageIndicator");
                        if (pi != null) pageIndicatorText = pi.GetComponent<TextMeshProUGUI>();
                    }

                    if (nextButton == null)
                    {
                        Transform nb = nav.Find("Btn_Next");
                        if (nb != null)
                        {
                            nextButton = nb.GetComponent<Button>();
                            nextButtonText = nb.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }

                    if (startButton == null)
                    {
                        Transform sb = nav.Find("Btn_Start");
                        if (sb != null)
                        {
                            startButton = sb.GetComponent<Button>();
                            startButtonText = sb.GetComponentInChildren<TextMeshProUGUI>();
                        }
                    }
                }
            }
        }
    }
}
