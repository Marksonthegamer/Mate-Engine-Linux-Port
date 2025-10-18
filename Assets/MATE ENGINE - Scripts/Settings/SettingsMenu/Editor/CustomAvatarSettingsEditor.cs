using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using System;
using System.Linq;
using System.Reflection;

[CustomEditor(typeof(CustomAvatarSettings))]
public class CustomAvatarSettingsEditor : Editor
{
    private ReorderableList reorderableList;
    private SerializedProperty parametersProp;
    private System.Collections.Generic.List<bool> foldouts = new();

    private void OnEnable()
    {
        parametersProp = serializedObject.FindProperty("parameters");

        while (foldouts.Count < parametersProp.arraySize) foldouts.Add(true);
        while (foldouts.Count > parametersProp.arraySize) foldouts.RemoveAt(foldouts.Count - 1);

        reorderableList = new ReorderableList(serializedObject, parametersProp, true, true, true, true);

        reorderableList.drawHeaderCallback = (Rect rect) => {
            EditorGUI.LabelField(rect, "Save Entries");
        };

        reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            SerializedProperty paramProp = parametersProp.GetArrayElementAtIndex(index);
            float y = rect.y + 2;
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = 2;

            float foldoutOffset = 24f;
            var foldRect = new Rect(rect.x + foldoutOffset, y, 16, lineHeight);
            var labelRect = new Rect(rect.x + foldoutOffset + 18, y, rect.width - (foldoutOffset + 18), lineHeight);
            if (foldouts.Count <= index) foldouts.Add(true);
            foldouts[index] = EditorGUI.Foldout(foldRect, foldouts[index], GUIContent.none, true);

            SerializedProperty label = paramProp.FindPropertyRelative("label");
            SerializedProperty type = paramProp.FindPropertyRelative("type");

            if (foldouts[index])
                label.stringValue = EditorGUI.TextField(labelRect, label.stringValue);
            else
                EditorGUI.LabelField(labelRect, label.stringValue);

            y += lineHeight + spacing;

            if (foldouts[index])
            {
                type.enumValueIndex = (int)(CustomAvatarSettings.ParamType)EditorGUI.EnumPopup(
                    new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight),
                    "Type", (CustomAvatarSettings.ParamType)type.enumValueIndex
                );
                y += lineHeight + spacing;

                string[] componentTypes = GameObject.FindObjectsOfType<MonoBehaviour>(true)
                    .Select(c => c.GetType().Name).Distinct().OrderBy(x => x).ToArray();

                SerializedProperty componentType = paramProp.FindPropertyRelative("componentType");
                int compIdx = Mathf.Max(0, Array.IndexOf(componentTypes, componentType.stringValue));
                compIdx = EditorGUI.Popup(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight),
                    "Component Type", compIdx, componentTypes);
                componentType.stringValue = componentTypes.Length > 0 ? componentTypes[compIdx] : "";
                y += lineHeight + spacing;

