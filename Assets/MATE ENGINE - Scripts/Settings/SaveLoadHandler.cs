using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using System;
using UnityEngine.Serialization;

public class SaveLoadHandler : MonoBehaviour
{
    public static SaveLoadHandler Instance { get; private set; }

    public SettingsData data;

    // Multi-Instance Variablen
    private static string fileName = "settings.json";
    private static string customDataDir = null;

    private string BaseDir => string.IsNullOrEmpty(customDataDir)
        ? Application.persistentDataPath
        : Path.Combine(Application.persistentDataPath, customDataDir);

    private string FilePath => Path.Combine(BaseDir, fileName);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Kommandozeilen-Argumente lesen
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--savefile", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                fileName = args[i + 1].Trim('"');

            if (args[i].Equals("--datadir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                customDataDir = args[i + 1].Trim('"');
        }

        LoadFromDisk();
        ApplyAllSettingsToAllAvatars();

        var limiters = FindObjectsByType<FPSLimiter>(FindObjectsSortMode.None);
        foreach (var limiter in limiters)
        {
            limiter.targetFPS = data.fpsLimit;
            limiter.ApplyFPSLimit();
        }
    }

    // Speichern
    public void SaveToDisk()
    {
        try
        {
            string dir = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(FilePath, json);
            Debug.Log("[SaveLoadHandler] Saved settings to: " + FilePath);
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveLoadHandler] Failed to save: " + e);
        }
    }

    // Laden
    public void LoadFromDisk()
    {
        if (File.Exists(FilePath))
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                data = JsonConvert.DeserializeObject<SettingsData>(json);
                Debug.Log("[SaveLoadHandler] Loaded settings from: " + FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveLoadHandler] Failed to load: " + e);
                data = new SettingsData();
            }
        }
        else
        {
            data = new SettingsData();
        }
    }

    [Serializable]
    public class SettingsData
    {
        public enum WindowSizeState { Normal, Big, Small }
        public WindowSizeState windowSizeState = WindowSizeState.Normal;

        public float soundThreshold = 0.2f;
        public float idleSwitchTime = 10f;
        public float idleTransitionTime = 1f;
        public bool enableDanceSwitch = false;
        public float danceSwitchTime = 15f;
        public float danceTransitionTime = 2f;
        public float avatarSize = 1.0f;
        public bool enableDancing = true;
        public bool enableMouseTracking = true;
        public int fpsLimit = 90;
        public bool isTopmost = true;

        public List<string> allowedApps = new();
        public bool bloom = false;
        public bool dayNight = true;

        public bool enableParticles = true;
        public float petVolume = 1f;
        public float effectsVolume = 1f;
        public float menuVolume = 1f;

        public float headBlend = 0.7f;
        public float eyeBlend = 1f;
        public float spineBlend = 0.5f;

        public bool enableHandHolding = true;
        public bool enableWindowSitting = false;
        public bool ambientOcclusion = false;

        public float uiHueShift = 0f;
        public float uiSaturation = 0.5f;

        public bool enableDiscordRPC = true;

        public bool tutorialDone = false;

        public string selectedLocaleCode = "en";
        public bool enableIK = true;

        public int bigScreenScreenSaverTimeoutIndex = 0;
        public bool bigScreenScreenSaverEnabled = false;

        public bool bigScreenAlarmEnabled = false;
        public int bigScreenAlarmHour = 0;
        public int bigScreenAlarmMinute = 0;
        public string bigScreenAlarmText = "Wake up! This is your alarm!";

        public float windowSitYOffset = 0f;

        public Dictionary<string, float> lightIntensities = new();
        public Dictionary<string, float> lightSaturations = new();
        public Dictionary<string, float> lightHues = new();
        public Dictionary<string, bool> groupToggles = new();

        public Dictionary<string, bool> modStates = new();
        public int graphicsQualityLevel = 1;
        public Dictionary<string, bool> accessoryStates = new();

        [FormerlySerializedAs("startWithWindows")] public bool startWithX11 = false;
        public bool enableRandomMessages = false;

        public string selectedModelPath = "";
        public int contextLength = 4096;
        public bool enableHusbandoMode = false;
    }

    public static void SyncAllowedAppsToAllAvatars()
    {
        var allAvatars = Resources.FindObjectsOfTypeAll<AvatarAnimatorController>();
        var list = new List<string>(Instance.data.allowedApps);

        foreach (var avatar in allAvatars)
            avatar.allowedApps = list;
    }

    public static void ApplyAllSettingsToAllAvatars()
    {
        var data = Instance.data;
        var avatars = Resources.FindObjectsOfTypeAll<AvatarAnimatorController>();

        foreach (var avatar in avatars)
        {
            avatar.SOUND_THRESHOLD = data.soundThreshold;
            avatar.IDLE_SWITCH_TIME = data.idleSwitchTime;
            avatar.IDLE_TRANSITION_TIME = data.idleTransitionTime;
            avatar.enableDancing = data.enableDancing;
            avatar.allowedApps = new List<string>(data.allowedApps);
            avatar.transform.localScale = Vector3.one * data.avatarSize;
            avatar.DANCE_SWITCH_TIME = data.danceSwitchTime;
            avatar.DANCE_TRANSITION_TIME = data.danceTransitionTime;
            avatar.enableDanceSwitch = data.enableDanceSwitch;
            avatar.enableHusbandoMode = data.enableHusbandoMode;

            foreach (var tracker in avatar.GetComponentsInChildren<AvatarMouseTracking>(true))
            {
                tracker.enableMouseTracking = data.enableMouseTracking;
                tracker.headBlend = data.headBlend;
                tracker.spineBlend = data.spineBlend;
                tracker.eyeBlend = data.eyeBlend;
            }

            foreach (var ik in avatar.GetComponentsInChildren<IKFix>(true))
                ik.enableIK = data.enableIK;

            foreach (var handler in avatar.GetComponentsInChildren<AvatarParticleHandler>(true))
            {
                handler.featureEnabled = data.enableParticles;
                handler.enabled = data.enableParticles;
            }

            foreach (var holder in avatar.GetComponentsInChildren<HandHolder>(true))
                holder.enableHandHolding = data.enableHandHolding;

            if (avatar.animator != null &&
                avatar.animator.isActiveAndEnabled &&
                avatar.animator.runtimeAnimatorController != null)
            {
                avatar.animator.SetBool("isDancing", false);
                avatar.animator.SetBool("isDragging", false);
                avatar.isDancing = false;
                avatar.isDragging = false;
            }


            foreach (var handler in Resources.FindObjectsOfTypeAll<AvatarWindowHandler>())
                handler.windowSitYOffset = data.windowSitYOffset;
        }
    }
}
