using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Multi-slide tutorial modal window displayed at the start of GameplayScene.
    /// Manages slides, pauses simulation (Time.timeScale = 0), unlocks cursor,
    /// blocks background inputs, and connects the player operator once completed.
    /// Uses Mayak font and orange-black-white aesthetic matching user specifications.
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialSlideshowUI : MonoBehaviour
    {
        public static TutorialSlideshowUI Instance { get; private set; }
        public static bool IsTutorialOpen { get; set; } = false;

        [Header("Root Panel")]
        [SerializeField] private GameObject tutorialWindowPanel;
        [SerializeField] private CanvasGroup tutorialCanvasGroup;

        [Header("Visual Frame & Slides")]
        [SerializeField] private Image slideImage;
        [SerializeField] private Sprite[] slideIllustrations = new Sprite[4];
        [SerializeField] private Sprite[] slideIllustrationsRU = new Sprite[4];
        [SerializeField] private Sprite[] slideIllustrationsEN = new Sprite[4];

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset tutorialFont;

        [Header("Navigation Bar")]
        [SerializeField] private GameObject navBarPanel;
        [SerializeField] private Button prevButton;
        [SerializeField] private TextMeshProUGUI prevButtonText;
        [SerializeField] private TextMeshProUGUI pageIndicatorText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI nextButtonText;
        [SerializeField] private Button startButton;
        [SerializeField] private TextMeshProUGUI startButtonText;

        // Legacy / Obsolete references kept for compatibility so scene references don't throw warnings
        [SerializeField, HideInInspector] private GameObject placeholderContainer;
        [SerializeField, HideInInspector] private TextMeshProUGUI headerText;
        [SerializeField, HideInInspector] private TextMeshProUGUI placeholderLabelText;
        [SerializeField, HideInInspector] private TextMeshProUGUI slideTitleText;
        [SerializeField, HideInInspector] private TextMeshProUGUI slideDescriptionText;

        private int _currentSlideIndex = 0;
        private const int TotalSlides = 4;

        // Orange - Black - White Color Palette constants
        private static readonly Color ColorBoxBg = new Color(0.08f, 0.07f, 0.07f, 0.96f);       // #141212 Dark Charcoal
        private static readonly Color ColorStartBg = new Color(0.12f, 0.09f, 0.04f, 0.98f);     // #1F170A Dark Amber
        private static readonly Color ColorOrangeAccent = new Color(1f, 0.73f, 0f, 1f);          // #FFBA00 Warm Orange
        private static readonly Color ColorTextWhite = Color.white;                              // #FFFFFF Pure White
        private static readonly Color ColorBackdropDim = new Color(0.02f, 0.02f, 0.04f, 0.82f);  // Translucent Dimmer

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

                HandleKeyboardNavigation();
            }
        }

        private void HandleKeyboardNavigation()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // Advance to next slide / start game: Space, Enter, Right Arrow, D
            if (keyboard.spaceKey.wasPressedThisFrame ||
                keyboard.enterKey.wasPressedThisFrame ||
                keyboard.numpadEnterKey.wasPressedThisFrame ||
                keyboard.rightArrowKey.wasPressedThisFrame ||
                keyboard.dKey.wasPressedThisFrame)
            {
                if (_currentSlideIndex < TotalSlides - 1)
                {
                    NextSlide();
                }
                else
                {
                    StartGameFromTutorial();
                }
            }
            // Go back to previous slide: Left Arrow, A, Backspace
            else if (keyboard.leftArrowKey.wasPressedThisFrame ||
                     keyboard.aKey.wasPressedThisFrame ||
                     keyboard.backspaceKey.wasPressedThisFrame)
            {
                if (_currentSlideIndex > 0)
                {
                    PreviousSlide();
                }
            }
            // Escape also completes and starts game
            else if (keyboard.escapeKey.wasPressedThisFrame)
            {
                StartGameFromTutorial();
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
            // Legacy text elements disabled to keep new slide artwork clean
            if (headerText != null) headerText.gameObject.SetActive(false);
            if (placeholderContainer != null) placeholderContainer.SetActive(false);
            if (slideTitleText != null) slideTitleText.gameObject.SetActive(false);
            if (slideDescriptionText != null) slideDescriptionText.gameObject.SetActive(false);

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

            EnsureSpritesLoaded();

            // Set current slide illustration based on active language
            bool isRU = LocalizationManager.IsRussian;
            Sprite[] activeSlides = isRU ? slideIllustrationsRU : slideIllustrationsEN;
            if (activeSlides == null || activeSlides.Length < TotalSlides || activeSlides[_currentSlideIndex] == null)
            {
                activeSlides = slideIllustrations;
            }

            Sprite currentSprite = null;
            if (activeSlides != null && _currentSlideIndex < activeSlides.Length)
            {
                currentSprite = activeSlides[_currentSlideIndex];
            }

            if (slideImage != null)
            {
                if (currentSprite != null)
                {
                    slideImage.sprite = currentSprite;
                    slideImage.color = Color.white;
                    slideImage.gameObject.SetActive(true);
                }
                else
                {
                    slideImage.gameObject.SetActive(false);
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
        /// Configure references from Editor setup script.
        /// </summary>
        public void ConfigureNewTutorial(Sprite[] sprites, TMP_FontAsset font)
        {
            ConfigureNewTutorial(sprites, null, null, font);
        }

        public void ConfigureNewTutorial(Sprite[] sprites, Sprite[] spritesRU, Sprite[] spritesEN, TMP_FontAsset font)
        {
            slideIllustrations = sprites;
            if (spritesRU != null) slideIllustrationsRU = spritesRU;
            if (spritesEN != null) slideIllustrationsEN = spritesEN;
            tutorialFont = font;
            BuildOrRebuildHierarchy();
            SetupButtonListeners();
            UpdateSlideUI();
        }

        private void EnsureSpritesLoaded()
        {
            if (slideIllustrations == null || slideIllustrations.Length < 4 || slideIllustrations[0] == null)
            {
                slideIllustrations = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    slideIllustrations[i] = Resources.Load<Sprite>($"Tutorial/Tutorial_Slide_{i + 1}");
#if UNITY_EDITOR
                    if (slideIllustrations[i] == null)
                    {
                        slideIllustrations[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Textures/UI/Tutorial/Tutorial_Slide_{i + 1}.png");
                    }
#endif
                }
            }

            if (slideIllustrationsRU == null || slideIllustrationsRU.Length < 4 || slideIllustrationsRU[0] == null)
            {
                slideIllustrationsRU = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    slideIllustrationsRU[i] = Resources.Load<Sprite>($"Tutorial/Tutorial_Slide_{i + 1}_RU");
#if UNITY_EDITOR
                    if (slideIllustrationsRU[i] == null)
                    {
                        slideIllustrationsRU[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Textures/UI/Tutorial/Tutorial_Slide_{i + 1}_RU.png");
                    }
#endif
                }
            }

            if (slideIllustrationsEN == null || slideIllustrationsEN.Length < 4 || slideIllustrationsEN[0] == null)
            {
                slideIllustrationsEN = new Sprite[4];
                for (int i = 0; i < 4; i++)
                {
                    slideIllustrationsEN[i] = Resources.Load<Sprite>($"Tutorial/Tutorial_Slide_{i + 1}_EN");
#if UNITY_EDITOR
                    if (slideIllustrationsEN[i] == null)
                    {
                        slideIllustrationsEN[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Textures/UI/Tutorial/Tutorial_Slide_{i + 1}_EN.png");
                    }
#endif
                }
            }
        }

        /// <summary>
        /// Programmatically creates or rebuilds the Tutorial Modal Window inside Canvas.
        /// </summary>
        public void EnsureTutorialHierarchy()
        {
            EnsureSpritesLoaded();

            if (tutorialWindowPanel != null && slideImage != null && prevButton != null && nextButton != null && startButton != null)
            {
                // Already properly connected
                return;
            }

            BuildOrRebuildHierarchy();
        }

        public void BuildOrRebuildHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            // Load font if needed
            if (tutorialFont == null)
            {
                tutorialFont = Resources.Load<TMP_FontAsset>("Mayak SDF");
#if UNITY_EDITOR
                if (tutorialFont == null)
                {
                    tutorialFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/Mayak SDF.asset");
                }
#endif
                if (tutorialFont == null)
                {
                    var fontCfg = Resources.Load<GlobalFontConfigSO>("GlobalFontConfig");
                    if (fontCfg != null && fontCfg.masterTMPFont != null)
                    {
                        tutorialFont = fontCfg.masterTMPFont;
                    }
                }
            }

            // Ensure all slide sprites (Default, RU, EN) are loaded
            EnsureSpritesLoaded();

            // 1. Root TutorialWindowPanel (Fullscreen Dimmer Backdrop)
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
                GameObject rootGo = new GameObject("TutorialWindowPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
                rootGo.transform.SetParent(canvas.transform, false);
                tutorialWindowPanel = rootGo;
            }

            RectTransform rootRt = tutorialWindowPanel.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.sizeDelta = Vector2.zero;
            rootRt.anchoredPosition = Vector2.zero;

            Image rootImg = tutorialWindowPanel.GetComponent<Image>();
            rootImg.color = ColorBackdropDim;
            rootImg.raycastTarget = true; // Blocks all clicks through the modal

            tutorialCanvasGroup = tutorialWindowPanel.GetComponent<CanvasGroup>();
            if (tutorialCanvasGroup == null)
            {
                tutorialCanvasGroup = tutorialWindowPanel.AddComponent<CanvasGroup>();
            }

            // Clean up obsolete Box_Modal if present
            Transform oldBox = tutorialWindowPanel.transform.Find("Box_Modal");
            if (oldBox != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    UnityEditor.Undo.DestroyObjectImmediate(oldBox.gameObject);
                }
                else
                {
                    Destroy(oldBox.gameObject);
                }
#else
                Destroy(oldBox.gameObject);
#endif
            }

            // 2. Slide Image (Full 1920x1080 display matching designer's coordinates)
            Transform slideImgT = tutorialWindowPanel.transform.Find("Slide_Image");
            GameObject slideImgGo = slideImgT != null ? slideImgT.gameObject : new GameObject("Slide_Image", typeof(RectTransform), typeof(Image));
            slideImgGo.transform.SetParent(tutorialWindowPanel.transform, false);

            RectTransform siRt = slideImgGo.GetComponent<RectTransform>();
            siRt.anchorMin = new Vector2(0.5f, 0.5f);
            siRt.anchorMax = new Vector2(0.5f, 0.5f);
            siRt.pivot = new Vector2(0.5f, 0.5f);
            siRt.anchoredPosition = Vector2.zero;
            siRt.sizeDelta = new Vector2(1920f, 1080f);

            slideImage = slideImgGo.GetComponent<Image>();
            slideImage.preserveAspect = true;
            slideImage.raycastTarget = false;
            if (slideIllustrations != null && slideIllustrations.Length > 0 && slideIllustrations[0] != null)
            {
                slideImage.sprite = slideIllustrations[0];
                slideImage.color = Color.white;
            }

            // 3. Navigation Bar (Positioned below the 1380px card)
            Transform navBarT = tutorialWindowPanel.transform.Find("NavBar");
            GameObject navGo = navBarT != null ? navBarT.gameObject : new GameObject("NavBar", typeof(RectTransform));
            navGo.transform.SetParent(tutorialWindowPanel.transform, false);
            navBarPanel = navGo;

            RectTransform nbRt = navGo.GetComponent<RectTransform>();
            nbRt.anchorMin = new Vector2(0.5f, 0.5f);
            nbRt.anchorMax = new Vector2(0.5f, 0.5f);
            nbRt.pivot = new Vector2(0.5f, 0.5f);
            // Centered vertically in the 115px space below the 1380x213 card (y = -482)
            nbRt.anchoredPosition = new Vector2(0f, -482f);
            nbRt.sizeDelta = new Vector2(1380f, 52f);

            // Button creation helper
            Button CreateMayakButton(string name, string label, Vector2 anchorMinMax, Vector2 pivot, Vector2 pos, Vector2 size,
                Color bgColor, Color outlineColor, Color textColor, float fontSize, out TextMeshProUGUI btnTxt)
            {
                Transform bT = navGo.transform.Find(name);
                GameObject bGo = bT != null ? bT.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(UIButtonTweenAnimator));
                bGo.transform.SetParent(navGo.transform, false);

                RectTransform bRt = bGo.GetComponent<RectTransform>();
                bRt.anchorMin = anchorMinMax;
                bRt.anchorMax = anchorMinMax;
                bRt.pivot = pivot;
                bRt.anchoredPosition = pos;
                bRt.sizeDelta = size;

                Image bImg = bGo.GetComponent<Image>();
                bImg.color = bgColor;
                bImg.raycastTarget = true;

                Outline bOut = bGo.GetComponent<Outline>();
                bOut.effectColor = outlineColor;
                bOut.effectDistance = new Vector2(2f, -2f);

                UIButtonTweenAnimator anim = bGo.GetComponent<UIButtonTweenAnimator>();
                if (anim == null) anim = bGo.AddComponent<UIButtonTweenAnimator>();
                anim.SetOriginalScale(Vector3.one);

                Transform tT = bGo.transform.Find("Text");
                GameObject tGo = tT != null ? tT.gameObject : new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                tGo.transform.SetParent(bGo.transform, false);

                RectTransform tRt = tGo.GetComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.sizeDelta = Vector2.zero;
                tRt.anchoredPosition = Vector2.zero;

                btnTxt = tGo.GetComponent<TextMeshProUGUI>();
                if (tutorialFont != null) btnTxt.font = tutorialFont;
                btnTxt.text = label;
                btnTxt.alignment = TextAlignmentOptions.Center;
                btnTxt.fontSize = fontSize;
                btnTxt.fontStyle = FontStyles.Bold;
                btnTxt.color = textColor;
                btnTxt.raycastTarget = false;

                return bGo.GetComponent<Button>();
            }

            // Left Button: < НАЗАД / < BACK
            prevButton = CreateMayakButton("Btn_Prev", "< НАЗАД",
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(175f, 46f),
                ColorBoxBg, ColorOrangeAccent, ColorTextWhite, 16f, out prevButtonText);

            // Center Text: Page Indicator (1 / 4)
            Transform piT = navGo.transform.Find("Text_PageIndicator");
            GameObject piGo = piT != null ? piT.gameObject : new GameObject("Text_PageIndicator", typeof(RectTransform), typeof(TextMeshProUGUI));
            piGo.transform.SetParent(navGo.transform, false);

            RectTransform piRt = piGo.GetComponent<RectTransform>();
            piRt.anchorMin = new Vector2(0.5f, 0.5f);
            piRt.anchorMax = new Vector2(0.5f, 0.5f);
            piRt.pivot = new Vector2(0.5f, 0.5f);
            piRt.anchoredPosition = Vector2.zero;
            piRt.sizeDelta = new Vector2(160f, 46f);

            pageIndicatorText = piGo.GetComponent<TextMeshProUGUI>();
            if (tutorialFont != null) pageIndicatorText.font = tutorialFont;
            pageIndicatorText.text = "1 / 4";
            pageIndicatorText.alignment = TextAlignmentOptions.Center;
            pageIndicatorText.fontSize = 20f;
            pageIndicatorText.fontStyle = FontStyles.Bold;
            pageIndicatorText.color = ColorOrangeAccent;
            pageIndicatorText.raycastTarget = false;

            // Right Button 1: ДАЛЕЕ > / NEXT >
            nextButton = CreateMayakButton("Btn_Next", "ДАЛЕЕ >",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(175f, 46f),
                ColorBoxBg, ColorOrangeAccent, ColorTextWhite, 16f, out nextButtonText);

            // Right Button 2: НАЧАТЬ ИГРУ / START GAME (replaces Next on last slide)
            startButton = CreateMayakButton("Btn_Start", "НАЧАТЬ ИГРУ",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(235f, 46f),
                ColorStartBg, ColorOrangeAccent, ColorOrangeAccent, 17f, out startButtonText);
            startButton.gameObject.SetActive(false);

            SetupButtonListeners();
            UpdateSlideUI();
        }
    }
}
