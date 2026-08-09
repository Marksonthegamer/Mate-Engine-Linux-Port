using DiscordRPC.Logging;
using Lachee.Discord.Control;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;


public class SettingsHandlerToggles : MonoBehaviour
{
    [Header("Toggles")]
    public Toggle enableDancingToggle;
    public Toggle enableMouseTrackingToggle;
    public Toggle isTopmostToggle;
    public Toggle enableParticlesToggle;
    public Toggle bloomToggle;
    public Toggle desktopAmbiToggle;
    public Toggle dayNightToggle;
    public Toggle enableWindowSittingToggle;
    public Toggle enableDiscordRPCToggle;
    public Toggle enableHandHoldingToggle;
    public Toggle ambientOcclusionToggle;
    public Toggle enableIKToggle;
    public Toggle enableDanceSwitchToggle;
    public Toggle enableRandomMessagesToggle;
    public Toggle enableHusbandoModeToggle;
    public Toggle enableAutoMemoryTrimToggle;
    public Toggle enableMinecraftMessagesToggle;
    public Toggle enableFeedSystemToggle;
    public Toggle enableRandomAvatarToggle;
    public Toggle verboseDiscordRpcLogToggle;

    [FormerlySerializedAs("bloomObject")] [Header("External Objects")]
    public GameObject desktopAmbientObject;
    public GameObject bloomObject;
    public ColorController colorController;
    public GameObject dayNightObject;
    public GameObject ambientOcclusionObject;
    
    private AvatarParticleHandler currentParticleHandler;

    void Start()
    {
        enableDancingToggle?.onValueChanged.AddListener(OnEnableDancingChanged);
        enableMouseTrackingToggle?.onValueChanged.AddListener(OnEnableMouseTrackingChanged);
        isTopmostToggle?.onValueChanged.AddListener(OnIsTopmostChanged);
        enableParticlesToggle?.onValueChanged.AddListener(OnEnableParticlesChanged);
        bloomToggle?.onValueChanged.AddListener(OnBloomChanged);
        desktopAmbiToggle?.onValueChanged.AddListener(OnDesktopAmbiChanged);
        dayNightToggle?.onValueChanged.AddListener(OnDayNightChanged);
        enableWindowSittingToggle?.onValueChanged.AddListener(OnEnableWindowSittingChanged);
        enableDiscordRPCToggle?.onValueChanged.AddListener(OnEnableDiscordRPCChanged);
        enableHandHoldingToggle?.onValueChanged.AddListener(OnEnableHandHoldingChanged);
        ambientOcclusionToggle?.onValueChanged.AddListener(OnAmbientOcclusionChanged);
        enableIKToggle?.onValueChanged.AddListener(OnEnableIKChanged);
        enableDanceSwitchToggle?.onValueChanged.AddListener(OnEnableDanceSwitchChanged);
        enableRandomMessagesToggle?.onValueChanged.AddListener(OnEnableRandomMessagesChanged);
        enableHusbandoModeToggle?.onValueChanged.AddListener(OnEnableHusbandoModeChanged);
        enableAutoMemoryTrimToggle?.onValueChanged.AddListener(OnEnableAutoMemoryTrimChanged);
        enableMinecraftMessagesToggle?.onValueChanged.AddListener(OnEnableMinecraftMessagesChanged);
        enableFeedSystemToggle?.onValueChanged.AddListener(OnEnableFeedSystemChanged);
        enableRandomAvatarToggle?.onValueChanged.AddListener(OnEnableRandomAvatarChanged);
        verboseDiscordRpcLogToggle?.onValueChanged.AddListener(OnVerboseDiscordRpcLogChanged);
        LoadSettings();
        ApplySettings();
    }

    #region Toggle Callbacks

