using System;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class HyprlandClients
{
    public HyprlandClient[] clients;
}

[Serializable]
public class HyprlandMonitors
{
    public HyprlandMonitor[] monitors;
}

[Serializable]
public class HyprlandMonitor
{
    public int id;
    public string name;
    public string description;
    public string make;
    public int width;
    public int height;
    public Vector2Int size => new Vector2Int(width, height);
    public int x;
    public int y;
    public Vector2Int position => new Vector2Int(x, y);
    public HyprlandWorkspace activeWorkspace;
    public bool disabled;
}

[Serializable]
public class HyprlandWorkspace
{
    public int id;
    public string name;
}

[Serializable]
public class HyprlandClient
{
    public int[] at;
    public Vector2Int atVector => new Vector2Int(at[0], at[1]);
    public int[] size;
    public Vector2Int sizeVector => new Vector2Int(size[0], size[1]);
    public string address;
    public IntPtr addressIntPtr => new IntPtr(Convert.ToInt64(address , 16));
    public string initialClass;
    public int pid;
    public bool floating;
    public int monitor;
    public int fullscreen;
    public string title;
    public bool hidden;
    public bool pinned;
    public bool xwayland;
    public HyprlandWorkspace workspace;
    public int focusHistoryID;
}