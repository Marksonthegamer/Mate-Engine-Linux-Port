using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Lets the user reposition the avatar inside the window, independent of the window itself.
/// Hold Alt and drag with the left mouse button, or drag with the middle mouse button.
/// Alt + right click puts the avatar back where it spawned.
/// When the window fills the monitor (see WindowManager.FillsMonitor) a plain left drag moves the avatar too,
/// because there is no point in moving a screen-sized window.
/// Added automatically next to <see cref="FixedPosition"/>.
/// </summary>
[DisallowMultipleComponent]
public class AvatarFreeMovement : MonoBehaviour
{
    [Tooltip("Master switch for in-window avatar dragging.")]
    public bool enableFreeMovement = true;

    [Tooltip("Drag with the middle mouse button, no modifier needed.")]
    public bool allowMiddleButtonDrag = true;

    [Tooltip("Hold one of these keys and drag with the left mouse button.")]
    public KeyCode modifierKey = KeyCode.LeftAlt;
    public KeyCode alternateModifierKey = KeyCode.RightAlt;

    [Tooltip("How far the avatar may leave its spawn position horizontally, as a fraction of the visible area.")]
    [Range(0.1f, 1f)] public float maxOffsetViewFraction = 0.9f;

    [Tooltip("Bottom guard: how many pixels the feet may sit below the screen edge before the avatar is lifted.")]
    public float bottomGuardTolerancePixels = 8f;

    [Tooltip("Top guard: screen margin in pixels below the top panel for the head.")]
    public float topGuardTolerancePixels = 12f;

    /// <summary>True while any avatar is being dragged inside the window.</summary>
    public static bool IsMoving { get; private set; }

    private static AvatarFreeMovement _active;
    private static readonly List<AvatarFreeMovement> Instances = new();

    // Margin kept between the avatar and the top/bottom of the camera frame, in world units.
    private const float FramePaddingWorld = 0.04f;
    // Fallback avatar height and the distance from the head bone to the top of the head, for a scale of 1.
    private const float DefaultHeadHeight = 1.48f;
    private const float HeadBoneToCrown = 0.18f;
    private const float ExtentsRefreshSeconds = 1f;

    private FixedPosition _fixedPosition;
    private Camera _camera;
    private int _dragButton = -1;
    private Vector3 _grabOffset;

    // World units the screen guard currently adds along the camera's up axis.
    private float _guardLift;

    // Cached model parts for the extents measurement; the VRM under this object can be swapped at runtime.
    private Animator _animator;
    private Transform _headBone;
    private Renderer[] _renderers;
    private float _nextExtentsRefresh;

    /// <summary>
    /// True when this pointer press belongs to the in-window drag gesture,
    /// so the window manager must not start moving the window for it.
    /// </summary>
    public static bool ClaimsPointer(PointerEventData eventData)
    {
        if (IsMoving) return true;
        if (eventData == null) return false;

        foreach (var instance in Instances)
        {
            if (!instance.CanMove) continue;
            if (eventData.button == PointerEventData.InputButton.Middle && instance.allowMiddleButtonDrag) return true;
            if (eventData.button == PointerEventData.InputButton.Left && instance.ModifierHeld()) return true;
        }
        return false;
    }

    /// <summary>
    /// Starts an in-window drag with the left button, without a modifier key.
    /// Used by the window manager when the press landed on the avatar and the window itself should stay put.
    /// </summary>
    public static bool BeginPointerDrag()
    {
        if (IsMoving) return true;
        if (MenuActions.IsMovementBlocked() || TutorialMenu.IsActive) return false;

        foreach (var instance in Instances)
        {
            if (instance.CanMove && instance.BeginDrag(0)) return true;
        }
        return false;
    }

