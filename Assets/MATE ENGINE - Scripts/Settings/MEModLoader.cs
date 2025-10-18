using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.IO;

public class MEModLoader : MonoBehaviour
{
    public static MEModLoader Instance;

    [Header("Required")]
    public ChibiToggle chibiToggle;

    [Header("Optional")]
    public AvatarDragSoundHandler dragSoundHandler;
    public PetVoiceReactionHandler petVoiceHandler;

    private string enterFolder;
    private string exitFolder;
    private string chibiSettingsPath;

    private string dragFolder;
    private string placeFolder;

    private string hoverReactionsFolder;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        string chibiBase = Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Chibi Mode/Sounds");
        enterFolder = Path.Combine(chibiBase, "Enter Sounds");
        exitFolder = Path.Combine(chibiBase, "Exit Sounds");
        chibiSettingsPath = Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Chibi Mode/settings.json");

        string dragBase = Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Drag Mode/Sounds");
        dragFolder = Path.Combine(dragBase, "Drag Sounds");
        placeFolder = Path.Combine(dragBase, "Place Sounds");

        hoverReactionsFolder = Path.Combine(Application.streamingAssetsPath, "Mods/ModLoader/Hover Reactions");

        EnsureFolderStructure();

        var currentAvatar = FindCurrentActiveAvatar();
        if (currentAvatar != null)
            AssignHandlersForCurrentAvatar(currentAvatar);

