using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

[System.Serializable]
public class MenuEntry
{
    public GameObject menu;
    public bool blockMovement = true;
    public bool blockHandTracking = false;
    public bool blockReaction = false;
    public bool blockChibiMode = false;
}

public class MenuActions : MonoBehaviour
{
    [Header("Menus")]
    public List<MenuEntry> menuEntries = new();

    [Header("Lock Canvas")]
    public GameObject moveCanvas;

    [Header("Radial Menu")]
    public GameObject radialMenuObject;
    public bool radialBlockMovement = true;
    public bool radialBlockHandTracking = false;
    public bool radialBlockReaction = false;
    public bool radialBlockChibiMode = false;
    public KeyCode radialMenuKey = KeyCode.F1;
    public bool radialDraggingBlocks = true;

    [Header("Bone Follow")]
    public bool followBone = true;
    public HumanBodyBones targetBone = HumanBodyBones.Head;
    [Range(0f, 1f)] public float followSmoothness = 0.15f;
    [Tooltip("Distance in pixels the radial menu's center keeps from the edges of the visible part of the window.")]
    public float radialScreenPadding = 120f;

    [Header("Perf")]
    public float avatarScanInterval = 0.25f;

    private static readonly List<MenuActions> Instances = new();

    private Xamin.CircleSelector radialMenu;
    private RectTransform radialRect;
    private Camera mainCam;

    private Transform modelRoot;
    private GameObject currentModel;
    private Animator currentAnimator;

    private Vector3 screenPosition;

    // Part of the window that hangs over the monitor edge, refreshed a few times per second.
    private const float RadialInsetRefreshSeconds = 0.2f;
    private float nextRadialInsetRefresh;
    private float radialInsetLeft, radialInsetTop, radialInsetRight, radialInsetBottom;

    private AvatarBigScreenHandler bigScreen;
    private FieldInfo bigScreenActiveField;
    private bool lastMoveCanvasState;
    private float nextAvatarScan;

    bool entryOpenPrevFrame;

    void OnEnable() { Instances.Add(this); }
    void OnDisable() { Instances.Remove(this); }

    void Start()
    {
        if (radialMenuObject != null)
        {
            radialMenu = radialMenuObject.GetComponent<Xamin.CircleSelector>();
            radialRect = radialMenuObject.GetComponent<RectTransform>();
        }

        modelRoot = GameObject.Find("Model")?.transform;
        mainCam = Camera.main;

        // Keep every menu on the visible part of the window (the window can hang over the screen edge).
        foreach (var entry in menuEntries)
        {
            if (entry.menu != null && !entry.menu.TryGetComponent<MenuScreenClamper>(out _))
                entry.menu.AddComponent<MenuScreenClamper>();
        }

        CacheBigScreen();
        nextAvatarScan = Time.unscaledTime;
        entryOpenPrevFrame = AnyEntryOpenLocal();
    }

    void Update()
    {
        if (Time.unscaledTime >= nextAvatarScan)
        {
            UpdateCurrentAvatar();
            nextAvatarScan = Time.unscaledTime + avatarScanInterval;
        }

        if (moveCanvas != null)
        {
            bool shouldBeActive = !IsBigScreenActive() && !IsMovementBlocked() && !TutorialMenu.IsActive;
            if (shouldBeActive != lastMoveCanvasState)
            {
                moveCanvas.SetActive(shouldBeActive);
                lastMoveCanvasState = shouldBeActive;
            }
        }

        HandleRadialMenu();
        entryOpenPrevFrame = AnyEntryOpenLocal();
    }

    void HandleRadialMenu()
    {
        if (radialMenu == null) return;

        bool keyDown = Input.GetKeyDown(radialMenuKey);
        if (!keyDown)
        {
            if (followBone && IsRadialOpen() && radialRect != null && currentAnimator != null)
            {
                var bone = currentAnimator.GetBoneTransform(targetBone);
                if (bone != null)
                {
                    Vector3 targetScreenPos = ClampRadialScreenPosition(mainCam.WorldToScreenPoint(bone.position));
                    screenPosition = Vector3.Lerp(screenPosition, targetScreenPos, 1f - followSmoothness);
                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(radialRect.parent as RectTransform, screenPosition, mainCam, out Vector3 worldPos))
                        radialRect.position = worldPos;
                }
            }
            return;
        }

        if (radialDraggingBlocks && currentAnimator != null && currentAnimator.GetBool("isDragging"))
            return;

        bool entryOpenNow = AnyEntryOpenLocal();

        if (IsRadialOpen())
        {
            radialMenu.Close();
            PlayMenuCloseSound();
            return;
        }

        if (entryOpenNow)
        {
            CloseAllMenus();
            PlayMenuCloseSound();
            return;
        }

        if (entryOpenPrevFrame && !entryOpenNow)
            return;

