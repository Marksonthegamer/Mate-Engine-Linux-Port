using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class ToggleBulkStylerWindow : EditorWindow
{
    bool includeInactive = true;

    Color normal = Color.white;
    Color highlighted = new(0.882f, 0.882f, 0.882f, 1f);
    Color pressed = new(0.698f, 0.698f, 0.698f, 1f);
    Color selected = Color.white;
    Color disabled = new(0.784f, 0.784f, 0.784f, 0.5f);

    bool applyBackgroundMaterial = false;
    Material backgroundMaterial;

    [MenuItem("Tools/Toggle Bulk Styler")]
    public static void Open()
    {
        GetWindow<ToggleBulkStylerWindow>("Toggle Styler");
    }

    void OnGUI()
    {
        GUILayout.Label("Scope", EditorStyles.boldLabel);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);

        GUILayout.Space(6);
        GUILayout.Label("Toggle Styling", EditorStyles.boldLabel);
        normal = EditorGUILayout.ColorField("Normal", normal);
        highlighted = EditorGUILayout.ColorField("Highlighted", highlighted);
        pressed = EditorGUILayout.ColorField("Pressed", pressed);
        selected = EditorGUILayout.ColorField("Selected", selected);
        disabled = EditorGUILayout.ColorField("Disabled", disabled);

        applyBackgroundMaterial = EditorGUILayout.Toggle("Apply Background Material", applyBackgroundMaterial);
        using (new EditorGUI.DisabledScope(!applyBackgroundMaterial))
        {
            backgroundMaterial = (Material)EditorGUILayout.ObjectField("Background Material", backgroundMaterial, typeof(Material), false);
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Apply to All Toggles"))
        {
            ApplyToAllToggles();
        }
    }

    void ApplyToAllToggles()
    {
        var toggles = FindObjectsOfType<Toggle>(true);
        int count = 0;
        foreach (var t in toggles)
        {
            if (t == null) continue;
            if (!includeInactive && !t.gameObject.activeInHierarchy) continue;

            Undo.RecordObject(t, "Apply Toggle Colors");
            var cb = t.colors;
            cb.normalColor = normal;
            cb.highlightedColor = highlighted;
            cb.pressedColor = pressed;
            cb.selectedColor = selected;
            cb.disabledColor = disabled;
            t.colors = cb;
            EditorUtility.SetDirty(t);

            if (applyBackgroundMaterial && backgroundMaterial != null)
            {
                var bg = FindBackgroundImage(t);
                if (bg != null)
                {
                    Undo.RecordObject(bg, "Apply Toggle Background Material");
                    bg.material = backgroundMaterial;
                    EditorUtility.SetDirty(bg);
                }
            }

            count++;
        }

        Debug.Log($"Applied to {count} toggles.");
    }

    static Image FindBackgroundImage(Toggle t)
    {
        if (t == null) return null;
        var imgs = t.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < imgs.Length; i++)
        {
            var img = imgs[i];
            if (img.name.ToLower().Contains("background")) return img;
        }
        return null;
    }
}
