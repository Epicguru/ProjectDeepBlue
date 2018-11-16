
using UnityEngine;
using UnityEngine.Events;

public class PlayerVaulting : MonoBehaviour
{
    public int RaycastCount = 5;
    public float PlayerHeight = 2f;
    public float PlayerRadius = 0.5f;
    public float MaxVaultStartDistance = 1f;
    public float MaxVaultTraversalDistance = 1.6f;
    public float MaxVaultHeight = 1.2f;
    public float MinVaultHeight = 0.8f;
    public bool CanVault;
    public float VaultTime = 0.6f;
    public AnimationCurve PositionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public bool IsVaulting;
    public LayerMask Mask;
    public UnityEvent UponVault;

    private bool[] BlockedStates;
    private float[] BlockedHeights;
    private float timer;
    private Vector3 startPos;
    private Vector3 endPos;

    private void Update()
    {
        if(BlockedStates == null || BlockedStates.Length != RaycastCount)
        {
            BlockedStates = new bool[RaycastCount];
            BlockedHeights = new float[RaycastCount];
        }

        for (int i = 0; i < RaycastCount; i++)
        {
            float height = i * (PlayerHeight / RaycastCount);
            bool hit = Physics.Raycast(transform.position + transform.forward * PlayerRadius + transform.up * height, transform.forward, MaxVaultStartDistance);
            BlockedStates[i] = hit;
            BlockedHeights[i] = height;
        }

        UpdateCanVault();
        UpdateVaulting();
    }

    private void UpdateVaulting()
    {
        if(Input.GetKeyDown(KeyCode.Space) && CanVault)
        {
            IsVaulting = true;
            timer = 0f;
            startPos = transform.position;
            bool worked = GetVaultEndPoint(out endPos);
            if (!worked)
                return;
            if(UponVault != null)
            {
                UponVault.Invoke();
            }
        }

        if (IsVaulting)
        {
            timer += Time.deltaTime;
            float p = Mathf.Clamp01(timer / VaultTime);
            float x = PositionCurve.Evaluate(p);

            transform.position = Vector3.Lerp(startPos, endPos, x);
            if(x == 1f)            
                IsVaulting = false;
            
        }
    }

    private bool GetVaultEndPoint(out Vector3 pos)
    {
        // UNDONE: Do a spherecast to make sure we can land there.
        float dst = GetVaultEndDistance();
        if (dst == -1f)
        {
            pos = Vector3.zero;
            return false;
        }

        dst += PlayerRadius * 0.9f;

        pos = transform.position + transform.forward * dst;
        return true;
    }

    private float GetVaultEndDistance()
    {
        // Get the distance of the vault obstacle. Just uses raycasts.
        float start = PlayerRadius;
        const int RAY_COUNT = 20;
        bool hasHit = false;
        for (int i = 0; i < RAY_COUNT; i++)
        {
            float distance = ((MaxVaultTraversalDistance + MaxVaultStartDistance) / RAY_COUNT) * i + start;
            Vector3 rayStart = (transform.position + transform.forward * distance) + (transform.up * (MaxVaultHeight + 0.01f));
            Vector3 dir = Vector3.down;
            float downDir = MaxVaultHeight - 0.05f;
            bool hit = Physics.Raycast(rayStart, dir, downDir);
            if (!hit)
            {
                if (hasHit)
                {
                    return distance;
                }
            }
            else
            {
                hasHit = true;
            }
        }

        return -1f;
    }

    private float GetVaultHeight()
    {
        // Gets the height of the current object we are vaulting over.
        float totalDistance = MaxVaultTraversalDistance + MaxVaultStartDistance;
        int count = 20;
        float acc = 0f;
        float change = totalDistance / count;
        int hits = 0;
        for (int i = 0; i < count; i++)
        {
            float dst = (i + 1) * change;
            Vector3 rayStart = transform.position + transform.forward * dst;
            rayStart += transform.up * (MaxVaultHeight + 0.08f);
            RaycastHit hitInfo;
            bool hit = Physics.Raycast(rayStart, Vector3.down, out hitInfo, MaxVaultHeight - 0.05f);
            if (hit)
            {
                acc += (transform.position.y + MaxVaultHeight) - hitInfo.point.y;
                hits++;
            }
        }
        return acc / hits;
    }

    private void UpdateCanVault()
    {
        CanVault = false;
        for (int i = 0; i < RaycastCount; i++)
        {
            bool hit = BlockedStates[i];
            float height = BlockedHeights[i];

            if (hit)
            {
                if(height > MaxVaultHeight)
                {
                    CanVault = false;
                    break;
                }
                else if(height > MinVaultHeight)
                {
                    CanVault = true;
                }
            }
        }

        if (CanVault)
        {
            float dst = GetVaultEndDistance();
            if(dst == -1f)
            {
                CanVault = false;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (BlockedStates == null || BlockedStates.Length != RaycastCount)
            return;

        Color neutral = Color.cyan;
        Color collided = Color.red;

        for (int i = 0; i < RaycastCount; i++)
        {
            bool hit = BlockedStates[i];
            float h = BlockedHeights[i];

            Gizmos.color = hit ? collided : neutral;
            Gizmos.DrawLine(transform.position + transform.forward * PlayerRadius + transform.up * h, transform.up * h + (transform.position + transform.forward * PlayerRadius) + transform.forward * MaxVaultStartDistance);
        }
    }
}
