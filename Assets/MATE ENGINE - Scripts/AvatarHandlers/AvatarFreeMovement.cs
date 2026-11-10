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

    // Scale and camera frame the current placement was last checked against.
    private float _checkedScale = -1f;
    private float _checkedHalfHeight = -1f;
    private float _checkedAspect = -1f;

    // Cached model parts for the extents measurement; the VRM under this object can be swapped at runtime.
    private Animator _animator;
    private Transform _headBone;
    private Renderer[] _renderers;
    private float _nextExtentsRefresh;

    // Tracked shadow planes to keep the shadow under the avatar when moved.
    private Transform _shadowPlaneTransform;
    private Transform _groundShadowTransform;
    private Vector3 _initialShadowPlanePos;
    private Vector3 _initialShadowPlaneScale;
    private float _initialAvatarScale = 1f;

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

        RefitAfterResize();

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
        _dragButton = button;
        _active = this;
        IsMoving = true;
        if (WindowManager.Instance != null) WindowManager.Instance.SetInWindowDrag(true);
        return true;
    }

    public void ResetPosition()
    {
        if (_fixedPosition != null) _fixedPosition.ResetToHome();
        SyncShadows();
    }

    void LateUpdate()
    {
        SyncShadows();
    }

    private void SyncShadows()
    {
        if (_shadowPlaneTransform == null)
        {
            var shadowPlaneGO = GameObject.Find("Shadow");
            if (shadowPlaneGO != null)
            {
                _shadowPlaneTransform = shadowPlaneGO.transform;
                _initialShadowPlanePos = _shadowPlaneTransform.position;
                _initialShadowPlaneScale = _shadowPlaneTransform.localScale;
                _initialAvatarScale = transform.lossyScale.x > 0.001f ? transform.lossyScale.x : 1f;
            }
        }

        if (_groundShadowTransform == null)
        {
            var groundShadowGO = GameObject.Find("GroundShadow");
            if (groundShadowGO != null)
            {
                _groundShadowTransform = groundShadowGO.transform;
            }
        }

        Vector3 curPos = GetPosition();
        Vector3 homePos = GetHome();
        Vector3 offset = curPos - homePos;
        float scale = transform.lossyScale.x;
        float scaleFactor = scale / _initialAvatarScale;

        // Realtime shadow receiver plane ("Shadow")
        if (_shadowPlaneTransform != null)
        {
            _shadowPlaneTransform.position = _initialShadowPlanePos + offset;
            _shadowPlaneTransform.localScale = _initialShadowPlaneScale * scaleFactor;
        }

        // Circular fake drag shadow ("GroundShadow")
        if (_groundShadowTransform != null)
        {
            float feetYOffset = 0.095f * scale;
            _groundShadowTransform.position = new Vector3(curPos.x, curPos.y - feetYOffset, curPos.z);
        }
    }

    /// <summary>
    /// The avatar is never lifted inside the window to keep it on screen: the camera does not follow, so that
    /// pushes the head out of the frame. Keeping the companion on the monitor is the window manager's job.
    /// The only correction made here is for a placement the user dragged to: when the avatar is resized or
    /// the camera frame changes, it is fitted into the frame again so a bigger avatar does not lose its head.
    /// </summary>
    private void RefitAfterResize()
    {
        if (!TryGetCamera()) return;

        RefreshModelCache();
        float scale = _animator != null ? _animator.transform.lossyScale.y : transform.lossyScale.y;
        // Only compared against its previous value, so the field of view serves for a perspective camera.
        float halfHeight = _camera.orthographic ? _camera.orthographicSize : _camera.fieldOfView;
        float aspect = _camera.aspect;

        if (Mathf.Approximately(scale, _checkedScale) &&
            Mathf.Approximately(halfHeight, _checkedHalfHeight) &&
            Mathf.Approximately(aspect, _checkedAspect)) return;

        bool first = _checkedScale < 0f;
        _checkedScale = scale;
        _checkedHalfHeight = halfHeight;
        _checkedAspect = aspect;
        if (first) return;

        // At its rest position the avatar is framed by the scene itself, leave that alone.
        var position = GetPosition();
        if ((position - GetHome()).sqrMagnitude < 1e-6f) return;

        _nextExtentsRefresh = 0f; // measure the resized model, not the cached one
        SetPosition(ClampToView(position));
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
