using UnityEngine;

public class RaiseMEWindow : MonoBehaviour
{
    public void Raise()
    {
        WindowManager.Instance.RaiseWindow();
    }
}