    /// <summary>
    /// How far the avatar currently is from its rest position, in Unity screen pixels (y pointing up).
    /// UI that was laid out around the resting avatar can add this to follow it.
    /// </summary>
    public static bool TryGetScreenOffsetFromHome(out Vector2 offsetPixels)
    {
        offsetPixels = Vector2.zero;
        foreach (var instance in Instances)
        {
            if (!instance.isActiveAndEnabled || !instance.TryGetCamera()) continue;
            Vector2 current = instance._camera.WorldToScreenPoint(instance.GetPosition());
            Vector2 home = instance._camera.WorldToScreenPoint(instance.GetHome());
            offsetPixels = current - home;
            return true;
        }
        return false;
    }

    /// <summary>Screen position (Unity pixels) of the avatar's origin, i.e. its feet.</summary>
    public static bool TryGetAvatarScreenPoint(out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        foreach (var instance in Instances)
        {
            if (!instance.isActiveAndEnabled || !instance.TryGetCamera()) continue;
            screenPoint = instance._camera.WorldToScreenPoint(instance.GetPosition());
            return true;
        }
        return false;
    }

    private bool CanMove => isActiveAndEnabled && enableFreeMovement;

    void Awake()
    {
        _fixedPosition = GetComponent<FixedPosition>();
    }

    void OnEnable()
    {
        if (!Instances.Contains(this)) Instances.Add(this);
    }

    void OnDisable()
    {
        EndDrag();
        Instances.Remove(this);
    }

    void Update()
    {
        if (_dragButton >= 0)
        {
            if (!Input.GetMouseButton(_dragButton) || !enableFreeMovement || MenuActions.IsMovementBlocked())
            {
                EndDrag();
                return;
            }
            if (TryGetPointerWorld(out var pointer))
                SetPosition(ClampToView(pointer + _grabOffset));
            return;
        }

        if (!enableFreeMovement || IsMoving || MenuActions.IsMovementBlocked() || TutorialMenu.IsActive) return;

        if (ModifierHeld() && Input.GetMouseButtonDown(1))
        {
            ResetPosition();
            return;
        }

        if (allowMiddleButtonDrag && Input.GetMouseButtonDown(2)) BeginDrag(2);
        else if (ModifierHeld() && Input.GetMouseButtonDown(0)) BeginDrag(0);
    }

    private bool BeginDrag(int button)
    {
        if (!TryGetPointerWorld(out var start)) return false;
        _grabOffset = GetPosition() - start;
        _guardLift = 0f; // the user takes over, whatever the guard added is now part of their placement
        _dragButton = button;
        _active = this;
        IsMoving = true;
        if (WindowManager.Instance != null) WindowManager.Instance.SetInWindowDrag(true);
        return true;
    }

    public void ResetPosition()
    {
        _guardLift = 0f;
        if (_fixedPosition != null) _fixedPosition.ResetToHome();
    }

    /// <summary>
    /// Backwards compatible helper for bottom edge checking.
    /// </summary>
    public static void ApplyBottomGuard(RectInt windowRect, int floorY)
    {
        ApplyScreenGuard(windowRect, new RectInt(windowRect.x, 0, windowRect.width, floorY));
    }

    /// <summary>
    /// Called by the window manager when the window moves.
    /// Adjusts the avatar inside the window so its feet do not sink below the screen floor,
    /// its head can reach the top panel, and its upper body is never clipped outside the window.
    /// </summary>
    public static void ApplyScreenGuard(RectInt windowRect, RectInt monitorRect)
    {
        foreach (var instance in Instances)
        {
            if (instance.CanMove) instance.GuardScreen(windowRect, monitorRect);
        }
    }

    /// <summary>Takes back whatever the screen guard added, e.g. when the window stops being movable.</summary>
    public static void ClearScreenGuard()
    {
        foreach (var instance in Instances)
        {
            if (Mathf.Approximately(instance._guardLift, 0f) || !instance.TryGetCamera()) continue;
            var position = instance.GetPosition() - instance._camera.transform.up * instance._guardLift;
            instance._guardLift = 0f;
            instance.SetPosition(position);
        }
    }

