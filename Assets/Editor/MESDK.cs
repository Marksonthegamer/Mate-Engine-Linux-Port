using UnityEditor;
using UnityEngine;
using System.Reflection;

public class MESDK : EditorWindow
{
    private enum Tab { ModExporter, ModelExporter, BoneMerger, VRMValidator }
    private Tab currentTab;

    private ScriptableObject modExporterInstance;
    private ScriptableObject modelExporterInstance;
    private ScriptableObject boneMergerInstance;
    private ScriptableObject vrmValidatorInstance;

    private Texture2D bannerTexture;
    private Vector2 scrollPosition; // ✅ scroll state

    [MenuItem("MateEngine/ME SDK")]
    public static void ShowWindow()
    {
        var window = GetWindow<MESDK>("MateEngine SDK 0.5");
        window.minSize = new Vector2(500, 400);
    }

    private void OnEnable()
    {
        modExporterInstance = CreateInstance("ModExporterWindow");
        modelExporterInstance = CreateInstance("MEModelExporter");
        boneMergerInstance = CreateInstance("MateEngine.MEBoneMerger"); // namespaced
        vrmValidatorInstance = CreateInstance("VrmValidatorWindow");

        bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Editor/sdk.png");
    }

    private void OnGUI()
    {
        DrawBanner();

        GUIStyle toolbarStyle = new GUIStyle(EditorStyles.toolbarButton)
        {
            fixedHeight = 30,
            fontStyle = FontStyle.Bold
        };

        GUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(30));
        for (int i = 0; i < 4; i++)
        {
            string label = ((Tab)i).ToString().Replace("ModExporter", "Mod Exporter")
                                              .Replace("ModelExporter", "Model Exporter")
                                              .Replace("BoneMerger", "Bone Merger")
                                              .Replace("VRMValidator", "VRM Validator");

            if (GUILayout.Toggle(currentTab == (Tab)i, label, toolbarStyle))
            {
                currentTab = (Tab)i;
            }
        }
        GUILayout.EndHorizontal();

        DrawSectionTitle();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        switch (currentTab)
        {
            case Tab.ModExporter:
                CallOnGUI(modExporterInstance);
                break;
            case Tab.ModelExporter:
                CallOnGUI(modelExporterInstance);
                break;
            case Tab.BoneMerger:
                CallOnGUI(boneMergerInstance);
                break;
            case Tab.VRMValidator:
                CallOnGUI(vrmValidatorInstance);
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSectionTitle()
    {
        string title = currentTab switch
        {
            Tab.ModExporter => "Export your mod",
            Tab.ModelExporter => "Export your .me Model",
            Tab.BoneMerger => "Let us help merge armatures",
            Tab.VRMValidator => "Check for Failures",
            _ => ""
        };

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            margin = new RectOffset(10, 10, 10, 10),
            padding = new RectOffset(0, 0, 6, 6)
        };

        GUILayout.Space(8);
        Rect labelRect = GUILayoutUtility.GetRect(new GUIContent(title), titleStyle);
        EditorGUI.LabelField(labelRect, title, titleStyle);
        GUILayout.Space(6);
    }

    private void DrawBanner()
    {
        if (bannerTexture == null) return;

        float bannerWidth = position.width;
        float bannerHeight = bannerTexture.height * (bannerWidth / bannerTexture.width);
        Rect bannerRect = GUILayoutUtility.GetRect(bannerWidth, bannerHeight, GUILayout.ExpandWidth(true));
        GUI.DrawTexture(bannerRect, bannerTexture, ScaleMode.ScaleToFit);
        EditorGUILayout.Space(5);
    }

    private void CallOnGUI(ScriptableObject instance)
    {
        if (instance == null)
        {
            EditorGUILayout.HelpBox("Tool failed to initialize.", MessageType.Error);
            return;
        }

        var method = instance.GetType().GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
        if (method != null)
        {
            method.Invoke(instance, null);
        }
        else
        {
            EditorGUILayout.HelpBox("Unable to render tool (OnGUI not found).", MessageType.Error);
        }
    }
}
