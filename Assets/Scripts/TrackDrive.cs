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
    public ExcavatorGrounding grounding;
    float velocity, yawVelocity;
    ExcavatorBody.Probe probe;
    void Awake()
    {
        if (!grounding && excavator) grounding = excavator.GetComponent<ExcavatorGrounding>();
        if (grounding) grounding.Initialize(excavator, forwardAxis);
        if (excavator && blockByWorld) probe = ExcavatorBody.For(excavator).CreateProbe(excavator);
    }
    float Input(float value)
    {
        if (!Ignition.EngineRunning) return 0;
        float a = Mathf.Clamp01((Mathf.Abs(value) - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
        return Mathf.Sign(value) * a * a;
    }
    void FixedUpdate() { Step(Time.fixedDeltaTime); }
    public void Step(float dt)
    {
        LeftSpeed = RightSpeed = 0;
        if (!excavator || dt <= 0) return;
        bool grounded = grounding && grounding.Ready;
        if (grounded)
        {
            Vector3 old = excavator.position; Quaternion rotation = excavator.rotation;
            if (probe != null) probe.BeginMove();
            grounding.Settle(dt);
            Accept(old, rotation, dt, false);
            grounding.RefreshState();
            if (!grounding.IsGrounded) { velocity = yawVelocity = 0; return; }
        }
        if (!leftLever || !rightLever) return;
        float t = (throttle ? throttle.Power : 1f) * (grounded ? grounding.DriveFactor : 1f);
        float l = Input(leftLever.Forward), r = Input(rightLever.Forward);
        float desired = (l + r) * 0.5f * moveSpeed * t;
        bool slowing = Mathf.Abs(desired) < Mathf.Abs(velocity) || desired * velocity < 0;
        velocity = Mathf.MoveTowards(velocity, desired, (slowing ? braking : acceleration) * dt);
        yawVelocity = Mathf.MoveTowards(yawVelocity, (l - r) * 0.5f * turnSpeed * t, turnAcceleration * dt);
        float distance = 0, turned = 0;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Mathf.Abs(velocity * dt) / 0.03f, Mathf.Abs(yawVelocity * dt))), 1, 64);
        for (int i = 0; i < steps; i++)
        {
            float tick = dt / steps;
            if (Mathf.Abs(velocity) > 0.0001f)
            {
                Vector3 direction = excavator.TransformDirection(forwardAxis.normalized);
                if (grounded) direction = Vector3.ProjectOnPlane(direction, grounding.SupportNormal).normalized;
                Vector3 before = excavator.position;
                if (Move(before + direction * velocity * tick, excavator.rotation, tick, grounded)) distance += Vector3.Dot(excavator.position - before, direction);
                else velocity = 0;
            }
            if (Mathf.Abs(yawVelocity) > 0.0001f)
            {
                Vector3 pivot = grounded ? grounding.Pivot : excavator.position;
                Quaternion turn = Quaternion.AngleAxis(yawVelocity * tick, grounded ? excavator.up : Vector3.up);
                if (Move(pivot + turn * (excavator.position - pivot), turn * excavator.rotation, tick, grounded)) turned += yawVelocity * tick;
                else yawVelocity = 0;
            }
        }
        // Drive the chain animation from accepted travel; blocked or airborne tracks report no travel.
        float yawLinear = turned / dt * Mathf.Deg2Rad * trackHalfWidth;
        LeftSpeed = distance / dt + yawLinear;
        RightSpeed = distance / dt - yawLinear;
    }
    bool Move(Vector3 position, Quaternion rotation, float dt, bool grounded)
    {
        Vector3 old = excavator.position; Quaternion before = excavator.rotation;
        if (grounded && !grounding.TryMovePose(position, rotation, dt, out position, out rotation)) return false;
        if (probe != null) probe.BeginMove();
        excavator.SetPositionAndRotation(position, rotation);
        bool accepted = Accept(old, before, dt);
        if (grounded) grounding.RefreshState();
        return accepted;
    }

    bool Accept(Vector3 oldPos, Quaternion oldRot, float dt, bool workSoil = true)
    {
        if (probe == null) return true;
        Physics.SyncTransforms();
        float after = probe.Depth(true);   // true: grindų kontaktai iš apačios ignoruojami
        if (workSoil && after <= probe.Tolerance) probe.WorkSoil(dt);
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
