using UnityEngine;
using UnityEditor;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.SceneManagement;
#else
#if UNITY_2018_3_OR_NEWER
using UnityEditor.Experimental.SceneManagement;
#endif
#endif

namespace UMotionEditor
{
	public static class EditorVersionCompatibilityUtility
	{
		//********************************************************************************
		// Public Properties
		//********************************************************************************

		public static float HandlesLineThickness
        {
			get
            {
#if UNITY_2020_2_OR_NEWER
				return Handles.lineThickness;
#else
				return 1f;
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

		//********************************************************************************
		// Public Methods
		//********************************************************************************

        public static bool IsModelPrefab(GameObject gameObject)
        {
#if UNITY_2018_3_OR_NEWER
            return (PrefabUtility.GetPrefabAssetType(gameObject) == PrefabAssetType.Model);
#else
            return (PrefabUtility.GetPrefabType(gameObject) == PrefabType.ModelPrefab);
#endif
        }

        public static bool IsPrefab(GameObject gameObject)
        {
#if UNITY_2018_3_OR_NEWER
            return (PrefabUtility.GetPrefabAssetType(gameObject) != PrefabAssetType.NotAPrefab);
#else
            return (PrefabUtility.GetPrefabType(gameObject) != PrefabType.None);
#endif
        }

		public static bool IsInPrefabStage()
		{
#if UNITY_2018_3_OR_NEWER
			return (PrefabStageUtility.GetCurrentPrefabStage() != null);
#else
			return false;
#endif
		}

		public static void DrawWireArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius, float thickness)
        {
#if UNITY_2020_2_OR_NEWER
			Handles.DrawWireArc(center, normal, from, angle, radius, thickness);
#else
			Handles.DrawWireArc(center, normal, from, angle, radius);
#endif
		}

		//********************************************************************************
		// Private Methods
		//********************************************************************************

	}
}
