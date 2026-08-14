#pragma warning disable 0162
//#pragma warning disable 0168
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
// named namespace to prevent naming conflicts with X11 classes
using TDBus= Tmds.DBus;
using UnityEngine;
using UnityEngine.EventSystems;
using Debug = UnityEngine.Debug;
using Unity.Burst;
using Unity.Collections;
using APIs.Hyprland;

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

public enum WindowType
{
    Normal = 0,
    Dock = 1,
    Desktop = 2
}

public class WindowManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static WindowManager Instance;
    
    private DesktopEnvironments _currentDesktopEnv;
    private SessionTypes _currentSessionType;

    private Vector2Int _initialMousePos;
    private Vector2Int _initialWindowPos;
    private volatile bool _isDragging;

    private bool _dontUpdateCursor;
    
    public bool transparentInputEnabled = true;

    public IntPtr Display => _display;

    public IntPtr RootWindow => _rootWindow;

    public IntPtr UnityWindow => _unityWindow;
    
    private XErrorHandler _errorHandlerDelegate;

    #region Unity Events

    TDBus.Connection _dBusConnection;
    TDBus.ConnectionInfo _dBusConnectionInfo;
    
    private async void OnEnable()
    {
        try
        {
            // Only one single living instance is allowed
            if (Instance)
            {
                if (Instance != this)
                {
                    Destroy(this);
                }
                return;
            }
            Instance = this;
            
            if(_dBusConnection == null)
                _dBusConnection = new TDBus.Connection(TDBus.Address.Session);
            if(_dBusConnectionInfo == null)
                _dBusConnectionInfo = await _dBusConnection.ConnectAsync();
            
            if (Enum.TryParse(Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"), true, out _currentDesktopEnv))
            {
                switch(_currentDesktopEnv)
                {
                    case DesktopEnvironments.Hyprland:
                        _windowManagerImplementation = Singleton<HyprlandManager>.Instance;
                        break;
                    case DesktopEnvironments.Kde:
                        if (SaveLoadHandler.Instance.data.useKWinApi)
                        {
                            var km = new KWinManager(_dBusConnection, _dBusConnectionInfo);
                            await km.SetupDBus();
                            _windowManagerImplementation = km;
                        }
                        break;
                }
                _windowManagerImplementation?.SetXUnityWindow(_unityWindow);
            }
            else
            {
                _currentDesktopEnv = _currentSessionType switch
                {
                    SessionTypes.X11 => DesktopEnvironments.OtherX11,
                    SessionTypes.Wayland => DesktopEnvironments.OtherWayland,
                    _ => DesktopEnvironments.Unknown
                };
            }
            if (!Enum.TryParse(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), true, out _currentSessionType))
            {
                _currentSessionType = SessionTypes.Unknown;
            }

            if (_currentSessionType == SessionTypes.Wayland)
            {
                // Removed _WaylandWarningPosted here because later OnEnable calls are blocked
                Singleton<DBusNotificationHelper>.Instance.Init(_dBusConnection);
                await Singleton<DBusNotificationHelper>.Instance.Send("Wayland is not fully supported by MateEngine",
                    "Although Wayland improves security, its fundamental design choices prevent MateEngine (and other X11 applications) from getting window information." +
                    "Thus, features like window-sitting could hardly be implemented under various Wayland-based DEs. Please use X11 DEs to enjoy the best of MateEngine.",
                    "wayland", null, 30000);
            }
        }
        catch (Exception e)
        {
            ShowError(e.ToString());
        }
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
        CheckSingleInstanceForNoNetWmPidSupportCompositors();
        var pid = Process.GetCurrentProcess().Id;
        var windows = FindWindowsByPid(pid, false);

        if (windows.Count > 0)
        {
            _unityWindow = windows[0]; // Typically the first is the main window
            Debug.Log($"Unity window handle: 0x{_unityWindow.ToInt64():X}");
            QueryMonitors();
#if UNITY_EDITOR
            return;
#endif
            SetWindowBorderless();
            XSelectInput(_display, _unityWindow, StructureNotifyMask | EnterWindowMask | LeaveWindowMask | PropertyChangeMask);
            EnableClickThroughTransparency();
            LoadCursors();
            print($"{GetType()} initialization completed.");
            return;
        }
        ShowError($"No matching windows found for PID {pid}.");
    }
    
    private void CheckSingleInstanceForNoNetWmPidSupportCompositors()
    {
        var allWindows = GetAllWindows();
        int matchCount = 0;

        foreach (var window in allWindows)
        {
            if (GetClassName(window).Equals(Process.GetCurrentProcess().ProcessName, StringComparison.Ordinal))
            {
                int windowPid = GetWindowPid(window);

                if (windowPid == -1)
                {
                    matchCount++;
                
                    if (matchCount > 1)
                    {
                        Debug.LogError("Blocking: Multiple windows found with PID -1.");
                        Application.Quit();
                        return;
                    }
                }
            }
        }
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
            _windowManagerImplementation.IsDragging = false;
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
        XInitThreads();
#endif
        // Open X11 display
        _display = XOpenDisplay(null);
        if (_display == IntPtr.Zero)
        {
            throw new Exception("Cannot open X11 display");
        }

        _errorHandlerDelegate = ShowError;
        XSetErrorHandler(_errorHandlerDelegate);
        RegisterExtension();


        _rootWindow = XDefaultRootWindow(_display);
        
        _netWmState = XInternAtom(_display, "_NET_WM_STATE", false);
        _netWmStateFullscreen = XInternAtom(_display, "_NET_WM_STATE_FULLSCREEN", false);
        _netWmStateMaxHorz = XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_HORZ", false);
        _netWmStateMaxVert = XInternAtom(_display, "_NET_WM_STATE_MAXIMIZED_VERT", false);
        _netWmWindowType = XInternAtom(_display, "_NET_WM_WINDOW_TYPE", false);
        _netMoveResizeWindow = XInternAtom(_display, "_NET_MOVERESIZE_WINDOW", false);
        _netWmStateAbove = XInternAtom(_display, "_NET_WM_STATE_ABOVE", false);
        _netWmStateSkipTaskbar = XInternAtom(_display, "_NET_WM_STATE_SKIP_TASKBAR", false);
        _netWmWindowTypeDock = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DOCK", false);
        _netWmWindowTypeDesktop = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_DESKTOP", false);
        _netWmWindowTypeNormal = XInternAtom(_display, "_NET_WM_WINDOW_TYPE_NORMAL", false);
        _motifHintsAtom = XInternAtom(_display, "_MOTIF_WM_HINTS", false);
        _wakeupAtom = XInternAtom(_display, "_SDL_WAKEUP", false);
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

        var buffer = new StringBuilder(256);
        XGetErrorText(_display, error.error_code, buffer, buffer.Capacity);
    
        string message = buffer.ToString();
    
        if (string.IsNullOrEmpty(message))
        {
            return $"Unknown error code: {error.error_code}";
        }
    
        return $"{message}, Module: {GetRequestName(error.request_code)}, minor_code: {error.minor_code}, Resource ID: 0x{error.resourceid:X}";
    }
    
    private Dictionary<int, string> _x11ExtensionsMap = new();
    
    private void RegisterExtension()
    {
        IntPtr listPtr = XListExtensions(_display, out int count);
    
        if (listPtr == IntPtr.Zero || count == 0) return;

        try
        {
            for (int i = 0; i < count; i++)
            {
                IntPtr strPtr = Marshal.ReadIntPtr(listPtr, i * IntPtr.Size);
            
                string name = Marshal.PtrToStringAnsi(strPtr);
            
                if (!string.IsNullOrEmpty(name))
                {
                    if (XQueryExtension(_display, name, out int opcode, out _, out _) == 0)
                    {
                        ShowError($"Cannot query extension {name}");
                        continue;
                    }
                    if (opcode > 0)
                    {
                        _x11ExtensionsMap[opcode] = name;
                    }
                }
            }
        }
        finally
        {
            XFree(listPtr);
        }
    }
    
    private string GetRequestName(byte requestCode)
    {
        int code = requestCode;

        if (_x11ExtensionsMap.TryGetValue(code, out string extName))
        {
            return $"[{extName}]";
        }

        return requestCode switch
        {
            1 => "X_CreateWindow",
            2 => "X_ChangeWindowAttributes",
            3 => "X_GetWindowAttributes",
            4 => "X_DestroyWindow",
            5 => "X_DestroySubwindows",
            6 => "X_ChangeSaveSet",
            7 => "X_ReparentWindow",
            8 => "X_MapWindow",
            9 => "X_MapSubwindows",
            10 => "X_UnmapWindow",
            11 => "X_UnmapSubwindows",
            12 => "X_ConfigureWindow",
            13 => "X_CirculateWindow",
            14 => "X_GetGeometry",
            15 => "X_QueryTree",
            16 => "X_InternAtom",
            17 => "X_GetAtomName",
            18 => "X_ChangeProperty",
            19 => "X_DeleteProperty",
            20 => "X_GetProperty",
            21 => "X_ListProperties",
            22 => "X_SetSelectionOwner",
            23 => "X_GetSelectionOwner",
            24 => "X_ConvertSelection",
            25 => "X_SendEvent",
            26 => "X_GrabPointer",
            27 => "X_UngrabPointer",
            28 => "X_GrabButton",
            29 => "X_UngrabButton",
            30 => "X_ChangeActivePointerGrab",
            31 => "X_GrabKeyboard",
            32 => "X_UngrabKeyboard",
            33 => "X_GrabKey",
            34 => "X_UngrabKey",
            35 => "X_AllowEvents",
            36 => "X_GrabServer",
            37 => "X_UngrabServer",
            38 => "X_QueryPointer",
            39 => "X_GetMotionEvents",
            40 => "X_TranslateCoords",
            41 => "X_WarpPointer",
            42 => "X_SetInputFocus",
            43 => "X_GetInputFocus",
            44 => "X_QueryKeymap",
            45 => "X_OpenFont",
            46 => "X_CloseFont",
            47 => "X_QueryFont",
            48 => "X_QueryTextExtents",
            49 => "X_ListFonts",
            50 => "X_ListFontsWithInfo",
            51 => "X_SetFontPath",
            52 => "X_GetFontPath",
            53 => "X_CreatePixmap",
            54 => "X_FreePixmap",
            55 => "X_CreateGC",
            56 => "X_ChangeGC",
            57 => "X_CopyGC",
            58 => "X_SetDashes",
            59 => "X_SetClipRectangles",
            60 => "X_FreeGC",
            61 => "X_ClearArea",
            62 => "X_CopyArea",
            63 => "X_CopyPlane",
            64 => "X_PolyPoint",
            65 => "X_PolyLine",
            66 => "X_PolySegment",
            67 => "X_PolyRectangle",
            68 => "X_PolyArc",
            69 => "X_FillPoly",
            70 => "X_PolyFillRectangle",
            71 => "X_PolyFillArc",
            72 => "X_PutImage",
            73 => "X_GetImage",
            74 => "X_PolyText8",
            75 => "X_PolyText16",
            76 => "X_ImageText8",
            77 => "X_ImageText16",
            78 => "X_CreateColormap",
            79 => "X_FreeColormap",
            80 => "X_CopyColormapAndFree",
            81 => "X_InstallColormap",
            82 => "X_UninstallColormap",
            83 => "X_ListInstalledColormaps",
            84 => "X_AllocColor",
            85 => "X_AllocNamedColor",
            86 => "X_AllocColorCells",
            87 => "X_AllocColorPlanes",
            88 => "X_FreeColors",
            89 => "X_StoreColors",
            90 => "X_StoreNamedColor",
            91 => "X_QueryColors",
            92 => "X_LookupColor",
            93 => "X_CreateCursor",
            94 => "X_CreateGlyphCursor",
            95 => "X_FreeCursor",
            96 => "X_RecolorCursor",
            97 => "X_QueryBestSize",
            98 => "X_QueryExtension",
            99 => "X_ListExtensions",
            100 => "X_ChangeKeyboardMapping",
            101 => "X_GetKeyboardMapping",
            102 => "X_ChangeKeyboardControl",
            103 => "X_GetKeyboardControl",
            104 => "X_Bell",
            105 => "X_ChangePointerControl",
            106 => "X_GetPointerControl",
            107 => "X_SetScreenSaver",
            108 => "X_GetScreenSaver",
            109 => "X_ChangeHosts",
            110 => "X_ListHosts",
            111 => "X_SetAccessControl",
            112 => "X_SetCloseDownMode",
            113 => "X_KillClient",
            114 => "X_RotateProperties",
            115 => "X_ForceScreenSaver",
            116 => "X_SetPointerMapping",
            117 => "X_GetPointerMapping",
            118 => "X_SetModifierMapping",
            119 => "X_GetModifierMapping",
            127 => "X_NoOperation",
            _ => $"Request_{requestCode}"
        };
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
            XCompositeUnredirectWindow(_display, _unityWindow, CompositeRedirectAutomatic);
            XClientMessageEvent wakeUpEv = new XClientMessageEvent
            {
                type = ClientMessage,
                window = _unityWindow,
                message_type = _wakeupAtom,
                format = 32
            };
        
            XSendEvent(_display, _unityWindow, false, 0, ref wakeUpEv);
            XFlush(_display);
        }
        if (_x11EventThread is { IsAlive: true })
        {
            _x11EventThread.Join();
        }
        if (_display != IntPtr.Zero)
        {
#if UNITY_EDITOR
            SetTopmost(false);
            SetTransientFor(IntPtr.Zero, true);
#endif
#if !UNITY_EDITOR
            if (_damage != IntPtr.Zero)
            {
                XDamageDestroy(_display, _damage);
                _damage = IntPtr.Zero;
            }
#endif
            if (_shmInfo.shmaddr != IntPtr.Zero)
            {
                XDestroyImage(_shmImagePtr);
                XShmDetach(_display, ref _shmInfo);
                shmdt(_shmInfo.shmaddr);
                _shmInfo.shmaddr = IntPtr.Zero;
            }
            XSync(_display, false);
            if (_defaultCursor != IntPtr.Zero) { XFreeCursor(_display, _defaultCursor); _defaultCursor = IntPtr.Zero; }
            if (_grabCursor != IntPtr.Zero) { XFreeCursor(_display, _grabCursor); _grabCursor = IntPtr.Zero; }
            if (_grabbingCursor != IntPtr.Zero) { XFreeCursor(_display, _grabbingCursor); _grabbingCursor = IntPtr.Zero; }
            XCloseDisplay(_display);
            _display = IntPtr.Zero;
        }
    }
    #endregion
    public bool GetWindowPosition(out int x, out int y)
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
            if (XTranslateCoordinates(_display, _unityWindow, _rootWindow, 0, 0, out var absX, out var absY, out _))
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
            if (_netMoveResizeWindow == IntPtr.Zero)
            {
                ShowError("Cannot find atom for _NET_MOVERESIZE_WINDOW!");
                return;
            }
            var xClient = new XClientMessageEvent
            {
                type = ClientMessage,
                window = _unityWindow,
                message_type = _netMoveResizeWindow,
                format = 32,
            };
            xClient.data0 = new IntPtr((1 << 12) | (1 << 9) | (1 << 8) | 10);
            xClient.data1 = new(position.x);
            xClient.data2 = new(position.y);
            xClient.data3 = IntPtr.Zero;
            xClient.data4 = IntPtr.Zero;

            XSendEvent(_display, _rootWindow, false, SubstructureRedirectMask | SubstructureNotifyMask, ref xClient);
            XFlush(_display);
        }
    }

    private void SetWindowPositionLegacy(Vector2Int position)
    {
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            XMoveWindow(_display, _unityWindow, position.x, position.y);
            XFlush(_display);
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
    
    public void SetTransientFor(IntPtr parentWindow, bool force = false)
    {
        if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero || _closing & !force) return;
        if(_windowManagerImplementation != null)
        {
            _windowManagerImplementation.SetSnapedWindow(parentWindow);
            return;
        }
    
        XSetTransientForHint(_display, _unityWindow, parentWindow);
        XFlush(_display);
    }
    
    public void QueryMonitors()
    {
        _monitors ??= new Dictionary<IntPtr, RectInt>();
        _monitors.Clear();

        if (SaveLoadHandler.Instance.safeMode)
        {
            _monitors.Add(IntPtr.Zero, new RectInt(0, 0, Screen.currentResolution.width, Screen.currentResolution.height));
            return;
        }

        if (_windowManagerImplementation != null && _currentDesktopEnv != DesktopEnvironments.Kde)
        {
            foreach (var m in _windowManagerImplementation.GetAllMonitors())
                _monitors[m.Id] = m.Rect;
            return;
        }

        if (_display == IntPtr.Zero) return;
        
        if (XRRQueryExtension(_display, out _, out _) == 0)
        {
            Debug.LogError("XRandR extension not available.");
            return;
        }

        if (XRRQueryVersion(_display, out var major, out var minor) == 0 || major < 1 || (major == 1 && minor < 3))
        {
            Debug.LogError("XRandR 1.3+ required for multi-monitor.");
            return;
        }

        IntPtr resHandle = XRRGetScreenResourcesCurrent(_display, _rootWindow);
        if (resHandle == IntPtr.Zero) return;

        try
        {
            XrrScreenResources res = Marshal.PtrToStructure<XrrScreenResources>(resHandle);
            if (res.noutput <= 0) return;

            for (int i = 0; i < res.noutput; i++)
            {
                IntPtr output = Marshal.ReadIntPtr(res.outputs, i * IntPtr.Size);
                IntPtr outInfoHandle = XRRGetOutputInfo(_display, resHandle, output);
            
                if (outInfoHandle == IntPtr.Zero) continue;

                try
                {
                    XrrOutputInfo outInfo = Marshal.PtrToStructure<XrrOutputInfo>(outInfoHandle);
                    if (outInfo.connection != Connection.Connected || outInfo.crtc == IntPtr.Zero) continue;

                    IntPtr crtcInfoHandle = XRRGetCrtcInfo(_display, resHandle, outInfo.crtc);
                    if (crtcInfoHandle == IntPtr.Zero) continue;

                    try
                    {
                        XrrCrtcInfo crtcInfo = Marshal.PtrToStructure<XrrCrtcInfo>(crtcInfoHandle);
                        if (crtcInfo.width == 0 || crtcInfo.height == 0) continue;

                        _monitors[output] = new RectInt(crtcInfo.x, crtcInfo.y, (int)crtcInfo.width, (int)crtcInfo.height);
                    }
                    finally
                    {
                        XRRFreeCrtcInfo(crtcInfoHandle);
                    }
                }
                finally
                {
                    XRRFreeOutputInfo(outInfoHandle);
                }
            }
        }
        catch (Exception e)
        {
            ShowError($"Error querying monitors: {e.Message}");
        }
        finally
        {
            XRRFreeScreenResources(resHandle);
        }

        if (_monitors.Count == 0)
        {
            ShowError("No monitors were found.");
        }
    }
    
    private string GetWindowType(IntPtr hwnd)  // Returns type atom name or empty
    {
        if (_netWmWindowType == IntPtr.Zero) return "";
        var status = XGetWindowProperty(_display, hwnd, _netWmWindowType, 0, 1, false, (IntPtr)XaAtom, out _, out _, out var nItems, out _, out var prop);
        if (status != 0 || prop == IntPtr.Zero || nItems == 0) { if (prop != IntPtr.Zero) XFree(prop); return ""; }
        var typeAtom = Marshal.ReadIntPtr(prop);
        XFree(prop);
        var atomNamePtr = XGetAtomName(_display, typeAtom);
        var atomName = Marshal.PtrToStringAuto(atomNamePtr);
        XFree(atomNamePtr);
        return atomName;
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
            var result = XGetWindowAttributes(_display, window, out var attributes);
            if (result != 0) // Non-zero indicates success in X11
            {
                return new Vector2Int(attributes.width, attributes.height);
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
                type = ClientMessage,
                window = _unityWindow,
                message_type = _netMoveResizeWindow,
                format = 32,
            };
            xClient.data0 = new IntPtr((1 << 12) | (1 << 10) | (1 << 11));
            xClient.data1 = IntPtr.Zero;
            xClient.data2 = IntPtr.Zero;
            xClient.data3 = new(size.x);
            xClient.data4 = new(size.y);
            
            XSendEvent(_display, _rootWindow, false, SubstructureRedirectMask | SubstructureNotifyMask, ref xClient);
            XFlush(_display);
        }
    }

    private void SetWindowSizeLegacy(Vector2 size)
    {
        if (_display != IntPtr.Zero && _unityWindow != IntPtr.Zero)
        {
            XResizeWindow(_display, _unityWindow, (int)size.x, (int)size.y);
            XFlush(_display);
        }
    }

    public Vector2Int GetMousePosition()
    {
        if (_windowManagerImplementation != null)
            return _windowManagerImplementation.GetMousePosition();
        // Query mouse position
        int rootX = 0, rootY = 0;
        IntPtr rootReturn = IntPtr.Zero, childReturn = IntPtr.Zero;
        int winX = 0, winY = 0;
        uint maskReturn = 0;

        if (!XQueryPointer(_display, _rootWindow, ref rootReturn, ref childReturn, ref rootX, ref rootY, ref winX,
                ref winY, ref maskReturn))
        {
            ShowError("No mouse found.");
            return Vector2Int.zero;
        }

        return new Vector2Int(rootX, rootY);
    }

    public bool GetMousePosition(out Vector2Int position)
    {
        if (_windowManagerImplementation != null)
        {
            position = _windowManagerImplementation.GetMousePosition();
            return true;
        }
        // Query mouse position
        int rootX = 0, rootY = 0;
        IntPtr rootReturn = IntPtr.Zero, childReturn = IntPtr.Zero;
        int winX = 0, winY = 0;
        uint maskReturn = 0;

        bool result = XQueryPointer(_display, _rootWindow, ref rootReturn, ref childReturn, ref rootX, ref rootY, ref winX, ref winY, ref maskReturn);
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
        if (!XQueryPointer(_display, _rootWindow, ref rootReturn, ref childReturn, ref rootX, ref rootY, ref winX,
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
        if (SaveLoadHandler.Instance.safeMode)
        {
            return Input.anyKeyDown;
        }
        if (_display == IntPtr.Zero) return false;

        byte[] keymap = new byte[32];

        if (XQueryKeymap(_display, keymap) == 0)
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
                    overlapMonitors.TryAdd(overlapWidth * overlapHeight, mon);
                }
            }

            if (overlapMonitors.Count > 0)
            {
                return overlapMonitors[overlapMonitors.Keys.Max()];
            }
        
            return RectInt.zero; 
        }
        return resultBasedOnWindowCenterPnt;
    }

    public RectInt GetMonitorFromHandle(IntPtr monitor)
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
        XGetWindowAttributes(_display, _unityWindow, out var attr);
        return new Vector2(attr.width, attr.height);
    }

    public Dictionary<IntPtr, RectInt> GetAllMonitors() => new(_monitors); // Copy to prevent external modification

    private List<IntPtr> FindWindowsByPid(int targetPid, bool excludeInvisible = true)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.FindWindowsByPid(targetPid);
        var result = new List<IntPtr>();

        var windows = GetAllWindows(excludeInvisible);

        foreach (var window in windows)
        {
            var pid = GetWindowPid(window);
            var cls = GetClassName(window);
            if (pid == targetPid)
            {
                result.Add(window);
            }
            
            if (pid == -1)
            {
                if (cls.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.Ordinal))
                {
                    result.Add(window);
                }
            }
        }

        return result;
    }

    public int GetWindowPid(IntPtr window)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetWindowPid(window);
        var pidAtom = XInternAtom(_display, "_NET_WM_PID", false);

        if (pidAtom == IntPtr.Zero)
        {
            ShowError("_NET_WM_PID atom not found");
            return -1;
        }
        
        var status = XGetWindowProperty(_display, window, pidAtom,
            0, 1, false, (IntPtr)XaCardinal,
            out _, out _,
            out var nItems, out _, out var prop);

        if (status == 0 && prop != IntPtr.Zero && nItems > 0)
        {
            var pid = Marshal.ReadInt32(prop);
            XFree(prop);
            return pid;
        }

        return -1;
    }

    private List<IntPtr> _cachedVisibleWindows;
    private DateTime _lastCacheTime;
    private const float CacheRefreshSeconds = 1f;

    private List<IntPtr> GetAllWindows(bool excludeInvisible = true)
    {
        if (_windowManagerImplementation != null)
            return _windowManagerImplementation.GetAllWindows();
        if (_cachedVisibleWindows != null && !((DateTime.Now - _lastCacheTime).TotalSeconds > CacheRefreshSeconds))
            return _cachedVisibleWindows;
        var result = new List<IntPtr>();

        var clientListAtom = XInternAtom(_display, "_NET_CLIENT_LIST", true);
        if (clientListAtom != IntPtr.Zero)
        {
            var status = XGetWindowProperty(_display, _rootWindow, clientListAtom, 0, 1024, false, (IntPtr)XaWindow,
                out var actualType, out var actualFormat, out var nItems, out _, out var prop);

            if (status == 0 && actualType == (IntPtr)XaWindow && actualFormat == 32 && prop != IntPtr.Zero)
            {
                for (ulong i = 0; i < nItems; i++)
                {
                    var win = Marshal.ReadIntPtr(prop, (int)(i * (ulong)IntPtr.Size));
                    if (excludeInvisible & IsWindowVisible(win) || !excludeInvisible)
                    {
                        result.Add(win);
                    }
                }

                XFree(prop);
                return result;
            }

            if (prop != IntPtr.Zero) XFree(prop);
            return new List<IntPtr>();
        }
        ShowError("Fallback to recursive enumeration because _NET_CLIENT_LIST is not available");
        EnumerateWindows(_rootWindow, result);
        _cachedVisibleWindows = result;
        return result;
    }

    private void EnumerateWindows(IntPtr window, List<IntPtr> accumulator)
    {
        var result = XGetWindowAttributes(_display, window, out var attr);
        if (result != 0 && attr.map_state == IsViewable)
        {
            accumulator.Add(window);

            if (XQueryTree(_display, window, out _, out _, out var children, out var nChildren) != 0)
            {
                if (children != IntPtr.Zero && nChildren > 0)
                {
                    try
                    {
                        for (var i = 0; i < nChildren; i++)
                        {
                            var child = Marshal.ReadIntPtr(children, i * IntPtr.Size);
                            EnumerateWindows(child, accumulator);
                        }
                    }
                    catch (Exception e)
                    {
                        ShowError(e.ToString());
                        throw;
                    }
                    finally
                    {
                        XFree(children);
                    }
                }
            }
        }
    }

    public bool GetWindowRect(out RectInt rect)
    {
        return GetWindowRect(_unityWindow, out rect);
    }

    public bool GetWindowRect(IntPtr window, out RectInt rect)
    {
        rect = new RectInt();
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetWindowRect(window, out rect);
        var result = XGetWindowAttributes(_display, window, out var attr);
        if (result == 0) return false;

        if (!XTranslateCoordinates(_display, window, _rootWindow, 0, 0, out var absX, out var absY, out _))
            return false;

        rect.x = absX;
        rect.y = absY;
        rect.width = attr.width;
        rect.height = attr.height;
        return true;
    }

    public void SetTopmost(bool topmost = true)
    {
#if UNITY_EDITOR
        return;
#endif
        if(_windowManagerImplementation != null)
        {
            _windowManagerImplementation.SetTopmost(topmost);
            return;
        }
        if (_closing || _display == IntPtr.Zero)
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
            type = ClientMessage,
            window = _unityWindow,
            message_type = _netWmState,
            format = 32,
        };
        xClient.data0 = new IntPtr(topmost ? 1 : 0); // 1=ADD, 0=REMOVE
        xClient.data1 = _netWmStateAbove;
        xClient.data2 = IntPtr.Zero;
        xClient.data3 = IntPtr.Zero;
        xClient.data4 = IntPtr.Zero;
        
        XSendEvent(_display, _rootWindow, false, SubstructureNotifyMask | SubstructureRedirectMask, ref xClient);
        XFlush(_display);
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
            type = ClientMessage,
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

        XSendEvent(_display, _rootWindow, false, SubstructureRedirectMask | SubstructureNotifyMask, ref msg);
        XFlush(_display);
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
            flags = (IntPtr)MwmHintsFlags,
            decorations = (IntPtr)MwmDecorationsNone,
            functions = IntPtr.Zero,
            input_mode = IntPtr.Zero,
            status = IntPtr.Zero
        };
        ChangeProperty(_motifHintsAtom, _motifHintsAtom, 32, PropModeReplace, hints, 5);
        XFlush(_display);
    }
    
    private void ChangeProperty<T>(IntPtr property, IntPtr type, int format, int mode, T data, int nelements)
    {
        GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            IntPtr ptr = handle.AddrOfPinnedObject();
            XChangeProperty(_display, _unityWindow, property, type, format, mode, ptr, nelements);
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
                ChangeProperty(_netWmWindowType, (IntPtr)XaAtom, 32, PropModeReplace, _netWmWindowTypeDock, 1);
                break;
            case WindowType.Desktop:
                ChangeProperty(_netWmWindowType, (IntPtr)XaAtom, 32, PropModeReplace, _netWmWindowTypeDesktop, 1);
                break;
            case WindowType.Normal:
                ChangeProperty(_netWmWindowType, (IntPtr)XaAtom, 32, PropModeReplace, _netWmWindowTypeNormal, 1);
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
        if (XGetClassHint(_display, window, out var hint) != 0)
        {
            var cls = Marshal.PtrToStringAnsi(hint.res_class);
            XFree(hint.res_name);
            XFree(hint.res_class);
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
            if (_desktop == IntPtr.Zero || XGetWindowAttributes(_display, _desktop, out _) == 0)
            {
                foreach (var win in GetAllWindows())
                {
                    if (!IsDesktop(win)) continue;
                    _desktop = win;
                    break;
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
                foreach (var win in GetAllWindows())
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
        var status = XGetWindowProperty(_display, hwnd, _netWmState, 0, 1024, false, (IntPtr)XaAtom, out _, out _, out var nItems, out _, out var prop);
        if (status != 0 || prop == IntPtr.Zero || nItems == 0) { if (prop != IntPtr.Zero) XFree(prop); return false; }
        bool maxH = false, maxV = false;
        for (ulong i = 0; i < nItems; i++)
        {
            var atom = Marshal.ReadIntPtr(prop, ((int)i * IntPtr.Size));
            if (atom == _netWmStateMaxHorz) maxH = true;
            if (atom == _netWmStateMaxVert) maxV = true;
        }
        XFree(prop);
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
        var status = XGetWindowProperty(_display, hwnd, _netWmState, 0, 1024, false, (IntPtr)XaAtom, out _, out _, out var nItems, out _, out var prop);
        var isFs = false;
        if (status == 0 && prop != IntPtr.Zero && nItems > 0)
        {
            for (ulong i = 0; i < nItems; i++)
            {
                if (Marshal.ReadIntPtr(prop, (int)i * IntPtr.Size) == _netWmStateFullscreen) { isFs = true; break; }
            }
            XFree(prop);
        }
        return isFs;
    }
        
    public bool IsWindowVisible(IntPtr window)
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.IsWindowVisible(window);
        if (_display == IntPtr.Zero) return false;

        var result = XGetWindowAttributes(_display, window, out var attr);
        if (result == 0 || attr.map_state != IsViewable) return false;
    
        if (!XTranslateCoordinates(_display, window, _rootWindow, 0, 0, out var absX, out var absY, out _))
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
            XGetWindowAttributes(_display, ow, out var oattr);
            if (oattr.map_state != IsViewable) continue;

            if (!XTranslateCoordinates(_display, ow, _rootWindow, 0, 0, out var oabsX, out var oabsY, out _))
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
        for (var screen = 0; screen < XScreenCount(_display); screen++)
        {
            var selectionAtom = XInternAtom(_display, "_NET_WM_CM_S" + screen, false);
            if (selectionAtom == IntPtr.Zero)
                continue;
            return XGetSelectionOwner(_display, selectionAtom) != 0;
        }
        return false;
    }
    
    public List<IntPtr> GetClientStackingList()
    {
        if(_windowManagerImplementation != null)
            return _windowManagerImplementation.GetClientStackingList();
        var atom = XInternAtom(_display, "_NET_CLIENT_LIST_STACKING", false);
        if (atom == IntPtr.Zero) return new List<IntPtr>();

        var status = XGetWindowProperty(_display, _rootWindow, atom, 0, 1024, false, (IntPtr)XaWindow,
            out _, out var actualFormat, out var nItems, out _, out var prop);

        if (status != 0 || prop == IntPtr.Zero || nItems == 0 || actualFormat != 32)
        {
            if (prop != IntPtr.Zero) XFree(prop);
            return new List<IntPtr>();
        }

        var windows = new List<IntPtr>((int)nItems);
        for (var i = 0; i < (int)nItems; i++)
        {
            var w = Marshal.ReadIntPtr(prop, (i * IntPtr.Size));
            windows.Add(w);
        }
        XFree(prop);
        return windows;
    }
        
    #region Window Shaping Logic
        
    private void EnableClickThroughTransparency()
    {
        if (_running || !transparentInputEnabled || SaveLoadHandler.Instance.safeMode) return;
        if (!SetupTransparentInput()) return;
        _running = true;
        _x11EventThread = new Thread(ApplyShaping)
        {
            Name = "WinShapeThread",
            IsBackground = true
        };

        _x11EventThread.Start();
    }

    private bool SetupTransparentInput()
    {
        if (XGetWindowAttributes(_display, _unityWindow, out var attrs) == 0)
        {
            ShowError("Failed to get window attributes");
            return false;
        }

        if (attrs.depth != 32 || !IsArgbVisual(_display, attrs.visual))
        {
            ShowError("Unity window does not have a 32-bit ARGB visual. Skipping shaping.");
            return false;
        }

        if (!IsCompositionSupported())
        {
            ShowError("No compositor found.");
            return false;
        }

        XCompositeRedirectWindow(_display, _unityWindow, CompositeRedirectAutomatic);
        
        _useShm = XShmQueryExtension(_display);
        if (_useShm)
        {
            int imageSize = attrs.width * attrs.height * 4;

            int shmid = shmget(IPC_PRIVATE, (IntPtr)imageSize, IPC_CREAT | 0x1FF);

            if (shmid == -1)
            {
                _useShm = false;
            }
            else
            {
                _shmInfo.shmid = shmid;
                _shmInfo.shmaddr = shmat(shmid, IntPtr.Zero, 0);
                _shmInfo.readOnly = 0;

                if (_shmInfo.shmaddr == new IntPtr(-1))
                {
                    _useShm = false;
                }
                else
                {
                    shmctl(shmid, IPC_RMID, IntPtr.Zero);

                    if (XShmAttach(_display, ref _shmInfo) == 0)
                    {
                        _useShm = false;
                        shmdt(_shmInfo.shmaddr);
                        _shmInfo.shmaddr = IntPtr.Zero;
                    }
                    else
                    {
                        XSync(_display, false);

                        _shmImagePtr = XShmCreateImage(_display, attrs.visual, (uint)attrs.depth,
                            ZPixmap, _shmInfo.shmaddr, ref _shmInfo, (uint)attrs.width, (uint)attrs.height);

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

        if (!XDamageQueryExtension(_display, out _damageEventBase, out _))
        {
            ShowError("XDamage extension not available");
            return false;
        }

        _damage = XDamageCreate(_display, _unityWindow, XDamageReportNonEmpty);
        if (_damage == IntPtr.Zero)
        {
            ShowError("Failed to create damage object");
            return false;
        }

        UpdateInputMask(attrs.width, attrs.height);
        return true;
    }

    private bool IsArgbVisual(IntPtr display, IntPtr visual)
    {
        var formatPtr = XRenderFindVisualFormat(display, visual);
        if (formatPtr == IntPtr.Zero) return false;

        var format = Marshal.PtrToStructure<XRenderPictFormat>(formatPtr);
        return format.type == PictTypeDirect && format.direct.alphaMask != 0;
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
        XShapeCombineRectangles(_display, _unityWindow, ShapeBounding, 0, 0, fullRect, 1, ShapeSet, Unsorted);

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

        IntPtr backingPixmap = XCompositeNameWindowPixmap(_display, _unityWindow);

        if (backingPixmap != IntPtr.Zero)
        {
            if (_useShm)
            {
                if (XShmGetImage(_display, backingPixmap, _shmImagePtr, 0, 0, AllPlanes))
                {
                    xImagePtr = _shmImagePtr;
                }
                else
                {
                    ShowError("XShmGetImage on Pixmap failed.");
                    _useShm = false;
                }
            }
            else
            {
                xImagePtr = XGetImage(_display, backingPixmap, 0, 0, (uint)width, (uint)height, AllPlanes, ZPixmap);
                if (xImagePtr == IntPtr.Zero)
                {
                    ShowError("XGetImage failed");
                    XFreePixmap(_display, backingPixmap);
                    return;
                }
            }

            XFreePixmap(_display, backingPixmap);
        }
        else
        {
            if (_useShm)
            {
                if (XShmGetImage(_display, _unityWindow, _shmImagePtr, 0, 0, AllPlanes))
                {
                    xImagePtr = _shmImagePtr;
                }
                else
                {
                    ShowError("XShmGetImage on Window failed unexpectedly.");
                    _useShm = false;
                }
            }
            else
            {
                xImagePtr = XGetImage(_display, _unityWindow, 0, 0, (uint)width, (uint)height, AllPlanes, ZPixmap);
                if (xImagePtr == IntPtr.Zero)
                {
                    ShowError("XGetImage failed");
                    return;
                }
            }
        }

        imageBytes = GetImageData(xImagePtr, width, height).Data;

        if (!_useShm && xImagePtr != IntPtr.Zero)
        {
            XDestroyImage(xImagePtr);
        }

        List<XRectangle> rects = GenerateRectangles(imageBytes, width, height);
        XRectangle[] rectArray = rects.ToArray();

        XShapeCombineRectangles(_display, _unityWindow, ShapeInput, 0, 0, rectArray, rectArray.Length, ShapeSet, YSorted);

        XFlush(_display);
    }

    private bool TryResizeShm(int width, int height)
    {
        if (_shmImagePtr != IntPtr.Zero)
        {
            XDestroyImage(_shmImagePtr);
            _shmImagePtr = IntPtr.Zero;
        }
        
        if (_shmInfo.shmaddr != IntPtr.Zero)
        {
            XShmDetach(_display, ref _shmInfo);
            shmdt(_shmInfo.shmaddr);
            _shmInfo.shmaddr = IntPtr.Zero;
        }

        if (XGetWindowAttributes(_display, _unityWindow, out var attrs) == 0) return false;

        int imageSize = width * height * 4;
        int shmid = shmget(IPC_PRIVATE, (IntPtr)imageSize, IPC_CREAT | 0x1FF);

        if (shmid == -1) return false;

        _shmInfo = new XShmSegmentInfo
        {
            shmid = shmid,
            shmaddr = shmat(shmid, IntPtr.Zero, 0),
            readOnly = 0
        };

        if (_shmInfo.shmaddr == new IntPtr(-1))
        {
            shmctl(shmid, IPC_RMID, IntPtr.Zero);
            return false;
        }

        shmctl(shmid, IPC_RMID, IntPtr.Zero);

        if (XShmAttach(_display, ref _shmInfo) == 0)
        {
            shmdt(_shmInfo.shmaddr);
            _shmInfo.shmaddr = IntPtr.Zero;
            return false;
        }

        XSync(_display, false);

        _shmImagePtr = XShmCreateImage(_display, attrs.visual, (uint)attrs.depth,
            ZPixmap, _shmInfo.shmaddr, ref _shmInfo, (uint)width, (uint)height);

        if (_shmImagePtr == IntPtr.Zero)
        {
            XShmDetach(_display, ref _shmInfo);
            shmdt(_shmInfo.shmaddr);
            _shmInfo.shmaddr = IntPtr.Zero;
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
                XNextEvent(_display, ref ev);

                switch (ev.type)
                {
                    case ConfigureNotify:
                    {
                        var ce = ev.configureEvent;
                        if (ce.window == _unityWindow)
                        {
                            UpdateInputMask(ce.width, ce.height);
                        }
                        break;
                    }
                    case DestroyNotify:
                    {
                        var de = ev.destroyWindowEvent;
                        if (de.window == _unityWindow)
                        {
                            _running = false;
                        }
                        break;
                    }
                    case EnterNotify:
                    {
                        _mouseOver = true;
                        break;
                    }
                    case LeaveNotify:
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
                                XDamageSubtract(_display, de.damage, IntPtr.Zero, IntPtr.Zero);
                                XGetWindowAttributes(_display, _unityWindow, out var attrs);
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
        var cursor = XcursorLibraryLoadCursor(_display, cursorName);
        if (cursor == IntPtr.Zero)
        {
            Debug.LogWarning($"WindowManager: Failed to load themed cursor '{cursorName}', falling back to font cursor.");
            cursor = XCreateFontCursor(_display, fallbackShape);
        }
        return cursor;
    }

    private void LoadCursors()
    {
        if (_display == IntPtr.Zero || _unityWindow == IntPtr.Zero) return;
        if (_currentSessionType == SessionTypes.Wayland || _currentDesktopEnv == DesktopEnvironments.Hyprland) return;

        _defaultCursor  = LoadThemedCursor("left_ptr", XC_LEFT_PTR);
        _grabCursor     = LoadThemedCursor("grab",     XC_HAND2);
        _grabbingCursor = LoadThemedCursor("grabbing", XC_HAND2);
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
        XDefineCursor(_display, _unityWindow, toSet);
        XFlush(_display);
    }
    #endregion
        
    #region API

    private IntPtr _display;
    private IntPtr _rootWindow;
    private IntPtr _unityWindow;
    private Dictionary<IntPtr, RectInt> _monitors;
    private IntPtr _netWmState, _netWmStateFullscreen, _netWmStateMaxHorz, _netWmStateMaxVert, _netWmStateAbove, _netWmStateSkipTaskbar;
    private IntPtr _netWmWindowType, _netWmWindowTypeDock, _netWmWindowTypeDesktop, _netWmWindowTypeNormal;
    private IntPtr _netMoveResizeWindow;
    private IntPtr _motifHintsAtom;
    
    private IntPtr _defaultCursor;
    private IntPtr _grabCursor;
    private IntPtr _grabbingCursor;
    private volatile bool _mouseOver;

    private int _damageEventBase;
    private IntPtr _damage = IntPtr.Zero;
    private volatile bool _running;
    private volatile bool _closing;
    private Thread _x11EventThread;
    private Stopwatch _shapingStopwatch;
    private bool _useShm;
    private XShmSegmentInfo _shmInfo;
    private IntPtr _shmImagePtr;
    private int _shmWidth;
    private int _shmHeight;

    private const long ShapingThrottleMs = 100; // Update mask every 100ms
    
    // Constants
    private const int XaCardinal = 6;
    private const int XaAtom = 4;
    private const int IsViewable = 2;
    private const int XaWindow = 33;
    
    private const long MwmHintsFlags = 1L << 1; // Use decorations
    private const long MwmDecorationsNone = 0; // No decorations
    private const int PropModeReplace = 0;

    private const int ClientMessage = 33;
    private const long StructureNotifyMask = (1L << 17);
    private const long SubstructureRedirectMask = 0x00080000;
    private const long SubstructureNotifyMask = 0x00040000;
    private const long EnterWindowMask = (1L << 4);
    private const long LeaveWindowMask = (1L << 5);
    private const long PropertyChangeMask = (1L << 22);
    private const int ConfigureNotify = 22;
    private const int DestroyNotify = 17;
    private const int PropertyNotify = 28;
    private const int ShapeBounding = 0;
    private const int ShapeInput = 2;
    private const int ShapeSet = 0;
    private const int PictTypeDirect = 1;
    private const int XDamageReportNonEmpty = 3;
    private const int ZPixmap = 2;
    private const ulong AllPlanes = 0xFFFFFFFFFFFFFFFFUL; // For 64-bit
    private const int Unsorted = 0;
    private const int YSorted = 1;
    private const uint XC_LEFT_PTR = 68;
    private const uint XC_HAND2 = 60;
    private const int EnterNotify = 7;
    private const int LeaveNotify = 8;
    private const int CompositeRedirectAutomatic = 0;

    private const int IPC_RMID = 0;
    private const int IPC_PRIVATE = 0;
    private const int IPC_CREAT = 00001000;
    
    public const string LibX11 = "libX11.so.6";
    private const string LibXExt = "libXext.so.6";
    public const string LibXRender = "libXrender.so.1";
    private const string LibXDamage = "libXdamage.so.1";
    private const string LibXRandR = "libXrandr.so.2";
    private const string LibXCursor = "libXcursor.so.1";
    private const string LibXComposite = "libXcomposite.so.1";
    private const string LibC = "libc.so.6";

    // X11 Event structures
    [StructLayout(LayoutKind.Sequential)]
    private struct XClientMessageEvent
    {
        public int type;
        public IntPtr serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr message_type;
        public int format;
        public IntPtr data0;
        public IntPtr data1;
        public IntPtr data2;
        public IntPtr data3;
        public IntPtr data4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public int x, y;
        public int width, height;
        public int border_width;
        public int depth;
        public IntPtr visual;
        public IntPtr root;
        public int c_class;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public ulong backing_planes;
        public ulong backing_pixel;
        public bool save_under;
        public IntPtr colormap;
        public bool map_installed;
        public int map_state;
        public long all_event_masks;
        public long your_event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public IntPtr screen;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct XSetWindowAttributes
    {
        public IntPtr background_pixmap;
        public ulong background_pixel;
        public IntPtr border_pixmap;
        public ulong border_pixel;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public ulong backing_planes;
        public ulong backing_pixel;
        public bool save_under;
        public long event_mask;
        public long do_not_propagate_mask;
        public bool override_redirect;
        public IntPtr colormap;
        public IntPtr cursor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XClassHint
    {
        public IntPtr res_name;
        public IntPtr res_class;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct XEvent
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(0)] public XAnyEvent anyEvent;
        [FieldOffset(0)] public XConfigureEvent configureEvent;
        [FieldOffset(0)] public XDestroyWindowEvent destroyWindowEvent;
        [FieldOffset(0)] public XDamageNotifyEvent damageNotifyEvent;
        [FieldOffset(0)] public XClientMessageEvent clientMessageEvent;
        [FieldOffset(0)] public XSelectionEvent selectionEvent;
        [FieldOffset(0)] public XCrossingEvent crossingEvent;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct XCrossingEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
        public IntPtr root;
        public IntPtr subwindow;
        public ulong time;
        public int x, y;
        public int x_root, y_root;
        public int mode;
        public int detail;
        public bool same_screen;
        public bool focus;
        public uint state;
    }
        
    [StructLayout(LayoutKind.Sequential)]
    private struct XSelectionEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr requestor;
        public IntPtr selection;
        public IntPtr target;
        public IntPtr property;
        public ulong time;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XAnyEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr window;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XConfigureEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr event_window;
        public IntPtr window;
        public int x, y;
        public int width, height;
        public int border_width;
        public IntPtr above;
        public bool override_redirect;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XDestroyWindowEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr event_window;
        public IntPtr window;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XDamageNotifyEvent
    {
        public int type;
        public ulong serial;
        public bool send_event;
        public IntPtr display;
        public IntPtr drawable;
        public IntPtr damage;
        public int level;
        public bool more;
        public ulong timestamp;
        public XRectangle area;
        public XRectangle geometry;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XRectangle
    {
        public short x, y;
        public ushort width, height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XRenderPictFormat
    {
        public IntPtr id;
        public int type;
        public int depth;
        public XRenderDirectFormat direct;
        public IntPtr colormap;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XRenderDirectFormat
    {
        public short red;
        public short redMask;
        public short green;
        public short greenMask;
        public short blue;
        public short blueMask;
        public short alpha;
        public short alphaMask;
    }

    private struct Image
    {
        public byte[] Data;
        public int Width, Height;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct XImage
    {
        public int width, height;
        public int xoffset;
        public int format;
        public IntPtr data;
        public int byte_order;
        public int bitmap_unit;
        public int bitmap_bit_order;
        public int bitmap_pad;
        public int depth;
        public int bytes_per_line;
        public int bits_per_pixel;
    }
        
    [Serializable]
    public struct RectF
    {
        public float x1, y1, x2, y2;
    }

    private struct CoveredFractionCalculator
    {
        [BurstCompile(CompileSynchronously = true)]
        public static float Compute(NativeArray<RectF> covers, RectF target, int gridSize = 10)
        {
            int totalSamples = gridSize * gridSize;
            int coveredSamples = 0;

            for (int gy = 0; gy < gridSize; gy++)
            {
                float y = target.y1 + (target.y2 - target.y1) * (gy + 0.5f) / gridSize;
                for (int gx = 0; gx < gridSize; gx++)
                {
                    float x = target.x1 + (target.x2 - target.x1) * (gx + 0.5f) / gridSize;

                    // Check if this sample point is inside ANY cover rectangle
                    bool isCovered = false;
                    for (int j = 0; j < covers.Length; j++)
                    {
                        var c = covers[j];
                        if (x >= c.x1 && x < c.x2 && y >= c.y1 && y < c.y2)
                        {
                            isCovered = true;
                            break;  // Early exit: no need to check more
                        }
                    }

                    if (isCovered) coveredSamples++;
                }
            }

            return (float)coveredSamples / totalSamples;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XMotifWmHints
    {
        public IntPtr flags;
        public IntPtr functions;
        public IntPtr decorations;
        public IntPtr input_mode;
        public IntPtr status;
    }
        
    [StructLayout(LayoutKind.Sequential)]
    private struct XrrScreenResources
    {
        public IntPtr timestamp;
        public IntPtr configTimestamp;
        public int ncrtc;
        public IntPtr crtcs;        // IntPtr* (array of XID)
        public int noutput;
        public IntPtr outputs;      // IntPtr* (array of XID)
        public int nmode;
        public IntPtr modes;        // pointer to array of XRRScreenModeInfo
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XrrOutputInfo
    {
        public IntPtr timestamp;
        public IntPtr crtc;         // XID of current CRTC, or None
        public IntPtr name;         // pointer to null-terminated string
        public int nameLen;
        public long mm_width;       // physical width in millimeters
        public long mm_height;      // physical height in millimeters
        public Connection connection; // 0 = connected, 1 = disconnected, 2 = unknown
        public byte subpixel_order;
        public int ncrtc;
        public IntPtr crtcs;        // array of possible CRTCs
        public int nclone;
        public IntPtr clones;
        public int nmode;
        public int npreferred;
        public IntPtr modes;        // array of mode XIDs
    }
        
    private enum Connection : byte
    {
        Connected = 0,
        Disconnected = 1,
        Unknown = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XrrCrtcInfo
    {
        public IntPtr timestamp;
        public int x, y;            // absolute position of CRTC
        public uint width, height;  // size in pixels
        public int mode;            // current mode XID
        public Rotation rotation;   // current rotation
        public int noutput;
        public IntPtr outputs;      // array of output XIDs currently driven
        public int npossible;
        public IntPtr possible;     // array of possible outputs
    }
        
    [Flags]
    private enum Rotation
    {
        Rotate0   = 1 << 0,
        Rotate90  = 1 << 1,
        Rotate180 = 1 << 2,
        Rotate270 = 1 << 3,
        ReflectX  = 1 << 4,
        ReflectY  = 1 << 5
    }

    private delegate int XErrorHandler(IntPtr display, IntPtr errorEvent);

    [StructLayout(LayoutKind.Sequential)]
    private struct XErrorEvent
    {
        public int type;
        public IntPtr display;
        public ulong resourceid;
        public ulong serial;
        public byte error_code;
        public byte request_code;
        public byte minor_code;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct XShmSegmentInfo
    {
        public IntPtr shmseg;
        public int shmid;
        private int padding;
        public IntPtr shmaddr;
        public int readOnly;
        private int padding2;
    }

    // X11 Library imports
    [DllImport(LibX11)]
    private static extern int XInitThreads();
    
    [DllImport(LibX11)]
    private static extern IntPtr XOpenDisplay(string displayName);

    [DllImport(LibX11)]
    private static extern void XCloseDisplay(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XInternAtom(IntPtr display, string atomName, bool onlyIfExists);

    [DllImport(LibX11)]
    private static extern int XGetWindowProperty(IntPtr display, IntPtr window, IntPtr property,
        long longOffset, long longLength, bool delete, IntPtr reqType,
        out IntPtr actualTypeReturn, out int actualFormatReturn,
        out ulong nItemsReturn, out ulong bytesAfterReturn, out IntPtr propReturn);

    [DllImport(LibX11)]
    private static extern int XGetGeometry(IntPtr display, IntPtr w, out IntPtr rootReturn, out int x, out int y,
        out int width, out int height, out int borderWidth, out uint depth);

    public int GetGeometry(out IntPtr rootReturn, out int x, out int y, out int width, out int height,
        out int borderWidth, out uint depth) => XGetGeometry(_display, _unityWindow, out rootReturn, out x, out y,
        out width, out height, out borderWidth, out depth);

    [DllImport(LibX11)]
    private static extern int XFree(IntPtr data);

    [DllImport(LibX11)]
    private static extern int XQueryTree(IntPtr display, IntPtr window,
        out IntPtr rootReturn, out IntPtr parentReturn,
        out IntPtr childrenReturn, out uint nChildrenReturn);

    [DllImport(LibX11)]
    private static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);
    
    [DllImport(LibX11)]
    private static extern int XChangeWindowAttributes(IntPtr display, IntPtr window, ulong valuemask, ref XSetWindowAttributes attributes);

    [DllImport(LibX11)]
    private static extern int XMoveWindow(IntPtr display, IntPtr window, int x, int y);

    [DllImport(LibX11)]
    private static extern int XResizeWindow(IntPtr display, IntPtr window, int width, int height);

    [DllImport(LibX11)]
    private static extern bool XQueryPointer(IntPtr display, IntPtr window, ref IntPtr windowReturn,
        ref IntPtr childReturn,
        ref int rootX, ref int rootY, ref int winX, ref int winY, ref uint mask);
        
    [DllImport(LibX11)]
    private static extern int XQueryKeymap(IntPtr display, [In, Out] byte[] keymap);

    [DllImport(LibX11)]
    private static extern int XFlush(IntPtr display);
        
    [DllImport(LibX11)]
    private static extern int XScreenCount(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XGetSelectionOwner(IntPtr display, IntPtr atom);

    [DllImport(LibX11)]
    private static extern int XSendEvent(IntPtr display, IntPtr window, bool propagate,
        long eventMask, ref XClientMessageEvent eventSend);

    [DllImport(LibX11)]
    private static extern IntPtr XRootWindow(IntPtr display, int screenNumber);

    [DllImport(LibX11)]
    private static extern bool XTranslateCoordinates(IntPtr display, IntPtr srcW, IntPtr destW,
        int srcX, int srcY, out int destX, out int destY, out IntPtr child);

    [DllImport(LibX11)]
    private static extern int XGetClassHint(IntPtr display, IntPtr w, out XClassHint classHints);

    [DllImport(LibX11)]
    private static extern int XDisplayWidth(IntPtr display, int screen);
        
    public int DisplayWidth(IntPtr display, int screen) => XDisplayWidth(display, screen);

    [DllImport(LibX11)]
    private static extern int XDisplayHeight(IntPtr display, int screen);
        
    public int DisplayHeight(IntPtr display, int screen) => XDisplayHeight(display, screen);

    [DllImport(LibX11)]
    private static extern void XSync(IntPtr display, bool discard);

    [DllImport(LibX11)]
    private static extern int XChangeProperty(IntPtr display, IntPtr window, IntPtr property, IntPtr type,
        int format, int mode, [In, Out] IntPtr data, int nItems);

    [DllImport(LibX11)]
    private static extern int XSelectInput(IntPtr display, IntPtr window, long eventMask);

    [DllImport(LibXDamage)]
    private static extern bool XDamageQueryExtension(IntPtr display, out int eventBase, out int errorBase);

    [DllImport(LibXDamage)]
    private static extern IntPtr XDamageCreate(IntPtr display, IntPtr drawable, int level);

    [DllImport(LibXDamage)]
    private static extern void XDamageDestroy(IntPtr display, IntPtr damage);

    [DllImport(LibXDamage)]
    private static extern void XDamageSubtract(IntPtr display, IntPtr damage, IntPtr repair, IntPtr parts);

    [DllImport(LibXRender)]
    private static extern IntPtr XRenderFindVisualFormat(IntPtr display, IntPtr visual);

    [DllImport(LibX11)]
    public static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height,
        ulong planeMask, int format);

    [DllImport(LibX11)]
    public static extern int XDestroyImage(IntPtr xImage);

    [DllImport(LibX11)]
    private static extern ulong XGetPixel(IntPtr xImage, int x, int y);

    [DllImport(LibX11)]
    private static extern int XNextEvent(IntPtr display, ref XEvent ev);
        
    [DllImport(LibXRandR)]
    private static extern int XRRQueryExtension(IntPtr display, out int eventBase, out int errorBase);

    [DllImport(LibXRandR)]
    private static extern int XRRQueryVersion(IntPtr display, out int major, out int minor);

    [DllImport(LibXRandR)]
    private static extern IntPtr XRRGetScreenResourcesCurrent(IntPtr display, IntPtr window);

    [DllImport(LibXRandR)]
    private static extern void XRRFreeScreenResources(IntPtr resources);

    [DllImport(LibXRandR)]
    private static extern IntPtr XRRGetOutputInfo(IntPtr display, IntPtr resources, IntPtr output);

    [DllImport(LibXRandR)]
    private static extern void XRRFreeOutputInfo(IntPtr outputInfo);

    [DllImport(LibXRandR)]
    private static extern IntPtr XRRGetCrtcInfo(IntPtr display, IntPtr resources, IntPtr crtc);

    [DllImport(LibXRandR)]
    private static extern void XRRFreeCrtcInfo(IntPtr crtcInfo);

    [DllImport(LibX11)]
    private static extern IntPtr XGetAtomName(IntPtr display, IntPtr atom);

    [DllImport(LibX11, CharSet = CharSet.Ansi)]
    private static extern void XGetErrorText(IntPtr display, int code, StringBuilder buffer, int size);

    [DllImport(LibX11)]
    private static extern XErrorHandler XSetErrorHandler(XErrorHandler handler);
    
    [DllImport(LibX11)]
    private static extern int XQueryExtension(IntPtr display, string name, out int opcode, out int first_event, out int first_error);
    
    [DllImport(LibX11)]
    private static extern IntPtr XListExtensions(IntPtr display, out int nExtensions);
    
    [DllImport(LibX11)]
    private static extern int XSetTransientForHint(IntPtr display, IntPtr w, IntPtr propWindow);
    
    [DllImport(LibXExt)]
    private static extern void XShapeCombineRectangles(
        IntPtr display, 
        IntPtr window, 
        int destKind, 
        int xOff, int yOff, 
        XRectangle[] rectangles, 
        int nRects, 
        int op, 
        int ordering
    );
    
    [DllImport(LibXCursor)]
    private static extern IntPtr XcursorLibraryLoadCursor(IntPtr display, string name);

    [DllImport(LibX11)]
    private static extern IntPtr XCreateFontCursor(IntPtr display, uint shape);

    [DllImport(LibX11)]
    private static extern int XDefineCursor(IntPtr display, IntPtr window, IntPtr cursor);

    [DllImport(LibX11)]
    private static extern int XFreeCursor(IntPtr display, IntPtr cursor);
    
    [DllImport(LibXExt)]
    private static extern bool XShmQueryExtension(IntPtr display);

    [DllImport(LibXExt)]
    private static extern int XShmAttach(IntPtr display, ref XShmSegmentInfo shminfo);

    [DllImport(LibXExt)]
    private static extern int XShmDetach(IntPtr display, ref XShmSegmentInfo shminfo);

    [DllImport(LibXExt)]
    private static extern IntPtr XShmCreateImage(IntPtr display, IntPtr visual, uint depth,
        int format, IntPtr data, ref XShmSegmentInfo shminfo, uint width, uint height);

    [DllImport(LibXExt)]
    private static extern bool XShmGetImage(IntPtr display, IntPtr drawable, IntPtr image,
        int x, int y, ulong plane_mask);

    [DllImport(LibC)]
    private static extern int shmget(uint key, IntPtr size, int shmflg);

    [DllImport(LibC)]
    private static extern IntPtr shmat(int shmid, IntPtr shmaddr, int shmflg);

    [DllImport(LibC)]
    private static extern int shmdt(IntPtr shmaddr);
    
    [DllImport(LibC)]
    private static extern int shmctl(int shmid, int cmd, IntPtr buf);
    
    [DllImport(LibXComposite)]
    private static extern void XCompositeRedirectWindow(IntPtr display, IntPtr window, int update);
    
    [DllImport(LibXComposite)]
    private static extern void XCompositeUnredirectWindow(IntPtr display, IntPtr window, int update);

    [DllImport(LibXComposite)]
    private static extern IntPtr XCompositeNameWindowPixmap(IntPtr display, IntPtr window);

    [DllImport(LibX11)]
    private static extern int XFreePixmap(IntPtr display, IntPtr pixmap);
    
    [DllImport(LibX11)]
    private static extern int XStoreName(IntPtr display, IntPtr window, string window_name);
    
    #endregion
}