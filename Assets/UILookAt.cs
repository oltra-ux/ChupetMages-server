using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UILookAt : MonoBehaviour
{
    Camera myCamera;
    void Start()
    {
        Camera myCamera = Camera.main;
    }
    // Start is called before the first frame update
    void Update()
    {
        transform.LookAt(Camera.allCameras[1].transform.position);
    }
    void DestroyUi()
    {
        Destroy(gameObject);
    }
}
