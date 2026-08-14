using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MoveToPrimaryScreen : MonoBehaviour
{
    public void Action()
    {
        WindowManager.Instance.GetWindowRect(out RectInt winRect);
        var targetScreen = FindMainMonitorRect(winRect);
        WindowManager.Instance.SetWindowPosition(targetScreen.x, targetScreen.y);
        WindowManager.Instance.RaiseWindow();
    }
    
    private RectInt FindMainMonitorRect(RectInt windowRect)
    {
        if (!WindowManager.Instance) return new RectInt(0, 0, Screen.currentResolution.width, Screen.currentResolution.height);
        
        List<RectInt> monitorRects = WindowManager.Instance.GetAllMonitors().Values.ToList();
        return new(new((monitorRects[0].x + monitorRects[0].width) / 2 - windowRect.width / 2, (monitorRects[0].y + monitorRects[0].height) / 2 - windowRect.height / 2), monitorRects[0].size);
    }

}