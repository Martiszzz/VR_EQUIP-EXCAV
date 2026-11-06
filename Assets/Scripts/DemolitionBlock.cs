using UnityEngine;

/// <summary>
/// Attach to every house cube that has a Rigidbody.
/// Registers itself with DemolitionTracker on Start.
/// A block is considered "demolished" when it rotates past the
/// tiltThreshold angle OR moves more than displaceThreshold from
/// its settled starting position.
///
/// settleDelay controls how long to wait before snapshotting the
/// resting pose, so rigidbodies that drop into place at startup
/// don't immediately count as demolished.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class DemolitionBlock : MonoBehaviour
{
    [Tooltip("Angle (degrees) from the settled rotation that counts as knocked over.")]
    public float tiltThreshold = 35f;

    [Tooltip("Distance (metres) from the settled position that counts as displaced.")]
    public float displaceThreshold = 0.4f;

    [Tooltip("Seconds after Start before the resting pose is snapshot. " +
             "Give rigidbodies time to fall into place first.")]
    public float settleDelay = 1.5f;

    Rigidbody rb;
    Vector3 startPos;
    Quaternion startRot;
    bool settled;
    bool demolished;
    float settleTimer;

    public bool IsDemolished => demolished;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        settleTimer = settleDelay;
        DemolitionTracker.Register(this);
    }

    void OnDestroy()
    {
        DemolitionTracker.Unregister(this);
    }

    /// <summary>Called each tick by DemolitionTracker (avoids per-block Update cost).</summary>
    public void Tick(float dt)
    {
        if (demolished) return;

        // Wait for rigidbodies to settle before locking in the baseline pose.
        if (!settled)
        {
            settleTimer -= dt;
            if (settleTimer > 0f) return;

            // Snapshot the resting pose now.
            startPos = rb.position;
            startRot = rb.rotation;
            settled = true;
            return; // start measuring from next tick
        }

        // Check displacement
        float moved = Vector3.Distance(rb.position, startPos);
        if (moved >= displaceThreshold) { SetDemolished(); return; }

        // Check tilt
        float tilt = Quaternion.Angle(rb.rotation, startRot);
        if (tilt >= tiltThreshold) { SetDemolished(); }
    }

    void SetDemolished()
    {
        demolished = true;
        DemolitionTracker.OnBlockDemolished();
    }
}
