using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class PlayerLook : NetworkBehaviour
{
    [SerializeField] private float sens;

    [SerializeField] Transform cam;
    [SerializeField] GameObject camGO;
    [SerializeField] Transform orientation;

    float mouseX;
    float mouseY;

    float multiplier = 0.01f;

    float xRotation;
    float yRotation;

    public override void OnNetworkSpawn()
    {
        if(!IsOwner)
        {
            camGO.SetActive(false);
        }else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        MyInput();
    }
    void MyInput()
    {
        if(IsOwner)
        {
            mouseX=Input.GetAxisRaw("Mouse X");
            mouseY=Input.GetAxisRaw("Mouse Y");
        }

        yRotation += mouseX * sens * multiplier;
        xRotation -= mouseY * sens * multiplier;

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cam.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        orientation.transform.rotation = Quaternion.Euler(0,yRotation,0);
    }

}
