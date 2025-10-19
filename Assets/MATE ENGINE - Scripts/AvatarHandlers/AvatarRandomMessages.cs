using System;
using System.Collections;
using System.Collections.Generic;
using LLMUnitySamples;
using UnityEngine;
using UnityEngine.Localization.Settings;
using Random = UnityEngine.Random;

[Serializable]
public class AvatarMessage
{
    [TextArea(1, 3)]
    public string text = "Hello!";
    public string locKey = "";
    public string state = "Idle";
    public bool onActive;
    public bool isHusbando;
}

public class AvatarRandomMessages : MonoBehaviour
{
    [Header("Localization")]
    public string localizationTable = "Languages (UI)";

    public bool enableRandomMessages = true;
    [Range(5, 60)] public int minDelay = 10;
    [Range(5, 60)] public int maxDelay = 60;
    [Range(5, 20)] public int despawnTime = 10;
    [Range(0, 100)] public int onActiveChance = 100;

    public List<AvatarMessage> messages = new();

    public Transform chatContainer;
    public Sprite bubbleSprite;
    public Color bubbleColor = new Color32(120, 120, 255, 255);
    public Color fontColor = Color.white;
    public Font font;
    public int fontSize = 16;
    public int bubbleWidth = 600;
    public float textPadding = 10f;
    public float bubbleSpacing = 10f;

    [Range(5, 100)] public int streamSpeed = 35;
    public AudioSource streamAudioSource;

    public bool useAllowedStatesWhitelist;
    public string[] allowedStates = { "Idle" };
    public List<GameObject> blockObjects = new();

    [SerializeField] private string inspectorEvent;

    private Bubble activeBubble;
    private Coroutine streamCoroutine;
    private Coroutine despawnCoroutine;
    private Coroutine loopCoroutine;
    private bool isBubbleActive;

    private Animator avatarAnimator;
    private string lastAnimatorStateName = "";

    static readonly int isMaleHash = Animator.StringToHash("isMale");

    void Start()
    {
        avatarAnimator = GetComponent<Animator>();
        enableRandomMessages = GetGlobalEnabled();
        ApplyEnableStateImmediate(enableRandomMessages);
    }

    void Update()
    {
        bool globalEnabled = GetGlobalEnabled();
        if (globalEnabled != enableRandomMessages)
        {
            enableRandomMessages = globalEnabled;
            ApplyEnableStateImmediate(enableRandomMessages);
        }

        if (!enableRandomMessages) return;

        if (isBubbleActive)
        {
            if (IsBlockedByObjects()) { inspectorEvent = "Bubble removed (blocked)"; RemoveBubble(); }
            else if (useAllowedStatesWhitelist && !IsInAllowedState()) { inspectorEvent = "Bubble removed (state not allowed)"; RemoveBubble(); }
        }

        if (avatarAnimator != null)
        {
            var current = avatarAnimator.GetCurrentAnimatorStateInfo(0);
            string currentStateName = GetCurrentStateName(current);

            if (currentStateName != lastAnimatorStateName)
            {
                var candidates = messages.FindAll(m => m.onActive && !string.IsNullOrEmpty(m.state) && current.IsName(m.state) && IsMessageAllowedByGender(m));
                if (candidates.Count > 0)
                {
                    if (Random.Range(0, 100) < onActiveChance)
                    {
                        AvatarMessage chosen = candidates[Random.Range(0, candidates.Count)];
                        ShowSpecificMessage(chosen);
                    }
                }
                lastAnimatorStateName = currentStateName;
            }
        }
    }

    private bool GetGlobalEnabled()
    {
        return SaveLoadHandler.Instance ? SaveLoadHandler.Instance.data.enableRandomMessages : enableRandomMessages;
    }

    private void ApplyEnableStateImmediate(bool enabled)
    {
        if (enabled)
        {
            if (loopCoroutine == null) loopCoroutine = StartCoroutine(RandomMessageLoop());
        }
        else
        {
            if (loopCoroutine != null) { StopCoroutine(loopCoroutine); loopCoroutine = null; }
            RemoveBubble();
        }
    }

    IEnumerator RandomMessageLoop()
    {
        while (true)
        {
            if (!enableRandomMessages) { yield return null; continue; }
            if (!isBubbleActive && messages.Count > 0)
            {
                float wait = Random.Range(minDelay, maxDelay + 1);
                yield return new WaitForSeconds(wait);

                if (!enableRandomMessages) continue;
                if (IsBlockedByObjects()) { yield return new WaitForSeconds(1f); continue; }
                if (useAllowedStatesWhitelist && !IsInAllowedState()) { yield return new WaitForSeconds(1f); continue; }

                ShowRandomMessage();
            }
            else yield return null;
        }
    }

