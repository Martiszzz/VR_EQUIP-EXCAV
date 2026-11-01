using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ExcavatorRadioControl : MonoBehaviour
{
    public enum Action { Power, Previous, Next, Volume, Tune }
    ExcavatorRadio radio;
    Action action;
    XRSimpleInteractable interactable;
    Quaternion restRotation, previousHand;
    bool held;
    float tuningTravel;

    public void Initialize(ExcavatorRadio owner, Action controlAction, XRSimpleInteractable target)
    {
        radio = owner;
        action = controlAction;
        interactable = target;
        restRotation = transform.localRotation;
        interactable.selectEntered.AddListener(OnSelected);
    }

    void OnSelected(SelectEnterEventArgs args)
    {
        if (action == Action.Power) radio.TogglePower();
        else if (action == Action.Previous) radio.ChangeStation(-1);
        else if (action == Action.Next) radio.ChangeStation(1);
    }

    void Update()
    {
        if (action != Action.Volume && action != Action.Tune) return;
        if (!interactable.isSelected || interactable.interactorsSelecting.Count == 0)
        {
            held = false;
            tuningTravel = 0;
            return;
        }
        var hand = interactable.interactorsSelecting[0].GetAttachTransform(interactable);
        // Use the fixed panel frame, not the rotating dial, and only read while held.
        var rotation = Quaternion.Inverse(radio.transform.rotation) * hand.rotation;
        if (held)
        {
            float delta = -Ignition.WristTwist(previousHand, rotation, Vector3.forward);
            if (action == Action.Volume) radio.AdjustVolume(delta);
            else
            {
                tuningTravel += delta;
                int steps = (int)(tuningTravel / 30f);
                if (steps != 0) { radio.ChangeStation(steps); tuningTravel -= steps * 30f; }
            }
        }
        previousHand = rotation;
        held = true;
    }

    public void SetAngle(float angle)
    {
        transform.localRotation = Quaternion.AngleAxis(-angle, Vector3.forward) * restRotation;
    }

    void OnDisable() { held = false; tuningTravel = 0; }
    void OnDestroy() { if (interactable) interactable.selectEntered.RemoveListener(OnSelected); }
}
