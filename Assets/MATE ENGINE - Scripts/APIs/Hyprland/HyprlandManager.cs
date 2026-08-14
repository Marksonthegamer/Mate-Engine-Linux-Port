using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using Random = System.Random;
using static APIs.Hyprland.HyprlandUtils;

namespace APIs.Hyprland
{
    public class HyprlandManager : Singleton<HyprlandManager>, IWindowManagerImplementation
    {
        const int _LayerBackground = 0;
        const int _LayerBottom = 1;
        const int _LayerTop = 2;
        const int _LayerOverlay = 3;

        Vector2Int _LastCursorPosition = Vector2Int.zero;
        bool _CursorOver = false;

        Stopwatch _ImidiateStopWatch;
        TimeSpan _ImidiateStopWatchRuntime = TimeSpan.FromSeconds(1);
        CancellationTokenSource _CancellationTokenSource;

        ConcurrentDictionary<IntPtr, HyprlandClient> _Clients;

        bool _MouseOverWindow;

        int _CurrentWorkspace = 0;

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

        HyprlandEventReader _HyprlandEventReader;
        HyprlandDispatcher _HyprlandDispatcher;

        public HyprlandManager()
        {
            _ImidiateStopWatch = new Stopwatch();
            var random = new Random();
            _Clients = new ConcurrentDictionary<IntPtr, HyprlandClient>();
            _Layers = new ConcurrentDictionary<IntPtr, HyprlandClient>();
            _CancellationTokenSource = new CancellationTokenSource();
            _LoopTask = Task.Run(async () => await UpdateAsync(_CancellationTokenSource.Token), _CancellationTokenSource.Token);

            _HyprlandDispatcher = new HyprlandDispatcher();
            _HyprlandDispatcher.Initialize();

            ShowError($"LUA Config: {_HyprlandDispatcher.LUAConfigMode}");

            _HyprlandEventReader = new HyprlandEventReader();
            _HyprlandEventReader.HyprlandEvent += HyprlandEvent;
            _HyprlandEventReader.Start(new List<string>
            {
                HyprlandEventNames.ActiveWindow,
                HyprlandEventNames.ActiveWindowV2,
                HyprlandEventNames.WindowTitle,
                HyprlandEventNames.WindowTitleV2
            });
        }

        // deconstructor
        ~HyprlandManager()
        {
            _CancellationTokenSource.Cancel();
            _LoopTask?.Dispose();
            _HyprlandDispatcher?.Dispose();
            _HyprlandEventReader?.Dispose();
        }

        async void HyprlandEvent(object sender, HyprlandEventArgs hyprlandEventArgs)
        {
            try
            {
                switch (hyprlandEventArgs.EventName)
                {
                    case HyprlandEventNames.MoveToWorkspace:
                        {
                            // update current workspace
                            _CurrentWorkspace = int.Parse(hyprlandEventArgs.Parameters[0]);
                            break;
                        }
                    case HyprlandEventNames.FocusMonitor:
                        {
                            // switching monitor also switches the workspace, but there wont be an workspace event
                            var ws = await _HyprlandDispatcher.GetActiveWorkspace();
                            _CurrentWorkspace = ws.id;
                            break;
                        }
                    case HyprlandEventNames.MoveWindowToWorkspace:
                        {
                            var movedWindow =  AddressToIntPtr(hyprlandEventArgs.Parameters[0]);
                            var targetWorkspace = int.Parse(hyprlandEventArgs.Parameters[1]);

                            // check if the snapped window move to a different window and move the avatar window to the same workspace
                            if(movedWindow == _SnappedWindow && _Window.workspace.id != targetWorkspace)
                                await _HyprlandDispatcher.MoveWindowToWorkspace(_Window.address, targetWorkspace, true);
                            break;
                        }
                    case HyprlandEventNames.CreateWorkspace:
                    case HyprlandEventNames.DestroyWorkspace:
                    case HyprlandEventNames.MoveToWorkspaceToMonitor:
                    case HyprlandEventNames.MoveWindowIntoGroup:
                    case HyprlandEventNames.MoveWindowOutOfGroup:
                    case HyprlandEventNames.CloseWindow:
                    case HyprlandEventNames.OpenWindow:
                        {
                            // layout likely changed
                            TriggerImidiateUpdates();
                            break;
                        }
                    case HyprlandEventNames.PinState:
                        {
                            var ptr = AddressToIntPtr(hyprlandEventArgs.Parameters[0]);
                            if (ptr == _SnappedWindow)
                                await SetPinStateAsync();
                            // ShowError(hyprlandEventArgs.EventName);
                            break;
                        }
                    case HyprlandEventNames.MonitorAdded:
                    case HyprlandEventNames.MonitorRemoved:
                        {
                            _Monitors = await _HyprlandDispatcher.GetMonitorsAsync();
                            // ShowError(hyprlandEventArgs.EventName);
                            break;
                        }
                    case HyprlandEventNames.LayerAdded:
                    case HyprlandEventNames.LayerRemoved:
                        {
                            await UpdateLayersAsync();
                            // ShowError(hyprlandEventArgs.EventName);
                            break;
                        }
                }
            }
            catch(Exception ex)
            {
                ShowError(ex.ToString());
            }
        }

