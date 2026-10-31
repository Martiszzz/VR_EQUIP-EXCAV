using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(XRBaseInteractable))]
public class LeverTilt : LeverBase
{
    [Range(5f, 45f)] public float maxAngle = 25f;
    public bool returnToCenter = true;
    [Min(1f)] public float returnSpeed = 75f;
    [Min(0.02f)] public float responseTime = 0.14f;
    [Min(1f)] public float followSpeed = 120f;
    public bool invertForward, invertSide;
    public bool forwardOnly;
    public Vector3 stemAxis = Vector3.forward;
    [Header("Controller feedback")]
    public bool hapticsEnabled = true;
    [Range(0f, 1f)] public float hapticStrength = 0.75f;
    readonly LeverHapticFeedback haptics = new LeverHapticFeedback();
    IXRSelectInteractor hapticHand;
    XRBaseInteractable interactable;
    Quaternion restLocalRot;
    float pitch, roll, pitchVelocity, rollVelocity;
    float grabPitch, grabRoll, startPitch, startRoll;
    bool wasHeld;
    public bool IsHeld => interactable && interactable.isSelected && interactable.interactorsSelecting.Count > 0;
    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        restLocalRot = transform.localRotation;
    }
    void Update()
    {
        bool held = IsHeld;
        float targetPitch = pitch, targetRoll = roll;
        if (held)
        {
            var hand = interactable.interactorsSelecting[0].GetAttachTransform(interactable);
            var restFrame = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRot;
            var d = Quaternion.Inverse(restFrame) * (hand.position - transform.position);
            var handAngles = JoystickMotion.HandAngles(d, stemAxis);
            float handPitch = handAngles.x;
            float handRoll = handAngles.y;
            if (!wasHeld)
            {
                grabPitch = handPitch; grabRoll = handRoll;
                startPitch = pitch; startRoll = roll;
            }
            var tilt = Vector2.ClampMagnitude(new Vector2(startPitch + Mathf.DeltaAngle(grabPitch, handPitch),
                forwardOnly ? 0 : startRoll + Mathf.DeltaAngle(grabRoll, handRoll)), maxAngle);
            targetPitch = tilt.x; targetRoll = tilt.y;
        }
        else if (returnToCenter) { targetPitch = targetRoll = 0; }
        StepTilt(new Vector2(targetPitch, targetRoll), held, Time.deltaTime);
        UpdateHaptics(held, Time.deltaTime);
    }
    void UpdateHaptics(bool held, float dt)
    {
        var hand = held ? interactable.interactorsSelecting[0] : null;
        if (!ReferenceEquals(hand, hapticHand)) { haptics.Reset(); hapticHand = hand; }
        // Read actual smoothed travel, not the unclamped controller pose. Diagonal travel shares one circular stop.
        float travel = new Vector2(pitch, forwardOnly ? 0 : roll).magnitude / Mathf.Max(1f, maxAngle);
        if (!haptics.Step(travel, held && hapticsEnabled && hapticStrength > 0, dt, out var amplitude, out var duration)) return;
        amplitude *= hapticStrength;
        if (hand is XRBaseInputInteractor input) input.SendHapticImpulse(amplitude, duration);
        else if (hand is Component component)
        {
            var player = component.GetComponentInParent<HapticImpulsePlayer>();
            if (player) player.SendHapticImpulse(amplitude, duration);
        }
    }
    void OnDisable() { haptics.Reset(); hapticHand = null; }
    public void StepTilt(Vector2 target, bool held, float dt)
    {
        if (forwardOnly) target.y = 0;
        var bounded = Vector2.ClampMagnitude(target, maxAngle);
        float targetPitch = bounded.x, targetRoll = bounded.y;
        if (forwardOnly) { targetRoll = roll = rollVelocity = 0; }
        float rate = held ? followSpeed : returnSpeed;
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, responseTime, rate, dt);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, responseTime, rate, dt);
        var limited = Vector2.ClampMagnitude(new Vector2(pitch, roll), maxAngle);
        pitch = limited.x; roll = limited.y;
        wasHeld = held;
        transform.localRotation = restLocalRot * JoystickMotion.Tilt(pitch, roll, stemAxis, maxAngle);
        Forward = (invertForward ? -1 : 1) * pitch / Mathf.Max(1f, maxAngle);
        Side = forwardOnly ? 0 : (invertSide ? -1 : 1) * roll / Mathf.Max(1f, maxAngle);
    }
}
