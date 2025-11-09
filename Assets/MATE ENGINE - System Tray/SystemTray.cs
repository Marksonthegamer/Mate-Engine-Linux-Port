using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SystemTray : MonoBehaviour
{
    [Serializable]
    public class TrayAction
    {
        public string label;
        public TrayActionType type;
        public GameObject handlerObject;
        public string toggleField;
        public string methodName;
    }
    public enum TrayActionType { Toggle, Button, Method }
    
    [SerializeField] private string iconName;
    [SerializeField] public List<TrayAction> actions = new();

    private bool trayBuilt;

    private void LateUpdate()
    {
        if (trayBuilt)
            return;
        TrayIndicator.Instance.OnBuildMenu = BuildMenu;
        TrayIndicator.Instance.InitializeTrayIcon(iconName);
        TrayIndicator.Instance.AddMenuItem(BuildMenu());
        trayBuilt = true;
    }

    private List<TrayMenuEntry> BuildMenu()
    {
        List<TrayMenuEntry> context = new();
        foreach (var action in actions)
        {
            if (action.type == TrayActionType.Toggle)
            {
                bool state = GetToggleState(action);
                context.Add(new (action.label, () => ToggleAction(action), true, state));
            }
            else if (action.type == TrayActionType.Button)
            {
                context.Add(new (action.label, () => ButtonAction(action)));
            }
        }
        
        var app = FindFirstObjectByType<RemoveTaskbarApp>();
        bool hidden = app != null && app.IsHidden;
        string toggleLabel = "Hide App from Dock";
        context.Add(new (toggleLabel, () =>
        {
            if (app != null)
            {
                app.ToggleAppMode();
            }
        }, true, hidden
        ));
        
        context.Add(new ("Quit MateEngine", QuitApp));
        return context;
    }

    private bool GetToggleState(TrayAction action)
    {
        if (action.handlerObject == null || string.IsNullOrEmpty(action.toggleField)) return false;
        var mono = action.handlerObject.GetComponent<MonoBehaviour>();
        if (mono == null) return false;
        var type = mono.GetType();
        var field = type.GetField(action.toggleField);
        if (field != null && field.FieldType == typeof(Toggle))
        {
            var toggle = field.GetValue(mono) as Toggle;
            if (toggle != null)
                return toggle.isOn;
        }
        return false;
    }

    private void ToggleAction(TrayAction action)
    {
        if (action.handlerObject == null || string.IsNullOrEmpty(action.toggleField)) return;
        var mono = action.handlerObject.GetComponent<MonoBehaviour>();
        if (mono == null) return;
        var type = mono.GetType();
        var field = type.GetField(action.toggleField);
        if (field != null && field.FieldType == typeof(Toggle))
        {
            var toggle = field.GetValue(mono) as Toggle;
            if (toggle != null)
            {
                toggle.isOn = !toggle.isOn;
            }
        }
    }

    private void ButtonAction(TrayAction action)
    {
        if (action.handlerObject == null || string.IsNullOrEmpty(action.methodName)) return;
        var mono = action.handlerObject.GetComponent<MonoBehaviour>();
        if (mono == null) return;
        var type = mono.GetType();
        var method = type.GetMethod(action.methodName);
        if (method != null)
            method.Invoke(mono, null);
    }

    private void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
