using UnityEngine;

public class FixedPosition : MonoBehaviour
{
    [Tooltip("When enabled, the transform is pinned to the fixed position every frame (cancels root motion drift).")]
    public bool lockPosition = true;

    [Tooltip("World units the avatar is raised above its spawn point.")]
    public float verticalRestOffset = 0f;

    private Vector3 fixedPosition;
    private Vector3 homePosition;

    /// <summary>The avatar's rest position: where it spawned.</summary>
    public Vector3 HomePosition => homePosition;

    /// <summary>
    /// The position the avatar is pinned to. Setting it moves the avatar, also while locked.
    /// </summary>
    public Vector3 Position
    {
        get => lockPosition ? fixedPosition : transform.position;
        set
        {
            fixedPosition = value;
            transform.position = value;
        }
    }

    void Start()
    {
        fixedPosition = transform.position + (verticalRestOffset != 0f ? Vector3.up * verticalRestOffset : Vector3.zero);
        homePosition = fixedPosition;
        transform.position = fixedPosition;

        // Scenes and prefabs only carry FixedPosition, so the in-window drag handler is attached here.
        if (!TryGetComponent<AvatarFreeMovement>(out _))
            gameObject.AddComponent<AvatarFreeMovement>();
    }

    void LateUpdate()
    {
        if (lockPosition)
            transform.position = fixedPosition;
        else
            fixedPosition = transform.position;
    }

    public void ResetToHome() => Position = homePosition;
}
