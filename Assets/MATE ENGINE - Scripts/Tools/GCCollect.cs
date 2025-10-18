using UnityEngine;
using UnityEngine.UI;
using System;
using UnityEngine.Profiling;

public class GCCollect : MonoBehaviour
{
    [Header("UI Button to trigger GC Clean")]
    public Button gcButton;

    [Tooltip("Enable memory logging before and after.")]
    public bool logMemoryUsage = true;

    void Start()
    {
        if (gcButton != null)
        {
            gcButton.onClick.AddListener(CleanMemory);
        }
        else
        {
            Debug.LogWarning("[GCCollect] GC Button is not assigned.");
        }
    }

    public void CleanMemory()
    {
        if (logMemoryUsage)
        {
            Debug.Log($"[GCCollect] Before - Mono: {Profiler.GetMonoUsedSizeLong() / (1024 * 1024)} MB");
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Resources.UnloadUnusedAssets();

        if (logMemoryUsage)
        {
            Debug.Log($"[GCCollect] After - Mono: {Profiler.GetMonoUsedSizeLong() / (1024 * 1024)} MB");
        }
    }
}
