using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using X11;

public class AvatarBigScreenHandler : MonoBehaviour
{
    [Header("Keybinds")]
    public List<KeyCode> ToggleKeys = new List<KeyCode> { KeyCode.B };

    [Header("Animator & Bone Selection")]
    public Animator avatarAnimator;
    public HumanBodyBones attachBone = HumanBodyBones.Head;

    [Header("Camera")]
    public Camera MainCamera;
    [Tooltip("Override for Zoom: Camera FOV (Perspective) or Size (Orthographic). 0 = auto.")]
    public float TargetZoom;
    public float ZoomMoveSpeed = 10f;
    [Tooltip("Y-Offset to bone position (meters, before scaling)")]
    public float YOffset = 0.08f;

    [Header("Fade Animation")]
    public float FadeYOffset = 0.5f;
    public float FadeInDuration = 0.5f;
    public float FadeOutDuration = 0.5f;

    [Header("Canvas Blocking")]
    public GameObject moveCanvas;

    private IntPtr unityHWND = IntPtr.Zero;
    private bool isBigScreenActive;
    private Vector3 originalCamPos;
    private Quaternion originalCamRot;
    private float originalFOV;
    private float originalOrthoSize;
    private Rect originalWindowRect;
    private bool originalRectSet;
    private Transform bone;
    private AvatarAnimatorController avatarAnimatorController;
    private bool moveCanvasWasActive;
    private Coroutine fadeCoroutine;
    private bool isFading;
    private bool isInDesktopTransition;

    public static List<AvatarBigScreenHandler> ActiveHandlers = new List<AvatarBigScreenHandler>();

    void OnEnable()
    {
        if (!ActiveHandlers.Contains(this))
            ActiveHandlers.Add(this);
    }
    void OnDisable()
    {
        ActiveHandlers.Remove(this);
    }

    public void ToggleBigScreenFromUI()
    {
        if (!isBigScreenActive)
            ActivateBigScreen();
        else
            DeactivateBigScreen();
    }

    void Start()
    {
        unityHWND = X11Manager.Instance.UnityWindow;
        if (MainCamera == null) MainCamera = Camera.main;
        if (avatarAnimator == null) avatarAnimator = GetComponent<Animator>();
        if (MainCamera != null)
        {
            originalCamPos = MainCamera.transform.position;
            originalCamRot = MainCamera.transform.rotation;
            originalFOV = MainCamera.fieldOfView;
            originalOrthoSize = MainCamera.orthographicSize;
        }
        if (unityHWND != IntPtr.Zero && X11Manager.Instance.GetWindowRect(unityHWND, out Rect r))
        {
            originalWindowRect = r;
            originalRectSet = true;
        }
        avatarAnimatorController = GetComponent<AvatarAnimatorController>();
    }

    public void SetAnimator(Animator a) => avatarAnimator = a;

    void Update()
    {
        foreach (var key in ToggleKeys)
        {
            if (Input.GetKeyDown(key))
            {
                if (!isBigScreenActive && !isFading)
                    ActivateBigScreen();
                else if (isBigScreenActive && !isFading)
                    DeactivateBigScreen();
                break;
            }
        }
        if (isBigScreenActive && MainCamera != null && bone != null && avatarAnimator != null && !isFading && !isInDesktopTransition)
            UpdateBigScreenCamera();
    }

    void UpdateBigScreenCamera()
    {
        var scale = avatarAnimator.transform.lossyScale.y;
        var headPos = bone.position;
        var neck = avatarAnimator.GetBoneTransform(HumanBodyBones.Neck);
        float headHeight = Mathf.Max(0.12f, neck ? Mathf.Abs(headPos.y - neck.position.y) : 0.25f) * scale;
        float buffer = 1.4f;

        Vector3 camPos = originalCamPos;
        camPos.y = headPos.y + YOffset * scale;
        MainCamera.transform.position = camPos;
        MainCamera.transform.rotation = Quaternion.identity;

        if (TargetZoom > 0f)
        {
            if (MainCamera.orthographic) MainCamera.orthographicSize = TargetZoom * scale;
            else MainCamera.fieldOfView = TargetZoom;
        }
        else
        {
            if (MainCamera.orthographic)
                MainCamera.orthographicSize = headHeight * buffer;
            else
            {
                float dist = Mathf.Abs(MainCamera.transform.position.z - headPos.z);
                MainCamera.fieldOfView = Mathf.Clamp(
                    2f * Mathf.Atan((headHeight * buffer) / (2f * dist)) * Mathf.Rad2Deg, 10f, 60f);
            }
        }
    }

