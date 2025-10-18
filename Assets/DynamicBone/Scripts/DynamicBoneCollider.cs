using UnityEngine;

[AddComponentMenu("Dynamic Bone/Dynamic Bone Collider")]
public class DynamicBoneCollider : DynamicBoneColliderBase
{
#if UNITY_5_3_OR_NEWER
    [Tooltip("The radius of the sphere or capsule.")]
#endif	
    public float m_Radius = 0.5f;

#if UNITY_5_3_OR_NEWER
    [Tooltip("The height of the capsule.")]
#endif		
    public float m_Height = 0;

#if UNITY_5_3_OR_NEWER
    [Tooltip("The other radius of the capsule.")]
#endif	
    public float m_Radius2 = 0;

    // prepare data
    float m_ScaledRadius;
    float m_ScaledRadius2;
    Vector3 m_C0;
    Vector3 m_C1;
    float m_C01Distance;
    int m_CollideType;

    void OnValidate()
    {
        m_Radius = Mathf.Max(m_Radius, 0);
        m_Height = Mathf.Max(m_Height, 0);
        m_Radius2 = Mathf.Max(m_Radius2, 0);
    }

    public override void Prepare()
    {
        float scale = Mathf.Abs(transform.lossyScale.x);
        float halfHeight = m_Height * 0.5f;

        if (m_Radius2 <= 0 || Mathf.Abs(m_Radius - m_Radius2) < 0.01f)
        {
            m_ScaledRadius = m_Radius * scale;

            float h = halfHeight - m_Radius;
            if (h <= 0)
            {
                m_C0 = transform.TransformPoint(m_Center);

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 0;
                }
                else
                {
                    m_CollideType = 1;
                }
            }
            else
            {
                Vector3 c0 = m_Center;
                Vector3 c1 = m_Center;

                switch (m_Direction)
                {
                    case Direction.X:
                        c0.x += h;
                        c1.x -= h;
                        break;
                    case Direction.Y:
                        c0.y += h;
                        c1.y -= h;
                        break;
                    case Direction.Z:
                        c0.z += h;
                        c1.z -= h;
                        break;
                }

                m_C0 = transform.TransformPoint(c0);
                m_C1 = transform.TransformPoint(c1);
                m_C01Distance = (m_C1 - m_C0).magnitude;

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 2;
                }
                else
                {
                    m_CollideType = 3;
                }
            }
        }
        else
        {
            float r = Mathf.Max(m_Radius, m_Radius2);
            if (halfHeight - r <= 0)
            {
                m_ScaledRadius = r * scale;
                m_C0 = transform.TransformPoint(m_Center);

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 0;
                }
                else
                {
                    m_CollideType = 1;
                }
            }
            else
            {
                m_ScaledRadius = m_Radius * scale;
                m_ScaledRadius2 = m_Radius2 * scale;

                float h0 = halfHeight - m_Radius;
                float h1 = halfHeight - m_Radius2;
                Vector3 c0 = m_Center;
                Vector3 c1 = m_Center;

                switch (m_Direction)
                {
                    case Direction.X:
                        c0.x += h0;
                        c1.x -= h1;
                        break;
                    case Direction.Y:
                        c0.y += h0;
                        c1.y -= h1;
                        break;
                    case Direction.Z:
                        c0.z += h0;
                        c1.z -= h1;
                        break;
                }

                m_C0 = transform.TransformPoint(c0);
                m_C1 = transform.TransformPoint(c1);
                m_C01Distance = (m_C1 - m_C0).magnitude;

                if (m_Bound == Bound.Outside)
                {
                    m_CollideType = 4;
                }
                else
                {
                    m_CollideType = 5;
                }
            }
        }
    }

    public override bool Collide(ref Vector3 particlePosition, float particleRadius)
    {
        switch (m_CollideType)
        {
            case 0:
                return OutsideSphere(ref particlePosition, particleRadius, m_C0, m_ScaledRadius);
            case 1:
                return InsideSphere(ref particlePosition, particleRadius, m_C0, m_ScaledRadius);
            case 2:
                return OutsideCapsule(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_C01Distance);
            case 3:
                return InsideCapsule(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_C01Distance);
            case 4:
                return OutsideCapsule2(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_ScaledRadius2, m_C01Distance);
            case 5:
                return InsideCapsule2(ref particlePosition, particleRadius, m_C0, m_C1, m_ScaledRadius, m_ScaledRadius2, m_C01Distance);
        }
        return false;
    }

    static bool OutsideSphere(ref Vector3 particlePosition, float particleRadius, Vector3 sphereCenter, float sphereRadius)
    {
        float r = sphereRadius + particleRadius;
        float r2 = r * r;
        Vector3 d = particlePosition - sphereCenter;
        float dlen2 = d.sqrMagnitude;

        // if is inside sphere, project onto sphere surface
        if (dlen2 > 0 && dlen2 < r2)
        {
            float dlen = Mathf.Sqrt(dlen2);
            particlePosition = sphereCenter + d * (r / dlen);
            return true;
        }
        return false;
    }

    static bool InsideSphere(ref Vector3 particlePosition, float particleRadius, Vector3 sphereCenter, float sphereRadius)
    {
        float r = sphereRadius - particleRadius;
        float r2 = r * r;
        Vector3 d = particlePosition - sphereCenter;
        float dlen2 = d.sqrMagnitude;

        // if is outside sphere, project onto sphere surface
        if (dlen2 > r2)
        {
            float dlen = Mathf.Sqrt(dlen2);
            particlePosition = sphereCenter + d * (r / dlen);
            return true;
        }
        return false;
    }

    static bool OutsideCapsule(ref Vector3 particlePosition, float particleRadius, Vector3 capsuleP0, Vector3 capsuleP1, float capsuleRadius, float dirlen)
    {
        float r = capsuleRadius + particleRadius;
        float r2 = r * r;
        Vector3 dir = capsuleP1 - capsuleP0;
        Vector3 d = particlePosition - capsuleP0;
        float t = Vector3.Dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float dlen2 = d.sqrMagnitude;
            if (dlen2 > 0 && dlen2 < r2)
            {
                float dlen = Mathf.Sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                d = particlePosition - capsuleP1;
                float dlen2 = d.sqrMagnitude;
                if (dlen2 > 0 && dlen2 < r2)
                {
                    float dlen = Mathf.Sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                Vector3 q = d - dir * (t / dirlen2);
                float qlen2 = q.sqrMagnitude;
                if (qlen2 > 0 && qlen2 < r2)
                {
                    float qlen = Mathf.Sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
        return false;
    }

    static bool InsideCapsule(ref Vector3 particlePosition, float particleRadius, Vector3 capsuleP0, Vector3 capsuleP1, float capsuleRadius, float dirlen)
    {
        float r = capsuleRadius - particleRadius;
        float r2 = r * r;
        Vector3 dir = capsuleP1 - capsuleP0;
        Vector3 d = particlePosition - capsuleP0;
        float t = Vector3.Dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float dlen2 = d.sqrMagnitude;
            if (dlen2 > r2)
            {
                float dlen = Mathf.Sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                d = particlePosition - capsuleP1;
                float dlen2 = d.sqrMagnitude;
                if (dlen2 > r2)
                {
                    float dlen = Mathf.Sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                Vector3 q = d - dir * (t / dirlen2);
                float qlen2 = q.sqrMagnitude;
                if (qlen2 > r2)
                {
                    float qlen = Mathf.Sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
        return false;
    }

    static bool OutsideCapsule2(ref Vector3 particlePosition, float particleRadius, Vector3 capsuleP0, Vector3 capsuleP1, float capsuleRadius0, float capsuleRadius1, float dirlen)
    {
        Vector3 dir = capsuleP1 - capsuleP0;
        Vector3 d = particlePosition - capsuleP0;
        float t = Vector3.Dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float r = capsuleRadius0 + particleRadius;
            float r2 = r * r;
            float dlen2 = d.sqrMagnitude;
            if (dlen2 > 0 && dlen2 < r2)
            {
                float dlen = Mathf.Sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                float r = capsuleRadius1 + particleRadius;
                float r2 = r * r;
                d = particlePosition - capsuleP1;
                float dlen2 = d.sqrMagnitude;
                if (dlen2 > 0 && dlen2 < r2)
                {
                    float dlen = Mathf.Sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                Vector3 q = d - dir * (t / dirlen2);
                float qlen2 = q.sqrMagnitude;

                float klen = Vector3.Dot(d, dir / dirlen);
                float r = Mathf.Lerp(capsuleRadius0, capsuleRadius1, klen / dirlen) + particleRadius;
                float r2 = r * r;

                if (qlen2 > 0 && qlen2 < r2)
                {
                    float qlen = Mathf.Sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
        return false;
    }

    static bool InsideCapsule2(ref Vector3 particlePosition, float particleRadius, Vector3 capsuleP0, Vector3 capsuleP1, float capsuleRadius0, float capsuleRadius1, float dirlen)
    {
        Vector3 dir = capsuleP1 - capsuleP0;
        Vector3 d = particlePosition - capsuleP0;
        float t = Vector3.Dot(d, dir);

        if (t <= 0)
        {
            // check sphere1
            float r = capsuleRadius0 - particleRadius;
            float r2 = r * r;
            float dlen2 = d.sqrMagnitude;
            if (dlen2 > r2)
            {
                float dlen = Mathf.Sqrt(dlen2);
                particlePosition = capsuleP0 + d * (r / dlen);
                return true;
            }
        }
        else
        {
            float dirlen2 = dirlen * dirlen;
            if (t >= dirlen2)
            {
                // check sphere2
                float r = capsuleRadius1 - particleRadius;
                float r2 = r * r;
                d = particlePosition - capsuleP1;
                float dlen2 = d.sqrMagnitude;
                if (dlen2 > r2)
                {
                    float dlen = Mathf.Sqrt(dlen2);
                    particlePosition = capsuleP1 + d * (r / dlen);
                    return true;
                }
            }
            else
            {
                // check cylinder
                Vector3 q = d - dir * (t / dirlen2);
                float qlen2 = q.sqrMagnitude;

                float klen = Vector3.Dot(d, dir / dirlen);
                float r = Mathf.Lerp(capsuleRadius0, capsuleRadius1, klen / dirlen) - particleRadius;
                float r2 = r * r;

                if (qlen2 > r2)
                {
                    float qlen = Mathf.Sqrt(qlen2);
                    particlePosition += q * ((r - qlen) / qlen);
                    return true;
                }
            }
        }
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

        switch (m_CollideType)
        {
            case 0:
            case 1:
                Gizmos.DrawWireSphere(m_C0, m_ScaledRadius);
                break;
            case 2:
            case 3:
                DrawCapsule(m_C0, m_C1, m_ScaledRadius, m_ScaledRadius);
                break;
            case 4:
            case 5:
                DrawCapsule(m_C0, m_C1, m_ScaledRadius, m_ScaledRadius2);
                break;
        }
    }

    static void DrawCapsule(Vector3 c0, Vector3 c1, float radius0, float radius1)
    {
        Gizmos.DrawLine(c0, c1);
        Gizmos.DrawWireSphere(c0, radius0);
        Gizmos.DrawWireSphere(c1, radius1);
    }
}
