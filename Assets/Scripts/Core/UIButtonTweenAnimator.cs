using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Reusable procedural juice animator for buttons and UI elements using DOTween.
    /// Operates in unscaled time (.SetUpdate(true)) so animations remain snappy during pause / TAB menu.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonTweenAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Hover Settings")]
        [SerializeField] private float hoverScaleMultiplier = 1.08f;
        [SerializeField] private float hoverDuration = 0.16f;
        [SerializeField] private float exitDuration = 0.14f;
        [SerializeField] private bool enableGlow = true;

        [Header("Click Settings")]
        [SerializeField] private float clickPunchScale = 0.15f;
        [SerializeField] private float clickPunchDuration = 0.22f;
        [SerializeField] private int clickPunchVibrato = 8;
        [SerializeField] private float clickPunchElasticity = 0.8f;

        [Header("Upgrade / Feedback Colors")]
        [SerializeField] private Color successFlashColor = new Color(0.2f, 1f, 0.4f, 1f); // Bright green
        [SerializeField] private Color failureFlashColor = new Color(1f, 0.2f, 0.2f, 1f); // Red

        private RectTransform _rectTransform;
        private Button _button;
        private Image _image;
        private Outline _outline;

        private Vector3 _originalScale = Vector3.one;
        private Color _originalImageColor;
        private Color _originalOutlineColor;
        private bool _isInitialized = false;
        private Tweener _scaleTween;
        private Tweener _colorTween;
        private Tweener _outlineTween;

        public Vector3 OriginalScale => _originalScale;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
            // Reset to base scale on enable
            transform.localScale = _originalScale;
        }

        private void OnDisable()
        {
            KillAllTweens();
            if (_isInitialized)
            {
                transform.localScale = _originalScale;
                RestoreOriginalColors();
            }
        }

        private void OnDestroy()
        {
            KillAllTweens();
        }

        private void InitializeIfNeeded()
        {
            if (_isInitialized) return;

            _rectTransform = GetComponent<RectTransform>();
            _button = GetComponent<Button>();
            _image = GetComponent<Image>();
            _outline = GetComponent<Outline>();

            _originalScale = transform.localScale;
            if (_originalScale == Vector3.zero)
            {
                _originalScale = Vector3.one;
            }

            if (_image != null)
            {
                _originalImageColor = _image.color;
            }
            if (_outline != null)
            {
                _originalOutlineColor = _outline.effectColor;
            }

            _isInitialized = true;
        }

        public void SetOriginalScale(Vector3 scale)
        {
            _originalScale = scale;
        }

        public void RefreshBaseColors()
        {
            if (_image != null) _originalImageColor = _image.color;
            if (_outline != null) _originalOutlineColor = _outline.effectColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale * hoverScaleMultiplier, hoverDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

            if (enableGlow && _outline != null)
            {
                _outlineTween?.Kill();
                Color glow = Color.Lerp(_originalOutlineColor, Color.white, 0.45f);
                _outlineTween = _outline.DOColor(glow, hoverDuration).SetUpdate(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale, exitDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            if (enableGlow && _outline != null)
            {
                _outlineTween?.Kill();
                _outlineTween = _outline.DOColor(_originalOutlineColor, exitDuration).SetUpdate(true);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!IsInteractable()) return;

            transform.DOPunchScale(-Vector3.one * clickPunchScale, clickPunchDuration, clickPunchVibrato, clickPunchElasticity)
                .SetUpdate(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // Handled naturally by DOPunchScale elasticity
        }

        /// <summary>
        /// Triggered when an affordable upgrade node is purchased.
        /// Punches scale, flashes bright green/gold, and signals success.
        /// </summary>
        public void PlayPurchaseSuccessAnimation()
        {
            InitializeIfNeeded();
            KillAllTweens();

            // 1. Scale punch
            transform.localScale = _originalScale;
            transform.DOPunchScale(Vector3.one * 0.25f, 0.32f, 10, 0.8f).SetUpdate(true);

            // 2. Flash outline or background
            if (_outline != null)
            {
                Color targetGreen = successFlashColor;
                _outlineTween = _outline.DOColor(targetGreen, 0.1f)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _outlineTween = _outline.DOColor(_originalOutlineColor, 0.35f).SetUpdate(true);
                    });
            }
            else if (_image != null)
            {
                Color prev = _image.color;
                _colorTween = _image.DOColor(successFlashColor, 0.1f)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _colorTween = _image.DOColor(prev, 0.35f).SetUpdate(true);
                    });
            }
        }

        /// <summary>
        /// Triggered when user attempts to purchase a node without sufficient coins.
        /// Horizontally shakes the button and flashes red.
        /// </summary>
        public void PlayInsufficientCoinsAnimation()
        {
            InitializeIfNeeded();
            if (_rectTransform == null) return;

            // 1. Shake anchor position horizontally
            _rectTransform.DOComplete();
            _rectTransform.DOShakeAnchorPos(0.25f, new Vector2(12f, 0f), 20, 90f, false, true)
                .SetUpdate(true);

            // 2. Flash red
            if (_outline != null)
            {
                _outlineTween?.Kill();
                Color current = _outline.effectColor;
                _outlineTween = _outline.DOColor(failureFlashColor, 0.08f)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _outlineTween = _outline.DOColor(current, 0.25f).SetUpdate(true);
                    });
            }
            else if (_image != null)
            {
                _colorTween?.Kill();
                Color prev = _image.color;
                _colorTween = _image.DOColor(failureFlashColor, 0.08f)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        _colorTween = _image.DOColor(prev, 0.25f).SetUpdate(true);
                    });
            }
        }

        private bool IsInteractable()
        {
            return _button == null || _button.interactable;
        }

        private void KillAllTweens()
        {
            _scaleTween?.Kill();
            _colorTween?.Kill();
            _outlineTween?.Kill();
            if (_rectTransform != null) _rectTransform.DOKill();
        }

        private void RestoreOriginalColors()
        {
            if (_image != null) _image.color = _originalImageColor;
            if (_outline != null) _outline.effectColor = _originalOutlineColor;
        }
    }
}
