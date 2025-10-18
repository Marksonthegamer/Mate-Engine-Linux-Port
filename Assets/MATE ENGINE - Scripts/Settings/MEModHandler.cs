using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SFB;
using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

public class MEModHandler : MonoBehaviour
{
    [Header("UI References")]
    public Button loadModButton;
    public Transform modListContainer;
    public GameObject modEntryPrefab;

    string modFolderPath;
    List<ModEntry> loadedMods = new List<ModEntry>();

    void Start()
    {
        modFolderPath = Path.Combine(Application.persistentDataPath, "Mods");
        Directory.CreateDirectory(modFolderPath);

        loadModButton.onClick.AddListener(OpenFileDialogAndLoadMod);
        LoadAllModsInFolder();
    }

    void LoadAllModsInFolder()
    {
        foreach (var file in Directory.GetFiles(modFolderPath, "*.me"))
            LoadMod(file);
    }

    void OpenFileDialogAndLoadMod()
    {
        var ext = new[] { new ExtensionFilter("Mod Files", "me") };
        var paths = StandaloneFileBrowser.OpenFilePanel("Select Mod", ".", ext, false);
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            var dest = Path.Combine(modFolderPath, Path.GetFileName(paths[0]));
            File.Copy(paths[0], dest, true);
            LoadMod(dest);
        }
    }

    void LoadMod(string path)
    {
        string temp = Path.Combine(Application.temporaryCachePath, "ME_TempMod");
        if (Directory.Exists(temp)) Directory.Delete(temp, true);
        ZipFile.ExtractToDirectory(path, temp);

        string bundlePath = null;
        string modName = Path.GetFileNameWithoutExtension(path);

        string modInfoPath = Path.Combine(temp, "modinfo.json");
        string refPathJson = Path.Combine(temp, "reference_paths.json");
        string sceneLinksPath = Path.Combine(temp, "scene_links.json");

        Dictionary<string, string> refPaths = new();
        Dictionary<string, string> sceneLinks = new();

        // Load reference_paths.json
        if (File.Exists(refPathJson))
        {
            var json = File.ReadAllText(refPathJson);
            var obj = JsonUtility.FromJson<RefPathMap>(json);
            for (int i = 0; i < obj.keys.Count; i++)
                refPaths[obj.keys[i]] = obj.values[i];
        }

        // Load scene_links.json
        if (File.Exists(sceneLinksPath))
        {
            var json = File.ReadAllText(sceneLinksPath);
            var obj = JsonUtility.FromJson<SceneLinkMap>(json);
            for (int i = 0; i < obj.keys.Count; i++)
                sceneLinks[obj.keys[i]] = obj.values[i];
        }

        // Load bundle
        foreach (var file in Directory.GetFiles(temp, "*.bundle"))
            bundlePath = file;

        if (!string.IsNullOrEmpty(bundlePath) && File.Exists(bundlePath))
        {
            var bundle = AssetBundle.LoadFromFile(bundlePath);
            if (bundle == null)
            {
                Debug.LogError("[MEModHandler] Failed to load AssetBundle: " + bundlePath);
                return;
            }

            var prefab = bundle.LoadAsset<GameObject>(modName);
            if (prefab == null)
            {
                Debug.LogError("[MEModHandler] Could not find prefab named " + modName + " inside bundle.");
                return;
            }

            var instance = Instantiate(prefab);
            bundle.Unload(false);

            ApplyReferencePaths(instance, refPaths, sceneLinks);

            var entry = new ModEntry { name = modName, instance = instance, localPath = path };
            loadedMods.Add(entry);
            AddToModListUI(entry);
            return;
        }

        Debug.LogWarning($"[MEModHandler] Unsupported mod format: {path}");
    }

    void ApplyReferencePaths(GameObject root, Dictionary<string, string> refPaths, Dictionary<string, string> sceneLinks)
    {
        var allBehaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (var mb in allBehaviours)
        {
            if (mb == null) continue;
            Type type = mb.GetType();
            string typeName = type.Name;

            foreach (var map in new[] { refPaths, sceneLinks })
            {
                foreach (var kv in map)
                {
                    if (!kv.Key.StartsWith(typeName + ".")) continue;

                    string rawPath = kv.Key.Substring(typeName.Length + 1);
                    GameObject sceneGO = GameObject.Find(kv.Value);
                    if (sceneGO == null) continue;

                    object current = mb;
                    Type currentType = type;

                    var parts = rawPath.Split('.');
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string part = parts[i];
                        int listIndex = -1;

                        // Listenindex erkennen
                        if (part.Contains("["))
                        {
                            int start = part.IndexOf('[');
                            int end = part.IndexOf(']');
                            listIndex = int.Parse(part.Substring(start + 1, end - start - 1));
                            part = part.Substring(0, start);
                        }

                        FieldInfo field = currentType.GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (field == null) break;

                        bool isLast = (i == parts.Length - 1);

                        if (isLast)
                        {
                            if (field.FieldType == typeof(GameObject))
                                field.SetValue(current, sceneGO);
                            else if (typeof(Component).IsAssignableFrom(field.FieldType))
                            {
                                var comp = sceneGO.GetComponent(field.FieldType);
                                if (comp != null) field.SetValue(current, comp);
                            }
                        }
                        else
                        {
                            object next = field.GetValue(current);
                            if (next == null) break;

                            if (listIndex >= 0 && next is IList list)
                            {
                                if (listIndex >= list.Count) break;
                                current = list[listIndex];
                            }
                            else
                            {
                                current = next;
                            }

                            if (current == null) break;
                            currentType = current.GetType();
                        }
                    }
                }
            }
        }
    }



    [Serializable]
    class RefPathMap
    {
        public List<string> keys = new();
        public List<string> values = new();
    }

    [Serializable]
    class SceneLinkMap
    {
        public List<string> keys = new();
        public List<string> values = new();
    }


    void AddToModListUI(ModEntry mod)
    {
        var entry = Instantiate(modEntryPrefab, modListContainer);
        entry.name = "Mod_" + mod.name;
        var nt = entry.transform.Find("ModNameText")?.GetComponent<TextMeshProUGUI>();
        if (nt != null) nt.text = mod.name;

        var tog = entry.GetComponentInChildren<Toggle>(true);
        if (tog != null)
        {
            var isOn = true;
            if (SaveLoadHandler.Instance.data.modStates.TryGetValue(mod.name, out var s)) isOn = s;
            tog.isOn = isOn;
            mod.instance.SetActive(isOn);
            tog.onValueChanged.AddListener(a => {
                mod.instance.SetActive(a);
                SaveLoadHandler.Instance.data.modStates[mod.name] = a;
                SaveLoadHandler.Instance.SaveToDisk();
            });
        }

        var btn = entry.GetComponentInChildren<Button>(true);
        if (btn != null) btn.onClick.AddListener(() => RemoveMod(mod, entry));
        var hueShifter = FindFirstObjectByType<MenuHueShift>();
        if (hueShifter != null)
            hueShifter.RefreshNewGraphicsAndSelectables(entry.transform);
    }

    void RemoveMod(ModEntry mod, GameObject ui)
    {
        Destroy(mod.instance);
        if (File.Exists(mod.localPath)) File.Delete(mod.localPath);
        loadedMods.Remove(mod);
        Destroy(ui);
    }

    private void SetNestedField(object obj, string fieldPath, string raw, Dictionary<string, GameObject> lookup)
    {
        // ... (dein bestehender Feldsetter bleibt hier unverändert)
    }

    object ParseValue(string raw, Type ft, Dictionary<string, GameObject> lookup)
    {
        // ... (dein bestehender Parser bleibt hier ebenfalls gleich)
        return null;
    }

    Type ResolveType(string name)
    {
        var t = Type.GetType(name);
        if (t != null) return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            if ((t = asm.GetType(name)) != null) return t;
        return null;
    }

    [Serializable] class ModEntry { public string name; public GameObject instance; public string localPath; }
    [Serializable] class ObjectInfo { public string name, path; public List<string> components; }
    [Serializable] class ObjectList { public List<ObjectInfo> objects; }
    [Serializable] class FieldValue { public string objectPath, componentType, fieldName, value; }
    [Serializable] class FieldList { public List<FieldValue> fields; }
}
