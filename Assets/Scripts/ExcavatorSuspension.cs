using UnityEngine;

public partial class ExcavatorGrounding
{
    [Header("Physical track suspension")]
    public bool physicalSuspension=true;
    [Min(.005f)] public float suspensionCompression=.018f;
    [Range(.3f,2)] public float suspensionDamping=1;
    [Min(.01f)] public float contactReach=.06f;
    [Min(.1f)] public float supportForceMargin=1.2f;
    [Min(.01f)] public float toolCompliance=.025f;
    [Min(.04f)] public float supportForceSmoothing=.15f;
    [Range(0,.2f)] public float liftForceSurplus=.06f;
    [Min(.001f)] public float terrainContactSkin=.005f;
    float smoothedToolForce,toolForceVelocity;
    public int GroundedTrackPoints {get;private set;}
    public Rigidbody PhysicalBody {get;private set;}
    public bool UsesPhysicalSupport => physicalSuspension && PhysicalBody && !PhysicalBody.isKinematic;
    GroundedAttachment[] physicalTools;
    BucketInside physicalBucket;
    Vector3 requestedPoint;
    float requestedEffort,requestRemaining;

    public void EnablePhysicalSupport(Rigidbody body)
    {
        PhysicalBody=body;
        physicalTools=GetComponentsInChildren<GroundedAttachment>(true);
        physicalBucket=GetComponentInChildren<BucketInside>(true);
        body.isKinematic=false;body.useGravity=true;
        body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotationY;
        body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping=.4f;body.angularDamping=5;body.maxAngularVelocity=.25f;
    }
    void RefreshPhysicalContact()
    {
        int left=0,right=0;Vector3 normal=Vector3.zero;
        for(int i=0;i<12;i++)if(found[i] && feet[i].y-surface[i].y<=groundClearance+suspensionCompression && feet[i].y-surface[i].y>=-maxStepHeight)
        {if(i<6)left++;else right++;}
        GroundedTrackPoints=left+right;
        if(Plane(false,out normal))SupportNormal=normal;
        else if(found[0] && found[2] && found[6])
        {normal=Vector3.Cross(surface[2]-surface[0],surface[6]-surface[0]).normalized;SupportNormal=normal.y<0?-normal:normal;}
        else SupportNormal=Vector3.up;
        Slope=Vector3.Angle(SupportNormal,Vector3.up);
        IsGrounded=left>=2 && right>=2 && Slope<=maxDriveSlope+.1f;
        VerticalSpeed=PhysicalBody.linearVelocity.y;
    }
    bool PhysicalMoveAllowed(Vector3 position,Quaternion heading)
    {
        Gather(position,heading);
        if(!Plane(false,out var normal) || Vector3.Angle(normal,Vector3.up)>maxDriveSlope)return false;
        // Ray-supported track ends can climb a ramp. A vertical pit wall still needs to be graded first.
        float rise=Height(heading)-chassis.position.y;
        return rise<=maxStepHeight;
    }
    void StepPhysicalSupport(float dt)
    {
        var body=PhysicalBody;
        AppliedLiftForce=0;RequiredLiftForce=0;
        if(massModel)massModel.Step();
        float mass=massModel?Mathf.Max(100,massModel.TotalMass):1700;
        body.mass=mass;
        var center=massModel?massModel.CenterOfMass:Pivot+chassis.up*.55f;
        body.centerOfMass=Quaternion.Inverse(chassis.rotation)*(center-chassis.position);
        float length=Vector3.Distance(SupportPoint(0),SupportPoint(2));
        float width=Vector3.Distance(SupportPoint(0),SupportPoint(6));
        body.inertiaTensorRotation=Quaternion.identity;
        body.inertiaTensor=new Vector3(mass*(length*length+1)/12,mass*(length*length+width*width)/12,mass*(width*width+1)/12);
        Gather(chassis.position,chassis.rotation);
        float stiffness=mass*gravity/(12*Mathf.Max(.005f,suspensionCompression));
        float damping=2*Mathf.Sqrt(stiffness*mass/12)*suspensionDamping;
        for(int i=0;i<12;i++)
        {
            if(!found[i])continue;
            float gap=feet[i].y-surface[i].y;
            if(gap>contactReach)continue;
            float force=mass*gravity/12+(groundClearance-gap)*stiffness-body.GetPointVelocity(feet[i]).y*damping;
            force=Mathf.Clamp(force,0,mass*gravity*2.5f/12);
            body.AddForceAtPosition(Vector3.up*force,feet[i],ForceMode.Force);
        }
        float deepest=0;Vector3 point=Vector3.zero;
        foreach(var tool in physicalTools)
        {
            if(!tool || !tool.isActiveAndEnabled || tool.CoveredByParent)continue;
            float depth=tool.Depth(terrain,out var p);
            depth=Mathf.Max(0,depth-tool.softSoilDepth);
            if(depth>deepest){deepest=depth;point=p;}
        }
        if(physicalBucket && DigSystem.I)
        {
            float depth=physicalBucket.GroundPenetration(DigSystem.I,false,out var p);
            if(depth>deepest){deepest=depth;point=p;}
        }
        if(deepest>0)ApplyToolForce(point,deepest,dt);
        else if(requestRemaining>0 && requestedEffort>0)
            ApplyToolForce(requestedPoint,.003f,dt);
        else {smoothedToolForce=0;toolForceVelocity=0;}
        requestRemaining=Mathf.Max(0,requestRemaining-dt);
        var velocity=body.linearVelocity;velocity.y=Mathf.Clamp(velocity.y,-maxFallSpeed,Mathf.Min(maxRiseSpeed,.35f));body.linearVelocity=velocity;
        float tilt=Vector3.Angle(chassis.up,SupportNormal);
        if(tilt>maxHydraulicTilt && Vector3.Dot(body.angularVelocity,Vector3.Cross(SupportNormal,chassis.up))>0)
        {
            var axis=Vector3.Cross(SupportNormal,chassis.up).normalized;
            body.angularVelocity-=axis*Mathf.Max(0,Vector3.Dot(body.angularVelocity,axis));
        }
        ConstrainTrackContact(dt);
        RefreshPhysicalContact();
    }
    void ConstrainTrackContact(float dt)
    {
        var body=PhysicalBody;float minGap=float.PositiveInfinity,minVelocity=-maxFallSpeed;
        for(int i=0;i<12;i++)
        {
            if(!found[i])continue;
            float gap=feet[i].y-surface[i].y;minGap=Mathf.Min(minGap,gap);
            if(gap>contactReach)continue;
            float angularY=body.GetPointVelocity(feet[i]).y-body.linearVelocity.y;
            minVelocity=Mathf.Max(minVelocity,(-terrainContactSkin-gap)/Mathf.Max(.001f,dt)-angularY+gravity*dt);
        }
        // A ray-supported chassis has no terrain collision impulse. Stop an incoming track at the
        // surface instead of waiting for capped suspension springs to recover a buried machine.
        var velocity=body.linearVelocity;velocity.y=Mathf.Max(velocity.y,Mathf.Min(.15f,minVelocity));body.linearVelocity=velocity;
        if(minGap < -terrainContactSkin)
        {
            body.position+=Vector3.up*Mathf.Min(.015f,-terrainContactSkin-minGap);
            body.angularVelocity*=Mathf.Exp(-20*dt);
            Gather(chassis.position,chassis.rotation);
        }
    }
    void RequestToolForce(Vector3 point,float depth,float effort)
    {
        if(!Ignition.EngineRunning || depth<=0 || depth>.1f || effort<=0)return;
        requestedPoint=point;requestedEffort=Mathf.Clamp01(effort);requestRemaining=.04f;
    }
    void ApplyToolForce(Vector3 point,float depth,float dt)
    {
        var direction=Vector3.ProjectOnPlane(point-Pivot,Vector3.up).normalized;
        if(direction.sqrMagnitude<.5f)return;
        float back=float.PositiveInfinity;
        for(int i=0;i<12;i++)if(found[i] && Mathf.Abs(feet[i].y-surface[i].y-groundClearance)<contactReach)
            back=Mathf.Min(back,Vector3.Dot(feet[i],direction));
        if(float.IsPositiveInfinity(back))return;
        Vector3 rear=Vector3.zero;int count=0;
        for(int i=0;i<12;i++)if(found[i] && Mathf.Abs(feet[i].y-surface[i].y-groundClearance)<contactReach && Vector3.Dot(feet[i],direction)<=back+.05f){rear+=feet[i];count++;}
        if(count==0)return;rear/=count;
        float arm=Vector3.Dot(point-rear,direction);if(arm<.25f)return;
        float weight=PhysicalBody.mass*gravity;
        RequiredLiftForce=weight*Mathf.Max(.05f,Vector3.Dot(PhysicalBody.worldCenterOfMass-rear,direction))/arm;
        float pressure=depth/Mathf.Max(.005f,toolCompliance);
        if(requestRemaining>0)pressure=Mathf.Max(pressure,requestedEffort);
        float cap=Mathf.Min(maximumHydraulicForce,RequiredLiftForce*Mathf.Min(supportForceMargin,1+liftForceSurplus));
        var torque=Quaternion.Inverse(PhysicalBody.rotation)*Vector3.Cross(point-PhysicalBody.worldCenterOfMass,Vector3.up);
        var inertia=PhysicalBody.inertiaTensor;
        float effectiveMass=1/(1/PhysicalBody.mass+torque.x*torque.x/Mathf.Max(1,inertia.x)+torque.y*torque.y/Mathf.Max(1,inertia.y)+torque.z*torque.z/Mathf.Max(1,inertia.z));
        float damping=2*Mathf.Sqrt(Mathf.Max(1,RequiredLiftForce/Mathf.Max(.005f,toolCompliance))*effectiveMass)*1.2f;
        float target=Mathf.Clamp(Mathf.Min(maximumHydraulicForce*Mathf.Clamp01(pressure),cap)-PhysicalBody.GetPointVelocity(point).y*damping,0,cap);
        float force=Mathf.Clamp(Mathf.SmoothDamp(smoothedToolForce,target,ref toolForceVelocity,supportForceSmoothing,float.PositiveInfinity,dt),0,cap);
        if(Vector3.Angle(chassis.up,SupportNormal)>=maxHydraulicTilt && force>RequiredLiftForce)force=RequiredLiftForce;
        smoothedToolForce=force;
        AppliedLiftForce=force;HydraulicLoad=Mathf.Max(HydraulicLoad,Mathf.Clamp01(force/maximumHydraulicForce));
        // Actual Newtons at the planted tool, balanced by independent track spring forces and gravity.
        PhysicalBody.AddForceAtPosition(Vector3.up*force,point,ForceMode.Force);
    }
}