    void ActivateBigScreen()
    {
        if (isBigScreenActive) return;
        SaveCameraState();

        if (moveCanvas != null)
            moveCanvasWasActive = moveCanvas.activeSelf;

        isBigScreenActive = true;
        if (avatarAnimator != null) avatarAnimator.SetBool("isBigScreen", true);
        if (avatarAnimatorController != null) avatarAnimatorController.BlockDraggingOverride = true;
        if (moveCanvas != null && moveCanvas.activeSelf) moveCanvas.SetActive(false);

        bone = avatarAnimator ? avatarAnimator.GetBoneTransform(attachBone) : null;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(BigScreenEnterSequence());
    }

    void DeactivateBigScreen()
    {
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(BigScreenExitSequence());
    }

    void SaveCameraState()
    {
        if (MainCamera != null)
        {
            originalCamPos = MainCamera.transform.position;
            originalCamRot = MainCamera.transform.rotation;
            originalFOV = MainCamera.fieldOfView;
            originalOrthoSize = MainCamera.orthographicSize;
        }
    }

    IEnumerator FadeCameraY(bool fadeIn)
    {
        isFading = true;
        if (avatarAnimator == null || bone == null || MainCamera == null)
        { isFading = false; yield break; }

        var scale = avatarAnimator.transform.lossyScale.y;
        var headPos = bone.position;
        float baseY = headPos.y + YOffset * scale;
        float fadeY = baseY + FadeYOffset;

        Vector3 camPos = MainCamera.transform.position;
        float fromY = fadeIn ? fadeY : baseY;
        float toY = fadeIn ? baseY : fadeY;
        float duration = fadeIn ? FadeInDuration : FadeOutDuration;
        float time = 0f;

        var neck = avatarAnimator.GetBoneTransform(HumanBodyBones.Neck);
        float headHeight = Mathf.Max(0.12f, neck ? Mathf.Abs(headPos.y - neck.position.y) : 0.25f) * scale;
        float buffer = 1.4f;

        while (time < duration)
        {
            float curve = Mathf.SmoothStep(0, 1, time / duration);
            camPos.y = Mathf.Lerp(fromY, toY, curve);
            MainCamera.transform.position = camPos;
            MainCamera.transform.rotation = Quaternion.identity;
            if (TargetZoom > 0f)
            {
                if (MainCamera.orthographic) MainCamera.orthographicSize = TargetZoom * scale;
                else MainCamera.fieldOfView = TargetZoom;
            }
            else
            {
                if (MainCamera.orthographic)
                    MainCamera.orthographicSize = headHeight * buffer;
                else
                {
                    float dist = Mathf.Abs(MainCamera.transform.position.z - headPos.z);
                    MainCamera.fieldOfView = Mathf.Clamp(
                        2f * Mathf.Atan((headHeight * buffer) / (2f * dist)) * Mathf.Rad2Deg, 10f, 60f);
                }
            }
            time += Time.deltaTime;
            yield return null;
        }

        camPos.y = toY;
        MainCamera.transform.position = camPos;
        MainCamera.transform.rotation = Quaternion.identity;
        if (TargetZoom > 0f)
        {
            if (MainCamera.orthographic) MainCamera.orthographicSize = TargetZoom * scale;
            else MainCamera.fieldOfView = TargetZoom;
        }
        else
        {
            if (MainCamera.orthographic)
                MainCamera.orthographicSize = headHeight * buffer;
            else
            {
                float dist = Mathf.Abs(MainCamera.transform.position.z - headPos.z);
                MainCamera.fieldOfView = Mathf.Clamp(
                    2f * Mathf.Atan((headHeight * buffer) / (2f * dist)) * Mathf.Rad2Deg, 10f, 60f);
            }
        }
        isFading = false;

        if (!fadeIn)
        {
            isBigScreenActive = false;
            if (avatarAnimator != null) avatarAnimator.SetBool("isBigScreen", false);
            if (avatarAnimatorController != null) avatarAnimatorController.BlockDraggingOverride = false;
            if (moveCanvas != null && moveCanvasWasActive) moveCanvas.SetActive(true);
            if (unityHWND != IntPtr.Zero && originalRectSet)
            {
                X11Manager.Instance.SetWindowPosition(new Vector2(originalWindowRect.x, originalWindowRect.y));
            }
            if (MainCamera != null)
            {
                MainCamera.transform.position = originalCamPos;
                MainCamera.transform.rotation = originalCamRot;
                MainCamera.fieldOfView = originalFOV;
                MainCamera.orthographicSize = originalOrthoSize;
            }
        }
    }