    private void GuardScreen(RectInt windowRect, RectInt monitorRect)
    {
        if (_dragButton >= 0 || !TryGetCamera() || Screen.height <= 0 || windowRect.height <= 0) return;

        var camTransform = _camera.transform;
        float depth = Mathf.Max(Vector3.Dot(GetHome() - camTransform.position, camTransform.forward), _camera.nearClipPlane + 0.01f);
        float halfHeight = _camera.orthographic
            ? _camera.orthographicSize
            : depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float worldHeight = halfHeight * 2f;
        float camTopY = camTransform.position.y + halfHeight;
        float camBottomY = camTransform.position.y - halfHeight;

        float pixelsPerWorldUnit = windowRect.height / worldHeight;
        float worldPerPixel = worldHeight / windowRect.height;

        GetAvatarVerticalExtents(out float headOffset, out float feetOffset);

        // Base position without current guard lift
        var basePosition = GetPosition() - camTransform.up * _guardLift;
        float baseRootY = basePosition.y;
        float baseHeadY = baseRootY + headOffset;
        float baseFeetY = baseRootY + feetOffset;

        // Desktop root Y positions (X11 coords, 0 at top, pointing down)
        float desktopHeadY = windowRect.y + (camTopY - baseHeadY) * pixelsPerWorldUnit;
        float desktopFeetY = windowRect.y + (camTopY - baseFeetY) * pixelsPerWorldUnit;

        // Strict ceiling: the lift may never take the head past the top of the camera frame.
        // It is never negative, so the guard cannot push the avatar down either.
        float maxAllowedHeadWorldY = camTopY - FramePaddingWorld;
        float maxAllowedLift = Mathf.Max(0f, maxAllowedHeadWorldY - baseHeadY);

        // Strict floor: feet must never cross the bottom of the camera frame
        float minAllowedFeetWorldY = camBottomY + FramePaddingWorld;
        float minAllowedLift = Mathf.Min(0f, minAllowedFeetWorldY - baseFeetY);

        float desiredLift = 0f;

        // 1. Bottom guard: feet below the monitor's bottom edge? Lift the avatar.
        float bottomFloorY = monitorRect.yMax;
        float bottomOverflowPx = desktopFeetY - bottomFloorY;
        if (bottomOverflowPx > bottomGuardTolerancePixels)
        {
            desiredLift = bottomOverflowPx * worldPerPixel;
        }

        // 2. Top guard: the compositor stops the window at the top panel while the frame above the head is empty.
        // Lift the avatar into that empty space so the head reaches the top edge.
        float topCeilingY = monitorRect.y;
        if (windowRect.y <= topCeilingY + 45)
        {
            float targetHeadDesktopY = topCeilingY + topGuardTolerancePixels;
            float topGapPx = desktopHeadY - targetHeadDesktopY;
            if (topGapPx > 0f)
            {
                float topLift = topGapPx * worldPerPixel;
                desiredLift = Mathf.Max(desiredLift, topLift);
            }
        }

        _guardLift = Mathf.Clamp(desiredLift, minAllowedLift, maxAllowedLift);
        SetPosition(basePosition + camTransform.up * _guardLift);
    }

