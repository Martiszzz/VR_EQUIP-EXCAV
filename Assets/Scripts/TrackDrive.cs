using UnityEngine;

public class TrackDrive : MonoBehaviour
{
    public LeverTilt leftLever, rightLever;   // Control_Track_L, Control_Track_R
    public Transform excavator;               // Excav
    public Vector3 forwardAxis = Vector3.forward;
    public float moveSpeed = 1f;
    public float turnSpeed = 40f;
    public float deadzone = 0.1f;
    public Throttle throttle;

    void Update()
    {
        if (!Ignition.EngineRunning) return;
        float t = throttle ? throttle.Value : 1f;
        float l = Mathf.Abs(leftLever.Forward) < deadzone ? 0 : leftLever.Forward;
        float r = Mathf.Abs(rightLever.Forward) < deadzone ? 0 : rightLever.Forward;

        excavator.Translate(forwardAxis * ((l + r) * 0.5f) * moveSpeed * t * Time.deltaTime, Space.Self);
        excavator.Rotate(Vector3.up, (l - r) * 0.5f * turnSpeed * t * Time.deltaTime, Space.World);
    }
}