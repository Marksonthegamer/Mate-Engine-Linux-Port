#pragma warning disable 0162
//#pragma warning disable 0168
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.EventSystems;
using Debug = UnityEngine.Debug;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.SceneManagement;
using System.Runtime.Remoting.Messaging;
using Unity.VisualScripting;
using X11;

public enum DesktopEnvironments
{
    Kde,
    Hyprland,
    OtherX11,
    OtherWayland,
    Unknown
}

public enum SessionTypes
{
    X11,
    Wayland,
    Unknown
}

public class WindowManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static WindowManager Instance;
    
    private DesktopEnvironments _currentDesktopEnv;
    private SessionTypes _currentSessionType;

    private Vector2Int _initialMousePos;
    private Vector2Int _initialWindowPos;
    private bool _isDragging;

    private bool _dontUpdateCursor;
    
    public bool transparentInputEnabled = true;

    public IntPtr Display => _display;

    public IntPtr RootWindow => _rootWindow;

    public IntPtr UnityWindow => _unityWindow;

    #region Unity Events

    private void OnEnable()
    {
        Instance = this;
        #if !UNITY_EDITOR
        if (Enum.TryParse(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"), true, out _currentDesktopEnv))
        {
            switch(_currentDesktopEnv)
            {
                case DesktopEnvironments.Hyprland:
                    _windowManagerImplementation = new HyprlandManager();
                    _windowManagerImplementation.SetXUnityWindow(_unityWindow);
                    break;
            }
            return;
        }
        if (!Enum.TryParse(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), true, out _currentSessionType))
        {
            _currentSessionType = SessionTypes.Unknown;
        }
        _currentDesktopEnv = _currentSessionType switch
        {
            SessionTypes.X11 => DesktopEnvironments.OtherX11,
            SessionTypes.Wayland => DesktopEnvironments.OtherWayland,
            _ => DesktopEnvironments.Unknown
        };
        #else
            _currentDesktopEnv = DesktopEnvironments.Unknown;
        #endif
    }

    IWindowManagerImplementation _windowManagerImplementation = null;

    private Vector2 _lastPos;

    private void Update()
    {
        if (_mouseOver)
            UpdateCursorState();
        if (_isDragging)
        {
            var currentMousePos = GetMousePosition();
            var delta = currentMousePos - _initialMousePos;
            var newPos = _initialWindowPos + delta;
            if (newPos == _lastPos) return;
            SetWindowPosition(newPos);
            _lastPos = newPos;
        }
    }

    private void Awake()
    {
        Init();
        var pid = Process.GetCurrentProcess().Id;
        var windows = FindWindowsByPid(pid);

        if (windows.Count > 0)
        {
            _unityWindow = windows[0]; // Typically the first is the main window
            Debug.Log($"Unity window handle: 0x{_unityWindow.ToInt64():X}");
            QueryMonitors();
#if UNITY_EDITOR
            return;
#endif
            SetWindowBorderless();
        }
        else
        {
            ShowError("No matching windows found for PID.");
        }
        Imports.XSelectInput(_display, _unityWindow, Constants.StructureNotifyMask | Constants.EnterWindowMask | Constants.LeaveWindowMask | Constants.PropertyChangeMask);
        EnableClickThroughTransparency();
        LoadCursors();
    }

    private void OnApplicationQuit() => Dispose();
    private void OnDestroy() => Dispose();

    public void OnPointerDown(PointerEventData eventData)
    {
        _initialMousePos = GetMousePosition();
        _initialWindowPos = GetWindowPosition();
        _isDragging = true;
        if (_windowManagerImplementation != null)
            _windowManagerImplementation.IsDragging = true;
        UpdateCursorState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging = false;
        if (_windowManagerImplementation != null)
            _windowManagerImplementation.IsDragging = true;
        UpdateCursorState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _dontUpdateCursor = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _dontUpdateCursor = true;
        SetCursor(IntPtr.Zero);
    }

    private void Init()
    {
#if !UNITY_EDITOR
        Imports.XInitThreads();
#endif
        // Open X11 display
        _display = Imports.XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            throw new Exception("Cannot open X11 display");
        }

        Imports.XSetErrorHandler(ShowError);
        X11Utils.RegisterExtension(_display);


        _rootWindow = Imports.XDefaultRootWindow(_display);
        
        _netWmState = Imports.XInternAtom(_display, "_NET_WM_STATE", false);
        _netWmStateFullscreen = Imports.XInternAtom(_display, "_NET_WM_STATE_FULLSCREEN", false);
        _netWmStateMaxHorz = Imports.XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_HORZ", false);
        _netWmStateMaxVert = Imports.XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_VERT", false);
        _netWmWindowType = Imports.XInternAtom(_display, "_NET_WM_WINDOW_TYPE", false);
        _netMoveResizeWindow = Imports.XInternAtom(_display, "_NET_MOVERESIZE_WINDOW", false);
        _netWmStateAbove = Imports.XInternAtom(_display, "_NET_WM_STATE_ABOVE", false);
        _netWmStateSkipTaskbar = Imports.XInternAtom(_display, "_NET_WM_STATE_SKIP_TASKBAR", false);
        _netWmWindowTypeDock = Imports.XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DOCK", false);
        _netWmWindowTypeNormal = Imports.XInternAtom(_display, "_NET_WM_WINDOW_TYPE_NORMAL", false);
        _motifHintsAtom = Imports.XInternAtom(_display, "_MOTIF_WM_HINTS", false);
        _wakeupAtom = Imports.XInternAtom(_display, "_SDL_WAKEUP", false);
    }
        
    private int ShowError(IntPtr display, IntPtr e)
    {
        ShowError(LookupError(e) ?? "???");
        return 0;
    }

    private void ShowError(string error)
    {
        Console.WriteLine($"\u001b[31m{GetType().Name}: {error}\u001b[0m");
    }

    private string LookupError(IntPtr errorEvent)
    {
        XErrorEvent error = Marshal.PtrToStructure<XErrorEvent>(errorEvent);
    
        if (_display == IntPtr.Zero) return "Display not initialized";

        var buffer = new byte[256];

        Imports.XGetErrorText(_display, error.error_code, buffer, buffer.Length);

        int count = Array.IndexOf(buffer, (byte)0);
        if (count < 0) count = buffer.Length;
    
        string message = System.Text.Encoding.ASCII.GetString(buffer, 0, count);
    
        if (string.IsNullOrEmpty(message))
        {
            return $"Unknown error code: {error.error_code}";
        }
    
        return $"{message}, Module: {X11Utils.GetRequestName(error.request_code)}, minor_code: {error.minor_code}, Resource ID: 0x{error.resourceid:X}";
    }
    
    private IntPtr _wakeupAtom;

    private void Dispose()
    {
        if (_closing) return;
        _running = false;
        _closing = true;

        if(_windowManagerImplementation is IDisposable disposable)
            disposable?.Dispose();

        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero && _wakeupAtom != IntPtr.Zero)
        {
            Imports.XCompositeUnredirectWindow(_display, _unityWindow, Constants.CompositeRedirectAutomatic);
            Imports.XStoreName(_display, _unityWindow, "Closing...");
            Imports.XFlush(_display);
        }
        if (_x11EventThread is { IsAlive: true })
        {
            _x11EventThread.Join();
        }
        if (_display != IntPtr.Zero)
        {
#if UNITY_EDITOR
            SetTopmost(false);
#endif
#if !UNITY_EDITOR
            if (_damage != IntPtr.Zero)
            {
                Imports.XDamageDestroy(_display, _damage);
                _damage = IntPtr.Zero;
            }
#endif
            if (_useShm)
            {
                Imports.XShmDetach(_display, ref _shmInfo);
                Imports.shmdt(_shmInfo.shmaddr);
            }
            Imports.XSync(_display, false);
            if (_defaultCursor != IntPtr.Zero) { Imports.XFreeCursor(_display, _defaultCursor); _defaultCursor = IntPtr.Zero; }
            if (_grabCursor != IntPtr.Zero) { Imports.XFreeCursor(_display, _grabCursor); _grabCursor = IntPtr.Zero; }
            if (_grabbingCursor != IntPtr.Zero) { Imports.XFreeCursor(_display, _grabbingCursor); _grabbingCursor = IntPtr.Zero; }
            Imports.XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }
    #endregion
    public bool GetWindowPosition(out float x, out float y)
    {
        var result = GetWindowPosition();
        if (result != Vector2.zero)
        {
            x = result.x;
            y = result.y;
            return true;
        }
        x = 0;
        y = 0;
        return false;
    }
    
        
    public Vector2Int GetWindowPosition()
    {
        if (_windowManagerImplementation != null)
            return _windowManagerImplementation.GetWindowPosition();
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            // Use XTranslateCoordinates to get absolute position
            if (Imports.XTranslateCoordinates(_display, _unityWindow, _rootWindow, 0, 0, out var absX, out var absY, out _))
            {
                return new Vector2Int(absX, absY);
            }

            ShowError("XTranslateCoordinates failed.");
        }

        return Vector2Int.zero;
    }

    public void SetWindowPosition(int x, int y)
    {
        SetWindowPosition(new Vector2Int(x, y));
    }

    public void SetWindowPosition(Vector2Int position)
    {
        if (SaveLoadHandler.Instance.data.useLegacyMoveResizeCalls)
        { 
            SetWindowPositionLegacy(position);
            return;
        }
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            if (_windowManagerImplementation != null)
            {
                _windowManagerImplementation.SetWindowPosition(position);
                return;
            }
            if (_currentDesktopEnv == DesktopEnvironments.Kde && _currentSessionType == SessionTypes.Wayland)
            {
                Singleton<KWinManager>.Instance.MoveWindow(position);
                return;
            }
            if (_netMoveResizeWindow == IntPtr.Zero)
            {
                ShowError("Cannot find atom for _NET_MOVERESIZE_WINDOW!");
                return;
            }
            var xClient = new XClientMessageEvent
            {
                type = Constants.ClientMessage,
                window = _unityWindow,
                message_type = _netMoveResizeWindow,
                format = 32,
            };
            xClient.data0 = new IntPtr((1 << 12) | (1 << 9) | (1 << 8) | 10);
            xClient.data1 = new(position.x);
            xClient.data2 = new(position.y);
            xClient.data3 = IntPtr.Zero;
            xClient.data4 = IntPtr.Zero;

            Imports.XSendEvent(_display, _rootWindow, false, Constants.SubstructureRedirectMask | Constants.SubstructureNotifyMask, ref xClient);
            Imports.XFlush(_display);
        }
    }

    private void SetWindowPositionLegacy(Vector2Int position)
    {
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            Imports.XMoveWindow(_display, _unityWindow, position.x, position.y);
            Imports.XFlush(_display);
        }
    }

    public Vector2 GetRelativeWindowPosition()
    {
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            if (GetGeometry(out _, out var x, out var y, out _, out _, out _, out _) != 0)
            {
                return new Vector2(x, y);
            }
            ShowError("Failed to get relative window geometry.");
        }
        return Vector2.zero;
    }
    
    public void SetWindowPositionMonitorRelative(int monitorIndex, Vector2Int relativePos)
    {
        if (_monitors == null || _monitors.Count == 0)
            QueryMonitors();

        if (monitorIndex < 0 || monitorIndex >= _monitors?.Count)
            return;

        if (_monitors == null) return;
        var monitorRect = _monitors.ElementAt(monitorIndex);

        var absolutePos = new Vector2Int(
            monitorRect.Value.x + relativePos.x,
            monitorRect.Value.y + relativePos.y
        );

        SetWindowPosition(absolutePos);
    }
    
    public void SetTransientFor(IntPtr parentWindow)
    {
        if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero || _closing) return;
    
        Imports.XSetTransientForHint(_display, _unityWindow, parentWindow);
        Imports.XFlush(_display);
    }
    
    public void QueryMonitors()
    {
        _monitors = new();
        _monitors.Clear();
        if(_windowManagerImplementation != null)
        {
            foreach(var m in _windowManagerImplementation.GetAllMonitors())
                _monitors[m.Id] = m.Rect;
            return;
        }

        if (_display == IntPtr.Zero) return;
            
        if (Imports.XRRQueryExtension(_display, out _, out _) == 0)
        {
            Debug.LogError("XRandR extension not available.");
            return;
        }

        if (Imports.XRRQueryVersion(_display, out var major, out var minor) == 0 || major < 1 || (major == 1 && minor < 3))
        {
            Debug.LogError("XRandR 1.3+ required for multi-monitor.");
            return;
        }

        var resHandle = Imports.XRRGetScreenResourcesCurrent(_display, _rootWindow);
        var res = Marshal.PtrToStructure<XrrScreenResources>(resHandle);
            
        if (res.noutput <= 0)
        {
            Imports.XRRFreeScreenResources(resHandle);
            return;
        }

        for (var i = 0; i < res.noutput; i++)
        {
            var output = Marshal.ReadIntPtr(res.outputs, i * IntPtr.Size);
            var outInfoHandle = Imports.XRRGetOutputInfo(_display, resHandle, output);
            var outInfo = Marshal.PtrToStructure<XrrOutputInfo>(outInfoHandle);
            if (outInfo.connection != Connection.Connected || outInfo.crtc == IntPtr.Zero)
            {
                Imports.XRRFreeOutputInfo(outInfoHandle);
                continue;
            }

            var crtcInfoHandle = Imports.XRRGetCrtcInfo(_display, resHandle, outInfo.crtc);
            var crtcInfo = Marshal.PtrToStructure<XrrCrtcInfo>(crtcInfoHandle);
            if (crtcInfo.width == 0 || crtcInfo.height == 0 || crtcInfoHandle == IntPtr.Zero)
            {
                Imports.XRRFreeCrtcInfo(crtcInfoHandle);
                Imports.XRRFreeOutputInfo(outInfoHandle);
                continue;
            }

            var monRect = new RectInt(crtcInfo.x, crtcInfo.y, (int)crtcInfo.width, (int)crtcInfo.height);
            _monitors.Add(crtcInfoHandle, monRect);

            Imports.XRRFreeCrtcInfo(crtcInfoHandle);
            Imports.XRRFreeOutputInfo(outInfoHandle);
        }

        Imports.XRRFreeScreenResources(resHandle);

        if (_monitors.Count == 0)
        {
            ShowError("No monitors were found.");
        }
    }


    private string GetWindowType(IntPtr hwnd)  // Returns type atom name or empty
    {
        if (_netWmWindowType == IntPtr.Zero) return "";
        var status = Imports.XGetWindowProperty(_display, hwnd, _netWmWindowType, 0, 1, false, (IntPtr)Constants.XaAtom, out _, out _, out var nItems, out _, out var prop);
        if (status != 0 || prop == IntPtr.Zero || nItems == 0) { if (prop != IntPtr.Zero) Imports.XFree(prop); return ""; }
        var typeAtom = Marshal.ReadIntPtr(prop);
        Imports.XFree(prop);
        // Map to string (add XGetAtomName if needed)
        return Imports.XGetAtomName(_display, typeAtom);
    }
    
    public bool GetWindowSize(out float x, out float y)
    {
        var result = GetWindowSize();
        if (result != Vector2.zero)
        {
            x = result.x;
            y = result.y;
            return true;
        }
        x = 0;
        y = 0;
        return false;
    }

    public Vector2 GetWindowSize(IntPtr window = default)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetWindowSize(window);
        if (window == IntPtr.Zero)
            window = _unityWindow;
        if (_display != IntPtr.Zero && window != IntPtr.Zero)
        {
            var result = Imports.XGetWindowAttributes(_display, window, out var attributes);
            if (result != 0) // Non-zero indicates success in X11
            {
                return new Vector2(attributes.width, attributes.height);
            }
        }

        return Vector2.zero;
    }
    
    public void SetWindowSize(int x, int y)
    {
        SetWindowSize(new Vector2Int(x, y));
    }

    public void SetWindowSize(Vector2Int size)
    {
        if (SaveLoadHandler.Instance.data.useLegacyMoveResizeCalls)
        {
            SetWindowSizeLegacy(size);
            return;
        }
        if (_windowManagerImplementation != null)
            _windowManagerImplementation.SetWindowSize(size);
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            if (_netMoveResizeWindow == IntPtr.Zero)
            {
                ShowError("Cannot find atom for _NET_MOVERESIZE_WINDOW!");
                return;
            }
            var xClient = new XClientMessageEvent
            {
                type = Constants.ClientMessage,
                window = _unityWindow,
                message_type = _netMoveResizeWindow,
                format = 32,
            };
            xClient.data0 = new IntPtr((1 << 12) | (1 << 10) | (1 << 11));
            xClient.data1 = IntPtr.Zero;
            xClient.data2 = IntPtr.Zero;
            xClient.data3 = new(size.x);
            xClient.data4 = new(size.y);
            
            Imports.XSendEvent(_display, _rootWindow, false, Constants.SubstructureRedirectMask | Constants.SubstructureNotifyMask, ref xClient);
            Imports.XFlush(_display);
        }
    }

    private void SetWindowSizeLegacy(Vector2 size)
    {
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            Imports.XResizeWindow(_display, _unityWindow, (int)size.x, (int)size.y);
            Imports.XFlush(_display);
        }
    }

    public Vector2Int GetMousePosition()
    {
        if (_windowManagerImplementation != null)
            return _windowManagerImplementation.GetMousePosition();
        if (SaveLoadHandler.Instance.data.forceKWinApi && _currentDesktopEnv == DesktopEnvironments.Kde)
        {
            return Singleton<KWinManager>.Instance.GetCursorPos().Result;
        }
        // Query mouse position
        int rootX = 0, rootY = 0;
        IntPtr rootReturn = IntPtr.Zero, childReturn = IntPtr.Zero;
        int winX = 0, winY = 0;
        uint maskReturn = 0;

        if (!Imports.XQueryPointer(_display, _rootWindow, ref rootReturn, ref childReturn, ref rootX, ref rootY, ref winX,
                ref winY, ref maskReturn))
        {
            ShowError("No mouse found.");
            return Vector2Int.zero;
        }

        return new Vector2Int(rootX, rootY);
    }
    
    public bool GetMousePosition(out Vector2Int position)
    {
        // Query mouse position
        int rootX = 0, rootY = 0;
        IntPtr rootReturn = IntPtr.Zero, childReturn = IntPtr.Zero;
        int winX = 0, winY = 0;
        uint maskReturn = 0;

        bool result = Imports.XQueryPointer(_display, _rootWindow, ref rootReturn, ref childReturn, ref rootX, ref rootY, ref winX, ref winY, ref maskReturn);
        position = new Vector2Int(rootX, rootY);
        return result;
    }
        
    public bool GetMouseButton(KeyCode button) // 0=left, 1=right, 2=middle
    {
        if (_display == IntPtr.Zero) return false;
            
        IntPtr rootReturn = IntPtr.Zero, childReturn = IntPtr.Zero;
        int winX = 0, winY = 0;
        uint mask = 0;
        int rootX = 0, rootY = 0;
        if (!Imports.XQueryPointer(_display, _rootWindow, ref rootReturn, ref childReturn, ref rootX, ref rootY, ref winX,
                ref winY, ref mask))
        {
            ShowError("No mouse found.");
        }

        return button switch
        {
            KeyCode.Mouse0 => (mask & 0x100) != 0,  // Button1Mask = 1 << 8  (= 0x100)
            KeyCode.Mouse1 => (mask & 0x400) != 0,  // Button3Mask = 1 << 10 (= 0x400) -> right
            KeyCode.Mouse2 => (mask & 0x200) != 0,  // Button2Mask = 1 << 9  (= 0x200) -> middle
            _ => false
        };
    }
        
    public bool IsAnyKeyDown()
    {
        if (_display == IntPtr.Zero) return false;

        byte[] keymap = new byte[32];

        if (Imports.XQueryKeymap(_display, keymap) == 0)
            return false;

        // Check keycodes 8 to 255 (skip 0–7 which are usually unused)
        for (int i = 1; i < 32; i++)        // bytes 1–31 -> keycodes 8–255
        {
            if (keymap[i] != 0)             // any bit set = key down
                return true;
        }
        return false;
    }
        
    public RectInt GetMonitorRectFromPoint(Vector2Int point)
    {
        foreach (var mon in _monitors.Values)
        {
            if (mon.Contains(point)) return mon;
        }
        return RectInt.zero;
    }

    public IntPtr GetMonitorFromWindow(IntPtr window = default)
    {
        var monRect = GetMonitorRectFromWindow(window);
        foreach (var kvp in _monitors.Where(kvp => kvp.Value == monRect))
        {
            return kvp.Key;
        }
        
        throw new KeyNotFoundException($"No such monitor includes the center point of that window.");
    }
        
    public RectInt GetMonitorRectFromWindow(IntPtr window = default)
    {
        if (!GetWindowRect(window, out var winRect)) return new RectInt();
        var center = new Vector2Int(winRect.x + winRect.width / 2, winRect.y + winRect.height / 2);
        var resultBasedOnWindowCenterPnt = GetMonitorRectFromPoint(center);
        if (resultBasedOnWindowCenterPnt == RectInt.zero)
        {
            Dictionary<float, RectInt> overlapMonitors = new();
            foreach (var mon in _monitors.Values)
            {
                if (mon.Overlaps(winRect))
                {
                    float xMin = Mathf.Max(mon.xMin, winRect.xMin);
                    float yMin = Mathf.Max(mon.yMin, winRect.yMin);
                    float xMax = Mathf.Min(mon.xMax, winRect.xMax);
                    float yMax = Mathf.Min(mon.yMax, winRect.yMax);

                    float overlapWidth = Mathf.Max(0f, xMax - xMin);
                    float overlapHeight = Mathf.Max(0f, yMax - yMin);
                    overlapMonitors.Add(overlapWidth * overlapHeight, mon);
                }
            }

            return overlapMonitors[overlapMonitors.Keys.Max()];
        }
        return resultBasedOnWindowCenterPnt;
    }
    
    public RectInt GetMonitorRectFromHandle(IntPtr monitor)
    {
        foreach (var kvp in _monitors.Where(kvp => kvp.Key == monitor))
        {
            return kvp.Value;
        }

        throw new KeyNotFoundException($"No such monitor with that handle.");
    }

    public Vector2 GetTotalDisplaySize()
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetTotalDisplaySize();
        Imports.XGetWindowAttributes(_display, _unityWindow, out var attr);
        return new Vector2(attr.width, attr.height);
    }
        
    public Dictionary<IntPtr, RectInt> GetAllMonitors() => new(_monitors); // Copy to prevent external modification

    private List<IntPtr> FindWindowsByPid(int targetPid)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.FindWindowsByPid(targetPid);
        var result = new List<IntPtr>();

        var windows = GetAllVisibleWindows();

        foreach (var window in windows)
        {
            var pid = GetWindowPid(window);
            if (pid == targetPid)
            {
                result.Add(window);
            }
        }

        return result;
    }

    public int GetWindowPid(string kWinUuid)
    {
        if (_currentDesktopEnv == DesktopEnvironments.Kde)
        {
            return Singleton<KWinManager>.Instance.GetWindowPid(kWinUuid).Result;
        }
        
        ShowError("The argument is passed as a string which is supposed to be a UUID of a window managed by KWin.\nHowever KWin/KDE is not detected.");
        return -1;
    }

    public int GetWindowPid(IntPtr window)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetWindowPid(window);
        var pidAtom = Imports.XInternAtom(_display, "_NET_WM_PID", false);

        if (pidAtom == IntPtr.Zero)
        {
            Debug.Log("_NET_WM_PID atom not found");
            return -1;
        }
        var status = Imports.XGetWindowProperty(_display, window, pidAtom,
            0, 1, false, (IntPtr)Constants.XaCardinal,
            out _, out _,
            out var nItems, out _, out var prop);

        if (status == 0 && prop != IntPtr.Zero && nItems > 0)
        {
            var pid = Marshal.ReadInt32(prop);
            Imports.XFree(prop);
            return pid;
        }

        return -1;
    }
    
    private List<IntPtr> _cachedVisibleWindows;
    private DateTime _lastCacheTime;
    private const float CacheRefreshSeconds = 1f;

    private List<IntPtr> GetAllVisibleWindows()
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetAllVisibleWindows();
        if (_cachedVisibleWindows != null && !((DateTime.Now - _lastCacheTime).TotalSeconds > CacheRefreshSeconds))
            return _cachedVisibleWindows;
        var result = new List<IntPtr>();

        var clientListAtom = Imports.XInternAtom(_display, "_NET_CLIENT_LIST", true);
        if (clientListAtom != IntPtr.Zero)
        {
            var status = Imports.XGetWindowProperty(_display, _rootWindow, clientListAtom, 0, 1024, false, (IntPtr)Constants.XaWindow,
                out var actualType, out var actualFormat, out var nItems, out _, out var prop);

            if (status == 0 && actualType == (IntPtr)Constants.XaWindow && actualFormat == 32 && prop != IntPtr.Zero)
            {
                for (ulong i = 0; i < nItems; i++)
                {
                    var win = Marshal.ReadIntPtr(prop, (int)(i * (ulong)IntPtr.Size));
                    if (IsWindowVisible(win))
                    {
                        result.Add(win);
                    }
                }

                Imports.XFree(prop);
                return result;
            }

            if (prop != IntPtr.Zero) Imports.XFree(prop);
        }

        ShowError("Fallback to recursive enumeration because _NET_CLIENT_LIST is not available");
        EnumerateWindows(_rootWindow, result);
        _cachedVisibleWindows = result;
        return result;
    }

    private void EnumerateWindows(IntPtr window, List<IntPtr> accumulator)
    {
        var result = Imports.XGetWindowAttributes(_display, window, out var attr);
        if (result != 0 && attr.map_state == Constants.IsViewable)
        {
            accumulator.Add(window);

            if (Imports.XQueryTree(_display, window, out _, out _, out var children, out var nChildren) != 0)
            {
                if (children != IntPtr.Zero && nChildren > 0)
                {
                    for (var i = 0; i < nChildren; i++)
                    {
                        var child = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                        EnumerateWindows(child, accumulator);
                    }
                    Imports.XFree(children);
                }
            }
        }
    }

    public bool GetWindowRect(out RectInt rect)
    {
        return GetWindowRect(_unityWindow, out rect);
    }

    public void GetWindowRect(string kWinUuid, out RectInt rect)
    {
        if (_currentDesktopEnv == DesktopEnvironments.Kde)
        {
            rect = Singleton<KWinManager>.Instance.GetWindowGeometry(kWinUuid).Result;
            return;
        }

        ShowError("The first argument is passed as a string which is supposed to be a UUID of a window managed by KWin.\nHowever KWin/KDE is not detected.");
        rect = RectInt.zero;
    }

    public bool GetWindowRect(IntPtr window, out RectInt rect)
    {
        rect = new RectInt();
        if(_windowManagerImplementation != null)
        {
            rect = _windowManagerImplementation.GetWindowRect(window);
            return true;
        }
        var result = Imports.XGetWindowAttributes(_display, window, out var attr);
        if (result == 0) return false;

        if (!Imports.XTranslateCoordinates(_display, window, _rootWindow, 0, 0, out var absX, out var absY, out _))
            return false;

        rect.x = absX;
        rect.y = absY;
        rect.width = attr.width;
        rect.height = attr.height;
        return true;
    }

    public void SetTopmost(bool topmost = true)
    {
        if(_windowManagerImplementation != null)
        {
            _windowManagerImplementation.SetTopmost(topmost);
            return;
        }
#if UNITY_EDITOR
        return;
#endif
        if (_closing)
            return;
        if (_netWmStateAbove == IntPtr.Zero)
        {
            ShowError("Cannot find atom for _NET_WM_STATE_ABOVE!");
            return;
        }

        if (_netWmState == IntPtr.Zero)
        {
            ShowError("Cannot find atom for _NET_WM_STATE!");
            return;
        }

        var xClient = new XClientMessageEvent
        {
            type = Constants.ClientMessage,
            window = _unityWindow,
            message_type = _netWmState,
            format = 32,
        };
        xClient.data0 = new IntPtr(topmost ? 1 : 0); // 1=ADD, 0=REMOVE
        xClient.data1 = _netWmStateAbove;
        xClient.data2 = IntPtr.Zero;
        xClient.data3 = IntPtr.Zero;
        xClient.data4 = IntPtr.Zero;
        
        Imports.XSendEvent(_display, _rootWindow, false, 0x00100000 | Constants.SubstructureRedirectMask, ref xClient);
        Imports.XFlush(_display);
    }
        
    public void HideFromTaskbar(bool reallyHide = true)
    {
        if(_windowManagerImplementation != null)
        {
            _windowManagerImplementation.HideFromTaskbar(reallyHide);
            return;
        }
        if (_netWmState == IntPtr.Zero || _netWmStateSkipTaskbar == IntPtr.Zero)
            return;

        XClientMessageEvent msg = new()
        {
            type = Constants.ClientMessage,
            display = _display,
            window = _unityWindow,
            message_type = _netWmState,
            format = 32,
        };
        msg.data0 = new(reallyHide ? 1 : 0);
        msg.data1 = _netWmStateSkipTaskbar;
        msg.data2 = IntPtr.Zero;
        msg.data3 = IntPtr.Zero;
        msg.data4 = new(1);

        Imports.XSendEvent(_display, _rootWindow, false, 0x10000L | 0x20000L, ref msg);
        Imports.XFlush(_display);
    }


    private void SetWindowBorderless()
    {
        if(_windowManagerImplementation != null)
        {
            _windowManagerImplementation.SetWindowBorderless();
            return;
        }
        if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return;

        // Remove window decorations using Motif hints
        object hints = new XMotifWmHints
        {
            flags = (IntPtr)Constants.MwmHintsFlags,
            decorations = (IntPtr)Constants.MwmDecorationsNone,
            functions = IntPtr.Zero,
            input_mode = IntPtr.Zero,
            status = IntPtr.Zero
        };
        ChangeProperty(_motifHintsAtom, _motifHintsAtom, 32, Constants.PropModeReplace, hints, 5);
        Imports.XFlush(_display);
    }
    
    private void ChangeProperty<T>(IntPtr property, IntPtr type, int format, int mode, T data, int nelements)
    {
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            Imports.XChangeProperty(_display, _unityWindow, property, type, format, mode, ptr, nelements);
        }
        finally
        {
            handle.Free();
        }
    }

    public void SetWindowType(WindowType type)
    {
        if(_windowManagerImplementation != null)
        {
            _windowManagerImplementation.SetWindowType(type);
            return;
        }
        switch (type)
        {
            case WindowType.Dock:
                ChangeProperty(_netWmWindowType, (IntPtr)Constants.XaAtom, 32, Constants.PropModeReplace, _netWmWindowTypeDock, 1);
                break;
            case WindowType.Normal:
                ChangeProperty(_netWmWindowType, (IntPtr)Constants.XaAtom, 32, Constants.PropModeReplace, _netWmWindowTypeNormal, 1);
                break;
            default:
                ShowError("What was that?");
                break;
        }
    }

    public string GetClassName(IntPtr window)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetClassName(window);
        if (Imports.XGetClassHint(_display, window, out var hint) != 0)
        {
            var cls = Marshal.PtrToStringAnsi(hint.res_class);
            Imports.XFree(hint.res_name);
            Imports.XFree(hint.res_class);
            return cls ?? "";
        }

        return "";
    }
        
    public bool IsDesktop(IntPtr hwnd)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.IsDesktop(hwnd);
        return GetWindowType(hwnd) == "_NET_WM_WINDOW_TYPE_DESKTOP";
    } 

    private IntPtr _desktop;

    public IntPtr GetDesktop
    {
        get
        {
            if (_desktop == IntPtr.Zero || Imports.XGetWindowAttributes(_display, _desktop, out _) == 0)
            {
                var allWin = GetAllVisibleWindows();
                foreach (var win in allWin)
                {
                    if (IsDesktop(win))
                        _desktop = win;
                }
            }
            return _desktop;
        }
    }
    
    public bool IsDock(IntPtr hwnd)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.IsDock(hwnd);
        return GetWindowType(hwnd) == "_NET_WM_WINDOW_TYPE_DOCK";
    }
    
    private IntPtr _dock;

    public IntPtr GetDock
    {
        get
        {
            if (_dock == IntPtr.Zero)
            {
                foreach (var win in GetAllVisibleWindows())
                {
                    if (!IsDock(win)) continue;
                    _dock = win;
                    break;
                }
            }
            return _dock;
        }
    }

    public bool IsWindowMaximized(IntPtr hwnd)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.IsWindowMaximized(hwnd);
        if (_netWmState == IntPtr.Zero) return false;
        var status = Imports.XGetWindowProperty(_display, hwnd, _netWmState, 0, 1024, false, (IntPtr)Constants.XaAtom, out _, out _, out var nItems, out _, out var prop);
        if (status != 0 || prop == IntPtr.Zero || nItems == 0) { if (prop != IntPtr.Zero) Imports.XFree(prop); return false; }
        bool maxH = false, maxV = false;
        for (ulong i = 0; i < nItems; i++)
        {
            var atom = Marshal.ReadIntPtr(prop, ((int)i * IntPtr.Size));
            if (atom == _netWmStateMaxHorz) maxH = true;
            if (atom == _netWmStateMaxVert) maxV = true;
        }
        Imports.XFree(prop);
        return maxH && maxV;
    }

    public bool IsWindowFullscreen(IntPtr hwnd)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.IsWindowFullscreen(hwnd);
        if (!GetWindowRect(hwnd, out var rect)) return false;
        int screenW = UnityEngine.Display.main.systemWidth, screenH = UnityEngine.Display.main.systemHeight;
        var tol = 2;
        var sizeMatch = Mathf.Abs(rect.width - screenW) <= tol && Mathf.Abs(rect.height - screenH) <= tol;
        if (!sizeMatch) return false;
        // Check _NET_WM_STATE_FULLSCREEN
        if (_netWmState == IntPtr.Zero) return true;  // Fallback if no EWMH
        var status = Imports.XGetWindowProperty(_display, hwnd, _netWmState, 0, 1024, false, (IntPtr)Constants.XaAtom, out _, out _, out var nItems, out _, out var prop);
        var isFs = false;
        if (status == 0 && prop != IntPtr.Zero && nItems > 0)
        {
            for (ulong i = 0; i < nItems; i++)
            {
                if (Marshal.ReadIntPtr(prop, (int)i * IntPtr.Size) == _netWmStateFullscreen) { isFs = true; break; }
            }
            Imports.XFree(prop);
        }
        return isFs;
    }
        
    public bool IsWindowVisible(IntPtr window)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.IsWindowVisible(window);
        if (_display == IntPtr.Zero) return false;

        var result = Imports.XGetWindowAttributes(_display, window, out var attr);
        if (result == 0 || attr.map_state != Constants.IsViewable) return false;
    
        if (!Imports.XTranslateCoordinates(_display, window, _rootWindow, 0, 0, out var absX, out var absY, out _))
            return false;

        float targetX1 = absX;
        float targetY1 = absY;
        float targetX2 = absX + attr.width;
        float targetY2 = absY + attr.height;
    
        var stacking = GetClientStackingList();
        var index = stacking.IndexOf(window);
        if (index < 0) return false;
    
        List<(float x1, float y1, float x2, float y2)> coversList = new();
        for (var i = index + 1; i < stacking.Count; i++)
        {
            var ow = stacking[i];
            Imports.XGetWindowAttributes(_display, ow, out var oattr);
            if (oattr.map_state != Constants.IsViewable) continue;

            if (!Imports.XTranslateCoordinates(_display, ow, _rootWindow, 0, 0, out var oabsX, out var oabsY, out _))
                continue;

            float ox1 = oabsX;
            float oy1 = oabsY;
            float ox2 = oabsX + oattr.width;
            float oy2 = oabsY + oattr.height;
        
            var ix1 = Math.Max(targetX1, ox1);
            var iy1 = Math.Max(targetY1, oy1);
            var ix2 = Math.Min(targetX2, ox2);
            var iy2 = Math.Min(targetY2, oy2);
            if (ix1 < ix2 && iy1 < iy2)
            {
                coversList.Add((ix1, iy1, ix2, iy2));
            }
        }

        if (coversList.Count == 0) return true;
    
        var covers = new NativeArray<RectF>(coversList.Count, Allocator.Temp);
        for (var i = 0; i < coversList.Count; i++)
        {
            var t = coversList[i];
            covers[i] = new RectF { x1 = t.x1, y1 = t.y1, x2 = t.x2, y2 = t.y2 };
        }
        var target = new RectF { x1 = targetX1, y1 = targetY1, x2 = targetX2, y2 = targetY2 };
        float coveredFraction = CoveredFractionCalculator.Compute(covers, target, gridSize: 10);
        covers.Dispose();
    
        var targetArea = (target.x2 - target.x1) * (target.y2 - target.y1);  // Not strictly needed, but for consistency
        float coveredAreaApprox = coveredFraction * targetArea;

        return coveredAreaApprox < targetArea - 1e-4f;
    }

    private bool IsCompositionSupported()
    {
        for (var screen = 0; screen < Imports.XScreenCount(_display); screen++)
        {
            var selectionAtom = Imports.XInternAtom(_display, "_NET_WM_CM_S" + screen, false);
            if (selectionAtom == IntPtr.Zero)
                continue;
            return Imports.XGetSelectionOwner(_display, selectionAtom) != 0;
        }
        return false;
    }

    public List<string> GetClientStackingListKWin()
    {
        if (_currentDesktopEnv == DesktopEnvironments.Kde)
        {
            return Singleton<KWinManager>.Instance.GetAllWindows().Result;
        }
        ShowError("KWin/KDE is not detected.");
        return new List<string>();
    }
    
    public List<IntPtr> GetClientStackingList()
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetClientStackingList();
        var atom = Imports.XInternAtom(_display, "_NET_CLIENT_LIST_STACKING", false);
        if (atom == IntPtr.Zero) return new List<IntPtr>();

        var status = Imports.XGetWindowProperty(_display, _rootWindow, atom, 0, 1024, false, (IntPtr)Constants.XaWindow,
            out _, out var actualFormat, out var nItems, out _, out var prop);

        if (status != 0 || prop == IntPtr.Zero || nItems == 0 || actualFormat != 32)
        {
            if (prop != IntPtr.Zero) Imports.XFree(prop);
            return new List<IntPtr>();
        }

        var windows = new List<IntPtr>((int)nItems);
        for (var i = 0; i < (int)nItems; i++)
        {
            var w = Marshal.ReadIntPtr(prop, (i * IntPtr.Size));
            windows.Add(w);
        }
        Imports.XFree(prop);
        return windows;
    }
        
    #region Window Shaping Logic
        
    private void EnableClickThroughTransparency()
    {
        if (_running || !transparentInputEnabled) return;
        SetupTransparentInput();
        _running = true;
        _x11EventThread = new Thread(ApplyShaping)
        {
            Name = "WinShapeThread",
            IsBackground = true
        };

        _x11EventThread.Start();
    }

    private void SetupTransparentInput()
    {
        if (Imports.XGetWindowAttributes(_display, _unityWindow, out var attrs) == 0)
        {
            ShowError("Failed to get window attributes");
            return;
        }

        if (attrs.depth != 32 || !IsArgbVisual(_display, attrs.visual))
        {
            ShowError("Unity window does not have a 32-bit ARGB visual. Skipping shaping.");
            return;
        }

        if (!IsCompositionSupported())
        {
            ShowError("No compositor found.");
            return;
        }

        Imports.XCompositeRedirectWindow(_display, _unityWindow, Constants.CompositeRedirectAutomatic);
        
        _useShm = Imports.XShmQueryExtension(_display);
        if (_useShm)
        {
            int imageSize = attrs.width * attrs.height * 4;

            int shmid = Imports.shmget(Constants.IPC_PRIVATE, (IntPtr)imageSize, Constants.IPC_CREAT | 0x1FF);

            if (shmid == -1)
            {
                _useShm = false;
            }
            else
            {
                _shmInfo.shmid = shmid;
                _shmInfo.shmaddr = Imports.shmat(shmid, IntPtr.Zero, 0);
                _shmInfo.readOnly = 0;

                if (_shmInfo.shmaddr == new IntPtr(-1))
                {
                    _useShm = false;
                }
                else
                {
                    Imports.shmctl(shmid, Constants.IPC_RMID, IntPtr.Zero);

                    if (Imports.XShmAttach(_display, ref _shmInfo) == 0)
                    {
                        _useShm = false;
                    }
                    else
                    {
                        Imports.XSync(_display, false);

                        _shmImagePtr = Imports.XShmCreateImage(_display, attrs.visual, (uint)attrs.depth,
                            Constants.ZPixmap, _shmInfo.shmaddr, ref _shmInfo, (uint)attrs.width, (uint)attrs.height);

                        if (_shmImagePtr == IntPtr.Zero)
                        {
                            _useShm = false;
                        }
                        else
                        {
                            _shmWidth = attrs.width;
                            _shmHeight = attrs.height;
                        }
                    }
                }
            }
        }

        if (!Imports.XDamageQueryExtension(_display, out _damageEventBase, out _))
        {
            ShowError("XDamage extension not available");
            return;
        }

        _damage = Imports.XDamageCreate(_display, _unityWindow, Constants.XDamageReportNonEmpty);
        if (_damage == IntPtr.Zero)
        {
            ShowError("Failed to create damage object");
            return;
        }

        UpdateInputMask(attrs.width, attrs.height);
    }

    private bool IsArgbVisual(IntPtr display, IntPtr visual)
    {
        var formatPtr = Imports.XRenderFindVisualFormat(display, visual);
        if (formatPtr == IntPtr.Zero) return false;

        var format = Marshal.PtrToStructure<XRenderPictFormat>(formatPtr);
        return format.type == Constants.PictTypeDirect && format.direct.alphaMask != 0;
    }

    private List<XRectangle> GenerateRectangles(byte[] imageData, int width, int height)
    {
        var rects = new List<XRectangle>();
        
        for (short y = 0; y < height; y++)
        {
            for (short x = 0; x < width; x++)
            {
                int idx = (y * width + x) * 4;
                if (imageData[idx + 3] > 10) 
                {
                    short startX = x;
                    while (x < width && imageData[(y * width + x) * 4 + 3] > 10)
                    {
                        x++;
                    }
        
                    rects.Add(new XRectangle 
                    { 
                        x = startX, 
                        y = y, 
                        width = (ushort)(x - startX), 
                        height = 1 
                    });
                }
            }
        }
        return rects;
    }
     
    private void UpdateInputMask(int width, int height)
    {
        if (_isDragging || !_running || _closing)
            return;

        // Throttle logic...
        _shapingStopwatch ??= new();
        if (_shapingStopwatch.IsRunning && _shapingStopwatch.ElapsedMilliseconds < ShapingThrottleMs)
            return;

        _shapingStopwatch.Restart();

        XRectangle[] fullRect = { 
            new() { x = 0, y = 0, width = (ushort)width, height = (ushort)height } 
        };
        Imports.XShapeCombineRectangles(_display, _unityWindow, Constants.ShapeBounding, 0, 0, fullRect, 1, Constants.ShapeSet, Constants.Unsorted);

        IntPtr xImagePtr = IntPtr.Zero;
        byte[] imageBytes;

        if (_useShm)
        {
            if (width != _shmWidth || height != _shmHeight)
            {
                if (!TryResizeShm(width, height))
                {
                    ShowError("Failed to resize SHM, falling back to XGetImage");
                    _useShm = false; 
                }
            }
        }

        IntPtr backingPixmap = Imports.XCompositeNameWindowPixmap(_display, _unityWindow);

        if (backingPixmap != IntPtr.Zero)
        {
            if (Imports.XShmGetImage(_display, backingPixmap, _shmImagePtr, 0, 0, Constants.AllPlanes))
            {
                xImagePtr = _shmImagePtr;
            }
            else
            {
                ShowError("XShmGetImage on Pixmap failed.");
                _useShm = false;
            }
                
            Imports.XFreePixmap(_display, backingPixmap);
        }
        else
        {
            if (Imports.XShmGetImage(_display, _unityWindow, _shmImagePtr, 0, 0, Constants.AllPlanes))
            {
                xImagePtr = _shmImagePtr;
            }
            else
            {
                ShowError("XShmGetImage on Window failed unexpectedly.");
                _useShm = false;
            }
        }

        if (!_useShm)
        {
            xImagePtr = Imports.XGetImage(_display, _unityWindow, 0, 0, (uint)width, (uint)height, Constants.AllPlanes, Constants.ZPixmap);
            if (xImagePtr == IntPtr.Zero)
            {
                ShowError("XGetImage failed");
                return;
            }
        }

        imageBytes = GetImageData(xImagePtr, width, height).Data;

        if (!_useShm && xImagePtr != IntPtr.Zero)
        {
            Imports.XDestroyImage(xImagePtr);
        }

        List<XRectangle> rects = GenerateRectangles(imageBytes, width, height);
        XRectangle[] rectArray = rects.ToArray();

        Imports.XShapeCombineRectangles(_display, _unityWindow, Constants.ShapeInput, 0, 0, rectArray, rectArray.Length, Constants.ShapeSet, Constants.YSorted);

        Imports.XFlush(_display);
    }

    private bool TryResizeShm(int width, int height)
    {
        if (_shmImagePtr != IntPtr.Zero)
        {
            Imports.XDestroyImage(_shmImagePtr);
            _shmImagePtr = IntPtr.Zero;
        }
        
        if (_shmInfo.shmaddr != IntPtr.Zero)
        {
            Imports.XShmDetach(_display, ref _shmInfo);
            Imports.shmdt(_shmInfo.shmaddr);
        }

        if (Imports.XGetWindowAttributes(_display, _unityWindow, out var attrs) == 0) return false;

        int imageSize = width * height * 4;
        int shmid = Imports.shmget(Constants.IPC_PRIVATE, (IntPtr)imageSize, Constants.IPC_CREAT | 0x1FF);

        if (shmid == -1) return false;

        _shmInfo = new XShmSegmentInfo
        {
            shmid = shmid,
            shmaddr = Imports.shmat(shmid, IntPtr.Zero, 0),
            readOnly = 0
        };

        if (_shmInfo.shmaddr == new IntPtr(-1))
        {
            Imports.shmctl(shmid, Constants.IPC_RMID, IntPtr.Zero);
            return false;
        }

        Imports.shmctl(shmid, Constants.IPC_RMID, IntPtr.Zero);

        if (Imports.XShmAttach(_display, ref _shmInfo) == 0)
        {
            Imports.shmdt(_shmInfo.shmaddr);
            return false;
        }

        Imports.XSync(_display, false);

        _shmImagePtr = Imports.XShmCreateImage(_display, attrs.visual, (uint)attrs.depth,
            Constants.ZPixmap, _shmInfo.shmaddr, ref _shmInfo, (uint)width, (uint)height);

        if (_shmImagePtr == IntPtr.Zero)
        {
            Imports.XShmDetach(_display, ref _shmInfo);
            Imports.shmdt(_shmInfo.shmaddr);
            return false;
        }

        _shmWidth = width;
        _shmHeight = height;
        
        return true;
    }

    private Image GetImageData(IntPtr xImagePtr, int width, int height)
    {
        Image image;
        image.Width = width;
        image.Height = height;
        int byteCount = width * height * 4;
        image.Data = new byte[byteCount];

        if (_useShm && _shmInfo.shmaddr != IntPtr.Zero)
        {
            Marshal.Copy(_shmInfo.shmaddr, image.Data, 0, byteCount);
        }
        else if (xImagePtr != IntPtr.Zero)
        {
            XImage ximg = Marshal.PtrToStructure<XImage>(xImagePtr);
        
            if (ximg.data != IntPtr.Zero)
            {
                Marshal.Copy(ximg.data, image.Data, 0, byteCount);
            }
        }

        return image;
    }
    
    private void ApplyShaping()
    {
        try
        {
            while (_running)
            {
                if (_display == IntPtr.Zero) break;
                // Removed XPending here to significantly improve CPU usage
                XEvent ev = default;
                Imports.XNextEvent(_display, ref ev);

                switch (ev.type)
                {
                    case Constants.ConfigureNotify:
                    {
                        var ce = ev.configureEvent;
                        if (ce.window == _unityWindow)
                        {
                            UpdateInputMask(ce.width, ce.height);
                        }
                        break;
                    }
                    case Constants.DestroyNotify:
                    {
                        var de = ev.destroyWindowEvent;
                        if (de.window == _unityWindow)
                        {
                            _running = false;
                        }
                        break;
                    }
                    case Constants.EnterNotify:
                    {
                        _mouseOver = true;
                        break;
                    }
                    case Constants.LeaveNotify:
                    {
                        _mouseOver = false;
                        break;
                    }
                    default:
                    {
                        if (ev.type == _damageEventBase)
                        {
                            var de = ev.damageNotifyEvent;
                            if (de.drawable == _unityWindow)
                            {
                                Imports.XDamageSubtract(_display, de.damage, IntPtr.Zero, IntPtr.Zero);
                                Imports.XGetWindowAttributes(_display, _unityWindow, out var attrs);
                                UpdateInputMask(attrs.width, attrs.height);
                            }
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
    #endregion
    
    #region Cursors
    private IntPtr LoadThemedCursor(string cursorName, uint fallbackShape)
    {
        var cursor = Imports.XcursorLibraryLoadCursor(_display, cursorName);
        if (cursor == IntPtr.Zero)
        {
            Debug.LogWarning($"WindowManager: Failed to load themed cursor '{cursorName}', falling back to font cursor.");
            cursor = Imports.XCreateFontCursor(_display, fallbackShape);
        }
        return cursor;
    }

    private void LoadCursors()
    {
        if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return;
        if (_currentSessionType == SessionTypes.Wayland || _currentDesktopEnv == DesktopEnvironments.Hyprland) return;

        _defaultCursor  = LoadThemedCursor("left_ptr", Constants.XC_LEFT_PTR);
        _grabCursor     = LoadThemedCursor("grab",     Constants.XC_HAND2);
        _grabbingCursor = LoadThemedCursor("grabbing", Constants.XC_HAND2);
    }
    
    private void UpdateCursorState()
    {
        if (_dontUpdateCursor)
            return;
        if (_isDragging)
            SetCursor(_grabbingCursor);
        else if (_mouseOver)
            SetCursor(_grabCursor);
        else
            SetCursor(IntPtr.Zero); // Or _defaultCursor
    }

    private void SetCursor(IntPtr cursor)
    {
        if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return;

        var toSet = (cursor != IntPtr.Zero) ? cursor : _defaultCursor;
        Imports.XDefineCursor(_display, _unityWindow, toSet);
        Imports.XFlush(_display);
    }
    #endregion
        
    #region API

    private IntPtr _display;
    private IntPtr _rootWindow;
    private IntPtr _unityWindow;
    private Dictionary<IntPtr, RectInt> _monitors;
    private IntPtr _netWmState, _netWmStateFullscreen, _netWmStateMaxHorz, _netWmStateMaxVert, _netWmStateAbove, _netWmStateSkipTaskbar;
    private IntPtr _netWmWindowType, _netWmWindowTypeDock, _netWmWindowTypeNormal;
    private IntPtr _netMoveResizeWindow;
    private IntPtr _motifHintsAtom;
    
    private IntPtr _defaultCursor;
    private IntPtr _grabCursor;
    private IntPtr _grabbingCursor;
    private volatile bool _mouseOver;

    private int _damageEventBase;
    private IntPtr _damage = IntPtr.Zero;
    private bool _running;
    private bool _closing;
    private Thread _x11EventThread;
    private Stopwatch _shapingStopwatch;
    private bool _useShm;
    private XShmSegmentInfo _shmInfo;
    private IntPtr _shmImagePtr;
    private int _shmWidth;
    private int _shmHeight;

    private const long ShapingThrottleMs = 100; // Update mask every 100ms
    
    public int GetGeometry(out IntPtr rootReturn, out int x, out int y, out int width, out int height, out int borderWidth, out uint depth) => 
        Imports.XGetGeometry(_display, _unityWindow, out rootReturn, out x, out y, out width, out height, out borderWidth, out depth);

    #endregion
}