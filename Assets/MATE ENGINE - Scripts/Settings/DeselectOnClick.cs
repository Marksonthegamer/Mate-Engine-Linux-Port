using UnityEngine;
using UnityEngine.EventSystems;

public class DeselectOnClick : MonoBehaviour
{
    public void Deselect()
    {
        EventSystem.current.SetSelectedGameObject(null);
    }
}
