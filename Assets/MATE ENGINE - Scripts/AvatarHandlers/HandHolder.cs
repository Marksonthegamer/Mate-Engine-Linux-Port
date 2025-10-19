using UnityEngine;
using X11;

[RequireComponent(typeof(Animator))]
public class HandHolder : MonoBehaviour
{
    [Header("World-Space Interaction")]
    public float screenInteractionRadius = 0.2f;
    public Color screenInteractionRadiusColor = new Color(0.2f, 0.7f, 1f, 0.2f);
    public float preZoneMargin = 0.1f;
    public Color preZoneMarginColor = new Color(0.1f, 0.5f, 1f, 0.15f);

    [Header("Big Screen Scaling")]
    [Range(0f, 100f)]
    public float bigScreenRadiusScale = 100f;

    public float followSpeed = 10f;

    [Header("Hand Tracking Settings")]
    public float maxIKWeight = 1f, blendInTime = 1f, blendOutTime = 1f;
    public float maxHandDistance = 0.8f, minForwardOffset = 0.2f, verticalOffset = 0.05f;
    public float elbowHintDistance = 0.25f, elbowHintBackOffset = 0.1f, elbowHintHeightOffset = -0.05f;
    public string[] allowedStates = { "Idle", "HoverReaction" };
    public Animator avatarAnimator;
    public bool showDebugGizmos = true;
    public bool enableHandHolding = true;

    private Camera mainCam;
    private Transform leftHand, rightHand, chest, leftShoulder, rightShoulder;
    private Vector3 leftTargetPos, rightTargetPos;
    private float leftIKWeight, rightIKWeight;
    private bool leftIsActive, rightIsActive;

    void Start()
    {
        mainCam = Camera.main;
        CacheTransforms();
    }

    void CacheTransforms()
    {
        leftHand = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHand = avatarAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        chest = avatarAnimator.GetBoneTransform(HumanBodyBones.Chest) ?? avatarAnimator.GetBoneTransform(HumanBodyBones.Spine);
        leftShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightShoulder = avatarAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
    }

    public void SetAnimator(Animator newAnimator)
    {
        avatarAnimator = newAnimator;
        CacheTransforms();
    }

    void Update()
    {
        if (!enableHandHolding || !IsValid())
        {
            leftIKWeight = Mathf.MoveTowards(leftIKWeight, 0f, Time.deltaTime / blendOutTime);
            rightIKWeight = Mathf.MoveTowards(rightIKWeight, 0f, Time.deltaTime / blendOutTime);
            return;
        }

        if (MenuActions.IsHandTrackingBlocked())
        {
            leftIKWeight = Mathf.MoveTowards(leftIKWeight, 0f, Time.deltaTime / blendOutTime);
            rightIKWeight = Mathf.MoveTowards(rightIKWeight, 0f, Time.deltaTime / blendOutTime);
            return;
        }

        if (!IsInAllowedState())
        {
            leftIKWeight = Mathf.MoveTowards(leftIKWeight, 0f, Time.deltaTime / blendOutTime);
            rightIKWeight = Mathf.MoveTowards(rightIKWeight, 0f, Time.deltaTime / blendOutTime);
            return;
        }

        float leftWeight = ComputeWorldWeight(leftHand);
        float rightWeight = ComputeWorldWeight(rightHand);

        if (leftWeight > rightWeight)
        {
            leftIsActive = leftWeight > 0f;
            rightIsActive = false;
            rightWeight = 0f;
        }
        else
        {
            rightIsActive = rightWeight > 0f;
            leftIsActive = false;
            leftWeight = 0f;
        }

        leftIKWeight = Mathf.MoveTowards(leftIKWeight, leftWeight, Time.deltaTime / (leftWeight > leftIKWeight ? blendInTime : blendOutTime));
        rightIKWeight = Mathf.MoveTowards(rightIKWeight, rightWeight, Time.deltaTime / (rightWeight > rightIKWeight ? blendInTime : blendOutTime));

        Vector3 target = GetProjectedMouseTarget();
        if (leftIsActive) leftTargetPos = Vector3.Lerp(leftTargetPos, target, Time.deltaTime * followSpeed);
        if (rightIsActive) rightTargetPos = Vector3.Lerp(rightTargetPos, target, Time.deltaTime * followSpeed);
    }

