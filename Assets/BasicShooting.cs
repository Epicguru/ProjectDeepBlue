using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BasicShooting : MonoBehaviour
{
    public Animator Anim;

    public void Update()
    {
        Anim.SetBool("Aim", Input.GetKey(KeyCode.Mouse1));
        Anim.SetBool("Fire", Input.GetKey(KeyCode.Mouse0));
    }
}
