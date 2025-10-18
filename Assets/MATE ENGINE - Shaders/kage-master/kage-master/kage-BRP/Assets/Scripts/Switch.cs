using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Switch : MonoBehaviour
{

    public float period = 2.0f;

    public Material lightOnMat;
    public Material lightOffMat;

    public new Light light;

    public bool lightOn = true;

    private float curTime = 0.0f;

    void SwitchLight()
    {
        if (light != null)
            light.enabled = lightOn;
        if (lightOn)
        {
            GetComponent<Renderer>().material = lightOnMat;
        }
        else
        {
            GetComponent<Renderer>().material = lightOffMat;
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        SwitchLight();
    }

    // Update is called once per frame
    void Update()
    {
        curTime += Time.deltaTime;
        if (curTime > period)
        {
            curTime = 0f;
            lightOn = !lightOn;
            SwitchLight();
        }
    }
}
