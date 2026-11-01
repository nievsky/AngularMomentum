using UnityEngine;
using TMPro;
using DG.Tweening;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class WorldScorePopup : MonoBehaviour
    {
        [SerializeField] private TextMeshPro textMesh;
        [SerializeField] private float duration = 1.6f;
        [SerializeField] private float floatDistance = 3.8f;

        private Color _baseColor = Color.white;
        private Vector3 _floatDirection = Vector3.up;
        private Sequence _tweenSequence;

        private void Awake()
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMeshPro>();
                if (textMesh == null)
                {
                    textMesh = gameObject.AddComponent<TextMeshPro>();
                }
            }

            var mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 500;
            }

            textMesh.alignment = TextAlignmentOptions.Center;
            textMesh.fontSize = 7.0f;
            textMesh.fontStyle = FontStyles.Bold;

            ApplyOverlayFontMaterial();
        }

        private void ApplyOverlayFontMaterial()
        {
            TMP_FontAsset fontAsset = Resources.Load<TMP_FontAsset>("MasterCyrillicFont_SDF");
#if UNITY_EDITOR
            if (fontAsset == null)
            {
                fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/MasterCyrillicFont_SDF.asset");
            }
#endif

            if (fontAsset != null)
            {
                textMesh.font = fontAsset;

                Material overlayMat = Resources.Load<Material>("M_ScorePopupTextOverlay");
#if UNITY_EDITOR
                if (overlayMat == null)
                {
                    overlayMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_ScorePopupTextOverlay.mat");
                }
#endif
                if (overlayMat != null)
                {
                    textMesh.fontSharedMaterial = overlayMat;
                }
                else
                {
                    Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay") ?? Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
                    if (overlayShader != null && fontAsset.material != null)
                    {
                        Material runtimeMat = new Material(fontAsset.material);
                        runtimeMat.shader = overlayShader;
                        runtimeMat.renderQueue = 4000;
                        textMesh.fontSharedMaterial = runtimeMat;
                    }
                }
            }
        }

        public void Setup(float score, bool isKamikaze, bool isObserved, Vector3 radialDir)
        {
            if (textMesh == null) Awake();

            int pts = Mathf.RoundToInt(score);

            if (isKamikaze)
            {
                textMesh.text = $"+{pts}\n<size=60%><color=#00FFFF>KAMIKAZE x2.5!</color></size>";
                _baseColor = new Color(0.2f, 1f, 0.95f, 1f);
            }
            else if (isObserved)
            {
                textMesh.text = $"+{pts}\n<size=60%><color=#FFD700>OBSERVED x3!</color></size>";
                _baseColor = new Color(1f, 0.88f, 0.15f, 1f);
            }
            else
            {
                textMesh.text = $"+{pts}";
                _baseColor = new Color(1.0f, 0.92f, 0.35f, 1f); // Bright golden-white for maximum contrast against black hole
            }

            textMesh.color = _baseColor;

            // Float outward from black hole center and upward
            _floatDirection = (Vector3.up * 1.35f + radialDir * 0.9f).normalized;

            _tweenSequence?.Kill();
            _tweenSequence = DOTween.Sequence();

            // 1. Snappy DOTween scale pop: start at 0, punch up to 1.35x, settle at 1.0x
            transform.localScale = Vector3.zero;
            _tweenSequence.Append(transform.DOScale(Vector3.one * 1.35f, 0.18f).SetEase(Ease.OutBack, 2.5f));
            _tweenSequence.Append(transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.InOutQuad));

            // 2. Smoothly float outward and upward
            Vector3 targetPos = transform.position + _floatDirection * floatDistance;
            transform.DOMove(targetPos, duration).SetEase(Ease.OutQuad);

            // 3. Alpha fadeout over the last portion of the duration
            float fadeStartTime = duration * 0.55f;
            float fadeDuration = duration - fadeStartTime;
            _tweenSequence.Insert(fadeStartTime, DOTween.To(
                () => textMesh.color.a,
                a => {
                    if (textMesh != null)
                    {
                        Color c = _baseColor;
                        c.a = a;
                        textMesh.color = c;
                    }
                },
                0f,
                fadeDuration
            ).SetEase(Ease.InQuad));

            _tweenSequence.OnComplete(() => {
                if (gameObject != null) Destroy(gameObject);
            });
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                transform.rotation = cam.transform.rotation;
            }
        }

        private void OnDestroy()
        {
            _tweenSequence?.Kill();
        }
    }
}