        public void HideFromTaskbar(bool reallyHide)
        {
            // not supported
            // has to be configured in the bar/widget application
        }

        public void SetWindowBorderless()
        {
            // not necessary
        }

        public void SetWindowType(WindowType type)
        {
            // not supported
        }

        Task _LoopTask;

        bool? _DefaultPinState;

        async Task SetInitialWindowPropsAsync()
        {
            // turn the window into a floating window
            if(!_Window.floating)
                await _HyprlandDispatcher.SetFloatingAsync(_Window.address);
            // disable the focus by default
            await _HyprlandDispatcher.SetPropAsync(_Window.address, "no_focus", true);
            // remove all window decorations
            await _HyprlandDispatcher.SetPropAsync(_Window.address, "decorate", false);
            // disable the window background blur
            await _HyprlandDispatcher.SetPropAsync(_Window.address, "no_blur", true);

            // set the initial window size
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
            if (size != Vector2Int.zero)
                SetWindowSize(size);
        }

        void TriggerImidiateUpdates()
        {
            // inits high poling rates
            _ImidiateStopWatch.Reset();
            _ImidiateStopWatch.Start();
        }

        async Task UpdateAsync(CancellationToken cancellationToken)
        {
            var c = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // check if the avatar window has been identified
                    if (_Window == null)
                    {
                        // initial setup
                        var workspace = await _HyprlandDispatcher.GetActiveWorkspace();
                        _CurrentWorkspace = workspace.id;
                        _Monitors = await _HyprlandDispatcher.GetMonitorsAsync();
                        await UpdateLayersAsync();
                        await UpdateWindowsAsync();
                        if (_Window != null)
                            await SetInitialWindowPropsAsync();
                    }
                    else
                    {
                        // update tracking variables
                        if (c % 50 == 0 || _ImidiateStopWatch.IsRunning || _MouseOverWindow)
                            _LastCursorPosition = await _HyprlandDispatcher.GetCursorPositionAsync();
                        if (c == 250 || _ImidiateStopWatch.IsRunning || _MouseOverWindow)
                        {
                            await UpdateWindowsAsync();
                            await UpdateFocusableAsync();
                            c = 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowError(ex.ToString());
                }
                await Task.Delay(1);
                // stop high polling rates after _ImidiateStopWatchRuntime elapsed
                if (_ImidiateStopWatch.IsRunning && _ImidiateStopWatch.Elapsed >= _ImidiateStopWatchRuntime)
                    _ImidiateStopWatch.Stop();
                //ShowError($"LoopDelay: {delay}");
                c++;
            }
        }

