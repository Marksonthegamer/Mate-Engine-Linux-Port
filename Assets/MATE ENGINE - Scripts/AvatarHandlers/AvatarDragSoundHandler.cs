using UnityEngine;

public class AvatarDragSoundHandler : MonoBehaviour
{
    [Header("Sound Settings")]
    public AudioSource dragStartSound, dragStopSound;
    [Range(0, 100)] public float maxHighPitchPercent = 10f, maxLowPitchPercent = 10f;

    private bool wasDragging;
    private AvatarAnimatorController avatarController;

    void Start()
    {
        avatarController = GetComponent<AvatarAnimatorController>();
        if (!avatarController) Debug.LogError("AvatarAnimatorController script not found on this GameObject.");
    }

    void Update()
    {
        if (!avatarController) return;
        bool dragging = avatarController.isDragging;
        if (dragging != wasDragging)
        {
            if (dragging) PlaySound(dragStartSound);
            else PlaySound(dragStopSound);
            wasDragging = dragging;
        }
    }

    void PlaySound(AudioSource audio)
    {
        if (!audio) return;
        float low = 1f - maxLowPitchPercent / 100f, high = 1f + maxHighPitchPercent / 100f;
        audio.pitch = Random.Range(low, high);
        audio.Play();
    }
}
