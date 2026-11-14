using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a menu inside the part of the window that is actually visible on the monitor.
///
/// Put it on a menu canvas (all point-anchored direct children are treated as one group, because a menu
/// usually consists of sibling panels such as a frame and its content that must stay aligned) or on a
/// single panel. MenuActions adds it to every menu entry at runtime.
///
/// Each frame, after everything else has positioned the panels, the group is
///   1. moved along with the avatar when it was moved away from its rest position inside the window,
///   2. scaled down if it is larger than the visible area,
///   3. shifted back inside the visible area.
/// The positions other scripts write (e.g. SettingsMenuPosition) stay the reference: the clamper only
/// adds its own offset on top and notices when someone else moved a panel.
/// </summary>
[DisallowMultipleComponent]
public class MenuScreenClamper : MonoBehaviour
{
    [Tooltip("Gap kept between the menu and the edge of the visible area, in canvas units.")]
    public float margin = 10f;

    [Tooltip("Move the menu along when the avatar is moved inside the window.")]
    public bool followAvatar = true;

    [Tooltip("Shrink the menu when it is larger than the visible area.")]
    public bool scaleToFit = true;

    private class Target
    {
        public RectTransform rect;
        public Vector2 basePosition;
        public Vector3 baseScale;
        public Vector2 writtenPosition;
        public Vector3 writtenScale;
    }

    private readonly List<Target> _targets = new();
    private RectTransform _space;
    private bool _initialized;

    // Window geometry is shared by all clampers and only changes when the window moves.
    private const float InsetRefreshSeconds = 0.2f;
    private static float _nextInsetRefresh;
    private static bool _hasInsets;
    private static RectInt _windowRect;
    private static int _insetLeft, _insetTop, _insetRight, _insetBottom;

    void OnEnable()
    {
        if (!_initialized) Initialize();
    }

    void OnDisable() => Restore();

    void LateUpdate()
    {
        if (!_initialized) Initialize();
        if (_space == null || _targets.Count == 0) return;
        Apply();
    }

    private void Initialize()
    {
        _initialized = true;
        _targets.Clear();

        if (TryGetComponent<Canvas>(out _))
        {
            _space = transform as RectTransform;
            foreach (Transform child in transform)
            {
                // Stretched children are backdrops/blockers that cover the canvas, not menu panels.
                if (child is RectTransform rect && rect.anchorMin == rect.anchorMax) AddTarget(rect);
            }
        }
        else if (transform is RectTransform self)
        {
            _space = transform.parent as RectTransform;
            AddTarget(self);
        }
    }

    private void AddTarget(RectTransform rect)
    {
        _targets.Add(new Target
        {
            rect = rect,
            basePosition = rect.anchoredPosition,
            baseScale = rect.localScale,
            writtenPosition = rect.anchoredPosition,
            writtenScale = rect.localScale
        });
    }

    private void Restore()
    {
        foreach (var t in _targets)
        {
            if (t.rect == null) continue;
            if (t.rect.anchoredPosition == t.writtenPosition) t.rect.anchoredPosition = t.basePosition;
            if (t.rect.localScale == t.writtenScale) t.rect.localScale = t.baseScale;
            t.writtenPosition = t.basePosition;
            t.writtenScale = t.baseScale;
        }
    }

