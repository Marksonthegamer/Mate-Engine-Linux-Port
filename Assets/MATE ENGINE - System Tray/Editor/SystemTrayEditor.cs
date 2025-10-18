using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UI;

[CustomEditor(typeof(SystemTray))]
public class SystemTrayEditor : Editor
{
    private ReorderableList list;

    void OnEnable()
    {
        list = new ReorderableList(serializedObject,
            serializedObject.FindProperty("actions"),
            true, true, true, true);

        list.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "Tray Actions");
        };

        list.elementHeightCallback = index =>
        {
            return EditorGUIUtility.singleLineHeight * 6 + 12;
        };

        list.drawElementCallback = (rect, index, active, focused) =>
        {
            var element = list.serializedProperty.GetArrayElementAtIndex(index);
            var label = element.FindPropertyRelative("label");
            var type = element.FindPropertyRelative("type");
            var handlerObject = element.FindPropertyRelative("handlerObject");
            var toggleField = element.FindPropertyRelative("toggleField");
            var methodName = element.FindPropertyRelative("methodName");

            float y = rect.y + 2;
            float h = EditorGUIUtility.singleLineHeight;
            float s = 2;

            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, h), label, new GUIContent("Label"));
            y += h + s;
            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, h), type, new GUIContent("Type"));
            y += h + s;
            EditorGUI.PropertyField(new Rect(rect.x, y, rect.width, h), handlerObject, new GUIContent("Handler Object"));
            y += h + s;

            if (handlerObject.objectReferenceValue != null)
            {
                var go = handlerObject.objectReferenceValue as GameObject;
                if (go != null)
                {
                    var actionType = (SystemTray.TrayActionType)type.enumValueIndex;

                    if (actionType == SystemTray.TrayActionType.Toggle)
                    {
                        var comp = go.GetComponent<MonoBehaviour>();
                        if (comp != null)
                        {
                            var fields = new List<string>();
                            foreach (var f in comp.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                            {
                                if (f.FieldType == typeof(Toggle)) fields.Add(f.Name);
                            }
                            int currentIdx = fields.IndexOf(toggleField.stringValue);
                            int shownIdx = Mathf.Clamp(currentIdx < 0 ? 0 : currentIdx, 0, Math.Max(fields.Count - 1, 0));
                            int newIdx = EditorGUI.Popup(new Rect(rect.x, y, rect.width, h), "Toggle Field", shownIdx, fields.ToArray());
                            if (fields.Count > 0 && newIdx != currentIdx) toggleField.stringValue = fields[newIdx];
                        }
                    }
                    else if (actionType == SystemTray.TrayActionType.Button)
                    {
                        var comp = go.GetComponent<MonoBehaviour>();
                        if (comp != null)
                        {
                            var methods = new List<string>();
                            foreach (var m in comp.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                            {
                                if (m.GetParameters().Length == 0 && m.ReturnType == typeof(void)) methods.Add(m.Name);
                            }
                            int currentIdx = methods.IndexOf(methodName.stringValue);
                            int shownIdx = Mathf.Clamp(currentIdx < 0 ? 0 : currentIdx, 0, Math.Max(methods.Count - 1, 0));
                            int newIdx = EditorGUI.Popup(new Rect(rect.x, y, rect.width, h), "Method", shownIdx, methods.ToArray());
                            if (methods.Count > 0 && newIdx != currentIdx) methodName.stringValue = methods[newIdx];
                        }
                    }
                    else if (actionType == SystemTray.TrayActionType.Method)
                    {
                        var display = new List<string>();
                        var values = new List<string>();
                        var comps = go.GetComponents<MonoBehaviour>();
                        foreach (var c in comps)
                        {
                            if (c == null) continue;
                            var t = c.GetType();
                            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                            {
                                if (m.GetParameters().Length == 0 && m.ReturnType == typeof(void))
                                {
                                    display.Add(t.Name + "." + m.Name);
                                    values.Add(m.Name);
                                }
                            }
                        }
                        int currentIdx = values.IndexOf(methodName.stringValue);
                        int shownIdx = Mathf.Clamp(currentIdx < 0 ? 0 : currentIdx, 0, Math.Max(values.Count - 1, 0));
                        int newIdx = EditorGUI.Popup(new Rect(rect.x, y, rect.width, h), "Method", shownIdx, display.ToArray());
                        if (values.Count > 0 && newIdx != currentIdx) methodName.stringValue = values[newIdx];
                    }
                }
            }
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("iconName"));
        list.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }
}
