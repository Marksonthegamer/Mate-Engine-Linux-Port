using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using static APIs.Hyprland.HyprlandUtils;

namespace APIs.Hyprland
{
    [Serializable]
    public class HyprlandClients
    {
        public HyprlandClient[] clients;
        public int hashCode;
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
        public IntPtr addressIntPtr => AddressToIntPtr(address);
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

    [JsonObject]
    public class HyprlandLayerLevels
    {
        [JsonProperty("levels")]
        public Dictionary<string, List<HyprlandLayer>> levels { get; set; }
    }

    [JsonObject]
    public class HyprlandLayer
    {
        [JsonProperty("address")]
        public string address;
        public IntPtr addressIntPtr =>  AddressToIntPtr(address);

        [JsonProperty("x")]
        public int x;
        [JsonProperty("y")]
        public int y;
        public Vector2Int posVector => new Vector2Int(x, y);

        [JsonProperty("w")]
        public int w;
        [JsonProperty("h")]
        public int h;
        public Vector2Int sizeVector => new Vector2Int(w, h);

        [JsonProperty("pid")]
        public int pid;

        [JsonProperty("namespace")]
        public string name;
    }
}