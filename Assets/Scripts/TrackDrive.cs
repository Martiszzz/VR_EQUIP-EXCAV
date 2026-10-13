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
    public Throttle throttle;
    float velocity, yawVelocity;
    float Input(float value)
    {
        if (!Ignition.EngineRunning) return 0;
        float a = Mathf.Clamp01((Mathf.Abs(value) - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
        return Mathf.Sign(value) * a * a;
    }
    void FixedUpdate()
    {
        if (!leftLever || !rightLever || !excavator) return;
        float t = throttle ? Mathf.Clamp01(throttle.Value) : 1f;
        float l = Input(leftLever.Forward), r = Input(rightLever.Forward);
        float desired = (l + r) * 0.5f * moveSpeed * t;
        bool slowing = Mathf.Abs(desired) < Mathf.Abs(velocity) || desired * velocity < 0;
        velocity = Mathf.MoveTowards(velocity, desired, (slowing ? braking : acceleration) * Time.fixedDeltaTime);
        yawVelocity = Mathf.MoveTowards(yawVelocity, (l - r) * 0.5f * turnSpeed * t, turnAcceleration * Time.fixedDeltaTime);
        excavator.Translate(forwardAxis.normalized * velocity * Time.fixedDeltaTime, Space.Self);
        excavator.Rotate(Vector3.up, yawVelocity * Time.fixedDeltaTime, Space.World);
    }
}
