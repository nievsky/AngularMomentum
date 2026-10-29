using UnityEngine;
using DG.Tweening; // Наш любимый DOTween

public class IntroScreenUI : MonoBehaviour
{
    [Header("UI Панели")]
    [SerializeField] private CanvasGroup _introPanel;     // Панель с картинкой и кнопкой "Далее"
    [SerializeField] private CanvasGroup _mainMenuPanel;  // Панель с уровнями и рангом

    [Header("Настройки анимации")]
    [SerializeField] private float _fadeDuration = 0.5f;

    private void Start()
    {
        // 1. При старте сцены гарантируем, что интро включено, а меню выключено
        
        _introPanel.gameObject.SetActive(true);
        _introPanel.alpha = 1f;
        _introPanel.interactable = true;   // Разрешаем кликать кнопку "Далее"
        _introPanel.blocksRaycasts = true;

        _mainMenuPanel.gameObject.SetActive(false);
        _mainMenuPanel.alpha = 0f;
        _mainMenuPanel.interactable = false;
        _mainMenuPanel.blocksRaycasts = false;
    }

    // Этот метод мы повесим на кнопку "Далее"
    public void OnNextButtonClicked()
    {
        // Сразу блокируем клики по интро, чтобы игрок не прокликал дважды
        _introPanel.interactable = false;
        _introPanel.blocksRaycasts = false;

        // Плавно растворяем интро...
        _introPanel.DOFade(0f, _fadeDuration).OnComplete(() => 
        {
            // ...а когда оно исчезло, полностью выключаем объект
            _introPanel.gameObject.SetActive(false);
            
            // Включаем главное меню и плавно проявляем его
            _mainMenuPanel.gameObject.SetActive(true);
            _mainMenuPanel.DOFade(1f, _fadeDuration).OnComplete(() => 
            {
                // Разрешаем кликать по уровням
                _mainMenuPanel.interactable = true;
                _mainMenuPanel.blocksRaycasts = true;
            });
        });
    }
}