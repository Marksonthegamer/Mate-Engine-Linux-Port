using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;

public class TMPFontReplacer : MonoBehaviour
{
    public TMP_FontAsset newFont;

    [ContextMenu("Replace All TMP Fonts in Scene")]
    void ReplaceFontsInScene()
    {
        if (newFont == null)
        {
            Debug.LogError("Assign a font in the inspector first.");
            return;
        }

        TextMeshProUGUI[] allTMPs = Object.FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (var tmp in allTMPs)
        {
            if (tmp.font != newFont)
            {
                Undo.RecordObject(tmp, "Replace TMP Font");
                tmp.font = newFont;
                count++;
            }
        }

        Debug.Log($"Updated {count} TMP components to use {newFont.name}");
    }
}
#endif
