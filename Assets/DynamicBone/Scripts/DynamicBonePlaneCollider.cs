using UnityEngine;

[AddComponentMenu("Dynamic Bone/Dynamic Bone Plane Collider")]
public class DynamicBonePlaneCollider : DynamicBoneColliderBase
{
    // prepare data
    Plane m_Plane;

    void OnValidate()
    {
    }

    public override void Prepare()
    {
        Vector3 normal = Vector3.up;
        switch (m_Direction)
        {
            case Direction.X:
                normal = transform.right;
                break;
            case Direction.Y:
                normal = transform.up;
                break;
            case Direction.Z:
                normal = transform.forward;
                break;
        }

        Vector3 p = transform.TransformPoint(m_Center);
        m_Plane.SetNormalAndPosition(normal, p);
    }

    public override bool Collide(ref Vector3 particlePosition, float particleRadius)
    {
        float d = m_Plane.GetDistanceToPoint(particlePosition);

        if (m_Bound == Bound.Outside)
        {
            if (d < 0)
            {
                particlePosition -= m_Plane.normal * d;
                return true;
            }
        }
        else
        {
            if (d > 0)
            {
                particlePosition -= m_Plane.normal * d;
                return true;
            }
        }

/*        // consider particleRadius
        if (m_Bound == Bound.Outside)
        {
            if (d < particleRadius)
            {
                particlePosition += m_Plane.normal * (particleRadius - d);
                return true;
            }
        }
        else
        {
            if (d > -particleRadius)
            {
                particlePosition -= m_Plane.normal * (particleRadius + d);
                return true;
            }
        }
*/
        return false;
    }

    void OnDrawGizmosSelected()
    {
        if (!enabled)
            return;

        Prepare();

        if (m_Bound == Bound.Outside)
        {
            Gizmos.color = Color.yellow;
        }
        else
        {
            Gizmos.color = Color.magenta;
        }

        Vector3 p = transform.TransformPoint(m_Center);
        Gizmos.DrawLine(p, p + m_Plane.normal);
    }
}
