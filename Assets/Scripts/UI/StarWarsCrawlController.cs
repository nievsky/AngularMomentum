using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace OrbitalPanopticon.UI
{
    [DisallowMultipleComponent]
    public class StarWarsCrawlController : MonoBehaviour
    {
        [Header("Crawl Hierarchy")]
        [SerializeField] private RectTransform creditsTextContainer;
        [SerializeField] private Button backToMenuButton;

        [Header("Scroll Tuning")]
        [SerializeField] private float crawlSpeed = 36f;
        [SerializeField] private float fastSpeedMultiplier = 3.0f;
        [SerializeField] private float initialYOffset = -420f;
        [SerializeField] private float endYThreshold = 2200f;

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

            if (creditsTextContainer != null)
            {
                creditsTextContainer.anchoredPosition = new Vector2(0f, initialYOffset);
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
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