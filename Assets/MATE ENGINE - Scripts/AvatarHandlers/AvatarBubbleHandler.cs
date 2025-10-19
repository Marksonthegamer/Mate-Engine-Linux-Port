using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class AvatarBubbleHandler : MonoBehaviour
{
    [Header("Animator")]
    public Animator avatarAnimator;
    public string animatorParameter = "isSitting";
    [Header("Attach Settings")]
    public GameObject attachTarget;
    public HumanBodyBones attachBone = HumanBodyBones.Head;
    public bool keepOriginalRotation;
    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip enableSound;
    public AudioClip disableSound;
    [Header("Interaction")]
    public KeyCode activationKey = KeyCode.Space;
    [Header("Spawn Animation")]
    [Range(0f, 1f)] public float spawnAnimationSpeed = 0.1f;

    private Animator animator;
    private Transform bone;
    private Transform originalParent;
    private Vector3 originalScale = Vector3.one;
    private float currentLerp;
    private bool wasActive;
    private bool initialized;
    public static List<AvatarBubbleHandler> ActiveHandlers = new List<AvatarBubbleHandler>();


    void OnEnable()
    {
        if (!Application.isPlaying) return;

        animator = avatarAnimator != null ? avatarAnimator : GetComponent<Animator>();

        if (!ActiveHandlers.Contains(this))
            ActiveHandlers.Add(this);


        if (attachTarget != null)
        {
            originalParent = attachTarget.transform.parent;

            if (originalScale == Vector3.zero || attachTarget.transform.localScale == Vector3.zero)
                originalScale = new Vector3(1f, 1f, 1f);
            attachTarget.transform.localScale = Vector3.zero;
            attachTarget.SetActive(false);
            currentLerp = 0f;
            wasActive = false;
            initialized = true;
        }
        bone = null;
    }

    void OnDisable()
    {
        if (!Application.isPlaying) return;
        ActiveHandlers.Remove(this);


        if (attachTarget != null)
        {
            attachTarget.transform.localScale = Vector3.zero;
            attachTarget.SetActive(false);
        }

        bone = null;
        wasActive = false;
        currentLerp = 0f;
        initialized = false;
    }

    void Update()
    {
        if (!Application.isPlaying || animator == null || attachTarget == null) return;
        if (bone == null)
            bone = animator.GetBoneTransform(attachBone);
        if (IsDragging() && Input.GetKeyDown(activationKey))
        {
            bool isWindowSit = animator.GetBool("isWindowSit");
            if (!isWindowSit)
            {
                bool newState = !animator.GetBool(animatorParameter);
                animator.SetBool(animatorParameter, newState);
            }
        }

        if (animator != null && animator.GetBool("isBigScreen"))
        {
            if (animator.GetBool(animatorParameter))
                animator.SetBool(animatorParameter, false);
            if (attachTarget != null)
                attachTarget.SetActive(false);
            wasActive = false;
            currentLerp = 0f;
            return;
        }

        bool shouldBeActive = animator.GetBool(animatorParameter);
        float target = shouldBeActive ? 1f : 0f;
        float speed = Mathf.Lerp(10f, 0.25f, spawnAnimationSpeed);
        currentLerp = Mathf.MoveTowards(currentLerp, target, Time.unscaledDeltaTime * speed);

        if (!wasActive && currentLerp > 0f)
        {
            attachTarget.SetActive(true);
            PlaySound(enableSound);
            wasActive = true;
        }

        Vector3 avatarScale = animator.transform.lossyScale;
        attachTarget.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.Scale(originalScale, avatarScale), currentLerp);


        if (wasActive && currentLerp <= 0f)
        {
            attachTarget.SetActive(false);
            PlaySound(disableSound);
            wasActive = false;
        }

        if (shouldBeActive && bone != null)
        {
            if (keepOriginalRotation)
                attachTarget.transform.position = bone.position;
            else if (attachTarget.transform.parent != bone)
                attachTarget.transform.SetParent(bone, false);
        }
        else if (!shouldBeActive && attachTarget.transform.parent != originalParent)
        {
            attachTarget.transform.SetParent(originalParent, false);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    public void SetAnimator(Animator newAnimator)
    {
        avatarAnimator = newAnimator;
        animator = newAnimator;
        bone = null;
    }

    private bool IsDragging()
    {
        return animator != null && animator.GetBool("isDragging");
    }
    public void ToggleBubbleFromUI()
    {
        if (animator == null) return;

        bool isWindowSit = animator.GetBool("isWindowSit");
        if (isWindowSit) return;

        bool newState = !animator.GetBool(animatorParameter);
        animator.SetBool(animatorParameter, newState);
    }
}
