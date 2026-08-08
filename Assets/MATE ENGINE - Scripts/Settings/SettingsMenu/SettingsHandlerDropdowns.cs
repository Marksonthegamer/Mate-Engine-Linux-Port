using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using ollama;

public class SettingsHandlerDropdowns : MonoBehaviour
{
    public TMP_Dropdown graphicsDropdown;
    public TMP_Dropdown contextLengthDropdown;
    public TMP_Dropdown ollamaModelDropdown;

    [System.Serializable]
    public class ParticleThemeEntry
    {
        public string id = "Standard";
        public string display = "Standard";
    }

    [Header("Particle Theme")]
    public TMP_Dropdown particleDropdown;
    public List<ParticleThemeEntry> particleThemes = new List<ParticleThemeEntry>();

    public LLMUnity.LLM llm;

    private readonly int[] contextOptions = { 2048, 4096, 8192, 16384, 32768 };
    private readonly List<string> ollamaModelOptions = new();

    async void Start()
    {
        if (graphicsDropdown != null)
        {
            graphicsDropdown.ClearOptions();
            graphicsDropdown.AddOptions(new List<string> { "ULTRA", "VERY HIGH", "HIGH", "NORMAL", "LOW" });
            graphicsDropdown.onValueChanged.AddListener(OnGraphicsChanged);
        }

        if (contextLengthDropdown != null)
        {
            contextLengthDropdown.ClearOptions();
            var labels = new List<string>();
            foreach (int c in contextOptions) labels.Add($"{c / 1024}K");
            contextLengthDropdown.AddOptions(labels);
            contextLengthDropdown.onValueChanged.AddListener(OnContextChanged);
        }

        if (ollamaModelDropdown != null)
        {
            ollamaModelDropdown.ClearOptions();
            var models = await Ollama.List();
            if (models == null || models.Length == 0)
            {
                Debug.LogWarning("No Ollama models found.");
                ollamaModelDropdown.AddOptions(new List<string> { "N/A" });
                return;
            }
            foreach (var model in models)
            {
                ollamaModelDropdown.AddOptions(new List<string> { model.name });
                ollamaModelOptions.Add(model.name);
            }
            ollamaModelDropdown.onValueChanged.AddListener(OnOllamaModelChanged);
            if (!string.IsNullOrEmpty(SaveLoadHandler.Instance.data.ollamaModel))
                ollamaModelDropdown.value = ollamaModelOptions.IndexOf(SaveLoadHandler.Instance.data.ollamaModel);
            else SaveLoadHandler.Instance.data.ollamaModel = ollamaModelOptions[0];
        }

        if (particleDropdown != null)
        {
            BuildParticleDropdown();
            particleDropdown.onValueChanged.AddListener(OnParticleChanged);
        }

        LoadSettings();
        ApplySettings();
    }

    void BuildParticleDropdown()
    {
        if (particleDropdown == null) return;

        if (particleThemes == null) particleThemes = new List<ParticleThemeEntry>();
        if (particleThemes.Count == 0)
            particleThemes.Add(new ParticleThemeEntry { id = "Standard", display = "Standard" });

        var options = new List<string>();
        for (int i = 0; i < particleThemes.Count; i++)
        {
            var d = string.IsNullOrWhiteSpace(particleThemes[i].display) ? particleThemes[i].id : particleThemes[i].display;
            options.Add(d);
        }

        particleDropdown.ClearOptions();
        particleDropdown.AddOptions(options);

        string sel = SaveLoadHandler.Instance.data.selectedParticleTheme;
        int idx = Mathf.Max(0, particleThemes.FindIndex(e => e.id == sel));
        particleDropdown.SetValueWithoutNotify(idx);
    }

    void OnParticleChanged(int index)
    {
        if (particleThemes == null || index < 0 || index >= particleThemes.Count) return;
        SaveLoadHandler.Instance.data.selectedParticleTheme = particleThemes[index].id;
        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
        SaveLoadHandler.Instance.SaveToDisk();
    }

    void OnGraphicsChanged(int index)
    {
        SaveLoadHandler.Instance.data.graphicsQualityLevel = index;
        QualitySettings.SetQualityLevel(index, true);
        SaveLoadHandler.Instance.SaveToDisk();
    }

    void OnContextChanged(int index)
    {
        if (llm != null) llm.contextSize = contextOptions[index];
        SaveLoadHandler.Instance.data.contextLength = contextOptions[index];
        SaveLoadHandler.Instance.SaveToDisk();
    }
    
    void OnOllamaModelChanged(int index)
    {
        SaveLoadHandler.Instance.data.ollamaModel = ollamaModelOptions[index];
        SaveLoadHandler.Instance.SaveToDisk();
    }

    public void LoadSettings()
    {
        var data = SaveLoadHandler.Instance.data;

        graphicsDropdown?.SetValueWithoutNotify(data.graphicsQualityLevel);
        QualitySettings.SetQualityLevel(data.graphicsQualityLevel, true);

        int currentContext = data.contextLength > 0 ? data.contextLength : 4096;
        int index = System.Array.IndexOf(contextOptions, currentContext);
        if (index < 0) index = 1;
        contextLengthDropdown?.SetValueWithoutNotify(index);
        if (llm != null) llm.contextSize = contextOptions[index];

        if (particleDropdown != null) BuildParticleDropdown();
    }

    public void ApplySettings()
    {
        var data = SaveLoadHandler.Instance.data;

        data.graphicsQualityLevel = graphicsDropdown?.value ?? data.graphicsQualityLevel;
        QualitySettings.SetQualityLevel(data.graphicsQualityLevel, true);

        if (contextLengthDropdown != null)
        {
            int index = contextLengthDropdown.value;
            data.contextLength = contextOptions[index];
            if (llm != null) llm.contextSize = data.contextLength;
        }

        if (particleDropdown != null)
        {
            int idx = Mathf.Clamp(particleDropdown.value, 0, particleThemes.Count - 1);
            if (particleThemes.Count > 0) data.selectedParticleTheme = particleThemes[idx].id;
        }

        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
        SaveLoadHandler.Instance.SaveToDisk();
    }

    public void ResetToDefaults()
    {
        graphicsDropdown?.SetValueWithoutNotify(1);
        QualitySettings.SetQualityLevel(1, true);
        SaveLoadHandler.Instance.data.graphicsQualityLevel = 1;

        int defaultIndex = 1;
        contextLengthDropdown?.SetValueWithoutNotify(defaultIndex);
        SaveLoadHandler.Instance.data.contextLength = contextOptions[defaultIndex];
        if (llm != null) llm.contextSize = contextOptions[defaultIndex];

        SaveLoadHandler.Instance.data.selectedParticleTheme = "Standard";
        if (particleDropdown != null) BuildParticleDropdown();

        SaveLoadHandler.ApplyAllSettingsToAllAvatars();
        SaveLoadHandler.Instance.SaveToDisk();
    }
}
