using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MarkdownTextAutoConverter : MonoBehaviour
{
    [Header("General")]
    [Range(1, 60)] public int updateEveryXFrames = 10;
    private int frameCounter;

    [Header("Headings")]
    public bool enableHeadingColors = true;
    [Range(30, 200)] public int heading1Size = 60;
    [Range(30, 200)] public int heading2Size = 50;
    [Range(30, 200)] public int heading3Size = 40;
    public Color heading1Color = new(1f, 0.6f, 0.95f);
    public Color heading2Color = new(0.3f, 1f, 1f);
    public Color heading3Color = new(0.9f, 0.9f, 1f);

    [Header("Bold")]
    public bool enableBoldColor = false;
    public Color boldColor = Color.white;

    [Header("Italic")]
    public bool enableItalicColor = false;
    public Color italicColor = Color.white;

    [Header("Strikethrough")]
    public bool enableStrikeColor = false;
    public Color strikeColor = Color.gray;

    private readonly Dictionary<Text, string> _raw = new();

    private static readonly Regex h3 = new(@"^### (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex h2 = new(@"^## (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex h1 = new(@"^# (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex boldItalic = new(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled);
    private static readonly Regex bold = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex italic = new(@"\*(.+?)\*", RegexOptions.Compiled);
    private static readonly Regex strike = new(@"~~(.+?)~~", RegexOptions.Compiled);
    private static readonly Regex richTextTag = new(@"<\s*(b|i|s|color|size|u)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex stripTags = new(@"<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);

    private MenuHueShift _hue;
    private float _lastHue = -2f, _lastSat = -2f;

    void Awake()
    {
        _hue = FindAnyObjectByType<MenuHueShift>(FindObjectsInactive.Include);
    }

    void OnEnable()
    {
        frameCounter = 0;
        _lastHue = _lastSat = -2f;
    }

    void Update()
    {
        bool hueChanged = _hue != null &&
                          (Mathf.Abs(_lastHue - _hue.hueShift) > 0.0005f ||
                           Mathf.Abs(_lastSat - _hue.saturation) > 0.0005f);

        if (hueChanged)
        {
            _lastHue = _hue.hueShift;
            _lastSat = _hue.saturation;
            ConvertAllNow();
            frameCounter = 0;
            return;
        }

        frameCounter++;
        if (frameCounter % updateEveryXFrames == 0)
        {
            ConvertAllNow();
            frameCounter = 0;
        }
    }

    public void ConvertAllNow()
    {
        var texts = GetComponentsInChildren<Text>(true);

        if (_raw.Count > 0)
        {
            var toRemove = new List<Text>();
            foreach (var kv in _raw)
                if (kv.Key == null) toRemove.Add(kv.Key);
            foreach (var t in toRemove) _raw.Remove(t);
        }

        foreach (var t in texts)
        {
            if (t == null) continue;
            t.supportRichText = true;

            string current = t.text ?? "";

            if (!_raw.TryGetValue(t, out var raw))
            {
                raw = richTextTag.IsMatch(current) ? stripTags.Replace(current, "") : current;
                _raw[t] = raw;
            }
            else if (!richTextTag.IsMatch(current) && current != raw)
            {
                raw = current;
                _raw[t] = raw;
            }

            t.text = ParseMarkdown(raw);
        }
    }

    private string ParseMarkdown(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        Color h1C = HueShiftColor(heading1Color);
        Color h2C = HueShiftColor(heading2Color);
        Color h3C = HueShiftColor(heading3Color);
        Color boldC = HueShiftColor(boldColor);
        Color italicC = HueShiftColor(italicColor);
        Color strikeC = HueShiftColor(strikeColor);

        string text = input;

        if (enableHeadingColors)
        {
            text = h3.Replace(text, $"<color={ColorToHex(h3C)}><size={heading3Size}%><b>$1</b></size></color>");
            text = h2.Replace(text, $"<color={ColorToHex(h2C)}><size={heading2Size}%><b>$1</b></size></color>");
            text = h1.Replace(text, $"<color={ColorToHex(h1C)}><size={heading1Size}%><b>$1</b></size></color>");
        }
        else
        {
            text = h3.Replace(text, $"<size={heading3Size}%><b>$1</b></size>");
            text = h2.Replace(text, $"<size={heading2Size}%><b>$1</b></size>");
            text = h1.Replace(text, $"<size={heading1Size}%><b>$1</b></size>");
        }

        text = boldItalic.Replace(text, $"<b><i>$1</i></b>");
        text = enableBoldColor
            ? bold.Replace(text, $"<color={ColorToHex(boldC)}><b>$1</b></color>")
            : bold.Replace(text, "<b>$1</b>");
        text = enableItalicColor
            ? italic.Replace(text, $"<color={ColorToHex(italicC)}><i>$1</i></color>")
            : italic.Replace(text, "<i>$1</i>");
        text = enableStrikeColor
            ? strike.Replace(text, $"<color={ColorToHex(strikeC)}><s>$1</s></color>")
            : strike.Replace(text, "<s>$1</s>");

        return text;
    }

    private Color HueShiftColor(Color original)
    {
        if (_hue == null) return original;
        Color.RGBToHSV(original, out float h, out float s, out float v);
        h = (h + _hue.hueShift) % 1f;
        float adjustedS = Mathf.Clamp01(s * (_hue.saturation * 2f));
        Color result = Color.HSVToRGB(h, adjustedS, v);
        result.a = original.a;
        return result;
    }

    private static string ColorToHex(Color c) => $"#{ColorUtility.ToHtmlStringRGB(c)}";
}
