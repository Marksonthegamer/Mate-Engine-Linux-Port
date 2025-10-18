using UnityEngine;
using System;
using System.Collections.Generic;
using X11;

public class AvatarWindowHandler : MonoBehaviour
{
    public int snapThreshold = 30, verticalOffset = 0;
    public float desktopScale = 1f;
    [Header("Pink Snap Zone (Unity-side)")]
    public Vector2 snapZoneOffset = new(0, -5);
    public Vector2 snapZoneSize = new(100, 10);[Header("Window Sit BlendTree")]
    public int totalWindowSitAnimations = 4;
    private static readonly int windowSitIndexParam = Animator.StringToHash("WindowSitIndex");
    private bool _wasSitting = false;

    [Header("User Y-Offset Slider")]
    [Range(-0.015f, 0.015f)]
    public float windowSitYOffset = 0f;

    [Header("Fine-Tune")]
    float snapFraction;
    public float baseOffset = 40f;
    public float baseScale = 1f;

    IntPtr snappedHWND = IntPtr.Zero, unityHWND = IntPtr.Zero;
    Vector2 snapOffset;
    Vector2 lastDesktopPosition;
    readonly List<WindowEntry> cachedWindows = new();
    Rect pinkZoneDesktopRect;
// float snapFraction, baseScale = 1f, baseOffset = 40f;
    Animator animator;
    AvatarAnimatorController controller;

    private float lastCacheUpdateTime = 0f;
    private const float CacheUpdateCooldown = 0.05f; // Optional cooldown during dragging

    void Start()
    {
        unityHWND = X11Manager.Instance.UnityWindow;
        animator = GetComponent<Animator>();
        controller = GetComponent<AvatarAnimatorController>();
        SetTopMost(true);
    }
    void Update()
    {
        if (unityHWND == IntPtr.Zero || animator == null || controller == null) return;
        if (!SaveLoadHandler.Instance.data.enableWindowSitting) return;

        bool isSittingNow = animator != null && animator.GetBool("isWindowSit");
        if (isSittingNow && !_wasSitting)
        {
            int sitIdx = UnityEngine.Random.Range(0, totalWindowSitAnimations);
            animator.SetFloat(windowSitIndexParam, sitIdx);
        }
        _wasSitting = isSittingNow;

        var unityPos = GetUnityWindowPosition();
        UpdatePinkZone(unityPos);

        if (controller.isDragging && !controller.animator.GetBool("isSitting"))
        {
            if (snappedHWND == IntPtr.Zero)
                TrySnap(unityPos);
            else if (!IsStillNearSnappedWindow())
            {
                snappedHWND = IntPtr.Zero;
                animator.SetBool("isWindowSit", false);
                SetTopMost(true);
            }
            else
                FollowSnappedWindowWhileDragging();
        }
        else if (!controller.isDragging && snappedHWND != IntPtr.Zero)
            FollowSnappedWindow();

        if (snappedHWND != IntPtr.Zero)
        {
            if (X11Manager.Instance.IsWindowMaximized(snappedHWND) || IsWindowFullscreen(snappedHWND))
            {
                MoveMateToDesktopPosition();

                snappedHWND = IntPtr.Zero;
                if (animator != null)
                {
                    animator.SetBool("isWindowSit", false);
                    animator.SetBool("isSitting", false);
                }
                SetTopMost(true);
            }
        }

        if (animator != null && animator.GetBool("isBigScreenAlarm"))
        {
            if (animator.GetBool("isWindowSit"))
            {
                animator.SetBool("isWindowSit", false);
            }
            snappedHWND = IntPtr.Zero;
            SetTopMost(true);
            return;
        }
    }
    void UpdateCachedWindows()
    {
        cachedWindows.Clear();
        var allWindows = X11Manager.Instance.GetAllWindows();
        foreach (var hWnd in allWindows)
        {
            if (!X11Manager.Instance.IsWindowVisible(hWnd)) continue;
            if (!X11Manager.Instance.GetWindowRect(hWnd, out Rect r)) continue;
            string cls = X11Manager.Instance.GetClassName(hWnd);
            bool isTaskbar = X11Manager.Instance.IsDock(hWnd);
            if (!isTaskbar)
            {
                if ((r.width - r.x) < 100 || (r.height - r.y) < 100) continue;
                if (cls.Length == 0) continue;
                if (X11Manager.Instance.IsDesktop(hWnd)) continue;
            }
            cachedWindows.Add(new WindowEntry { hwnd = hWnd, rect = r });
        }
        lastCacheUpdateTime = Time.time;
    }

