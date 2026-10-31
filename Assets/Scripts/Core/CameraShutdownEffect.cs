using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class CameraShutdownEffect : MonoBehaviour
    {
        public static CameraShutdownEffect Instance { get; private set; }

        [Header("UI Overlay References")]
        [SerializeField] private GameObject shutdownOverlayRoot;
        [SerializeField] private Image blackoutCurtain;
        [SerializeField] private Image staticBurstImage;
        [SerializeField] private RectTransform crtCollapseLine;
        [SerializeField] private Image crtCollapseLineImage;

        private static Sprite _cachedStaticSprite;

        public bool IsShutdownActive => shutdownOverlayRoot != null && shutdownOverlayRoot.activeSelf;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureHierarchy();
            ResetToDefaultState();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ResetToDefaultState()
        {
            if (crtCollapseLine != null)
            {
                crtCollapseLine.DOKill();
                crtCollapseLine.localScale = Vector3.one;
                crtCollapseLine.gameObject.SetActive(false);
            }
            if (crtCollapseLineImage != null)
            {
                crtCollapseLineImage.DOKill();
                crtCollapseLineImage.color = new Color(0.92f, 0.98f, 1f, 1f);
            }
            if (staticBurstImage != null)
            {
                staticBurstImage.DOKill();
                staticBurstImage.gameObject.SetActive(false);
            }
            if (blackoutCurtain != null)
            {
                blackoutCurtain.gameObject.SetActive(false);
                blackoutCurtain.color = Color.black;
            }
            if (shutdownOverlayRoot != null)
            {
                shutdownOverlayRoot.SetActive(false);
            }
        }

        public void EnsureHierarchy()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            if (shutdownOverlayRoot == null)
            {
                Transform existing = canvas.transform.Find("CameraShutdownOverlay");
                if (existing != null) shutdownOverlayRoot = existing.gameObject;
            }

            if (shutdownOverlayRoot == null)
            {
                shutdownOverlayRoot = new GameObject("CameraShutdownOverlay", typeof(RectTransform));
                shutdownOverlayRoot.transform.SetParent(canvas.transform, false);

                RectTransform rootRt = shutdownOverlayRoot.GetComponent<RectTransform>();
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            shutdownOverlayRoot.transform.SetAsLastSibling();

            // 1. Blackout Curtain (Fullscreen pure black background)
            if (blackoutCurtain == null)
            {
                Transform bTr = shutdownOverlayRoot.transform.Find("BlackoutCurtain");
                if (bTr != null) blackoutCurtain = bTr.GetComponent<Image>();
            }
            if (blackoutCurtain == null)
            {
                GameObject boGo = new GameObject("BlackoutCurtain", typeof(RectTransform), typeof(Image));
                boGo.transform.SetParent(shutdownOverlayRoot.transform, false);

                RectTransform boRt = boGo.GetComponent<RectTransform>();
                boRt.anchorMin = Vector2.zero;
                boRt.anchorMax = Vector2.one;
                boRt.offsetMin = Vector2.zero;
                boRt.offsetMax = Vector2.zero;

                blackoutCurtain = boGo.GetComponent<Image>();
                blackoutCurtain.color = Color.black;
                blackoutCurtain.raycastTarget = false;
                boGo.SetActive(false);
            }

            // 2. Static Burst Image (Fullscreen high-contrast white noise overlay)
            if (staticBurstImage == null)
            {
                Transform sTr = shutdownOverlayRoot.transform.Find("StaticBurstImage");
                if (sTr != null) staticBurstImage = sTr.GetComponent<Image>();
            }
            if (staticBurstImage == null)
            {
                GameObject sGo = new GameObject("StaticBurstImage", typeof(RectTransform), typeof(Image));
                sGo.transform.SetParent(shutdownOverlayRoot.transform, false);

                RectTransform sRt = sGo.GetComponent<RectTransform>();
                sRt.anchorMin = Vector2.zero;
                sRt.anchorMax = Vector2.one;
                sRt.offsetMin = new Vector2(-24f, -24f);
                sRt.offsetMax = new Vector2(24f, 24f);

                staticBurstImage = sGo.GetComponent<Image>();
                staticBurstImage.color = Color.white;
                staticBurstImage.raycastTarget = false;
                staticBurstImage.sprite = GetOrCreateStaticNoiseSprite();
                sGo.SetActive(false);
            }
            else if (staticBurstImage.sprite == null)
            {
                staticBurstImage.sprite = GetOrCreateStaticNoiseSprite();
            }

            // 3. CRT Collapse Line (Full width glowing bar centered on screen)
            if (crtCollapseLine == null)
            {
                Transform lTr = shutdownOverlayRoot.transform.Find("CRTCollapseLine");
                if (lTr != null)
                {
                    crtCollapseLine = lTr.GetComponent<RectTransform>();
                    crtCollapseLineImage = lTr.GetComponent<Image>();
                }
            }
            if (crtCollapseLine == null)
            {
                GameObject lGo = new GameObject("CRTCollapseLine", typeof(RectTransform), typeof(Image), typeof(Outline));
                lGo.transform.SetParent(shutdownOverlayRoot.transform, false);

                crtCollapseLine = lGo.GetComponent<RectTransform>();
                crtCollapseLine.anchorMin = new Vector2(0f, 0.5f);
                crtCollapseLine.anchorMax = new Vector2(1f, 0.5f);
                crtCollapseLine.pivot = new Vector2(0.5f, 0.5f);
                crtCollapseLine.anchoredPosition = Vector2.zero;
                crtCollapseLine.sizeDelta = new Vector2(0f, 1080f);

                crtCollapseLineImage = lGo.GetComponent<Image>();
                crtCollapseLineImage.color = new Color(0.92f, 0.98f, 1f, 1f);
                crtCollapseLineImage.raycastTarget = false;

                Outline outline = lGo.GetComponent<Outline>();
                outline.effectColor = new Color(0.2f, 0.85f, 1f, 0.9f);
                outline.effectDistance = new Vector2(0f, 2f);

                lGo.SetActive(false);
            }
        }

        public void PlayCRTShutdownSequence(Action onComplete = null)
        {
            StopAllCoroutines();
            StartCoroutine(PlayCRTShutdownSequenceRoutine(onComplete));
        }

        public IEnumerator PlayCRTShutdownSequenceRoutine(Action onComplete = null)
        {
            EnsureHierarchy();

            if (shutdownOverlayRoot != null)
            {
                shutdownOverlayRoot.SetActive(true);
                shutdownOverlayRoot.transform.SetAsLastSibling();
            }

            // 1. Flash StaticBurstImage at 100% intensity for 0.18s (heavy camera signal death)
            if (staticBurstImage != null)
            {
                staticBurstImage.gameObject.SetActive(true);
                staticBurstImage.color = Color.white;
            }

            float staticElapsed = 0f;
            Vector2 origPos = staticBurstImage != null ? staticBurstImage.rectTransform.anchoredPosition : Vector2.zero;

            while (staticElapsed < 0.18f)
            {
                staticElapsed += Time.unscaledDeltaTime;
                if (staticBurstImage != null)
                {
                    staticBurstImage.rectTransform.anchoredPosition = origPos + new Vector2(UnityEngine.Random.Range(-18f, 18f), UnityEngine.Random.Range(-18f, 18f));
                }
                yield return null;
            }

            if (staticBurstImage != null)
            {
                staticBurstImage.rectTransform.anchoredPosition = origPos;
                staticBurstImage.gameObject.SetActive(false);
            }

            // 2. Enable solid BlackoutCurtain so behind is pitch black, rapidly squash CRTCollapseLine vertically
            if (blackoutCurtain != null)
            {
                blackoutCurtain.gameObject.SetActive(true);
                blackoutCurtain.color = Color.black;
            }

            if (crtCollapseLine != null)
            {
                crtCollapseLine.gameObject.SetActive(true);
                crtCollapseLine.localScale = Vector3.one;

                if (crtCollapseLineImage != null)
                {
                    crtCollapseLineImage.color = new Color(0.92f, 0.98f, 1f, 1f);
                }

                // Rapidly squash vertically into razor-thin line across the middle of the screen
                crtCollapseLine.DOScaleY(0.008f, 0.16f)
                    .SetEase(Ease.InExpo)
                    .SetUpdate(true);
            }

            yield return new WaitForSecondsRealtime(0.16f);

            // 3. Collapse the horizontal line's width into a single bright center dot and fade to black
            if (crtCollapseLine != null)
            {
                crtCollapseLine.DOScaleX(0.0f, 0.18f)
                    .SetEase(Ease.InQuad)
                    .SetUpdate(true);

                if (crtCollapseLineImage != null)
                {
                    crtCollapseLineImage.DOFade(0.0f, 0.18f)
                        .SetEase(Ease.InQuad)
                        .SetUpdate(true);
                }
            }

            yield return new WaitForSecondsRealtime(0.18f);

            if (crtCollapseLine != null)
            {
                crtCollapseLine.gameObject.SetActive(false);
            }

            onComplete?.Invoke();
        }

        private static Sprite GetOrCreateStaticNoiseSprite()
        {
            if (_cachedStaticSprite != null) return _cachedStaticSprite;

            int width = 256;
            int height = 256;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Repeat;

            Color[] cols = new Color[width * height];
            for (int i = 0; i < cols.Length; i++)
            {
                float val = UnityEngine.Random.value > 0.45f ? UnityEngine.Random.Range(0.75f, 1f) : UnityEngine.Random.Range(0f, 0.2f);
                cols[i] = new Color(val, val, val, 1f);
            }
            tex.SetPixels(cols);
            tex.Apply();

            _cachedStaticSprite = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
            return _cachedStaticSprite;
        }
    }
}
