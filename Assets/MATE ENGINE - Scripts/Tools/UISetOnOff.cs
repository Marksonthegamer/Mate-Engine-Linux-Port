using UnityEngine;
using System.Collections.Generic;

public class UISetOnOff : MonoBehaviour
{
    public GameObject target;
    public void ToggleTarget()
    {
        if (target != null)
            target.SetActive(!target.activeSelf);
    }
    public void SetOnOff(GameObject obj)
    {
        if (obj != null)
            obj.SetActive(!obj.activeSelf);
    }

    public void ToggleAccessoryByName(string ruleName)
    {
        foreach (var handler in AccessoiresHandler.ActiveHandlers)
        {
            foreach (var rule in handler.rules)
            {
                if (rule.ruleName == ruleName)
                {
                    rule.isEnabled = !rule.isEnabled;
                    break;
                }
            }
        }
    }
    public void ToggleBubbleFeature()
    {
        foreach (var handler in AvatarBubbleHandler.ActiveHandlers)
            handler.ToggleBubbleFromUI();
    }


    public void SetAccessoryState(string ruleName, bool state)
    {
        foreach (var handler in AccessoiresHandler.ActiveHandlers)
        {
            foreach (var rule in handler.rules)
            {
                if (rule.ruleName == ruleName)
                {
                    rule.isEnabled = state;
                    break;
                }
            }
        }
    }
    public void ToggleBigScreenFeature()
    {
        foreach (var handler in AvatarBigScreenHandler.ActiveHandlers)
            handler.ToggleBigScreenFromUI();
    }

    public void ToggleChibiMode()
    {
        foreach (var chibi in GameObject.FindObjectsByType<ChibiToggle>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            chibi.ToggleChibiMode();
    }

    public void CloseApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    public void OpenWebsite(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
        }
    }
}
