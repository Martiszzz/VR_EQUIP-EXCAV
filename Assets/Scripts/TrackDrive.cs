using UnityEngine;

public class TrackDrive : MonoBehaviour
{
    public LeverTilt leftLever, rightLever;
    public Transform excavator;
    public Vector3 forwardAxis = Vector3.forward;
    [Min(0f)] public float moveSpeed = 0.7f;          // vieno vikšro maksimalus greitis (m/s)
    [Range(0f, 0.4f)] public float deadzone = 0.12f;
    [Min(0.01f)] public float acceleration = 0.6f;
    [Min(0.01f)] public float braking = 1.2f;
    [Min(0.05f)] public float trackHalfWidth = 0.5f;  // pusė atstumo tarp vikšrų (m)
    [Min(0f)] public float turnMultiplier = 1f;       // >1 daro posūkį greitesnį
    public Throttle throttle;
    public bool blockByWorld = true;

    public float LeftSpeed { get; private set; }
    public float RightSpeed { get; private set; }

    ExcavatorBody.Probe probe;

    void Awake()
    {
        if (excavator && blockByWorld) probe = ExcavatorBody.For(excavator).CreateProbe(excavator);
    }

    float Shape(float value)
    {
        if (!Ignition.EngineRunning) return 0;
        float a = Mathf.Clamp01((Mathf.Abs(value) - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
        return Mathf.Sign(value) * a * a;
    }

    float Approach(float current, float target, float dt)
    {
        bool slowing = Mathf.Abs(target) < Mathf.Abs(current) || target * current < 0;
        return Mathf.MoveTowards(current, target, (slowing ? braking : acceleration) * dt);
    }

    void FixedUpdate()
    {
        if (!leftLever || !rightLever || !excavator) return;
        float dt = Time.fixedDeltaTime;
        float t = throttle ? throttle.Power : 1f;

        // kiekvienas vikšras seka tik savo svirtį
        LeftSpeed = Approach(LeftSpeed, Shape(leftLever.Forward) * moveSpeed * t, dt);
        RightSpeed = Approach(RightSpeed, Shape(rightLever.Forward) * moveSpeed * t, dt);

        float forward = (LeftSpeed + RightSpeed) * 0.5f;
        float yaw = (LeftSpeed - RightSpeed) / (2f * trackHalfWidth) * Mathf.Rad2Deg * turnMultiplier;

        if (Mathf.Abs(forward) > 0.0001f)
        {
            Vector3 p = excavator.position; Quaternion q = excavator.rotation;
            if (probe != null) probe.BeginMove();
            excavator.Translate(forwardAxis.normalized * forward * dt, Space.Self);
            if (!Accept(p, q)) LeftSpeed = RightSpeed = 0;
        }
        if (Mathf.Abs(yaw) > 0.0001f)
        {
            Vector3 p = excavator.position; Quaternion q = excavator.rotation;
            if (probe != null) probe.BeginMove();
            excavator.Rotate(Vector3.up, yaw * dt, Space.World);
            if (!Accept(p, q)) LeftSpeed = RightSpeed = 0;
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