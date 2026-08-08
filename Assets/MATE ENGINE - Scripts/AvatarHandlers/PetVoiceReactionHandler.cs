using System;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Random = UnityEngine.Random;

public class PetVoiceReactionHandler : MonoBehaviour
{
    [System.Serializable]
    public class VoiceRegion
    {
        public string name;
        public bool IsHusbando;
        public HumanBodyBones targetBone;
        public Vector3 offset;
        public Vector3 worldOffset;
        public float hoverRadius = 50f;
        public Color gizmoColor = new Color(1f, 0.5f, 0f, 0.25f);
        public List<AudioClip> voiceClips = new List<AudioClip>();
        public string hoverAnimationState;
        public string hoverAnimationLayer;
        public string hoverAnimationParameter;
        public string faceAnimationState;
        public string faceAnimationLayer;
        public string faceAnimationParameter;
        public bool enableHoverObject;
        public bool bindHoverObjectToBone;
        public bool enableLayeredSound;
        public GameObject hoverObject;
        [Range(0.1f, 10f)] public float despawnAfterSeconds = 5f;
        public List<AudioClip> layeredVoiceClips = new List<AudioClip>();
        [HideInInspector] public bool wasHovering;
        [HideInInspector] public Transform bone;
        [HideInInspector] public int hoverLayerIndex;
        [HideInInspector] public int faceLayerIndex;
        [HideInInspector] public bool hasHoverBool;
        [HideInInspector] public bool hasFaceBool;

        [Header("Pat Mode")]
        public bool patMode = false;
        [Range(90f, 1080f)] public float patCircleDegrees = 540f;
        [Range(0f, 50f)] public float patMinRadius = 12f;
        [Range(0.1f, 2f)] public float patResetTime = 0.6f;
        [Range(0f, 1f)] public float patCooldown = 0.5f;
        [Range(20f, 600f)] public float patWiggleDistance = 180f;
        [Range(1, 10)] public int patWiggleDirectionChanges = 3;

        [Header("Per-Region State Whitelist")]
        [SerializeField] public List<string> stateWhitelist = new List<string>();
        [HideInInspector] public HashSet<int> whitelistHashes = new HashSet<int>();

        [HideInInspector] public bool patHasLast;
        [HideInInspector] public Vector2 patLastPos;
        [HideInInspector] public Vector2 patLastMove;
        [HideInInspector] public float patLastAngle;
        [HideInInspector] public float patCircleAccum;
        [HideInInspector] public float patWiggleAccumDist;
        [HideInInspector] public int patWiggleChanges;
        [HideInInspector] public float patExpireAt;
        [HideInInspector] public float patCooldownUntil;
    }

    class HoverInstance { public GameObject obj; public float despawnTime; }

    public static bool GlobalHoverObjectsEnabled = true;
    public Animator avatarAnimator;
    public List<VoiceRegion> regions = new List<VoiceRegion>();
    public AudioSource voiceAudioSource;
    public AudioSource layeredAudioSource;
    public bool showDebugGizmos = true;

    [Header("Global State Whitelist")]
    [SerializeField] public List<string> stateWhitelist = new List<string>();

    [Range(0f, 0.1f)] public float checkInterval = 0.02f;

    [Header("OS Occlusion")]
    public bool blockWhenCovered = true;
    System.IntPtr _unityHwnd;
    bool _hwndCached;

    Camera cachedCamera;
    readonly Dictionary<VoiceRegion, List<HoverInstance>> pool = new Dictionary<VoiceRegion, List<HoverInstance>>();
    bool hasSetup;

    static readonly int isMaleHash = Animator.StringToHash("isMale");

    readonly HashSet<int> boolParams = new HashSet<int>();
    bool hasIsMaleParam;
    readonly HashSet<int> globalWhitelistHashes = new HashSet<int>();

    AvatarBigScreenHandler cachedBigScreen;
    FieldInfo bigScreenFlag;
    bool bigScreenBlocked;
    float nextCheck;

    void Start()
    {
        if (!hasSetup) TrySetup();
    }

    public void SetAnimator(Animator a)
    {
        avatarAnimator = a;
        hasSetup = false;
    }

