using System;
using UnityEngine;
using X11;

public class RemoveTaskbarApp : MonoBehaviour
{

    private IntPtr _unityHwnd = IntPtr.Zero;

    private bool _isHidden = true;
    public bool IsHidden => _isHidden;

    void Start()
    {
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
        _unityHwnd = X11Manager.Instance.UnityWindow;
        if (_unityHwnd != IntPtr.Zero)
        {
            X11Manager.Instance.HideFromTaskbar();
            _isHidden = true;
        }
#endif
    }

    public void ToggleAppMode()
    {
#if UNITY_STANDALONE_WIN && UNITY_EDITOR
        if (unityHWND == IntPtr.Zero)
            return;

        if (_isHidden)
        {
            X11Manager.Instance.HideFromTaskbar(false);
            _isHidden = false;
        }
        else
        {
            X11Manager.Instance.HideFromTaskbar();
            _isHidden = true;
        }
#endif
    }
}
