using UnityEngine;
using System;
using System.Collections.Generic;
using LLMUnity;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class KeyBindHandler : MonoBehaviour
{
    [Serializable]
    public class KeyBindEntry
    {
        public GameObject target;
        public KeyCode key = KeyCode.None;
        public AudioSource toggleOnSound;
        public AudioSource toggleOffSound;
        public bool unloadLLMOnToggleOff = false;
        public bool waitForKey = false;
    }

    public List<KeyBindEntry> keyBinds = new List<KeyBindEntry>();

    private void Update()
    {
        foreach (var bind in keyBinds)
        {
            if (bind.waitForKey)
            {
                foreach (KeyCode code in Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(code))
                    {
                        bind.key = code;
                        bind.waitForKey = false;
                        Debug.Log($"Key bound: {code}");
                        break;
                    }
                }
            }
            else if (Application.isPlaying && bind.target != null && bind.key != KeyCode.None && Input.GetKeyDown(bind.key))
            {
                bool newState = !bind.target.activeSelf;
                bind.target.SetActive(newState);

                if (newState && bind.toggleOnSound != null)
                    bind.toggleOnSound.Play();
                else if (!newState)
                {
                    if (bind.toggleOffSound != null)
                        bind.toggleOffSound.Play();

                    if (bind.unloadLLMOnToggleOff)
                    {
                        // FindObjectOfType<LLM>()?.Unload(); // Update in MateEngine 1.8.0 Or Later
                    }
                }
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(KeyBindHandler))]
public class KeyBindHandlerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var handler = (KeyBindHandler)target;

        EditorGUILayout.LabelField("Key Binds", EditorStyles.boldLabel);

        for (int i = 0; i < handler.keyBinds.Count; i++)
        {
            var entry = handler.keyBinds[i];
            EditorGUILayout.BeginVertical("box");

            entry.target = (GameObject)EditorGUILayout.ObjectField("Target Object", entry.target, typeof(GameObject), true);
            entry.toggleOnSound = (AudioSource)EditorGUILayout.ObjectField("Toggle ON Sound", entry.toggleOnSound, typeof(AudioSource), true);
            entry.toggleOffSound = (AudioSource)EditorGUILayout.ObjectField("Toggle OFF Sound", entry.toggleOffSound, typeof(AudioSource), true);
            entry.unloadLLMOnToggleOff = EditorGUILayout.Toggle("Unload LLM on Toggle OFF", entry.unloadLLMOnToggleOff);

            EditorGUILayout.BeginHorizontal();
            entry.key = (KeyCode)EditorGUILayout.EnumPopup("Key", entry.key);
            if (GUILayout.Button("Detect", GUILayout.Width(60)))
            {
                entry.waitForKey = true;
                EditorUtility.SetDirty(handler);
            }
            EditorGUILayout.EndHorizontal();

            if (entry.waitForKey)
            {
                EditorGUILayout.HelpBox("Press any key in Play Mode...", MessageType.Info);
            }

            if (GUILayout.Button("Remove"))
            {
                handler.keyBinds.RemoveAt(i);
                break;
            }

            EditorGUILayout.EndVertical();
        }

        GUILayout.Space(5);
        if (GUILayout.Button("+ Add New Key Bind"))
        {
            handler.keyBinds.Add(new KeyBindHandler.KeyBindEntry());
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(handler);
        }
    }
}
#endif
