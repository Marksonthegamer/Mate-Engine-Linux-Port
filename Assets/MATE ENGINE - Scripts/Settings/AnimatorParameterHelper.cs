using UnityEngine;

public static class AnimatorParameterHelper
{
    public static bool IsAnyAnimatorBoolTrue(string parameterName)
    {
        foreach (var anim in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (anim.runtimeAnimatorController == null) continue;

            foreach (var param in anim.parameters)
            {
                if (param.name == parameterName && param.type == AnimatorControllerParameterType.Bool)
                {
                    if (anim.GetBool(parameterName))
                        return true;
                }
            }
        }
        return false;
    }
}
