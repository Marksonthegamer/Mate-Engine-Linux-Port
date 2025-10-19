using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Animator))]
public class IKFix : MonoBehaviour
{
    [Serializable]
    public class IKFixState
    {
        public string stateName;
        public bool fixFeetIK;
    }

    [Header("IK Master Toggle")]
    public bool enableIK = true;

    [Header("IK Fix States")]
    public List<IKFixState> ikFixStates = new List<IKFixState>();

    [Header("Blend Speed")]
    public float blendSpeed = 5f;

    private Animator animator;
    private float currentIKWeight;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isActiveAndEnabled)
            return;

        float targetWeight = 0f;

        if (enableIK)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layerIndex);
            foreach (var state in ikFixStates)
            {
                if (state.fixFeetIK && stateInfo.IsName(state.stateName))
                {
                    targetWeight = 1f;
                    break;
                }
            }
        }

        // Always blend toward the target weight
        currentIKWeight = Mathf.MoveTowards(currentIKWeight, targetWeight, Time.deltaTime * blendSpeed);

        if (currentIKWeight > 0f)
        {
            ApplyFootIK(AvatarIKGoal.LeftFoot, currentIKWeight);
            ApplyFootIK(AvatarIKGoal.RightFoot, currentIKWeight);
        }
        else
        {
            // Ensure IK is cleanly disabled if fully blended out
            animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, 0f);
            animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, 0f);
        }
    }

    private void ApplyFootIK(AvatarIKGoal foot, float weight)
    {
        animator.SetIKPositionWeight(foot, weight);
        animator.SetIKRotationWeight(foot, weight);
        animator.SetIKPosition(foot, animator.GetIKPosition(foot));
        animator.SetIKRotation(foot, animator.GetIKRotation(foot));
    }
}
