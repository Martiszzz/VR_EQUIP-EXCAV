using UnityEngine;

// FK collider changes can wake the parked actor after TrackDrive has already settled it.
// Reassert support after arm/cab movement and terrain synchronization, before native gravity.
[DefaultExecutionOrder(1000)]
public class ExcavatorSupportFinalizer : MonoBehaviour
{
    public ExcavatorGrounding grounding;
    void FixedUpdate()
    {
        if(grounding && grounding.isActiveAndEnabled)grounding.FinalizeSupportedRest();
    }
}
