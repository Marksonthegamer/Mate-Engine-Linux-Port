using UnityEngine;
using System.Collections.Generic;

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

    private static MenuActions Instance;
    private Xamin.CircleSelector radialMenu;
    private RectTransform radialRect;
    private Camera mainCam;

    private Transform modelRoot;
    private GameObject currentModel;
    private Animator currentAnimator;

    private Vector3 screenPosition;

    void Awake() => Instance = this;

    void Start()
    {
        if (radialMenuObject != null)
        {
            radialMenu = radialMenuObject.GetComponent<Xamin.CircleSelector>();
            radialRect = radialMenuObject.GetComponent<RectTransform>();
        }

        modelRoot = GameObject.Find("Model")?.transform;
        mainCam = Camera.main;
    }

    void Update()
    {
        UpdateCurrentAvatar();

        if (moveCanvas != null)
        {
            var bigScreen = FindFirstObjectByType<AvatarBigScreenHandler>();
            bool isBigScreen = bigScreen != null && bigScreen.GetType()
                .GetField("isBigScreenActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(bigScreen) as bool? == true;

            if (!isBigScreen)
                moveCanvas.SetActive(!IsMovementBlocked() && !TutorialMenu.IsActive);
        }


        HandleRadialMenu();
    }

    void HandleRadialMenu()
    {
        if (Input.GetKeyDown(radialMenuKey) && radialMenu != null)
        {
            if (radialDraggingBlocks && currentAnimator != null && currentAnimator.GetBool("isDragging"))
                return;

            if (IsAnyMenuOpen())

            {
                CloseAllMenus();
                PlayMenuCloseSound();
            }
            else
            {
                if (followBone && currentAnimator != null)
                {
                    var bone = currentAnimator.GetBoneTransform(targetBone);
                    if (bone != null)
                    {
                        screenPosition = mainCam.WorldToScreenPoint(bone.position);
                        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(radialRect.parent as RectTransform, screenPosition, mainCam, out Vector3 worldPos))
                            radialRect.position = worldPos;
                    }
                }
                if (radialMenu.Open())
                {
                    PlayMenuOpenSound();
                }

            }
        }

        if (followBone && IsRadialOpen() && radialRect != null && currentAnimator != null)
        {
            var bone = currentAnimator.GetBoneTransform(targetBone);
            if (bone != null)
            {
                Vector3 targetScreenPos = mainCam.WorldToScreenPoint(bone.position);
                screenPosition = Vector3.Lerp(screenPosition, targetScreenPos, 1f - followSmoothness);

                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(radialRect.parent as RectTransform, screenPosition, mainCam, out Vector3 worldPos))
                    radialRect.position = worldPos;
            }
        }
    }

    private void UpdateCurrentAvatar()
    {
        if (!modelRoot) return;

        for (int i = 0; i < modelRoot.childCount; i++)
        {
            var child = modelRoot.GetChild(i);
            if (child.gameObject.activeInHierarchy)
            {
                if (currentModel != child.gameObject)
                {
                    currentModel = child.gameObject;
                    currentAnimator = currentModel.GetComponent<Animator>();
                }
                return;
            }
        }
    }

    public static bool IsMovementBlocked()
    {
        if (Instance == null) return false;

        if (Instance.IsRadialOpen() && Instance.radialBlockMovement) return true;

        foreach (var entry in Instance.menuEntries)
            if (entry.menu.activeInHierarchy && entry.blockMovement)
                return true;

        return false;
    }

    public static bool IsHandTrackingBlocked()
    {
        if (Instance == null) return false;

        if (Instance.IsRadialOpen() && Instance.radialBlockHandTracking) return true;

        foreach (var entry in Instance.menuEntries)
            if (entry.menu.activeInHierarchy && entry.blockHandTracking)
                return true;

        return false;
    }

    public static bool IsReactionBlocked()
    {
        if (Instance == null) return false;

        if (Instance.IsRadialOpen() && Instance.radialBlockReaction) return true;

        foreach (var entry in Instance.menuEntries)
            if (entry.menu.activeInHierarchy && entry.blockReaction)
                return true;

        return false;
    }

    public static bool IsChibiModeBlocked()
    {
        if (Instance == null) return false;

        if (Instance.IsRadialOpen() && Instance.radialBlockChibiMode) return true;

        foreach (var entry in Instance.menuEntries)
            if (entry.menu.activeInHierarchy && entry.blockChibiMode)
                return true;

        return false;
    }

    public static bool IsAnyMenuOpen()
    {
        if (Instance == null) return false;
        if (Instance.IsRadialOpen()) return true;

        foreach (var entry in Instance.menuEntries)
            if (entry.menu.activeInHierarchy)
                return true;

        return false;
    }

    private bool IsRadialOpen() => radialMenuObject && radialMenuObject.transform.localScale.x > 0.01f;

    public void CloseAllMenus()
    {
        foreach (var entry in menuEntries)
            entry.menu?.SetActive(false);

        if (IsRadialOpen()) radialMenu?.Close();

        // AvatarSettingsMenu.IsMenuOpen = false;
    }

    void PlayMenuOpenSound() => FindFirstObjectByType<MenuAudioHandler>()?.PlayOpenSound();
    void PlayMenuCloseSound() => FindFirstObjectByType<MenuAudioHandler>()?.PlayCloseSound();
}
