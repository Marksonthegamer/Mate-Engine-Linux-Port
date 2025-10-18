using UnityEngine;
using UnityEngine.UI;

public class SettingsHandlerSliders : MonoBehaviour
{
    public Slider soundThresholdSlider;
    public Slider idleSwitchTimeSlider;
    public Slider idleTransitionTimeSlider;
    public Slider avatarSizeSlider;
    public Slider fpsLimitSlider;
    public Slider headBlendSlider;
    public Slider spineBlendSlider;
    public Slider eyeBlendSlider;
    public Slider hueShiftSlider;
    public Slider saturationSlider;
    public Slider windowSitYOffsetSlider;
    public Slider danceSwitchTimeSlider;
    public Slider danceTransitionTimeSlider;

    private void Start()
    {
        soundThresholdSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.soundThreshold = v;
            SaveAll();
        });

        idleSwitchTimeSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.idleSwitchTime = v;
            SaveAll();
        });

        idleTransitionTimeSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.idleTransitionTime = v;
            SaveAll();
        });

        avatarSizeSlider?.onValueChanged.AddListener(v => {
            SaveLoadHandler.Instance.data.avatarSize = v;
            SaveAll();
        });

        fpsLimitSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.fpsLimit = (int)v;
            foreach (var limiter in FindObjectsByType<FPSLimiter>(FindObjectsSortMode.None))
                limiter.SetFPSLimit((int)v);
            SaveAll();
        });

        headBlendSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.headBlend = v;
            SaveAll();
        });

        spineBlendSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.spineBlend = v;
            SaveAll();
        });

        eyeBlendSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.eyeBlend = v;
            SaveAll();
        });

        hueShiftSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.uiHueShift = v;
            var shifter = FindFirstObjectByType<MenuHueShift>();
            if (shifter != null) shifter.hueShift = v;
            SaveAll();
        });

        saturationSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.uiSaturation = v;
            var shifter = FindFirstObjectByType<MenuHueShift>();
            if (shifter != null) shifter.saturation = v;
            SaveAll();
        });
        windowSitYOffsetSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.windowSitYOffset = v;
            SaveAll();
        });
        danceSwitchTimeSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.danceSwitchTime = v;
            SaveAll();
        });

        danceTransitionTimeSlider?.onValueChanged.AddListener(v =>
        {
            SaveLoadHandler.Instance.data.danceTransitionTime = v;
            SaveAll();
        });


        LoadSettings();
        ApplySettings();
    }

    private void SaveAll()
    {
        SaveLoadHandler.Instance.SaveToDisk();
        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
    }

    public void LoadSettings()
    {
        var data = SaveLoadHandler.Instance.data;
        soundThresholdSlider?.SetValueWithoutNotify(data.soundThreshold);
        idleSwitchTimeSlider?.SetValueWithoutNotify(data.idleSwitchTime);
        idleTransitionTimeSlider?.SetValueWithoutNotify(data.idleTransitionTime);
        avatarSizeSlider?.SetValueWithoutNotify(data.avatarSize);
        fpsLimitSlider?.SetValueWithoutNotify(data.fpsLimit);
        headBlendSlider?.SetValueWithoutNotify(data.headBlend);
        spineBlendSlider?.SetValueWithoutNotify(data.spineBlend);
        eyeBlendSlider?.SetValueWithoutNotify(data.eyeBlend);
        hueShiftSlider?.SetValueWithoutNotify(data.uiHueShift);
        saturationSlider?.SetValueWithoutNotify(data.uiSaturation);
        windowSitYOffsetSlider?.SetValueWithoutNotify(data.windowSitYOffset);
        danceSwitchTimeSlider?.SetValueWithoutNotify(data.danceSwitchTime);
        danceTransitionTimeSlider?.SetValueWithoutNotify(data.danceTransitionTime);
    }

    public void ApplySettings()
    {
        var data = SaveLoadHandler.Instance.data;

        foreach (var limiter in FindObjectsByType<FPSLimiter>(FindObjectsSortMode.None))
            limiter.SetFPSLimit(data.fpsLimit);

        var scaleController = FindFirstObjectByType<AvatarScaleController>();
        if (scaleController != null)
            scaleController.SyncWithSlider();

        var shifter = FindFirstObjectByType<MenuHueShift>();
        if (shifter != null)
        {
            shifter.hueShift = data.uiHueShift;
            shifter.saturation = data.uiSaturation;
        }
        foreach (var handler in FindObjectsByType<AvatarWindowHandler>(FindObjectsSortMode.None))
        {
            handler.windowSitYOffset = SaveLoadHandler.Instance.data.windowSitYOffset;
        }
        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
    }

    public void ResetToDefaults()
    {
        soundThresholdSlider?.SetValueWithoutNotify(0.2f);
        idleSwitchTimeSlider?.SetValueWithoutNotify(10f);
        idleTransitionTimeSlider?.SetValueWithoutNotify(1f);
        avatarSizeSlider?.SetValueWithoutNotify(1.0f);
        fpsLimitSlider?.SetValueWithoutNotify(90);
        headBlendSlider?.SetValueWithoutNotify(0.7f);
        spineBlendSlider?.SetValueWithoutNotify(0.5f);
        eyeBlendSlider?.SetValueWithoutNotify(1.0f);
        hueShiftSlider?.SetValueWithoutNotify(0f);
        saturationSlider?.SetValueWithoutNotify(0.5f);
        windowSitYOffsetSlider?.SetValueWithoutNotify(0f);
        danceSwitchTimeSlider?.SetValueWithoutNotify(15f);
        danceTransitionTimeSlider?.SetValueWithoutNotify(2f);



        var data = SaveLoadHandler.Instance.data;
        data.soundThreshold = 0.2f;
        data.idleSwitchTime = 10f;
        data.idleTransitionTime = 1f;
        data.avatarSize = 1.0f;
        data.fpsLimit = 90;
        data.headBlend = 0.7f;
        data.spineBlend = 0.5f;
        data.eyeBlend = 1.0f;
        data.uiHueShift = 0f;
        data.uiSaturation = 0.5f;
        data.windowSitYOffset = 0f;
        data.danceSwitchTime = 15f;
        data.danceTransitionTime = 2f;

        SaveLoadHandler.Instance.SaveToDisk();
        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
        ApplySettings();
    }

}
