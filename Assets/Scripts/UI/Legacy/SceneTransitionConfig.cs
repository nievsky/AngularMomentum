using UnityEngine;

[CreateAssetMenu(fileName = "SceneTransitionConfig", menuName = "Scene Transitions/Transition Config")]
public class SceneTransitionConfig : ScriptableObject
{
    [Header("Visual")]
    [SerializeField] private Color _overlayColor = Color.black;
    [SerializeField] private Sprite _overlaySprite;
    [SerializeField] private bool _preserveSpriteAspect = false;
    [SerializeField] private Material _transitionMaterial;
    [SerializeField] private string _materialProgressProperty = "_Progress";

    [Header("Timing")]
    [SerializeField] private float _coverDuration = 0.45f;
    [SerializeField] private float _revealDuration = 0.45f;
    [SerializeField] private float _minimumCoveredDuration = 0.15f;
    [SerializeField] private bool _startLoadingDuringCoverAnimation = false;
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("Animation Curves")]
    [SerializeField] private AnimationCurve _coverCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve _revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Canvas")]
    [SerializeField] private int _sortingOrder = 32760;

    public Color OverlayColor => _overlayColor;
    public Sprite OverlaySprite => _overlaySprite;
    public bool PreserveSpriteAspect => _preserveSpriteAspect;
    public Material TransitionMaterial => _transitionMaterial;
    public string MaterialProgressProperty => _materialProgressProperty;
    public float CoverDuration => Mathf.Max(0f, _coverDuration);
    public float RevealDuration => Mathf.Max(0f, _revealDuration);
    public float MinimumCoveredDuration => Mathf.Max(0f, _minimumCoveredDuration);
    public bool StartLoadingDuringCoverAnimation => _startLoadingDuringCoverAnimation;
    public bool UseUnscaledTime => _useUnscaledTime;
    public AnimationCurve CoverCurve => _coverCurve;
    public AnimationCurve RevealCurve => _revealCurve;
    public int SortingOrder => _sortingOrder;
}
