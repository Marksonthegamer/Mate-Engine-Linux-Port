using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VoicePack : MonoBehaviour
{
    // ---- Drag (wie gehabt) ----
    [Header("AvatarDragSoundHandler (nur Clips)")]
    public AudioClip dragStartClip;
    public AudioClip dragStopClip;

    // ---- Pet Overrides ----
    [Header("PetVoiceReactionHandler: Region-Overrides")]
    public List<PetRegionOverride> petRegionOverrides = new();

    [Header("Scope & Timing")]
    public bool applyOnEnable = true;
    public bool revertOnDisable = false;

    [Tooltip("Neue/geladene Avatare automatisch erkennen und sofort anwenden.")]
    public bool autoWatchForNewHandlers = true;

    [Tooltip("Animator automatisch zuweisen, falls im Handler leer.")]
    public bool autoBindAnimator = true;

    [Tooltip("Wenn State-Whitelist eines Handlers leer ist, automatisch sinnvolle Einträge ergänzen.")]
    public bool fixEmptyStateWhitelist = true;

    [Tooltip("Scan-Intervall in Sekunden für autoWatchForNewHandlers.")]
    [Range(0.1f, 5f)] public float watchInterval = 0.5f;

    // --- intern ---
    private readonly List<(AudioSource src, AudioClip original)> _dragOriginals = new();
    private readonly List<PetRegionSnapshot> _petOriginals = new();
    private readonly HashSet<int> _processedPet = new();   // instanceIDs, die bereits gebunden/überschrieben wurden
    private readonly HashSet<int> _processedDrag = new();

    private bool _applied;
    private Coroutine _watcher;

    // ===== Datenstrukturen (wie zuvor) =====
    public enum MappingMode { ReplaceAll, MatchExistingCount_Cycle }

    [System.Serializable]
    public class PetRegionOverride
    {
        public string regionName;
        public int fallbackRegionIndex = -1;

        public bool overrideVoiceClips = true;
        public MappingMode voiceMapping = MappingMode.MatchExistingCount_Cycle;
        public List<AudioClip> voiceClips = new();

        public bool overrideLayeredClips = true;
        public MappingMode layeredMapping = MappingMode.MatchExistingCount_Cycle;
        public List<AudioClip> layeredClips = new();
    }

    private class PetRegionSnapshot
    {
        public PetVoiceReactionHandler handler;
        public int regionIndex;
        public List<AudioClip> origVoice;
        public List<AudioClip> origLayered;
    }

    // ===== Lifecycle =====
    void OnEnable()
    {
        if (applyOnEnable) Apply();
        if (autoWatchForNewHandlers)
            _watcher = StartCoroutine(WatchForNewHandlers());
    }

    void OnDisable()
    {
        if (_watcher != null) { StopCoroutine(_watcher); _watcher = null; }
        _processedPet.Clear();
        _processedDrag.Clear();

        if (revertOnDisable) Revert();
    }

    [ContextMenu("Apply VoicePack Now")]
    public void Apply()
    {
        // einmaliger Pass über bereits vorhandene Instanzen
#if UNITY_2023_1_OR_NEWER
        var dragHandlers = FindObjectsByType<AvatarDragSoundHandler>(FindObjectsSortMode.None);
        var petHandlers = FindObjectsByType<PetVoiceReactionHandler>(FindObjectsSortMode.None);
#else
        var dragHandlers = FindObjectsOfType<AvatarDragSoundHandler>(true);
        var petHandlers  = FindObjectsOfType<PetVoiceReactionHandler>(true);
#endif
        ApplyDragOverridesTo(dragHandlers);
        ApplyPetOverridesTo(petHandlers);

        _applied = true;
    }

    [ContextMenu("Revert VoicePack")]
    public void Revert()
    {
        foreach (var (src, orig) in _dragOriginals) if (src) src.clip = orig;
        _dragOriginals.Clear();

        foreach (var snap in _petOriginals)
        {
            if (!snap.handler) continue;
            var regions = snap.handler.regions;
            if (regions == null || snap.regionIndex < 0 || snap.regionIndex >= regions.Count) continue;
            var r = regions[snap.regionIndex];
            r.voiceClips = new List<AudioClip>(snap.origVoice ?? new List<AudioClip>());
            r.layeredVoiceClips = new List<AudioClip>(snap.origLayered ?? new List<AudioClip>());
        }
        _petOriginals.Clear();

        _applied = false;
    }

    // ===== Auto-Watcher =====
    private IEnumerator WatchForNewHandlers()
    {
        var wait = new WaitForSeconds(watchInterval);
        while (enabled)
        {
#if UNITY_2023_1_OR_NEWER
            var drags = FindObjectsByType<AvatarDragSoundHandler>(FindObjectsSortMode.None);
            var pets = FindObjectsByType<PetVoiceReactionHandler>(FindObjectsSortMode.None);
#else
            var drags = FindObjectsOfType<AvatarDragSoundHandler>(true);
            var pets  = FindObjectsOfType<PetVoiceReactionHandler>(true);
#endif
            // neue Drag-Handler
            foreach (var d in drags)
            {
                int id = d.GetInstanceID();
                if (_processedDrag.Contains(id)) continue;
                ApplyDragOverridesTo(new[] { d });
                _processedDrag.Add(id);
            }

            // neue Pet-Handler
            foreach (var p in pets)
            {
                int id = p.GetInstanceID();
                if (_processedPet.Contains(id)) continue;

                // Animator binden (wichtig!)
                if (autoBindAnimator && p.avatarAnimator == null)
                {
                    var anim = p.GetComponentInParent<Animator>();
                    if (anim) p.SetAnimator(anim);
                }

                // leere Whitelist reparieren (optional)
                if (fixEmptyStateWhitelist)
                    EnsureStateWhitelistNotEmpty(p);

                ApplyPetOverridesTo(new[] { p });
                _processedPet.Add(id);
            }

            yield return wait;
        }
    }

    // ===== Drag =====
    private void ApplyDragOverridesTo(AvatarDragSoundHandler[] handlers)
    {
        if (handlers == null || handlers.Length == 0) return;
        if (!_applied) _dragOriginals.Clear();

        foreach (var h in handlers)
        {
            if (dragStartClip && h.dragStartSound)
            {
                if (!_applied) _dragOriginals.Add((h.dragStartSound, h.dragStartSound.clip));
                h.dragStartSound.clip = dragStartClip;
                h.dragStartSound.playOnAwake = false;
            }
            if (dragStopClip && h.dragStopSound)
            {
                if (!_applied) _dragOriginals.Add((h.dragStopSound, h.dragStopSound.clip));
                h.dragStopSound.clip = dragStopClip;
                h.dragStopSound.playOnAwake = false;
            }
        }
    }

    // ===== Pet =====
    private void ApplyPetOverridesTo(PetVoiceReactionHandler[] petHandlers)
    {
        if (petHandlers == null || petHandlers.Length == 0) return;
        if (!_applied) _petOriginals.Clear();

        foreach (var ph in petHandlers)
        {
            if (autoBindAnimator && ph.avatarAnimator == null)
            {
                var anim = ph.GetComponentInParent<Animator>();
                if (anim) ph.SetAnimator(anim);
            }

            var regions = ph.regions;
            if (regions == null || regions.Count == 0) continue;

            foreach (var ov in petRegionOverrides)
            {
                int idx = ResolveRegionIndex(regions, ov);
                if (idx < 0 || idx >= regions.Count) continue;

                var r = regions[idx];

                if (!_applied)
                {
                    _petOriginals.Add(new PetRegionSnapshot
                    {
                        handler = ph,
                        regionIndex = idx,
                        origVoice = new List<AudioClip>(r.voiceClips ?? new List<AudioClip>()),
                        origLayered = new List<AudioClip>(r.layeredVoiceClips ?? new List<AudioClip>()),
                    });
                }

                if (ov.overrideVoiceClips && ov.voiceClips != null && ov.voiceClips.Count > 0)
                {
                    r.voiceClips = BuildMappedList(
                        ov.voiceClips,
                        r.voiceClips != null ? r.voiceClips.Count : ov.voiceClips.Count,
                        ov.voiceMapping
                    );
                }
                if (ov.overrideLayeredClips && ov.layeredClips != null && ov.layeredClips.Count > 0)
                {
                    r.layeredVoiceClips = BuildMappedList(
                        ov.layeredClips,
                        r.layeredVoiceClips != null ? r.layeredVoiceClips.Count : ov.layeredClips.Count,
                        ov.layeredMapping
                    );
                }
            }
        }
    }

    private static int ResolveRegionIndex(List<PetVoiceReactionHandler.VoiceRegion> regions, PetRegionOverride ov)
    {
        if (!string.IsNullOrEmpty(ov.regionName))
        {
            for (int i = 0; i < regions.Count; i++)
                if (string.Equals(regions[i].name, ov.regionName, System.StringComparison.OrdinalIgnoreCase))
                    return i;
        }
        return ov.fallbackRegionIndex;
    }

    private static List<AudioClip> BuildMappedList(List<AudioClip> source, int existingCount, MappingMode mode)
    {
        if (mode == MappingMode.ReplaceAll) return new(source);
        int count = Mathf.Max(existingCount, source.Count);
        var outList = new List<AudioClip>(count);
        for (int i = 0; i < count; i++) outList.Add(source[i % source.Count]);
        return outList;
    }

    // === Whitelist-Heuristik ===
    private void EnsureStateWhitelistNotEmpty(PetVoiceReactionHandler p)
    {
        if (p == null) return;
        var listField = p.stateWhitelist; // ist [SerializeField]
        if (listField != null && listField.Count > 0) return;

        var anim = p.avatarAnimator ? p.avatarAnimator : p.GetComponentInParent<Animator>();
        if (!anim) return;

        // 1) aktuelle Clips einsammeln (oft == State-Namen in vielen Controllern)
        var names = new HashSet<string>();
        var infos = anim.GetCurrentAnimatorClipInfo(0);
        foreach (var ci in infos)
            if (ci.clip) names.Add(ci.clip.name);

        // 2) gängige Fallbacks ergänzen
        names.Add("Idle");
        names.Add("Base Layer.Idle");
        names.Add("Locomotion");
        names.Add("Base Layer.Locomotion");

        // 3) zurückschreiben
        p.stateWhitelist = new List<string>(names);
    }
}
