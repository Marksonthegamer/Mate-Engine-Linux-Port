using System.Collections.Generic;
using UnityEngine;
using UniVRM10;
using VRM;
using X11;

public class AvatarGravityController : MonoBehaviour
{
    [Header("Impact Settings")]
    [Tooltip("How much motion from window drag affects SpringBones")]
    public float impactMultiplier = 0.05f;

    [Header("Debug")]
    public bool showDebugForce = true;
    public Color debugColor = Color.cyan;

    private Vector2Int previousWindowPos;
    private Vector3 currentForce;

    private List<VRMSpringBone> springBones = new();
    private List<VRM10SpringBoneJoint> springBoneJoints = new();
    private Vrm10Instance vrm10Instance;

    void Start()
    {
        previousWindowPos = GetWindowPosition();

        // VRM0 spring bones
        springBones.AddRange(GetComponentsInChildren<VRMSpringBone>(true));

        // VRM1 spring joints
        springBoneJoints.AddRange(GetComponentsInChildren<VRM10SpringBoneJoint>(true));

        // VRM1 runtime handler
        vrm10Instance = GetComponentInParent<Vrm10Instance>();
    }

    void Update()
    {
        Vector2Int currentWindowPos = GetWindowPosition();
        Vector2Int delta = currentWindowPos - previousWindowPos;

        if (delta != Vector2Int.zero)
        {
            Vector3 impact = new Vector3(-delta.x, delta.y, 0).normalized * impactMultiplier;
            currentForce = impact;
        }
        else
        {
            currentForce = Vector3.zero;
        }

        // VRM0: set external force
        foreach (var spring in springBones)
        {
            if (spring != null)
                spring.ExternalForce = currentForce;
        }

        // VRM1: apply gravity dir/power and notify runtime
        foreach (var joint in springBoneJoints)
        {
            if (joint == null) continue;

            joint.m_gravityDir = currentForce.normalized;
            joint.m_gravityPower = currentForce.magnitude;

            if (vrm10Instance != null && vrm10Instance.Runtime != null)
            {
                vrm10Instance.Runtime.SpringBone.SetJointLevel(joint.transform, joint.Blittable);
            }
        }

        previousWindowPos = currentWindowPos;
    }

    void OnDrawGizmos()
    {
        if (!showDebugForce) return;

        Gizmos.color = debugColor;
        Gizmos.DrawLine(transform.position, transform.position + currentForce);
        Gizmos.DrawSphere(transform.position + currentForce, 0.02f);
    }

    #region Linux API

    private Vector2Int GetWindowPosition()
    {
        Vector2 vect = X11Manager.Instance.GetWindowPosition();
        return new Vector2Int((int)vect.x, (int)vect.y);
    }

    #endregion
}
