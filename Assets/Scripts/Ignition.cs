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
    float angle, grabAngle;
    Quaternion grabHandRotation;
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
            Quaternion handRotation = Quaternion.Inverse(restFrame) * hand.rotation;
            if (!wasHeld) grabHandRotation = handRotation;
            Step(WristTwist(grabHandRotation, handRotation, ax), true, Time.deltaTime);
        }
        else Step(0, false, Time.deltaTime);
    }

    public static float WristTwist(Quaternion from, Quaternion to, Vector3 axis)
    {
        var delta = to * Quaternion.Inverse(from);
        float projected = Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), axis.normalized);
        return Mathf.DeltaAngle(0, 2f * Mathf.Atan2(projected, delta.w) * Mathf.Rad2Deg);
    }
    public void Step(float relativeTurn, bool held, float dt)
    {
        if (held)
        {
            if (!wasHeld) grabAngle = angle;
            angle = Mathf.Clamp(grabAngle + relativeTurn, 0, startAngle);
            if (!EngineRunning && angle >= startAngle - 0.5f) EngineRunning = true;
            else if (EngineRunning && angle <= offThreshold) EngineRunning = false;
        }
        else angle = Mathf.MoveTowards(angle, EngineRunning ? runAngle : 0, returnSpeed * dt);
        wasHeld = held;
        running = EngineRunning;
        transform.localRotation = restLocalRot * Quaternion.AngleAxis(angle, axis.normalized * (invert ? -1 : 1));
    }
}
