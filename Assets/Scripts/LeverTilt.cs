using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

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
    public Vector3 stemAxis = Vector3.forward;
    XRBaseInteractable interactable;
    Quaternion restLocalRot;
    float pitch, roll, pitchVelocity, rollVelocity;
    float grabPitch, grabRoll, startPitch, startRoll;
    bool wasHeld;
    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        restLocalRot = transform.localRotation;
    }
    void Update()
    {
        bool held = interactable.isSelected && interactable.interactorsSelecting.Count > 0;
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
                startRoll + Mathf.DeltaAngle(grabRoll, handRoll)), maxAngle);
            targetPitch = tilt.x; targetRoll = tilt.y;
        }
        else if (returnToCenter) { targetPitch = targetRoll = 0; }
        float rate = held ? followSpeed : returnSpeed;
        pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, responseTime, rate);
        roll = Mathf.SmoothDamp(roll, targetRoll, ref rollVelocity, responseTime, rate);
        var limited = Vector2.ClampMagnitude(new Vector2(pitch, roll), maxAngle);
        pitch = limited.x; roll = limited.y;
        wasHeld = held;
        transform.localRotation = restLocalRot * JoystickMotion.Tilt(pitch, roll, stemAxis, maxAngle);
        Forward = (invertForward ? -1 : 1) * pitch / Mathf.Max(1f, maxAngle);
        Side = (invertSide ? -1 : 1) * roll / Mathf.Max(1f, maxAngle);
    }
}
