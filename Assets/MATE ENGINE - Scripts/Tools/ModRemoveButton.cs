using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class ModRemoveButton : MonoBehaviour
{
    public Button button;
    public string filePath;
    public ulong workshopId;

    void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(OnClick);
    }

    void OnClick()
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch { }
        }

        var modHandler = FindFirstObjectByType<MEModHandler>();
        if (modHandler != null) modHandler.SendMessage("LoadAllModsInFolder", SendMessageOptions.DontRequireReceiver);
        var dance = FindFirstObjectByType<CustomDancePlayer.AvatarDanceHandler>();
        if (dance != null) dance.RescanMods();
    }
}
