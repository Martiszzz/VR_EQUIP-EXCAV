using UnityEngine;

public class PartDriver : MonoBehaviour
{
    public enum Axis { Forward, Side }
    public LeverBase lever;
    public Axis input = Axis.Forward;
    public Transform target;
    public Vector3 localAxis = Vector3.right;
    [Min(1f)] public float speed = 22f;
    public bool limit = true;
    public float minAngle = -45f, maxAngle = 45f;
    [Range(0f, 0.4f)] public float deadzone = 0.12f;
    [Min(1f)] public float acceleration = 45f;
    [Min(1f)] public float braking = 80f;
    [Range(1f, 3f)] public float responseCurve = 1.6f;
    public bool invert, useWorldAxis;
    public Throttle throttle;
    public bool blockByWorld = true;
    public bool blockByTerrain;
    float angle, angularVelocity;
    Quaternion restRotation;
    ExcavatorBody.Probe probe;
    ExcavatorHydraulics circuit;
    float cuttingResistance=1;
    float acceptedFraction=1;
    public float RequestedFlow => !Ignition.EngineRunning || !lever ? 0 : Mathf.Clamp01((Mathf.Abs(input==Axis.Forward?lever.Forward:lever.Side)-deadzone)/Mathf.Max(0.01f,1-deadzone));
    public float Angle => angle;
    public float AngularVelocity => angularVelocity;
    public float HydraulicLoad { get; private set; }
    void Awake() { Initialize(); }
    public void Initialize()
    {
        if (target)
        {
            restRotation = target.localRotation;
            if (blockByTerrain && !target.GetComponent<GroundedAttachment>()) target.gameObject.AddComponent<GroundedAttachment>();
            if (blockByWorld || blockByTerrain) probe = ExcavatorBody.For(target).CreateProbe(target);
        }
        circuit=target?target.GetComponentInParent<ExcavatorHydraulics>():null;
        angle = angularVelocity = 0;
    }
    void FixedUpdate()
    {
        Step(Time.fixedDeltaTime);
        if(lever is LeverTilt grip)grip.ReportHydraulicLoad(HydraulicLoad);
        else if(lever is FingerStick thumb && thumb.gripLever)thumb.gripLever.ReportHydraulicLoad(HydraulicLoad);
    }
    public void Step(float deltaTime)
    {
        if (!lever || !target || deltaTime <= 0) return;
        float v = Ignition.EngineRunning ? (input == Axis.Forward ? lever.Forward : lever.Side) : 0;
        float a = Mathf.Clamp01((Mathf.Abs(v) - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
        v = Mathf.Sign(v) * Mathf.Pow(a, responseCurve) * (invert ? -1 : 1);
        HydraulicLoad = Mathf.MoveTowards(HydraulicLoad,0,deltaTime*1.5f);
        float desired = v * speed * (throttle ? throttle.Power : 1f) * Mathf.Lerp(1,0.55f,HydraulicLoad) * (circuit ? circuit.FlowFactor*circuit.ForceFactor(target,cuttingResistance) : 1);
        float rate = Mathf.Abs(desired) < Mathf.Abs(angularVelocity) || desired * angularVelocity < 0 ? braking : acceleration;
        rate*=circuit?circuit.InertiaFactor:1;
        if (limit)
        {
            float remaining = desired >= 0 ? maxAngle - angle : angle - minAngle;
            desired = Mathf.Clamp(desired, -Mathf.Sqrt(2f * braking * Mathf.Max(0, remaining)),
                Mathf.Sqrt(2f * braking * Mathf.Max(0, remaining)));
        }
        angularVelocity = Mathf.MoveTowards(angularVelocity, desired, rate * deltaTime);
        float next = angle + angularVelocity * deltaTime;
        if (limit)
        {
            next = Mathf.Clamp(next, minAngle, maxAngle);
            if ((next >= maxAngle && angularVelocity > 0) || (next <= minAngle && angularVelocity < 0)) angularVelocity = 0;
        }
        float from = angle;
        int steps = probe!=null?probe.MotionSteps(next-from):Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(next-from)/.25f),1,64);
        for (int i = 1; i <= steps; i++)
        {
            float candidate = Mathf.Lerp(from, next, i / (float)steps);
            Quaternion old = target.localRotation;
            if (probe != null) probe.BeginMove();
            if (useWorldAxis) target.Rotate(target.parent ? target.parent.TransformDirection(localAxis.normalized) : localAxis.normalized, candidate - angle, Space.World);
            else target.localRotation = restRotation * Quaternion.AngleAxis(candidate, localAxis.normalized);
            acceptedFraction=1;
            if (probe != null && Mathf.Abs(candidate - angle) > 0.0001f && !Accept(old, deltaTime / steps))
            {
                angularVelocity *= .5f;
                return;
            }
            if(acceptedFraction<.999f)
            {angle=Mathf.Lerp(angle,candidate,acceptedFraction);angularVelocity*=.35f;return;}
            angle = candidate;
        }
        angle = limit ? next : Mathf.Repeat(next + 180f, 360f) - 180f;
    }

