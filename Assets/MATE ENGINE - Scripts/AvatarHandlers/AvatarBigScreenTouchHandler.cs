using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UniVRM10;
using VRM;
using X11;

public class AvatarBigScreenTouchHandler : MonoBehaviour
{
    [Header("Spring Bone Touch Settings")]
    public float mouseColliderRadius = 0.04f;

    private AvatarBigScreenHandler bigScreenHandler;
    private Animator avatarAnimator;
    private Camera mainCamera;

    private GameObject mouseColliderObj;
    private VRMSpringBoneColliderGroup mouseSpringColliderGroupVRM0;
    private VRM10SpringBoneColliderGroup mouseSpringColliderGroupVRM1;
    private VRM10SpringBoneCollider mouseSpringColliderVRM1;

    void Awake()
    {
        bigScreenHandler = GetComponent<AvatarBigScreenHandler>();
        avatarAnimator = GetComponent<Animator>();
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (bigScreenHandler == null || avatarAnimator == null || mainCamera == null)
            return;

        if (IsBigScreenActive())
        {
            if (Input.GetMouseButton(0))
            {
                HandleSpringBoneTouch();
            }
            else
            {
                CleanupMouseCollider();
            }
        }
        else
        {
            CleanupMouseCollider();
        }
    }

    bool IsBigScreenActive()
    {
        var type = bigScreenHandler.GetType();
        var field = type.GetField("isBigScreenActive", BindingFlags.NonPublic | BindingFlags.Instance);
        return field != null && (bool)field.GetValue(bigScreenHandler);
    }

    void HandleSpringBoneTouch()
    {
        if (mouseColliderObj == null)
        {
            mouseColliderObj = new GameObject("MouseSpringBoneCollider");
            mouseColliderObj.hideFlags = HideFlags.HideAndDontSave;

            // VRM0
            var vrmSpringBones = avatarAnimator.GetComponentsInChildren<VRMSpringBone>();
            if (vrmSpringBones != null && vrmSpringBones.Length > 0)
            {
                mouseSpringColliderGroupVRM0 = mouseColliderObj.AddComponent<VRMSpringBoneColliderGroup>();
                var sc = new VRMSpringBoneColliderGroup.SphereCollider
                {
                    Offset = Vector3.zero,
                    Radius = mouseColliderRadius
                };
                mouseSpringColliderGroupVRM0.Colliders = new[] { sc };

                foreach (var sb in vrmSpringBones)
                {
                    var list = sb.ColliderGroups?.ToList() ?? new List<VRMSpringBoneColliderGroup>();
                    if (!list.Contains(mouseSpringColliderGroupVRM0))
                    {
                        list.Add(mouseSpringColliderGroupVRM0);
                        sb.ColliderGroups = list.ToArray();
                    }
                }
            }
            // VRM1
            var vrm10SpringBones = avatarAnimator.GetComponentsInChildren<VRM10SpringBoneJoint>();
            if (vrm10SpringBones != null && vrm10SpringBones.Length > 0)
            {
                mouseSpringColliderGroupVRM1 = mouseColliderObj.AddComponent<VRM10SpringBoneColliderGroup>();
                mouseSpringColliderGroupVRM1.Name = "MouseColliderGroup";
                mouseSpringColliderGroupVRM1.Colliders = new List<VRM10SpringBoneCollider>();
                mouseSpringColliderVRM1 = mouseColliderObj.AddComponent<VRM10SpringBoneCollider>();
                mouseSpringColliderVRM1.ColliderType = VRM10SpringBoneColliderTypes.Sphere;
                mouseSpringColliderVRM1.Offset = Vector3.zero;
                mouseSpringColliderVRM1.Radius = mouseColliderRadius;
                mouseSpringColliderGroupVRM1.Colliders.Add(mouseSpringColliderVRM1);

                var vrm10Root = avatarAnimator.GetComponentInParent<Vrm10Instance>();
                if (vrm10Root != null && vrm10Root.SpringBone != null)
                {
                    if (!vrm10Root.SpringBone.ColliderGroups.Contains(mouseSpringColliderGroupVRM1))
                        vrm10Root.SpringBone.ColliderGroups.Add(mouseSpringColliderGroupVRM1);
                }
            }
        }

        // ColliderObjekt an Mausposition setzen (auf 3D-Position in Avatarn�he)
        Vector3 mouse = X11Manager.Instance.GetMousePosition();
        float zDist = 1.0f;
        if (bigScreenHandler.attachBone != HumanBodyBones.LastBone)
        {
            var bone = avatarAnimator.GetBoneTransform(bigScreenHandler.attachBone);
            if (bone)
            {
                Vector3 boneScreen = mainCamera.WorldToScreenPoint(bone.position);
                zDist = Mathf.Max(0.4f, boneScreen.z);
            }
        }
        mouse.z = zDist;
        Vector3 world = mainCamera.ScreenToWorldPoint(mouse);
        mouseColliderObj.transform.position = world;
    }

    void CleanupMouseCollider()
    {
        if (mouseColliderObj != null)
        {
            // VRM0
            if (mouseSpringColliderGroupVRM0 != null && avatarAnimator != null)
            {
                var vrmSpringBones = avatarAnimator.GetComponentsInChildren<VRMSpringBone>();
                foreach (var sb in vrmSpringBones)
                {
                    var list = sb.ColliderGroups?.ToList() ?? new List<VRMSpringBoneColliderGroup>();
                    if (list.Contains(mouseSpringColliderGroupVRM0))
                    {
                        list.Remove(mouseSpringColliderGroupVRM0);
                        sb.ColliderGroups = list.ToArray();
                    }
                }
            }
            // VRM1
            if (mouseSpringColliderGroupVRM1 != null && avatarAnimator != null)
            {
                var vrm10Root = avatarAnimator.GetComponentInParent<Vrm10Instance>();
                if (vrm10Root != null && vrm10Root.SpringBone != null &&
                    vrm10Root.SpringBone.ColliderGroups.Contains(mouseSpringColliderGroupVRM1))
                {
                    vrm10Root.SpringBone.ColliderGroups.Remove(mouseSpringColliderGroupVRM1);
                }
            }

            Destroy(mouseColliderObj);
            mouseColliderObj = null;
            mouseSpringColliderGroupVRM0 = null;
            mouseSpringColliderGroupVRM1 = null;
            mouseSpringColliderVRM1 = null;
        }
    }
}