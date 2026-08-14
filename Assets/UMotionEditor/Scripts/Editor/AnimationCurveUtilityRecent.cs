using UnityEngine;

namespace UMotionEditor
{
	public static class AnimationCurveUtilityRecent
	{
		//********************************************************************************
		// Public Properties
		//********************************************************************************

        public static bool WeightedTangentsImplemented
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

        public static void SetKeyWeightedMode(ref Keyframe key, int weightedMode)
        {
            #if UNITY_2018_1_OR_NEWER
            key.weightedMode = (WeightedMode)weightedMode;
            #endif
        }

        public static int GetKeyWeightedMode(Keyframe key)
        {
            #if UNITY_2018_1_OR_NEWER
            return (int)key.weightedMode;
            #else
            return 0;
            #endif
        }

        public static void SetKeyLeftWeight(ref Keyframe key, float weight)
        {
            #if UNITY_2018_1_OR_NEWER
            key.inWeight = weight;
            #endif
        }

        public static float GetKeyLeftWeight(Keyframe key)
        {
            #if UNITY_2018_1_OR_NEWER
            return key.inWeight;
            #else
            return 1f / 3f;
            #endif
        }

        public static void SetKeyRightWeight(ref Keyframe key, float weight)
        {
            #if UNITY_2018_1_OR_NEWER
            key.outWeight = weight;
            #endif
        }        

        public static float GetKeyRightWeight(Keyframe key)
        {
            #if UNITY_2018_1_OR_NEWER
            return key.outWeight;
            #else
            return 1f / 3f;
            #endif
        }

        public static void InitializeKeyframe(int frame, float value, float inTangent, float outTangent,  int weightedMode, float leftWeight, float rightWeight, out Keyframe key)
        {
            key = new Keyframe(frame, value, inTangent, outTangent);

            #if UNITY_2018_1_OR_NEWER
            key.weightedMode = (WeightedMode)weightedMode;
            key.inWeight = leftWeight;
            key.outWeight = rightWeight;
            #endif
        }

        //********************************************************************************
        // Private Methods
        //********************************************************************************
    }
}
