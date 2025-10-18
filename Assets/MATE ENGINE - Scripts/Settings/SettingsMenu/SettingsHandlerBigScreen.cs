using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsHandlerBigScreen : MonoBehaviour
{
    public Toggle bigScreenSaverEnableToggle;
    public Slider bigScreenSaverTimeoutSlider;
    public TMP_Text bigScreenSaverTimeoutLabel;
    public Toggle bigScreenAlarmEnableToggle;
    public TMP_Dropdown bigScreenAlarmHourDropdown;
    public TMP_Dropdown bigScreenAlarmMinuteDropdown;
    public InputField bigScreenAlarmTextInput;

    private static readonly string[] TimeoutLabels = {
        "30s", "1 min", "5 min", "15 min", "30 min", "45 min", "1 h", "1.5 h", "2 h", "2.5 h", "3 h"
    };

    private void Start()
    {
        SetupDropdowns();
        SetupListeners();
        LoadSettings();
    }

    private void SetupDropdowns()
    {
        if (bigScreenAlarmHourDropdown != null && bigScreenAlarmHourDropdown.options.Count != 24)
        {
            bigScreenAlarmHourDropdown.ClearOptions();
            var hours = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 24; i++) hours.Add(i.ToString("D2"));
            bigScreenAlarmHourDropdown.AddOptions(hours);
        }
        if (bigScreenAlarmMinuteDropdown != null && bigScreenAlarmMinuteDropdown.options.Count != 60)
        {
            bigScreenAlarmMinuteDropdown.ClearOptions();
            var minutes = new System.Collections.Generic.List<string>();
            for (int i = 0; i < 60; i++) minutes.Add(i.ToString("D2"));
            bigScreenAlarmMinuteDropdown.AddOptions(minutes);
        }
    }

    private void SetupListeners()
    {
        bigScreenSaverEnableToggle?.onValueChanged.AddListener(OnScreenSaverEnableChanged);
        bigScreenSaverTimeoutSlider?.onValueChanged.AddListener(OnTimeoutSliderChanged);
        bigScreenAlarmHourDropdown?.onValueChanged.AddListener(OnAlarmHourChanged);
        bigScreenAlarmMinuteDropdown?.onValueChanged.AddListener(OnAlarmMinuteChanged);
        bigScreenAlarmEnableToggle?.onValueChanged.AddListener(OnAlarmEnableChanged);
        bigScreenAlarmTextInput?.onEndEdit.AddListener(OnAlarmTextChanged);
    }

    public void LoadSettings()
    {
        var data = SaveLoadHandler.Instance.data;
        bigScreenSaverEnableToggle?.SetIsOnWithoutNotify(data.bigScreenScreenSaverEnabled);
        bigScreenSaverTimeoutSlider?.SetValueWithoutNotify(data.bigScreenScreenSaverTimeoutIndex);
        if (bigScreenSaverTimeoutLabel != null && data.bigScreenScreenSaverTimeoutIndex >= 0 && data.bigScreenScreenSaverTimeoutIndex < TimeoutLabels.Length)
            bigScreenSaverTimeoutLabel.text = TimeoutLabels[data.bigScreenScreenSaverTimeoutIndex];

        bigScreenAlarmHourDropdown?.SetValueWithoutNotify(data.bigScreenAlarmHour);
        bigScreenAlarmMinuteDropdown?.SetValueWithoutNotify(data.bigScreenAlarmMinute);
        bigScreenAlarmEnableToggle?.SetIsOnWithoutNotify(data.bigScreenAlarmEnabled);
        bigScreenAlarmTextInput?.SetTextWithoutNotify(data.bigScreenAlarmText);
    }

    void OnScreenSaverEnableChanged(bool v)
    {
        SaveLoadHandler.Instance.data.bigScreenScreenSaverEnabled = v;
        Save();
    }

    void OnTimeoutSliderChanged(float v)
    {
        int idx = Mathf.Clamp(Mathf.RoundToInt(v), 0, TimeoutLabels.Length - 1);
        SaveLoadHandler.Instance.data.bigScreenScreenSaverTimeoutIndex = idx;
        if (bigScreenSaverTimeoutLabel != null)
            bigScreenSaverTimeoutLabel.text = TimeoutLabels[idx];
        Save();
    }

    void OnAlarmHourChanged(int v)
    {
        SaveLoadHandler.Instance.data.bigScreenAlarmHour = v;
        Save();
    }

    void OnAlarmMinuteChanged(int v)
    {
        SaveLoadHandler.Instance.data.bigScreenAlarmMinute = v;
        Save();
    }

    void OnAlarmEnableChanged(bool v)
    {
        SaveLoadHandler.Instance.data.bigScreenAlarmEnabled = v;
        Save();
    }

    void OnAlarmTextChanged(string text)
    {
        SaveLoadHandler.Instance.data.bigScreenAlarmText = text;
        Save();
    }

    public void ApplySettings()
    {
        var data = SaveLoadHandler.Instance.data;
        data.bigScreenScreenSaverEnabled = bigScreenSaverEnableToggle?.isOn ?? data.bigScreenScreenSaverEnabled;
        data.bigScreenScreenSaverTimeoutIndex = Mathf.RoundToInt(bigScreenSaverTimeoutSlider?.value ?? data.bigScreenScreenSaverTimeoutIndex);
        data.bigScreenAlarmHour = bigScreenAlarmHourDropdown?.value ?? data.bigScreenAlarmHour;
        data.bigScreenAlarmMinute = bigScreenAlarmMinuteDropdown?.value ?? data.bigScreenAlarmMinute;
        data.bigScreenAlarmEnabled = bigScreenAlarmEnableToggle?.isOn ?? data.bigScreenAlarmEnabled;
        data.bigScreenAlarmText = bigScreenAlarmTextInput?.text ?? data.bigScreenAlarmText;
    }

    public void ResetToDefaults()
    {
        bigScreenSaverEnableToggle?.SetIsOnWithoutNotify(false);
        bigScreenSaverTimeoutSlider?.SetValueWithoutNotify(0);
        bigScreenAlarmEnableToggle?.SetIsOnWithoutNotify(false);
        bigScreenAlarmHourDropdown?.SetValueWithoutNotify(0);
        bigScreenAlarmMinuteDropdown?.SetValueWithoutNotify(0);
        bigScreenAlarmTextInput?.SetTextWithoutNotify("");

        var data = SaveLoadHandler.Instance.data;
        data.bigScreenScreenSaverEnabled = false;
        data.bigScreenScreenSaverTimeoutIndex = 0;
        data.bigScreenAlarmEnabled = false;
        data.bigScreenAlarmHour = 0;
        data.bigScreenAlarmMinute = 0;
        data.bigScreenAlarmText = "";

        SaveLoadHandler.Instance.SaveToDisk();
    }


    private void Save()
    {
        SaveLoadHandler.Instance.SaveToDisk();
    }
}
