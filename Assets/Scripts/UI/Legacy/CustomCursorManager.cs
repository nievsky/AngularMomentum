using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-1000)]
public class CustomCursorManager : MonoBehaviour
{
    private const int CursorCanvasSortingOrder = 32700;

    private enum CursorState
    {
        Default,
        Interactable
    }

    public static CustomCursorManager Instance { get; private set; }

    [Header("Cursor Sprites")]
    [SerializeField] private Sprite _defaultCursor;
    [SerializeField] private Sprite _interactableCursor;
    [SerializeField] private Vector2 _defaultHotspot = Vector2.zero;
    [SerializeField] private Vector2 _interactableHotspot = Vector2.zero;
    [SerializeField] private float _cursorScale = 1f;

    [Header("UI Hover")]
    [SerializeField] private LayerMask _uiInteractableLayers;
    [SerializeField] private bool _onlyInteractiveUiElements = true;

    [Header("World Hover")]
    [SerializeField] private LayerMask _worldInteractableLayers;
    [SerializeField] private Camera _worldRaycastCamera;
    [SerializeField] private float _worldRaycastDistance = 1000f;
    [SerializeField] private bool _checkPhysics3D = true;
    [SerializeField] private bool _checkPhysics2D = false;
    [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Cursor Visibility")]
    [SerializeField] private bool _forceVisibleAndUnlocked = true;

    private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>();
    private EventSystem _cachedEventSystem;
    private PointerEventData _pointerEventData;
    private CursorState? _currentState;
    private Canvas _cursorCanvas;
    private Image _cursorImage;
    private RectTransform _cursorRectTransform;

    private void Reset()
    {
        _uiInteractableLayers = LayerMask.GetMask("UI");
        _worldInteractableLayers = LayerMask.GetMask("BoardLayer", "CellLayer");
        AssignProjectCursorSpriteDefaults();
    }

    private void OnValidate()
    {
        _cursorScale = Mathf.Max(0.01f, _cursorScale);
        AssignProjectCursorSpriteDefaults();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            DestroyDuplicateCursorComponents();
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _cursorScale = Mathf.Max(0.01f, _cursorScale);
        EnsureDefaultLayerMasks();
        CreateSoftwareCursor();
        ApplyCursor(CursorState.Default, true);
    }

    private void DestroyDuplicateCursorComponents()
    {
        enabled = false;

        if (_cursorCanvas != null)
        {
            Destroy(_cursorCanvas.gameObject);
        }

        Destroy(this);
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            if (_cursorCanvas != null)
            {
                _cursorCanvas.enabled = true;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            ApplyCursor(CursorState.Default, true);
        }
    }

    private void OnDisable()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_cursorCanvas != null)
        {
            _cursorCanvas.enabled = false;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        _currentState = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LateUpdate()
    {
        Cursor.visible = false;

        if (_forceVisibleAndUnlocked)
        {
            Cursor.lockState = CursorLockMode.None;
        }

        bool isHoveringInteractable = IsPointerOverInteractableUi() || IsPointerOverInteractableWorldObject();
        ApplyCursor(isHoveringInteractable ? CursorState.Interactable : CursorState.Default);
        UpdateSoftwareCursorPosition();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _pointerEventData = null;
        EnsureDefaultLayerMasks();
        CreateSoftwareCursor();
        ApplyCursor(CursorState.Default, true);
    }

    private void EnsureDefaultLayerMasks()
    {
        if (_uiInteractableLayers.value == 0)
        {
            _uiInteractableLayers = LayerMask.GetMask("UI");
        }

        if (_worldInteractableLayers.value == 0)
        {
            _worldInteractableLayers = LayerMask.GetMask("BoardLayer", "CellLayer");
        }
    }

    private bool IsPointerOverInteractableUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return false;
        }

        if (_pointerEventData == null || _cachedEventSystem != eventSystem)
        {
            _cachedEventSystem = eventSystem;
            _pointerEventData = new PointerEventData(eventSystem);
        }

