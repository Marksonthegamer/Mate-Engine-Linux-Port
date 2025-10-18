using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonLinker : MonoBehaviour
{
    [Tooltip("The button whose OnClick event will be invoked when this button is clicked.")]
    public Button targetButton;

    [Tooltip("Optional: The toggle to be toggled when this button is clicked.")]
    public Toggle targetToggle;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(InvokeTargets);
    }

    private void InvokeTargets()
    {
        if (targetButton != null)
            targetButton.onClick.Invoke();

        if (targetToggle != null)
            targetToggle.isOn = !targetToggle.isOn;

        if (targetButton == null && targetToggle == null)
            Debug.LogWarning("ButtonLinker: No target button or toggle assigned!", this);
    }
}