        StartCoroutine(LoadChibiSounds());
        StartCoroutine(LoadDragSounds());
        StartCoroutine(ApplyChibiSettings());
        StartCoroutine(LoadHoverReactionSounds());
    }

    public void AssignHandlersForCurrentAvatar(GameObject avatar)
    {
        if (avatar == null)
            return;

        chibiToggle = avatar.GetComponentInChildren<ChibiToggle>(true);
        dragSoundHandler = avatar.GetComponentInChildren<AvatarDragSoundHandler>(true);
        petVoiceHandler = avatar.GetComponentInChildren<PetVoiceReactionHandler>(true);

        StartCoroutine(LoadChibiSounds());
        StartCoroutine(LoadDragSounds());
        StartCoroutine(LoadHoverReactionSounds());
        StartCoroutine(ApplyChibiSettings());
    }

    private GameObject FindCurrentActiveAvatar()
    {
        var modelRoot = GameObject.Find("Model");
        if (modelRoot == null)
            return null;
        foreach (Transform child in modelRoot.transform)
        {
            if (child.gameObject.activeInHierarchy)
                return child.gameObject;
        }
        return null;
    }

    private void EnsureFolderStructure()
    {
        TryCreateDirectory(enterFolder);
        TryCreateDirectory(exitFolder);
        TryCreateDirectory(dragFolder);
        TryCreateDirectory(placeFolder);
        TryCreateDirectory(hoverReactionsFolder);
    }

    private void TryCreateDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        string keep = Path.Combine(path, ".keep");
        if (!File.Exists(keep))
            File.WriteAllText(keep, "Keeps folder in build.");
    }

    private IEnumerator LoadChibiSounds()
    {
        List<AudioClip> enterSounds = new List<AudioClip>();
        List<AudioClip> exitSounds = new List<AudioClip>();

        if (!Directory.Exists(enterFolder)) yield break;
        foreach (string file in Directory.GetFiles(enterFolder))
            yield return LoadClip(file, clip => enterSounds.Add(clip));

        if (!Directory.Exists(exitFolder)) yield break;
        foreach (string file in Directory.GetFiles(exitFolder))
            yield return LoadClip(file, clip => exitSounds.Add(clip));

        if (chibiToggle != null)
        {
            if (enterSounds.Count > 0)
                chibiToggle.chibiEnterSounds = enterSounds;
            if (exitSounds.Count > 0)
                chibiToggle.chibiExitSounds = exitSounds;
        }
    }

    private IEnumerator LoadDragSounds()
    {
        if (dragSoundHandler == null) yield break;

        List<AudioClip> dragClips = new List<AudioClip>();
        List<AudioClip> placeClips = new List<AudioClip>();

        if (Directory.Exists(dragFolder))
        {
            foreach (string file in Directory.GetFiles(dragFolder))
                yield return LoadClip(file, clip => dragClips.Add(clip));
        }

        if (Directory.Exists(placeFolder))
        {
            foreach (string file in Directory.GetFiles(placeFolder))
                yield return LoadClip(file, clip => placeClips.Add(clip));
        }

        if (dragClips.Count > 0)
            dragSoundHandler.dragStartSound = CreateRandomAudioSource(dragClips, "DragStart");

        if (placeClips.Count > 0)
            dragSoundHandler.dragStopSound = CreateRandomAudioSource(placeClips, "DragStop");
    }

    private IEnumerator LoadHoverReactionSounds()
    {
        if (petVoiceHandler == null || petVoiceHandler.regions == null)
            yield break;

        foreach (var region in petVoiceHandler.regions)
        {
            string regionName = string.IsNullOrWhiteSpace(region.name)
                ? region.targetBone.ToString()
                : region.name;

            string regionFolder = Path.Combine(hoverReactionsFolder, regionName);
            string voiceClipsFolder = Path.Combine(regionFolder, "Voice Clips");
            string layeredClipsFolder = Path.Combine(regionFolder, "Layered Voice Clips");

            TryCreateDirectory(regionFolder);
            TryCreateDirectory(voiceClipsFolder);
            TryCreateDirectory(layeredClipsFolder);

            List<AudioClip> voiceClips = new List<AudioClip>();
            List<AudioClip> layeredClips = new List<AudioClip>();

            if (Directory.Exists(voiceClipsFolder))
            {
                foreach (string file in Directory.GetFiles(voiceClipsFolder))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".wav" || ext == ".mp3" || ext == ".ogg")
                        yield return LoadClip(file, clip => { if (clip != null) voiceClips.Add(clip); });
                }
            }

            if (Directory.Exists(layeredClipsFolder))
            {
                foreach (string file in Directory.GetFiles(layeredClipsFolder))
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext == ".wav" || ext == ".mp3" || ext == ".ogg")
                        yield return LoadClip(file, clip => { if (clip != null) layeredClips.Add(clip); });
                }
            }

            if (voiceClips.Count > 0)
            {
                region.voiceClips.Clear();
                region.voiceClips.AddRange(voiceClips);
            }

            if (layeredClips.Count > 0)
            {
                region.layeredVoiceClips.Clear();
                region.layeredVoiceClips.AddRange(layeredClips);
            }

        }

        Debug.Log("[MEModLoader] Hover reaction sounds loaded.");
    }

    private IEnumerator LoadClip(string filePath, System.Action<AudioClip> onSuccess)
    {
        string ext = Path.GetExtension(filePath).ToLower();
        if (ext != ".wav" && ext != ".mp3" && ext != ".ogg") yield break;

        string url = "file://" + filePath;

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, GetAudioType(ext)))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
                Debug.LogWarning($"[MEModLoader] Failed to load sound: {filePath} | {www.error}");
            else
                onSuccess?.Invoke(DownloadHandlerAudioClip.GetContent(www));
        }
    }

    private AudioType GetAudioType(string extension)
    {
        switch (extension)
        {
            case ".mp3": return AudioType.MPEG;
            case ".ogg": return AudioType.OGGVORBIS;
            case ".wav": return AudioType.WAV;
            default: return AudioType.UNKNOWN;
        }
    }

    private AudioSource CreateRandomAudioSource(List<AudioClip> clips, string label)
    {
        GameObject soundObj = new GameObject($"DynamicSoundPlayer_{label}");
        soundObj.transform.SetParent(this.transform);
        AudioSource source = soundObj.AddComponent<AudioSource>();
        source.playOnAwake = false;
        StartCoroutine(RandomizeClipEveryFrame(source, clips));
        return source;
    }

    private IEnumerator RandomizeClipEveryFrame(AudioSource source, List<AudioClip> clips)
    {
        while (true)
        {
            if (!source.isPlaying && clips.Count > 0)
                source.clip = clips[Random.Range(0, clips.Count)];

            yield return null;
        }
    }

    private IEnumerator ApplyChibiSettings()
    {
        if (!File.Exists(chibiSettingsPath))
        {
            Debug.Log("[MEModLoader] No Chibi settings.json found, skipping.");
            yield break;
        }

        string json = File.ReadAllText(chibiSettingsPath);
        if (string.IsNullOrEmpty(json)) yield break;

        ChibiSettingsData settings;
        try
        {
            settings = JsonUtility.FromJson<ChibiSettingsData>(json);
        }
        catch
        {
            Debug.LogWarning("[MEModLoader] Failed to parse Chibi settings.json.");
            yield break;
        }

        if (chibiToggle != null)
        {
            chibiToggle.chibiArmatureScale = settings.chibiArmatureScale;
            chibiToggle.chibiHeadScale = settings.chibiHeadScale;
            chibiToggle.chibiUpperLegScale = settings.chibiUpperLegScale;
        }

        Debug.Log("[MEModLoader] Applied Chibi settings from JSON.");
    }
}

[System.Serializable]
public class ChibiSettingsData
{
    public Vector3 chibiArmatureScale = new Vector3(0.3f, 0.3f, 0.3f);
    public Vector3 chibiHeadScale = new Vector3(2.7f, 2.7f, 2.7f);
    public Vector3 chibiUpperLegScale = new Vector3(0.6f, 0.6f, 0.6f);
    public float screenInteractionRadius = 30f;
    public float holdDuration = 2f;
}
