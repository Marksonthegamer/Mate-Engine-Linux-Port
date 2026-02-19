using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using UnityEditor;
using System.Threading;
using Unity.VisualScripting;
using System.Runtime.CompilerServices;
using UnityEngine.Timeline;
using Gdk;
using System.Collections.Concurrent;


public class HyprlandManager : IDisposable, IWindowManagerImplementation
{
    const int _UpdateDelay = 250;
    const int _CloseDelay = 25;
    const int _FocusableUpdateDelay = 5;

    Vector2Int _LastCursorPosition = Vector2Int.zero;
    bool _CursorOver = false;
    bool _Focusable = false;
    CancellationTokenSource _CancellationTokenSource;

    ConcurrentDictionary<IntPtr, HyprlandClient> _Clients;

    Vector2Int? _NewWindowPosition = null;
    Vector2Int? _NewWindowSize = null;

    bool _MouseOverWindow;

    bool _IsDragging;

    public bool IsDragging
    {
        get => _IsDragging;
        set
        {
            _IsDragging = value;
            //ShowError(value.ToString());
        }
    }

    HyprlandClient _Window = null;

    public HyprlandManager() 
    {
        _Clients = new ConcurrentDictionary<IntPtr, HyprlandClient>();
        _CancellationTokenSource = new CancellationTokenSource();
        _LoopTask = Task.Run(async () => Update(_CancellationTokenSource.Token), _CancellationTokenSource.Token);
    }

    const string _Hyprctl = "/usr/bin/hyprctl";

    static void SetProp(string windowAddress, string propName, params object[] propValue)
    {
        var args = new string[4 + propValue.Length];
        args[0] = "dispatch";
        args[1] = "setprop";
        args[2] = $"address:{windowAddress}";
        args[3] = propName;
        for(int i = 4; i < propValue.Length + 4; i++)
            args[i] = propValue[i - 4]?.ToString();
        var result = RunCommand(_Hyprctl, args);
        if(!CommandSuccessful(result))
            ShowError($"{propName}: {result}");
    }

    static string GetProp(string windowAddress, string propName)
    {
        return RunCommand(_Hyprctl, "getprop", $"address:{windowAddress}", propName );
    }

    public void HideFromTaskbar(bool reallyHide)
    {
        // ShowError(reallyHide);
    }

    public void SetWindowBorderless()
    {

    }

    static string IntPtrToHex(IntPtr ptr) => ptr.ToString("X8");

    public void SetWindowType(WindowType type)
    {
    }

    Task _LoopTask;

    void SetInitialWindowProps()
    {
        RunCommand(_Hyprctl, "dispatch", "setfloating", $"address:{_Window.address}");
        SetProp(_Window.address, "no_focus", true);
        SetProp(_Window.address, "decorate", false);
        SetProp(_Window.address, "no_blur", "on");
        // SetProp(_Window.address, "opacity", 2, 2);
        //SetProp(_Window.address, "border_size", 0);
        // SetProp(_Window.address, "opaque", false);
        // SetProp(_Window.address, "immediate", "on");

        var data = SaveLoadHandler.Instance.data;
        Vector2Int size = Vector2Int.zero;
        switch (data.windowSizeState)
        {
            case SaveLoadHandler.SettingsData.WindowSizeState.Normal:
                size = new Vector2Int(1536, 1024);
                break;
            case SaveLoadHandler.SettingsData.WindowSizeState.Big:
                size = new Vector2Int(2048, 1536);
                break;
            case SaveLoadHandler.SettingsData.WindowSizeState.Small:
                size = new Vector2Int(768, 512); 
                break;
        }
        _NewWindowSize = size;
    }