    private void OnEnableDancingChanged(bool v) { SaveLoadHandler.Instance.data.enableDancing = v; ApplySettings(); Save(); }
    private void OnEnableMouseTrackingChanged(bool v) { SaveLoadHandler.Instance.data.enableMouseTracking = v; ApplySettings(); Save(); }
    private void OnIsTopmostChanged(bool v) { SaveLoadHandler.Instance.data.isTopmost = v; ApplySettings(); Save(); }
    private void OnEnableParticlesChanged(bool v) { SaveLoadHandler.Instance.data.enableParticles = v; ApplySettings(); Save(); }
    private void OnBloomChanged(bool v) { SaveLoadHandler.Instance.data.bloom = v; ApplySettings(); Save(); }
    private void OnDesktopAmbiChanged(bool v) { SaveLoadHandler.Instance.data.desktopAmbient = v; ApplySettings(); Save(); }
    private void OnDayNightChanged(bool v) { SaveLoadHandler.Instance.data.dayNight = v; ApplySettings(); Save(); }
    private void OnEnableWindowSittingChanged(bool v) { SaveLoadHandler.Instance.data.enableWindowSitting = v; ApplySettings(); if (!v) { var handlers = FindObjectsByType<AvatarWindowHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None); foreach (var handler in handlers) handler.ForceExitWindowSitting(); } Save(); }
    private void OnEnableDiscordRPCChanged(bool v) { SaveLoadHandler.Instance.data.enableDiscordRPC = v; ApplySettings(); Save(); }
    private void OnEnableHandHoldingChanged(bool v) { SaveLoadHandler.Instance.data.enableHandHolding = v; ApplySettings(); Save(); }
    private void OnAmbientOcclusionChanged(bool v) { SaveLoadHandler.Instance.data.ambientOcclusion = v; ApplySettings(); Save(); }
    private void OnEnableIKChanged(bool v) { SaveLoadHandler.Instance.data.enableIK = v; ApplySettings(); Save(); }
    private void OnEnableDanceSwitchChanged(bool v) { SaveLoadHandler.Instance.data.enableDanceSwitch = v; Save(); }
    private void OnEnableAutoMemoryTrimChanged(bool v) { SaveLoadHandler.Instance.data.enableAutoMemoryTrim = v; ApplySettings(); Save(); }
    private void OnEnableRandomMessagesChanged(bool v)
    {
        SaveLoadHandler.Instance.data.enableRandomMessages = v;
        ApplySettings();
        Save();
    }
    private void OnEnableHusbandoModeChanged(bool v)
    {
        SaveLoadHandler.Instance.data.enableHusbandoMode = v;
        ApplySettings();
        Save();
    }
    private void OnEnableMinecraftMessagesChanged(bool v)
    {
        SaveLoadHandler.Instance.data.enableMinecraftMessages = v;
        ApplySettings();
        Save();
    }
    private void OnEnableFeedSystemChanged(bool v)
    {
        SaveLoadHandler.Instance.data.enableFeedSystem = v;
        ApplySettings();
        Save();
    }
    private void OnEnableRandomAvatarChanged(bool v) { SaveLoadHandler.Instance.data.enableRandomAvatar = v; Save(); }
    
    private void OnVerboseDiscordRpcLogChanged(bool v) { SaveLoadHandler.Instance.data.verboseDiscordRPCLog = v; if (DiscordPresence.Instance.isInitialized) DiscordPresence.Instance.client.Logger = v ? new UnityLogger { Level = LogLevel.Trace } : new NullLogger(); Save(); }
    #endregion

    public void LoadSettings()
    {
        var data = SaveLoadHandler.Instance.data;
        enableDancingToggle?.SetIsOnWithoutNotify(data.enableDancing);
        enableMouseTrackingToggle?.SetIsOnWithoutNotify(data.enableMouseTracking);
        isTopmostToggle?.SetIsOnWithoutNotify(data.isTopmost);
        enableParticlesToggle?.SetIsOnWithoutNotify(data.enableParticles);
        bloomToggle?.SetIsOnWithoutNotify(data.bloom);
        desktopAmbiToggle?.SetIsOnWithoutNotify(data.desktopAmbient);
        dayNightToggle?.SetIsOnWithoutNotify(data.dayNight);
        enableWindowSittingToggle?.SetIsOnWithoutNotify(data.enableWindowSitting);
        enableDiscordRPCToggle?.SetIsOnWithoutNotify(data.enableDiscordRPC);
        enableHandHoldingToggle?.SetIsOnWithoutNotify(data.enableHandHolding);
        ambientOcclusionToggle?.SetIsOnWithoutNotify(data.ambientOcclusion);
        enableIKToggle?.SetIsOnWithoutNotify(data.enableIK);
        enableDanceSwitchToggle?.SetIsOnWithoutNotify(data.enableDanceSwitch);
        enableRandomMessagesToggle?.SetIsOnWithoutNotify(data.enableRandomMessages);
        enableHusbandoModeToggle?.SetIsOnWithoutNotify(data.enableHusbandoMode);
        enableAutoMemoryTrimToggle?.SetIsOnWithoutNotify(data.enableAutoMemoryTrim);
        enableMinecraftMessagesToggle?.SetIsOnWithoutNotify(data.enableMinecraftMessages);
        enableFeedSystemToggle?.SetIsOnWithoutNotify(SaveLoadHandler.Instance.data.enableFeedSystem);
        enableRandomAvatarToggle?.SetIsOnWithoutNotify(SaveLoadHandler.Instance.data.enableRandomAvatar);
        verboseDiscordRpcLogToggle?.SetIsOnWithoutNotify(data.verboseDiscordRPCLog);
        ApplySettings();
    }

    public void ApplySettings()
    {
        var data = SaveLoadHandler.Instance.data;

        // Random Messages
        foreach (var arm in Resources.FindObjectsOfTypeAll<AvatarRandomMessages>())
        {
            arm.enableRandomMessages = data.enableRandomMessages;
            if (data.enableRandomMessages && arm.isActiveAndEnabled)
            {
                arm.StopAllCoroutines();
                arm.StartCoroutine("RandomMessageLoop");
            }
            else
            {
                arm.StopAllCoroutines();
            }
        }

        foreach (var mt in Resources.FindObjectsOfTypeAll<MemoryTrim>())
            mt.SetAutoTrimEnabled(data.enableAutoMemoryTrim);


        // Visuals
        if (desktopAmbientObject) desktopAmbientObject.SetActive(data.desktopAmbient);
        if (bloomObject) bloomObject.SetActive(data.bloom);
        if (dayNightObject) dayNightObject.SetActive(data.dayNight);
        if (ambientOcclusionObject) ambientOcclusionObject.SetActive(data.ambientOcclusion);

        // Window
        WindowManager.Instance.SetTopmost(data.isTopmost);

        // Food
        foreach (var c in Resources.FindObjectsOfTypeAll<AvatarFoodController>())
            c.SetFeatureEnabled(SaveLoadHandler.Instance.data.enableFeedSystem);


        // Particles
        if (currentParticleHandler == null)
        {
            var handlers = FindObjectsByType<AvatarParticleHandler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            currentParticleHandler = handlers.Length > 0 ? handlers[0] : null;
        }
        if (currentParticleHandler != null)
        {
            currentParticleHandler.featureEnabled = data.enableParticles;
            currentParticleHandler.enabled = data.enableParticles;
        }
        PetVoiceReactionHandler.GlobalHoverObjectsEnabled = data.enableParticles;

        foreach (var amm in Resources.FindObjectsOfTypeAll<AvatarMinecraftMessages>())
            amm.enableMinecraftMessages = data.enableMinecraftMessages;

        WindowManager.Instance.SetWindowType(data.windowType);
    }

    public void ResetToDefaults()
    {
        enableDancingToggle?.SetIsOnWithoutNotify(true);
        enableMouseTrackingToggle?.SetIsOnWithoutNotify(true);
        isTopmostToggle?.SetIsOnWithoutNotify(true);
        enableParticlesToggle?.SetIsOnWithoutNotify(true);
        desktopAmbiToggle?.SetIsOnWithoutNotify(false);
        dayNightToggle?.SetIsOnWithoutNotify(true);
        enableWindowSittingToggle?.SetIsOnWithoutNotify(false);
        enableDiscordRPCToggle?.SetIsOnWithoutNotify(true);
        enableHandHoldingToggle?.SetIsOnWithoutNotify(true);
        ambientOcclusionToggle?.SetIsOnWithoutNotify(false);
        enableIKToggle?.SetIsOnWithoutNotify(true);
        enableDanceSwitchToggle?.SetIsOnWithoutNotify(false);
        enableRandomMessagesToggle?.SetIsOnWithoutNotify(false);
        enableHusbandoModeToggle?.SetIsOnWithoutNotify(false);
        enableAutoMemoryTrimToggle?.SetIsOnWithoutNotify(false);
        enableMinecraftMessagesToggle?.SetIsOnWithoutNotify(false);
        enableFeedSystemToggle?.SetIsOnWithoutNotify(false);
        enableRandomAvatarToggle?.SetIsOnWithoutNotify(false);
        verboseDiscordRpcLogToggle?.SetIsOnWithoutNotify(false);

        var data = SaveLoadHandler.Instance.data;
        data.enableDancing = true;
        data.enableMouseTracking = true;
        data.isTopmost = true;
        data.enableParticles = true;
        data.desktopAmbient = false;
        data.bloom = false;
        data.dayNight = true;
        data.enableWindowSitting = false;
        data.enableDiscordRPC = true;
        data.enableHandHolding = true;
        data.ambientOcclusion = false;
        data.enableIK = true;
        data.enableDanceSwitch = false;
        data.enableRandomMessages = false;
        data.enableHusbandoMode = false;
        data.enableAutoMemoryTrim = false;
        data.enableFeedSystem = false;
        data.enableMinecraftMessages = false;
        data.enableRandomAvatar = false;
        data.useLegacyMoveResizeCalls = false;
        data.verboseDiscordRPCLog = false;
        data.windowType = WindowType.Normal;
        data.useKWinApi = false;
        
        SaveLoadHandler.Instance.SaveToDisk();
        ApplySettings();
    }

    private void Save()
    {
        SaveLoadHandler.Instance.SaveToDisk();
        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
    }
}