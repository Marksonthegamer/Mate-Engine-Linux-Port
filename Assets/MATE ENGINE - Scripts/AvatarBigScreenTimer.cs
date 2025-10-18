using UnityEngine;
using System;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;

public class AvatarBigScreenTimer : MonoBehaviour
{
    [Header("Enable BigScreen Alarm Feature")]
    public bool enableBigScreenAlarm = false;

    [Header("Alarm Time (24h, PC local time)")]
    [Range(0, 23)] public int targetHour = 17;
    [Range(0, 59)] public int targetMinute = 0;
    [Range(0, 59)] public int targetSecond = 0;

    [Header("Allowed Animator States")]
    public bool useAllowedStatesWhitelist = false;
    public string[] allowedStates = { "Idle" };

    [Header("Click disables BigScreen completely")]
    public bool clickDisablesBoth = false;

    [Header("Audio")]
    public AudioSource audioSource;
    public List<AudioClip> alarmClips = new List<AudioClip>();

    [Header("Alarm Chat Bubble")]
    [TextArea(1, 3)]
    public string alarmText = "Wake up! This is your alarm!";
    public Transform chatContainer;
    public Sprite bubbleSprite;
    public Color bubbleColor = new Color32(255, 72, 38, 255);
    public Color fontColor = Color.white;
    public Font font;
    public int fontSize = 16;
    public int bubbleWidth = 600;
    public float textPadding = 10f;
    public float bubbleSpacing = 10f;

    [Header("Fake Stream Settings")]
    [Tooltip("Stream speed: characters per second")]
    [Range(5, 100)]
    public int streamSpeed = 35;

    [Header("Stream Audio")]
    public AudioSource streamAudioSource;

    private float alarmInputBlockUntil = 0f;
    [Header("Alarm Cooldown")]
    public float alarmInputBlockDuration = 5f; 


    [Header("Live Status (Inspector)")]
    [SerializeField] private string inspectorEvent;
    [SerializeField] private string inspectorTargetTime;
    [SerializeField] private string inspectorCurrentTime;

    private AvatarBigScreenHandler bigScreenHandler;
    private Animator avatarAnimator;
    private bool alarmActive = false;

