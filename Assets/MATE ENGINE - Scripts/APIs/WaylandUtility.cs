using UnityEngine;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using Gtk;



public static class WaylandUtility
{
    public static async Task<Vector2> GetWindowPositionKWin() 
    {
        var winRect = await Object.FindFirstObjectByType<KWinManager>().GetWindowGeometry();
        return new Vector2(winRect.x, winRect.y);
    }
}
