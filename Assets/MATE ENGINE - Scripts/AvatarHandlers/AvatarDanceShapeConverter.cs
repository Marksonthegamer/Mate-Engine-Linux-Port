using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace CustomDancePlayer
{
    [DisallowMultipleComponent]
    public class AvatarDanceShapeConverter : MonoBehaviour
    {
        public Mesh dummyBlendshapeMesh;
        public string[] candidatePaths = { "Body", "Model/Body", "Face" };
        public bool hideProxyRenderer = true;

        private Animator targetAnimator;
        private AvatarDanceHandler dancePlayer;
        private VRMLoader vrmLoader;
        private GameObject lastModel;

        private UniversalBlendshapes ub;
        private PlayableGraph graph;
        private AnimationClipPlayable clipPlayable;
        private Animator proxyAnimator;
        private SkinnedMeshRenderer[] proxySmrs = Array.Empty<SkinnedMeshRenderer>();
        private AnimationClip boundClip;
        private bool built;
        private bool lastPlaying;
        private bool bypassForThisAvatar;

        void Awake()
        {
            dancePlayer = FindFirstObjectByType<AvatarDanceHandler>();
            vrmLoader = FindFirstObjectByType<VRMLoader>();
        }

        void OnEnable()
        {
            TearDownGraph();
            TearDownProxy();
            FindAndBindAnimator(true);
            lastPlaying = false;
        }

        void OnDisable()
        {
            TearDownGraph();
            TearDownProxy();
            targetAnimator = null;
            ub = null;
            lastModel = null;
            lastPlaying = false;
            bypassForThisAvatar = false;
        }

        void Update()
        {
            if (dancePlayer == null) dancePlayer = FindFirstObjectByType<AvatarDanceHandler>();
            if (vrmLoader == null) vrmLoader = FindFirstObjectByType<VRMLoader>();
            FindAndBindAnimator(false);
        }

        void LateUpdate()
        {
            if (targetAnimator == null || dancePlayer == null) return;
            if (bypassForThisAvatar) return;

            EnsureProxyIfNeeded();
            if (!built || ub == null) return;

            bool playing = dancePlayer.IsPlaying;
            if (!playing)
            {
                if (lastPlaying)
                {
                    ZeroOut();
                    TearDownGraph();
                }
                lastPlaying = false;
                return;
            }

            lastPlaying = true;

            var clip = dancePlayer.GetCurrentClip();
            if (clip != boundClip) EnsureGraph(clip);
            if (!clipPlayable.IsValid()) return;

            double t = dancePlayer.GetPlaybackTime();
            clipPlayable.SetTime(t);
            graph.Evaluate(0);

            float b = GetMax("まばたき");
            float bl = Mathf.Max(GetMax("ウィンク"), GetMax("ウィンク２"));
            float br = Mathf.Max(GetMax("ウィンク右"), GetMax("ｳｨﾝｸ２右"));
            ub.Blink = b; ub.Blink_L = bl; ub.Blink_R = br;

            ub.A = GetMax("あ");
            ub.I = GetMax("い");
            ub.U = GetMax("う");
            ub.E = GetMax("え");
            ub.O = GetMax("お");

            ub.Joy = GetMax("にこり");
            ub.Angry = GetMax("怒り");
            ub.Sorrow = GetMax("困る");
            ub.Neutral = GetMax("真面目");
            ub.Fun = GetMax("笑い");
        }

        public void ForceReset()
        {
            if (!bypassForThisAvatar) ZeroOut();
            TearDownGraph();
            lastPlaying = false;
        }

        private void FindAndBindAnimator(bool force)
        {
            GameObject model = null;
            if (vrmLoader != null)
            {
                model = vrmLoader.GetCurrentModel();
                if (model == null || !model.activeInHierarchy) model = vrmLoader.mainModel;
            }
            if (model == null)
            {
                var a = FindFirstObjectByType<Animator>();
                if (a != null) model = a.transform.root.gameObject;
            }
            if (model == null) return;
            if (!force && model == lastModel && targetAnimator != null && targetAnimator.isActiveAndEnabled) return;

            var anim = model.GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isActiveAndEnabled) return;

            targetAnimator = anim;
            lastModel = model;

            bypassForThisAvatar = HasMmdBlendshapes(targetAnimator);

            if (bypassForThisAvatar)
            {
                TearDownGraph();
                TearDownProxy();
                ub = null;
                return;
            }

            ub = targetAnimator.GetComponent<UniversalBlendshapes>();
            if (ub == null) ub = targetAnimator.gameObject.AddComponent<UniversalBlendshapes>();
            ZeroOut();
        }

        private bool HasMmdBlendshapes(Animator a)
        {
            if (a == null) return false;
            var smrs = a.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            string[] tokens = {
                "まばたき","ウィンク","ウィンク２","ウィンク右","ｳｨﾝｸ２右",
                "あ","い","う","え","お",
                "にこり","怒り","困る","真面目","笑い"
            };
            for (int s = 0; s < smrs.Length; s++)
            {
                var m = smrs[s].sharedMesh;
                if (!m || m.blendShapeCount == 0) continue;
                for (int i = 0; i < m.blendShapeCount; i++)
                {
                    string n = m.GetBlendShapeName(i);
                    for (int t = 0; t < tokens.Length; t++)
                        if (n.Contains(tokens[t])) return true;
                }
            }
            return false;
        }

        private void EnsureProxyIfNeeded()
        {
            if (built || dummyBlendshapeMesh == null) return;

            var root = new GameObject("ADS_ProxyRoot");
            root.transform.SetParent(transform, false);
            proxyAnimator = root.AddComponent<Animator>();

            var list = new System.Collections.Generic.List<SkinnedMeshRenderer>();
            for (int i = 0; i < candidatePaths.Length; i++)
            {
                var current = root.transform;
                var parts = candidatePaths[i].Split('/');
                for (int p = 0; p < parts.Length; p++)
                {
                    var child = current.Find(parts[p]);
                    if (!child)
                    {
                        var go = new GameObject(parts[p]);
                        go.transform.SetParent(current, false);
                        child = go.transform;
                    }
                    current = child;
                }
                var smr = current.GetComponent<SkinnedMeshRenderer>();
                if (!smr) smr = current.gameObject.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = dummyBlendshapeMesh;
                smr.updateWhenOffscreen = true;
                if (hideProxyRenderer)
                {
                    smr.enabled = false;
                    smr.shadowCastingMode = ShadowCastingMode.Off;
                    smr.receiveShadows = false;
                }
                list.Add(smr);
            }
            proxySmrs = list.ToArray();
            built = true;
        }

        private void EnsureGraph(AnimationClip clip)
        {
            TearDownGraph();
            if (!proxyAnimator) return;
            if (clip == null) { boundClip = null; return; }

            graph = PlayableGraph.Create("ADS_Graph");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetSpeed(0);
            var output = AnimationPlayableOutput.Create(graph, "ADS_Output", proxyAnimator);
            output.SetSourcePlayable(clipPlayable);
            boundClip = clip;
            graph.Play();
        }

        private void TearDownGraph()
        {
            if (graph.IsValid())
            {
                graph.Stop();
                graph.Destroy();
            }
            clipPlayable = default;
            boundClip = null;
        }

        private void TearDownProxy()
        {
            if (proxyAnimator) Destroy(proxyAnimator.gameObject);
            proxyAnimator = null;
            proxySmrs = Array.Empty<SkinnedMeshRenderer>();
            built = false;
        }

        private float GetMax(string token)
        {
            float v = 0f;
            for (int s = 0; s < proxySmrs.Length; s++)
            {
                var smr = proxySmrs[s];
                var mesh = smr ? smr.sharedMesh : null;
                if (!mesh) continue;
                int n = mesh.blendShapeCount;
                for (int i = 0; i < n; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    if (name.Contains(token))
                    {
                        float w = Mathf.Clamp01(smr.GetBlendShapeWeight(i) / 100f);
                        if (w > v) v = w;
                    }
                }
            }
            return v;
        }

        private void ZeroOut()
        {
            if (!ub) return;
            ub.Blink = 0f;
            ub.Blink_L = 0f;
            ub.Blink_R = 0f;
            ub.A = 0f;
            ub.I = 0f;
            ub.U = 0f;
            ub.E = 0f;
            ub.O = 0f;
            ub.Joy = 0f;
            ub.Angry = 0f;
            ub.Sorrow = 0f;
            ub.Neutral = 0f;
            ub.Fun = 0f;
        }
    }
}
