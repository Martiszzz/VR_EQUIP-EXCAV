using UnityEngine;

// Mass positions follow the articulated model. Gravity projects this center into the support polygon.
[DefaultExecutionOrder(-40)]
public class ExcavatorMass : MonoBehaviour
{
    public ExcavatorGrounding grounding;
    public Transform boomPivot, stickPivot, toolPivot;
    public BucketInside bucket;
    public Transform blade;
    [Min(100)] public float baseMass=1700;
    [Min(1)] public float boomMass=170,stickMass=100,toolMass=70;
    public Vector3 baseCenterOfMassMetres=new Vector3(0,0.55f,0);
    public Vector3 CenterOfMass {get;private set;}
    public float TotalMass {get;private set;}
    public float Stability {get;private set;}=1;
    public bool BladeSupported {get;private set;}
    public Vector3 BladePoint {get;private set;}
    GroundedAttachment bladeContact;
    void Awake(){Step();}
    void FixedUpdate(){Step();}
    public void Step()
    {
        if(!grounding)grounding=GetComponent<ExcavatorGrounding>();
        Vector3 center=grounding && grounding.Ready?grounding.Pivot:transform.position;
        Vector3 baseCenter=center+transform.rotation*baseCenterOfMassMetres;
        TotalMass=baseMass;Vector3 moment=baseCenter*baseMass;
        if(boomPivot && stickPivot)Add((boomPivot.position+stickPivot.position)*0.5f,boomMass,ref moment);
        if(stickPivot && toolPivot)Add((stickPivot.position+toolPivot.position)*0.5f,stickMass,ref moment);
        if(toolPivot)Add(toolPivot.position,toolMass,ref moment);
        if(bucket)Add(bucket.SoilCenterOfMass,bucket.HeldMass,ref moment);
        CenterOfMass=moment/Mathf.Max(1,TotalMass);
        BladeSupported=false;
        if(blade && grounding && grounding.terrain)
        {
            if(!bladeContact)bladeContact=blade.GetComponent<GroundedAttachment>();
            if(bladeContact)
            {
                var b=bladeContact.WorkBounds;float min=float.PositiveInfinity;Vector3 point=Vector3.zero;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {var p=blade.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z)));if(p.y<min){min=p.y;point=p;}}
                // Use the blade's middle at its lowest edge, avoiding a lateral bias.
                BladePoint=new Vector3(blade.TransformPoint(b.center).x,point.y,blade.TransformPoint(b.center).z);
                var local=BladePoint-grounding.terrain.transform.position;var size=grounding.terrain.terrainData.size;
                if(local.x>=0 && local.z>=0 && local.x<=size.x && local.z<=size.z)
                    BladeSupported=Mathf.Abs(BladePoint.y-grounding.terrain.SampleHeight(BladePoint)-grounding.terrain.transform.position.y)<0.035f;
            }
        }
        if(grounding && grounding.Ready)
        {
            float minX=float.PositiveInfinity,maxX=float.NegativeInfinity,minZ=minX,maxZ=maxX;
            for(int i=0;i<12;i++){var p=transform.InverseTransformPoint(grounding.SupportPoint(i)-CenterOfMass+transform.position);minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);}
            if(BladeSupported){var p=transform.InverseTransformPoint(BladePoint-CenterOfMass+transform.position);minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z);}
            float margin=Mathf.Min(-minX,maxX,-minZ,maxZ)*Mathf.Min(transform.lossyScale.x,transform.lossyScale.z);
            Stability=Mathf.Clamp01(margin/0.25f);
        }
        else Stability=1;
    }
    public float PressureAt(Vector3 point)
    {
        if(!grounding || !grounding.Ready)return 1;
        var local=transform.InverseTransformVector(CenterOfMass-grounding.Pivot);
        var sample=transform.InverseTransformVector(point-grounding.Pivot);
        float x=Mathf.Max(0.01f,Mathf.Abs(transform.InverseTransformVector(grounding.SupportPoint(6)-grounding.SupportPoint(0)).x));
        float z=Mathf.Max(0.01f,Mathf.Abs(transform.InverseTransformVector(grounding.SupportPoint(2)-grounding.SupportPoint(0)).z));
        return Mathf.Clamp(TotalMass/baseMass*(1+local.x*sample.x/(x*x)*4+local.z*sample.z/(z*z)*4),0.2f,2.5f);
    }
    void Add(Vector3 point,float mass,ref Vector3 moment){TotalMass+=mass;moment+=point*mass;}
}
