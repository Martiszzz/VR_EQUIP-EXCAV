using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(XRBaseInteractable))]
public class LeverTilt : LeverBase
{
    public float maxAngle = 30f;
    public bool returnToCenter = true;
    public float returnSpeed = 200f;
    public bool invertForward, invertSide;    
    XRBaseInteractable interactable;
    Quaternion restLocalRot;
    float pitch, roll;
    float grabPitch, grabRoll;
    bool wasHeld;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        restLocalRot = transform.localRotation;
    }

    void Update()
    {
        bool held = interactable.isSelected && interactable.interactorsSelecting.Count > 0;

        if (held)
        {
            var interactor = interactable.interactorsSelecting[0];
            Transform hand = interactor.GetAttachTransform(interactable);
            Quaternion restFrame = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRot;
            Vector3 d = Quaternion.Inverse(restFrame) * (hand.position - transform.position);

            float handPitch = Mathf.Atan2(d.z, d.y) * Mathf.Rad2Deg;
            float handRoll = Mathf.Atan2(-d.x, d.y) * Mathf.Rad2Deg;

            if (!wasHeld)
            {
                // paėmimo momentu ranka = centras
                grabPitch = handPitch;
                grabRoll = handRoll;
            }

            pitch = Mathf.Clamp(handPitch - grabPitch, -maxAngle, maxAngle);
            roll = Mathf.Clamp(handRoll - grabRoll, -maxAngle, maxAngle);
        }
        else if (returnToCenter)
        {
            pitch = Mathf.MoveTowards(pitch, 0, returnSpeed * Time.deltaTime);
            roll = Mathf.MoveTowards(roll, 0, returnSpeed * Time.deltaTime);
        }

        wasHeld = held;

        transform.localRotation = restLocalRot * Quaternion.Euler(pitch, 0, roll);

        Forward = (invertForward ? -1 : 1) * pitch / maxAngle;
        Side = (invertSide ? -1 : 1) * roll / maxAngle;
    }
}