    void ShowRandomMessage()
    {
        if (!enableRandomMessages) return;
        var idlePool = messages.FindAll(m => !m.onActive && IsMessageAllowedByGender(m));
        if (idlePool.Count == 0) return;
        ShowSpecificMessage(idlePool[Random.Range(0, idlePool.Count)]);
    }

    void ShowSpecificMessage(AvatarMessage msg)
    {
        if (!enableRandomMessages) return;
        if (chatContainer == null || msg == null) return;
        RemoveBubble();

        var ui = new BubbleUI
        {
            sprite = bubbleSprite,
            font = font,
            fontSize = fontSize,
            fontColor = fontColor,
            bubbleColor = bubbleColor,
            bottomPosition = 0,
            leftPosition = 1,
            textPadding = textPadding,
            bubbleOffset = bubbleSpacing,
            bubbleWidth = bubbleWidth,
            bubbleHeight = -1
        };

        string finalText = ResolveText(msg);

        activeBubble = new Bubble(chatContainer, ui, "RandomBubble", "");
        isBubbleActive = true;

        if (streamAudioSource != null) { streamAudioSource.Stop(); streamAudioSource.Play(); }
        if (streamCoroutine != null) StopCoroutine(streamCoroutine);

        if (avatarAnimator != null) avatarAnimator.SetBool("isTalking", true);

        streamCoroutine = StartCoroutine(FakeStreamText(finalText));
        if (despawnCoroutine != null) StopCoroutine(despawnCoroutine);
        despawnCoroutine = StartCoroutine(DespawnAfterDelay());
    }

    string ResolveText(AvatarMessage msg)
    {
        if (!string.IsNullOrEmpty(msg.locKey))
        {
            try
            {
                string localized = LocalizationSettings.StringDatabase.GetLocalizedString(localizationTable, msg.locKey);
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
            catch { }
        }
        return msg.text;
    }

    IEnumerator FakeStreamText(string fullText)
    {
        if (activeBubble == null) yield break;
        activeBubble.SetText("");
        int length = 0;
        float delay = 1f / Mathf.Max(streamSpeed, 1);
        while (length < fullText.Length)
        {
            if (!enableRandomMessages) yield break;
            length++;
            activeBubble.SetText(fullText.Substring(0, length));
            yield return new WaitForSeconds(delay);
            if (activeBubble == null) yield break;
        }
        activeBubble.SetText(fullText);
        if (streamAudioSource != null && streamAudioSource.isPlaying) streamAudioSource.Stop();

        if (avatarAnimator != null) avatarAnimator.SetBool("isTalking", false);

        streamCoroutine = null;
    }

    IEnumerator DespawnAfterDelay()
    {
        float t = 0f;
        while (t < despawnTime)
        {
            if (!enableRandomMessages) yield break;
            t += Time.deltaTime;
            yield return null;
        }
        RemoveBubble();
    }

    void RemoveBubble()
    {
        if (streamCoroutine != null) { StopCoroutine(streamCoroutine); streamCoroutine = null; }
        if (despawnCoroutine != null) { StopCoroutine(despawnCoroutine); despawnCoroutine = null; }
        if (activeBubble != null) { activeBubble.Destroy(); activeBubble = null; }
        if (streamAudioSource != null && streamAudioSource.isPlaying) streamAudioSource.Stop();
        isBubbleActive = false;

        if (avatarAnimator != null) avatarAnimator.SetBool("isTalking", false);
    }

    bool IsInAllowedState()
    {
        if (avatarAnimator == null || allowedStates == null || allowedStates.Length == 0) return true;
        var current = avatarAnimator.GetCurrentAnimatorStateInfo(0);
        foreach (var s in allowedStates) if (!string.IsNullOrEmpty(s) && current.IsName(s)) return true;
        return false;
    }

    bool IsBlockedByObjects()
    {
        if (blockObjects == null || blockObjects.Count == 0) return false;
        foreach (var go in blockObjects) if (go != null && go.activeInHierarchy) return true;
        return false;
    }

    bool IsMessageAllowedByGender(AvatarMessage msg)
    {
        if (avatarAnimator == null) return true;
        if (!HasParam(isMaleHash)) return true;
        bool isMale = avatarAnimator.GetFloat(isMaleHash) > 0.5f;
        return msg.isHusbando ? isMale : !isMale;
    }

    bool HasParam(int hash)
    {
        var ps = avatarAnimator.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash) return true;
        return false;
    }

    string GetCurrentStateName(AnimatorStateInfo stateInfo)
    {
        if (avatarAnimator == null) return "";
        var clips = avatarAnimator.GetCurrentAnimatorClipInfo(0);
        if (clips.Length > 0 && clips[0].clip != null) return clips[0].clip.name;
        return stateInfo.IsName("") ? "" : stateInfo.shortNameHash.ToString();
    }
}