    float ComputeWorldWeight(Transform hand)
    {
        if (!hand) return 0f;
        var oriMouse = X11Manager.Instance.GetMousePosition();
        var winPos = X11Manager.Instance.GetWindowPosition();
        var mouseScreen = new Vector3(oriMouse.x - winPos.x, Screen.height - oriMouse.y + winPos.y);
        mouseScreen.z = mainCam.WorldToScreenPoint(hand.position).z;
        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(mouseScreen);

        float scale = hand.lossyScale.magnitude;
        float radiusScale = 1f;

        if (avatarAnimator != null && avatarAnimator.GetBool("isBigScreen"))
            radiusScale = bigScreenRadiusScale * 0.01f;

        float mainZone = screenInteractionRadius * scale * radiusScale;
        float outerZone = (screenInteractionRadius + preZoneMargin) * scale * radiusScale;

        float dist = Vector3.Distance(hand.position, mouseWorld);

        if (dist <= mainZone) return maxIKWeight;
        if (dist >= outerZone) return 0f;
        return Mathf.Lerp(maxIKWeight, 0f, (dist - mainZone) / (outerZone - mainZone));
    }

    bool IsInAllowedState()
    {
        AnimatorStateInfo current = avatarAnimator.GetCurrentAnimatorStateInfo(0);
        for (int i = 0; i < allowedStates.Length; i++)
            if (current.IsName(allowedStates[i])) return true;
        return false;
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (avatarAnimator == null) return;

        if (!IsValid())
        {
            avatarAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            avatarAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            avatarAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            avatarAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            avatarAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            avatarAnimator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
            return;
        }

        Quaternion naturalRotation = Quaternion.LookRotation(avatarAnimator.transform.forward, avatarAnimator.transform.up);

        ApplyIK(AvatarIKGoal.LeftHand, AvatarIKHint.LeftElbow, leftIKWeight, leftTargetPos, leftShoulder, true, naturalRotation);
        ApplyIK(AvatarIKGoal.RightHand, AvatarIKHint.RightElbow, rightIKWeight, rightTargetPos, rightShoulder, false, naturalRotation);
    }

    void ApplyIK(AvatarIKGoal hand, AvatarIKHint elbow, float weight, Vector3 targetPos, Transform shoulder, bool isLeft, Quaternion rotation)
    {
        avatarAnimator.SetIKPositionWeight(hand, weight);
        avatarAnimator.SetIKRotationWeight(hand, weight);
        avatarAnimator.SetIKHintPositionWeight(elbow, weight);

        if (weight <= 0f) return;

        avatarAnimator.SetIKPosition(hand, targetPos);
        avatarAnimator.SetIKRotation(hand, rotation);
        avatarAnimator.SetIKHintPosition(elbow, GetElbowHint(shoulder, targetPos, isLeft));
    }

    Vector3 GetElbowHint(Transform shoulder, Vector3 target, bool isLeft)
    {
        Vector3 toTarget = (target - shoulder.position).normalized;
        Vector3 bendDir = Vector3.Cross(toTarget, avatarAnimator.transform.up).normalized;
        if (!isLeft) bendDir = -bendDir;

        return shoulder.position + bendDir * elbowHintDistance
            - avatarAnimator.transform.forward * elbowHintBackOffset
            + avatarAnimator.transform.up * elbowHintHeightOffset;
    }

    Vector3 GetProjectedMouseTarget()
    {
        var oriMouse = X11Manager.Instance.GetMousePosition();
        var winPos = X11Manager.Instance.GetWindowPosition();
        var mouse = new Vector3(oriMouse.x - winPos.x, Screen.height - oriMouse.y + winPos.y);
        mouse.z = mainCam.WorldToScreenPoint(chest.position).z;
        Vector3 world = mainCam.ScreenToWorldPoint(mouse);
        Vector3 local = avatarAnimator.transform.InverseTransformPoint(world);

        local.z = Mathf.Clamp(local.z, minForwardOffset, maxHandDistance);
        local.y += verticalOffset;
        return avatarAnimator.transform.TransformPoint(local);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !mainCam) return;
        DrawRadiusGizmo(leftHand, screenInteractionRadius, screenInteractionRadiusColor);
        DrawRadiusGizmo(leftHand, screenInteractionRadius + preZoneMargin, preZoneMarginColor);
        DrawRadiusGizmo(rightHand, screenInteractionRadius, screenInteractionRadiusColor);
        DrawRadiusGizmo(rightHand, screenInteractionRadius + preZoneMargin, preZoneMarginColor);
    }

    void DrawRadiusGizmo(Transform hand, float radius, Color color)
    {
        if (!hand) return;
        float scale = hand.lossyScale.magnitude;
        float radiusScale = 1f;
        if (avatarAnimator != null && avatarAnimator.GetBool("isBigScreen"))
            radiusScale = bigScreenRadiusScale * 0.01f;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(hand.position, radius * scale * radiusScale);
    }
#endif

    bool IsValid() => avatarAnimator && leftHand && rightHand && chest && leftShoulder && rightShoulder;
}