    /// <summary>
    /// Height of the top of the head and of the lowest point of the model, relative to this transform.
    /// The head bone is the primary source; renderer bounds are only allowed to add a little on top of it
    /// (hats, hair), because skinned mesh bounds are often much larger than what is actually drawn.
    /// </summary>
    private void GetAvatarVerticalExtents(out float headOffset, out float feetOffset)
    {
        RefreshModelCache();

        float rootY = transform.position.y;
        float scale = Mathf.Max(_animator != null ? _animator.transform.lossyScale.y : transform.lossyScale.y, 0.01f);

        bool hasHeadBone = _headBone != null;
        headOffset = hasHeadBone
            ? _headBone.position.y - rootY + HeadBoneToCrown * scale
            : DefaultHeadHeight * scale;
        feetOffset = 0f;

        // With a head bone the mesh may raise the estimate only slightly, without one it is all there is.
        float maxHeadOffset = hasHeadBone ? headOffset + 0.15f * scale : 2.5f * scale;
        float minFeetOffset = -0.5f * scale;

        if (_renderers == null) return;
        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
            var b = r.bounds;
            float top = b.max.y - rootY;
            float bottom = b.min.y - rootY;
            if (top > headOffset) headOffset = Mathf.Min(top, maxHeadOffset);
            if (bottom < feetOffset) feetOffset = Mathf.Max(bottom, minFeetOffset);
        }
    }

    private void RefreshModelCache()
    {
        if (Time.unscaledTime < _nextExtentsRefresh && _animator != null) return;
        _nextExtentsRefresh = Time.unscaledTime + ExtentsRefreshSeconds;

        _animator = GetComponentInChildren<Animator>();
        _headBone = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private bool ModifierHeld() =>
        (modifierKey != KeyCode.None && Input.GetKey(modifierKey)) ||
        (alternateModifierKey != KeyCode.None && Input.GetKey(alternateModifierKey));

    private void EndDrag()
    {
        _dragButton = -1;
        if (_active == this)
        {
            _active = null;
            IsMoving = false;
            if (WindowManager.Instance != null) WindowManager.Instance.SetInWindowDrag(false);
        }
    }

    private Vector3 GetPosition() => _fixedPosition != null ? _fixedPosition.Position : transform.position;

    private void SetPosition(Vector3 position)
    {
        if (_fixedPosition != null) _fixedPosition.Position = position;
        else transform.position = position;
    }

    private Vector3 GetHome() => _fixedPosition != null ? _fixedPosition.HomePosition : transform.position;

    private bool TryGetCamera()
    {
        if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
        return _camera != null;
    }

    /// <summary>Pointer position on the camera-facing plane the avatar currently sits on.</summary>
    private bool TryGetPointerWorld(out Vector3 world)
    {
        world = default;
        if (!TryGetCamera()) return false;

        var camTransform = _camera.transform;
        float depth = Vector3.Dot(GetPosition() - camTransform.position, camTransform.forward);
        if (depth <= _camera.nearClipPlane) depth = _camera.nearClipPlane + 0.01f;

        var mouse = Input.mousePosition;
        world = _camera.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, depth));
        return true;
    }

    /// <summary>
    /// Keeps the avatar reachable and completely within the camera frame during manual dragging.
    /// All limits come from the camera's current frustum, so they follow the window size and aspect.
    /// </summary>
    private Vector3 ClampToView(Vector3 target)
    {
        if (!TryGetCamera()) return target;

        var camTransform = _camera.transform;
        var home = GetHome();
        float depth = Mathf.Max(Vector3.Dot(home - camTransform.position, camTransform.forward), _camera.nearClipPlane + 0.01f);

        float halfHeight = _camera.orthographic
            ? _camera.orthographicSize
            : depth * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * _camera.aspect;
        float camTopY = camTransform.position.y + halfHeight;
        float camBottomY = camTransform.position.y - halfHeight;

        GetAvatarVerticalExtents(out float headOffset, out float feetOffset);

        var delta = target - home;
        float x = Mathf.Clamp(Vector3.Dot(delta, camTransform.right), -halfWidth * maxOffsetViewFraction, halfWidth * maxOffsetViewFraction);

        // Strict vertical bounds ensuring neither head nor feet cross the camera frame
        float maxRootY = camTopY - headOffset - FramePaddingWorld;
        float minRootY = camBottomY - feetOffset + FramePaddingWorld;

        float clampedY = Mathf.Clamp(target.y, minRootY, Mathf.Max(minRootY, maxRootY));

        return new Vector3(home.x + x, clampedY, home.z);
    }
}
