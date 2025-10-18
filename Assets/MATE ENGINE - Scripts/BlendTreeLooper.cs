using UnityEngine;

public class BlendTreeLooper : StateMachineBehaviour
{
    [Tooltip("Name of the Float parameter that controls the BlendTree")]
    public string blendParam = "Index";

    [Tooltip("How many animations are inside the BlendTree")]
    public int animationCount = 6;

    [Tooltip("How long each animation should play before switching (in seconds)")]
    public float animationDuration = 2f;

    [Tooltip("How long the transition between two animations should take (in seconds, 0 = instant)")]
    [Range(0f, 10f)]
    public float transitionDuration = 1f;

    private float timer;
    private float currentValue;
    private float targetValue;
    private bool isTransitioning;
    private float transitionTimer;

    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timer = 0f;
        transitionTimer = 0f;
        isTransitioning = false;
        currentValue = 0f;
        targetValue = 0f;

        animator.SetFloat(blendParam, currentValue);
    }

    override public void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        timer += Time.deltaTime;

        if (!isTransitioning && timer >= animationDuration)
        {
            // Start a new transition to the next animation index (always forward)
            timer = 0f;
            transitionTimer = 0f;
            isTransitioning = true;

            targetValue = currentValue + 1f; // always increase
        }

        if (isTransitioning)
        {
            transitionTimer += Time.deltaTime;
            float t = transitionDuration > 0f ? transitionTimer / transitionDuration : 1f;

            // Smooth slide forward
            float rawValue = Mathf.Lerp(currentValue, targetValue, t);

            // Wrap into 0..animationCount
            float wrappedValue = Mathf.Repeat(rawValue, animationCount);

            animator.SetFloat(blendParam, wrappedValue);

            if (t >= 1f)
            {
                // Transition finished
                isTransitioning = false;
                currentValue = targetValue;
            }
        }
        else
        {
            // Keep setting the current wrapped value while waiting
            float wrappedValue = Mathf.Repeat(currentValue, animationCount);
            animator.SetFloat(blendParam, wrappedValue);
        }
    }
}
