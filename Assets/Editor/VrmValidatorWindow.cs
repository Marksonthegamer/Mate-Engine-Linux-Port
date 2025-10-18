#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using VRM;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Animations;
using System.IO;

public class VrmValidatorWindow : EditorWindow
{
    private GameObject vrmObject;
    private GameObject lastCheckedObject;

    private List<(string message, MessageType type)> messages = new();
    private string version = "Unknown";
    private string metaTitle = "";
    private string metaAuthor = "";
    private string[] shaders = new string[0];
    private string[] materials = new string[0];
    private int boneCount = 0;
    private int springCount = 0;
    private int meshCount = 0;
    private int polyCount = 0;
    private int blendshapeCount = 0;
    private Vector2 scrollPosition;

    private Dictionary<HumanBodyBones, string> boneMappings = new();
    private List<string> runtimeComponents = new();

    [MenuItem("MateEngine/ME VRM Validator")]
    public static void ShowWindow()
    {
        GetWindow<VrmValidatorWindow>("ME VRM Validator");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("MateEngine - VRM Validator", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        vrmObject = (GameObject)EditorGUILayout.ObjectField("VRM GameObject", vrmObject, typeof(GameObject), true);

        EditorGUI.BeginDisabledGroup(vrmObject == null);
        if (GUILayout.Button("Validate VRM"))
        {
            if (vrmObject != null)
            {
                RunValidation(vrmObject);
                lastCheckedObject = vrmObject;
            }
        }
        EditorGUI.EndDisabledGroup();

        if (vrmObject == null)
        {
            EditorGUILayout.HelpBox("Assign a VRM GameObject to validate.", MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("VRM Debug Info", EditorStyles.boldLabel);
        DrawSeparator();

        foreach (var (msg, type) in messages)
            EditorGUILayout.HelpBox("・" + msg, type);

        DrawSeparator();
        EditorGUILayout.LabelField("VRM Version", version);
        if (!string.IsNullOrEmpty(metaTitle)) EditorGUILayout.LabelField("Title", metaTitle);
        if (!string.IsNullOrEmpty(metaAuthor)) EditorGUILayout.LabelField("Author", metaAuthor);

        DrawSeparator();
        EditorGUILayout.LabelField("Shaders Used", string.Join(", ", shaders));
        EditorGUILayout.LabelField("Materials", string.Join(", ", materials));

        DrawSeparator();
        EditorGUILayout.LabelField("Bone Count", boneCount.ToString());
        EditorGUILayout.LabelField("Spring Bones", springCount.ToString());
        EditorGUILayout.LabelField("Mesh Count", meshCount.ToString());
        EditorGUILayout.LabelField("Total Triangles", polyCount.ToString());
        EditorGUILayout.LabelField("BlendShapes", blendshapeCount.ToString());

        DrawSeparator();
        EditorGUILayout.LabelField("Humanoid Bone Mappings", EditorStyles.boldLabel);
        foreach (var pair in boneMappings)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(pair.Key.ToString(), GUILayout.Width(160));
            EditorGUILayout.LabelField(pair.Value);
            EditorGUILayout.EndHorizontal();
        }

        DrawSeparator();
        EditorGUILayout.LabelField("Runtime VRM Components", EditorStyles.boldLabel);
        foreach (var comp in runtimeComponents)
        {
            EditorGUILayout.LabelField(comp);
        }

        EditorGUILayout.EndScrollView();
    }

    private void RunValidation(GameObject model)
    {
        messages.Clear();
        boneMappings.Clear();
        runtimeComponents.Clear();

        version = GetVrmVersion(model, out metaTitle, out metaAuthor);
        shaders = GetShaders(model);
        materials = GetMaterialNames(model);
        boneCount = GetBoneCount(model);
        springCount = GetSpringBoneCount(model);
        GetMeshStats(model, out meshCount, out polyCount);
        blendshapeCount = GetBlendshapeCount(model);
        FillHumanoidMapping(model);
        FillRuntimeComponents(model);

        // Armature check
        var hips = FindBoneByName(model.transform, "Hips");
        if (hips != null && hips.parent == model.transform)
            messages.Add(("Missing Armature/Root Bone! This VRM Model will work but the movement will be broken. Please Fix This!", MessageType.Error));
        else
            messages.Add(("Armature/Root Bone Setup is correct!", MessageType.Info));

        // Shader check (strict)
        var allRenderers = model.GetComponentsInChildren<Renderer>(true);
        List<string> nonMToonRenderers = new();
        foreach (var r in allRenderers)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat != null && mat.shader != null)
                {
                    string shaderName = mat.shader.name;
                    if (!shaderName.Contains("MToon"))
                    {
                        nonMToonRenderers.Add($"{r.name} uses {shaderName}");
                    }
                }
            }
        }

