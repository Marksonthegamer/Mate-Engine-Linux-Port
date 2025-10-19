using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class BigScreenToggleSetting
{
    public string componentTypeName;
    public bool disableInBigScreen;
}

public class AvatarBigScreenToggleHandler : MonoBehaviour
{
    public List<BigScreenToggleSetting> settings = new List<BigScreenToggleSetting>();

    private AvatarBigScreenHandler bigScreenHandler;
    private Dictionary<Behaviour, bool> wasEnabledBefore = new Dictionary<Behaviour, bool>();

    void Awake()
    {
        bigScreenHandler = GetComponent<AvatarBigScreenHandler>();
    }

    void Update()
    {
        if (!bigScreenHandler) return;

        bool isBigScreenActive = false;
        var field = typeof(AvatarBigScreenHandler).GetField("isBigScreenActive", BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
            isBigScreenActive = (bool)field.GetValue(bigScreenHandler);

        var behaviours = GetComponents<Behaviour>();
        foreach (var b in behaviours)
        {
            if (b == this || b == bigScreenHandler) continue;

            bool shouldDisable = settings.Exists(s => s.componentTypeName == b.GetType().FullName && s.disableInBigScreen);

            if (isBigScreenActive && shouldDisable)
            {
                if (!wasEnabledBefore.ContainsKey(b))
                {
                    wasEnabledBefore[b] = b.enabled;
                    b.enabled = false;
                }
            }
            else if (!isBigScreenActive)
            {
                if (wasEnabledBefore.ContainsKey(b))
                {
                    b.enabled = wasEnabledBefore[b];
                }
            }
        }

        if (!isBigScreenActive)
            wasEnabledBefore.Clear();
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(AvatarBigScreenToggleHandler))]
public class AvatarBigScreenToggleHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var tgt = (AvatarBigScreenToggleHandler)target;

        var behaviours = tgt.GetComponents<Behaviour>();
        var already = new HashSet<string>();
        foreach (var b in behaviours)
        {
            if (b == tgt || b == tgt.GetComponent<AvatarBigScreenHandler>()) continue;
            already.Add(b.GetType().FullName);
            var entry = tgt.settings.Find(e => e.componentTypeName == b.GetType().FullName);
            if (entry == null)
            {
                entry = new BigScreenToggleSetting { componentTypeName = b.GetType().FullName, disableInBigScreen = false };
                tgt.settings.Add(entry);
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(b.GetType().Name, GUILayout.Width(200));
            entry.disableInBigScreen = EditorGUILayout.ToggleLeft("disable in BigScreen", entry.disableInBigScreen, GUILayout.Width(180));
            EditorGUILayout.EndHorizontal();
        }

        tgt.settings.RemoveAll(e => !already.Contains(e.componentTypeName));

        if (GUI.changed)
            EditorUtility.SetDirty(tgt);
    }
}
#endif
