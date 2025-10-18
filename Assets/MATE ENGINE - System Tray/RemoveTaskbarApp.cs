using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class RemoveTaskbarApp : MonoBehaviour
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    const int GWL_EXSTYLE = -20;
    const int WS_EX_TOOLWINDOW = 0x00000080;
    const int SW_RESTORE = 9;

    private static IntPtr unityHWND = IntPtr.Zero;

    private bool _isHidden = true;
    public bool IsHidden => _isHidden;

    void Start()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        unityHWND = GetUnityWindow();
        if (unityHWND != IntPtr.Zero)
        {
            int exStyle = GetWindowLong(unityHWND, GWL_EXSTYLE);
            SetWindowLong(unityHWND, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
            _isHidden = true;
        }
#endif
    }

    public void ToggleAppMode()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        unityHWND = GetUnityWindow();
        if (unityHWND == IntPtr.Zero)
            return;

        int exStyle = GetWindowLong(unityHWND, GWL_EXSTYLE);

        if (_isHidden)
        {
            SetWindowLong(unityHWND, GWL_EXSTYLE, exStyle & ~WS_EX_TOOLWINDOW);
            ShowWindow(unityHWND, SW_RESTORE);
            SetForegroundWindow(unityHWND);
            _isHidden = false;
        }
        else
        {
            SetWindowLong(unityHWND, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
            _isHidden = true;
        }
#endif
    }

    private IntPtr GetUnityWindow()
    {
        string title = Application.productName;
        IntPtr hwnd = FindWindow(null, title);
        if (hwnd == IntPtr.Zero)
            hwnd = FindWindow("UnityWndClass", null);
        return hwnd;
    }
}