    private LLMUnitySamples.Bubble alarmBubble;
    private Coroutine streamCoroutine;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X; public int Y; }

    void Start()
    {
        bigScreenHandler = GetComponent<AvatarBigScreenHandler>();
        avatarAnimator = GetComponent<Animator>();
        alarmActive = false;
        RemoveAlarmBubble();
    }

    void Update()
    {

        enableBigScreenAlarm = SaveLoadHandler.Instance.data.bigScreenAlarmEnabled;
        targetHour = SaveLoadHandler.Instance.data.bigScreenAlarmHour;
        targetMinute = SaveLoadHandler.Instance.data.bigScreenAlarmMinute;
        alarmText = SaveLoadHandler.Instance.data.bigScreenAlarmText;

        if (!enableBigScreenAlarm)
        {
            inspectorEvent = "Alarm disabled";
            StopAlarm();
            return;
        }

        inspectorTargetTime = $"{targetHour:D2}:{targetMinute:D2}:{targetSecond:D2}";
        DateTime now = DateTime.Now;
        inspectorCurrentTime = now.ToString("HH:mm:ss");

        bool isBigScreen = avatarAnimator != null && avatarAnimator.GetBool("isBigScreen");
        bool isBigScreenAlarm = avatarAnimator != null && avatarAnimator.GetBool("isBigScreenAlarm");

        if (!isBigScreenAlarm)
        {
            RemoveAlarmBubble();
        }

        if (isBigScreen && isBigScreenAlarm && alarmActive)
        {
            if (Time.time < alarmInputBlockUntil)
            {
                inspectorEvent = $"Alarm active! Cooldown ({(alarmInputBlockUntil - Time.time):F1}s)";
                return;
            }
            inspectorEvent = "Alarm active! Waiting for user input";
            if (IsGlobalUserInput())
            {
                inspectorEvent = "Alarm stopped by input";
                avatarAnimator.SetBool("isBigScreenAlarm", false);
                alarmActive = false;
                if (audioSource != null && audioSource.isPlaying) audioSource.Stop();

                if (clickDisablesBoth)
                {
                    avatarAnimator.SetBool("isBigScreen", false);
                    if (bigScreenHandler != null)
                        bigScreenHandler.SendMessage("DeactivateBigScreen");
                }
                RemoveAlarmBubble();
            }
            return;
        }


        if (useAllowedStatesWhitelist && !IsInAllowedState())
        {
            inspectorEvent = "Alarm blocked by state";
            StopAlarm();
            RemoveAlarmBubble();
            return;
        }

        if (!alarmActive && now.Hour == targetHour && now.Minute == targetMinute && now.Second == targetSecond)
        {
            inspectorEvent = "Alarm time reached! Activating alarm";
            if (avatarAnimator != null)
            {
                avatarAnimator.SetBool("isBigScreenSaver", false);
                avatarAnimator.SetBool("isBigScreen", true);
                avatarAnimator.SetBool("isBigScreenAlarm", true);
                avatarAnimator.SetBool("isWindowSit", false);
                avatarAnimator.SetBool("isSitting", false);
            }

            if (bigScreenHandler != null)
                bigScreenHandler.SendMessage("ActivateBigScreen");
            PlayRandomAlarm();
            alarmActive = true;
            alarmInputBlockUntil = Time.time + alarmInputBlockDuration; 
            StartCoroutine(ShowAlarmBubbleStreamedDelayed());
        }


        if (alarmActive && (now.Second != targetSecond || now.Minute != targetMinute || now.Hour != targetHour))
        {
            alarmActive = false;
            RemoveAlarmBubble();
        }
    }

    void PlayRandomAlarm()
    {
        if (audioSource != null && alarmClips != null && alarmClips.Count > 0)
        {
            AudioClip clip = alarmClips[UnityEngine.Random.Range(0, alarmClips.Count)];
            audioSource.clip = clip;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    void StopAlarm()
    {
        if (avatarAnimator != null)
            avatarAnimator.SetBool("isBigScreenAlarm", false);
        alarmActive = false;
        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();
        RemoveAlarmBubble();
    }

    bool IsInAllowedState()
    {
        if (avatarAnimator == null || allowedStates == null || allowedStates.Length == 0)
            return true;
        var current = avatarAnimator.GetCurrentAnimatorStateInfo(0);
        foreach (var s in allowedStates)
            if (!string.IsNullOrEmpty(s) && current.IsName(s)) return true;
        return false;
    }

    private bool lastGlobalMouseDown = false;
    private bool IsGlobalUserInput()
    {
        bool mouseDown = (GetAsyncKeyState(0x01) & 0x8000) != 0;
        bool mouseClick = mouseDown && !lastGlobalMouseDown;
        lastGlobalMouseDown = mouseDown;

        bool keyPressed = false;
        for (int key = 0x08; key <= 0xFE; key++)
        {
            if ((GetAsyncKeyState(key) & 0x8000) != 0)
            {
                keyPressed = true;
                break;
            }
        }
        return mouseClick || keyPressed;
    }

    public void TriggerAlarmNow()
    {
        if (avatarAnimator != null)
        {
            avatarAnimator.SetBool("isBigScreen", true);
            avatarAnimator.SetBool("isBigScreenAlarm", true);
            avatarAnimator.SetBool("isBigScreenSaver", false);
            avatarAnimator.SetBool("isWindowSit", false);
            avatarAnimator.SetBool("isSitting", false);
        }
        if (bigScreenHandler != null)
            bigScreenHandler.SendMessage("ActivateBigScreen");
        PlayRandomAlarm();
        alarmActive = true;
        alarmInputBlockUntil = Time.time + alarmInputBlockDuration;
        inspectorEvent = "Alarm triggered manually";
        StartCoroutine(ShowAlarmBubbleStreamedDelayed());
    }


    void ShowAlarmBubbleStreamed()
    {
        if (chatContainer == null) return;
        RemoveAlarmBubble();



        var ui = new LLMUnitySamples.BubbleUI
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

        alarmBubble = new LLMUnitySamples.Bubble(chatContainer, ui, "AlarmBubble", "");

        if (streamAudioSource != null)
        {
            streamAudioSource.Stop();
            streamAudioSource.Play();
        }

        if (streamCoroutine != null) StopCoroutine(streamCoroutine);
        streamCoroutine = StartCoroutine(FakeStreamAlarmText(alarmText));
    }

    IEnumerator FakeStreamAlarmText(string fullText)
    {
        if (alarmBubble == null) yield break;
        alarmBubble.SetText("");
        int length = 0;
        float delay = 1f / Mathf.Max(streamSpeed, 1);

        while (length < fullText.Length)
        {
            length++;
            alarmBubble.SetText(fullText.Substring(0, length));
            yield return new WaitForSeconds(delay);
            if (alarmBubble == null) yield break;
        }
        alarmBubble.SetText(fullText);
        if (streamAudioSource != null && streamAudioSource.isPlaying)
            streamAudioSource.Stop();
        streamCoroutine = null;
    }

    void RemoveAlarmBubble()
    {
        if (streamCoroutine != null)
        {
            StopCoroutine(streamCoroutine);
            streamCoroutine = null;
        }
        if (alarmBubble != null)
        {
            alarmBubble.Destroy();
            alarmBubble = null;
        }
        if (streamAudioSource != null && streamAudioSource.isPlaying)
            streamAudioSource.Stop();

    }

    IEnumerator ShowAlarmBubbleStreamedDelayed()
    {
        yield return new WaitForSeconds(3f); 
        ShowAlarmBubbleStreamed();
    }
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(AvatarBigScreenTimer))]
public class AvatarBigScreenTimerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        AvatarBigScreenTimer script = (AvatarBigScreenTimer)target;
        if (GUILayout.Button("Trigger Alarm Now (Debug)"))
        {
            script.TriggerAlarmNow();
        }
    }
}
#endif