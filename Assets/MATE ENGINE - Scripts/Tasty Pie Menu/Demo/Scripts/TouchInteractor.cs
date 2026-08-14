using UnityEngine;
using Xamin;

/// <summary>
/// Sample class for touch screen interactions.
/// </summary>
[RequireComponent(typeof(Camera))]
public class TouchInteractor : MonoBehaviour {
    
    public CircleSelector menu;
    private Camera _cam;

    private void Start()
    {
        _cam = GetComponent<Camera>();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            menu.Open(Input.mousePosition);
        }
    }
}
