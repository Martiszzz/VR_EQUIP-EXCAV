using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class ExcavatorRadioControl : MonoBehaviour
{
    public enum Action { Power, Previous, Next, Volume, Tune, Preset1, Preset2, Preset3, VolumeDown, VolumeUp }
    ExcavatorRadio radio;
    Action action;
    XRSimpleInteractable interactable;
    Quaternion previousHand;
    bool held;
    float tuningTravel;

    public void Initialize(ExcavatorRadio owner, Action controlAction, XRSimpleInteractable target)
    {
        radio = owner;
        action = controlAction;
        interactable = target;
        interactable.selectEntered.AddListener(OnSelected);
    }

    void OnSelected(SelectEnterEventArgs args)
    {
        if (action == Action.Power) radio.TogglePower();
        else if (action == Action.Previous) radio.ChangeStation(-1);
        else if (action == Action.Next) radio.ChangeStation(1);
        else if (action == Action.Preset1) radio.SelectStation(0);
        else if (action == Action.Preset2) radio.SelectStation(1);
        else if (action == Action.Preset3) radio.SelectStation(2);
        else if (action == Action.VolumeDown) radio.AdjustVolume(-27);
        else if (action == Action.VolumeUp) radio.AdjustVolume(27);
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

    void OnDisable() { held = false; tuningTravel = 0; }
    void OnDestroy() { if (interactable) interactable.selectEntered.RemoveListener(OnSelected); }
}