    private void Apply()
    {
        Rect space = _space.rect;
        if (space.width <= 0f || space.height <= 0f) return;

        // Anything that differs from what was written last frame was set by someone else: that is the new reference.
        foreach (var t in _targets)
        {
            if (t.rect == null) continue;
            if (t.rect.anchoredPosition != t.writtenPosition) t.basePosition = t.rect.anchoredPosition;
            if (t.rect.localScale != t.writtenScale) t.baseScale = t.rect.localScale;
        }

        Vector2 follow = Vector2.zero;
        if (followAvatar && Screen.height > 0 && AvatarFreeMovement.TryGetScreenOffsetFromHome(out var offsetPixels))
            follow = offsetPixels * (space.height / Screen.height);

        // Bounds of the visible panels in the parent's local space, at their reference position and scale.
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;
        foreach (var t in _targets)
        {
            if (t.rect == null || !t.rect.gameObject.activeInHierarchy) continue;
            Vector2 pivot = AnchorReference(t.rect, space) + t.basePosition + follow;
            Rect r = t.rect.rect;
            Vector2 a = pivot + Vector2.Scale(r.min, t.baseScale);
            Vector2 b = pivot + Vector2.Scale(r.max, t.baseScale);
            Vector2 lo = Vector2.Min(a, b), hi = Vector2.Max(a, b);
            if (!any) { min = lo; max = hi; any = true; }
            else { min = Vector2.Min(min, lo); max = Vector2.Max(max, hi); }
        }
        if (!any) return;

        Rect visible = GetVisibleArea(space);
        Vector2 size = max - min;
        Vector2 center = (min + max) * 0.5f;

        float k = 1f;
        if (scaleToFit && visible.width > 0f && visible.height > 0f && size.x > 0f && size.y > 0f)
            k = Mathf.Min(1f, visible.width / size.x, visible.height / size.y);

        Vector2 half = size * (0.5f * k);
        Vector2 shift = new Vector2(
            ShiftInto(center.x - half.x, center.x + half.x, visible.xMin, visible.xMax),
            ShiftInto(center.y - half.y, center.y + half.y, visible.yMin, visible.yMax));

        foreach (var t in _targets)
        {
            if (t.rect == null) continue;
            Vector2 reference = AnchorReference(t.rect, space);
            Vector2 pivot = reference + t.basePosition + follow;
            // Scale the whole group around its center so sibling panels stay aligned.
            Vector2 position = center + (pivot - center) * k + shift - reference;
            Vector3 scale = new Vector3(t.baseScale.x * k, t.baseScale.y * k, t.baseScale.z);

            if (t.rect.anchoredPosition != position) t.rect.anchoredPosition = position;
            if (t.rect.localScale != scale) t.rect.localScale = scale;
            t.writtenPosition = t.rect.anchoredPosition;
            t.writtenScale = t.rect.localScale;
        }
    }

    /// <summary>Point in the parent's local space that anchoredPosition is measured from.</summary>
    private static Vector2 AnchorReference(RectTransform rect, Rect space)
    {
        return new Vector2(space.xMin + rect.anchorMin.x * space.width, space.yMin + rect.anchorMin.y * space.height);
    }

    /// <summary>How far [min, max] has to move to lie inside [low, high]. Centers it when it does not fit.</summary>
    private static float ShiftInto(float min, float max, float low, float high)
    {
        if (high <= low) return 0f;
        if (max - min > high - low) return (low + high) * 0.5f - (min + max) * 0.5f;
        if (min < low) return low - min;
        if (max > high) return high - max;
        return 0f;
    }

    /// <summary>
    /// The part of the canvas that is on screen, in the parent's local space. The canvas covers the whole
    /// window, but the compositor lets the window hang over the edge of the monitor.
    /// </summary>
    private Rect GetVisibleArea(Rect space)
    {
        RefreshInsets();

        float left = 0f, top = 0f, right = 0f, bottom = 0f;
        if (_hasInsets && _windowRect.width > 0 && _windowRect.height > 0)
        {
            float kx = space.width / _windowRect.width;
            float ky = space.height / _windowRect.height;
            left = _insetLeft * kx;
            right = _insetRight * kx;
            top = _insetTop * ky;
            bottom = _insetBottom * ky;
        }

        var visible = Rect.MinMaxRect(
            space.xMin + left + margin, space.yMin + bottom + margin,
            space.xMax - right - margin, space.yMax - top - margin);

        // Window (almost) entirely off screen: nothing sensible to clamp to, use the whole canvas.
        if (visible.width <= 1f || visible.height <= 1f) return space;
        return visible;
    }

    private static void RefreshInsets()
    {
        if (Time.unscaledTime < _nextInsetRefresh) return;
        _nextInsetRefresh = Time.unscaledTime + InsetRefreshSeconds;

        var windowManager = WindowManager.Instance;
        _hasInsets = windowManager != null &&
                     windowManager.GetWindowOffscreenInsets(out _windowRect, out _insetLeft, out _insetTop, out _insetRight, out _insetBottom);
    }
}
