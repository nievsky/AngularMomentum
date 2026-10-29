using UnityEngine;
using UnityEngine.EventSystems;
using DG.Tweening;

public class UIButtonTween : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private RectTransform rectTransform;
    private Vector3 originalScale;

    [Header("Animation Settings")]
    public float hoverScale = 1.1f;
    public float animationDuration = 0.2f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
    }

    private void Start()
    {
        rectTransform.DOScale(originalScale * 1.02f, 1f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        rectTransform.DOKill();

        rectTransform.DOScale(originalScale * hoverScale, animationDuration)
            .SetEase(Ease.OutBack);

        // AudioManager.PlayUIHoverSound();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // AudioManager.PlayUIClickSound();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        rectTransform.DOKill();

        rectTransform.DOScale(originalScale, animationDuration)
            .SetEase(Ease.OutBack);
    }
}