using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MateEngine.EditorKit
{
    [InitializeOnLoad]
    public static class MEEditorIcons
    {
        const string ScriptsRoot = "Assets/MATE ENGINE - Scripts";
        const string IconPath = "Assets/MATE ENGINE - Icons/ComponentIcon.png";
        const string Pref_Auto = "ME_AutoApplyMateEngineIcons";

        static MEEditorIcons()
        {
            if (AutoApply) EditorApplication.update += TryAutoApplyOnce;
        }

        static bool _didAuto;
        static void TryAutoApplyOnce()
        {
            if (_didAuto) return;
            _didAuto = true;
            EditorApplication.update -= TryAutoApplyOnce;
            ApplyIcons();
        }

        public static bool AutoApply
        {
            get => EditorPrefs.GetBool(Pref_Auto, true);
            set => EditorPrefs.SetBool(Pref_Auto, value);
        }

        [MenuItem("MateEngine/Icons/Apply to MateEngine Scripts")]
        public static void ApplyIcons()
        {
            var icon = LoadIcon();
            if (icon == null) return;

            var scripts = FindScriptsUnder(ScriptsRoot);
            int count = 0;

            foreach (var ms in scripts)
            {
                if (ms == null) continue;
                var path = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(ScriptsRoot)) continue;
                EditorGUIUtility.SetIconForObject(ms, icon);
                EditorUtility.SetDirty(ms);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("MateEngine/Icons/Clear on MateEngine Scripts")]
        public static void ClearIcons()
        {
            var scripts = FindScriptsUnder(ScriptsRoot);
            int count = 0;

            foreach (var ms in scripts)
            {
                if (ms == null) continue;
                var path = AssetDatabase.GetAssetPath(ms);
                if (string.IsNullOrEmpty(path) || !path.StartsWith(ScriptsRoot)) continue;
                EditorGUIUtility.SetIconForObject(ms, null);
                EditorUtility.SetDirty(ms);
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("MateEngine/Icons/Auto-Apply Enable", true)]
        static bool ValidateOn() => !AutoApply;
        [MenuItem("MateEngine/Icons/Auto-Apply Enable")]
        static void TurnOnAuto() => AutoApply = true;

        [MenuItem("MateEngine/Icons/Auto-Apply Disable", true)]
        static bool ValidateOff() => AutoApply;
        [MenuItem("MateEngine/Icons/Auto-Apply Disable")]
        static void TurnOffAuto() => AutoApply = false;

        static Texture2D LoadIcon()
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        static MonoScript[] FindScriptsUnder(string root)
        {
            if (string.IsNullOrEmpty(root)) return new MonoScript[0];
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { root });
            return guids.Select(g => AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g)))
                        .Where(s => s != null)
                        .ToArray();
        }
    }

    public class MEIconsPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!MEEditorIcons.AutoApply) return;

            bool affected = false;
            foreach (var p in imported.Concat(moved))
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (p.StartsWith("Assets/MATE ENGINE - Scripts") || p == "Assets/MATE ENGINE - Icons/ComponentIcon.png")
                {
                    affected = true; break;
                }
            }
            if (affected) MEEditorIcons.ApplyIcons();
        }
    }
}
