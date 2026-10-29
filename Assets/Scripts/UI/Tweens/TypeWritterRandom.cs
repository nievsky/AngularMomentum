using System;
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TypeWritterRandom : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text textUI;

    [Header("Messages")]
    [TextArea(3, 6)]
    public List<string> messages = new List<string>(); // pool to pick from

    [Header("Typing")]
    [Min(0f)] public float letterDelay = 0.05f;
    public AudioSource audioSource;
    public AudioClip typeSound;
    [Min(0f)] public float pitchVariation = 0.1f;

    [Header("Pop Window")]
    [SerializeField] private UIPopWindow popWindow; // child reference

    [Header("Flow")]
    [Tooltip("Delay after the message completes before hiding the window.")]
    [Min(0f)] public float endDelay = 2f;

    [Tooltip("If true, starts typing when this GameObject becomes active.")]
    [SerializeField] private bool playOnEnable = true;

    public bool isEnded { get; private set; }
    public event Action Finished;

    private Coroutine typingCoroutine;
    private bool skipCurrentTyping;

    private void Awake()
    {
        if (popWindow == null)
            popWindow = GetComponentInChildren<UIPopWindow>(true);
        if (textUI == null)
            textUI = GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }
        skipCurrentTyping = false;
    }

    // Public control API ------------------------------------------------------

    public void SetMessages(IEnumerable<string> newMessages)
    {
        messages.Clear();
        if (newMessages != null)
            messages.AddRange(newMessages);
    }

    public void Play()
    {
        if (popWindow != null) popWindow.Show();

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeRandomMessageRoutine());
    }

    public void PlayOne(string message)
    {
        messages.Clear();
        if (!string.IsNullOrEmpty(message))
            messages.Add(message);
        Play();
    }

    public void PlayRandomFrom(IList<string> pool)
    {
        messages.Clear();
        if (pool != null) messages.AddRange(pool);
        Play();
    }

    public void StopTyping(bool hideWindow = true, bool clearText = true)
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);
        typingCoroutine = null;

        isEnded = true;
        if (clearText && textUI != null) textUI.text = "";
        if (hideWindow && popWindow != null) popWindow.Hide();
    }

    // Internals ---------------------------------------------------------------

    private void Update()
    {
        if (typingCoroutine != null && Input.anyKeyDown)
            skipCurrentTyping = true;
    }

    private IEnumerator TypeRandomMessageRoutine()
    {
        isEnded = false;
        skipCurrentTyping = false;

        if (textUI != null) textUI.text = "";

        if (messages == null || messages.Count == 0)
        {
            Debug.LogWarning("TypeWritterRandom: No messages assigned.");
            yield return new WaitForSeconds(endDelay);
            isEnded = true;
            if (popWindow != null) popWindow.Hide();
            Finished?.Invoke();
            yield break;
        }

        // pick one random message
        int idx = UnityEngine.Random.Range(0, messages.Count);
        string message = messages[idx];

        yield return StartCoroutine(TypeSingleMessage(message));

        // wait and close
        yield return new WaitForSeconds(endDelay);
        isEnded = true;
        if (popWindow != null) popWindow.Hide();
        Finished?.Invoke();
    }

    private IEnumerator TypeSingleMessage(string message)
    {
        if (textUI == null) yield break;

        textUI.text = "";
        int i = 0;

        while (i < message.Length)
        {
            if (skipCurrentTyping)
            {
                textUI.text = message;
                yield break;
            }

            if (message[i] == '<')
            {
                int closingIndex = message.IndexOf('>', i);
                if (closingIndex != -1)
                {
                    string tag = message.Substring(i, closingIndex - i + 1);
                    textUI.text += tag;
                    i = closingIndex + 1;
                    continue;
                }
            }

            textUI.text += message[i];
            i++;

            if (typeSound != null && audioSource != null)
            {
                audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchVariation, pitchVariation);
                audioSource.PlayOneShot(typeSound);
            }

            float t = 0f;
            while (t < letterDelay)
            {
                if (skipCurrentTyping)
                {
                    textUI.text = message;
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }
        }
    }
}