using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FPSLimiter : MonoBehaviour
{
    [Range(15, 165)]
    public int targetFPS = 60;

    [Header("UI References")]
    public Slider fpsSlider;
    public TextMeshProUGUI fpsLabel;

    private int previousFPS;

    void Start()
    {
        // Load saved FPS limit
        targetFPS = PlayerPrefs.GetInt("FPSLimit", targetFPS);

        // Setup slider
        if (fpsSlider)
        {
            fpsSlider.minValue = 15;
            fpsSlider.maxValue = 165;
            fpsSlider.value = targetFPS;
            fpsSlider.onValueChanged.AddListener(SetFPSLimit);
        }

        ApplyFPSLimit();
        UpdateFPSLabel(targetFPS);
    }

    void Update()
    {
        if (targetFPS != previousFPS)
        {
            ApplyFPSLimit();
        }
    }

    public void ApplyFPSLimit()
    {
        Application.targetFrameRate = targetFPS;
        QualitySettings.vSyncCount = 0;
        previousFPS = targetFPS;

        PlayerPrefs.SetInt("FPSLimit", targetFPS);
        PlayerPrefs.Save();

        UpdateFPSLabel(targetFPS);
        Debug.Log("FPS set to: " + targetFPS);
    }

    public void SetFPSLimit(float fps)
    {
        targetFPS = Mathf.RoundToInt(Mathf.Clamp(fps, 15, 165));
        ApplyFPSLimit();
    }

    private void UpdateFPSLabel(int fpsValue)
    {
        if (fpsLabel)
        {
            fpsLabel.text = $"{fpsValue}";
        }
    }
}