using UnityEngine;

public class AvatarSleepController : MonoBehaviour
{
    [Header("Enable Sleep Feature")]
    public bool enableSleep;

    [Header("Sleep Timer (seconds)")]
    [Range(30f, 360f)]
    public float sleepTimer = 60f;

    [Header("Allowed States (Whitelist)")]
    public string[] allowedStates = { "Idle", "Sleeping" };

    [Header("Wake Up If Any Of These Animator Bools Is True")]
    public string[] wakeUpBools = { "isDragging" };

    [Header("Debug Info (Read Only)")]
    [SerializeField] private float idleTime;
    [SerializeField] private string currentState = "";
    [SerializeField] private bool isSleeping;

    private Animator animator;
    private static readonly int isSleepingParam = Animator.StringToHash("IsSleeping");

    void Start()
    {
        animator = GetComponent<Animator>();
        SetSleeping(false);
        idleTime = 0f;
    }

    void Update()
    {
        if (!enableSleep || animator == null)
        {
            SetSleeping(false);
            idleTime = 0f;
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        currentState = GetCurrentStateName(state);

        if (IsAnyWakeUpBoolTrue())
        {
            WakeUp();
            return;
        }

        bool allowed = IsInAllowedState(state);

        if (allowed)
        {
            if (!isSleeping)
                idleTime += Time.deltaTime;
            if (idleTime >= sleepTimer && !isSleeping)
                SetSleeping(true);
        }
        else
        {
            idleTime = 0f;
            SetSleeping(false);
        }

        if (isSleeping && !allowed)
        {
            SetSleeping(false);
            idleTime = 0f;
        }
    }

    string GetCurrentStateName(AnimatorStateInfo state)
    {
        foreach (var s in allowedStates)
            if (!string.IsNullOrEmpty(s) && state.IsName(s))
                return s;
        return state.shortNameHash.ToString();
    }

    bool IsInAllowedState(AnimatorStateInfo state)
    {
        if (allowedStates == null || allowedStates.Length == 0)
            return true;
        foreach (var s in allowedStates)
            if (!string.IsNullOrEmpty(s) && state.IsName(s))
                return true;
        return false;
    }

    bool IsAnyWakeUpBoolTrue()
    {
        if (wakeUpBools == null || wakeUpBools.Length == 0 || animator == null)
            return false;
        foreach (var b in wakeUpBools)
        {
            if (!string.IsNullOrEmpty(b) && animator.GetBool(b))
                return true;
        }
        return false;
    }

    void SetSleeping(bool value)
    {
        if (isSleeping == value)
            return;
        isSleeping = value;
        if (animator != null)
            animator.SetBool(isSleepingParam, value);
    }

    public void WakeUp()
    {
        SetSleeping(false);
        idleTime = 0f;
    }
}
