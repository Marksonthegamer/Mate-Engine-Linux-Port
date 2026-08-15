using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;
using UnityEngine;
using KWinUUID = System.String;

[DBusInterface("org.kde.kwin.Scripting")]
internal interface IScripting : IDBusObject
{
    Task<int> loadScriptAsync(string path, string name);
    Task unloadScriptAsync(string name);
}

[DBusInterface("org.kde.kwin.Script")]
internal interface IScriptInstance : IDBusObject
{
    Task runAsync();
}

[DBusInterface("org.kdotool.callback")]
public interface IKWinCallback : IDBusObject
{
    Task ResultAsync(string id, string message);
    Task ErrorAsync(string id, string message);
    Task FinishAsync(string id);
}

public class KWinCallbackReceiver : IKWinCallback
{
    public ObjectPath ObjectPath => "/";
    private readonly ConcurrentDictionary<string, TaskCompletionSource<List<string>>> _pendingTasks = new();
    private readonly ConcurrentDictionary<string, List<string>> _results = new();

    public void PrepareId(string id, TaskCompletionSource<List<string>> tcs)
    {
        _pendingTasks[id] = tcs;
        _results[id] = new List<string>();
    }

    public Task ResultAsync(string id, string message)
    {
        if (_results.TryGetValue(id, out var list))
            list.Add(message);
        return Task.CompletedTask;
    }

    public Task ErrorAsync(string id, string message)
    {
        if (_pendingTasks.TryRemove(id, out var tcs))
        {
            _results.TryRemove(id, out _);
            tcs.SetException(new Exception($"KWin Script Error: {message}"));
        }
        return Task.CompletedTask;
    }

    public Task FinishAsync(string id)
    {
        if (_pendingTasks.TryRemove(id, out var tcs))
        {
            if (_results.TryRemove(id, out var list))
                tcs.SetResult(list);
        }
        return Task.CompletedTask;
    }
}

public class KWinClient
{
    public IntPtr Hwnd;
    public KWinUUID Uuid;
    public int Pid;
    public RectInt Rect;
}

public class KWinManager : IDisposable, IWindowManagerImplementation
{
    private enum ScriptDeletionMode
    {
        DeleteOnFinishExecution,
        DeleteOnApplicationQuit
    }
    
    private Connection _connection;
    private ConnectionInfo _connectionInfo;
    private KWinCallbackReceiver _callbackHandler;
    private string _kdeVersion;
    private KWinUUID _unityUuid;
    private string _tempPath;
    private IScripting _scripting;
    private string _template;
    private bool _dbusReady;
    private bool _initialized;
    static CancellationTokenSource _CancellationTokenSource;
    Task _LoopTask;

    private readonly List<KWinClient> _cachedClients = new();
    private Dictionary<IntPtr, KWinUUID> _ptrToUuidMap = new();
    private Dictionary<KWinUUID, IntPtr> _uuidToPtrMap = new();
    private int _ptrCounter = 1;
    
    private Vector2Int _mousePos;

    public bool IsDragging { get; set; }
    
    private volatile bool _isProcessing;
    
    public KWinManager(Connection connection, ConnectionInfo connectionInfo) 
    {
        _CancellationTokenSource = new CancellationTokenSource();
        _kdeVersion = Environment.GetEnvironmentVariable("KDE_SESSION_VERSION") ?? "6";
        _tempPath = Application.temporaryCachePath;
        _LoopTask = Task.Run(() => Update(_CancellationTokenSource.Token), _CancellationTokenSource.Token);
        _connection = connection;
        _connectionInfo = connectionInfo;
    }
    
