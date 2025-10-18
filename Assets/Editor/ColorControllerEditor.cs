using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(ColorController))]
public class ColorControllerEditor : Editor
{
    SerializedProperty targetsProp;
    SerializedProperty fadeDurationProp;
    ReorderableList reorderableList;

    void OnEnable()
    {
        targetsProp = serializedObject.FindProperty("targets");
        fadeDurationProp = serializedObject.FindProperty("fadeDuration");

        reorderableList = new ReorderableList(serializedObject, targetsProp, true, true, true, true);
        reorderableList.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, "Color Targets");
        };

        reorderableList.elementHeightCallback = (int index) =>
        {
            var element = targetsProp.GetArrayElementAtIndex(index);
            if (!element.isExpanded) return EditorGUIUtility.singleLineHeight + 4;

            float h = 0;
            h += EditorGUIUtility.singleLineHeight + 2;
            h += EditorGUIUtility.singleLineHeight + 2;
            h += EditorGUIUtility.singleLineHeight + 2;
            h += EditorGUIUtility.singleLineHeight + 2;
            h += EditorGUIUtility.singleLineHeight + 2;
            h += EditorGUIUtility.singleLineHeight + 2;

            h += EditorGUIUtility.singleLineHeight + 2;
            var tagsProp = element.FindPropertyRelative("exclusiveTags");
            h += (EditorGUIUtility.singleLineHeight + 2) * tagsProp.arraySize;
            h += EditorGUIUtility.singleLineHeight + 2;

            h += EditorGUIUtility.singleLineHeight + 2;
            if (element.FindPropertyRelative("swingMode").boolValue)
            {
                h += EditorGUIUtility.singleLineHeight + 2;
                h += EditorGUIUtility.singleLineHeight + 2;
                h += EditorGUIUtility.singleLineHeight + 2;
            }

            h += EditorGUIUtility.singleLineHeight + 2;
            h += EditorGUIUtility.singleLineHeight + 2;

            if ((ColorController.TargetType)element.FindPropertyRelative("type").enumValueIndex == ColorController.TargetType.Light)
            {
                h += EditorGUIUtility.singleLineHeight + 2;
                if (element.FindPropertyRelative("intensityOverride").boolValue)
                    h += EditorGUIUtility.singleLineHeight + 2;
                h += EditorGUIUtility.singleLineHeight + 2;
            }
            return h + 4;
        };

        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            var element = targetsProp.GetArrayElementAtIndex(index);

            Rect foldRect = new Rect(rect.x, rect.y, 16, EditorGUIUtility.singleLineHeight);
            element.isExpanded = EditorGUI.Foldout(foldRect, element.isExpanded, GUIContent.none);

            Rect idRect = new Rect(rect.x + 20, rect.y, rect.width - 20, EditorGUIUtility.singleLineHeight);
            string idVal = element.FindPropertyRelative("id").stringValue;
            EditorGUI.LabelField(idRect, string.IsNullOrEmpty(idVal) ? "(no ID)" : idVal, EditorStyles.boldLabel);

            if (!element.isExpanded)
                return;

            float y = rect.y + EditorGUIUtility.singleLineHeight + 2;
            var groupID = element.FindPropertyRelative("groupID").stringValue;
            bool hasGroup = !string.IsNullOrEmpty(groupID);

            EditorGUI.BeginDisabledGroup(hasGroup);
            EditorGUI.PropertyField(new Rect(rect.x, y, 90, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("enabled"), new GUIContent("Enabled"));
            EditorGUI.EndDisabledGroup();

            EditorGUI.PropertyField(new Rect(rect.x + 100, y, 160, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("allowEnableControl"), new GUIContent("Allow Enable Ctrl"));
            y += EditorGUIUtility.singleLineHeight + 2;

            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("type"));
            y += EditorGUIUtility.singleLineHeight + 2;

            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("target"));
            y += EditorGUIUtility.singleLineHeight + 2;

            element.FindPropertyRelative("id").stringValue =
                EditorGUI.TextField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), "ID", element.FindPropertyRelative("id").stringValue);
            y += EditorGUIUtility.singleLineHeight + 2;

            element.FindPropertyRelative("groupID").stringValue =
                EditorGUI.TextField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), "Group ID", element.FindPropertyRelative("groupID").stringValue);
            y += EditorGUIUtility.singleLineHeight + 2;

            var tagsProp = element.FindPropertyRelative("exclusiveTags");
            EditorGUI.LabelField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), "Exclusive Tags");
            y += EditorGUIUtility.singleLineHeight + 2;
            int removeIdx = -1;
            for (int t = 0; t < tagsProp.arraySize; t++)
            {
                tagsProp.GetArrayElementAtIndex(t).stringValue = EditorGUI.TextField(
                    new Rect(rect.x + 12, y, rect.width - 34, EditorGUIUtility.singleLineHeight),
                    tagsProp.GetArrayElementAtIndex(t).stringValue);
                if (GUI.Button(new Rect(rect.x + rect.width - 20, y, 18, EditorGUIUtility.singleLineHeight), "-", EditorStyles.miniButton))
                    removeIdx = t;
                y += EditorGUIUtility.singleLineHeight + 2;
            }
            if (removeIdx >= 0)
                tagsProp.DeleteArrayElementAtIndex(removeIdx);
            if (GUI.Button(new Rect(rect.x, y, 80, EditorGUIUtility.singleLineHeight), "+ Add Tag", EditorStyles.miniButton))
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            y += EditorGUIUtility.singleLineHeight + 2;

            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("swingMode"), new GUIContent("Swing Mode"));
            y += EditorGUIUtility.singleLineHeight + 2;
            if (element.FindPropertyRelative("swingMode").boolValue)
            {
                EditorGUI.PropertyField(new Rect(rect.x + 20, y, rect.width - 20, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("targetBone"), new GUIContent("Bone (for Swing)"));
                y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.Slider(new Rect(rect.x + 20, y, rect.width - 20, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("swingSmoothness"), 0, 1, new GUIContent("Swing Smoothness"));
                y += EditorGUIUtility.singleLineHeight + 2;
                EditorGUI.PropertyField(new Rect(rect.x + 20, y, rect.width - 20, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("blockYMovement"), new GUIContent("Block Y Movement"));
                y += EditorGUIUtility.singleLineHeight + 2;
            }

            EditorGUI.Slider(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("hue"), 0, 1, new GUIContent("Hue"));
            y += EditorGUIUtility.singleLineHeight + 2;
            EditorGUI.Slider(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("saturation"), 0, 1, new GUIContent("Saturation"));
            y += EditorGUIUtility.singleLineHeight + 2;

            if ((ColorController.TargetType)element.FindPropertyRelative("type").enumValueIndex == ColorController.TargetType.Light)
            {
                EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("intensityOverride"), new GUIContent("Intensity Override"));
                y += EditorGUIUtility.singleLineHeight + 2;
                float maxIntensity = 1f;
                if (element.FindPropertyRelative("intensityOverride").boolValue)
                {
                    EditorGUI.PropertyField(new Rect(rect.x + 20, y, rect.width - 20, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("maxIntensity"), new GUIContent("Max Intensity"));
                    maxIntensity = element.FindPropertyRelative("maxIntensity").floatValue;
                    y += EditorGUIUtility.singleLineHeight + 2;
                }
                EditorGUI.Slider(new Rect(rect.x, y, rect.width, EditorGUIUtility.singleLineHeight), element.FindPropertyRelative("intensity"), 0, maxIntensity, new GUIContent("Intensity"));
                y += EditorGUIUtility.singleLineHeight + 2;
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Transition Settings", EditorStyles.boldLabel);
        EditorGUILayout.Slider(fadeDurationProp, 0f, 6f, new GUIContent("Fade Duration (sec)"));
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("Color Groups", EditorStyles.boldLabel);
        var colorController = (ColorController)target;
        List<string> allGroups = new List<string>();
        for (int i = 0; i < targetsProp.arraySize; i++)
        {
            var element = targetsProp.GetArrayElementAtIndex(i);
            var groupProp = element.FindPropertyRelative("groupID");
            string groupID = groupProp.stringValue;
            if (!string.IsNullOrWhiteSpace(groupID) && !allGroups.Contains(groupID))
                allGroups.Add(groupID);
        }

        foreach (var group in allGroups)
        {
            bool state = colorController.targets.Any(t => t.groupID == group && t.enabled);
            EditorGUI.BeginChangeCheck();
            state = EditorGUILayout.ToggleLeft($"Enable Group \"{group}\"", state);
            if (EditorGUI.EndChangeCheck())
            {
                colorController.SetGroupEnabled(group, state);
                EditorUtility.SetDirty(target);
            }
        }
        EditorGUILayout.Space(8);

        reorderableList.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}
