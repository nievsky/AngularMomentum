using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class NarrativeCommsUI : MonoBehaviour
    {
        public static NarrativeCommsUI Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private NarrativeDialogueConfigSO dialogueConfig;

        [Header("UI Hierarchy References")]
        [SerializeField] private GameObject commsBoxContainer;
        [SerializeField] private RectTransform commsBoxRect;
        [SerializeField] private CanvasGroup commsCanvasGroup;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image portraitFrame;
        [SerializeField] private Outline boxOutline;
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;

        [Header("Audio (Star Fox / Radio Chatter)")]
        [SerializeField] private AudioClip radioOpenSfx;
        [SerializeField] private AudioClip radioBeepSfx;
        [SerializeField] private AudioClip radioCloseSfx;
        [SerializeField] private float beepAudioPitchJitter = 0.15f;

        private Coroutine _transmissionCoroutine;
        private AudioSource _audioSource;
        private Vector2 _hiddenPos = new Vector2(0f, 95f);
        private Vector2 _visiblePos = new Vector2(0f, -85f);
        private bool _isTransmissionActive = false;

        public bool IsTransmissionActive => _isTransmissionActive;
        public NarrativeDialogueConfigSO Config => dialogueConfig;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.playOnAwake = false;
                _audioSource.spatialBlend = 0f; // 2D HUD Audio
            }

            if (dialogueConfig == null)
            {
#if UNITY_EDITOR
                dialogueConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<NarrativeDialogueConfigSO>("Assets/_Project/ScriptableObjects/NarrativeDialogueConfig.asset");
#endif
            }

            EnsureUIHierarchy();
        }

        private void Start()
        {
            HookStageEvents();

            // Play Stage 1 Start Transmission after brief 1.2s delay
            StartCoroutine(DelayedStartTransmissionRoutine());
        }

        private void OnDestroy()
        {
            UnhookStageEvents();
            if (Instance == this) Instance = null;
        }

        private void HookStageEvents()
        {
            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.OnStageAdvanced += HandleStageAdvanced;
            }
        }

        private void UnhookStageEvents()
        {
            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.OnStageAdvanced -= HandleStageAdvanced;
            }
        }

        private IEnumerator DelayedStartTransmissionRoutine()
        {
            yield return new WaitForSeconds(1.2f);
            if (dialogueConfig != null)
            {
                PlayTransmission(dialogueConfig.stage1Start);
            }
        }

        private void HandleStageAdvanced(int newStage)
        {
            if (dialogueConfig == null) return;

            switch (newStage)
            {
                case 2:
                    PlayTransmission(dialogueConfig.stage2FirstBeamOffline);
                    break;
                case 3:
                    PlayTransmission(dialogueConfig.stage3SecondBeamOffline);
                    break;
                case 4:
                    PlayTransmission(dialogueConfig.stage4ThirdBeamOffline);
                    break;
            }
        }

        public void PlayFinaleTransmission()
        {
            if (dialogueConfig != null)
            {
                PlayTransmission(dialogueConfig.finaleAllBeamsOffline);
            }
        }

        public void PlayTransmission(DialogueEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.dialogueTextRU)) return;

            if (_transmissionCoroutine != null)
            {
                StopCoroutine(_transmissionCoroutine);
            }

            _transmissionCoroutine = StartCoroutine(TransmissionRoutine(entry));
        }

        private IEnumerator TransmissionRoutine(DialogueEntry entry)
        {
            _isTransmissionActive = true;
            EnsureUIHierarchy();

            if (commsBoxContainer != null)
            {
                commsBoxContainer.SetActive(true);
            }

            // Apply Theme Color
            Color theme = entry.themeColor;
            if (boxOutline != null) boxOutline.effectColor = theme;
            if (speakerNameText != null)
            {
                speakerNameText.text = entry.speakerName;
                speakerNameText.color = theme;
            }

            if (portraitImage != null)
            {
                if (entry.speakerPortraitPlaceholder != null)
                {
                    portraitImage.sprite = entry.speakerPortraitPlaceholder;
                    portraitImage.color = Color.white;
                }
                else
                {
                    portraitImage.color = new Color(theme.r * 0.85f, theme.g * 0.85f, theme.b * 0.85f, 0.9f);
                }
            }

            if (dialogueText != null)
            {
                dialogueText.text = "";
            }

            // Play Radio Open Chime
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayCommsOpenBeep();
            }
            else
            {
                PlayRadioSound(radioOpenSfx, 0.8f);
            }

            // 1. Slide In Tween (0.28s)
            float slideElapsed = 0f;
            const float slideDuration = 0.28f;
            while (slideElapsed < slideDuration)
            {
                slideElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, slideElapsed / slideDuration);

                if (commsBoxRect != null)
                {
                    commsBoxRect.anchoredPosition = Vector2.Lerp(_hiddenPos, _visiblePos, t);
                }
                if (commsCanvasGroup != null)
                {
                    commsCanvasGroup.alpha = t;
                }
                yield return null;
            }

            if (commsBoxRect != null) commsBoxRect.anchoredPosition = _visiblePos;
            if (commsCanvasGroup != null) commsCanvasGroup.alpha = 1f;

            // 2. Typewriter Effect
            string fullText = entry.dialogueTextRU;
            float charDelay = Mathf.Max(0.01f, entry.typewriterCharDelay);
            int charIndex = 0;
            float soundThrottleTimer = 0f;

            while (charIndex <= fullText.Length)
            {
                if (dialogueText != null)
                {
                    dialogueText.text = fullText.Substring(0, charIndex);
                }

                // Radio Chatter audio blips
                soundThrottleTimer += charDelay;
                if (soundThrottleTimer >= 0.06f && charIndex < fullText.Length)
                {
                    soundThrottleTimer = 0f;
                    char c = fullText[charIndex];
                    if (c != ' ' && c != '.' && c != ',' && c != '!' && c != '?')
                    {
                        PlayTypewriterChirp();
                    }
                }

                charIndex++;
                yield return new WaitForSecondsRealtime(charDelay);
            }

            // 3. Display Hold Duration
            yield return new WaitForSecondsRealtime(entry.displayDuration);

            // Play Radio Close Chime
            PlayRadioSound(radioCloseSfx, 0.6f);

            // 4. Slide Out Tween (0.25s)
            slideElapsed = 0f;
            while (slideElapsed < 0.25f)
            {
                slideElapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, slideElapsed / 0.25f);

                if (commsBoxRect != null)
                {
                    commsBoxRect.anchoredPosition = Vector2.Lerp(_visiblePos, _hiddenPos, t);
                }
                if (commsCanvasGroup != null)
                {
                    commsCanvasGroup.alpha = 1f - t;
                }
                yield return null;
            }

            if (commsBoxContainer != null)
            {
                commsBoxContainer.SetActive(false);
            }

            _isTransmissionActive = false;
            _transmissionCoroutine = null;
        }

        private void PlayTypewriterChirp()
        {
            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayCommsVoiceBlip();
                return;
            }

            if (_audioSource == null) return;

            if (radioBeepSfx != null)
            {
                _audioSource.pitch = 1.0f + UnityEngine.Random.Range(-beepAudioPitchJitter, beepAudioPitchJitter);
                _audioSource.PlayOneShot(radioBeepSfx, 0.35f);
            }
            else if (GameAudioManager.Instance != null && GameAudioManager.Instance.Config != null)
            {
                // Fallback to camera lock beep or UI click
                var clip = GameAudioManager.Instance.Config.cameraLockBeepSfx ?? GameAudioManager.Instance.Config.buttonClickSfx;
                if (clip != null)
                {
                    _audioSource.pitch = 1.4f + UnityEngine.Random.Range(-0.2f, 0.2f);
                    _audioSource.PlayOneShot(clip, 0.22f);
                }
            }
        }

        private void PlayRadioSound(AudioClip clip, float volume)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.pitch = 1.0f;
            _audioSource.PlayOneShot(clip, volume);
        }

        public void EnsureUIHierarchy()
        {
            if (commsBoxContainer != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Transform existing = canvas.transform.Find("Panel_NarrativeComms");
            if (existing != null)
            {
                commsBoxContainer = existing.gameObject;
                commsBoxRect = commsBoxContainer.GetComponent<RectTransform>();
                commsCanvasGroup = commsBoxContainer.GetComponent<CanvasGroup>();
                portraitImage = commsBoxContainer.transform.Find("PortraitFrame/PortraitImage")?.GetComponent<Image>();
                portraitFrame = commsBoxContainer.transform.Find("PortraitFrame")?.GetComponent<Image>();
                boxOutline = commsBoxContainer.GetComponent<Outline>();
                speakerNameText = commsBoxContainer.transform.Find("SpeakerName")?.GetComponent<TextMeshProUGUI>();
                dialogueText = commsBoxContainer.transform.Find("DialogueText")?.GetComponent<TextMeshProUGUI>();
                return;
            }

            // Create Star Fox Comms Box Root
            GameObject commsGo = new GameObject("Panel_NarrativeComms");
            commsGo.transform.SetParent(canvas.transform, false);
            commsBoxContainer = commsGo;

            commsBoxRect = commsGo.AddComponent<RectTransform>();
            commsBoxRect.anchorMin = new Vector2(0.5f, 1f);
            commsBoxRect.anchorMax = new Vector2(0.5f, 1f);
            commsBoxRect.pivot = new Vector2(0.5f, 1f);
            commsBoxRect.anchoredPosition = _hiddenPos;
            commsBoxRect.sizeDelta = new Vector2(780f, 108f);

            commsCanvasGroup = commsGo.AddComponent<CanvasGroup>();
            commsCanvasGroup.alpha = 0f;

            Image bg = commsGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.07f, 0.12f, 0.94f);

            boxOutline = commsGo.AddComponent<Outline>();
            boxOutline.effectColor = new Color(0.2f, 0.9f, 1f, 0.85f);
            boxOutline.effectDistance = new Vector2(2f, -2f);

            // Left Side: Square Portrait Frame
            GameObject frameGo = new GameObject("PortraitFrame");
            frameGo.transform.SetParent(commsGo.transform, false);
            RectTransform frameRt = frameGo.AddComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0f, 0.5f);
            frameRt.anchorMax = new Vector2(0f, 0.5f);
            frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.anchoredPosition = new Vector2(62f, 15f);
            frameRt.sizeDelta = new Vector2(64f, 64f);

            portraitFrame = frameGo.AddComponent<Image>();
            portraitFrame.color = new Color(0.08f, 0.15f, 0.22f, 0.95f);
            Outline frameOutline = frameGo.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.2f, 0.9f, 1f, 0.65f);
            frameOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Portrait Inner Silhouette
            GameObject portGo = new GameObject("PortraitImage");
            portGo.transform.SetParent(frameGo.transform, false);
            RectTransform portRt = portGo.AddComponent<RectTransform>();
            portRt.anchorMin = new Vector2(0.08f, 0.08f);
            portRt.anchorMax = new Vector2(0.92f, 0.92f);
            portRt.offsetMin = Vector2.zero;
            portRt.offsetMax = Vector2.zero;
            portraitImage = portGo.AddComponent<Image>();
            portraitImage.color = new Color(0.2f, 0.85f, 1f, 0.85f);

            // Speaker Name Text (Below Portrait Frame)
            GameObject speakerGo = new GameObject("SpeakerName");
            speakerGo.transform.SetParent(commsGo.transform, false);
            RectTransform speakerRt = speakerGo.AddComponent<RectTransform>();
            speakerRt.anchorMin = new Vector2(0f, 0.5f);
            speakerRt.anchorMax = new Vector2(0f, 0.5f);
            speakerRt.pivot = new Vector2(0.5f, 0.5f);
            speakerRt.anchoredPosition = new Vector2(62f, -32f);
            speakerRt.sizeDelta = new Vector2(116f, 22f);

            speakerNameText = speakerGo.AddComponent<TextMeshProUGUI>();
            speakerNameText.fontSize = 10.5f;
            speakerNameText.fontStyle = FontStyles.Bold;
            speakerNameText.alignment = TextAlignmentOptions.Center;
            speakerNameText.color = new Color(0.2f, 0.9f, 1f, 1f);
            speakerNameText.text = "КОРПОРАТИВНЫЙ КУРАТОР";

            // Dialogue Text (Right Side Speech Panel)
            GameObject textGo = new GameObject("DialogueText");
            textGo.transform.SetParent(commsGo.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0f, 0f);
            textRt.anchorMax = new Vector2(1f, 1f);
            textRt.pivot = new Vector2(0f, 0.5f);
            textRt.offsetMin = new Vector2(128f, 12f);
            textRt.offsetMax = new Vector2(-16f, -12f);

            dialogueText = textGo.AddComponent<TextMeshProUGUI>();
            dialogueText.fontSize = 14;
            dialogueText.lineSpacing = 16f;
            dialogueText.alignment = TextAlignmentOptions.MidlineLeft;
            dialogueText.color = Color.white;
            dialogueText.text = "";

            commsBoxContainer.SetActive(false);
        }
    }
}