        CloseOtherRadials();
        if (followBone && currentAnimator != null)
        {
            var bone = currentAnimator.GetBoneTransform(targetBone);
            if (bone != null)
            {
                screenPosition = ClampRadialScreenPosition(mainCam.WorldToScreenPoint(bone.position));
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(radialRect.parent as RectTransform, screenPosition, mainCam, out Vector3 worldPos))
                    radialRect.position = worldPos;
            }
        }
        if (radialMenu.Open())
            PlayMenuOpenSound();
    }

    /// <summary>
    /// Keeps the radial menu's center on the visible part of the window: inside the window itself (the bone
    /// can be outside the camera frame) and inside the monitor (the window can hang over the screen edge).
    /// </summary>
    Vector3 ClampRadialScreenPosition(Vector3 position)
    {
        if (Screen.width <= 0 || Screen.height <= 0) return position;

        if (Time.unscaledTime >= nextRadialInsetRefresh)
        {
            nextRadialInsetRefresh = Time.unscaledTime + RadialInsetRefreshSeconds;
            radialInsetLeft = radialInsetTop = radialInsetRight = radialInsetBottom = 0f;
            var windowManager = WindowManager.Instance;
            if (windowManager != null &&
                windowManager.GetWindowOffscreenInsets(out RectInt windowRect, out int left, out int top, out int right, out int bottom) &&
                windowRect.width > 0 && windowRect.height > 0)
            {
                float kx = (float)Screen.width / windowRect.width;
                float ky = (float)Screen.height / windowRect.height;
                radialInsetLeft = left * kx;
                radialInsetRight = right * kx;
                radialInsetTop = top * ky;
                radialInsetBottom = bottom * ky;
            }
        }

        // Unity screen space has y pointing up, so the window's bottom inset is the low end.
        float minX = radialInsetLeft, maxX = Screen.width - radialInsetRight;
        float minY = radialInsetBottom, maxY = Screen.height - radialInsetTop;
        if (maxX - minX < 1f) { minX = 0f; maxX = Screen.width; }
        if (maxY - minY < 1f) { minY = 0f; maxY = Screen.height; }

        // In an area too small for the padding the menu is centered instead.
        float padX = Mathf.Min(Mathf.Max(0f, radialScreenPadding), (maxX - minX) * 0.5f);
        float padY = Mathf.Min(Mathf.Max(0f, radialScreenPadding), (maxY - minY) * 0.5f);

        position.x = Mathf.Clamp(position.x, minX + padX, maxX - padX);
        position.y = Mathf.Clamp(position.y, minY + padY, maxY - padY);
        return position;
    }

    void CacheBigScreen()
    {
        bigScreen = FindFirstObjectByType<AvatarBigScreenHandler>();
        if (bigScreen != null)
            bigScreenActiveField = typeof(AvatarBigScreenHandler).GetField("isBigScreenActive", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    bool IsBigScreenActive()
    {
        if (bigScreen == null || bigScreenActiveField == null) return false;
        object v = bigScreenActiveField.GetValue(bigScreen);
        return v is bool b && b;
    }

    void UpdateCurrentAvatar()
    {
        if (!modelRoot) return;

        for (int i = 0; i < modelRoot.childCount; i++)
        {
            var child = modelRoot.GetChild(i).gameObject;
            if (!child.activeInHierarchy) continue;
            if (currentModel == child) return;
            currentModel = child;
            currentAnimator = currentModel.GetComponent<Animator>();
            return;
        }
    }

    bool AnyEntryOpenLocal()
    {
        for (int i = 0; i < menuEntries.Count; i++)
        {
            var m = menuEntries[i].menu;
            if (m && m.activeInHierarchy) return true;
        }
        return false;
    }

    bool IsRadialOpen() => radialMenuObject && radialMenuObject.transform.localScale.x > 0.01f;

    void CloseOtherRadials()
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var inst = Instances[i];
            if (inst == null || inst == this) continue;
            if (inst.IsRadialOpen()) inst.radialMenu?.Close();
        }
    }

    public void CloseAllMenus()
    {
        for (int i = 0; i < menuEntries.Count; i++)
        {
            var m = menuEntries[i].menu;
            if (m) m.SetActive(false);
        }
        if (IsRadialOpen()) radialMenu?.Close();
    }

    public static bool IsMovementBlocked()
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var inst = Instances[i];
            if (inst == null) continue;
            if (inst.IsRadialOpen() && inst.radialBlockMovement) return true;
            var list = inst.menuEntries;
            for (int j = 0; j < list.Count; j++)
                if (list[j].menu && list[j].menu.activeInHierarchy && list[j].blockMovement)
                    return true;
        }
        return false;
    }

    public static bool IsHandTrackingBlocked()
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var inst = Instances[i];
            if (inst == null) continue;
            if (inst.IsRadialOpen() && inst.radialBlockHandTracking) return true;
            var list = inst.menuEntries;
            for (int j = 0; j < list.Count; j++)
                if (list[j].menu && list[j].menu.activeInHierarchy && list[j].blockHandTracking)
                    return true;
        }
        return false;
    }

    public static bool IsReactionBlocked()
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var inst = Instances[i];
            if (inst == null) continue;
            if (inst.IsRadialOpen() && inst.radialBlockReaction) return true;
            var list = inst.menuEntries;
            for (int j = 0; j < list.Count; j++)
                if (list[j].menu && list[j].menu.activeInHierarchy && list[j].blockReaction)
                    return true;
        }
        return false;
    }

    public static bool IsChibiModeBlocked()
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var inst = Instances[i];
            if (inst == null) continue;
            if (inst.IsRadialOpen() && inst.radialBlockChibiMode) return true;
            var list = inst.menuEntries;
            for (int j = 0; j < list.Count; j++)
                if (list[j].menu && list[j].menu.activeInHierarchy && list[j].blockChibiMode)
                    return true;
        }
        return false;
    }

    public static bool IsAnyMenuOpen()
    {
        for (int i = 0; i < Instances.Count; i++)
        {
            var inst = Instances[i];
            if (inst == null) continue;
            if (inst.IsRadialOpen()) return true;
            var list = inst.menuEntries;
            for (int j = 0; j < list.Count; j++)
            {
                var m = list[j].menu;
                if (m && m.activeInHierarchy) return true;
            }
        }
        return false;
    }

    void PlayMenuOpenSound() => FindFirstObjectByType<MenuAudioHandler>()?.PlayOpenSound();
    void PlayMenuCloseSound() => FindFirstObjectByType<MenuAudioHandler>()?.PlayCloseSound();
}
