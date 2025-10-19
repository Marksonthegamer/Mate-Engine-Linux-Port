using System;
using System.Collections.Generic;
using UnityEngine;

public class AvatarParticleHandler : MonoBehaviour
{
    [Serializable]
    public class ParticleRule
    {
        public string stateOrParameterName;
        public bool useParameter;
        public HumanBodyBones targetBone;
        public List<GameObject> linkedObjects = new();
    }

    public Animator animator;
    public List<ParticleRule> rules = new();
    public bool featureEnabled = true;

    private struct RuleCache
    {
        public Transform bone;
        public GameObject[] objects;
        public int paramIndex;
        public bool useParameter;
        public string stateName;
    }

    private RuleCache[] cache = Array.Empty<RuleCache>();
    private AnimatorControllerParameter[] animParams;

    void Start()
    {
        animator ??= GetComponent<Animator>();
        animParams = animator.parameters;
        var tmp = new List<RuleCache>(rules.Count);
        foreach (var rule in rules)
        {
            var bone = animator.GetBoneTransform(rule.targetBone);
            if (!bone) continue;
            var objs = rule.linkedObjects.FindAll(o => o != null);
            foreach (var o in objs) o.SetActive(false);
            int idx = -1;
            if (rule.useParameter)
                for (int i = 0; i < animParams.Length; i++)
                    if (animParams[i].type == AnimatorControllerParameterType.Bool &&
                        animParams[i].name == rule.stateOrParameterName) { idx = i; break; }
            tmp.Add(new RuleCache
            {
                bone = bone,
                objects = objs.ToArray(),
                paramIndex = idx,
                useParameter = rule.useParameter,
                stateName = rule.stateOrParameterName
            });
        }
        cache = tmp.ToArray();
    }

    void Update()
    {
        if (!featureEnabled || !animator) return;
        var state = animator.GetCurrentAnimatorStateInfo(0);
        for (int i = 0; i < cache.Length; i++)
        {
            var r = cache[i];
            bool active = r.useParameter && r.paramIndex >= 0
                ? animator.GetBool(animParams[r.paramIndex].name)
                : state.IsName(r.stateName);
            var bone = r.bone;
            var arr = r.objects;
            for (int j = 0; j < arr.Length; j++)
            {
                var o = arr[j];
                if (!o) continue;
                o.SetActive(active);
                if (active) { o.transform.SetPositionAndRotation(bone.position, bone.rotation); }
            }
        }
    }
}
