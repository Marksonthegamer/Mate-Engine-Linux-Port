using System;
using System.Collections.Generic;
using UnityEngine;

public interface  IWindowManagerImplementation
{
    bool IsDragging { get; set; }
    void SetWindowPosition(Vector2Int position);
    void SetWindowSize(Vector2Int size);
    Vector2Int GetWindowPosition();
    int GetWindowPid(IntPtr window);
    Vector2Int GetMousePosition();
    List<IntPtr> FindWindowsByPid(int targetPid);
    List<IntPtr> GetAllVisibleWindows();
    bool IsWindowVisible(IntPtr window);
    bool IsWindowFullscreen(IntPtr window);
    bool IsWindowMaximized(IntPtr window);
    Vector2Int GetWindowSize(IntPtr window);
    Vector2Int GetTotalDisplaySize();
    RectInt GetWindowRect(IntPtr window);
    List<IntPtr> GetClientStackingList();
    List<(IntPtr Id, RectInt Rect)> GetAllMonitors();
    bool IsDesktop(IntPtr window);
    bool IsDock(IntPtr window);
    string GetClassName(IntPtr window);
    void SetTopmost(bool topmost);
    void HideFromTaskbar(bool reallyHide);
    void SetWindowBorderless();
    void SetWindowType(WindowType type);
    void SetXUnityWindow(IntPtr unityWindow);
}