    void TrySetup()
    {
        if (!avatarAnimator) return;
        if (!voiceAudioSource) voiceAudioSource = gameObject.AddComponent<AudioSource>();
        if (!layeredAudioSource) layeredAudioSource = gameObject.AddComponent<AudioSource>();
        cachedCamera = Camera.main;

        boolParams.Clear();
        var ps = avatarAnimator.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].type == AnimatorControllerParameterType.Bool)
                boolParams.Add(ps[i].nameHash);
        hasIsMaleParam = false;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == isMaleHash) { hasIsMaleParam = true; break; }

        globalWhitelistHashes.Clear();
        for (int i = 0; i < stateWhitelist.Count; i++)
            if (!string.IsNullOrEmpty(stateWhitelist[i]))
                globalWhitelistHashes.Add(Animator.StringToHash(stateWhitelist[i]));

        for (int i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            region.whitelistHashes.Clear();
            for (int s = 0; s < region.stateWhitelist.Count; s++)
                if (!string.IsNullOrEmpty(region.stateWhitelist[s]))
                    region.whitelistHashes.Add(Animator.StringToHash(region.stateWhitelist[s]));

            region.bone = avatarAnimator.GetBoneTransform(region.targetBone);
            region.hoverLayerIndex = GetLayerIndexByName(region.hoverAnimationLayer);
            region.faceLayerIndex = GetLayerIndexByName(region.faceAnimationLayer);
            region.hasHoverBool = !string.IsNullOrEmpty(region.hoverAnimationParameter) && boolParams.Contains(Animator.StringToHash(region.hoverAnimationParameter));
            region.hasFaceBool = !string.IsNullOrEmpty(region.faceAnimationParameter) && boolParams.Contains(Animator.StringToHash(region.faceAnimationParameter));

            if (region.enableHoverObject && region.hoverObject)
            {
                var list = new List<HoverInstance>();
                for (int k = 0; k < 4; k++)
                {
                    var clone = Instantiate(region.hoverObject);
                    if (region.bindHoverObjectToBone && region.bone)
                    {
                        clone.transform.SetParent(region.bone, false);
                        clone.transform.localPosition = Vector3.zero;
                    }
                    clone.SetActive(false);
                    list.Add(new HoverInstance { obj = clone, despawnTime = -1f });
                }
                pool[region] = list;
            }
        }

        if (cachedBigScreen == null)
            cachedBigScreen = FindFirstObjectByType<AvatarBigScreenHandler>();
        if (cachedBigScreen != null && bigScreenFlag == null)
            bigScreenFlag = cachedBigScreen.GetType().GetField("isBigScreenActive", BindingFlags.NonPublic | BindingFlags.Instance);

        hasSetup = true;
    }

    void Update()
    {
        if (!hasSetup) TrySetup();
        if (cachedCamera == null || avatarAnimator == null) return;

        if (Time.time >= nextCheck)
        {
            if (cachedBigScreen == null && bigScreenFlag == null)
            {
                cachedBigScreen = FindFirstObjectByType<AvatarBigScreenHandler>();
                if (cachedBigScreen != null)
                    bigScreenFlag = cachedBigScreen.GetType().GetField("isBigScreenActive", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            bigScreenBlocked = false;
            if (cachedBigScreen != null && bigScreenFlag != null)
            {
                var v = bigScreenFlag.GetValue(cachedBigScreen) as bool?;
                bigScreenBlocked = v == true;
            }
            nextCheck = Time.time + checkInterval;
        }

        Vector2 mouse = Input.mousePosition;
        bool menuBlocked = MenuActions.IsReactionBlocked();
        bool occluded = blockWhenCovered && IsOccludedByOS();
        bool anyBlocked = menuBlocked || bigScreenBlocked || occluded;

        for (int r = 0; r < regions.Count; r++)
        {
            var region = regions[r];
            if (region.bone == null) continue;

            Vector3 world = region.bone.position + region.bone.TransformVector(region.offset) + region.worldOffset;
            Vector2 screen = cachedCamera.WorldToScreenPoint(world);

            float scale = region.bone.lossyScale.magnitude;
            float radius = region.hoverRadius * scale;
            Vector2 edge = cachedCamera.WorldToScreenPoint(world + cachedCamera.transform.right * radius);

            Vector2 diffMouse = mouse - screen;
            Vector2 diffEdge = screen - edge;
            float dist2 = diffMouse.sqrMagnitude;
            float radius2 = diffEdge.sqrMagnitude;
            bool hovering = dist2 <= radius2;

            bool genderAllowed = IsRegionAllowedByGender(region);
            bool stateOk = IsStateAllowedForRegion(region);
            
            if (hovering && !region.wasHovering && stateOk && !anyBlocked && genderAllowed)
            {
                bool allow = true;
                if (region.patMode) allow = ProcessPat(region, screen, mouse);
                if (allow)
                {
                    region.wasHovering = true;
                    TriggerAnim(region, true);
                    PlayRandomVoice(region);

                    if (GlobalHoverObjectsEnabled && region.enableHoverObject && region.hoverObject != null)
                    {
                        var list = pool[region];
                        HoverInstance chosen = null;
                        for (int i = 0; i < list.Count; i++)
                            if (!list[i].obj.activeSelf) { chosen = list[i]; break; }
                        if (chosen == null)
                        {
                            float oldest = float.MaxValue;
                            for (int i = 0; i < list.Count; i++)
                                if (list[i].despawnTime < oldest) { oldest = list[i].despawnTime; chosen = list[i]; }
                        }
                        if (chosen != null)
                        {
                            if (!region.bindHoverObjectToBone)
                                chosen.obj.transform.position = world;
                            chosen.obj.SetActive(false);
                            chosen.obj.SetActive(true);
                            chosen.despawnTime = Time.time + region.despawnAfterSeconds;
                        }
                    }
                }
            }
            else if ((!hovering || anyBlocked || !genderAllowed) && region.wasHovering)
            {
                region.wasHovering = false;
                TriggerAnim(region, false);
            }

            if (!hovering || anyBlocked)
                ResetPat(region);
        }

        foreach (var region in regions)
        {
            if (!region.enableHoverObject || !pool.ContainsKey(region)) continue;
            var list = pool[region];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].obj.activeSelf && Time.time >= list[i].despawnTime)
                {
                    list[i].obj.SetActive(false);
                    list[i].despawnTime = -1f;
                }
            }
        }
    }

    bool ProcessPat(VoiceRegion r, Vector2 center, Vector2 mouse)
    {
        float now = Time.time;
        if (now >= r.patExpireAt) ResetPat(r);
        float minMove = 4f;
        if (!r.patHasLast)
        {
            r.patHasLast = true;
            r.patLastPos = mouse;
            r.patLastMove = Vector2.zero;
            r.patLastAngle = Mathf.Atan2(mouse.y - center.y, mouse.x - center.x) * Mathf.Rad2Deg;
            r.patExpireAt = now + r.patResetTime;
            return false;
        }

        Vector2 delta = mouse - r.patLastPos;
        if (delta.sqrMagnitude >= minMove * minMove)
        {
            float ang = Mathf.Atan2(mouse.y - center.y, mouse.x - center.x) * Mathf.Rad2Deg;
            float dAng = Mathf.DeltaAngle(r.patLastAngle, ang);
            float radPrev = (r.patLastPos - center).magnitude;
            float radCurr = (mouse - center).magnitude;
            if (radPrev >= r.patMinRadius || radCurr >= r.patMinRadius)
                r.patCircleAccum += Mathf.Abs(dAng);

            if (r.patLastMove.sqrMagnitude > 0f)
            {
                float dot = Vector2.Dot(delta.normalized, r.patLastMove.normalized);
                if (dot < -0.2f) r.patWiggleChanges++;
            }
            r.patWiggleAccumDist += delta.magnitude;

            r.patLastAngle = ang;
            r.patLastMove = delta;
            r.patLastPos = mouse;
            r.patExpireAt = now + r.patResetTime;
        }

        bool circleOK = r.patCircleAccum >= r.patCircleDegrees;
        bool wiggleOK = r.patWiggleAccumDist >= r.patWiggleDistance && r.patWiggleChanges >= r.patWiggleDirectionChanges;

        if ((circleOK || wiggleOK) && now >= r.patCooldownUntil)
        {
            r.patCooldownUntil = now + r.patCooldown;
            ResetPat(r);
            return true;
        }
        return false;
    }

    void ResetPat(VoiceRegion r)
    {
        r.patHasLast = false;
        r.patLastPos = Vector2.zero;
        r.patLastMove = Vector2.zero;
        r.patLastAngle = 0f;
        r.patCircleAccum = 0f;
        r.patWiggleAccumDist = 0f;
        r.patWiggleChanges = 0;
        r.patExpireAt = Time.time + r.patResetTime;
    }

    void TriggerAnim(VoiceRegion region, bool state)
    {
        if (avatarAnimator == null) return;
        if (HasBoolHash(Animator.StringToHash("isCustomDancing")) && avatarAnimator.GetBool("isCustomDancing")) return;

        if (region.hasHoverBool)
            avatarAnimator.SetBool(region.hoverAnimationParameter, state);
        else if (!string.IsNullOrEmpty(region.hoverAnimationState))
            avatarAnimator.CrossFadeInFixedTime(region.hoverAnimationState, 0.1f, region.hoverLayerIndex);

        if (region.hasFaceBool)
            avatarAnimator.SetBool(region.faceAnimationParameter, state);
        else if (!string.IsNullOrEmpty(region.faceAnimationState))
            avatarAnimator.CrossFadeInFixedTime(region.faceAnimationState, 0.1f, region.faceLayerIndex);
    }

    void PlayRandomVoice(VoiceRegion region)
    {
        if (region.voiceClips.Count > 0 && !voiceAudioSource.isPlaying)
        {
            voiceAudioSource.clip = region.voiceClips[Random.Range(0, region.voiceClips.Count)];
            voiceAudioSource.Play();
        }
        if (region.enableLayeredSound && region.layeredVoiceClips.Count > 0)
            layeredAudioSource.PlayOneShot(region.layeredVoiceClips[Random.Range(0, region.layeredVoiceClips.Count)]);
    }

    bool IsStateAllowedForRegion(VoiceRegion region)
    {
        if (avatarAnimator == null) return false;
        var st = avatarAnimator.GetCurrentAnimatorStateInfo(0);
        int h = st.shortNameHash;
        if (region.whitelistHashes != null && region.whitelistHashes.Count > 0)
            return region.whitelistHashes.Contains(h);
        if (globalWhitelistHashes.Count > 0)
            return globalWhitelistHashes.Contains(h);
        return false;
    }

    bool IsRegionAllowedByGender(VoiceRegion region)
    {
        if (avatarAnimator == null) return true;
        if (!hasIsMaleParam) return true;
        bool isMale = avatarAnimator.GetFloat(isMaleHash) > 0.5f;
        return region.IsHusbando ? isMale : !isMale;
    }

    int GetLayerIndexByName(string layerName)
    {
        if (string.IsNullOrEmpty(layerName)) return 0;
        int count = avatarAnimator.layerCount;
        for (int i = 0; i < count; i++)
            if (avatarAnimator.GetLayerName(i) == layerName) return i;
        return 0;
    }

    bool HasBoolHash(int hash) => boolParams.Contains(hash);

    public void ResetAfterDance()
    {
        if (avatarAnimator == null) return;
        for (int i = 0; i < regions.Count; i++)
        {
            regions[i].wasHovering = false;
            if (regions[i].hasHoverBool)
                avatarAnimator.SetBool(regions[i].hoverAnimationParameter, false);
            if (regions[i].hasFaceBool)
                avatarAnimator.SetBool(regions[i].faceAnimationParameter, false);
        }
    }

    void ResolveWindowHandle()
    {
        if (_hwndCached) return;
        _unityHwnd = WindowManager.Instance.UnityWindow;
        _hwndCached = _unityHwnd != System.IntPtr.Zero;
    }

    bool IsOccludedByOS()
    {
        ResolveWindowHandle();
        if (_unityHwnd == IntPtr.Zero) return false;
        if (!WindowManager.Instance.GetMousePosition(out var p)) return false;
        var stackingList = WindowManager.Instance.GetClientStackingList();
        for (int i = stackingList.Count - 1; i >= 0; i--)
        {
            var win = stackingList[i];

            if (WindowManager.Instance.IsDesktop(win) || WindowManager.Instance.IsDock(win))
                continue;

            if (!WindowManager.Instance.GetWindowRect(win, out var rect))
                continue;

            if (!rect.Contains(p))
                continue;

            return win != _unityHwnd;
        }

        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || !cachedCamera || !avatarAnimator) return;
        for (int i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (!region.bone) continue;
            float scale = region.bone.lossyScale.magnitude;
            Vector3 center = region.bone.position + region.bone.TransformVector(region.offset) + region.worldOffset;
            Gizmos.color = region.gizmoColor;
            Gizmos.DrawWireSphere(center, region.hoverRadius * scale);
        }
    }
#endif
}
