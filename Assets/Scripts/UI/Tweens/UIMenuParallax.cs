using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem;

public class UIMenuParallax : MonoBehaviour
{
    [Header("Settings")]
    public float maxOffset = 20f; // maximum offset in pixels
    public float smoothTime = 0.3f; // tween duration

    private RectTransform rectTransform;
    private Vector3 originalPos;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPos = rectTransform.anchoredPosition;
    }

    void Update()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        // Get normalized mouse position relative to screen center (-1 to 1)
        float normX = (mousePos.x / Screen.width - 0.5f) * 2f;
        float normY = (mousePos.y  / Screen.height - 0.5f) * 2f;
        
        float maxRotation = 5f; // degrees
        Vector3 targetRot = new Vector3(normY * maxRotation, -normX * maxRotation, 0);
        rectTransform.DORotate(targetRot, smoothTime);

        // Invert to move opposite direction
        Vector3 targetPos = originalPos + new Vector3(-normX, -normY, 0) * maxOffset;

        // Tween to target position (DOTween handles smoothness)
        rectTransform.DOAnchorPos(targetPos, smoothTime);
    }
}
