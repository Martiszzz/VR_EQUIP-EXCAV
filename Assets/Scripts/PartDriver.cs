using UnityEngine;

public class PartDriver : MonoBehaviour
{
    public enum Axis { Forward, Side }

    public LeverBase lever;
    public Axis input = Axis.Forward;
    public Transform target;
    public Vector3 localAxis = Vector3.right;  // aplink kurią ašį sukasi (dalies lokali)
    public float speed = 40f;                  // laipsniai per sekundę
    public bool limit = true;                  // swing sukimui išjunk
    public float minAngle = -45f, maxAngle = 45f;
    public float deadzone = 0.1f;
    public bool invert;
    public bool useWorldAxis;
    public Throttle throttle;

    float angle;

    void Update()
    {
        if (!Ignition.EngineRunning) return;
        if (!lever || !target) return;

        float v = input == Axis.Forward ? lever.Forward : lever.Side;
        float a = Mathf.Abs(v);
        if (a < deadzone) return;
        v = Mathf.Sign(v) * (a - deadzone) / (1f - deadzone);
        if (invert) v = -v;

        float t = throttle ? throttle.Value : 1f;
        float newAngle = angle + v * speed * t * Time.deltaTime;
        if (limit) newAngle = Mathf.Clamp(newAngle, minAngle, maxAngle);

        if (useWorldAxis)
            target.Rotate(Vector3.up, newAngle - angle, Space.World);
        else
            target.Rotate(localAxis.normalized, newAngle - angle, Space.Self);
        angle = newAngle;
    }
}