using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public class DesktopAmbientProbe : MonoBehaviour
{
    public enum Band { Top, Bottom, Left, Right }

    [Serializable]
    public class BandTarget
    {
        public Band band = Band.Top;
        public string targetID = "";
    }

    [Header("Color Controller Linkage")]
    public ColorController colorController;
    public List<BandTarget> bandTargets = new List<BandTarget>();

    public bool enabledAuto = true;
    public bool driveIntensity = true;
    [Range(1f, 60f)] public float captureHz = 10f;
    public int captureWidth = 160;
    public int captureHeight = 90;
    public int bandThicknessPx = 120;
    public int excludeMarginPx = 12;
    [Range(0f, 1f)] public float smoothing = 0.85f;
    public string saveKey = "auto_ambient";
    [Range(0f, 4f)] public float minGrayIntensity = 0.3f;
    [Range(0f, 4f)] public float maxColorIntensity = 0.8f;
    [Range(0.5f, 3f)] public float saturationGamma = 1.3f;

    [DllImport("libX11.so.6")]
    static extern int XGetWindowAttributes(IntPtr display, IntPtr window, out XWindowAttributes attributes);
    
    [DllImport("libX11.so.6")]
    static extern IntPtr XGetImage(IntPtr display, IntPtr drawable, int x, int y, uint width, uint height, ulong plane_mask, int format);
    
    [DllImport("libX11.so.6")]
    static extern int XDestroyImage(IntPtr image);

    [StructLayout(LayoutKind.Sequential)]
    struct XWindowAttributes
    {
        public int x, y, width, height, border_width, depth;
        public IntPtr visual, root;
        public int class_, bit_gravity, win_gravity, backing_store;
        public ulong backing_planes, backing_pixel;
        public int save_under;
        public ulong colormap;
        public int map_installed, map_state;
        public long all_event_masks, your_event_mask, do_not_propagate_mask;
        public int override_redirect;
        public IntPtr screen;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct XImage
    {
        public int width, height, xoffset, format;
        public IntPtr data;
        public int byte_order, bitmap_unit, bitmap_bit_order, bitmap_pad, depth, bytes_per_line, bits_per_pixel;
        public ulong red_mask, green_mask, blue_mask;
        public IntPtr obdata;
    }

    float nextTick;
    static readonly Vector3 DefaultHsv = new Vector3(0f, 0f, 1f);
    Vector3 hsvTop = DefaultHsv;
    Vector3 hsvBot = DefaultHsv;
    Vector3 hsvLeft = DefaultHsv;
    Vector3 hsvRight = DefaultHsv;
    Vector3 hsvTopTarget = DefaultHsv;
    Vector3 hsvBotTarget = DefaultHsv;
    Vector3 hsvLeftTarget = DefaultHsv;
    Vector3 hsvRightTarget = DefaultHsv;
    bool inited;
    bool hasSample;

    void Start()
    {
        TryLoadToggle();
        EnsureSelfConfig();
        inited = true;
    }

    void EnsureSelfConfig()
    {
        if (colorController == null)
            colorController = FindAnyObjectByType<ColorController>();
            
        if (bandTargets == null) bandTargets = new List<BandTarget>();
        if (bandTargets.Count == 0)
        {
            bandTargets.Add(new BandTarget { band = Band.Top, targetID = "ambi_1" });
            bandTargets.Add(new BandTarget { band = Band.Bottom, targetID = "ambi_2" });
            bandTargets.Add(new BandTarget { band = Band.Left, targetID = "ambi_3" });
            bandTargets.Add(new BandTarget { band = Band.Right, targetID = "ambi_3" });
        }
    }

    void TryLoadToggle()
    {
        var s = SaveLoadHandler.Instance;
        if (s != null && s.data != null && s.data.groupToggles != null)
        {
            if (s.data.groupToggles.TryGetValue(saveKey, out bool v)) enabledAuto = v;
        }
    }

    public void SetEnabled(bool v)
    {
        enabledAuto = v;
        var s = SaveLoadHandler.Instance;
        if (s != null && s.data != null)
        {
            s.data.groupToggles[saveKey] = v;
            s.SaveToDisk();
        }
    }

    void LateUpdate()
    {
        if (!inited) return;
        if (!enabledAuto) return;
        if (Time.unscaledTime >= nextTick)
        {
            nextTick = Time.unscaledTime + 1f / Mathf.Max(1f, captureHz);
            CaptureAndAnalyze();
        }
        SmoothTowardsTargets(Time.unscaledDeltaTime);
        ApplyToTargets();
    }

    void CaptureAndAnalyze()
    {
        var wm = WindowManager.Instance;
        if (wm == null || wm.Display == IntPtr.Zero) return;

        bool haveWnd = wm.GetWindowRect(out var wr);
        
        XGetWindowAttributes(wm.Display, wm.RootWindow, out var rootAttr);
        int screenW = rootAttr.width, screenH = rootAttr.height;

        CalculateSampleRects(screenW, screenH, wr.x, wr.y, wr.width, wr.height, 
            bandThicknessPx, excludeMarginPx, haveWnd, 
            out var topRect, out var botRect, out var leftRect, out var rightRect);

        SetHSVTargets(AvgColor(topRect), AvgColor(botRect), 
            AvgColor(leftRect), AvgColor(rightRect));
    }
    
    void CalculateSampleRects(int screenW, int screenH, int wx0, int wy0, int winW, int winH, 
                              int band, int margin, bool haveWnd, 
                              out RectInt topRect, out RectInt botRect, out RectInt leftRect, out RectInt rightRect)
    {
        if (haveWnd)
        {
            int wx1 = wx0 + winW;
            int wy1 = wy0 + winH;

            topRect = new RectInt(0, Mathf.Max(0, wy0 - band), screenW, Mathf.Clamp(band, 1, screenH));
            botRect = new RectInt(0, Mathf.Min(screenH - band, wy1), screenW, Mathf.Clamp(band, 1, screenH));
            leftRect = new RectInt(Mathf.Max(0, wx0 - band), Mathf.Clamp(wy0, 0, screenH - 1), Mathf.Clamp(band, 1, screenW), Mathf.Clamp(winH, 1, screenH));
            rightRect = new RectInt(Mathf.Min(screenW - band, wx1), Mathf.Clamp(wy0, 0, screenH - 1), Mathf.Clamp(band, 1, screenW), Mathf.Clamp(winH, 1, screenH));

            topRect = ClampRect(topRect, screenW, screenH);
            botRect = ClampRect(botRect, screenW, screenH);
            leftRect = ClampRect(leftRect, screenW, screenH);
            rightRect = ClampRect(rightRect, screenW, screenH);

            RectInt inside = new RectInt(Mathf.Clamp(wx0 - margin, 0, screenW - 1), Mathf.Clamp(wy0 - margin, 0, screenH - 1), Mathf.Clamp(winW + 2 * margin, 1, screenW), Mathf.Clamp(winH + 2 * margin, 1, screenH));
            Exclude(ref topRect, inside);
            Exclude(ref botRect, inside);
            Exclude(ref leftRect, inside);
            Exclude(ref rightRect, inside);
        }
        else
        {
            int hband = Mathf.Max(1, screenH / 5);
            int wband = Mathf.Max(1, screenW / 8);
            topRect = new RectInt(0, hband, screenW, hband);
            botRect = new RectInt(0, screenH - hband * 2, screenW, hband);
            leftRect = new RectInt(wband, hband, wband, screenH - 2 * hband);
            rightRect = new RectInt(screenW - wband * 2, hband, wband, screenH - 2 * hband);
        }
    }

    void SetHSVTargets(Color ct, Color cb, Color cl, Color cr)
    {
        Color.RGBToHSV(ct, out float hTop, out float sTop, out float vTop);
        Color.RGBToHSV(cb, out float hBot, out float sBot, out float vBot);
        Color.RGBToHSV(cl, out float hLeft, out float sLeft, out float vLeft);
        Color.RGBToHSV(cr, out float hRight, out float sRight, out float vRight);

        Vector3 tTop = new Vector3(hTop, sTop, vTop);
        Vector3 tBot = new Vector3(hBot, sBot, vBot);
        Vector3 tLeft = new Vector3(hLeft, sLeft, vLeft);
        Vector3 tRight = new Vector3(hRight, sRight, vRight);

        if (!hasSample)
        {
            hsvTop = tTop; hsvBot = tBot; hsvLeft = tLeft; hsvRight = tRight;
            hsvTopTarget = tTop; hsvBotTarget = tBot; hsvLeftTarget = tLeft; hsvRightTarget = tRight;
            hasSample = true;
        }
        else
        {
            hsvTopTarget = tTop; hsvBotTarget = tBot;
            hsvLeftTarget = tLeft; hsvRightTarget = tRight;
        }
    }

    RectInt ClampRect(RectInt r, int w, int h)
    {
        int x = Mathf.Clamp(r.x, 0, w - 1);
        int y = Mathf.Clamp(r.y, 0, h - 1);
        int rw = Mathf.Clamp(r.width, 1, w - x);
        int rh = Mathf.Clamp(r.height, 1, h - y);
        return new RectInt(x, y, rw, rh);
    }

    void Exclude(ref RectInt r, RectInt inside)
    {
        if (!r.Overlaps(inside)) return;
        int left = Mathf.Max(r.x, inside.x);
        int right = Mathf.Min(r.x + r.width, inside.x + inside.width);
        int top = Mathf.Max(r.y, inside.y);
        int bottom = Mathf.Min(r.y + r.height, inside.y + inside.height);
        RectInt a = new RectInt(r.x, r.y, r.width, Mathf.Max(0, top - r.y));
        RectInt b = new RectInt(r.x, bottom, r.width, Mathf.Max(0, (r.y + r.height) - bottom));
        RectInt c = new RectInt(r.x, top, Mathf.Max(0, left - r.x), Mathf.Max(0, bottom - top));
        RectInt d = new RectInt(right, top, Mathf.Max(0, (r.x + r.width) - right), Mathf.Max(0, bottom - top));
        RectInt best = a;
        if (b.width * b.height > best.width * best.height) best = b;
        if (c.width * c.height > best.width * best.height) best = c;
        if (d.width * d.height > best.width * best.height) best = d;
        r = best.width > 0 && best.height > 0 ? best : new RectInt(r.x, r.y, 1, 1);
    }

    unsafe Color AvgColor(RectInt r)
    {
        var wm = WindowManager.Instance;
        if (wm == null || wm.Display == IntPtr.Zero || r.width <= 0 || r.height <= 0) return Color.black;

        IntPtr imgPtr = XGetImage(wm.Display, wm.RootWindow, r.x, r.y, (uint)r.width, (uint)r.height, 0xFFFFFFFF, 2); // 2 = ZPixmap
        if (imgPtr == IntPtr.Zero) return Color.black;

        XImage img = Marshal.PtrToStructure<XImage>(imgPtr);
        long rb = 0, gb = 0, bb = 0, count = 0;
        bool isLSB = img.byte_order == 0;

        for (int y = 0; y < img.height; y++)
        {
            byte* row = (byte*)img.data + y * img.bytes_per_line;
            for (int x = 0; x < img.width; x++)
            {
                byte r8 = 0, g = 0, b = 0, a = 255;
                if (img.bits_per_pixel == 32)
                {
                    if (isLSB) { b = row[x * 4]; g = row[x * 4 + 1]; r8 = row[x * 4 + 2]; a = row[x * 4 + 3]; }
                    else       { a = row[x * 4]; r8 = row[x * 4 + 1]; g = row[x * 4 + 2]; b = row[x * 4 + 3]; }
                    if (a == 0) continue;
                }
                else if (img.bits_per_pixel == 24)
                {
                    int offset = x * 3;
                    if (isLSB) { b = row[offset]; g = row[offset + 1]; r8 = row[offset + 2]; }
                    else       { r8 = row[offset]; g = row[offset + 1]; b = row[offset + 2]; }
                }
                rb += r8; gb += g; bb += b; count++;
            }
        }
        XDestroyImage(imgPtr);

        if (count == 0) return Color.black;
        return new Color(rb / (255f * count), gb / (255f * count), bb / (255f * count), 1f);
    }

    void SmoothTowardsTargets(float dt)
    {
        if (!hasSample) return;
        float tau = 0.05f + 1.5f * Mathf.Clamp01(smoothing);
        float a = 1f - Mathf.Exp(-dt / Mathf.Max(0.0001f, tau));
        hsvTop = DampHSV(hsvTop, hsvTopTarget, a);
        hsvBot = DampHSV(hsvBot, hsvBotTarget, a);
        hsvLeft = DampHSV(hsvLeft, hsvLeftTarget, a);
        hsvRight = DampHSV(hsvRight, hsvRightTarget, a);
    }

    Vector3 DampHSV(Vector3 cur, Vector3 target, float a)
    {
        float dh = Mathf.DeltaAngle(cur.x * 360f, target.x * 360f) / 360f;
        float h = Mathf.Repeat(cur.x + a * dh, 1f);
        float s = Mathf.Lerp(cur.y, target.y, a);
        float v = Mathf.Lerp(cur.z, target.z, a);
        return new Vector3(h, s, v);
    }

    void ApplyToTargets()
    {
        if (colorController == null) return;
        if (!hasSample) return;
        
        for (int i = 0; i < bandTargets.Count; i++)
        {
            var bt = bandTargets[i];
            var target = FindTarget(bt.targetID);
            if (target == null) continue;
            ApplyTarget(target, GetBandHsv(bt.band));
        }
    }

    ColorController.ColorTarget FindTarget(string id)
    {
        if (string.IsNullOrEmpty(id) || colorController == null) return null;
        var targets = colorController.targets;
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].id == id) return targets[i];
        return null;
    }

    Vector3 GetBandHsv(Band b)
    {
        switch (b)
        {
            case Band.Top: return hsvTop;
            case Band.Bottom: return hsvBot;
            case Band.Left: return hsvLeft;
            case Band.Right: return hsvRight;
            default: return hsvTop;
        }
    }

    void ApplyTarget(ColorController.ColorTarget target, Vector3 hsv)
    {
        target.hue = hsv.x;
        target.saturation = hsv.y;
        
        if (driveIntensity)
        {
            float i = Mathf.Lerp(minGrayIntensity, maxColorIntensity, Mathf.Pow(Mathf.Clamp01(hsv.y), saturationGamma));
            float maxI = target.intensityOverride ? target.maxIntensity : 1f;
            target.intensity = Mathf.Clamp(i * maxI, 0f, Mathf.Max(0.01f, maxI));
        }
    }
}