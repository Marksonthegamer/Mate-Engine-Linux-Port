using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

public class MEManipulator : MonoBehaviour
{
    [Serializable]
    public class ReplacementEntry
    {
        public GameObject sourceObject;
    }

    public List<ReplacementEntry> replacements = new();

    private HashSet<Animator> patchedAnimators = new HashSet<Animator>();

    // Dummy fallback (will be reused for all empty slots)
    private static AnimationClip _dummyClip;
    private static AnimationClip DummyClip
    {
        get
        {
            if (_dummyClip == null)
            {
                _dummyClip = new AnimationClip();
                _dummyClip.name = "EmptyFallback";
            }
            return _dummyClip;
        }
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        var receivers = GameObject.FindObjectsByType<AvatarAnimatorReceiver>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var receiver in receivers)
        {
            if (receiver == null || receiver.avatarAnimator == null)
                continue;

            var animator = receiver.avatarAnimator;
            if (patchedAnimators.Contains(animator))
                continue; // Already patched

            ApplyAllReplacementsToAnimator(animator);
            patchedAnimators.Add(animator);
        }
    }

    public void ApplyAllReplacementsToAnimator(Animator animator)
    {
        GameObject targetRoot = animator.gameObject;

        foreach (var entry in replacements)
        {
            if (!entry.sourceObject) continue;
            var overrideAnimator = entry.sourceObject.GetComponent<Animator>();
            if (overrideAnimator != null && overrideAnimator.runtimeAnimatorController != null)
            {
                RuntimeAnimatorController ctrl = overrideAnimator.runtimeAnimatorController;
                AnimatorOverrideController overrideCtrl;

                // Always wrap in override controller so we can set slots
                if (ctrl is AnimatorOverrideController alreadyOverride)
                    overrideCtrl = alreadyOverride;
                else
                    overrideCtrl = new AnimatorOverrideController(ctrl);

                // Ensure HoverReaction and HoverFace are NEVER empty
                EnsureClipNotEmpty(overrideCtrl, "HoverReaction");
                EnsureClipNotEmpty(overrideCtrl, "HoverFace");

                animator.runtimeAnimatorController = overrideCtrl;

                // Patch PetVoiceReactionHandler if present
                var voiceHandler = animator.GetComponent<PetVoiceReactionHandler>();
                if (voiceHandler != null)
                {
                    var overrideControllerField = typeof(PetVoiceReactionHandler)
                        .GetField("overrideController", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (overrideControllerField != null)
                        overrideControllerField.SetValue(voiceHandler, overrideCtrl);

                    var lastControllerField = typeof(PetVoiceReactionHandler)
                        .GetField("lastController", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (lastControllerField != null)
                        lastControllerField.SetValue(voiceHandler, overrideCtrl.runtimeAnimatorController);

                    var hasSetupField = typeof(PetVoiceReactionHandler)
                        .GetField("hasSetup", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (hasSetupField != null)
                        hasSetupField.SetValue(voiceHandler, false);
                }
            }

            var overrideComponents = entry.sourceObject.GetComponents<MonoBehaviour>();
            foreach (var overrideComp in overrideComponents)
            {
                if (overrideComp == null) continue;
                Type t = overrideComp.GetType();
                CopyFieldsAndProperties(targetRoot, t, overrideComp);

                if (overrideComp is Behaviour b)
                    b.enabled = false;
            }
            entry.sourceObject.SetActive(false);
        }
    }

    void EnsureClipNotEmpty(AnimatorOverrideController ctrl, string slot)
    {
        // Get all override pairs (OriginalClip, OverrideClip)
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        ctrl.GetOverrides(overrides);

        bool found = false;
        for (int i = 0; i < overrides.Count; i++)
        {
            if (overrides[i].Key != null && overrides[i].Key.name == slot)
            {
                found = true;
                // If the override is null, fill it with DummyClip
                if (overrides[i].Value == null)
                {
                    ctrl[overrides[i].Key] = DummyClip;
                }
                break;
            }
        }

        // If not found, try to set the slot by name anyway (Unity will create it if the slot exists in the controller)
        if (!found)
        {
            try
            {
                ctrl[slot] = DummyClip;
            }
            catch { }
        }
    }


    void CopyFieldsAndProperties(GameObject targetRoot, Type type, MonoBehaviour source)
    {
        var target = targetRoot.GetComponent(type);
        if (!target)
            target = targetRoot.AddComponent(type);

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var f in fields)
        {
            if (f.IsNotSerialized || f.Name == "enabled") continue;
            try
            {
                object value = f.GetValue(source);
                if (IsEmpty(value)) continue;
                f.SetValue(target, value);
            }
            catch { }
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var p in properties)
        {
            if (!p.CanWrite || !p.CanRead || p.Name == "name" || p.Name == "tag" || p.Name == "enabled") continue;
            try
            {
                object value = p.GetValue(source, null);
                if (IsEmpty(value)) continue;
                p.SetValue(target, value, null);
            }
            catch { }
        }
    }

    bool IsEmpty(object value)
    {
        if (value == null) return true;

        Type type = value.GetType();

        if (type == typeof(string)) return string.IsNullOrWhiteSpace((string)value);
        if (type.IsValueType)
        {
            object defaultValue = Activator.CreateInstance(type);
            return value.Equals(defaultValue);
        }

        if (value is UnityEngine.Object unityObj)
            return unityObj == null;

        return false;
    }

    public void ResetPatchedAnimators()
    {
        patchedAnimators.Clear();
    }
}
