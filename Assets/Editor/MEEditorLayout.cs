using UnityEditor;
using UnityEngine;
using System;

namespace MateEngine.EditorKit
{
    [InitializeOnLoad]
    public static class MEEditorLayout
    {
        const string PrefKey_OnOffBools = "ME_UseOnOffButtonsForBools";

        public static bool UseOnOffButtonsForBools
        {
            get => EditorPrefs.GetBool(PrefKey_OnOffBools, true);
            set => EditorPrefs.SetBool(PrefKey_OnOffBools, value);
        }

        static MEEditorLayout()
        {
            if (!EditorPrefs.HasKey(PrefKey_OnOffBools))
                EditorPrefs.SetBool(PrefKey_OnOffBools, true);
        }

        [MenuItem("MateEngine/On-Off Buttons für Bools/Enable")]
        static void EnableOnOff() => UseOnOffButtonsForBools = true;

        [MenuItem("MateEngine/On-Off Buttons für Bools/Disable")]
        static void DisableOnOff() => UseOnOffButtonsForBools = false;

        public static bool OnOffButton(Rect rect, bool value)
        {
            var txt = value ? "On" : "Off";
            var prev = GUI.color;
            if (value) GUI.color = new Color(1f, 0.5686f, 0.9843f); // Color
            bool result = GUI.Toggle(rect, value, txt, EditorStyles.miniButton);
            GUI.color = prev;
            return result;
        }

        public static bool OnOffButton(string label, bool value, float buttonWidth = 72f)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label);
            GUILayout.FlexibleSpace();
            var r = GUILayoutUtility.GetRect(buttonWidth, EditorGUIUtility.singleLineHeight, GUILayout.Width(buttonWidth));
            bool v = OnOffButton(r, value);
            EditorGUILayout.EndHorizontal();
            return v;
        }

        public static void LabeledText(string label, SerializedProperty prop, float minFieldWidth = 100f)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(label);
            prop.stringValue = EditorGUILayout.TextField(prop.stringValue, GUILayout.MinWidth(minFieldWidth));
            EditorGUILayout.EndHorizontal();
        }

        public static void Row(Action left, Action right, float gutter = 8f)
        {
            EditorGUILayout.BeginHorizontal();
            left?.Invoke();
            GUILayout.FlexibleSpace();
            GUILayout.Space(gutter);
            right?.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        public static void ThreeCols(Action c1, Action c2, Action c3, float w1 = 0.5f, float w2 = 0.35f, float w3 = 0.15f, float gutter = 8f)
        {
            float total = EditorGUIUtility.currentViewWidth - 48f;
            float cw1 = Mathf.Floor(total * Mathf.Clamp01(w1));
            float cw2 = Mathf.Floor(total * Mathf.Clamp01(w2));
            float cw3 = Mathf.Max(60f, total - cw1 - cw2 - 2 * gutter);

            EditorGUILayout.BeginHorizontal();
            BeginWidth(cw1); c1?.Invoke(); EndWidth();
            GUILayout.Space(gutter);
            BeginWidth(cw2); c2?.Invoke(); EndWidth();
            GUILayout.Space(gutter);
            BeginWidth(cw3); c3?.Invoke(); EndWidth();
            EditorGUILayout.EndHorizontal();
        }

        public static void BeginWidth(float w) => EditorGUILayout.BeginHorizontal(GUILayout.Width(w));
        public static void EndWidth() => EditorGUILayout.EndHorizontal();
    }

    [CustomPropertyDrawer(typeof(bool))]
    public class ME_GlobalBoolDrawer : PropertyDrawer
    {
        const float BtnW = 72f;
        const float Gap = 6f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!MEEditorLayout.UseOnOffButtonsForBools)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            float h = EditorGUIUtility.singleLineHeight;
            Rect btnRect = new Rect(position.x + position.width - BtnW, position.y, BtnW, h);
            Rect labelRect = new Rect(position.x, position.y, Mathf.Max(0f, btnRect.x - position.x - Gap), h);

            EditorGUI.LabelField(labelRect, label);

            var prev = GUI.color;
            if (property.boolValue) GUI.color = new Color(1f, 0.5686f, 0.9843f); // Color
            bool v = GUI.Toggle(btnRect, property.boolValue, property.boolValue ? "On" : "Off", EditorStyles.miniButton);
            GUI.color = prev;

            if (v != property.boolValue) property.boolValue = v;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight + 2f;
        }
    }

    public class ME_TwoLineBlock
    {
        public struct RowConfig { public float col1; public float col2; public float col3; public float gutter; }
        public static RowConfig DefaultRow => new RowConfig { col1 = 0.55f, col2 = 0.35f, col3 = 0.10f, gutter = 8f };

        public static void Draw(
            string topLabel, Action topField,
            string l1, Action f1,
            string l2, Action f2,
            string l3, Action f3,
            RowConfig cfg)
        {
            float h = EditorGUIUtility.currentViewWidth - 48f;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(topLabel, GUILayout.Width(Mathf.Clamp(EditorGUIUtility.currentViewWidth * 0.18f, 60f, 160f)));
            topField?.Invoke();
            EditorGUILayout.EndHorizontal();

            float total = EditorGUIUtility.currentViewWidth - 48f;
            float c1 = Mathf.Floor(total * cfg.col1);
            float c2 = Mathf.Floor(total * cfg.col2);
            float c3 = Mathf.Max(60f, total - c1 - c2 - 2 * cfg.gutter);

            EditorGUILayout.BeginHorizontal();

            MEEditorLayout.BeginWidth(c1);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(l1, GUILayout.Width(80f));
            f1?.Invoke();
            EditorGUILayout.EndHorizontal();
            MEEditorLayout.EndWidth();

            GUILayout.Space(cfg.gutter);

            MEEditorLayout.BeginWidth(c2);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(l2, GUILayout.Width(80f));
            f2?.Invoke();
            EditorGUILayout.EndHorizontal();
            MEEditorLayout.EndWidth();

            GUILayout.Space(cfg.gutter);

            MEEditorLayout.BeginWidth(c3);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(l3, GUILayout.Width(80f));
            f3?.Invoke();
            EditorGUILayout.EndHorizontal();
            MEEditorLayout.EndWidth();

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(2f);
        }
    }
}
