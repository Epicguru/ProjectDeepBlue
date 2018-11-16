
using UnityEngine;
using UnityEngine.Events;

public class PlayerCameraAnimations : MonoBehaviour
{
    public PlayerMovementController Movement;
    public Animator Anim;

    private bool oldOnFloor = true;

    private void Update()
    {
        if(Movement.OnFloor == true && oldOnFloor == false)
        {
            OnHitGround(Mathf.Abs(Movement.LastFallVelocity)); // LastFallVelocity is the velocity that the character hit the ground with.          
        }

        oldOnFloor = Movement.OnFloor;
    }

    public void OnVault()
    {
        Anim.SetTrigger("Vault");
    }

    private void OnHitGround(float vel)
    {
        if(vel > 6.5f)
        {
            Anim.SetTrigger("Hit Ground");
        }
    }
}