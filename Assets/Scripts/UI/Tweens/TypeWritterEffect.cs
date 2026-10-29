using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class TypeWritterEffect : MonoBehaviour
{
    [Header("References")]
    public TMP_Text textUI;

    [Header("Settings")]
    [TextArea(3, 6)]
    public List<string> messages = new List<string>(); // Each entry = one message
    public float letterDelay = 0.05f;
    public float messageDelay = 1.5f; // delay between messages

    public AudioSource audioSource;
    public AudioClip typeSound;
    public float pitchVariation = 0.1f;

    public bool isEnded = false;

    [Header("Pop Window")]
    [SerializeField] private UIPopWindow popWindow; // child reference

    private Coroutine typingCoroutine;
    private bool skipCurrentTyping = false;

    private void Awake()
    {
        // Auto-assign from children if not set in Inspector
        if (popWindow == null)
            popWindow = GetComponentInChildren<UIPopWindow>(true);
    }

    void Start()
    {
        popWindow.Show();
        StartTypingSequence();
    }

    void Update()
    {
        // If typing is active and player presses anything, request skip for the current message
        if (typingCoroutine != null && Input.anyKeyDown)
        {
            skipCurrentTyping = true;
        }
    }

    public void StartTypingSequence()
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeMessages());
    }

    private IEnumerator TypeMessages()
    {
        if (textUI != null) textUI.text = "";

        foreach (string message in messages)
        {
            yield return StartCoroutine(TypeSingleMessage(message));

            // reset skip flag between messages
            skipCurrentTyping = false;

            // Wait before next message (allow player to skip this wait by pressing any key)
            float waited = 0f;
            while (waited < messageDelay)
            {
                if (skipCurrentTyping)
                    break;
                waited += Time.deltaTime;
                yield return null;
            }
        }

        yield return new WaitForSeconds(2f);
        isEnded = true;

        // Hide the pop window when finished
        if (popWindow != null)
            popWindow.Hide();
    }

    private IEnumerator TypeSingleMessage(string message)
    {
        if (textUI == null)
            yield break;

        textUI.text = "";

        int i = 0;
        while (i < message.Length)
        {
            // If player requested skip, reveal whole message immediately
            if (skipCurrentTyping)
            {
                textUI.text = message;
                yield break;
            }

            // Handle rich text tags instantly (so <color> doesn't appear letter by letter)
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

            // Add one visible character
            textUI.text += message[i];
            i++;

            if (typeSound != null && audioSource != null)
            {
                audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                audioSource.PlayOneShot(typeSound);
            }

            // Responsive delay: check skip each frame instead of a single WaitForSeconds call
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