        _pointerEventData.position = Input.mousePosition;
        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(_pointerEventData, _uiRaycastResults);

        for (int i = 0; i < _uiRaycastResults.Count; i++)
        {
            GameObject hitObject = _uiRaycastResults[i].gameObject;

            if (hitObject == null || !IsLayerInMask(hitObject.layer, _uiInteractableLayers))
            {
                continue;
            }

            if (!_onlyInteractiveUiElements || IsInteractiveUiElement(hitObject))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInteractiveUiElement(GameObject hitObject)
    {
        Selectable selectable = hitObject.GetComponentInParent<Selectable>();
        if (selectable != null && selectable.IsInteractable())
        {
            return true;
        }

        return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hitObject) != null
            || ExecuteEvents.GetEventHandler<IPointerDownHandler>(hitObject) != null
            || ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hitObject) != null;
    }

    private bool IsPointerOverInteractableWorldObject()
    {
        if (_worldInteractableLayers.value == 0)
        {
            return false;
        }

        Camera raycastCamera = _worldRaycastCamera != null ? _worldRaycastCamera : Camera.main;
        if (raycastCamera == null)
        {
            return false;
        }

        Ray ray = raycastCamera.ScreenPointToRay(Input.mousePosition);

        if (_checkPhysics3D && Physics.Raycast(ray, _worldRaycastDistance, _worldInteractableLayers, _triggerInteraction))
        {
            return true;
        }

        if (_checkPhysics2D)
        {
            RaycastHit2D hit = Physics2D.GetRayIntersection(ray, _worldRaycastDistance, _worldInteractableLayers);
            return hit.collider != null;
        }

        return false;
    }

    private void ApplyCursor(CursorState state, bool force = false)
    {
        CreateSoftwareCursor();

        if (!force && _currentState == state)
        {
            return;
        }

        _currentState = state;

        Sprite sprite = state == CursorState.Interactable && _interactableCursor != null
            ? _interactableCursor
            : _defaultCursor;

        _cursorImage.sprite = sprite;
        _cursorImage.enabled = sprite != null;

        if (sprite != null)
        {
            Vector2 spriteSize = sprite.rect.size * _cursorScale;
            _cursorRectTransform.sizeDelta = spriteSize;
        }
    }

    private void CreateSoftwareCursor()
    {
        if (_cursorImage != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Custom Cursor Canvas");
        canvasObject.transform.SetParent(transform, false);
        _cursorCanvas = canvasObject.AddComponent<Canvas>();
        _cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _cursorCanvas.sortingOrder = CursorCanvasSortingOrder;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

        GameObject cursorObject = new GameObject("Cursor");
        cursorObject.transform.SetParent(canvasObject.transform, false);
        _cursorImage = cursorObject.AddComponent<Image>();
        _cursorImage.raycastTarget = false;
        _cursorImage.preserveAspect = true;

        _cursorRectTransform = cursorObject.GetComponent<RectTransform>();
        _cursorRectTransform.anchorMin = Vector2.zero;
        _cursorRectTransform.anchorMax = Vector2.zero;
        _cursorRectTransform.pivot = Vector2.up;
    }

    private void UpdateSoftwareCursorPosition()
    {
        if (_cursorRectTransform == null)
        {
            return;
        }

        Vector2 hotspot = _currentState == CursorState.Interactable ? _interactableHotspot : _defaultHotspot;
        _cursorRectTransform.anchoredPosition = (Vector2)Input.mousePosition - (hotspot * _cursorScale);
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void AssignProjectCursorSpriteDefaults()
    {
#if UNITY_EDITOR
        if (_defaultCursor == null)
        {
            _defaultCursor = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cursors/gauntlet_default.png");
        }

        if (_interactableCursor == null)
        {
            _interactableCursor = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Cursors/gauntlet_point.png");
        }
#endif
    }
}