        async Task UpdateLayersAsync()
        {
            // sync layers 
            var layersPerMonitor = await _HyprlandDispatcher.GetLayersAsync();
            foreach (var monitorName in layersPerMonitor.Keys)
            {
                var levels = layersPerMonitor[monitorName];
                foreach (var levelKey in levels.levels.Keys)
                {
                    var level = int.Parse(levelKey);
                    if (level == _LayerTop || level == _LayerBottom)
                    {
                        var layers = levels.levels[levelKey];
                        foreach (var layer in layers)
                        {
                            if(layer.posVector.y == 0)
                                continue;
                            _Layers[layer.addressIntPtr] = new HyprlandClient
                            {
                                address = layer.address,
                                at = new int[] { layer.posVector.x, layer.posVector.y },
                                floating = false,
                                size = new int[] { layer.sizeVector.x, layer.sizeVector.y },
                                pinned = true,
                                initialClass = layer.name,
                                pid = layer.pid,
                                monitor = _Monitors.FirstOrDefault(a => a.name == monitorName)?.id ?? 0
                            };
                        }
                    }
                }
            }

            foreach (var address in _Layers.Keys)
            {
                if(_Layers[address].initialClass == "hyprland.imaginary")
                    continue;
                if (!layersPerMonitor.Any(a => a.Value.levels.Any(b => b.Value.Any(c => c.addressIntPtr == address))))
                {
                    // ShowError($"Remove Layer: {_Layers[address].initialClass} {address}");
                    _Layers.TryRemove(address, out _);
                }
            }

            // create imaginary layers on each monitor when allowHyprlandMonitorSitting is enabled
            if (_Monitors != null && _Monitors.Length > 0)
            {
                foreach (var monitor in _Monitors)
                {
                    var pointer = new IntPtr(monitor.name.GetHashCode());
                    if(!SaveLoadHandler.Instance.data.allowHyprlandMonitorSitting)
                    {
                        if(_Layers.ContainsKey(pointer))
                        {
                            // ShowError($"Remove imaginary layer: {monitor.name}");
                            _Layers.TryRemove(pointer, out _);
                        }
                        continue;
                    }
                    
                    if (!_Layers.ContainsKey(pointer))
                    {
                        _Layers[pointer] = new HyprlandClient
                        {
                            address = IntPtrToHex(pointer),
                            at = new int[] { monitor.position.x, monitor.position.y + monitor.size.y - 2 },
                            floating = false,
                            size = new int[] { monitor.size.x, 100 },
                            pinned = true,
                            initialClass = "hyprland.imaginary",
                            title = monitor.name,
                            pid = 0,
                            monitor = monitor.id
                        };
                        //ShowError($"Create imaginary layer: {monitor.name}, pos:{_Layers[pointer].atVector}, size:{_Layers[pointer].sizeVector}");
                    }
                }
            }
            // ShowError("------");
            // foreach(var layers in _Layers.Values)
            //     ShowError($"{layers.address}: {layers.monitor} {layers.title} {layers.atVector} {layers.sizeVector}");
            // ShowError("------");
        }

        ConcurrentDictionary<IntPtr, HyprlandClient> _Layers;

        int _LastHashCode = 0;

