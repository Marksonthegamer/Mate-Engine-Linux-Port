using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsHandlerLights : MonoBehaviour
{
    [System.Serializable]
    public class LightControlEntry
    {
        public string lightID;
        public Slider intensitySlider;
        public Slider saturationSlider;
        public Slider hueSlider;
        public float defaultIntensity;
        public float defaultSaturation;
        public float defaultHue;
    }

    [System.Serializable]
    public class LightToggleEntry
    {
        public string activeID;
        public string nonActiveID;
        public Toggle checkmark;
    }

    public List<LightControlEntry> lights = new List<LightControlEntry>();
    public List<LightToggleEntry> lightToggles = new List<LightToggleEntry>();
    public ColorController colorController;

    private void Start()
    {
        for (int i = 0; i < lights.Count; i++)
        {
            int idx = i;
            var entry = lights[i];
            entry.defaultIntensity = entry.intensitySlider.value;
            entry.defaultSaturation = entry.saturationSlider.value;
            entry.defaultHue = entry.hueSlider.value;

            entry.intensitySlider.onValueChanged.AddListener((v) => {
                SaveLoadHandler.Instance.data.lightIntensities[entry.lightID] = v;
                OnLightSliderChanged(idx);
                Save();
            });
            entry.saturationSlider.onValueChanged.AddListener((v) => {
                SaveLoadHandler.Instance.data.lightSaturations[entry.lightID] = v;
                OnLightSliderChanged(idx);
                Save();
            });
            entry.hueSlider.onValueChanged.AddListener((v) => {
                SaveLoadHandler.Instance.data.lightHues[entry.lightID] = v;
                OnLightSliderChanged(idx);
                Save();
            });
        }

        for (int i = 0; i < lightToggles.Count; i++)
        {
            int idx = i;
            var entry = lightToggles[i];
            if (entry.checkmark != null)
            {
                entry.checkmark.onValueChanged.AddListener((v) => {
                    if (!string.IsNullOrEmpty(entry.activeID))
                        SaveLoadHandler.Instance.data.groupToggles[entry.activeID] = v;
                    OnLightToggleChanged(idx, v);
                    Save();
                });
            }
        }

        LoadSettings();
        ApplySettings();
    }

    public void LoadSettings()
    {
        var data = SaveLoadHandler.Instance.data;

        for (int i = 0; i < lights.Count; i++)
        {
            var entry = lights[i];
            if (!string.IsNullOrEmpty(entry.lightID))
            {
                if (data.lightIntensities.TryGetValue(entry.lightID, out float iVal)) entry.intensitySlider.SetValueWithoutNotify(iVal);
                if (data.lightSaturations.TryGetValue(entry.lightID, out float sVal)) entry.saturationSlider.SetValueWithoutNotify(sVal);
                if (data.lightHues.TryGetValue(entry.lightID, out float hVal)) entry.hueSlider.SetValueWithoutNotify(hVal);
            }
            OnLightSliderChanged(i);
        }

        for (int i = 0; i < lightToggles.Count; i++)
        {
            var entry = lightToggles[i];
            if (!string.IsNullOrEmpty(entry.activeID) && entry.checkmark != null)
            {
                bool toggleState = false;
                if (data.groupToggles.TryGetValue(entry.activeID, out bool state)) toggleState = state;
                entry.checkmark.SetIsOnWithoutNotify(toggleState);
                OnLightToggleChanged(i, toggleState);
            }
        }
    }

    public void ApplySettings()
    {
        for (int i = 0; i < lights.Count; i++)
            OnLightSliderChanged(i);
        for (int i = 0; i < lightToggles.Count; i++)
        {
            var entry = lightToggles[i];
            OnLightToggleChanged(i, entry.checkmark != null && entry.checkmark.isOn);
        }
    }

    public void ResetLightToDefault(int idx)
    {
        var entry = lights[idx];
        entry.intensitySlider.value = entry.defaultIntensity;
        entry.saturationSlider.value = entry.defaultSaturation;
        entry.hueSlider.value = entry.defaultHue;
        OnLightSliderChanged(idx);
    }

    public void ResetAllLightsToDefault()
    {
        for (int i = 0; i < lights.Count; i++)
        {
            var entry = lights[i];
            entry.intensitySlider.value = entry.defaultIntensity;
            entry.saturationSlider.value = entry.defaultSaturation;
            entry.hueSlider.value = entry.defaultHue;

            if (!string.IsNullOrEmpty(entry.lightID))
            {
                SaveLoadHandler.Instance.data.lightIntensities[entry.lightID] = entry.defaultIntensity;
                SaveLoadHandler.Instance.data.lightSaturations[entry.lightID] = entry.defaultSaturation;
                SaveLoadHandler.Instance.data.lightHues[entry.lightID] = entry.defaultHue;
            }
        }
        SaveLoadHandler.Instance.SaveToDisk();
    }

    public void ResetAllLightTogglesToDefault()
    {
        for (int i = 0; i < lightToggles.Count; i++)
        {
            var entry = lightToggles[i];
            if (entry.checkmark != null)
            {
                entry.checkmark.SetIsOnWithoutNotify(false);
                OnLightToggleChanged(i, false);
            }
            if (!string.IsNullOrEmpty(entry.activeID))
                SaveLoadHandler.Instance.data.groupToggles[entry.activeID] = false;
        }
        SaveLoadHandler.Instance.SaveToDisk();
    }


    private void OnLightSliderChanged(int idx)
    {
        var entry = lights[idx];
        if (colorController == null) return;
        var target = colorController.targets.Find(t => t.id == entry.lightID);
        if (target != null)
        {
            target.intensity = entry.intensitySlider.value;
            target.saturation = entry.saturationSlider.value;
            target.hue = entry.hueSlider.value;
        }
    }

    private void OnLightToggleChanged(int idx, bool state)
    {
        var entry = lightToggles[idx];
        if (colorController == null) return;
        colorController.SetGroupEnabled(entry.activeID, state);
        colorController.SetGroupEnabled(entry.nonActiveID, !state);
    }

    private void Save()
    {
        SaveLoadHandler.Instance.SaveToDisk();
    }
}
