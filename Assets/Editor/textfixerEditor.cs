using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(textfixer))]
public class textfixerEditor : Editor
{
    private TextAlignmentOptions alignmentOption = TextAlignmentOptions.Center;
    private bool alignmentInitialized = false;

    private Dictionary<(float, float), List<TextMeshProUGUI>> sizeBatches = new Dictionary<(float, float), List<TextMeshProUGUI>>();
    private Dictionary<(float, float), Vector2> batchSizes = new Dictionary<(float, float), Vector2>();

    private static List<RectTransform> highlightedRects = new List<RectTransform>();
    private static bool sceneHooked = false;

    private static Dictionary<(float, float), string> batchNames = new Dictionary<(float, float), string>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Global TextMeshPro Alignment Tool", EditorStyles.boldLabel);

        var root = (target as textfixer).gameObject;
        var texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);

        if (!sceneHooked)
        {
            SceneView.duringSceneGui += DrawBatchRects;
            sceneHooked = true;
        }

        if (texts.Length == 0)
        {
            EditorGUILayout.HelpBox("No TextMeshProUGUI elements found.", MessageType.Info);
            return;
        }

        if (!alignmentInitialized && texts.Length > 0)
        {
            alignmentOption = texts[0].alignment;
            alignmentInitialized = true;
        }

        EditorGUILayout.LabelField("Set Alignment For ALL Texts", EditorStyles.boldLabel);
        alignmentOption = (TextAlignmentOptions)EditorGUILayout.EnumPopup("Alignment", alignmentOption);

        if (GUILayout.Button("Apply Alignment to ALL TextMeshProUGUI"))
        {
            foreach (var t in texts)
            {
                if (t == null) continue;
                Undo.RecordObject(t, "Batch Alignment Change");
                t.alignment = alignmentOption;
                EditorUtility.SetDirty(t);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Affected TextMeshProUGUI Elements:", EditorStyles.boldLabel);

        foreach (var t in texts)
            EditorGUILayout.ObjectField(t, typeof(TextMeshProUGUI), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Batch RectTransform Size Editor", EditorStyles.boldLabel);

        sizeBatches.Clear();
        batchSizes.Clear();
        foreach (var t in texts)
        {
            RectTransform rt = t.GetComponent<RectTransform>();
            if (rt == null) continue;
            var size = rt.sizeDelta;
            var key = (size.x, size.y);
            if (!sizeBatches.ContainsKey(key)) sizeBatches[key] = new List<TextMeshProUGUI>();
            sizeBatches[key].Add(t);

            if (!batchSizes.ContainsKey(key)) batchSizes[key] = size;
        }

        foreach (var kv in sizeBatches)
        {
            var key = kv.Key;
            var textList = kv.Value;
            Vector2 sizeValue = batchSizes[key];

            EditorGUILayout.BeginVertical("box");

            var uiTypes = new HashSet<string>();
            foreach (var t in textList)
            {
                var go = t.gameObject.transform.parent != null ? t.gameObject.transform.parent.gameObject : t.gameObject;
                if (go.GetComponent<UnityEngine.UI.Button>() != null) uiTypes.Add("Button");
                else if (go.GetComponent<UnityEngine.UI.Toggle>() != null) uiTypes.Add("Toggle");
                else if (go.GetComponent<UnityEngine.UI.Slider>() != null) uiTypes.Add("Slider");
                else if (go.GetComponent<TMP_InputField>() != null) uiTypes.Add("InputField");
                else if (go.GetComponent<TMP_Dropdown>() != null) uiTypes.Add("Dropdown");
                else if (go.GetComponent<UnityEngine.UI.Scrollbar>() != null) uiTypes.Add("Scrollbar");
                else if (go.GetComponent<UnityEngine.UI.ScrollRect>() != null) uiTypes.Add("ScrollRect");
                else if (go.GetComponent<UnityEngine.UI.RawImage>() != null) uiTypes.Add("RawImage");
                else if (go.GetComponent<UnityEngine.UI.Image>() != null) uiTypes.Add("Image");
                else uiTypes.Add("Text Only");
            }
            string autoName = string.Join(", ", uiTypes.OrderBy(s => s));
            if (!batchNames.ContainsKey(key) || string.IsNullOrEmpty(batchNames[key]))
                batchNames[key] = autoName;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Batch Name:", GUILayout.Width(85));
            batchNames[key] = EditorGUILayout.TextField(batchNames[key]);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField($"Batch: {textList.Count} Elements - Width: {key.Item1}, Height: {key.Item2} [{batchNames[key]}]");

            if (GUILayout.Button("Select All in Batch"))
            {
                var objects = new List<GameObject>();
                highlightedRects.Clear();
                foreach (var t in textList)
                {
                    if (t != null)
                    {
                        objects.Add(t.gameObject);
                        var rt = t.GetComponent<RectTransform>();
                        if (rt != null) highlightedRects.Add(rt);
                    }
                }
                Selection.objects = objects.ToArray();
                SceneView.RepaintAll();
            }

            Vector2 newSize = EditorGUILayout.Vector2Field("Width / Height", sizeValue);

            if (newSize.x != key.Item1 || newSize.y != key.Item2)
            {
                foreach (var t in textList)
                {
                    var rt = t.GetComponent<RectTransform>();
                    if (rt == null) continue;

                    Undo.RecordObject(rt, "Batch Rect Size Change");

                    Vector3[] worldCorners = new Vector3[4];
                    rt.GetWorldCorners(worldCorners);
                    float worldTopY = worldCorners[1].y; 

                    rt.sizeDelta = newSize;

                    rt.GetWorldCorners(worldCorners);
                    float newWorldTopY = worldCorners[1].y;
                    float deltaWorldY = worldTopY - newWorldTopY;

                    rt.anchoredPosition += new Vector2(0, deltaWorldY / rt.lossyScale.y);

                    EditorUtility.SetDirty(rt);
                }
                batchSizes[key] = newSize;
            }
            EditorGUILayout.EndVertical();
        }
    }

    private static void DrawBatchRects(SceneView sceneView)
    {
        if (highlightedRects == null || highlightedRects.Count == 0) return;

        Handles.color = Color.yellow;
        foreach (var rt in highlightedRects)
        {
            if (rt == null) continue;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Handles.DrawLine(corners[0], corners[1]);
            Handles.DrawLine(corners[1], corners[2]);
            Handles.DrawLine(corners[2], corners[3]);
            Handles.DrawLine(corners[3], corners[0]);
        }
        Handles.color = Color.white;
    }
}
