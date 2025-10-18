using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;

public class DeleteButtonHoldHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [HideInInspector] public AvatarLibraryMenu.AvatarEntry entry;

    [Header("UI References")]
    public TMP_Text labelText;
    public AudioSource audioSource;
    public AudioClip tickSound;
    public AudioClip completeSound;

    private Coroutine holdRoutine;
    private bool isHolding = false;

    private void Start()
    {
        UpdateButtonLabel();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (holdRoutine == null)
        {
            isHolding = true;
            holdRoutine = StartCoroutine(HoldToDelete());
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isHolding = false;
    }

    private void UpdateButtonLabel()
    {
        if (labelText != null)
            labelText.text = "Delete";
    }

    private IEnumerator HoldToDelete()
    {
        float duration = 3f;
        float timeHeld = 0f;
        int lastSecond = -1;
        float pitch = 1f;
        bool completed = false;

        if (labelText != null) labelText.text = "3";
        GetComponent<Button>().interactable = false;

        while (isHolding && timeHeld < duration)
        {
            timeHeld += Time.deltaTime;
            int currentSecond = Mathf.CeilToInt(duration - timeHeld);

            if (currentSecond != lastSecond)
            {
                lastSecond = currentSecond;
                if (labelText != null) labelText.text = currentSecond.ToString();

                if (audioSource != null && tickSound != null)
                {
                    audioSource.pitch = pitch;
                    audioSource.PlayOneShot(tickSound);
                    pitch += 0.1f;
                }
            }
            yield return null;
        }

        if (timeHeld >= duration && isHolding)
        {
            completed = true;
            if (labelText != null) labelText.text = "0";

            if (audioSource != null && completeSound != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(completeSound);
            }

            yield return new WaitForSeconds(0.5f);
            if (entry != null)
            {
                var menu = FindFirstObjectByType<AvatarLibraryMenu>();
                if (menu != null)
                    menu.SendMessage("RemoveAvatar", entry, SendMessageOptions.DontRequireReceiver);
            }

            if (labelText != null) labelText.text = "Deleted!";
            yield return new WaitForSeconds(1.0f);
        }

        if (!completed && labelText != null)
            labelText.text = "Delete";

        GetComponent<Button>().interactable = true;
        holdRoutine = null;
    }
}
