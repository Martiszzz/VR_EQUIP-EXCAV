using UnityEngine;

public class TrackDrive : MonoBehaviour
{
    public LeverTilt leftLever, rightLever;
    public Transform excavator;
    public Vector3 forwardAxis = Vector3.forward;
    [Min(0f)] public float moveSpeed = 0.7f;
    [Min(0f)] public float turnSpeed = 25f;
    [Range(0f, 0.4f)] public float deadzone = 0.12f;
    [Min(0.01f)] public float acceleration = 0.6f;
    [Min(0.01f)] public float braking = 1.2f;
    [Min(1f)] public float turnAcceleration = 40f;
    public float LeftSpeed { get; private set; }
    public float RightSpeed { get; private set; }
    public float trackHalfWidth = 0.5f;
    public Throttle throttle;
    public bool blockByWorld = true;
    float velocity, yawVelocity;
    ExcavatorBody.Probe probe;
    void Awake()
    {
        if (excavator && blockByWorld) probe = ExcavatorBody.For(excavator).CreateProbe(excavator);
    }
    float Input(float value)
    {
        if (!Ignition.EngineRunning) return 0;
        float a = Mathf.Clamp01((Mathf.Abs(value) - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
        return Mathf.Sign(value) * a * a;
    }
    void FixedUpdate()
    {
        if (!leftLever || !rightLever || !excavator) return;
        float t = throttle ? throttle.Power : 1f;
        float l = Input(leftLever.Forward), r = Input(rightLever.Forward);
        float desired = (l + r) * 0.5f * moveSpeed * t;
        bool slowing = Mathf.Abs(desired) < Mathf.Abs(velocity) || desired * velocity < 0;
        velocity = Mathf.MoveTowards(velocity, desired, (slowing ? braking : acceleration) * Time.fixedDeltaTime);
        yawVelocity = Mathf.MoveTowards(yawVelocity, (l - r) * 0.5f * turnSpeed * t, turnAcceleration * Time.fixedDeltaTime);
        float yawLinear = yawVelocity * Mathf.Deg2Rad * trackHalfWidth;
        LeftSpeed = velocity - yawLinear;
        RightSpeed = velocity + yawLinear;

        if (Mathf.Abs(velocity) > 0.0001f)
        {
            Vector3 p = excavator.position; Quaternion q = excavator.rotation;
            if (probe != null) probe.BeginMove();
            excavator.Translate(forwardAxis.normalized * velocity * Time.fixedDeltaTime, Space.Self);
            if (!Accept(p, q)) velocity = 0;
        }
        if (Mathf.Abs(yawVelocity) > 0.0001f)
        {
            Vector3 p = excavator.position; Quaternion q = excavator.rotation;
            if (probe != null) probe.BeginMove();
            excavator.Rotate(Vector3.up, yawVelocity * Time.fixedDeltaTime, Space.World);
            if (!Accept(p, q)) yawVelocity = 0;
        }
    }

    bool Accept(Vector3 oldPos, Quaternion oldRot)
    {
        if (probe == null) return true;
        Physics.SyncTransforms();
        float after = probe.Depth(true);   // true: grindų kontaktai iš apačios ignoruojami
        if (after <= probe.Tolerance) probe.WorkSoil(Time.fixedDeltaTime);
        float groundAfter = probe.GroundDepth();
        if (after <= probe.Tolerance && groundAfter <= probe.Tolerance) return true;
        Vector3 p = excavator.position; Quaternion q = excavator.rotation;
        excavator.SetPositionAndRotation(oldPos, oldRot);
        Physics.SyncTransforms();
        bool worldAllowed = after <= probe.Tolerance || after < probe.Depth(true) - 0.0005f;
        bool groundAllowed = groundAfter <= probe.Tolerance || groundAfter < probe.GroundDepth() - 0.0001f;
        if (worldAllowed && groundAllowed) { excavator.SetPositionAndRotation(p, q); return true; }
        probe.CancelMove();
        return false;
    }
}