        if (nonMToonRenderers.Count > 0)
        {
            string errorMsg = "You don't use MToon or MToon10 on all materials! This model will not load properly.\n";
            errorMsg += "These renderers use invalid shaders:\n- " + string.Join("\n- ", nonMToonRenderers);
            messages.Add((errorMsg, MessageType.Error));
        }
        else
        {
            messages.Add(("Uses only MToon/MToon10 Shaders!", MessageType.Info));
        }

        // Poly analysis
        if (polyCount <= 80000)
            messages.Add(($"Very Optimized Model! It has {polyCount} polygons and will not have a big performance impact!", MessageType.Info));
        else if (polyCount <= 120000)
            messages.Add(($"Mildly Optimized, but high poly count of {polyCount}. It's okay, but consider improving it for better performance!", MessageType.Warning));
        else
            messages.Add(($"Very Badly Optimized Model! High poly count of {polyCount}. It will impact performance drastically! Please improve it!", MessageType.Error));

        // Total memory usage estimate: Meshes + Textures + Materials
        long totalBytes = 0;

        var meshes = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(m => m.sharedMesh != null)
            .Select(m => m.sharedMesh)
            .Distinct();

        foreach (var mesh in meshes)
            totalBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh);

        var textures = model.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null)
            .SelectMany(m => m.GetTexturePropertyNames().Select(prop => m.GetTexture(prop)))
            .Where(tex => tex != null)
            .Distinct();

        foreach (var tex in textures)
            totalBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(tex);

        var allMaterials = model.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null)
            .Distinct();

        foreach (var mat in allMaterials)
            totalBytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mat);

        float totalMB = totalBytes / (1024f * 1024f);
        if (totalMB <= 50f)
            messages.Add(($"Estimated total model size is {totalMB:F2}MB — Optimized!", MessageType.Info));
        else if (totalMB <= 75f)
            messages.Add(($"Estimated total model size is {totalMB:F2}MB — Acceptable but could be smaller.", MessageType.Warning));
        else
            messages.Add(($"Estimated total model size is {totalMB:F2}MB — Too large! Consider reducing texture sizes or mesh complexity.", MessageType.Error));
    }

    private void FillHumanoidMapping(GameObject obj)
    {
        var animator = obj.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            boneMappings.Add(HumanBodyBones.Hips, "Not Humanoid");
            return;
        }

        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone == HumanBodyBones.LastBone) continue;
            var t = animator.GetBoneTransform(bone);
            if (t != null)
            {
                boneMappings.Add(bone, t.name);
            }
        }
    }

    private void FillRuntimeComponents(GameObject obj)
    {
        var components = obj.GetComponents<MonoBehaviour>();
        foreach (var c in components)
        {
            if (c != null)
                runtimeComponents.Add(c.GetType().Name);
        }
    }

    private Transform FindBoneByName(Transform root, string boneName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == boneName)
                return t;
        }
        return null;
    }

    private string GetVrmVersion(GameObject obj, out string title, out string author)
    {
        title = "";
        author = "";

        var meta0 = obj.GetComponent<VRMMeta>();
        if (meta0 != null && meta0.Meta != null)
        {
            title = meta0.Meta.Title;
            author = meta0.Meta.Author;
            return "0.x";
        }

#if VRM10_EXISTS
        var meta10 = obj.GetComponent<VRM10.VRM10Meta>();
        if (meta10 != null)
        {
            title = meta10.Title;
            author = meta10.Author;
            return meta10.SpecVersion;
        }
#endif
        return "Unknown";
    }

    private string[] GetShaders(GameObject obj)
    {
        return obj.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null && m.shader != null)
            .Select(m => m.shader.name)
            .Distinct()
            .ToArray();
    }

    private string[] GetMaterialNames(GameObject obj)
    {
        return obj.GetComponentsInChildren<Renderer>(true)
            .SelectMany(r => r.sharedMaterials)
            .Where(m => m != null)
            .Select(m => m.name)
            .Distinct()
            .ToArray();
    }

    private int GetBoneCount(GameObject obj)
    {
        return obj.GetComponentsInChildren<Transform>(true).Length;
    }

    private int GetSpringBoneCount(GameObject obj)
    {
        int count = obj.GetComponentsInChildren<VRMSpringBone>(true).Length;
#if VRM10_EXISTS
        count += obj.GetComponentsInChildren<VRM10.VRM10SpringBone>(true).Length;
#endif
        return count;
    }

    private void GetMeshStats(GameObject obj, out int meshCount, out int polyCount)
    {
        meshCount = 0;
        polyCount = 0;

        var meshes = obj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        meshCount = meshes.Length;

        foreach (var smr in meshes)
        {
            if (smr.sharedMesh != null)
                polyCount += smr.sharedMesh.triangles.Length / 3;
        }
    }

    private int GetBlendshapeCount(GameObject obj)
    {
        return obj.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            .Where(m => m.sharedMesh != null)
            .Sum(m => m.sharedMesh.blendShapeCount);
    }

    private void DrawSeparator()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
    }
}
#endif
