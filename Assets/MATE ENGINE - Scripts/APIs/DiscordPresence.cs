using UnityEngine;
using DiscordRPC;
using System;
using System.Collections.Generic;

public class DiscordPresence : MonoBehaviour
{
    public enum TimerMode
    {
        None,
        StartNow,
        FixedStartTime
    }

    [Header("Discord App Info")]
    public string appId = "123456789012345678";

    [Header("Default Text")]
    public string detailsLine = "Playing with my desktop pet";
    public string stateLine = "Just vibing";

    [Header("Timer")]
    public TimerMode timerMode = TimerMode.StartNow;
    public string fixedStartTimeISO = "2025-04-07T12:00:00Z";

    [Header("Button")]
    public string buttonLabel = "Visit Website";
    public string buttonUrl = "https://mateengine.com";

    [Header("Icons")]
    public string largeImageKey = "logo";
    public string largeImageText = "MateEngine";
    public string smallImageKey = "steam-icon";
    public string smallImageText = "Steam Edition";

    [Header("Model Root (VRMModel or CustomVRM must be child)")]
    public GameObject modelRoot;

    [Header("State-Based Overrides")]
    public List<PresenceEntry> presenceOverrides = new List<PresenceEntry>();

    [Serializable]
    public class PresenceEntry
    {
        public string stateName;
        public string details;
        public string state;
    }

    private DiscordRpcClient client;
    private RichPresence presence;
    private string lastState = "";
    private Animator cachedAnimator;
    private bool wasRPCEnabled = false;

    private long gameStartTimestamp = 0;

    void Start()
    {
        wasRPCEnabled = SaveLoadHandler.Instance?.data.enableDiscordRPC == true;
        if (wasRPCEnabled)
        {
            InitGameStartTimestamp();
            client = new DiscordRpcClient(appId);
            client.Initialize();
            ResolveAnimator();
            UpdatePresence(force: true);
        }
    }
    void Update()
    {
        bool isEnabled = SaveLoadHandler.Instance?.data.enableDiscordRPC == true;

        if (isEnabled != wasRPCEnabled)
        {
            wasRPCEnabled = isEnabled;

            if (isEnabled)
            {
                InitGameStartTimestamp();
                client = new DiscordRpcClient(appId);
                client.Initialize();
                ResolveAnimator();
                UpdatePresence(force: true);
                Debug.Log("[DiscordPresence] Enabled and client initialized at runtime.");
            }
            else
            {
                if (client != null)
                {
                    client.ClearPresence();
                    client.Dispose();
                    client = null;
                    Debug.Log("[DiscordPresence] Disabled and client disposed at runtime.");
                }
            }
        }
        if (wasRPCEnabled && client != null)
        {
            if (cachedAnimator == null)
            {
                ResolveAnimator();
                if (cachedAnimator == null) return;
            }

            UpdatePresence();
        }
    }
    void InitGameStartTimestamp()
    {
        if (timerMode == TimerMode.FixedStartTime)
        {
            if (DateTimeOffset.TryParse(fixedStartTimeISO, out var fixedTime))
            {
                gameStartTimestamp = fixedTime.ToUnixTimeMilliseconds();
            }
            else
            {
                Debug.LogWarning("[DiscordPresence] Invalid fixed timestamp. Using now.");
                gameStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
        else if (timerMode == TimerMode.StartNow)
        {
            if (gameStartTimestamp == 0)
            {
                gameStartTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }
        else
        {
            gameStartTimestamp = 0;
        }
    }

    void ResolveAnimator()
    {
        if (modelRoot == null)
        {
            Debug.LogWarning("[DiscordPresence] ModelRoot not assigned.");
            return;
        }

        Transform vrmModel = modelRoot.transform.Find("CustomVRM(Clone)");
        if (vrmModel == null || !vrmModel.gameObject.activeInHierarchy)
        {
            vrmModel = modelRoot.transform.Find("VRMModel");
        }

        if (vrmModel != null && vrmModel.gameObject.activeInHierarchy)
        {
            cachedAnimator = vrmModel.GetComponent<Animator>();
        }
    }

    void UpdatePresence(bool force = false)
    {
        if (cachedAnimator == null) return;

        string currentState = GetCurrentAnimatorState();
        if (!force && currentState == lastState)
            return;

        lastState = currentState;
        string details = detailsLine;
        string state = stateLine;

        for (int i = 0; i < presenceOverrides.Count; i++)
        {
            var entry = presenceOverrides[i];
            if (currentState == entry.stateName)
            {
                if (!string.IsNullOrEmpty(entry.details)) details = entry.details;
                if (!string.IsNullOrEmpty(entry.state)) state = entry.state;
                break;
            }
        }

        presence = new RichPresence
        {
            Details = details,
            State = state,
            Assets = new Assets
            {
                LargeImageKey = largeImageKey,
                LargeImageText = largeImageText,
                SmallImageKey = smallImageKey,
                SmallImageText = smallImageText
            }
        };

        if (timerMode != TimerMode.None)
        {
            presence.Timestamps = new Timestamps
            {
                StartUnixMilliseconds = (ulong?)gameStartTimestamp
            };
        }

        if (!string.IsNullOrEmpty(buttonLabel) && !string.IsNullOrEmpty(buttonUrl))
        {
            presence.Buttons = new DiscordRPC.Button[]
            {
                new DiscordRPC.Button { Label = buttonLabel, Url = buttonUrl }
            };
        }

        client.SetPresence(presence);
        Debug.Log($"[DiscordPresence] Updated to state: {currentState} → {details} / {state}");
    }

    string GetCurrentAnimatorState()
    {
        if (cachedAnimator == null) return "";

        AnimatorStateInfo stateInfo = cachedAnimator.GetCurrentAnimatorStateInfo(0);
        if (cachedAnimator.IsInTransition(0)) return "";

        for (int i = 0; i < presenceOverrides.Count; i++)
        {
            var name = presenceOverrides[i].stateName;
            if (stateInfo.IsName(name))
                return name;
        }

        return "";
    }

    void OnApplicationQuit()
    {
        try
        {
            if (client != null)
            {
                client.ClearPresence();
                client.Dispose();
                client = null;
                Debug.Log("[DiscordPresence] Presence cleared and client disposed.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[DiscordPresence] Error during Discord shutdown: " + ex);
        }
    }
}