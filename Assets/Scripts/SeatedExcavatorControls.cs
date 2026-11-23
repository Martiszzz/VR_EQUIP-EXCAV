using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

// Thumbsticks control the machine while the player remains in the cab.
[DefaultExecutionOrder(-1000)]
public class SeatedExcavatorControls : MonoBehaviour
{
    void Awake() { DisableLocomotion(); }

    IEnumerator Start()
    {
        // A newly loaded rig can enable shared actions before the previous rig disables them.
        yield return null;

        foreach (var manager in FindObjectsByType<InputActionManager>(FindObjectsSortMode.None))
            if (manager.isActiveAndEnabled) manager.EnableInput();

        foreach (var controller in FindObjectsByType<ControllerInputActionManager>(FindObjectsSortMode.None))
        {
            bool smoothMotion = controller.smoothMotionEnabled;
            bool smoothTurn = controller.smoothTurnEnabled;
            controller.smoothMotionEnabled = smoothMotion;
            controller.smoothTurnEnabled = smoothTurn;
        }
    }

    public void DisableLocomotion()
    {
        foreach (var provider in transform.root.GetComponentsInChildren<LocomotionProvider>(true))
            provider.gameObject.SetActive(false);
    }
}
