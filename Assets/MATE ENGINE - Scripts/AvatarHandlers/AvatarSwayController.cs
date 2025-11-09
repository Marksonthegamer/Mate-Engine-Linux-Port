using System;
using UnityEngine;
using X11;

public class AvatarSwayController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public string draggingParam = "isDragging";

    [Header("Space")]
    public bool useLocalRotation = true;
    public Transform globalReference;

    [Header("Input")]
    public bool useWindowVelocity = true;
    public bool fallbackToMouse = true;
    public float mouseSensitivity = 0.6f;
    public bool invertHorizontal;
    public bool invertVertical;

    [Header("Sway Physics")]
    public float horizontalVelocityToLean = 0.25f;
    public float verticalVelocityToPitch = 0.15f;
    public float maxLeanZ = 25f;
    public float maxLeanX = 12f;
    public float springFrequency = 2.6f;
    public float dampingRatio = 0.35f;
    public float blendSpeed = 8f;

    [Header("Limb Additive")]
    [Range(0f, 1f)] public float armsAdditive;
    [Range(0f, 1f)] public float legsAdditive;
    public bool invertArms;
    public bool invertLegs;
    public float armsMaxZ = 18f;
    public float armsMaxX = 8f;
    public float legsMaxZ = 12f;
    public float legsMaxX = 6f;
    public float limbLag = 6f;

    [Header("State Whitelist")]
    public bool useAllowedStatesWhitelist;
    public string[] allowedStates = { "Drag" };
    public int stateLayerIndex;

    Animator anim;
    int draggingHash;
    Animator cachedForBones;

    Transform hips;
    Transform leftUpperArm;
    Transform rightUpperArm;
    Transform leftUpperLeg;
    Transform rightUpperLeg;

    float leanZ;
    float leanZVel;
    float leanX;
    float leanXVel;
    float effectWeight;

    float limbZ;
    float limbX;

    Vector2 filteredDelta;
    Vector2 prevMousePos;
    
    IntPtr hwnd;
    Vector2 prevWinPos;

    void Start()
    {
        draggingHash = Animator.StringToHash(draggingParam);
        prevMousePos = X11Manager.Instance.GetMousePosition();
        hwnd = X11Manager.Instance.UnityWindow;
        if (hwnd != IntPtr.Zero) prevWinPos = X11Manager.Instance.GetWindowPosition();
    }

    void Update()
    {
        EnsureAnimatorAndBones();
        if (!anim || !hips) return;

        bool dragging = anim.GetBool(draggingHash);
        bool whitelisted = IsInAllowedState();
        bool active = dragging && whitelisted;

        float dt = Time.deltaTime;
        Vector2 delta = Vector2.zero;
        
        if (useWindowVelocity && hwnd != IntPtr.Zero)
        {
            Vector2 wp = X11Manager.Instance.GetWindowPosition();
            Vector2 d = wp - prevWinPos;
            prevWinPos = wp;
            delta = new Vector2(d.x, d.y);
        }
        
        if (delta == Vector2.zero && fallbackToMouse && dragging)
        {
            Vector2 m = X11Manager.Instance.GetMousePosition();
            Vector2 md = (m - prevMousePos) * mouseSensitivity;
            prevMousePos = m;
            delta = md;
        }
        else
        {
            prevMousePos = X11Manager.Instance.GetMousePosition();
        }

        filteredDelta = Vector2.Lerp(filteredDelta, delta, 1f - Mathf.Exp(-12f * dt));

        float signH = invertHorizontal ? 1f : -1f;
        float signV = invertVertical ? -1f : 1f;

        float targetLeanZ = Mathf.Clamp(signH * filteredDelta.x * horizontalVelocityToLean, -maxLeanZ, maxLeanZ);
        float targetLeanX = Mathf.Clamp(signV * filteredDelta.y * verticalVelocityToPitch, -maxLeanX, maxLeanX);

        Spring(ref leanZ, ref leanZVel, targetLeanZ, springFrequency, dampingRatio, dt);
        Spring(ref leanX, ref leanXVel, targetLeanX, springFrequency, dampingRatio, dt);

        limbZ = Mathf.Lerp(limbZ, -leanZ, 1f - Mathf.Exp(-limbLag * dt));
        limbX = Mathf.Lerp(limbX, -leanX, 1f - Mathf.Exp(-limbLag * dt));

        effectWeight = Mathf.MoveTowards(effectWeight, active ? 1f : 0f, blendSpeed * dt);
    }

    void LateUpdate()
    {
        if (!anim || !hips || effectWeight <= 0.0001f) return;

        float xH = leanX * effectWeight;
        float zH = leanZ * effectWeight;

        if (useLocalRotation)
        {
            Quaternion baseLocal = hips.localRotation;
            Quaternion addLocal = Quaternion.Euler(xH, 0f, zH);
            hips.localRotation = baseLocal * addLocal;
        }
        else
        {
            Transform space = globalReference ? globalReference : transform;
            Quaternion baseWorld = hips.rotation;
            Quaternion addWorld = Quaternion.AngleAxis(xH, space.right) * Quaternion.AngleAxis(zH, space.forward);
            hips.rotation = addWorld * baseWorld;
        }

        if (armsAdditive > 0f)
        {
            float sA = invertArms ? -1f : 1f;
            float xA = Mathf.Clamp(limbX * armsAdditive, -armsMaxX, armsMaxX) * sA * effectWeight;
            float zA = Mathf.Clamp(limbZ * armsAdditive, -armsMaxZ, armsMaxZ) * sA * effectWeight;

            if (useLocalRotation)
            {
                if (leftUpperArm) leftUpperArm.localRotation = leftUpperArm.localRotation * Quaternion.Euler(xA, 0f, zA);
                if (rightUpperArm) rightUpperArm.localRotation = rightUpperArm.localRotation * Quaternion.Euler(xA, 0f, zA);
            }
            else
            {
                Transform space = globalReference ? globalReference : transform;
                Quaternion addWorld = Quaternion.AngleAxis(xA, space.right) * Quaternion.AngleAxis(zA, space.forward);
                if (leftUpperArm) leftUpperArm.rotation = addWorld * leftUpperArm.rotation;
                if (rightUpperArm) rightUpperArm.rotation = addWorld * rightUpperArm.rotation;
            }
        }

        if (legsAdditive > 0f)
        {
            float sL = invertLegs ? -1f : 1f;
            float xL = Mathf.Clamp(limbX * legsAdditive, -legsMaxX, legsMaxX) * sL * effectWeight;
            float zL = Mathf.Clamp(limbZ * legsAdditive, -legsMaxZ, legsMaxZ) * sL * effectWeight;

            if (useLocalRotation)
            {
                if (leftUpperLeg) leftUpperLeg.localRotation = leftUpperLeg.localRotation * Quaternion.Euler(xL, 0f, zL);
                if (rightUpperLeg) rightUpperLeg.localRotation = rightUpperLeg.localRotation * Quaternion.Euler(xL, 0f, zL);
            }
            else
            {
                Transform space = globalReference ? globalReference : transform;
                Quaternion addWorld = Quaternion.AngleAxis(xL, space.right) * Quaternion.AngleAxis(zL, space.forward);
                if (leftUpperLeg) leftUpperLeg.rotation = addWorld * leftUpperLeg.rotation;
                if (rightUpperLeg) rightUpperLeg.rotation = addWorld * rightUpperLeg.rotation;
            }
        }
    }

    void EnsureAnimatorAndBones()
    {
        if (!animator)
        {
            Animator p = GetComponentInParent<Animator>();
            if (p) animator = p;
        }
        if (anim != animator)
        {
            anim = animator;
            cachedForBones = null;
        }
        if (!anim) return;
        if (cachedForBones != anim || !hips)
        {
            hips = anim.GetBoneTransform(HumanBodyBones.Hips);
            leftUpperArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            rightUpperArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            leftUpperLeg = anim.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            rightUpperLeg = anim.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            cachedForBones = anim;
        }
    }

    bool IsInAllowedState()
    {
        if (!useAllowedStatesWhitelist) return true;
        if (!anim) return false;
        if (allowedStates == null || allowedStates.Length == 0) return true;
        AnimatorStateInfo current = anim.GetCurrentAnimatorStateInfo(Mathf.Clamp(stateLayerIndex, 0, Mathf.Max(0, anim.layerCount - 1)));
        for (int i = 0; i < allowedStates.Length; i++)
        {
            string s = allowedStates[i];
            if (!string.IsNullOrEmpty(s) && current.IsName(s)) return true;
        }
        return false;
    }

    static void Spring(ref float x, ref float v, float xt, float f, float z, float dt)
    {
        float w = Mathf.Max(0.01f, f) * 2f * Mathf.PI;
        float a = w * w * (xt - x) - 2f * z * w * v;
        v += a * dt;
        x += v * dt;
    }
}
