using UnityEngine;

[ExecuteInEditMode]
[AddComponentMenu("Image Effects/MateEngine/Cavity (BiRP)")]
public class MECavityEffect : MonoBehaviour
{
    public enum BlendMode { Add = 0, Overlay = 1, Darken = 2 }

    public Shader shader;
    private Material mat;

    [Range(0.25f, 5f)] public float radius = 1.2f;
    [Range(1, 32)] public int samples = 12;
    [Range(0f, 4f)] public float depthIntensity = 1.0f;
    [Range(0f, 4f)] public float normalIntensity = 1.0f;
    [Range(0f, 4f)] public float cavityIntensity = 1.25f;
    [Range(0f, 4f)] public float ridgeIntensity = 0.85f;
    [Range(0.5f, 8f)] public float power = 1.2f;
    public Color cavityColor = new Color(0f, 0f, 0f, 1f);
    public Color ridgeColor = new Color(1f, 1f, 1f, 1f);
    public BlendMode blendMode = BlendMode.Darken;
    public bool debugView = false;

    void OnEnable()
    {
        var cam = GetComponent<Camera>();
        if (cam != null) cam.depthTextureMode |= DepthTextureMode.DepthNormals;
    }

    void OnDisable()
    {
        if (mat) DestroyImmediate(mat);
    }

    void EnsureMat()
    {
        if (!mat && shader)
        {
            mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        EnsureMat();
        if (!mat)
        {
            Graphics.Blit(src, dst);
            return;
        }

        mat.SetFloat("_Radius", radius);
        mat.SetInt("_SampleCount", samples);
        mat.SetFloat("_DepthIntensity", depthIntensity);
        mat.SetFloat("_NormalIntensity", normalIntensity);
        mat.SetFloat("_CavityIntensity", cavityIntensity);
        mat.SetFloat("_RidgeIntensity", ridgeIntensity);
        mat.SetFloat("_Power", power);
        mat.SetColor("_CavityColor", cavityColor);
        mat.SetColor("_RidgeColor", ridgeColor);
        mat.SetInt("_BlendMode", (int)blendMode);
        mat.SetFloat("_DebugView", debugView ? 1f : 0f);

        Graphics.Blit(src, dst, mat, 0);
    }
}
