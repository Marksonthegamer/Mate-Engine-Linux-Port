using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SwingFollowEntry
{
    public string label = "Follow";
    public GameObject menuObject;
    public HumanBodyBones targetBone = HumanBodyBones.Head;
    [Range(0f, 1f)] public float smoothness = 0.15f;
    [HideInInspector] public Vector2 baseOffset;
    [HideInInspector] public bool hasBaseOffset;
    [HideInInspector] public Vector2 currentPosition;
    [HideInInspector] public float originalY;

    public bool blockYMovement = false;

    [Range(0f, 100f), Tooltip("0 - 100")]
    public float yThreshold = 0f;
}

public class SwingController : MonoBehaviour
{
    public List<SwingFollowEntry> follows = new();

    private Camera mainCam;
    private Transform modelRoot;
    private GameObject currentModel;
    private AvatarAnimatorReceiver currentReceiver;

    void Awake()
    {
        mainCam = Camera.main;
        modelRoot = GameObject.Find("Model")?.transform;
    }

    void UpdateCurrentAvatar()
    {
        if (!modelRoot) return;
        for (int i = 0; i < modelRoot.childCount; i++)
        {
            var child = modelRoot.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                if (currentModel != child.gameObject)
                {
                    currentModel = child.gameObject;
                    currentReceiver = currentModel.GetComponent<AvatarAnimatorReceiver>();
                }
                return;
            }
        }
    }

    void LateUpdate()
    {
        if (mainCam == null)
            mainCam = Camera.main;

        UpdateCurrentAvatar();
        if (currentReceiver == null || currentReceiver.avatarAnimator == null)
            return;

        foreach (var entry in follows)
        {
            if (entry.menuObject == null) continue;

            Transform bone = currentReceiver.avatarAnimator.GetBoneTransform(entry.targetBone);
            if (bone == null) continue;

            RectTransform rect = entry.menuObject.GetComponent<RectTransform>();
            if (rect == null || rect.parent == null) continue;

            Vector3 bonePos = bone.position;
            Vector3 targetScreenPos = mainCam.WorldToScreenPoint(bonePos);

            Vector2 targetLocal = ScreenToLocal(rect, targetScreenPos);
            if (!entry.hasBaseOffset)
            {
                Vector2 currentAnchored = rect.anchoredPosition;
                entry.baseOffset.x = currentAnchored.x - targetLocal.x;
                entry.baseOffset.y = 0f;
                entry.currentPosition = currentAnchored;
                entry.originalY = currentAnchored.y;
                entry.hasBaseOffset = true;
            }

            Vector2 finalTarget = targetLocal + entry.baseOffset;

            if (entry.blockYMovement || entry.yThreshold >= 100f)
            {
                finalTarget.y = entry.originalY;
            }
            else
            {
                float boneDeltaY = targetLocal.y - entry.originalY;
                float factor = 1f - (entry.yThreshold / 100f);
                finalTarget.y = entry.originalY + boneDeltaY * factor;
            }

            entry.currentPosition = Vector2.Lerp(entry.currentPosition, finalTarget, 1f - entry.smoothness);
            rect.anchoredPosition = entry.currentPosition;
        }
    }

    Vector2 ScreenToLocal(RectTransform rect, Vector3 screen)
    {
        RectTransform parent = rect.parent as RectTransform;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, mainCam, out localPoint);
        return localPoint;
    }
}
