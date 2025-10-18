using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Xamin;

/// <summary>
/// Sample script for OculusGO
/// https://developer.oculus.com/documentation/unity/latest/concepts/unity-ovrinput/
/// </summary>
public class OculusVRTracking : MonoBehaviour
{
    public CircleSelector pieMenu;
    [SerializeField] private Transform controller;

    public static bool leftHanded { get; private set; }

    private void Start()
    {
        Debug.Log("[IMPORTANT] To use this script you should import the Oculus Plugin and remove the comments below");
        pieMenu.controlType = CircleSelector.ControlType.customVector;
    }

    /*void Awake()
    {
#if UNITY_EDITOR
        leftHanded = false;
#else
        leftHanded = OVRInput.GetControllerPositionTracked(OVRInput.Controller.LTouch);
#endif
    }

    void Update()
    {
        OVRInput.Controller c = leftHanded ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        if (OVRInput.GetControllerPositionTracked(c))
        {
            controller.localRotation = OVRInput.GetLocalControllerRotation(c);
            controller.localPosition = OVRInput.GetLocalControllerPosition(c);
            Debug.Log("tracked");
        }
        else
        {
            Debug.Log("Can't find controller");
        }

        if (OVRInput.GetDown(OVRInput.Touch.PrimaryTouchpad))
            pieMenu.Open();
        if (OVRInput.Get(OVRInput.Touch.PrimaryTouchpad))
            pieMenu.CustomInputVector =
                OVRInput.Get(OVRInput.Axis2D.PrimaryTouchpad, OVRInput.Controller.RTrackedRemote);
        else if (OVRInput.GetUp(OVRInput.Touch.PrimaryTouchpad))
            pieMenu.Close();
    }*/
}