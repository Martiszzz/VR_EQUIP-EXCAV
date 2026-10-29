using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(XRBaseInteractable))]
public class Throttle : MonoBehaviour
{
    public Vector3 axis = Vector3.right;   // aplink kurią lokalią ašį sukasi rankenėlė
    public float maxAngle = 60f;           // kampas ties pilnomis dujomis
    public bool invert;                    // jei didėja ne ta kryptimi

    [Range(0, 1)] public float Value;      // 0..1
    [Range(0.1f, 0.6f)] public float idlePower = 0.3f;
    public float Power => Ignition.EngineRunning ? Mathf.Lerp(idlePower, 1f, Mathf.Clamp01(Value)) : 0;

    XRBaseInteractable interactable;
    Quaternion restLocalRot;
    float angle, grabHandAngle, grabAngle;
    bool wasHeld;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        Value = Mathf.Clamp01(Value);
        angle = Value * maxAngle;
        // Keep the authored handle pose while retaining its configured starting power.
        Vector3 ax = axis.normalized * (invert ? -1f : 1f);
        restLocalRot = transform.localRotation * Quaternion.Inverse(Quaternion.AngleAxis(angle, ax));
    }

    void Update()
    {
        Vector3 ax = axis.normalized * (invert ? -1f : 1f);
        bool held = interactable.isSelected && interactable.interactorsSelecting.Count > 0;

        if (held)
        {
            Transform hand = interactable.interactorsSelecting[0].GetAttachTransform(interactable);
            Quaternion restFrame = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRot;
            Vector3 d = Quaternion.Inverse(restFrame) * (hand.position - transform.position);

            Vector3 reference = Vector3.ProjectOnPlane(Vector3.up, ax);
            if (reference.sqrMagnitude < 0.01f) reference = Vector3.ProjectOnPlane(Vector3.forward, ax);
            float handAngle = Vector3.SignedAngle(reference, Vector3.ProjectOnPlane(d, ax), ax);

            if (!wasHeld)
            {
                grabHandAngle = handAngle;
                grabAngle = angle;       // tęsiam nuo dabartinės vertės
            }

            angle = Mathf.Clamp(grabAngle + Mathf.DeltaAngle(grabHandAngle, handAngle), 0f, maxAngle);
        }

        wasHeld = held;

        transform.localRotation = restLocalRot * Quaternion.AngleAxis(angle, ax);
        Value = angle / Mathf.Max(1f, maxAngle);
    }
}
