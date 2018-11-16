using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMouseLook : MonoBehaviour
{
    public float Sensitivity = 1f;
    public Transform Camera;
    public Transform Base;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {       
        float x = Input.GetAxisRaw("Mouse X") * Sensitivity;
        float y = -Input.GetAxisRaw("Mouse Y") * Sensitivity;
        Base.transform.Rotate(0f, x, 0f);
        Camera.transform.Rotate(y, 0f, 0f);
    }
}
