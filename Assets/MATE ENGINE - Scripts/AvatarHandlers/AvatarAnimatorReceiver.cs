using UnityEngine;

public class AvatarAnimatorReceiver : MonoBehaviour
{
    [Header("Animator to be used by menus, etc.")]
    public Animator avatarAnimator;

    // Optionale ID oder Tag zur besseren Unterscheidung im Log
    public string avatarName = "Default";

    void Awake()
    {
        // Holt automatisch den Animator, wenn keiner gesetzt ist
        if (avatarAnimator == null)
            avatarAnimator = GetComponent<Animator>();

        LogWithColor($"[Receiver] Awake! Avatar: {avatarName}", "#bada55");
    }

    // Setzt einen neuen Animator (z.B. wenn Custom-Avatar geladen wird)
    public void SetAnimator(Animator newAnimator)
    {
        avatarAnimator = newAnimator;
        LogWithColor($"[Receiver] SetAnimator called! Neuer Animator gesetzt für: {avatarName} ({gameObject.name})", "#93FF4F");
    }

    void OnEnable()
    {
        LogWithColor($"[Receiver] Aktiviert: {avatarName} ({gameObject.name})", "#4FFFD7");
    }

    void OnDisable()
    {
        LogWithColor($"[Receiver] Deaktiviert: {avatarName} ({gameObject.name})", "#FFBC4F");
    }

    void OnDestroy()
    {
        LogWithColor($"[Receiver] Destroyed: {avatarName} ({gameObject.name})", "#FF4848");
    }

    // Für Debugging: Colorful Log
    void LogWithColor(string msg, string hexColor)
    {
        Debug.Log($"<color={hexColor}>{msg}</color>", this);
    }
}