    Rect FindBestMonitorRect(Rect windowRect)
    {
        /*
        List<Rect> monitorRects = new List<Rect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref Rect lprcMonitor, IntPtr data) =>{ monitorRects.Add(lprcMonitor); return true; }, IntPtr.Zero);
        int idx = 0, maxArea = 0;
        for (int i = 0; i < monitorRects.Count; i++)
        {
            int overlap = OverlapArea(windowRect, monitorRects[i]);
            if (overlap > maxArea) { idx = i; maxArea = overlap; }
        }
        return monitorRects.Count > 0 ? monitorRects[idx] : new Rect { x = 0, y = 0, width = Screen.currentResolution.width, height = Screen.currentResolution.height };
        */
        return new Rect { x = 0, y = 0, width = Screen.currentResolution.width, height = Screen.currentResolution.height };
    }
    int OverlapArea(Rect a, Rect b)
    {
        int x1 = (int)Math.Max(a.x, b.x), x2 = (int)Math.Min(a.width, b.width);
        int y1 = (int)Math.Max(a.y, b.y), y2 = (int)Math.Min(a.height, b.height);
        int w = x2 - x1, h = y2 - y1;
        return (w > 0 && h > 0) ? w * h : 0;
    }

    IEnumerator GlideAvatarDesktop(float duration, bool toFadeY)
    {
        isInDesktopTransition = true;
        if (avatarAnimator == null || bone == null || MainCamera == null)
        { isInDesktopTransition = false; yield break; }

        var scale = avatarAnimator.transform.lossyScale.y;
        var headPos = bone.position;
        float baseY = headPos.y + YOffset * scale;
        float fadeY = baseY + FadeYOffset;

        Vector3 camPos = MainCamera.transform.position;
        float fromY = toFadeY ? baseY : fadeY;
        float toY = toFadeY ? fadeY : baseY;
        float time = 0f;

        while (time < duration)
        {
            camPos.y = Mathf.Lerp(fromY, toY, Mathf.SmoothStep(0, 1, time / duration));
            MainCamera.transform.position = camPos;
            time += Time.deltaTime;
            yield return null;
        }
        camPos.y = toY;
        MainCamera.transform.position = camPos;

        if (toFadeY && unityHWND != IntPtr.Zero)
        {
            if (X11Manager.Instance.GetWindowRect(unityHWND, out Rect windowRect))
            {
                Rect targetScreen = FindBestMonitorRect(windowRect);
                X11Manager.Instance.SetWindowPosition(targetScreen.x, targetScreen.y);
                originalWindowRect = windowRect;
                originalRectSet = true;
            }
        }
        if (!toFadeY && MainCamera != null)
        {
            MainCamera.transform.position = originalCamPos;
            MainCamera.transform.rotation = originalCamRot;
            MainCamera.fieldOfView = originalFOV;
            MainCamera.orthographicSize = originalOrthoSize;
        }
        isInDesktopTransition = false;
    }

    IEnumerator BigScreenEnterSequence()
    {
        yield return StartCoroutine(GlideAvatarDesktop(0.4f, true));
        yield return StartCoroutine(FadeCameraY(true));
    }
    IEnumerator BigScreenExitSequence()
    {
        yield return StartCoroutine(FadeCameraY(false));
        yield return StartCoroutine(GlideAvatarDesktop(0.4f, false));
    }
}
