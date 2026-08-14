using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;


public class SettingsMenuPosition : MonoBehaviour
{
    [Serializable]
    public class MenuEntry
    {
        public RectTransform settingsMenu;
        [HideInInspector] public float originalX;
        [HideInInspector] public float originalY;
        [HideInInspector] public Vector2 lastApplied;
    }

    [Header("Menus to track")]
    public List<MenuEntry> menus = new();

    [Header("Edge margin in Pixels")]
    public float edgeMargin = 50f;

    [Header("Checks per second")]
    public float checkFPS = 20f;

    [Header("Monitor refresh (sec)")]
    public float monitorRefreshInterval = 2f;

    private List<RectInt> monitorRects = new();
    private float checkTimer;
    private float monitorTimer;
    private bool lastAtRightEdge;
    private bool initedEdge;

    void Start()
    {
        if (WindowManager.Instance == null)
        {
            Debug.LogError("WindowManager.Instance is null. SettingsMenuPosition requires WindowManager to be present.");
            enabled = false;
            return;
        }

        RefreshMonitors();
        foreach (var menu in menus)
        {
            if (!menu.settingsMenu) continue;
            menu.originalX = menu.settingsMenu.anchoredPosition.x;
            menu.originalY = menu.settingsMenu.anchoredPosition.y;
            menu.lastApplied = menu.settingsMenu.anchoredPosition;
        }
        initedEdge = false;
    }

    void Update()
    {
        if (WindowManager.Instance == null || WindowManager.Instance.Display == IntPtr.Zero) return;

        monitorTimer += Time.unscaledDeltaTime;
        if (monitorTimer >= Mathf.Max(0.1f, monitorRefreshInterval))
        {
            monitorTimer = 0f;
            RefreshMonitors();
        }

        checkTimer += Time.unscaledDeltaTime;
        float step = 1f / Mathf.Max(1f, checkFPS);
        if (checkTimer < step) return;
        checkTimer = 0f;

        if (!WindowManager.Instance.GetWindowRect(WindowManager.Instance.UnityWindow, out var winRect)) return;

        RectInt screen = monitorRects.Count > 0 ? GetBestMonitor(winRect) : new RectInt(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);

        bool atRightEdge = winRect.x + winRect.width >= (screen.x + screen.width - edgeMargin);
        if (!initedEdge) { lastAtRightEdge = atRightEdge; initedEdge = true; }

        if (atRightEdge != lastAtRightEdge)
        {
            lastAtRightEdge = atRightEdge;
            for (int i = 0; i < menus.Count; i++)
            {
                var m = menus[i];
                if (!m.settingsMenu) continue;
                Vector2 target = new Vector2(atRightEdge ? -m.originalX : m.originalX, m.originalY);
                if (m.lastApplied != target)
                {
                    m.settingsMenu.anchoredPosition = target;
                    m.lastApplied = target;
                }
            }
        }
    }

    void RefreshMonitors()
    {
        WindowManager.Instance.QueryMonitors();
        monitorRects = WindowManager.Instance.GetAllMonitors().Values.ToList();
    }

    RectInt GetBestMonitor(RectInt win)
    {
        int idx = 0;
        float maxArea = 0;
        for (int i = 0; i < monitorRects.Count; i++)
        {
            float a = OverlapArea(win, monitorRects[i]);
            if (a > maxArea) { maxArea = a; idx = i; }
        }
        return monitorRects[idx];
    }

    int OverlapArea(RectInt a, RectInt b)
    {
        int x1 = Mathf.Max(a.x, b.x);
        int x2 = Mathf.Min(a.x + a.width, b.x + b.width);
        int y1 = Mathf.Max(a.y, b.y);
        int y2 = Mathf.Min(a.y + a.height, b.y + b.height);
        int w = x2 - x1;
        int h = y2 - y1;
        return (w > 0 && h > 0) ? w * h : 0;
    }
}