        async Task UpdateWindowsAsync()
        {
            var clients = await _HyprlandDispatcher.GetClientsAsync();
            // layout has not changed => skip
            if (clients.hashCode == _LastHashCode)
                return;
            _LastHashCode = clients.hashCode;

            var lastWindowSize = _Window?.sizeVector;
            var lastWindowPos = _Window?.atVector;
            var lastMonitor = _Window?.monitor;
            var lastWorkSpace = _Window?.workspace;
            // detect avatar window
            if (_Window == null)
                _Window = clients.clients.FirstOrDefault(a => a.pid == Process.GetCurrentProcess().Id);
            else
                _Window = clients.clients.FirstOrDefault(a => a.address == _Window.address);
            if (_Window == null)
                return;
            
            // hyprland does not immidiatly update the monitor and workspace of the avatar window
            if(_Monitors?.Any() == true)
            {
                var centerX = _Window.atVector.x + (_Window.sizeVector.x / 2);
                var centerY = _Window.atVector.y + (_Window.sizeVector.y / 2);
                var center = new Vector2Int(centerX,centerY);
                var monitor = _Monitors.FirstOrDefault(a => new RectInt(a.position,a.size).Contains(center));
                if(monitor != null)
                {
                    
                    if(monitor.id != _Window.monitor)
                    {
                        // ShowError($"Monitor does not match, remapped: {_Window.monitor} > {monitor.id}");
                        _Window.monitor = monitor.id;
                        _Window.workspace = monitor.activeWorkspace;
                    }
                }
                else if(lastMonitor != null)
                {
                    if(lastMonitor.Value != _Window.monitor)
                    {
                        // ShowError($"Last Monitor does not match, remapped: {_Window.monitor} > {lastMonitor.Value}");
                        _Window.monitor = lastMonitor.Value;
                        _Window.workspace = lastWorkSpace;
                    }
                }
            }

            // window change outside, fix it
            if(lastWindowSize != null && _Window.sizeVector != lastWindowSize)
                SetWindowSize(lastWindowSize.Value);

            if(lastWindowPos != null && _Window.atVector != lastWindowPos)
                SetWindowPosition(lastWindowPos.Value);

            //ShowError($"Size: {_Window.sizeVector}, Postion: {_Window.atVector}");
            // sync other windows
            var closedWindows = _Clients.Where(existing => !clients.clients.Any(found => found.address == existing.Value.address)).Select(a => a.Key).ToList();
            foreach (var closedWindow in closedWindows)
                _Clients.TryRemove(closedWindow, out _);

            foreach (var client in clients.clients)
            {
                // workspace changed of the snapped window before the event caught it
                if(client.addressIntPtr == _SnappedWindow && client.workspace.id != _Window.workspace.id)
                {
                    await _HyprlandDispatcher.MoveWindowToWorkspace(_Window.address, client.workspace.id, true);
                    _Window.workspace = client.workspace;
                }
                _Clients[client.addressIntPtr] = client;
            }

            // layout changed so trigger high polling rates
            TriggerImidiateUpdates();
            // foreach(var client in _Clients)
            //     ShowError($"{client.Key}: {client.Value.address}, {client.Value.title}, {client.Value.atVector}, {client.Value.sizeVector}");
        }

        async Task UpdateFocusableAsync()
        {   
            // check if user is on an inactive workspace
            if(_Window.workspace.id != _CurrentWorkspace)
            {
                _MouseOverWindow = false;
                _CursorOver = false;
                return;
            }
            var isBigScreen = AvatarBigScreenHandler.ActiveHandlers.FirstOrDefault()?.IsBigScreenActive ?? false;
            // if the window is being dragged, in bigscreen or a menu is open the window is always focusable
            var forceFocus = MenuActions.IsAnyMenuOpen() || IsDragging || isBigScreen;
            if (forceFocus)
            {
                await SetFocusableAsync(true);
                _MouseOverWindow = true;
            }
            else
            {
                // check if the mouse is over the window before making more complicated checks
                var windowRect = new Rect(_Window.atVector.x, _Window.atVector.y, _Window.sizeVector.x, _Window.sizeVector.y);
                //ShowError($"windowRect: {windowRect}");
                //ShowError($"cursorPos: {_LastCursorPosition}");
                _MouseOverWindow = windowRect.Contains(_LastCursorPosition);
                //ShowError($"MoveOverWindow: {_MouseOverWindow}");
                if (_MouseOverWindow)
                {
                    // check if the mouse is roughly over the avatar
                    var correction = SaveLoadHandler.Instance.data.avatarSize - 0.10F;
                    var avatarScale = SaveLoadHandler.Instance.data.avatarSize - (0.28F * correction);
                    var avatarHeight = _Window.sizeVector.y * avatarScale;
                    var avatarWidth = avatarHeight / 4.5625F;
                    var verticalOffset = _Window.sizeVector.y - avatarHeight;
                    var horizontalOffset = (_Window.sizeVector.x - avatarWidth) / 2;

                    var avatarRectX = _Window.atVector.x + horizontalOffset;
                    var avatarRectY = _Window.atVector.y + verticalOffset;
                    var avatarRectWidth =  _Window.sizeVector.x - horizontalOffset * 2;
                    var avatarRectHeight = _Window.sizeVector.y - verticalOffset;

                    var monitor = _Monitors[_Window.monitor];
                    if(avatarRectX < monitor.position.x)
                        avatarRectX = monitor.position.x;
                    if(avatarRectX + avatarWidth > monitor.position.x + monitor.width)
                        avatarRectX = (monitor.width + monitor.position.x) - avatarWidth;

                    var avatarRect = new Rect(avatarRectX, avatarRectY,avatarRectWidth, avatarRectHeight);
                    //ShowError($"avatarRect: {avatarRect}");
                    var cursorOver = avatarRect.Contains(_LastCursorPosition);
                    if (_CursorOver != cursorOver)
                    {
                        await SetFocusableAsync(cursorOver);
                        _CursorOver = cursorOver;
                    }
                }
                //ShowError($"CursorOver: {_CursorOver}");
            }
        }

