using UnityEngine;
using UnityEngine.UI;

public class SceneTransitionView : MonoBehaviour
{
    private const float HiddenProgress = 0f;
    private const float CoveredProgress = 1f;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private Image _image;
    private RectTransform _imageRectTransform;
    private Material _runtimeMaterial;
    private SceneTransitionConfig _config;
    private static Texture2D _generatedNoiseTexture;

    public void Initialize()
    {
        if (_canvas != null)
        {
            return;
        }

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        gameObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("Transition Overlay");
        imageObject.transform.SetParent(transform, false);

        _image = imageObject.AddComponent<Image>();
        _image.raycastTarget = true;

        _imageRectTransform = imageObject.GetComponent<RectTransform>();
        _imageRectTransform.anchorMin = Vector2.zero;
        _imageRectTransform.anchorMax = Vector2.one;
        _imageRectTransform.offsetMin = Vector2.zero;
        _imageRectTransform.offsetMax = Vector2.zero;

        SetInputBlocked(false);
        SetProgress(HiddenProgress);
        gameObject.SetActive(false);
    }

    public void Prepare(SceneTransitionConfig config)
    {
        Initialize();

        _config = config;
        _canvas.sortingOrder = config.SortingOrder;
        _image.sprite = config.OverlaySprite;
        _image.preserveAspect = config.PreserveSpriteAspect;
        _image.color = config.OverlayColor;

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
            _runtimeMaterial = null;
        }

        if (config.TransitionMaterial != null)
        {
            _runtimeMaterial = new Material(config.TransitionMaterial);

            if (_runtimeMaterial.HasProperty("_Color"))
            {
                _runtimeMaterial.SetColor("_Color", Color.white);
            }

            if (_runtimeMaterial.HasProperty("_NoiseTex") && _runtimeMaterial.GetTexture("_NoiseTex") == null)
            {
                _runtimeMaterial.SetTexture("_NoiseTex", GetGeneratedNoiseTexture());
            }
        }

        _image.material = _runtimeMaterial;
        gameObject.SetActive(true);
        SetInputBlocked(true);
        SetProgress(HiddenProgress);
    }

    public void Hide()
    {
        SetProgress(HiddenProgress);
        SetInputBlocked(false);
        gameObject.SetActive(false);
    }

    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        if (_image == null)
        {
            return;
        }

        if (_runtimeMaterial != null && _config != null && !string.IsNullOrEmpty(_config.MaterialProgressProperty))
        {
            _canvasGroup.alpha = 1f;

            if (_runtimeMaterial.HasProperty(_config.MaterialProgressProperty))
            {
                _runtimeMaterial.SetFloat(_config.MaterialProgressProperty, progress);
            }

            return;
        }

        _canvasGroup.alpha = progress;
    }

    public void SetCovered()
    {
        SetProgress(CoveredProgress);
    }

    private void SetInputBlocked(bool blocked)
    {
        if (_canvasGroup == null)
        {
            return;
        }

        _canvasGroup.interactable = blocked;
        _canvasGroup.blocksRaycasts = blocked;
    }

    private static Texture2D GetGeneratedNoiseTexture()
    {
        if (_generatedNoiseTexture != null)
        {
            return _generatedNoiseTexture;
        }

        const int size = 128;
        _generatedNoiseTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Generated Transition Noise",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;
                float value01 = 0f;
                float amplitude = 0.5f;
                float totalAmplitude = 0f;

                for (int octave = 0; octave < 4; octave++)
                {
                    float frequency = 2f * Mathf.Pow(2f, octave);
                    value01 += Mathf.PerlinNoise((u * frequency) + 11.17f, (v * frequency) + 37.29f) * amplitude;
                    totalAmplitude += amplitude;
                    amplitude *= 0.5f;
                }

                value01 /= totalAmplitude;
                byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(value01) * 255f);
                pixels[y * size + x] = new Color32(value, value, value, 255);
            }
        }

        _generatedNoiseTexture.SetPixels32(pixels);
        _generatedNoiseTexture.Apply(false, true);
        return _generatedNoiseTexture;
    }
}
