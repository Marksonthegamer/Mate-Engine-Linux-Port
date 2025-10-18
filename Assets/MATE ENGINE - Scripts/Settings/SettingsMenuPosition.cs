using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

public class SettingsMenuPosition : MonoBehaviour
{
    [Serializable]
    public class MenuEntry
    {
        public RectTransform settingsMenu;
        [HideInInspector] public float originalX;
        [HideInInspector] public float originalY; 
    }

    [Header("Menus to track")]
    public List<MenuEntry> menus = new List<MenuEntry>();

    [Header("Edge margin in Pixels")]
    public float edgeMargin = 50f;

    private IntPtr unityHWND;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    void Start()
    {
        unityHWND = Process.GetCurrentProcess().MainWindowHandle;
        foreach (var menu in menus)
        {
            if (menu.settingsMenu)
            {
                menu.originalX = menu.settingsMenu.anchoredPosition.x;
                menu.originalY = menu.settingsMenu.anchoredPosition.y; 
            }
        }
    }

    void Update()
    {
        if (unityHWND == IntPtr.Zero) return;

        RECT winRect;
        if (!GetWindowRect(unityHWND, out winRect)) return;

        List<RECT> monitorRects = new List<RECT>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT lprcMonitor, IntPtr data) =>
        {
            monitorRects.Add(lprcMonitor);
            return true;
        }, IntPtr.Zero);

        int idx = 0, maxArea = 0;
        for (int i = 0; i < monitorRects.Count; i++)
        {
            int overlap = OverlapArea(winRect, monitorRects[i]);
            if (overlap > maxArea)
            {
                idx = i;
                maxArea = overlap;
            }
        }
        RECT screen = monitorRects.Count > 0 ? monitorRects[idx] : new RECT { left = 0, right = Screen.currentResolution.width };

        bool atRightEdge = winRect.right >= (screen.right - edgeMargin);

        foreach (var menu in menus)
        {
            if (!menu.settingsMenu) continue;
            menu.settingsMenu.anchoredPosition = new Vector2(
                atRightEdge ? -menu.originalX : menu.originalX,
                menu.originalY
            );
        }
    }

    int OverlapArea(RECT a, RECT b)
    {
        int x1 = Math.Max(a.left, b.left);
        int x2 = Math.Min(a.right, b.right);
        int y1 = Math.Max(a.top, b.top);
        int y2 = Math.Min(a.bottom, b.bottom);
        int w = x2 - x1;
        int h = y2 - y1;
        return (w > 0 && h > 0) ? w * h : 0;
    }
}
