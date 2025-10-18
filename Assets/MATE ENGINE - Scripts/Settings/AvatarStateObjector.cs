using UnityEngine;
using System;
using System.Collections.Generic;

public class AvatarStateObjector : MonoBehaviour
{
    [Header("Avatar State Objector Rules")]
    public List<ObjectorRule> objectorRules = new List<ObjectorRule>();

    [Serializable]
    public class ObjectorRule
    {
        public string stateName;
        public GameObject targetObject;
        [Range(0f, 1f)] public float spawnAnimationSpeed = 0.1f;
        [NonSerialized] public Vector3 originalScale;
        [NonSerialized] public float currentLerp;
        [NonSerialized] public bool wasActive;
        [NonSerialized] public bool initialized;
    }

    private Animator cachedAnimator;
    private AvatarAnimatorController cachedAvatar;
    private GameObject currentModel;
    private Transform modelRoot;

    void Start()
    {
        var modelRootGO = GameObject.Find("Model");
        modelRoot = modelRootGO != null ? modelRootGO.transform : null;
        RebindToActiveModel();
    }

    void Update()
    {
        if (modelRoot == null)
        {
            var modelRootGO = GameObject.Find("Model");
            modelRoot = modelRootGO != null ? modelRootGO.transform : null;
            return;
        }

        GameObject activeModel = null;
        for (int i = 0; i < modelRoot.childCount; i++)
        {
            var child = modelRoot.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                activeModel = child.gameObject;
                break;
            }
        }

        if (activeModel != currentModel)
        {
            currentModel = activeModel;
            RebindToActiveModel();
        }

        if (cachedAnimator == null) return;

        for (int i = 0; i < objectorRules.Count; i++)
        {
            var rule = objectorRules[i];
            if (rule.targetObject == null) continue;

            if (!rule.initialized)
            {
                if (rule.originalScale == Vector3.zero)
                    rule.originalScale = rule.targetObject.transform.localScale;

                rule.targetObject.transform.localScale = Vector3.zero;
                rule.targetObject.SetActive(false);
                rule.wasActive = false;
                rule.currentLerp = 0f;
                rule.initialized = true;
            }

            bool shouldBeActive = false;

            if (cachedAnimator.HasParameter(rule.stateName, AnimatorControllerParameterType.Bool))
                shouldBeActive = cachedAnimator.GetBool(rule.stateName);
            else
            {
                var stateInfo = cachedAnimator.GetCurrentAnimatorStateInfo(0);
                if (!cachedAnimator.IsInTransition(0) && stateInfo.IsName(rule.stateName))
                    shouldBeActive = true;
            }

            float target = shouldBeActive ? 1f : 0f;
            float speed = Mathf.Lerp(10f, 0.25f, rule.spawnAnimationSpeed);
            rule.currentLerp = Mathf.MoveTowards(rule.currentLerp, target, Time.unscaledDeltaTime * speed);

            if (!rule.wasActive && rule.currentLerp > 0f)
            {
                rule.targetObject.SetActive(true);
                rule.wasActive = true;
            }

            rule.targetObject.transform.localScale = Vector3.Lerp(Vector3.zero, rule.originalScale, rule.currentLerp);

            if (rule.wasActive && rule.currentLerp <= 0f)
            {
                rule.targetObject.SetActive(false);
                rule.wasActive = false;
            }
        }
    }

    private void RebindToActiveModel()
    {
        cachedAnimator = null;
        cachedAvatar = null;

        if (currentModel == null) return;

        cachedAvatar = currentModel.GetComponent<AvatarAnimatorController>();
        if (cachedAvatar != null)
            cachedAnimator = cachedAvatar.GetComponent<Animator>();

        for (int i = 0; i < objectorRules.Count; i++)
            objectorRules[i].initialized = false;
    }
}

public static class AnimatorExtensions
{
    public static bool HasParameter(this Animator animator, string name, AnimatorControllerParameterType type)
    {
        if (animator == null) return false;
        var parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == name && parameters[i].type == type)
                return true;
        }
        return false;
    }
}