        HyprlandMonitor[] _Monitors;

        public Vector2Int GetMousePosition()
        {
            //ShowError(_LastCursorPosition);
            return _LastCursorPosition;
        }

        private bool _CurrentFocusState;

        async Task SetFocusableAsync(bool focusable)
        {
            if(focusable != _CurrentFocusState)
            {
                _CurrentFocusState = focusable;
                // ShowError(focusable);
                await _HyprlandDispatcher.SetPropAsync(_Window.address, "no_focus", !focusable);
            }
        }

        public async void SetWindowPosition(Vector2Int position)
        {
            //ShowError(position);
            await _HyprlandDispatcher.MoveWindowPixelExactAsync(_Window.address, position);
            _Window.at = new int[] { position.x, position.y };
            TriggerImidiateUpdates();
        }

        public async void SetWindowSize(Vector2Int size)
        {
            //ShowError(size);
            await _HyprlandDispatcher.ResizewindowpixelExactWindowSizeAsync(_Window.address, size);
            _Window.size = new int[] { size.x, size.y };
            TriggerImidiateUpdates();
        }

        public Vector2Int GetWindowPosition()
        {
            return _Window.atVector;
        }

        private static void ShowError(object messageObject, [CallerMemberName] string callsource = "")
        {
            Console.WriteLine($"\u001b[31m{nameof(HyprlandManager)}.{callsource}: {messageObject}\u001b[0m");
        }

        public int GetWindowPid(IntPtr window)
        {
            var pid = -1;
            if (window == _XUnityWindow)
                window = _Window.addressIntPtr;
            if (_Clients.ContainsKey(window))
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
            var client = FindClientByWindowId(window);
            var hidden = !client?.hidden ?? true;
            //ShowError($"{IntPtrToHex(window)} {hidden}");
            return hidden;
        }

        public void SetTopmost(bool topmost)
        {
            // hyprland does not support this
            // this can either be done with full native wayland support window (by creating it as a layer) 
            // or with a hyprland plugin that wraps the window (similar to hyprwinwrap)
        }

        public bool IsWindowFullscreen(IntPtr window)
        {
            var client = FindClientByWindowId(window);
            var fullscreen = client?.fullscreen == 2;
            //ShowError($"{IntPtrToHex(window)} {fullscreen}");
            return fullscreen;
        }

        public bool IsWindowMaximized(IntPtr window)
        {
            var client = FindClientByWindowId(window);
            var maximixed = client?.fullscreen == 1;
            //ShowError($"{IntPtrToHex(window)} {maximixed}");
            return maximixed;
        }

        public Vector2Int GetWindowSize(IntPtr window)
        {
            var w = FindClientByWindowId(window);
            if (w == null)
                w = _Window;
            return w.sizeVector;
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
            var display = new Vector2Int(width, height);
            // ShowError(display);
            return display;
        }

        public bool GetWindowRect(IntPtr window, out RectInt rect)
        {
            var w = FindClientByWindowId(window);
            if (w == null)
            {
                ShowError($"{IntPtrToHex(window)}");
                rect = RectInt.zero;
                return false;
            }
            var pos = w.atVector;
            var size = w.sizeVector;
            // tiled windows returned  with reduced height to enable snapping
            if (w != _Window && !w.floating)
                size = new Vector2Int(w.sizeVector.x, 100); 
            rect = new RectInt(pos, size);
            // ShowError($"{IntPtrToHex(window)} {rect} {w.initialClass}");
            return true;
        }

