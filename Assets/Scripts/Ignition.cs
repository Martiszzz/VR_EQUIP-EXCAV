using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(XRBaseInteractable))]
public class Ignition : MonoBehaviour
{
    // visi valdikliai tikrina šį kintamąjį
    public static bool EngineRunning = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { EngineRunning = true; }

    public Vector3 axis = Vector3.forward;  // lokali ašis, aplink kurią sukasi raktas
    public bool invert;
    public float startAngle = 70f;          // iki šio kampo pasukus variklis užsiveda
    public float runAngle = 45f;            // kur raktas lieka dirbant varikliui
    public float offThreshold = 5f;         // pasukus atgal žemiau šio kampo variklis užgęsta
    public float returnSpeed = 200f;
    public bool startsRunning;              // testavimui

    public bool running;                    // tik matyti Inspector'e

    XRBaseInteractable interactable;
    Quaternion restLocalRot;
    float angle, grabAngle, grabHandAngle;
    bool wasHeld;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        restLocalRot = transform.localRotation;
        EngineRunning = startsRunning;
        angle = startsRunning ? runAngle : 0f;
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

            if (!wasHeld) { grabHandAngle = handAngle; grabAngle = angle; }

            angle = Mathf.Clamp(grabAngle + Mathf.DeltaAngle(grabHandAngle, handAngle), 0f, startAngle);

            if (!EngineRunning && angle >= startAngle - 0.5f) EngineRunning = true;
            if (EngineRunning && angle <= offThreshold) EngineRunning = false;
        }
        else
        {
            float target = EngineRunning ? runAngle : 0f;
            angle = Mathf.MoveTowards(angle, target, returnSpeed * Time.deltaTime);
        }

        wasHeld = held;
        running = EngineRunning;
        transform.localRotation = restLocalRot * Quaternion.AngleAxis(angle, ax);
    }
}