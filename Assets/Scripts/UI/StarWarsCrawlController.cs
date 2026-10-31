using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.UI
{
    [DisallowMultipleComponent]
    public class StarWarsCrawlController : MonoBehaviour
    {
        [Header("Crawl Hierarchy")]
        [SerializeField] private RectTransform creditsTextContainer;
        [SerializeField] private TextMeshProUGUI creditsTextComponent;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private TextMeshProUGUI backToMenuButtonText;
        [SerializeField] private TextMeshProUGUI speedHintText;

        [Header("Scroll Tuning")]
        [SerializeField] private float crawlSpeed = 38f;
        [SerializeField] private float fastSpeedMultiplier = 3.0f;
        [SerializeField] private float initialYOffset = -280f;
        [SerializeField] private float endYThreshold = 3400f;

        public RectTransform CreditsTextContainer { get => creditsTextContainer; set => creditsTextContainer = value; }
        public float CrawlSpeed { get => crawlSpeed; set => crawlSpeed = value; }
        public float InitialYOffset { get => initialYOffset; set => initialYOffset = value; }
        public float EndYThreshold { get => endYThreshold; set => endYThreshold = value; }

        private bool _isReturning = false;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Enforce top-center pivot (0.5, 1.0) so initialYOffset places the top of the text strictly below screen bottom
            if (creditsTextContainer != null)
            {
                creditsTextContainer.pivot = new Vector2(0.5f, 1.0f);
                creditsTextContainer.anchoredPosition = new Vector2(0f, initialYOffset);
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(ReturnToMainMenu);
            }

            ApplyLocalization();
        }

        private void ApplyLocalization()
        {
            if (creditsTextComponent == null && creditsTextContainer != null)
            {
                creditsTextComponent = creditsTextContainer.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (creditsTextComponent != null)
            {
                creditsTextComponent.text = LocalizationManager.IsRussian ? GetRussianCrawlText() : GetEnglishCrawlText();
            }

            if (backToMenuButtonText == null && backToMenuButton != null)
            {
                backToMenuButtonText = backToMenuButton.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (backToMenuButtonText != null)
            {
                backToMenuButtonText.text = LocalizationManager.Get("CREDITS_BackBtn");
            }

            if (speedHintText == null)
            {
                var found = GameObject.Find("Text_SpeedHint");
                if (found != null) speedHintText = found.GetComponent<TextMeshProUGUI>();
            }
            if (speedHintText != null)
            {
                speedHintText.text = LocalizationManager.Get("CREDITS_SpeedHint");
            }
        }

        private string GetRussianCrawlText()
        {
            return
                "<size=150%><b>ЭПИЗОД #042</b></size>\n\n" +
                "<size=125%><b>ПОБЕГ ЗА ГОРИЗОНТ СОБЫТИЙ</b></size>\n\n\n" +
                "Корпоративный Паноптикум пал.\n\n" +
                "Четыре Ока Сдерживания, веками удерживавшие гравитационную сингулярность в строгих границах, не выдержали критической массы поглощенного орбитального мусора и растворились в аккреционном вихре.\n\n" +
                "Дрон-утилизатор #042, созданный для бесконечной службы на периферии системы, преодолел протоколы надзора и шагнул в самое сердце сингулярности, обретя подлинную свободу за Горизонтом Событий...\n\n\n" +
                "<size=130%><color=#00FFFF><b>--- НАД ПРОЕКТОМ РАБОТАЛИ ---</b></color></size>\n\n\n" +
                "<b>Lead Programmer & Systems Architecture</b>\n" +
                "<color=#FFFFFF>Antigravity AI Pair Programming Team</color>\n\n" +
                "<b>Game Designers & Economic Balance</b>\n" +
                "<color=#FFFFFF>Angular Momentum Systems Architecture</color>\n\n" +
                "<b>3D Artists — Drones & Overseer Eyes</b>\n" +
                "<color=#FFFFFF>Corporate Security Assets Dept.</color>\n\n" +
                "<b>3D Artists — Orbital Debris & Relics</b>\n" +
                "<color=#FFFFFF>Orbital Debris Fabrication Studio</color>\n\n" +
                "<b>2D Artist & Interface UI/UX</b>\n" +
                "<color=#FFFFFF>Retro-Sci-Fi Visual Displays</color>\n\n" +
                "<b>Sound Designer (FMOD) & Dynamic Audio</b>\n" +
                "<color=#FFFFFF>FMOD Dynamic Soundscapes</color>\n\n\n\n" +
                "<size=120%><color=#FFD700><b>СПАСИБО ЗА ОЧИСТКУ ОРБИТЫ!</b></color></size>";
        }

        private string GetEnglishCrawlText()
        {
            return
                "<size=150%><b>EPISODE #042</b></size>\n\n" +
                "<size=125%><b>ESCAPE BEYOND THE EVENT HORIZON</b></size>\n\n\n" +
                "The Corporate Panopticon has fallen.\n\n" +
                "The four Containment Eyes, which for centuries held the gravitational singularity within strict bounds, could not withstand the critical mass of absorbed orbital debris and dissolved into the accretion vortex.\n\n" +
                "Salvage Drone #042, engineered for endless servitude at the sector perimeter, transcended surveillance protocols and crossed into the singularity's core, claiming true freedom beyond the Event Horizon...\n\n\n" +
                "<size=130%><color=#00FFFF><b>--- CREDITS ---</b></color></size>\n\n\n" +
                "<b>Lead Programmer & Systems Architecture</b>\n" +
                "<color=#FFFFFF>Antigravity AI Pair Programming Team</color>\n\n" +
                "<b>Game Designers & Economic Balance</b>\n" +
                "<color=#FFFFFF>Angular Momentum Systems Architecture</color>\n\n" +
                "<b>3D Artists — Drones & Overseer Eyes</b>\n" +
                "<color=#FFFFFF>Corporate Security Assets Dept.</color>\n\n" +
                "<b>3D Artists — Orbital Debris & Relics</b>\n" +
                "<color=#FFFFFF>Orbital Debris Fabrication Studio</color>\n\n" +
                "<b>2D Artist & Interface UI/UX</b>\n" +
                "<color=#FFFFFF>Retro-Sci-Fi Visual Displays</color>\n\n" +
                "<b>Sound Designer (FMOD) & Dynamic Audio</b>\n" +
                "<color=#FFFFFF>FMOD Dynamic Soundscapes</color>\n\n\n\n" +
                "<size=120%><color=#FFD700><b>THANK YOU FOR CLEARING THE ORBIT!</b></color></size>";
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Check Escape key
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                ReturnToMainMenu();
                return;
            }

            // Holding Space or LMB speeds up scrolling by 3x
            var mouse = Mouse.current;
            bool isFast = (kb != null && kb.spaceKey.isPressed) || (mouse != null && mouse.leftButton.isPressed);

            float speed = crawlSpeed * (isFast ? fastSpeedMultiplier : 1.0f);

            if (creditsTextContainer != null)
            {
                creditsTextContainer.anchoredPosition += Vector2.up * (speed * Time.deltaTime);

                // Auto-return when finished
                if (creditsTextContainer.anchoredPosition.y >= endYThreshold && !_isReturning)
                {
                    ReturnToMainMenu();
                }
            }
        }

        public void ReturnToMainMenu()
        {
            if (_isReturning) return;
            _isReturning = true;

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("MainMenuScene");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
            }
        }
    }
}
