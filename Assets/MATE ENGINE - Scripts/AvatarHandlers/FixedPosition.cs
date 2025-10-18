using UnityEngine;

public class FixedPosition : MonoBehaviour
{
    private Vector3 fixedPosition;

    void Start()
    {
        fixedPosition = transform.position;
    }

    void LateUpdate()
    {
        transform.position = fixedPosition;
    }
}
