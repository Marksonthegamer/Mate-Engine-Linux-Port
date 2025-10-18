using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class MEPhysicBones : MonoBehaviour
{
    public enum UpdateMode { Update, FixedUpdate, LateUpdate }
    public enum SimulationSpace { World, Local }
    public enum FreezeAxis { None, X, Y, Z }

    [Header("Chain")]
    public Transform root;
    public bool autoCollectChildren = true;
    public List<Transform> boneChain = new List<Transform>();
    public bool addVirtualEnd = true;
    public float virtualEndLength = 0.05f;

    [Header("Simulation")]
    public UpdateMode updateMode = UpdateMode.LateUpdate;
    public SimulationSpace simulationSpace = SimulationSpace.World;
    public int iterations = 1;
    public int substeps = 1;
    public float stiffness = 0.6f;
    public float damping = 0.2f;
    public float elasticity = 0.15f;
    public float inertia = 0.0f;
    public Vector3 gravity = new Vector3(0, -9.81f, 0);
    public float gravityScale = 1.0f;
    public Vector3 externalForce = Vector3.zero;
    public float windStrength = 0.0f;
    public Vector3 windDirection = new Vector3(1, 0, 0);
    public float windFrequency = 0.75f;

    [Header("Constraints")]
    public float blendToPose = 0.0f;
    public float maxAngleFromParent = 75.0f;
    public float maxStretch = 1.1f;
    public FreezeAxis freezeAxis = FreezeAxis.None;

    [Header("Collision")]
    public float particleRadius = 0.01f;
    public List<MEPB_SphereCollider> sphereColliders = new List<MEPB_SphereCollider>();
    public List<MEPB_CapsuleCollider> capsuleColliders = new List<MEPB_CapsuleCollider>();

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public float gizmoSize = 0.01f;

    struct Particle
    {
        public Transform t;
        public int parentIndex;
        public Vector3 pos;
        public Vector3 prevPos;
        public Vector3 localPos;
        public Quaternion localRot;
        public float restLength;
        public Vector3 axisLocal;
    }

    List<Particle> particles = new List<Particle>();
    Transform simRoot;
    Vector3 windRand;
    float accumulatedTime;

    void Reset()
    {
        root = transform;
        CollectChain();
    }

    void OnValidate()
    {
        iterations = Mathf.Max(1, iterations);
        substeps = Mathf.Max(1, substeps);
        virtualEndLength = Mathf.Max(0.0001f, virtualEndLength);
        particleRadius = Mathf.Max(0.0f, particleRadius);
        maxStretch = Mathf.Max(1.0f, maxStretch);
        if (autoCollectChildren) CollectChain();
    }

    void OnEnable()
    {
        BuildParticles();
        TeleportToPose();
    }

    void Start()
    {
        if (particles.Count == 0) BuildParticles();
        TeleportToPose();
    }

    void Update()
    {
        if (updateMode == UpdateMode.Update) Simulate(Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (updateMode == UpdateMode.FixedUpdate) Simulate(Time.fixedDeltaTime);
    }

    void LateUpdate()
    {
        if (updateMode == UpdateMode.LateUpdate) Simulate(Time.deltaTime);
    }

    void CollectChain()
    {
        boneChain.Clear();
        if (root == null) return;
        var q = new Queue<Transform>();
        q.Enqueue(root);
        while (q.Count > 0)
        {
            var t = q.Dequeue();
            if (!boneChain.Contains(t)) boneChain.Add(t);
            for (int i = 0; i < t.childCount; i++) q.Enqueue(t.GetChild(i));
        }
    }

    void BuildParticles()
    {
        particles.Clear();
        if (boneChain.Count == 0 && root != null) CollectChain();
        if (boneChain.Count == 0) return;

        simRoot = simulationSpace == SimulationSpace.Local ? transform : null;

        for (int i = 0; i < boneChain.Count; i++)
        {
            var p = new Particle();
            p.t = boneChain[i];
            p.parentIndex = FindParentIndex(i);
            p.localPos = p.t.localPosition;
            p.localRot = p.t.localRotation;
            p.axisLocal = Vector3.forward;
            p.pos = p.t.position;
            p.prevPos = p.pos;
            if (p.parentIndex >= 0)
                p.restLength = Vector3.Distance(boneChain[i - 1].position, p.t.position);
            else
                p.restLength = 0;
            particles.Add(p);
        }

        if (addVirtualEnd)
        {
            var last = particles[particles.Count - 1];
            var end = new GameObject(boneChain[boneChain.Count - 1].name + "_End_ME").transform;
            end.SetParent(last.t, false);
            end.localPosition = new Vector3(0, 0, virtualEndLength);
            var p = new Particle();
            p.t = end;
            p.parentIndex = particles.Count - 1;
            p.localPos = end.localPosition;
            p.localRot = end.localRotation;
            p.axisLocal = Vector3.forward;
            p.pos = end.position;
            p.prevPos = p.pos;
            p.restLength = virtualEndLength;
            particles.Add(p);
            boneChain.Add(end);
        }
    }

    int FindParentIndex(int i)
    {
        if (i == 0) return -1;
        var child = boneChain[i];
        var parent = child.parent;
        for (int p = i - 1; p >= 0; p--)
        {
            if (boneChain[p] == parent) return p;
        }
        return i - 1;
    }

    void TeleportToPose()
    {
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            p.pos = p.t.position;
            p.prevPos = p.pos;
            particles[i] = p;
        }
    }

    void Simulate(float dt)
    {
        if (!isActiveAndEnabled || particles.Count == 0) return;
        int steps = Mathf.Max(1, substeps);
        float stepDt = dt / steps;
        for (int s = 0; s < steps; s++)
        {
            ApplyForces(stepDt);
            SatisfyConstraints();
            ApplyTransforms(blendToPose);
        }
    }

    void ApplyForces(float dt)
    {
        windRand = new Vector3(Mathf.PerlinNoise(Time.time * windFrequency, 0f) - 0.5f, Mathf.PerlinNoise(0f, Time.time * windFrequency) - 0.5f, Mathf.PerlinNoise(Time.time * windFrequency, Time.time * windFrequency) - 0.5f);
        Vector3 wind = windDirection.normalized * windStrength + windRand * windStrength * 0.5f;

        for (int i = 0; i < particles.Count; i++)
        {
            if (particles[i].parentIndex < 0) continue;
            var p = particles[i];
            Vector3 cur = p.pos;
            Vector3 vel = (p.pos - p.prevPos);
            Vector3 acc = gravity * gravityScale + externalForce + wind;
            Vector3 posePos = p.t.position;
            if (inertia > 0f)
            {
                var parentIdx = p.parentIndex;
                if (parentIdx >= 0)
                {
                    var parent = particles[parentIdx];
                    Vector3 parentMove = (parent.pos - parent.prevPos);
                    posePos += parentMove * inertia;
                }
            }
            Vector3 stiffnessTarget = Vector3.LerpUnclamped(cur, posePos, stiffness);
            Vector3 next = cur + vel * (1f - damping) + acc * dt * dt;
            next = Vector3.LerpUnclamped(next, stiffnessTarget, elasticity);
            p.prevPos = cur;
            p.pos = next;
            particles[i] = p;
        }
    }

    void SatisfyConstraints()
    {
        for (int it = 0; it < iterations; it++)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                int parentIdx = p.parentIndex;
                if (parentIdx >= 0)
                {
                    var parent = particles[parentIdx];
                    Vector3 dir = p.pos - parent.pos;
                    float len = dir.magnitude;
                    if (len > 0.000001f)
                    {
                        float targetLen = Mathf.Clamp(len, p.restLength * 0.999f, p.restLength * maxStretch);
                        Vector3 n = dir / len;
                        Vector3 newPos = parent.pos + n * targetLen;
                        p.pos = newPos;
                    }
                    LimitAngle(ref p, parent);
                    ApplyFreeze(ref p, parent);
                    ResolveCollisions(ref p);
                    particles[i] = p;
                }
            }
        }
    }

    void LimitAngle(ref Particle child, Particle parent)
    {
        var parentDir = ParentForward(parent);
        Vector3 v = child.pos - parent.pos;
        if (v.sqrMagnitude < 1e-10f) return;
        float angle = Vector3.Angle(parentDir, v);
        if (angle > maxAngleFromParent)
        {
            Vector3 axis = Vector3.Cross(parentDir, v).normalized;
            Quaternion rot = Quaternion.AngleAxis(angle - maxAngleFromParent, axis);
            Vector3 limited = rot * v;
            child.pos = parent.pos + limited;
        }
    }

    Vector3 ParentForward(Particle p)
    {
        if (p.t.childCount > 0) return (p.t.GetChild(0).position - p.t.position).normalized;
        return p.t.rotation * Vector3.forward;
    }

    void ApplyFreeze(ref Particle child, Particle parent)
    {
        if (freezeAxis == FreezeAxis.None) return;
        var basis = parent.t.rotation;
        Vector3 rel = Quaternion.Inverse(basis) * (child.pos - parent.pos);
        if (freezeAxis == FreezeAxis.X) rel.x = 0f;
        if (freezeAxis == FreezeAxis.Y) rel.y = 0f;
        if (freezeAxis == FreezeAxis.Z) rel.z = 0f;
        child.pos = parent.pos + basis * rel;
    }

    void ResolveCollisions(ref Particle p)
    {
        float r = particleRadius;
        for (int i = 0; i < sphereColliders.Count; i++)
        {
            var c = sphereColliders[i];
            if (c == null || !c.enabled) continue;
            Vector3 cPos = c.transform.TransformPoint(c.center);
            float cr = Mathf.Max(0f, c.radius);
            Vector3 to = p.pos - cPos;
            float d = to.magnitude;
            float push = (r + cr) - d;
            if (push > 0f && d > 1e-6f) p.pos += to / d * push;
        }
        for (int i = 0; i < capsuleColliders.Count; i++)
        {
            var c = capsuleColliders[i];
            if (c == null || !c.enabled) continue;
            c.GetWorldCapsule(out Vector3 a, out Vector3 b, out float cr);
            Vector3 cp = ClosestPointOnSegment(a, b, p.pos);
            Vector3 to = p.pos - cp;
            float d = to.magnitude;
            float push = (r + cr) - d;
            if (push > 0f && d > 1e-6f) p.pos += to / d * push;
        }
    }

    static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude);
        t = Mathf.Clamp01(t);
        return a + ab * t;
    }

    void ApplyTransforms(float blend)
    {
        blend = Mathf.Clamp01(blend);
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            int parentIdx = p.parentIndex;
            if (parentIdx < 0) continue;
            var parent = particles[parentIdx];
            Vector3 aimPos = p.pos;
            Vector3 basePos = p.t.position;
            Vector3 finalPos = Vector3.Lerp(aimPos, basePos, blend);
            p.pos = finalPos;
            particles[i] = p;

            Vector3 dir = finalPos - parent.pos;
            if (dir.sqrMagnitude > 1e-10f)
            {
                Quaternion look = Quaternion.LookRotation(dir.normalized, parent.t.up);
                parent.t.rotation = Quaternion.Slerp(parent.t.rotation, look, 1f);
            }

            float len = (finalPos - parent.pos).magnitude;
            if (p.restLength > 1e-6f)
            {
                parent.t.localScale = Vector3.one;
                parent.t.position = parent.t.position;
            }
        }

        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            p.t.position = i == 0 ? p.t.position : particles[i].pos;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || particles == null || particles.Count == 0) return;
        Gizmos.color = new Color(1f, 0f, 1f, 0.85f);
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            if (p.t == null) continue;
            Gizmos.DrawSphere(p.pos == Vector3.zero ? p.t.position : p.pos, gizmoSize);
            if (p.parentIndex >= 0)
            {
                var parent = particles[p.parentIndex];
                Gizmos.DrawLine(parent.pos == Vector3.zero ? parent.t.position : parent.pos, p.pos == Vector3.zero ? p.t.position : p.pos);
            }
        }
    }
}

public class MEPB_SphereCollider : MonoBehaviour
{
    public Vector3 center = Vector3.zero;
    public float radius = 0.05f;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireSphere(center, radius);
    }
}

public class MEPB_CapsuleCollider : MonoBehaviour
{
    public Vector3 pointA = new Vector3(0, -0.05f, 0);
    public Vector3 pointB = new Vector3(0, 0.05f, 0);
    public float radius = 0.05f;

    public void GetWorldCapsule(out Vector3 a, out Vector3 b, out float r)
    {
        a = transform.TransformPoint(pointA);
        b = transform.TransformPoint(pointB);
        r = Mathf.Max(0f, radius);
    }

    void OnDrawGizmosSelected()
    {
        GetWorldCapsule(out Vector3 a, out Vector3 b, out float r);
        Gizmos.color = new Color(1f, 0f, 1f, 0.35f);
        Gizmos.DrawLine(a, b);
        DrawCircle(a, r);
        DrawCircle(b, r);
    }

    void DrawCircle(Vector3 c, float r)
    {
        const int n = 24;
        Vector3 prev = c + Vector3.right * r;
        for (int i = 1; i <= n; i++)
        {
            float t = i / (float)n;
            float ang = t * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}