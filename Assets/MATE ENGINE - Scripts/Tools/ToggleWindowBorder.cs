using UnityEngine;

public class ToggleWindowBorder : MonoBehaviour
{
    private bool showBorder;

    private void Start()
    {
        showBorder = SaveLoadHandler.Instance.data.windowType != WindowType.ShowBorder;
    }

    public void ToggleBordered()
    {
        if (SaveLoadHandler.Instance.data.windowType == WindowType.ShowBorder) return;
        showBorder = !showBorder;
        WindowManager.Instance.SetWindowBorderless(showBorder);
    }
}