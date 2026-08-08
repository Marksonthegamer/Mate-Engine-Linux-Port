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

    private readonly Dictionary<Text, string> rawText = new();
    private readonly Dictionary<Text, Color> baseTextColor = new();

    private static readonly Regex h3 = new(@"^### (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex h2 = new(@"^## (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex h1 = new(@"^# (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex boldItalic = new(@"\*\*\*(.+?)\*\*\*", RegexOptions.Compiled);
    private static readonly Regex bold = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex italic = new(@"\*(.+?)\*", RegexOptions.Compiled);
    private static readonly Regex strike = new(@"~~(.+?)~~", RegexOptions.Compiled);
    private static readonly Regex richTextTag = new(@"<\s*(b|i|s|color|size|u)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex stripTags = new(@"<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);

    private float lastHue = -2f, lastSat = -2f;

    void OnEnable()
    {
        frameCounter = 0;
        lastHue = lastSat = -2f;
    }

    void Update()
    {
        float h, s;
        bool hasTheme = TryGetHueSat(out h, out s);
        bool hueChanged = hasTheme && (Mathf.Abs(lastHue - h) > 0.0005f || Mathf.Abs(lastSat - s) > 0.0005f);

        if (hueChanged)
        {
            lastHue = h;
            lastSat = s;
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

        if (rawText.Count > 0 || baseTextColor.Count > 0)
        {
            var toRemove = new List<Text>();
            foreach (var kv in rawText) if (kv.Key == null) toRemove.Add(kv.Key);
            foreach (var t in toRemove) rawText.Remove(t);
            toRemove.Clear();
            foreach (var kv in baseTextColor) if (kv.Key == null) toRemove.Add(kv.Key);
            foreach (var t in toRemove) baseTextColor.Remove(t);
        }

        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null) continue;
            t.supportRichText = true;

            if (!baseTextColor.ContainsKey(t)) baseTextColor[t] = t.color;

            string current = t.text ?? "";
            if (!rawText.TryGetValue(t, out var raw))
            {
                raw = richTextTag.IsMatch(current) ? stripTags.Replace(current, "") : current;
                rawText[t] = raw;
            }
            else if (!richTextTag.IsMatch(current) && current != raw)
            {
                raw = current;
                rawText[t] = raw;
            }

            t.text = ParseMarkdown(raw);
            t.color = HueShiftColor(baseTextColor[t]);
        }
    }
    
    public void AppendText(Text target, string appendStr)
    {
        if (target == null) return;

        if (!baseTextColor.ContainsKey(target)) 
            baseTextColor[target] = target.color;

        // 尝试获取缓存的原始文本
        if (!rawText.TryGetValue(target, out var raw))
        {
            // 如果没有缓存，取当前文本（如果是占位符 "..." 则视为空）
            string current = target.text == "..." ? "" : (target.text ?? "");
            raw = richTextTag.IsMatch(current) ? stripTags.Replace(current, "") : current;
        }
        else if (raw == "...")
        {
            // 如果缓存的是占位符，重置为空
            raw = "";
        }

        // 追加新字符
        raw += appendStr;
        rawText[target] = raw;

        // 直接解析并赋值，绕过 ConvertAllNow 的防覆盖冲突
        target.text = ParseMarkdown(raw);
        target.color = HueShiftColor(baseTextColor[target]);
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
        float h, s;
        if (!TryGetHueSat(out h, out s)) return original;
        Color.RGBToHSV(original, out float hh, out float ss, out float vv);
        hh = (hh + h) % 1f;
        ss = Mathf.Clamp01(ss * s);
        Color result = Color.HSVToRGB(hh, ss, vv);
        result.a = original.a;
        return result;
    }

    private bool TryGetHueSat(out float h, out float s)
    {
        h = 0f; s = 1f;
        if (ThemeManager.Instance != null)
        {
            h = ThemeManager.Instance.hue;
            s = ThemeManager.Instance.saturation;
            return true;
        }
        return false;
    }

    private static string ColorToHex(Color c) => $"#{ColorUtility.ToHtmlStringRGB(c)}";
}
