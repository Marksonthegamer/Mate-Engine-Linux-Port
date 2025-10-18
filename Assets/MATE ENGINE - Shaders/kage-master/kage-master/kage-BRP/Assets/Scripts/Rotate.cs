using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    [Range(-200.0f, 200.0f)]
    public float speed = 10.0f;
    public Transform center;

    public Transform lookAt;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        transform.RotateAround(center.position, Vector3.up, speed * Time.deltaTime);

        if (lookAt != null)
        {
            transform.LookAt(lookAt, Vector3.up);
        }
    }
}