        HyprlandClient FindClientByWindowId(IntPtr window)
        {
            if (_Window == null)
                return null;
            if (window == _XUnityWindow)
                window = _Window.addressIntPtr;
            if (_Clients.ContainsKey(window))
                return _Clients[window];
            if (_Layers.ContainsKey(window))
                return _Layers[window];
            return null;
        }

        Dictionary<IntPtr, HyprlandClient> GetAllClientsOnWorkspace()
        {
            if (_Window == null)
                return null;
            var windows = new Dictionary<IntPtr, HyprlandClient>();
            foreach (var client in _Clients.ToList())
            {
                if (client.Value.workspace.id == _Window.workspace.id || client.Value.pinned)
                    windows.Add(client.Key, client.Value);
            }
            return windows;
        }

        public List<IntPtr> GetClientStackingList()
        {
            var windowsOnWorkspace = GetAllClientsOnWorkspace();
            var stacking = new List<IntPtr>();

            if (_Window != null)
            {
                // remove the avatar window
                windowsOnWorkspace.Remove(_Window.addressIntPtr);
                // add surfaces in selected layers
                stacking.AddRange(_Layers.Where(a => a.Value.monitor == _Window.monitor).Select(a => a.Key));
            }
            // add the tiled windows
            stacking.AddRange(windowsOnWorkspace.Where(a => !a.Value.floating).Select(a => a.Key));
            // add floating windows
            stacking.AddRange(windowsOnWorkspace.Where(a => a.Value.floating).OrderBy(a => a.Value.focusHistoryID).Select(a => a.Key));
            // ShowError(string.Join(",", stackingPointers));
            return stacking;
        }

        public List<(IntPtr Id, RectInt Rect)> GetAllMonitors()
        {
            var monitors = _Monitors.Select(a => (new IntPtr(a.id), new RectInt(a.position, a.size))).ToList();
            // ShowError(string.Join(";", monitors.Select(a => $"{a.Item1}:({a.Item2})")));
            return monitors;
        }

        public bool IsDesktop(IntPtr window)
        {
            // wayland handles such things via surfaces in the background layers,
            // surfaces in the background layer are already filtered out
            return false;
        }

        public bool IsDock(IntPtr window)
        {
            // if the window is on a layer its a surface
            return _Layers.ContainsKey(window);
        }

        public string GetClassName(IntPtr window)
        {
            var className = FindClientByWindowId(window)?.initialClass ?? string.Empty;
            // ShowError($"{IntPtrToHex(window)} : {className}");
            return className;
        }

        IntPtr? _XUnityWindow;

        public void SetXUnityWindow(IntPtr unityWindow)
        {
            // store the XWindow so that the hyprland manager recognizes it as the avatarwindow
            // ShowError(IntPtrToHex(unityWindow));
            _XUnityWindow = unityWindow;
        }

        IntPtr? _SnappedWindow;

        public async void SetSnapedWindow(IntPtr window)
        {
            // set the snapped window and sync the pin state
            if (window != IntPtr.Zero)
            {
                _SnappedWindow = window;
                await SetPinStateAsync();
            }
            else
            {
                _SnappedWindow = null;
                await RestorePinStateAsync();
            }
            //ShowError($"SnappedWindow: {IntPtrToHex(window)}");
        }

        async Task RestorePinStateAsync()
        {
            // restore the pin state after unsnapping the window
            if (_DefaultPinState.HasValue && _Window.pinned != _DefaultPinState.Value)
            {
                await _HyprlandDispatcher.TogglePinAsync(_Window.address);
                _DefaultPinState = null;
            }
        }

        async Task SetPinStateAsync()
        {
            // get the default pin state
            if (_DefaultPinState == null)
                _DefaultPinState = _Window.pinned;
            // set the pin state to that of the window/surface
            if ((_Clients.ContainsKey(_SnappedWindow.Value) && _Window.pinned != _Clients[_SnappedWindow.Value].pinned) ||
               (_Layers.ContainsKey(_SnappedWindow.Value) && _Window.pinned != true))
                await _HyprlandDispatcher.TogglePinAsync(_Window.address);
        }
    }
}