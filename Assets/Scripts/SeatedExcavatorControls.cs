using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// Thumbsticks control the machine while the player remains in the cab.
[DefaultExecutionOrder(-1000)]
public class SeatedExcavatorControls : MonoBehaviour
{
    void Awake() { DisableLocomotion(); }

    public void DisableLocomotion()
    {
        foreach (var provider in transform.root.GetComponentsInChildren<LocomotionProvider>(true))
            provider.gameObject.SetActive(false);
    }
}
