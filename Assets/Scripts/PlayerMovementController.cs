using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    public CharacterController Controller;
    public PlayerVaulting Vaulting;
    public float GravityScale = 1f;
    public bool OnFloor;
    [Tooltip("If true, the input from the keyboard is passed directly to the movement calculation, without filtering. (acceleration, deccelration)")]
    public bool UseRawInput = false;
    public float Speed = 10f;
    public float JumpVelocity = 10f;
    [Tooltip("The acceleration rate when applying input. " +
             "A value of 1 means 1*speed per second, so it takes one second to accelerate to full speed. " +
             "A value of 2 means 2*speed, so it takes half a second to accelerate to full speed.")]
    public float InputAcceleration = 2f;
    public float InputDecceleration = 2f;
    public KeyCode JumpKey = KeyCode.Space;
    public float DowncastRate = 5f;
    public int MaxDowncastsPerFrame = 10;
    public float DowncastForce = 1f;
    public int DowncastsDone;
    public bool DebugMode = true;
    public Vector3 Velocity
    {
        get;
        private set;
    }
    public float LastFallVelocity { get; private set; }

    private Vector3 gravityAccumulator;
    private Vector3 inputAccumulator;
    private Vector3[][] rings;
    private int downcastPointCount;
    private int downcastRingCount;
    private float downcastTimer;
    private int pendingDowncasts;
    private List<int> casted;
    private List<Vector3> downcastForces = new List<Vector3>();

    private void Start()
    {        
        CreateRings(3, Controller.radius, (x) => { return (x + 1) * 4; });
        Debug.Log("Created " + downcastPointCount + " downcast points for the character controller.");
    }

    private void Update()
    {
        if (Vaulting.IsVaulting)
        {
            Controller.enabled = false;
            return;
        }
        else
        {
            Controller.enabled = true;
        }

        UpdateOnFloor();
        UpdateDowncasting();

        // Downcast forces (that prevent a character from 'clinging' to an edge with their collider, essentially makes edges slippery)
        Vector3 downcastForce = Vector3.zero;
        if(!OnFloor && downcastForces.Count > 0)
        {
            for (int i = 0; i < downcastForces.Count; i++)
            {
                downcastForce += downcastForces[i];
            }
            downcastForce /= downcastForces.Count;
            downcastForce *= this.DowncastForce;

            // Also reset gravity, because we are hanging onto an edge.
            gravityAccumulator.y = 0f;
        }

        // Gravity.
        Vector3 gravityForce = Physics.gravity * GravityScale;
        gravityAccumulator += gravityForce * Time.deltaTime;

        if (!Vaulting.CanVault && !Vaulting.IsVaulting && Input.GetKeyDown(KeyCode.Space))
        {
            if (OnFloor)
            {
                gravityAccumulator.y = JumpVelocity;
            }
        }

        // Raw input.
        Vector3 rawInput = new Vector3();
        rawInput.x = Input.GetAxisRaw("Horizontal");
        rawInput.z = Input.GetAxisRaw("Vertical");        

        // Accelerated input.
        if (!UseRawInput)
        {
            inputAccumulator.x = CalculateAccumulatedInput(rawInput.x, inputAccumulator.x);
            inputAccumulator.z = CalculateAccumulatedInput(rawInput.z, inputAccumulator.z);
        }
        else
        {
            inputAccumulator.x = rawInput.x;
            inputAccumulator.z = rawInput.z;
        }

        // Translated input. (put the raw input into the local axis)
        Vector3 worldInput = transform.TransformDirection(inputAccumulator);
        if (worldInput.magnitude > 1f)
            worldInput.Normalize();

        Vector3 finalVelocity = downcastForce + gravityAccumulator + (worldInput * Speed);
        Velocity = finalVelocity;
        bool oldOnFloor = OnFloor;

        Controller.Move(finalVelocity * Time.deltaTime);
        UpdateOnFloor();
        if (OnFloor)
        {
            gravityAccumulator.y = 0f;
            if (!oldOnFloor)
            {
                // We just hit the floor!
                LastFallVelocity = Velocity.y;
            }
        }
    }

    private delegate int RingFunction(int ringNumber);
    private void CreateRings(int rings, float radius, RingFunction function)
    {
        this.rings = new Vector3[rings][];
        downcastPointCount = 0;
        downcastRingCount = rings;

        for (int i = 0; i < rings; i++)
        {
            int count = function.Invoke(i);
            this.rings[i] = new Vector3[count];
            float distance = radius / rings * (i + 1);
            float y = distance / radius * 0.5f;
            for (int j = 0; j < count; j++)
            {
                // Work out the position for this point in the ring.
                float angle = ((2 * Mathf.PI) / count) * j + i; // In radians, hence the 2PI thing.
                float x = Mathf.Cos(angle) * distance;
                float z = Mathf.Sin(angle) * distance;
                this.rings[i][j] = new Vector3(x, y, z);
                downcastPointCount++;
            }
        }
    }

    private void UpdateDowncasting()
    {
        // Where we do many raycasts downwards to determine if the bottom of the character capsule is caught on any edge.
        // Is only applied when the center of the collider is not over a solid surface (when the character is considered falling).

        downcastTimer += Time.unscaledDeltaTime;
        float interval = 1f / DowncastRate;
        if(downcastTimer >= interval)
        {
            downcastTimer = 0;
            if (OnFloor)
                return;

            if(pendingDowncasts == 0)
            {
                pendingDowncasts = downcastPointCount;
                downcastForces.Clear();
            }
        }

        if (casted != null)
            casted.Clear();

        if (OnFloor)
        {
            pendingDowncasts = 0;
            downcastForces.Clear();
        }

        if(pendingDowncasts > 0)
        {
            if (DebugMode && casted == null)
                casted = new List<int>();

            int toDo = Mathf.Min(pendingDowncasts, MaxDowncastsPerFrame);
            DowncastsDone += Downcast(downcastPointCount - pendingDowncasts, toDo);
            pendingDowncasts -= toDo;       
        }
    }

    private int Downcast(int done, int count)
    {
        int found = 0;
        int executed = 0;
        Vector3 pos = transform.position;
        const float VERTICAL_TARGET = 0.15f; // Just below the 'grounded' mark.

        for (int i = 0; i < rings.Length && executed < count; i++)
        {
            var r = rings[i];
            for (int j = 0; j < r.Length && executed < count; j++)
            {
                if(found >= done)
                {
                    Vector3 offset = r[j];
                    bool hit = Physics.Raycast(pos + offset, Vector3.down, VERTICAL_TARGET + offset.y);
                    if (hit)
                    {
                        ApplyForceFromDowncast(offset);
                    }
                    executed++;
                    if (DebugMode)
                    {
                        casted.Add(downcastRingCount * i + j * 31 + 6 - 2 * i); // It's really late and I can't think of a way to make a unique ID.
                    }
                }
                found++;
            }
        }

        return executed;
    }

    private void ApplyForceFromDowncast(Vector3 offset)
    {
        Vector3 resultant = -new Vector3(offset.x, 0f, offset.z);
        downcastForces.Add(resultant.normalized);
    }

    private void UpdateOnFloor()
    {
        OnFloor = Physics.Raycast(transform.position, Vector3.down, 0.1f);
    }

    private float CalculateAccumulatedInput(float input, float accumulated)
    {
        if (input != 0)
        {
            if (accumulated < input)
            {
                return accumulated + Mathf.Min(InputAcceleration * Time.deltaTime, Mathf.Abs(input - accumulated));
            }
            else if(accumulated > input)
            {
                return accumulated - Mathf.Min(InputAcceleration * Time.deltaTime, Mathf.Abs(input - accumulated));
            }
            else
            {
                return input;
            }
        }
        else
        {
            if (accumulated > 0f)
            {
                return accumulated - Mathf.Min(InputDecceleration * Time.deltaTime, accumulated);
            }
            else if (accumulated < 0f)
            {
                return accumulated + Mathf.Min(InputDecceleration * Time.deltaTime, -accumulated);
            }
            else
            {
                return accumulated;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (rings == null)
            return;

        Color neutral = Color.cyan;
        Color casted = Color.red;

        Vector3 pos = transform.position;
        const float TARGET_VERTICAL = 0.15f;
        for (int i = 0; i < rings.Length; i++)
        {
            var r = rings[i];
            for (int j = 0; j < r.Length; j++)
            {
                Color c = neutral;
                if (DebugMode && this.casted != null && this.casted.Contains(downcastRingCount * i + j * 31 + 6 - 2 * i))
                    c = casted;

                Vector3 offset = r[j];
                Vector3 start = pos + offset;
                Gizmos.color = c;
                Gizmos.DrawLine(start, start + Vector3.down * (TARGET_VERTICAL + offset.y));
            }
        }
    }
}
