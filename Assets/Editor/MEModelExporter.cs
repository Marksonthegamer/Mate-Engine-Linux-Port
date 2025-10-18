using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

public class MEModelExporter : EditorWindow
{
    private GameObject prefabToExport;
    private string bundleName = "custommodel";

    [MenuItem("MateEngine/ME Model Exporter")]
    public static void ShowWindow()
    {
        GetWindow<MEModelExporter>("ME Model Exporter");
    }

    private void OnGUI()
    {
        GUILayout.Label("Export .ME Model", EditorStyles.boldLabel);
        EditorGUILayout.Space();


        GUILayout.Space(8); // space above

        EditorGUILayout.HelpBox(
            "ME. Model Format Usage\n" +
            "The MateEngine SDK is limited and only supports a few modding aspects. Creating your own C# assemblies is not allowed, as we aim to prevent any potential malware distribution.",
            MessageType.Info
        );

        GUILayout.Space(12); // space below


        prefabToExport = (GameObject)EditorGUILayout.ObjectField("Your Model", prefabToExport, typeof(GameObject), true);
        bundleName = EditorGUILayout.TextField("Model Name", bundleName);

        EditorGUILayout.Space();

        GUI.enabled = prefabToExport != null && !string.IsNullOrEmpty(bundleName);
        if (GUILayout.Button("Export"))
        {
            ExportAssetBundle();
        }
        GUI.enabled = true;
    }

    private void ExportAssetBundle()
    {
        if (prefabToExport == null)
        {
            Debug.LogError("[AssetBundle Exporter] No object assigned.");
            return;
        }

        string exportFolder = "Assets/ExportedModel";
        if (!Directory.Exists(exportFolder))
            Directory.CreateDirectory(exportFolder);

        string tempBuildPath = "TempBundleBuild";
        if (!Directory.Exists(tempBuildPath))
            Directory.CreateDirectory(tempBuildPath);

        string assetPath = AssetDatabase.GetAssetPath(prefabToExport);
        bool isSceneObject = string.IsNullOrEmpty(assetPath);

        if (isSceneObject)
        {
            string tempPrefabPath = "Assets/__TempExport.prefab";
            PrefabUtility.SaveAsPrefabAsset(prefabToExport, tempPrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            assetPath = tempPrefabPath;
        }

        var allDependencies = new HashSet<string>(AssetDatabase.GetDependencies(assetPath, true)
            .Where(path => !string.IsNullOrEmpty(path) && !path.EndsWith(".cs")));

        // Also include any files referenced by components
        CollectComponentFileReferences(prefabToExport, allDependencies, exportFolder);

        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = bundleName,
            assetNames = allDependencies.ToArray()
        };

        BuildPipeline.BuildAssetBundles(tempBuildPath, new[] { build }, BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget);

        string builtFilePath = Path.Combine(tempBuildPath, bundleName);
        string finalBundlePath = Path.Combine(exportFolder, bundleName + ".me");

        if (File.Exists(builtFilePath))
        {
            File.Copy(builtFilePath, finalBundlePath, true);
            Debug.Log($"[AssetBundle Exporter] Exported to: {Path.GetFullPath(finalBundlePath)}");
        }
        else
        {
            Debug.LogError("[AssetBundle Exporter] Export failed: .me file not found.");
        }

        if (isSceneObject && File.Exists(assetPath))
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        if (Directory.Exists(tempBuildPath)) Directory.Delete(tempBuildPath, true);

        EditorUtility.RevealInFinder(exportFolder);
    }

    private void CollectComponentFileReferences(GameObject root, HashSet<string> deps, string exportFolder)
    {
        foreach (var comp in root.GetComponentsInChildren<Component>(true))
        {
            if (comp == null) continue;

            if (comp is VideoPlayer vp)
            {
                if (vp.clip != null)
                {
                    string clipPath = AssetDatabase.GetAssetPath(vp.clip);
                    if (!string.IsNullOrEmpty(clipPath))
                        deps.Add(clipPath);
                }

                if (!string.IsNullOrEmpty(vp.url) && File.Exists(vp.url))
                {
                    string fileName = Path.GetFileName(vp.url);
                    string destPath = Path.Combine(exportFolder, fileName);
                    File.Copy(vp.url, destPath, true);
                    Debug.Log($"[Exporter] Copied external video: {vp.url} -> {destPath}");
                }
            }

            // General reflection fallback: search fields for UnityEngine.Object
            var fields = comp.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;

                var value = field.GetValue(comp) as Object;
                if (value == null) continue;

                string fieldPath = AssetDatabase.GetAssetPath(value);
                if (!string.IsNullOrEmpty(fieldPath) && fieldPath.StartsWith("Assets"))
                {
                    deps.Add(fieldPath);
                }
            }
        }
    }
}