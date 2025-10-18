using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using PulseAudio;

public class AllowedAppsManager : MonoBehaviour
{
    public TMP_Dropdown runningAppsDropdown;
    public Button addToAllowedListButton;
    public Transform allowedAppsListContent;
    public GameObject allowedAppItemPrefab;
    
    private List<string> _currentRunningAppNames = new List<string>();
    private List<string> AllowedApps => SaveLoadHandler.Instance.data.allowedApps;

    private void Start()
    {
        addToAllowedListButton.onClick.AddListener(() =>
        {
            if (runningAppsDropdown.options.Count == 0) return;

            string selectedApp = runningAppsDropdown.options[runningAppsDropdown.value].text;
            if (!AllowedApps.Contains(selectedApp))
            {
                AllowedApps.Add(selectedApp);
                UpdateAllowedListUI();
                StartCoroutine(RefreshRunningAppsDropdown());
                SaveLoadHandler.Instance.SaveToDisk();
                SaveLoadHandler.SyncAllowedAppsToAllAvatars();
            }

        });

        StartCoroutine(RefreshRunningAppsDropdown());
        UpdateAllowedListUI();
        SaveLoadHandler.SyncAllowedAppsToAllAvatars(); // Initial sync on load
    }
    
    
    private IEnumerator RefreshRunningAppsDropdown()
    {
        while (!PulseAudioManager.Instance.allSet || PulseAudioManager.Instance.callbackRunning)
        {
            yield return null;
        }
        StartCoroutine(GetRunningAudioAppNames(apps =>
        {
            _currentRunningAppNames = apps;
            var filteredAppNames = _currentRunningAppNames
                .Where(app => !AllowedApps.Contains(app))
                .OrderBy(app => app)
                .ToList();

            runningAppsDropdown.ClearOptions();
            runningAppsDropdown.AddOptions(
                filteredAppNames.Select(app => new TMP_Dropdown.OptionData(app)).ToList()
            );

            // Reset dropdown index if empty
            if (filteredAppNames.Count == 0)
                runningAppsDropdown.value = 0;
        }));
    }

    public void OnDropdownOpened()
    {
        StartCoroutine(RefreshRunningAppsDropdown());
    }

    private void UpdateAllowedListUI()
    {
        foreach (Transform child in allowedAppsListContent)
            Destroy(child.gameObject);

        foreach (var app in AllowedApps)
        {
            var item = Instantiate(allowedAppItemPrefab, allowedAppsListContent);

            var label = item.GetComponentsInChildren<TextMeshProUGUI>()
                            .FirstOrDefault(t => t.transform.parent == item.transform);
            if (label) label.text = app;

            var button = item.transform.Find("Button")?.GetComponent<Button>();
            if (button)
            {
                button.onClick.AddListener(() =>
                {
                    AllowedApps.Remove(app);
                    UpdateAllowedListUI();
                    SaveLoadHandler.Instance.SaveToDisk();
                    SaveLoadHandler.SyncAllowedAppsToAllAvatars();
                });
            }
        }
    }

    private IEnumerator GetRunningAudioAppNames(Action<List<string>> onComplete)
    {
        var appNames = new List<string>();
        List<AudioProgram> audioPrograms = new();
        bool isComplete = false;
        PulseAudioManager.Instance.GetPlayingAudioPrograms(programs =>
        {
            audioPrograms = programs;
            isComplete = true;
        });
        while (!isComplete)
        {
            yield return null;
        }
        for (int i = 0, count = audioPrograms.Count; i < count; i++)
        {
            appNames.Add(audioPrograms[i].ProcessName);
        }
        onComplete?.Invoke(appNames.OrderBy(n => n).ToList());
    }
    
    public void RefreshAppListOnMenuOpen()
    {
        StartCoroutine(RefreshRunningAppsDropdown());
        UpdateAllowedListUI();
        SaveLoadHandler.SyncAllowedAppsToAllAvatars();
    }

    public void RefreshUI()
    {
        StartCoroutine(RefreshRunningAppsDropdown());
        UpdateAllowedListUI();
        SaveLoadHandler.SyncAllowedAppsToAllAvatars();
    }
}