    void UpdatePinkZone(Vector2 unityPos)
    {
        float cx = unityPos.x + GetUnityWindowWidth() * 0.5f + snapZoneOffset.x;
        float by = unityPos.y + GetUnityWindowHeight() + snapZoneOffset.y;
        pinkZoneDesktopRect = new Rect(cx - snapZoneSize.x * 0.5f, by, snapZoneSize.x, snapZoneSize.y);
    }

    void TrySnap(Vector2 unityWindowPosition)
    {
        if (Time.time < lastCacheUpdateTime + CacheUpdateCooldown) return; // Optional: skip if recently updated
        UpdateCachedWindows();

        foreach (var win in cachedWindows)
        {
            if (win.hwnd == unityHWND) continue;
            //float barMidX = win.rect.x + (win.rect.width - win.rect.x) / 2, barY = win.rect.y + 2;
            //var pt = new Vector2() { x = barMidX, y = barY };
            //IntPtr pointWin = X11Manager.Instance.WindowFromPoint(pt);
            //if (X11Manager.Instance.GetTopLevelParent(pointWin) != win.hwnd) continue;
            var topBar = new Rect(win.rect.x, win.rect.y, win.rect.width - win.rect.x, 5);
            if (!pinkZoneDesktopRect.Overlaps(topBar)) continue;
            lastDesktopPosition = GetUnityWindowPosition();
            snappedHWND = win.hwnd;
            float winWidth = win.rect.width - win.rect.x, unityWidth = GetUnityWindowWidth();
            float petCenterX = unityWindowPosition.x + unityWidth * 0.5f;
            snapFraction = (petCenterX - win.rect.x) / winWidth;
            snapOffset.y = GetUnityWindowHeight() + snapZoneOffset.y + snapZoneSize.y * 0.5f;
            animator.SetBool("isWindowSit", true);
            return;
        }
    }
    void FollowSnappedWindowWhileDragging()
    {
        if (!X11Manager.Instance.GetWindowRect(snappedHWND, out Rect winRect) || !X11Manager.Instance.IsWindowVisible(snappedHWND))
        {
            snappedHWND = IntPtr.Zero;
            animator.SetBool("isWindowSit", false);
            SetTopMost(true);
            return;
        }

        var unityPos = GetUnityWindowPosition();
        float winWidth = winRect.width - winRect.x, unityWidth = GetUnityWindowWidth();
        float petCenterX = unityPos.x + unityWidth * 0.5f;
        snapFraction = (petCenterX - winRect.x) / winWidth;
        float newCenterX = winRect.x + snapFraction * winWidth;
        int targetX = Mathf.RoundToInt(newCenterX - unityWidth * 0.5f);
        float yOffset = GetUnityWindowHeight() + snapZoneOffset.y + snapZoneSize.y * 0.5f;
        float scale = transform.localScale.y, scaleOffset = (baseScale - scale) * baseOffset;
        float windowSitOffset = windowSitYOffset * GetUnityWindowHeight();
        float targetY = winRect.y - (int)(yOffset + scaleOffset) + verticalOffset + Mathf.RoundToInt(windowSitOffset);
        SetUnityWindowPosition(targetX, targetY);
    }

