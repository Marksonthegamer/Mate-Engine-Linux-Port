using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace UMotionEditor
{
    public static class GUICompatibilityUtility
    {
        //********************************************************************************
        // Public Properties
        //********************************************************************************

        public static event System.Action<SceneView> OnSceneGui
        {
            add
            {
                #if UNITY_2019_1_OR_NEWER
                SceneView.duringSceneGui += value;
                #else
                legacySceneViewGUI += value;
                if (!initialized)
                {
                    // Doing this in a static constructor instead caused an exception in Unity 2017.4
                    SceneView.onSceneGUIDelegate += delegate(SceneView sceneView) { legacySceneViewGUI(sceneView); };
                    initialized = true;
                }
                #endif
            }
            remove
            {
                #if UNITY_2019_1_OR_NEWER
                SceneView.duringSceneGui -= value;
                #else
                legacySceneViewGUI -= value;
                #endif
            }
        }

        //********************************************************************************
        // Private Properties
        //********************************************************************************

        //----------------------
        // Inspector
        //----------------------

        //----------------------
        // Internal
        //----------------------
        #if !UNITY_2019_1_OR_NEWER
        private static event System.Action<SceneView> legacySceneViewGUI;
        private static bool initialized = false;
        #endif

        //********************************************************************************
        // Public Methods
        //********************************************************************************

        [MenuItem("Window/UMotion Editor/Contact Support", true, 1232)]
        public static bool UMotionSupportMenuItemValidate()
        {
            CheckCurrentAssembly();
            return true;
        }

        [MenuItem("Window/UMotion Editor/Contact Support", false, 1232)]
        public static void UMotionSupportMenuItem()
        {
            Help.BrowseURL("https://support.soxware.com");
        }

        public static Color ColorField(GUIContent label, Color value, bool showEyedropper, bool showAlpha, bool hdr, params GUILayoutOption[] options)
        {
            #if UNITY_2018_1_OR_NEWER
            return EditorGUILayout.ColorField(label, value, showEyedropper, showAlpha, hdr, options);
            #else
            return EditorGUILayout.ColorField(label, value, showEyedropper, showAlpha, hdr, null, options);
            #endif
        }

        //********************************************************************************
        // Private Methods
        //********************************************************************************

        private static bool CheckCurrentAssembly()
        {
            string applicationAssemblyName = VersionCompatibilityUtility.GetCurrentAssemblyName();
            string editorAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            
            bool assemblyOk = (applicationAssemblyName == "UMotionSourceApplication") && (editorAssemblyName == "UMotionSourceEditor");

            if (!assemblyOk)
            {
                string message = string.Format("The UMotion script files are not compiled to the correct assembly:\r\n\r\n\"{0}\"\r\n(should be \"UMotionSourceApplication\")\r\n\r\n\"{1}\"\r\n(should be \"UMotionSourceEditor\")\r\n\r\nMake sure that you haven't deleted or re-named the assembly definition files inside the UMotion folder.", applicationAssemblyName, editorAssemblyName);
                EditorUtility.DisplayDialog("UMotion - Invalid Assembly", message, "OK");
            }

            return assemblyOk;
        }
    }
}