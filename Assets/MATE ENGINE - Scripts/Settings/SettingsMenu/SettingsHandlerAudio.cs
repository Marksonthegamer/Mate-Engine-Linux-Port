using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsHandlerAudio : MonoBehaviour
{
    public Slider petVolumeSlider;
    public Slider effectsVolumeSlider;
    public Slider menuVolumeSlider;

    public List<AudioSource> petAudioSources = new List<AudioSource>();
    public List<AudioSource> effectsAudioSources = new List<AudioSource>();
    public List<AudioSource> menuAudioSources = new List<AudioSource>();

    private Dictionary<AudioSource, float> baseVolumes = new Dictionary<AudioSource, float>();

    private void Start()
    {
        SetupListeners();
        LoadSettings();
        UpdateAllCategoryVolumes();
    }

    public void SetupListeners()
    {
        petVolumeSlider?.onValueChanged.AddListener(v => { SaveLoadHandler.Instance.data.petVolume = v; UpdateAllCategoryVolumes(); Save(); });
        effectsVolumeSlider?.onValueChanged.AddListener(v => { SaveLoadHandler.Instance.data.effectsVolume = v; UpdateAllCategoryVolumes(); Save(); });
        menuVolumeSlider?.onValueChanged.AddListener(v => { SaveLoadHandler.Instance.data.menuVolume = v; UpdateAllCategoryVolumes(); Save(); });
    }

    public void LoadSettings()
    {
        var data = SaveLoadHandler.Instance.data;
        petVolumeSlider?.SetValueWithoutNotify(data.petVolume);
        effectsVolumeSlider?.SetValueWithoutNotify(data.effectsVolume);
        menuVolumeSlider?.SetValueWithoutNotify(data.menuVolume);
        UpdateAllCategoryVolumes();
    }

    public void ApplySettings()
    {
        var data = SaveLoadHandler.Instance.data;
        data.petVolume = petVolumeSlider?.value ?? data.petVolume;
        data.effectsVolume = effectsVolumeSlider?.value ?? data.effectsVolume;
        data.menuVolume = menuVolumeSlider?.value ?? data.menuVolume;
        UpdateAllCategoryVolumes();
    }

    public void ResetToDefaults()
    {
        petVolumeSlider?.SetValueWithoutNotify(1f);
        effectsVolumeSlider?.SetValueWithoutNotify(1f);
        menuVolumeSlider?.SetValueWithoutNotify(1f);

        var data = SaveLoadHandler.Instance.data;
        data.petVolume = 1f;
        data.effectsVolume = 1f;
        data.menuVolume = 1f;

        UpdateAllCategoryVolumes();
        SaveLoadHandler.Instance.SaveToDisk();
    }


    private void UpdateAllCategoryVolumes()
    {
        float petVolume = petVolumeSlider?.value ?? 1f;
        float effectsVolume = effectsVolumeSlider?.value ?? 1f;
        float menuVolume = menuVolumeSlider?.value ?? 1f;

        foreach (var src in petAudioSources) if (src != null) src.volume = GetBaseVolume(src) * petVolume;
        foreach (var src in effectsAudioSources) if (src != null) src.volume = GetBaseVolume(src) * effectsVolume;
        foreach (var src in menuAudioSources) if (src != null) src.volume = GetBaseVolume(src) * menuVolume;
    }

    private float GetBaseVolume(AudioSource src)
    {
        if (src == null) return 1f;
        if (!baseVolumes.TryGetValue(src, out float baseVol))
        {
            baseVol = src.volume;
            baseVolumes[src] = baseVol;
        }
        return baseVol;
    }

    private void Save()
    {
        SaveLoadHandler.Instance.SaveToDisk();
    }
}