    async Task Update(CancellationToken cancellationToken)
    {
        var c = 0;
        var d = 0;
        while(!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if(_Window == null)
                {
                    UpdateMonitors();
                    UpdateWindows();
                    if(_Window != null)
                        SetInitialWindowProps();
                }
                else
                {
                    UpdateMousePosition();
                    SetNewWindowSize();
                    SetNewWindowPosition();
                    if(c == 2)
                    {
                        UpdateWindows();
                        UpdateFocusable();
                        c = 0;
                    }
                    if(d == 100)
                    {
                        UpdateMonitors();
                        d = 0;
                    }
                }
            }
            catch(Exception ex)
            {
                ShowError(ex.ToString());
            }
            var delay = _UpdateDelay;
            if(_MouseOverWindow)
                delay = _CloseDelay;
            if(_Focusable)
                delay = _FocusableUpdateDelay;
            await Task.Delay(delay);
            // ShowError($"LoopDelay: {delay}");
            c++;
            d++;
        }
    }

    static HyprlandClients GetHyprlandClients()
    {
        var output = RunCommand(_Hyprctl, "clients", "-j");
        output = $"{{ \"clients\" : {output} }}";
        return JsonUtility.FromJson<HyprlandClients>(output);
    }

    void UpdateMousePosition()
    {
        string output = RunCommand(_Hyprctl, "cursorpos");
        _LastCursorPosition = HyprlandVectorToVector2Int(output);
        // ShowError($"MousePos: {_LastCursorPosition}");
    }

    void UpdateWindows()
    {
        var clients = GetHyprlandClients();
        if(_Window == null)
            _Window = clients.clients.FirstOrDefault(a => a.pid == Process.GetCurrentProcess().Id);
        else
            _Window = clients.clients.FirstOrDefault(a => a.address == _Window.address);
        if(_Window == null)
            return;
        

        var closedWindows = _Clients.Where(existing => !clients.clients.Any(found => found.address == existing.Value.address)).Select(a => a.Key).ToList();
        foreach(var closedWindow in closedWindows)
            _Clients.TryRemove(closedWindow,out _);

        foreach(var client in clients.clients)
            _Clients[client.addressIntPtr] = client;

        // foreach(var client in _Clients)
        //     ShowError($"{client.Key}: {client.Value.address}, {client.Value.title}, {client.Value.atVector}, {client.Value.sizeVector}");
    }

    void SetNewWindowPosition()
    {
        if(_NewWindowPosition != null && _NewWindowPosition != _Window.atVector)
        {
            var output = RunCommand(_Hyprctl, $"dispatch movewindowpixel exact {_NewWindowPosition.Value.x} {_NewWindowPosition.Value.y} , address:{_Window.address}");
            if(!CommandSuccessful(output))
                ShowError(output);
            _Window.at = new int[] { _NewWindowPosition.Value.x, _NewWindowPosition.Value.y };
            _NewWindowPosition = null;
        }
    }

    void SetNewWindowSize()
    {
        if(_NewWindowSize != null && _NewWindowSize != _Window.sizeVector)
        {
            var output = RunCommand(_Hyprctl,  $"dispatch resizewindowpixel exact {_NewWindowSize.Value.x} {_NewWindowSize.Value.y} , address:{_Window.address}");
            if(!CommandSuccessful(output))
                ShowError(output);
            _Window.size = new int[] { _NewWindowSize.Value.x, _NewWindowSize.Value.y };
            _NewWindowSize = null;
        }
    }


    void UpdateFocusable()
    {
        var forceFocus = MenuActions.IsAnyMenuOpen() || IsDragging;
        if(forceFocus)
        {
            SetFocusable(true);
            _MouseOverWindow = true;
        }
        else
        {
            var windowRect = new Rect(_Window.atVector.x ,_Window.atVector.y,_Window.sizeVector.x, _Window.sizeVector.y);
            _MouseOverWindow = windowRect.Contains(_LastCursorPosition);
            if(_MouseOverWindow)
            {
                // ShowError($"Cursor: {_LastCursorPosition}");
                var correction = SaveLoadHandler.Instance.data.avatarSize - 0.10F;
                var avatarScale = SaveLoadHandler.Instance.data.avatarSize - (0.28F * correction);
                var avatarHeight = _Window.sizeVector.y * avatarScale;
                var avatarWidth = avatarHeight / 4.5625F;
                var verticalOffset = _Window.sizeVector.y - avatarHeight;
                var horizontalOffset = (_Window.sizeVector.x - avatarWidth) / 2;
                
                // ShowError($"windowRect: {windowRect}");
                var avatarRect = new Rect(_Window.atVector.x + horizontalOffset,_Window.atVector.y + verticalOffset, _Window.sizeVector.x - horizontalOffset * 2, _Window.sizeVector.y - verticalOffset);
                // ShowError($"avatarRect: {avatarRect}");
                var cursorOver = avatarRect.Contains(_LastCursorPosition);
                if(_CursorOver != cursorOver)
                {
                    SetFocusable(cursorOver);
                    _CursorOver = cursorOver;
                }
            }
            // ShowError($"CursorOver: {_CursorOver}");
        }
    }

    List<HyprlandMonitor> _Monitors;

    void UpdateMonitors()
    {
        var output = RunCommand(_Hyprctl, "monitors", "-j");
        output = $"{{ \"monitors\" : {output} }}";
        var monitors = JsonUtility.FromJson<HyprlandMonitors>(output);
        // foreach(var mon in monitors.monitors)
        //     ShowError($"{mon.name}: {mon.position} , {mon.size}");
        _Monitors = monitors.monitors.ToList();
    }

    public Vector2Int GetMousePosition() 
    {
        return _LastCursorPosition;
    }

    void SetFocusable(bool focusable)
    {
        _Focusable = focusable;
        SetProp(_Window.address, "no_focus", !focusable);
    }

    public void SetWindowPosition(Vector2Int position)
    {
        // ShowError(position);
        _NewWindowPosition = position;
    }

    public void SetWindowSize(Vector2Int size)
    {
        // ShowError(size);
        _NewWindowSize = size;
    }

    Vector2Int _LastPrintedPost = Vector2Int.zero;

    public Vector2Int GetWindowPosition()
    {
        var winPos = _Window.atVector;
        if(_NewWindowPosition != null)
            winPos = _NewWindowPosition.Value;
        // if(_LastPrintedPost != winPos)
        //     ShowError(winPos);
        // _LastPrintedPost = winPos;
        return winPos;
    }

    private static Vector2Int  HyprlandVectorToVector2Int(string hyprlandVector)
    {
        var parts = hyprlandVector.Trim().Split(',');
        if(parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
            return new Vector2Int(x,y);
        return Vector2Int.zero;
    }

    private static void ShowError(object messageObject,[CallerMemberName] string callsource = "")
    {
        Console.WriteLine($"\u001b[31m{nameof(HyprlandManager)}.{callsource}: {messageObject}\u001b[0m");
    }

    static bool CommandSuccessful(string commandoutput) => string.IsNullOrWhiteSpace(commandoutput) || commandoutput?.ToLower() == "ok";

    private static string RunCommand(string file, params string[] arguments)
    {
        ProcessStartInfo psi = new ProcessStartInfo()
        {
            FileName = file,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.AddRange(arguments);

        using (Process p = Process.Start(psi))
        {
            p?.WaitForExit();
            return p?.StandardOutput.ReadToEnd().Trim();
        }
    }


    public int GetWindowPid(IntPtr window)
    {
        var pid = -1;
        if(window == _XUnityWindow)
            window = _Window.addressIntPtr;
        if(_Clients.ContainsKey(window))
            pid = _Clients[window].pid;
        // ShowError($"{IntPtrToHex(window)} {pid}");
        return pid;
    }

    public List<IntPtr> FindWindowsByPid(int targetPid)
    {
        var windows = _Clients.Where(a => a.Value.pid == targetPid).Select(a => a.Key).ToList();
        // ShowError($"{targetPid} {string.Join(",",windows.Select(a => IntPtrToHex(a)))}");
        return windows;
    }

    public List<IntPtr> GetAllVisibleWindows()
    {
        var windows = _Clients.Keys.ToList();
        // ShowError(string.Join(",",windows));
        return windows;
    }

    public bool IsWindowVisible(IntPtr window)
    {
        var client = FindWindowOnWorkspace(window);
        var hidden = !client?.hidden ?? true;
        //ShowError($"{IntPtrToHex(window)} {hidden}");
        return hidden;
    }

    public void SetTopmost(bool topmost)
    {
        
    }

    public bool IsWindowFullscreen(IntPtr window)
    {
        var client = FindWindowOnWorkspace(window);
        var fullscreen = client?.fullscreen > 0;
        //ShowError($"{IntPtrToHex(window)} {fullscreen}");
        return fullscreen;
    }

    public bool IsWindowMaximized(IntPtr window)
    {
        var client = FindWindowOnWorkspace(window);
        var maximixed = client?.fullscreen > 0;
        //ShowError($"{IntPtrToHex(window)} {maximixed}");
        return maximixed;
    }

    public Vector2Int GetWindowSize(IntPtr window)
    {
        var w = FindWindowOnWorkspace(window);
        if(w == null)
            w = _Window;
        var size = w.sizeVector;
        if(w == _Window && _NewWindowSize != null)
            size = _NewWindowSize.Value;
        // ShowError(size.ToString());
        return size;
    }

    public Vector2Int GetTotalDisplaySize()
    {
        
        var rects = GetAllMonitors();
        var minX = Math.Abs(rects.Min(a => a.Rect.x));
        var minY = Math.Abs(rects.Min(a => a.Rect.y));
        var maxX = rects.Max(a => a.Rect.x);
        var maxY = rects.Max(a => a.Rect.y);
        var rightMost = rects.OrderByDescending(a => a.Rect.x).FirstOrDefault().Rect;
        var bottomMost = rects.OrderByDescending(a => a.Rect.y).FirstOrDefault().Rect;

        var width = minX + maxX + rightMost.width;
        var height = minY + maxY + bottomMost.height;
        var display = new Vector2Int(width,height);
        // ShowError(display);
        return display;
    }

    public RectInt GetWindowRect(IntPtr window)
    {
        var w = FindWindowOnWorkspace(window);
        if(w == null)
        {
            ShowError($"{IntPtrToHex(window)}");
            throw new NullReferenceException(nameof(window));
        }
        var pos = w.atVector;
        var size = w.sizeVector;
        if(w == _Window)
        {
            if(_NewWindowPosition != null)
                pos = _NewWindowPosition.Value;
            if(_NewWindowSize != null)
                size = _NewWindowSize.Value;
        }
        var rect = new RectInt(pos,size);
        // ShowError($"{IntPtrToHex(window)} {rect} {w.initialClass}");
        return rect;
    }

    HyprlandClient FindWindowOnWorkspace(IntPtr window)
    {
        if(_Clients == null)
            UpdateWindows();
        if(window == _XUnityWindow)
            window = _Window.addressIntPtr;
        if(_Clients.ContainsKey(window))
        {
            var client = _Clients[window];
            if(client.workspace.id == _Window.workspace.id || client.pinned)
                return client;
        }
        return null;
    }

    List<KeyValuePair<IntPtr, HyprlandClient>> FindWindowOnWorkspace()
    {
         if(_Window == null)
            UpdateWindows();
        var windows = new List<KeyValuePair<IntPtr, HyprlandClient>>();
        foreach(var client in _Clients.ToList())
        {
            if(client.Value.workspace.id == _Window.workspace.id || client.Value.pinned)
                windows.Add(client);
        }
        return windows;
    }

    public List<IntPtr> GetClientStackingList()
    {
        var windowsOnWorkspace = FindWindowOnWorkspace();
        var stacking = windowsOnWorkspace.Where(a => !a.Value.floating).ToList();
        stacking.AddRange(windowsOnWorkspace.Where(a => a.Value.floating).OrderBy(a => a.Value.focusHistoryID));
        var stackingPointers = stacking.Select(a => a.Key).ToList();
        // ShowError(string.Join(",", stackingPointers));
        return stackingPointers;
    }

    public List<(IntPtr Id, RectInt Rect)> GetAllMonitors()
    {
        if(_Monitors == null)
            UpdateMonitors();
        var monitors = _Monitors.Select(a => (new IntPtr(a.id), new RectInt(a.position,a.size))).ToList();
        // ShowError(string.Join(";", monitors.Select(a => $"{a.Item1}:({a.Item2})")));
        return monitors;
    }

    public bool IsDesktop(IntPtr window)
    {
        return false;
    }

    public bool IsDock(IntPtr window) => false;

    public string GetClassName(IntPtr window)
    {
        var className = FindWindowOnWorkspace(window)?.initialClass ?? string.Empty;
        // ShowError($"{IntPtrToHex(window)} : {className}");
        return className;
    }

    public void Dispose()
    {
        _CancellationTokenSource.Cancel();
        _LoopTask?.Dispose();
    }

    IntPtr? _XUnityWindow;

    public void SetXUnityWindow(IntPtr unityWindow)
    {
        // ShowError(IntPtrToHex(unityWindow));
        _XUnityWindow = unityWindow;
    }
}