    bool Accept(Quaternion old, float dt)
    {
        acceptedFraction=1;
        probe.AlignLinkages();
        Physics.SyncTransforms();
        float after = probe.Depth(false);
        probe.ResolveDynamicContacts(dt);
        // Dig along the actual tooth sweep before testing the remaining terrain contact.
        // Do not excavate through a solid obstacle such as the cab or a wall.
        if (after <= probe.Tolerance) HydraulicLoad=Mathf.Max(HydraulicLoad,Mathf.Clamp01(probe.WorkSoil(dt)/Mathf.Max(dt,0.001f)/0.012f));
        cuttingResistance=probe.CuttingResistance;
        float groundAfter = probe.GroundDepth();
        if (after <= probe.Tolerance && groundAfter <= probe.Tolerance) return true;
        if(after<=probe.Tolerance && probe.ReactToGround(dt,RequestedFlow*(throttle?throttle.Power:1))) {HydraulicLoad=1;return true;}
        if(groundAfter>probe.Tolerance)HydraulicLoad=1;
        Quaternion moved = target.localRotation;
        target.localRotation = old;
        probe.AlignLinkages();
        Physics.SyncTransforms();
        // jei judesys mažina persidengimą (dalis išlenda), leidžiam
        bool worldAllowed = after <= probe.Tolerance || after < probe.Depth(false) - 0.0005f;
        float beforeGround=probe.GroundDepth();
        bool groundAllowed = groundAfter <= probe.Tolerance || groundAfter < beforeGround - 0.000001f ||
            (beforeGround<=.12f && groundAfter<=beforeGround+.00005f);
        if (worldAllowed && groundAllowed) { target.localRotation = moved;probe.AlignLinkages();return true; }
        // An already obstructed starting pose has no admissible interval to bisect. Repeating seven
        // complete jaw/terrain queries every tick caused severe stalls while holding a tool down.
        if(beforeGround>probe.Tolerance && !groundAllowed)
        {probe.CancelMove();return false;}
        // Keep the admissible part of a stroke instead of alternating full steps and full rollbacks at contact.
        float low=0,high=1;
        for(int i=0;i<7;i++)
        {
            float middle=(low+high)*.5f;target.localRotation=Quaternion.Slerp(old,moved,middle);probe.AlignLinkages();Physics.SyncTransforms();
            if(probe.Depth(false)<=probe.Tolerance && probe.GroundDepth()<=probe.Tolerance)low=middle;else high=middle;
        }
        target.localRotation=Quaternion.Slerp(old,moved,low);probe.AlignLinkages();Physics.SyncTransforms();
        if(low>.005f){acceptedFraction=low;return true;}
        target.localRotation=old;
        probe.AlignLinkages();Physics.SyncTransforms();
        probe.CancelMove();
        return false;
    }
}