                if ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Slider ||
                    (CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Toggle ||
                    (CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Dropdown)
                {
                    var comps = GameObject.FindObjectsOfType<MonoBehaviour>(true)
                        .Where(c => c.GetType().Name == componentType.stringValue);
                    var fields = comps.SelectMany(c => c.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                        .Where(f =>
                            ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Slider && (f.FieldType == typeof(float) || f.FieldType == typeof(int))) ||
                            ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Toggle && f.FieldType == typeof(bool)) ||
                            ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Dropdown && (f.FieldType == typeof(int) || f.FieldType == typeof(string)))
                        )
                        .Select(f => f.Name).Distinct().ToArray();
                    SerializedProperty field = paramProp.FindPropertyRelative("field");
                    int fieldIdx = Mathf.Max(0, Array.IndexOf(fields, field.stringValue));
                    fieldIdx = EditorGUI.Popup(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Field", fieldIdx, fields);
                    field.stringValue = fields.Length > 0 ? fields[fieldIdx] : "";
                    y += lineHeight + spacing;
                }
                if ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Slider)
                {
                    var min = paramProp.FindPropertyRelative("min");
                    var max = paramProp.FindPropertyRelative("max");
                    var defaultValue = paramProp.FindPropertyRelative("defaultValue");
                    var uiObject = paramProp.FindPropertyRelative("uiObject");

                    min.floatValue = EditorGUI.FloatField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Min", min.floatValue);
                    y += lineHeight + spacing;
                    max.floatValue = EditorGUI.FloatField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Max", max.floatValue);
                    y += lineHeight + spacing;
                    defaultValue.floatValue = EditorGUI.FloatField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Default Value", defaultValue.floatValue);
                    y += lineHeight + spacing;
                    uiObject.objectReferenceValue = EditorGUI.ObjectField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "UI Object", uiObject.objectReferenceValue, typeof(GameObject), true);
                    y += lineHeight + spacing;
                }
                if ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Toggle)
                {
                    var defaultToggle = paramProp.FindPropertyRelative("defaultToggle");
                    var uiObject = paramProp.FindPropertyRelative("uiObject");
                    defaultToggle.boolValue = EditorGUI.Toggle(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Default Value", defaultToggle.boolValue);
                    y += lineHeight + spacing;
                    uiObject.objectReferenceValue = EditorGUI.ObjectField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "UI Object", uiObject.objectReferenceValue, typeof(GameObject), true);
                    y += lineHeight + spacing;
                }
                if ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Dropdown)
                {
                    var options = paramProp.FindPropertyRelative("options");
                    var defaultDropdown = paramProp.FindPropertyRelative("defaultDropdown");
                    var uiObject = paramProp.FindPropertyRelative("uiObject");

                    int optionCount = Mathf.Max(1, EditorGUI.IntField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Option Count", options.arraySize > 0 ? options.arraySize : 1));
                    y += lineHeight + spacing;
                    while (options.arraySize < optionCount) options.InsertArrayElementAtIndex(options.arraySize);
                    while (options.arraySize > optionCount) options.DeleteArrayElementAtIndex(options.arraySize - 1);
                    for (int opt = 0; opt < options.arraySize; opt++)
                    {
                        var optProp = options.GetArrayElementAtIndex(opt);
                        optProp.stringValue = EditorGUI.TextField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Option " + opt, optProp.stringValue);
                        y += lineHeight + spacing;
                    }
                    defaultDropdown.intValue = EditorGUI.IntSlider(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "Default Index", defaultDropdown.intValue, 0, optionCount - 1);
                    y += lineHeight + spacing;
                    uiObject.objectReferenceValue = EditorGUI.ObjectField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "UI Object", uiObject.objectReferenceValue, typeof(GameObject), true);
                    y += lineHeight + spacing;
                }

                if ((CustomAvatarSettings.ParamType)type.enumValueIndex == CustomAvatarSettings.ParamType.Button)
                {
                    var uiObject = paramProp.FindPropertyRelative("uiObject");
                    uiObject.objectReferenceValue = EditorGUI.ObjectField(new Rect(rect.x + foldoutOffset, y, rect.width - foldoutOffset, lineHeight), "UI Object", uiObject.objectReferenceValue, typeof(GameObject), true);
                    y += lineHeight + spacing;
                }
            }
        };

        reorderableList.elementHeightCallback = (index) =>
        {
            var paramProp = parametersProp.GetArrayElementAtIndex(index);
            float h = EditorGUIUtility.singleLineHeight + 2;
            if (foldouts[index])
            {
                h += EditorGUIUtility.singleLineHeight + 2;
                h += EditorGUIUtility.singleLineHeight + 2;
                var type = (CustomAvatarSettings.ParamType)paramProp.FindPropertyRelative("type").enumValueIndex;
                if (type == CustomAvatarSettings.ParamType.Slider || type == CustomAvatarSettings.ParamType.Toggle || type == CustomAvatarSettings.ParamType.Dropdown)
                    h += EditorGUIUtility.singleLineHeight + 2;
                if (type == CustomAvatarSettings.ParamType.Slider)
                    h += 4 * (EditorGUIUtility.singleLineHeight + 2);
                if (type == CustomAvatarSettings.ParamType.Toggle)
                    h += 2 * (EditorGUIUtility.singleLineHeight + 2);
                if (type == CustomAvatarSettings.ParamType.Dropdown)
                {
                    var options = paramProp.FindPropertyRelative("options");
                    h += (Mathf.Max(1, options.arraySize) + 3) * (EditorGUIUtility.singleLineHeight + 2);
                }
                if (type == CustomAvatarSettings.ParamType.Button)
                    h += EditorGUIUtility.singleLineHeight + 2;
            }
            return h;
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        while (foldouts.Count < parametersProp.arraySize) foldouts.Add(true);
        while (foldouts.Count > parametersProp.arraySize) foldouts.RemoveAt(foldouts.Count - 1);

        reorderableList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }
}