    void FollowSnappedWindow()
    {
        if (!X11Manager.Instance.GetWindowRect(snappedHWND, out Rect winRect) || !X11Manager.Instance.IsWindowVisible(snappedHWND))
        {
            snappedHWND = IntPtr.Zero;
            animator.SetBool("isWindowSit", false);
            SetTopMost(true);
            return;
        }

        float winWidth = winRect.width - winRect.x, unityWidth = GetUnityWindowWidth();
        float newCenterX = winRect.x + snapFraction * winWidth;
        int targetX = Mathf.RoundToInt(newCenterX - unityWidth * 0.5f);
        float yOffset = GetUnityWindowHeight() + snapZoneOffset.y + snapZoneSize.y * 0.5f;
        float scale = transform.localScale.y, scaleOffset = (baseScale - scale) * baseOffset;
        float windowSitOffset = windowSitYOffset * GetUnityWindowHeight();
        float targetY = winRect.y - (int)(yOffset + scaleOffset) + verticalOffset + Mathf.RoundToInt(windowSitOffset);
        SetUnityWindowPosition(targetX, targetY);
    }

    bool IsStillNearSnappedWindow()
    {
        if (!X11Manager.Instance.GetWindowRect(snappedHWND, out Rect winRect) || !X11Manager.Instance.IsWindowVisible(snappedHWND))
        {
            return false;
        }
        return pinkZoneDesktopRect.Overlaps(new Rect(winRect.x, winRect.y, winRect.width - winRect.x, 5));
    }

    struct WindowEntry { public IntPtr hwnd; public Rect rect; }

    void SetTopMost(bool en) => X11Manager.Instance.SetTopmost(en);
    Vector2 GetUnityWindowPosition() { Vector2 r = X11Manager.Instance.GetWindowPosition(); return new Vector2(r.x, r.y); }
    int GetUnityWindowWidth() { Vector2 r = X11Manager.Instance.GetWindowSize(); return (int)r.x; }
    int GetUnityWindowHeight() { Vector2 r = X11Manager.Instance.GetWindowSize(); return (int)r.y; }
    void SetUnityWindowPosition(float x, float y) => X11Manager.Instance.SetWindowPosition(x, y);

    bool IsWindowFullscreen(IntPtr hwnd)
    {
        if (!X11Manager.Instance.GetWindowRect(hwnd, out Rect rect)) return false;

        float width = rect.width - rect.x;
        float height = rect.height - rect.y;
        int screenWidth = Display.main.systemWidth;
        int screenHeight = Display.main.systemHeight;
        int tolerance = 2; 
        return Mathf.Abs(width - screenWidth) <= tolerance && Mathf.Abs(height - screenHeight) <= tolerance;
    }
    void MoveMateToDesktopPosition()
    {
        int x = Mathf.RoundToInt(lastDesktopPosition.x);
        int y = Mathf.RoundToInt(lastDesktopPosition.y);
        SetUnityWindowPosition(x, y);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        float basePixel = 1000f / desktopScale;
        Gizmos.color = Color.magenta; DrawDesktopRect(pinkZoneDesktopRect, basePixel);
        X11Manager.Instance.GetWindowRect(unityHWND, out Rect uRect);
        Gizmos.color = Color.green; DrawDesktopRect(new Rect(uRect.x, uRect.height - 5, uRect.width - uRect.x, 5), basePixel);
        foreach (var win in cachedWindows)
        {
            if (win.hwnd == unityHWND) continue;
            float w = win.rect.width - win.rect.x, h = win.rect.height - win.rect.y;
            Gizmos.color = Color.red; DrawDesktopRect(new Rect(win.rect.x, win.rect.y, w, 5), basePixel);
            Gizmos.color = Color.yellow; DrawDesktopRect(new Rect(win.rect.x, win.rect.y, w, h), basePixel);
        }
    }

    void DrawDesktopRect(Rect r, float basePixel)
    {
        float cx = r.x + r.width * 0.5f, cy = r.y + r.height * 0.5f;
        int screenWidth = Display.main.systemWidth, screenHeight = Display.main.systemHeight;
        float unityX = (cx - screenWidth * 0.5f) / basePixel, unityY = -(cy - screenHeight * 0.5f) / basePixel;
        Vector3 worldPos = new(unityX, unityY, 0), worldSize = new(r.width / basePixel, r.height / basePixel, 0);
        Gizmos.DrawWireCube(worldPos, worldSize);
    }

    public void ForceExitWindowSitting()
    {
        snappedHWND = IntPtr.Zero;
        if (animator != null)
        {
            animator.SetBool("isWindowSit", false);
            animator.SetBool("isSitting", false);
        }
        SetTopMost(true);
    }}