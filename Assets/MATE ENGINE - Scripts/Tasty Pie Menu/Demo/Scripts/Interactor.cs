using System;
using UnityEngine;
using System.Collections;
using UnityEngine.UI;
//using UnityStandardAssets.Characters.FirstPerson;
//using UnityStandardAssets.ImageEffects;
using Xamin;
using Xamin.Demo;

[RequireComponent(typeof(Camera))]
public class Interactor : MonoBehaviour {

    public MouseLook mouseLook;
    public CircleSelector menu;
    public Text guiText;
    private Camera _cam;

    private void Start()
    {
        _cam = GetComponent<Camera>();
    }

    void Update()
    {
        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5F, 0.5F, 0));
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            print("I'm looking at " + hit.collider.tag);
            if (hit.collider.CompareTag("CoffeMug"))
            {
                currentMug = hit.collider.GetComponent<CoffeMug>();
                guiText.gameObject.SetActive(true);
                if (Input.GetKeyDown(KeyCode.E))
                {
                    guiText.text = "Click on the option or release E";
                    mouseLook.enabled = false;
                    Cursor.lockState = CursorLockMode.None;
                    menu.GetButtonWithId("drink").unlocked = currentMug.isFilled;
                    menu.GetButtonWithId("refill").unlocked = !currentMug.isFilled;
                    menu.Open();
                }
                else if (Input.GetKeyUp(KeyCode.E))
                {
                    menu.Close();
                    Cursor.lockState = CursorLockMode.Locked;
                    mouseLook.enabled = true;
                    guiText.gameObject.SetActive(false);
                    guiText.text = "Hold E to interact";
                }
            }
            else {
                menu.Close();
                guiText.gameObject.SetActive(false);
                mouseLook.enabled = true;
                guiText.text = "Hold E to interact";
            }
        }
        else
        {
            guiText.text = "Hold E to interact";
            print("I'm looking at nothing!");
            mouseLook.enabled = true;
            menu.Close();
        }
    }

    CoffeMug currentMug;

    public void SetFillStateOfCup(bool isFilled)
    {
        currentMug.SetMugFilled(isFilled);
    }
}
