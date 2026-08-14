#if UNITY_EDITOR
using System.Reflection;
using UnityEngine;

namespace UMotionEditor
{
    public static class VersionCompatibilityUtility
    {
        #if !UNITY_2017_4_OR_NEWER
        #error "This Unity version is not supported by UMotion. Please update to Unity 2017.4 or higher."
        #endif

        //********************************************************************************
        // Public Properties
        //********************************************************************************

        public enum EditorPlatform
        {
            Windows = 0,
            Mac,
            Linux,
            Invalid
        }

        public static EditorPlatform CurrentEditorPlatform
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                        return EditorPlatform.Windows;

                    case RuntimePlatform.OSXEditor:
                        return EditorPlatform.Mac;

                    case RuntimePlatform.LinuxEditor:
                        return EditorPlatform.Linux;

                    default:
                        return EditorPlatform.Invalid;
                }
            }
        }

        public static bool Unity2018_1_OrNewer
        {
            get
            {
                #if UNITY_2018_1_OR_NEWER
                return true;
                #else
                return false;
                #endif
            }
        }

        public static bool Unity2018_3_OrNewer
        {
            get
            {
                #if UNITY_2018_3_OR_NEWER
                return true;
                #else
                return false;
                #endif
            }
        }

        public static bool Unity2019_1_Or_Newer
        {
            get
            {
                #if UNITY_2019_1_OR_NEWER
                return true;
                #else
                return false;
                #endif
            }
        }

        public static bool Unity2020_2_OrNewer
        {
            get
            {
                #if UNITY_2020_2_OR_NEWER
                return true;
                #else
                return false;
                #endif
            }
        }

        public static bool Unity2021_2_OrNewer
        {
            get
            {
                #if UNITY_2021_2_OR_NEWER
                return true;
                #else
                return false;
                #endif
            }
        }

        

        public static bool Unity2022_2_OrNewer
        {
            get
            {
                #if UNITY_2022_2_OR_NEWER
                return true;
                #else
                return false;
                #endif
            }
        }

        public static bool UsesScriptableRenderPipeline
        {
            get
            {
                #if UNITY_2019_1_OR_NEWER
                return (UnityEngine.Rendering.RenderPipelineManager.currentPipeline != null);
                #else
                #if UNITY_2018_1_OR_NEWER
                return (UnityEngine.Experimental.Rendering.RenderPipelineManager.currentPipeline != null);
                #else
                return false;
                #endif
                #endif
            }
        }

        public static string GetCurrentAssemblyName()
        {
            return Assembly.GetExecutingAssembly().GetName().Name;
        }

        //********************************************************************************
        // Private Properties
        //********************************************************************************

        //********************************************************************************
        // Public Methods
        //********************************************************************************

        //********************************************************************************
        // Private Methods
        //********************************************************************************
    }
}
#endif