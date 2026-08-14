using UnityEngine;

public class ResetPosition : MonoBehaviour
{
    public float distanceToReset = 5;

    private Vector3 startPosition;

	// Use this for initialization
	void Start ()
    {
        startPosition = transform.position;
	}
	
	// Update is called once per frame
	void Update ()
    {
        if (Vector3.Distance(startPosition, transform.position) >= distanceToReset)
        {
            transform.position = startPosition;
        }
	}
}
