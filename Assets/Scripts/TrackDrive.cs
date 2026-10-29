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
    // Seni laukai paliekami, kad nesulūžtų kiti skriptai (pvz. Editor validacijos); nebenaudojami.
    [HideInInspector] public float turnSpeed = 25f;
    [HideInInspector] public float turnAcceleration = 40f;
    public Throttle throttle;
    public bool blockByWorld = true;
    public ExcavatorGrounding grounding;

    public float LeftSpeed { get; private set; }      // faktiškai nuvažiuotas vikšro greitis
    public float RightSpeed { get; private set; }

    float leftCmd, rightCmd;                          // kiekvieno vikšro valdomas greitis
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

    float Approach(float current, float target, float dt)
    {
        bool slowing = Mathf.Abs(target) < Mathf.Abs(current) || target * current < 0;
        return Mathf.MoveTowards(current, target, (slowing ? braking : acceleration) * dt);
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
            if (!grounding.IsGrounded) { velocity = yawVelocity = leftCmd = rightCmd = 0; return; }
        }
        if (!leftLever || !rightLever) return;

        float t = (throttle ? throttle.Power : 1f) * (grounded ? grounding.DriveFactor : 1f);
        // kiekvienas vikšras seka tik savo svirtį
        leftCmd = Approach(leftCmd, Input(leftLever.Forward) * moveSpeed * t, dt);
        rightCmd = Approach(rightCmd, Input(rightLever.Forward) * moveSpeed * t, dt);
        velocity = (leftCmd + rightCmd) * 0.5f;
        yawVelocity = (leftCmd - rightCmd) / (2f * Mathf.Max(0.05f, trackHalfWidth)) * Mathf.Rad2Deg * turnMultiplier;

        float distance = 0, turned = 0;
        bool forwardBlocked = false, yawBlocked = false;
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
                else { velocity = 0; forwardBlocked = true; }
            }
            if (Mathf.Abs(yawVelocity) > 0.0001f)
            {
                Vector3 pivot = grounded ? grounding.Pivot : excavator.position;
                Quaternion turn = Quaternion.AngleAxis(yawVelocity * tick, grounded ? excavator.up : Vector3.up);
                if (Move(pivot + turn * (excavator.position - pivot), turn * excavator.rotation, tick, grounded)) turned += yawVelocity * tick;
                else { yawVelocity = 0; yawBlocked = true; }
            }
        }

        // Atsimušus: nuimame tik užblokuotą judesio dalį, kitos dalies (pvz. posūkio) nenutraukiame.
        if (forwardBlocked)
        {
            float yawPart = (leftCmd - rightCmd) * 0.5f;
            leftCmd = yawPart; rightCmd = -yawPart;
        }
        if (yawBlocked)
        {
            float forwardPart = (leftCmd + rightCmd) * 0.5f;
            leftCmd = rightCmd = forwardPart;
        }

        // Grandinė animuojama pagal faktiškai priimtą judesį.
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