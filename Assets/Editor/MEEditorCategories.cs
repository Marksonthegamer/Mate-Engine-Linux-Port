using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MateEngine.EditorKit
{
    static class MEHeaderStyles
    {
        static GUIStyle _title;
        static GUIStyle _box;
        public static GUIStyle Title
        {
            get
            {
                if (_title != null) return _title;
                _title = new GUIStyle(EditorStyles.boldLabel);
                _title.alignment = TextAnchor.MiddleLeft;
                _title.normal.textColor = Color.white;
                _title.fontSize = 12;
                return _title;
            }
        }
        public static GUIStyle Box
        {
            get
            {
                if (_box != null) return _box;
                _box = new GUIStyle(EditorStyles.helpBox);
                _box.padding = new RectOffset(10, 10, 8, 10);
                _box.margin = new RectOffset(4, 4, 8, 8);
                return _box;
            }
        }
        public static Color HeaderBg => new Color32(0x9c, 0x7a, 0xff, 0xff);

        public static Color HeaderBorder => EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0.08f);
    }

    [CustomPropertyDrawer(typeof(HeaderAttribute))]
    public sealed class ME_SuppressUnityHeader : DecoratorDrawer
    {
        public override void OnGUI(Rect position) { }
        public override float GetHeight() { return 0f; }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(MonoBehaviour), true, isFallback = true)]
    public class MEEditorCategories : Editor
    {
        bool _built;
        Type _type;

        public override void OnInspectorGUI()
        {
            serializedObject.UpdateIfRequiredOrScript();

            var script = serializedObject.FindProperty("m_Script");
            if (script != null)
            {
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.PropertyField(script);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.Space(4);
            }

            if (!_built) { _type = target.GetType(); _built = true; }

            var it = serializedObject.GetIterator();
            bool enterChildren = true;
            bool inGroup = false;
            string activeHeader = null;

            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyPath == "m_Script") continue;

                string root = Root(it.propertyPath);
                string header = GetHeader(root);

                if (header != null)
                {
                    if (!inGroup)
                    {
                        BeginHeader(header);
                        EditorGUILayout.BeginVertical(MEHeaderStyles.Box);
                        inGroup = true;
                        activeHeader = header;
                    }
                    else if (header != activeHeader)
                    {
                        EndGroup();
                        BeginHeader(header);
                        EditorGUILayout.BeginVertical(MEHeaderStyles.Box);
                        activeHeader = header;
                    }
                }

                EditorGUILayout.PropertyField(it, true);
            }

            if (inGroup) EndGroup();

            serializedObject.ApplyModifiedProperties();
        }

        string GetHeader(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName)) return null;
            Type t = _type;
            while (t != null && t != typeof(MonoBehaviour))
            {
                var f = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null)
                {
                    var h = f.GetCustomAttribute<HeaderAttribute>();
                    if (h != null) return h.header;
                    return null;
                }
                t = t.BaseType;
            }
            return null;
        }

        void BeginHeader(string title)
        {
            GUILayout.Space(6f);
            Rect r = GUILayoutUtility.GetRect(0f, 24f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, MEHeaderStyles.HeaderBg);
            Rect border = new Rect(r.x, r.yMax - 1f, r.width, 1f);
            EditorGUI.DrawRect(border, MEHeaderStyles.HeaderBorder);
            Rect label = new Rect(r.x + 10f, r.y, r.width - 20f, r.height);
            EditorGUI.LabelField(label, title, MEHeaderStyles.Title);
        }

        void EndGroup()
        {
            EditorGUILayout.EndVertical();
            GUILayout.Space(2f);
        }

        static string Root(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            int dot = path.IndexOf('.');
            string root = dot >= 0 ? path.Substring(0, dot) : path;
            int bracket = root.IndexOf('[');
            if (bracket >= 0) root = root.Substring(0, bracket);
            return root;
        }
    }
}
