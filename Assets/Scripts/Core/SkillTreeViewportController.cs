using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Controls smooth scroll-wheel zooming (toward cursor) and drag panning for the skill tree canvas.
    /// Operates fully in unscaled time (.SetUpdate(true)) so interaction works while game is paused (Time.timeScale == 0).
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillTreeViewportController : MonoBehaviour, IDragHandler, IBeginDragHandler, IScrollHandler
    {
        [Header("Containers")]
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform mapContainer;

        [Header("Zoom Settings")]
        [SerializeField] private float minZoom = 0.45f;
        [SerializeField] private float maxZoom = 2.2f;
        [SerializeField] private float zoomStep = 0.15f;
        [SerializeField] private float zoomTweenDuration = 0.12f;

        [Header("Pan Settings")]
        [SerializeField] private float maxPanDistanceX = 900f;
        [SerializeField] private float maxPanDistanceY = 700f;

        private float _currentZoom = 1.0f;
        private Vector2 _currentPosition = Vector2.zero;
        private Canvas _parentCanvas;

        public float CurrentZoom => _currentZoom;
        public RectTransform MapContainer => mapContainer;

        public void Initialize(RectTransform viewport, RectTransform map)
        {
            viewportRect = viewport;
            mapContainer = map;
            _parentCanvas = GetComponentInParent<Canvas>();
            _currentZoom = 1.0f;
            _currentPosition = Vector2.zero;
        }

        private void Awake()
        {
            if (viewportRect == null) viewportRect = GetComponent<RectTransform>();
            _parentCanvas = GetComponentInParent<Canvas>();
        }

        private void Update()
        {
            if (!UpgradeTreeManager.IsTreeOpen) return;

            // Space key resets view
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                ResetView();
                return;
            }

            // Fallback scroll handling from Mouse device
            if (Mouse.current != null)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.05f)
                {
                    Vector2 mousePos = Mouse.current.position.ReadValue();
                    HandleZoom(scrollY, mousePos);
                }
            }
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!UpgradeTreeManager.IsTreeOpen) return;
            HandleZoom(eventData.scrollDelta.y, eventData.position);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (mapContainer != null)
            {
                mapContainer.DOKill();
                _currentPosition = mapContainer.anchoredPosition;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!UpgradeTreeManager.IsTreeOpen || mapContainer == null) return;

            float scaleFactor = _parentCanvas != null ? _parentCanvas.scaleFactor : 1.0f;
            if (scaleFactor <= 0.001f) scaleFactor = 1.0f;

            _currentPosition += eventData.delta / scaleFactor;
            _currentPosition = ClampPosition(_currentPosition, _currentZoom);
            mapContainer.anchoredPosition = _currentPosition;
        }

        public void HandleZoom(float scrollDelta, Vector2 mouseScreenPos)
        {
            if (Mathf.Abs(scrollDelta) < 0.001f || mapContainer == null || viewportRect == null) return;

            float zoomDelta = (scrollDelta > 0f ? 1f : -1f) * zoomStep;
            float newZoom = Mathf.Clamp(_currentZoom + zoomDelta, minZoom, maxZoom);
            if (Mathf.Approximately(newZoom, _currentZoom)) return;

            // Compute local mouse point in viewport
            Camera eventCamera = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _parentCanvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewportRect, mouseScreenPos, eventCamera, out Vector2 mouseLocalInViewport);

            // Compute new position to zoom toward mouse cursor
            Vector2 currentMapPos = mapContainer.anchoredPosition;
            float scaleRatio = newZoom / _currentZoom;
            Vector2 newMapPos = mouseLocalInViewport - (mouseLocalInViewport - currentMapPos) * scaleRatio;

            _currentZoom = newZoom;
            _currentPosition = ClampPosition(newMapPos, _currentZoom);

            mapContainer.DOKill();
            mapContainer.DOScale(Vector3.one * _currentZoom, zoomTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            mapContainer.DOAnchorPos(_currentPosition, zoomTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        /// <summary>
        /// Smoothly centers and resets zoom back to 1.0f in unscaled time.
        /// </summary>
        public void ResetView()
        {
            if (mapContainer == null) return;

            _currentZoom = 1.0f;
            _currentPosition = Vector2.zero;

            mapContainer.DOKill();
            mapContainer.DOScale(Vector3.one, 0.25f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);

            mapContainer.DOAnchorPos(Vector2.zero, 0.25f)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        private Vector2 ClampPosition(Vector2 pos, float zoom)
        {
            float limitX = maxPanDistanceX * Mathf.Max(1.0f, zoom);
            float limitY = maxPanDistanceY * Mathf.Max(1.0f, zoom);
            return new Vector2(
                Mathf.Clamp(pos.x, -limitX, limitX),
                Mathf.Clamp(pos.y, -limitY, limitY)
            );
        }
    }
}
