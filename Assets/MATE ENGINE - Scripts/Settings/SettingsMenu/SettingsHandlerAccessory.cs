using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsHandlerAccessory : MonoBehaviour
{
    [System.Serializable]
    public class AccessoryToggleEntry
    {
        public string ruleName;
        public Toggle toggle;
    }

    public List<AccessoryToggleEntry> accessoryToggleBindings = new List<AccessoryToggleEntry>();

    private void Start()
    {
        SetupListeners();
        LoadSettings();
    }

    public void SetupListeners()
    {
        foreach (var entry in accessoryToggleBindings)
        {
            if (!string.IsNullOrEmpty(entry.ruleName) && entry.toggle != null)
            {
                string key = entry.ruleName;
                // RemoveAllListeners entfernt, damit Unity-UI-Sound erhalten bleibt!
                entry.toggle.onValueChanged.AddListener((v) =>
                {
                    SaveLoadHandler.Instance.data.accessoryStates[key] = v;
                    UpdateAccessoryObjects();
                    ForceRefreshSceneObjects();
                    SaveLoadHandler.Instance.SaveToDisk();
                });
            }
        }
    }

    public void LoadSettings()
    {
        foreach (var entry in accessoryToggleBindings)
        {
            if (!string.IsNullOrEmpty(entry.ruleName) && entry.toggle != null)
            {
                bool state = false;
                SaveLoadHandler.Instance.data.accessoryStates.TryGetValue(entry.ruleName, out state);
                entry.toggle.SetIsOnWithoutNotify(state);
            }
        }
        UpdateAccessoryObjects();
        ForceRefreshSceneObjects();
    }

    public void ApplySettings()
    {
        UpdateAccessoryObjects();
        ForceRefreshSceneObjects();
    }

    public void ResetToDefaults()
    {
        foreach (var entry in accessoryToggleBindings)
        {
            if (!string.IsNullOrEmpty(entry.ruleName))
            {
                SaveLoadHandler.Instance.data.accessoryStates[entry.ruleName] = false;
                if (entry.toggle != null)
                    entry.toggle.SetIsOnWithoutNotify(false);
            }
        }

        UpdateAccessoryObjects();
        ForceRefreshSceneObjects();
        SaveLoadHandler.Instance.SaveToDisk();
    }

    private void UpdateAccessoryObjects()
    {
        foreach (var entry in accessoryToggleBindings)
        {
            bool toggleOn = entry.toggle != null && entry.toggle.isOn;
            foreach (var handler in AccessoiresHandler.ActiveHandlers)
            {
                foreach (var rule in handler.rules)
                {
                    if (rule.ruleName == entry.ruleName)
                        rule.isEnabled = toggleOn;
                }
            }
        }
    }

    private void ForceRefreshSceneObjects()
    {
        foreach (var handler in AccessoiresHandler.ActiveHandlers)
        {
            foreach (var rule in handler.rules)
            {
                if (rule.linkedObject != null)
                    rule.linkedObject.SetActive(rule.isEnabled);
            }
        }
    }
}
