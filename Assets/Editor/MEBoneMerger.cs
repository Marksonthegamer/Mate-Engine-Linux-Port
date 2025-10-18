using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MateEngine
{
    public class MEBoneMerger : EditorWindow
    {
        private GameObject mainArmature;
        private List<GameObject> clothingArmatures = new List<GameObject>();
        private Vector2 scroll;

        [MenuItem("MateEngine/ME Bone Merger")]
        public static void ShowWindow()
        {
            GetWindow<MEBoneMerger>("ME Bone Merger");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Main Avatar Armature (Root)", EditorStyles.boldLabel);
            mainArmature = (GameObject)EditorGUILayout.ObjectField(mainArmature, typeof(GameObject), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Clothing Armatures to Merge", EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < clothingArmatures.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                clothingArmatures[i] = (GameObject)EditorGUILayout.ObjectField(clothingArmatures[i], typeof(GameObject), true);
                if (GUILayout.Button("-", GUILayout.Width(20)))
                {
                    clothingArmatures.RemoveAt(i);
                    i--;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Clothing Armature"))
            {
                clothingArmatures.Add(null);
            }

            EditorGUILayout.Space();

            GUI.enabled = mainArmature != null && clothingArmatures.Count > 0;
            if (GUILayout.Button("Merge Bones"))
            {
                MergeBones();
            }
            GUI.enabled = true;
        }

        private void MergeBones()
        {
            foreach (var clothing in clothingArmatures)
            {
                if (clothing == null) continue;

                Transform[] clothingBones = clothing.GetComponentsInChildren<Transform>(true);
                Transform[] mainBones = mainArmature.GetComponentsInChildren<Transform>(true);

                foreach (var cb in clothingBones)
                {
                    Transform matching = FindMatchingBone(cb.name, mainBones);
                    if (matching != null)
                    {
                        cb.position = matching.position;
                        cb.rotation = matching.rotation;
                        cb.localScale = matching.localScale;
                        cb.SetParent(matching);
                    }
                }
            }

            Debug.Log("Bone merging completed.");
        }

        private Transform FindMatchingBone(string name, Transform[] mainBones)
        {
            foreach (var bone in mainBones)
            {
                if (bone.name == name) return bone;
            }
            return null;
        }
    }
}
