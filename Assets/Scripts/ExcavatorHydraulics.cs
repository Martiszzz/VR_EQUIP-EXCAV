using UnityEngine;

// One pump supplies all simultaneously operated valves on this machine.
[DefaultExecutionOrder(-20)]
public class ExcavatorHydraulics : MonoBehaviour
{
    [Min(0.2f)] public float pumpCapacity = 1.5f;
    [Min(0.1f)] public float maximumPressure = 3;
    PartDriver[] actuators;
    TrackDrive tracks;
    ExcavatorGrounding grounding;
    public float Demand {get;private set;}
    public float FlowFactor {get;private set;}=1;
    void Awake(){Refresh();}
    public void Refresh(){actuators=GetComponentsInChildren<PartDriver>();tracks=GetComponentInChildren<TrackDrive>();grounding=GetComponent<ExcavatorGrounding>();}
    void FixedUpdate(){Step();}
    public void Step()
    {
        if(actuators==null)Refresh();Demand=0;
        foreach(var actuator in actuators)if(actuator && actuator.isActiveAndEnabled)Demand+=actuator.RequestedFlow;
        if(tracks && tracks.isActiveAndEnabled)Demand+=tracks.RequestedFlow*0.7f;
        FlowFactor=Demand>0?Mathf.Min(1,pumpCapacity/Demand):1;
        if(grounding && Demand>0)grounding.ReportHydraulicWork(Mathf.Max(0,Demand-pumpCapacity*0.5f)*0.003f,1);
    }
    public float ForceFactor(Transform actuator,float resistance)
    {
        float reach=grounding?Vector3.Distance(grounding.Pivot,actuator.position):1;
        float leverage=1+Mathf.Max(0,reach-1.5f)*0.22f;
        return Mathf.Clamp01(maximumPressure/(Mathf.Max(1,resistance)*leverage));
    }
}