    public async Task SetupDBus()
    {
        if (_initialized) return;
        _template = $@"
            function send(msg) {{
                callDBus('{_connectionInfo.LocalName}', '/', 'org.kdotool.callback', 'Result', 'placeholder', msg);
            }}
            function err(msg) {{
                callDBus('{_connectionInfo.LocalName}', '/', 'org.kdotool.callback', 'Error', 'placeholder', msg);
            }}
            function done() {{
                callDBus('{_connectionInfo.LocalName}', '/', 'org.kdotool.callback', 'Finish', 'placeholder');
            }}";

        _callbackHandler = new KWinCallbackReceiver();
        await _connection.RegisterObjectAsync(_callbackHandler);
        
        await _connection.RegisterServiceAsync("org.kdotool.callback");
        
        _scripting = _connection.CreateProxy<IScripting>("org.kde.KWin", "/Scripting");

        _dbusReady = true;
    }

    private async Task Update(CancellationToken cancellationToken)
    {
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_dbusReady)
                {
                    await UpdateWindows();
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
                throw;
            }

            if (!_initialized)
            {
                _unityUuid = GetSelfWindowUuid();
                if (_unityUuid != string.Empty)
                    _initialized = true;
            }
            
            await Task.Delay(16, cancellationToken);
        }
    }

    private async Task UpdateWindows()
    {
        var cachedClients = new List<KWinClient>();
        var activeUuids = new HashSet<KWinUUID>();
        var resultLines = await GetAllInOne();
        foreach (var line in resultLines)
        {
            if (line.StartsWith("Mouse:"))
            {
                var mouse = line.Split(":")[1];
                int.TryParse(mouse.Split(",")[0], out var x);
                int.TryParse(mouse.Split(",")[1], out var y);
                _mousePos = new Vector2Int(x, y);
            }
            else
            {
                var prop = line.Split(":");
                var uuid = prop[0];
                int.TryParse(prop[1], out var pid);
                activeUuids.Add(uuid);
                var parts = prop[2].Split(',');
                cachedClients.Add(new KWinClient
                {
                    Hwnd = GetPtrFromUuid(uuid), Pid = pid, Uuid = uuid,
                    Rect = new RectInt(Mathf.RoundToInt(float.Parse(parts[0])), Mathf.RoundToInt(float.Parse(parts[1])), int.Parse(parts[2]), int.Parse(parts[3]))
                }); // You may wonder why I use float instead of int here - that's because KWin 6 sometimes return float coordinates!
            }
        }
        var deadUuids = _uuidToPtrMap.Keys.Where(u => !activeUuids.Contains(u)).ToList();
        foreach (var uuid in deadUuids)
        {
            var ptr = _uuidToPtrMap[uuid];
            _uuidToPtrMap.Remove(uuid);
            _ptrToUuidMap.Remove(ptr);
        }
        
        _cachedClients.Clear();
        _cachedClients.AddRange(cachedClients);
    }

    private IntPtr GetPtrFromUuid(string uuid)
    {
        if (string.IsNullOrEmpty(uuid)) return IntPtr.Zero;
    
        if (_uuidToPtrMap.TryGetValue(uuid, out var existingPtr))
            return existingPtr;
    
        var newPtr = new IntPtr(_ptrCounter++);
        _ptrToUuidMap[newPtr] = uuid;
        _uuidToPtrMap[uuid] = newPtr;
        return newPtr;
    }

    private KWinUUID GetUuidFromPtr(IntPtr ptr)
    {
        return _ptrToUuidMap.TryGetValue(ptr, out string uuid) ? uuid : _unityUuid;
    }
    
    public void SetWindowPosition(Vector2Int position)
    {
        Task.Run(() => MoveWindow(new Vector2Int(position.x, position.y))).GetAwaiter().GetResult();
    }

    public void SetWindowSize(Vector2Int size)
    {
        string scriptName = "KWin_ResizeWin.js";
        string jsScript = _template.Replace("placeholder", "KWin_ResizeWin") + $@"
            for (let w of workspace.{(_kdeVersion.StartsWith("5") ? "clientList" : "windowList")}()) {{
                if (w.internalId.toString() == ""{_unityUuid}"") {{
                    w.frameGeometry = {{ x: w.frameGeometry.x, y: w.frameGeometry.y, width: {size.x}, height: {size.y} }};
                    break;
                }}
            }}
            done();";
        
        File.WriteAllText(Path.Combine(_tempPath, scriptName), jsScript);
        Task.Run(() => ExecuteKWinScript(scriptName, ScriptDeletionMode.DeleteOnFinishExecution).GetAwaiter().GetResult());
    }

    public Vector2Int GetWindowPosition()
    {
        RectInt rect = Task.Run(() => GetWindowGeometry(_unityUuid)).GetAwaiter().GetResult();
        return new Vector2Int(rect.x, rect.y);
    }

    public int GetWindowPid(IntPtr window)
    {
        foreach (var client in _cachedClients.Where(client => client.Hwnd == window))
        {
            return client.Pid;
        }
        return -1;
    }

    public Vector2Int GetMousePosition()
    {
        return _mousePos;
    }

    public List<IntPtr> FindWindowsByPid(int targetPid)
    {
        return _cachedClients.Where(client => client.Pid == targetPid).Select(client => client.Hwnd).ToList();
    }

    public List<IntPtr> GetAllWindows()
    {
        return _cachedClients.Select(client => client.Hwnd).ToList();
    }

    public Vector2Int GetWindowSize(IntPtr window)
    {
        RectInt rect = Task.Run(() => GetWindowGeometry(GetUuidFromPtr(window))).GetAwaiter().GetResult();
        return new Vector2Int(rect.width, rect.height);
    }

    public bool GetWindowRect(IntPtr window, out RectInt rectInt)
    {
        rectInt = Task.Run(() => GetWindowGeometry(GetUuidFromPtr(window))).GetAwaiter().GetResult();
        return rectInt != RectInt.zero;
    }

    public Vector2Int GetTotalDisplaySize()
    {
        string scriptName = "KWin_GetWorkspaceSize.js";
        string jsScript = _template.Replace("placeholder", "KWin_GetWorkspaceSize") + $@"
             send(workspace.workspaceSize.width + ',' + workspace.workspaceSize.height);
             done();";
        
        File.WriteAllText(Path.Combine(_tempPath, scriptName), jsScript);
    
        var result = Task.Run(() => ExecuteKWinScript(scriptName, ScriptDeletionMode.DeleteOnFinishExecution)).GetAwaiter().GetResult()[0].Split(',');
        return new Vector2Int(int.Parse(result[0]), int.Parse(result[1]));
    }
    
    public bool IsWindowVisible(IntPtr window) => true; 
    public bool IsWindowFullscreen(IntPtr window) => false;
    public bool IsWindowMaximized(IntPtr window) => false;
    public List<IntPtr> GetClientStackingList() => GetAllWindows();
    public List<(IntPtr Id, RectInt Rect)> GetAllMonitors() => new();
    public bool IsDesktop(IntPtr window) => false;
    public bool IsDock(IntPtr window) => false;
    public string GetClassName(IntPtr window) => "KWinWindow";
    public void SetTopmost(bool topmost) { }
    public void HideFromTaskbar(bool reallyHide) { }
    public void SetWindowBorderless(bool value) { }
    public void SetWindowType(WindowType type) { }
    public void SetXUnityWindow(IntPtr unityWindow) { }
    public void SetSnapedWindow(IntPtr window) { }

    private KWinUUID GetSelfWindowUuid()
    {
        if (_cachedClients.Count == 0)
            return string.Empty;
        var self = _cachedClients.Find(client => client.Pid == Process.GetCurrentProcess().Id);
        return self?.Uuid ?? string.Empty;
    }

    private async Task<List<string>> GetAllInOne()
    {
        if (!_dbusReady) return new List<string>();
        string scriptName = "KWin_GetAll.js";
        string jsScript = _template.Replace("placeholder", "KWin_GetAll") + $@"
            for (let win of workspace.{(_kdeVersion.StartsWith("5") ? "clientList" : "windowList")}()) {{
                send(win.internalId.toString() + ':' + win.pid.toString() + ':' + win.frameGeometry.x + ',' + win.frameGeometry.y + ',' + win.frameGeometry.width + ',' + win.frameGeometry.height);
            }}
            send('Mouse:' + workspace.cursorPos.x + ',' + workspace.cursorPos.y);
            done();";
        
        if (!File.Exists(Path.Combine(_tempPath, scriptName))) await File.WriteAllTextAsync(Path.Combine(_tempPath, scriptName), jsScript);
        return await ExecuteKWinScript(scriptName, ScriptDeletionMode.DeleteOnApplicationQuit);
    }

    private RectInt GetWindowGeometry(string uuid)
    {
        if (!_initialized || string.IsNullOrEmpty(uuid)) return RectInt.zero;
    
        var client = _cachedClients.Find(c => c.Uuid == uuid);
        if (client != null)
        {
            return client.Rect;
        }
    
        return RectInt.zero;
    }

    private async Task MoveWindow(Vector2Int pos)
    {
        if (!_initialized | _isProcessing) return;
        _isProcessing = true;
        string scriptName = $"KWin_MoveWin.js"; 
        string jsScript = _template.Replace("placeholder", "KWin_MoveWin") + $@"
            for (let w of workspace.{(_kdeVersion.StartsWith("5") ? "clientList" : "windowList")}()) {{
                if (w.internalId.toString() == ""{_unityUuid}"") {{
                    w.frameGeometry = {{ x: {pos.x}, y: {pos.y}, width: w.frameGeometry.width, height: w.frameGeometry.height }};
                    break;
                }}
            }}
            done();";
        
        await File.WriteAllTextAsync(Path.Combine(_tempPath, scriptName), jsScript);
        await ExecuteKWinScript(scriptName, ScriptDeletionMode.DeleteOnFinishExecution);
    }
    
    private async Task<List<string>> ExecuteKWinScript(string scriptFileName, ScriptDeletionMode deletionMode)
    {
        if (!_dbusReady) return new List<string>();
        
        var tcs = new TaskCompletionSource<List<string>>();
        _callbackHandler.PrepareId(Path.GetFileNameWithoutExtension(scriptFileName), tcs);
        
        string scriptPath = Path.Combine(_tempPath, scriptFileName);
        
        int scriptId = await _scripting.loadScriptAsync(scriptPath, Path.GetFileNameWithoutExtension(scriptFileName));

        if (scriptId == -1)
        {
            throw new Exception($"Script {scriptFileName} failed to load.");
        }
        
        var instance = _kdeVersion.StartsWith("5")
            ? _connection.CreateProxy<IScriptInstance>("org.kde.KWin", $"/{scriptId}") 
            : _connection.CreateProxy<IScriptInstance>("org.kde.KWin", $"/Scripting/Script{scriptId}");

        try
        {
            await instance.runAsync();
            var timeout = Task.Delay(2000);
            var completedTask = await Task.WhenAny(tcs.Task, timeout);
            
            if (completedTask == timeout) throw new TimeoutException($"Script {scriptFileName} timed out.");

            _isProcessing = false;
            return await tcs.Task;
        }
        finally
        {
            await _scripting.unloadScriptAsync(Path.GetFileNameWithoutExtension(scriptFileName));
            if (deletionMode == ScriptDeletionMode.DeleteOnFinishExecution && File.Exists(scriptPath)) File.Delete(scriptPath);
            if (deletionMode == ScriptDeletionMode.DeleteOnApplicationQuit && !_pendingDeletions.Contains(scriptPath)) _pendingDeletions.Add(scriptPath);
        }
    }

    private readonly List<string> _pendingDeletions = new();

    public void Dispose()
    {
        foreach (var script in _pendingDeletions.Where(File.Exists))
        {
            File.Delete(script);
        }

        _CancellationTokenSource.Cancel();
        _connection?.UnregisterObject(_callbackHandler);
    }
}