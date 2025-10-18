using UnityEngine;
using TMPro;

public class SettingsHandlerDropdowns : MonoBehaviour
{
    [Header("Dropdown References")]
    public TMP_Dropdown graphicsDropdown;
    public TMP_Dropdown contextLengthDropdown;

    [Header("LLM Reference")]
    public LLMUnity.LLM llm;

    private readonly int[] contextOptions = { 2048, 4096, 8192, 16384, 32768 };

    private void Start()
    {
 
        if (graphicsDropdown != null)
        {
            graphicsDropdown.ClearOptions();
            graphicsDropdown.AddOptions(new System.Collections.Generic.List<string> {
                "ULTRA", "VERY HIGH", "HIGH", "NORMAL", "LOW"
            });
            graphicsDropdown.onValueChanged.AddListener(OnGraphicsChanged);
        }


        if (contextLengthDropdown != null)
        {
            contextLengthDropdown.ClearOptions();
            var labels = new System.Collections.Generic.List<string>();
            foreach (int c in contextOptions) labels.Add($"{c / 1024}K");
            contextLengthDropdown.AddOptions(labels);
            contextLengthDropdown.onValueChanged.AddListener(OnContextChanged);
        }

        LoadSettings();
        ApplySettings();
    }

    private void OnGraphicsChanged(int index)
    {
        SaveLoadHandler.Instance.data.graphicsQualityLevel = index;
        QualitySettings.SetQualityLevel(index, true);
        SaveLoadHandler.Instance.SaveToDisk();
    }

    private void OnContextChanged(int index)
    {
        if (llm != null)
        {
            llm.contextSize = contextOptions[index];
            Debug.Log($"[Settings] Context length changed to {llm.contextSize}");
        }

        SaveLoadHandler.Instance.data.contextLength = contextOptions[index];
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

        if (llm != null)
            llm.contextSize = contextOptions[index];
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
            if (llm != null)
                llm.contextSize = data.contextLength;
        }

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
        if (llm != null)
            llm.contextSize = contextOptions[defaultIndex];

        SaveLoadHandler.Instance.SaveToDisk();
    }
}
