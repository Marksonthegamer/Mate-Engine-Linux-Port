using UnityEditor;
using UnityEngine;
using System.IO;

[InitializeOnLoad]
public static class MEModInitializer
{
    private const string SETTINGS_PATH = "Assets/StreamingAssets/Mods/ModLoader/Chibi Mode/settings.json.deactivated";
    private const string PREFAB_PATH_KEY = "MEModInitializer.ReferenceInputPath";

    static MEModInitializer()
    {
        // Chibi folders
        CreateFolderWithKeep(Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Chibi Mode/Sounds/Enter Sounds"));
        CreateFolderWithKeep(Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Chibi Mode/Sounds/Exit Sounds"));
        CreateFolderWithKeep(Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Chibi Mode"));

        // Hover Reactions base folder
        CreateFolderWithKeep(Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Hover Reactions"));

#if UNITY_EDITOR
        EditorApplication.delayCall += TryGenerateSettingsFromReference;
#endif
    }

    private static void CreateFolderWithKeep(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            Debug.Log("[MEModInitializer] Created folder: " + path);
        }

        string keepFile = Path.Combine(path, ".keep");
        if (!File.Exists(keepFile))
        {
            File.WriteAllText(keepFile, "This file ensures the folder is included in build.");
            Debug.Log("[MEModInitializer] Created .keep file in: " + path);
        }
    }

    private static void TryGenerateSettingsFromReference()
    {
        string prefabPath = EditorPrefs.GetString(PREFAB_PATH_KEY, "");
        if (string.IsNullOrEmpty(prefabPath)) return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("[MEModInitializer] Reference prefab not found at: " + prefabPath);
            return;
        }

        var chibi = prefab.GetComponentInChildren<ChibiToggle>();
        if (chibi == null)
        {
            Debug.LogWarning("[MEModInitializer] No ChibiToggle found in reference input.");
            return;
        }

        ChibiSettingsData data = new ChibiSettingsData
        {
            chibiArmatureScale = chibi.chibiArmatureScale,
            chibiHeadScale = chibi.chibiHeadScale,
            chibiUpperLegScale = chibi.chibiUpperLegScale,
        };

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SETTINGS_PATH, json);
        Debug.Log("[MEModInitializer] Wrote Chibi Mode settings.json.deactivated from reference input.");
    }

    [MenuItem("MateEngine/Set Reference Values Input")]
    public static void SetReferenceInput()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Reference Input", "Select a prefab in the Project window to use as Reference Values Input.", "OK");
            return;
        }

        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
        {
            EditorUtility.DisplayDialog("Reference Input", "Selection must be a prefab in the Project window.", "OK");
            return;
        }

        EditorPrefs.SetString(PREFAB_PATH_KEY, path);
        Debug.Log("[MEModInitializer] Set Reference Values Input to: " + path);
    }
}
