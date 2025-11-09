using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using X11;
using Random = UnityEngine.Random;

public class PetVoiceReactionHandler : MonoBehaviour
{
    [Serializable]
    public class VoiceRegion
    {
        public string name;
        public bool IsHusbando;
        public HumanBodyBones targetBone;
        public Vector3 offset, worldOffset;
        public float hoverRadius = 50f;
        public Color gizmoColor = new(1f, 0.5f, 0f, 0.25f);
        public List<AudioClip> voiceClips = new();
        public AnimationClip hoverAnimation, faceAnimation;
        public bool enableHoverObject, bindHoverObjectToBone, enableLayeredSound;
        public GameObject hoverObject;
        [Range(0.1f, 10f)] public float despawnAfterSeconds = 5f;
        public List<AudioClip> layeredVoiceClips = new();
        [HideInInspector] public bool wasHovering;
        [HideInInspector] public Transform bone;
    }


    class HoverInstance { public GameObject obj; public float despawnTime; }

    public static bool GlobalHoverObjectsEnabled = true;
    public Animator avatarAnimator;
    public List<VoiceRegion> regions = new();
    [Header("Source Settings")]
    public AudioSource voiceAudioSource, layeredAudioSource;
    public string hoverTriggerParam = "HoverTrigger", hoverFaceTriggerParam = "HoverFaceTrigger";
    public bool showDebugGizmos = true;
    [SerializeField] public List<string> stateWhitelist = new();

    Camera cachedCamera;
    readonly Dictionary<VoiceRegion, List<HoverInstance>> pool = new();
    AnimatorOverrideController overrideController;
    RuntimeAnimatorController lastController;
    bool hasSetup;

    static readonly int isMaleHash = Animator.StringToHash("isMale");

    void Start() { if (!hasSetup) TrySetup(); }
    public void SetAnimator(Animator a) { avatarAnimator = a; hasSetup = false; }

    void TrySetup()
    {
        if (!avatarAnimator) return;
        if (!voiceAudioSource) voiceAudioSource = gameObject.AddComponent<AudioSource>();
        if (!layeredAudioSource) layeredAudioSource = gameObject.AddComponent<AudioSource>();
        cachedCamera = Camera.main;

        var baseCtrl = avatarAnimator.runtimeAnimatorController;
        if (!baseCtrl) return;

        if (overrideController == null || baseCtrl != lastController)
        {
            overrideController = baseCtrl is AnimatorOverrideController oc ? oc : new AnimatorOverrideController(baseCtrl);
            avatarAnimator.runtimeAnimatorController = overrideController;
            lastController = baseCtrl;
        }

        foreach (var region in regions)
        {
            region.bone = avatarAnimator.GetBoneTransform(region.targetBone);
            if (region.enableHoverObject && region.hoverObject)
            {
                var list = new List<HoverInstance>();
                for (int i = 0; i < 4; i++)
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
        hasSetup = true;
    }

    void Update()
    {
        if (!hasSetup) TrySetup();
        if (cachedCamera == null || avatarAnimator == null) return;

        Vector2 mouse = X11Manager.Instance.GetMousePosition();

        bool menuBlocked = MenuActions.IsReactionBlocked();
        bool bigScreenBlocked = false;
        var bigScreen = FindFirstObjectByType<AvatarBigScreenHandler>();
        if (bigScreen != null)
        {
            var isBig = bigScreen.GetType()
                .GetField("isBigScreenActive", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(bigScreen) as bool?;
            bigScreenBlocked = isBig == true;
        }

        bool anyBlocked = menuBlocked || bigScreenBlocked;

        for (int r = 0; r < regions.Count; r++)
        {
            var region = regions[r];
            if (region.bone == null) continue;

            Vector3 world = region.bone.position + region.bone.TransformVector(region.offset) + region.worldOffset;
            Vector2 screen = cachedCamera.WorldToScreenPoint(world);
            float scale = region.bone.lossyScale.magnitude;
            float radius = region.hoverRadius * scale;
            Vector2 edge = cachedCamera.WorldToScreenPoint(world + cachedCamera.transform.right * radius);
            float screenRadius = Vector2.Distance(screen, edge);
            float dist = Vector2.Distance(mouse, screen);
            bool hovering = dist <= screenRadius;

            bool genderAllowed = IsRegionAllowedByGender(region);

            if (hovering && !region.wasHovering && IsStateAllowed() && !anyBlocked && genderAllowed)
            {
                region.wasHovering = true;
                TriggerAnim(region, true);
                PlayRandomVoice(region);

                if (GlobalHoverObjectsEnabled && region.enableHoverObject && region.hoverObject != null)
                {
                    var list = pool[region];
                    HoverInstance chosen = null;

                    for (int i = 0; i < list.Count; i++)
                    {
                        if (!list[i].obj.activeSelf)
                        {
                            chosen = list[i];
                            break;
                        }
                    }

                    if (chosen == null)
                    {
                        float oldest = float.MaxValue;
                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i].despawnTime < oldest)
                            {
                                oldest = list[i].despawnTime;
                                chosen = list[i];
                            }
                        }
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
            else if ((!hovering || anyBlocked || !genderAllowed) && region.wasHovering)
            {
                region.wasHovering = false;
                TriggerAnim(region, false);
            }
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

    void TriggerAnim(VoiceRegion region, bool state)
    {
        if (region.hoverAnimation && overrideController != null)
        {
            overrideController["HoverReaction"] = region.hoverAnimation;
            avatarAnimator.SetBool(hoverTriggerParam, state);
        }
        if (region.faceAnimation && overrideController != null)
        {
            overrideController["HoverFace"] = region.faceAnimation;
            avatarAnimator.SetBool(hoverFaceTriggerParam, state);
        }
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

    bool IsStateAllowed()
    {
        if (avatarAnimator == null || stateWhitelist == null || stateWhitelist.Count == 0)
            return false;
        var currentState = avatarAnimator.GetCurrentAnimatorStateInfo(0);
        foreach (var allowed in stateWhitelist)
            if (!string.IsNullOrEmpty(allowed) && currentState.IsName(allowed))
                return true;
        return false;
    }

    bool IsRegionAllowedByGender(VoiceRegion region)
    {
        if (avatarAnimator == null) return true;
        if (!HasParam(isMaleHash)) return true;
        bool isMale = avatarAnimator.GetFloat(isMaleHash) > 0.5f;
        return region.IsHusbando ? isMale : !isMale;
    }

    bool HasParam(int hash)
    {
        var ps = avatarAnimator.parameters;
        for (int i = 0; i < ps.Length; i++)
            if (ps[i].nameHash == hash) return true;
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || !cachedCamera || !avatarAnimator) return;
        foreach (var region in regions)
        {
            if (!region.bone) continue;
            float scale = region.bone.lossyScale.magnitude;
            Vector3 center = region.bone.position + region.bone.TransformVector(region.offset) + region.worldOffset;
            Gizmos.color = region.gizmoColor;
            Gizmos.DrawWireSphere(center, region.hoverRadius * scale);
        }
    }
#endif
}

public static class ListExt
{
    public static T MinByOrDefault<T, TKey>(this List<T> list, Func<T, TKey> selector) where TKey : IComparable<TKey>
    {
        if (list == null || list.Count == 0) return default;
        int minIdx = 0;
        TKey min = selector(list[0]);
        for (int i = 1; i < list.Count; i++)
        {
            TKey val = selector(list[i]);
            if (val.CompareTo(min) < 0) { min = val; minIdx = i; }
        }
        return list[minIdx];
    }
}
