using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FingerStick : LeverBase
{
    public Transform head;
    public float touchRadius = 0.03f;
    [Range(5f, 45f)] public float maxAngle = 25f;
    public float followSpeed = 100f;
    public float returnSpeed = 65f;
    [Min(0.02f)] public float responseTime = 0.16f;
    public bool invertForward, invertSide;
    public Vector3 stemAxis = Vector3.up;
    public Transform[] extraTips;
    public LeverTilt gripLever;
    public XRNode controllerHand = XRNode.RightHand;
    [Range(0, 0.4f)] public float thumbDeadzone = 0.15f;
    Quaternion restLocalRot;
    float pitch, roll, pitchVelocity, rollVelocity;
    float grabPitch, grabRoll, startPitch, startRoll, lastTouch;
    bool touching;
    bool initialized;
    XRPokeInteractor[] pokes;
    void Awake()
    {
        restLocalRot = transform.localRotation;
        initialized = true;
        pokes = FindObjectsByType<XRPokeInteractor>(FindObjectsSortMode.None);
    }
    bool FindTip(out Vector3 tip)
    {
        tip = default;
        float best = touchRadius * (touching ? 1.6f : 1f);
        bool found = false;
        foreach (var p in pokes)
        {
            if (!p || !p.isActiveAndEnabled) continue;
            var pos = p.attachTransform ? p.attachTransform.position : p.transform.position;
            float distance = Vector3.Distance(pos, head.position);
            if (distance < best) { best = distance; tip = pos; found = true; }
        }
        if (extraTips != null) foreach (var t in extraTips)
        {
            if (!t || !t.gameObject.activeInHierarchy) continue;
            float distance = Vector3.Distance(t.position, head.position);
            if (distance < best) { best = distance; tip = t.position; found = true; }
        }
        return found;
    }
    void Update()
    {
        // Gate every input path, including direct hand pokes, on the main handle grip.
        if (!head || !gripLever || !gripLever.IsHeld)
        {
            ResetInput();
            return;
        }
        var device = InputDevices.GetDeviceAtXRNode(controllerHand);
        if (gripLever && gripLever.IsHeld && device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var thumb))
        {
            StepThumbstick(thumb, true, Time.deltaTime);
            touching = false;
            return;
        }
        float targetPitch = pitch, targetRoll = roll;
        bool held = FindTip(out var tip);
        if (held)
        {
            var restFrame = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRot;
            var d = Quaternion.Inverse(restFrame) * (tip - transform.position);
            var handAngles = JoystickMotion.HandAngles(d, stemAxis);
            float handPitch = handAngles.x;
            float handRoll = handAngles.y;
            if (!touching)
            {
                grabPitch = handPitch; grabRoll = handRoll;
                startPitch = pitch; startRoll = roll;
            }
            touching = true;
            lastTouch = Time.time;
            var tilt = Vector2.ClampMagnitude(new Vector2(startPitch + Mathf.DeltaAngle(grabPitch, handPitch),
                startRoll + Mathf.DeltaAngle(grabRoll, handRoll)), maxAngle);
            targetPitch = tilt.x; targetRoll = tilt.y;
        }
        else if (Time.time - lastTouch > 0.1f)
        {
            touching = false;
            targetPitch = targetRoll = 0;
        }
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, responseTime, held ? followSpeed : returnSpeed);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, responseTime, held ? followSpeed : returnSpeed);
        var limited = Vector2.ClampMagnitude(new Vector2(pitch, roll), maxAngle);
        pitch = limited.x; roll = limited.y;
        transform.localRotation = restLocalRot * JoystickMotion.Tilt(pitch, roll, stemAxis, maxAngle);
        Forward = (invertForward ? -1 : 1) * pitch / Mathf.Max(1f, maxAngle);
        Side = (invertSide ? -1 : 1) * roll / Mathf.Max(1f, maxAngle);
    }
    public void StepThumbstick(Vector2 input, bool gripped, float dt)
    {
        if (!gripped) { ResetInput(); return; }
        float magnitude = Mathf.Clamp01(input.magnitude);
        input = magnitude <= thumbDeadzone ? Vector2.zero :
            input.normalized * Mathf.InverseLerp(thumbDeadzone, 1, magnitude);
        pitch = Mathf.SmoothDamp(pitch, input.y * maxAngle, ref pitchVelocity, responseTime, followSpeed, dt);
        roll = Mathf.SmoothDamp(roll, input.x * maxAngle, ref rollVelocity, responseTime, followSpeed, dt);
        transform.localRotation = restLocalRot * JoystickMotion.Tilt(pitch, roll, stemAxis, maxAngle);
        Forward = (invertForward ? -1 : 1) * pitch / maxAngle;
        Side = (invertSide ? -1 : 1) * roll / maxAngle;
    }
    void OnDisable() { ResetInput(); }
    void ResetInput()
    {
        touching = false;
        pitch = roll = pitchVelocity = rollVelocity = 0;
        grabPitch = grabRoll = startPitch = startRoll = lastTouch = 0;
        Forward = Side = 0;
        if (initialized) transform.localRotation = restLocalRot;
    }
}
