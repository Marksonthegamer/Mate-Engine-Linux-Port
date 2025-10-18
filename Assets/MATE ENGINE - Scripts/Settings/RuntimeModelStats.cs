using UnityEngine;
using TMPro;
using System.Linq;
using VRM;
using UniVRM10;

public class RuntimeModelStats : MonoBehaviour
{
    [Header("UI Fields")]
    public TextMeshProUGUI polyText;
    public TextMeshProUGUI boneText;
    public TextMeshProUGUI vrmVersionText;
    public TextMeshProUGUI buildVersionText; // NEW FIELD

    [Header("Model Root Parent")]
    public Transform customModelRoot; // Assign the "Model" parent object

    [Header("Debug")]
    public bool liveUpdate = true; // Enable for frequent checks

    private GameObject currentCustomModel;
    private float updateInterval = 0.5f;
    private float timer = 0f;

    void Start()
    {
        // Set version label once at startup
        if (buildVersionText != null)
        {
            buildVersionText.text = "MEV: " + Application.version;
        }

        RefreshNow(); // First check at startup
    }

    void Update()
    {
        if (!liveUpdate) return;

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            RefreshNow();
        }
    }

    public void RefreshNow()
    {
        if (customModelRoot == null)
        {
            Debug.LogWarning("[RuntimeModelStats] Custom Model Root is not assigned.");
            return;
        }

        currentCustomModel = customModelRoot
            .GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t.name.Contains("CustomVRM") && t.name.Contains("Clone"))?.gameObject;

        if (currentCustomModel == null)
        {
            polyText.text = "Polys: -";
            boneText.text = "Bones: -";
            vrmVersionText.text = "VRM: -";
            return;
        }

        polyText.text = "Polys: " + GetPolyCount(currentCustomModel);
        boneText.text = "Bones: " + (HasProperArmature(currentCustomModel) ? "Perfect" : "Failure");
        vrmVersionText.text = "VRM: " + GetVrmVersion(currentCustomModel);
    }

    int GetPolyCount(GameObject model)
    {
        return model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                    .Where(m => m.sharedMesh != null)
                    .Sum(m => m.sharedMesh.triangles.Length / 3);
    }

    bool HasProperArmature(GameObject model)
    {
        var hips = FindBoneByName(model.transform, "Hips");
        return hips != null && hips.parent != model.transform;
    }

    Transform FindBoneByName(Transform root, string name)
    {
        return root.GetComponentsInChildren<Transform>(true)
                   .FirstOrDefault(t => t.name == name);
    }

    string GetVrmVersion(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        var shaders = renderers
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null && m.shader != null)
            .Select(m => m.shader.name)
            .Distinct()
            .ToList();

        bool usesMToon10 = shaders.Any(s => s.Contains("MToon10"));
        bool usesMToon = shaders.Any(s => s.Contains("MToon")) && !usesMToon10;

        if (usesMToon10) return "1.X";
        if (usesMToon) return "0.X";

        return "Unknown";
    }
}
