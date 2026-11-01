using UnityEngine;

public partial class ExcavatorGrounding
{
    [Header("Physical track suspension")]
    public bool physicalSuspension=true;
    [Min(.005f)] public float suspensionCompression=.018f;
    [Range(.3f,2)] public float suspensionDamping=1;
    [Min(.01f)] public float contactReach=.06f;
    [HideInInspector] public float supportForceMargin=1.2f; // Legacy serialized tuning.
    [Min(.01f)] public float toolCompliance=.025f;
    [Min(.04f)] public float supportForceSmoothing=.15f;
    [HideInInspector] public float liftForceSurplus=.06f;
    [Min(.001f)] public float terrainContactSkin=.005f;
    [Min(.01f)] public float plantedToolSpeed=.08f;
    [Min(.005f)] public float supportReleaseGap=.02f;
    float toolSupportLoad;
    Transform plantedFrame;
    Vector3 plantedLocalPoint;
    float plantedAllowance;
    [Min(.02f)] public float supportedRestDelay=.12f;
    PartDriver[] supportDrivers;
    TrackDrive supportDrive;
    bool restingOnTool;
    float supportedRestClock,restMass;
    Vector3 restPosition,restToolPosition;
    Quaternion restRotation,restToolRotation;
    Transform restTool;
    readonly float[] restGround=new float[12];
    readonly Vector3[] supportPolygon=new Vector3[15];
    float restToolGround;
    public int GroundedTrackPoints {get;private set;}
    public Rigidbody PhysicalBody {get;private set;}
    public bool UsesPhysicalSupport => physicalSuspension && PhysicalBody && !PhysicalBody.isKinematic;
    GroundedAttachment[] physicalTools;
    BucketInside physicalBucket;
    float requestedEffort,requestRemaining;

    public void EnablePhysicalSupport(Rigidbody body)
    {
        PhysicalBody=body;
        plantedFrame=null;toolSupportLoad=0;requestRemaining=0;
        restingOnTool=false;supportedRestClock=0;
        supportDrivers=GetComponentsInChildren<PartDriver>(true);
        supportDrive=GetComponentInChildren<TrackDrive>(true);
        physicalTools=GetComponentsInChildren<GroundedAttachment>(true);
        physicalBucket=GetComponentInChildren<BucketInside>(true);
        body.isKinematic=false;body.useGravity=true;
        body.constraints=RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ|RigidbodyConstraints.FreezeRotationY;
        body.interpolation=RigidbodyInterpolation.Interpolate;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping=.4f;body.angularDamping=5;body.maxAngularVelocity=.25f;
        var finalizer=GetComponent<ExcavatorSupportFinalizer>();
        if(!finalizer)finalizer=gameObject.AddComponent<ExcavatorSupportFinalizer>();
        finalizer.grounding=this;
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
        var center=massModel?massModel.CenterOfMass:Pivot+chassis.up*.55f;
        Gather(chassis.position,chassis.rotation);
        float deepest=0,allowance=0;Vector3 point=Vector3.zero;Transform contactFrame=null;
        foreach(var tool in physicalTools)
        {
            if(!tool || !tool.isActiveAndEnabled || tool.CoveredByParent)continue;
            float depth=tool.Depth(terrain,out var p);
            depth=Mathf.Max(0,depth-tool.softSoilDepth);
            if(depth>deepest){deepest=depth;point=p;contactFrame=tool.transform;allowance=tool.softSoilDepth;}
        }
        if(physicalBucket && DigSystem.I)
        {
            float depth=physicalBucket.GroundPenetration(DigSystem.I,false,out var p);
            if(depth>deepest){deepest=depth;point=p;contactFrame=physicalBucket.transform;allowance=0;}
        }
        if(contactFrame)
        {plantedFrame=contactFrame;plantedLocalPoint=contactFrame.InverseTransformPoint(point);plantedAllowance=allowance;}
        bool supporting=deepest>0;
        if(!supporting && plantedFrame && plantedFrame.gameObject.activeInHierarchy)
        {
            point=plantedFrame.TransformPoint(plantedLocalPoint);
            if(Ground(point,out float y)){deepest=y-point.y-plantedAllowance;supporting=deepest>=-supportReleaseGap;}
        }
        if(!supporting){toolSupportLoad=0;plantedFrame=null;}
        if(TryRestOnSupports(point,deepest,mass,center,dt,supporting))
        {requestRemaining=Mathf.Max(0,requestRemaining-dt);RefreshPhysicalContact();return;}
        // Do not rewrite rigidbody mass properties or add support forces to a sleeping, parked rig.
        body.mass=mass;
        body.centerOfMass=Quaternion.Inverse(chassis.rotation)*(center-chassis.position);
        float length=Vector3.Distance(SupportPoint(0),SupportPoint(2));
        float width=Vector3.Distance(SupportPoint(0),SupportPoint(6));
        body.inertiaTensorRotation=Quaternion.identity;
        body.inertiaTensor=new Vector3(mass*(length*length+1)/12,mass*(length*length+width*width)/12,mass*(width*width+1)/12);
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
        if(supporting)ApplyToolForce(point,deepest,dt);
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
    public void FinalizeSupportedRest()
    {
        if(!UsesPhysicalSupport || !restingOnTool)return;
        if(massModel)massModel.Step();
        Gather(chassis.position,chassis.rotation);
        bool withTool=plantedFrame && plantedFrame.gameObject.activeInHierarchy;
        var point=withTool?plantedFrame.TransformPoint(plantedLocalPoint):Vector3.zero;
        float depth=0;
        if(withTool)
        {
            if(!Ground(point,out float height)){WakeToolSupport();return;}
            depth=height-point.y-plantedAllowance;
        }
        float mass=massModel?Mathf.Max(100,massModel.TotalMass):1700;
        var center=massModel?massModel.CenterOfMass:Pivot+chassis.up*.55f;
        TryRestOnSupports(point,depth,mass,center,0,withTool);
        RefreshPhysicalContact();
    }
    bool SupportControlsMoving()
    {
        if(supportDrive && supportDrive.HasMotion)return true;
        if(supportDrivers!=null)foreach(var driver in supportDrivers)
        {
            if(!driver || !driver.isActiveAndEnabled)continue;
            // Upper-body slew does not move the parked tracks or an independent blade support.
            if(driver.useWorldAxis && driver.target && !chassis.IsChildOf(driver.target) && (!plantedFrame || !plantedFrame.IsChildOf(driver.target)))continue;
            if(driver.RequestedFlow>.001f || Mathf.Abs(driver.AngularVelocity)>.01f)return true;
        }
        return false;
    }
    bool StableSupportPolygon(Vector3 tool,Vector3 center,bool withTool)
    {
        int count=0;
        float minimumGap=-Mathf.Max(terrainContactSkin,suspensionCompression);
        for(int i=0;i<12;i++)
        {
            if(!found[i])continue;
            float gap=feet[i].y-surface[i].y;
            if(gap<minimumGap)return false;
            if(gap<=groundClearance+suspensionCompression)supportPolygon[count++]=feet[i];
        }
        if(withTool)supportPolygon[count++]=tool;
        // Include the blade's contacting corners so a sideways arm load can remain supported.
        var blade=massModel?massModel.blade:null;
        bool broadBlade=withTool && blade && plantedFrame && (plantedFrame==blade || plantedFrame.IsChildOf(blade));
        var patch=broadBlade?plantedFrame.GetComponent<GroundedAttachment>():null;
        float halfWidth=patch?patch.WorkBounds.size.x*Mathf.Abs(plantedFrame.lossyScale.x)*.45f:0;
        if(halfWidth>0)for(int side=-1;side<=1;side+=2)
        {
            var edge=tool+plantedFrame.right*(side*halfWidth);
            if(Ground(edge,out float height) && height-edge.y>=-supportReleaseGap && height-edge.y<=.04f)
                supportPolygon[count++]=edge;
        }
        for(int a=0;a<count-2;a++)for(int b=a+1;b<count-1;b++)for(int c=b+1;c<count;c++)
        {
            var origin=supportPolygon[a];var u=supportPolygon[b]-origin;var v=supportPolygon[c]-origin;var p=center-origin;
            float determinant=u.x*v.z-v.x*u.z;
            if(Mathf.Abs(determinant)<.0001f)continue;
            float x=(p.x*v.z-v.x*p.z)/determinant,y=(u.x*p.z-p.x*u.z)/determinant;
            if(x>=-.001f && y>=-.001f && x+y<=1.001f)return true;
        }
        return false;
    }
    bool TryRestOnSupports(Vector3 point,float depth,float mass,Vector3 center,float dt,bool withTool)
    {
        var body=PhysicalBody;
        float maximumDepth=physicalBucket && plantedFrame==physicalBucket.transform?Mathf.Max(.04f,physicalBucket.maximumToothPenetration+.015f):.04f;
        if(SupportControlsMoving() || (withTool && (!plantedFrame || depth < -terrainContactSkin || depth>maximumDepth)) || !StableSupportPolygon(point,center,withTool))
        {WakeToolSupport();return false;}
        var localTool=withTool?chassis.InverseTransformPoint(plantedFrame.position):Vector3.zero;
        var localRotation=withTool?Quaternion.Inverse(chassis.rotation)*plantedFrame.rotation:Quaternion.identity;
        if(restingOnTool)
        {
            bool changed=restTool!=plantedFrame || Mathf.Abs(restMass-mass)>.25f ||
                (localTool-restToolPosition).sqrMagnitude>.000001f ||
                Quaternion.Angle(localRotation,restToolRotation)>.05f || (body.position-restPosition).sqrMagnitude>.0001f ||
                Quaternion.Angle(body.rotation,restRotation)>.5f;
            for(int i=0;i<12 && !changed;i++)
                changed=found[i]?(float.IsNaN(restGround[i]) || Mathf.Abs(surface[i].y-restGround[i])>.002f):!float.IsNaN(restGround[i]);
            if(withTool && (!Ground(point,out float ground) || Mathf.Abs(ground-restToolGround)>.002f))changed=true;
            if(changed || body.linearVelocity.sqrMagnitude>.04f || body.angularVelocity.sqrMagnitude>.04f)
            {WakeToolSupport();return false;}
        }
        else
        {
            // Closed valves dissipate the remaining chassis motion while supported; otherwise an
            // existing oscillation could continually restart the settling timer and never stop.
            float brake=Mathf.Exp(-dt/Mathf.Max(.04f,supportedRestDelay*.5f));
            body.linearVelocity*=brake;body.angularVelocity*=brake;
            if(body.linearVelocity.sqrMagnitude>.04f || body.angularVelocity.sqrMagnitude>.04f)
            {supportedRestClock=0;return false;}
            supportedRestClock+=dt;
            if(supportedRestClock<supportedRestDelay)return false;
            restingOnTool=true;restTool=plantedFrame;restMass=mass;
            restToolPosition=localTool;restToolRotation=localRotation;restPosition=body.position;restRotation=body.rotation;
            for(int i=0;i<12;i++)restGround[i]=found[i]?surface[i].y:float.NaN;
            if(withTool)Ground(point,out restToolGround);
            // Retain the supported load for a continuous hand-off when driving or moving the tool.
        }
        // Parked tracks and closed tool valves retain a stable pose without per-tick wake forces.
        if(!body.IsSleeping()){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.Sleep();}
        return true;
    }
    void WakeToolSupport()
    {
        if(restingOnTool)PhysicalBody.WakeUp();
        restingOnTool=false;supportedRestClock=0;
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
            minVelocity=Mathf.Max(minVelocity,(-terrainContactSkin-gap)/Mathf.Max(.001f,dt)-angularY);
        }
        // A ray-supported chassis has no terrain collision impulse. Stop an incoming track at the
        // surface instead of waiting for capped suspension springs to recover a buried machine.
        var velocity=body.linearVelocity;velocity.y=Mathf.Max(velocity.y,Mathf.Min(.15f,minVelocity));body.linearVelocity=velocity;
        if(minGap < -Mathf.Max(.025f,terrainContactSkin))
        {
            body.position+=Vector3.up*Mathf.Min(.005f,-terrainContactSkin-minGap);
            body.angularVelocity*=Mathf.Exp(-20*dt);
            Gather(chassis.position,chassis.rotation);
        }
    }
    void RequestToolForce(Vector3 point,float depth,float effort,Transform frame)
    {
        if(!Ignition.EngineRunning || depth<=0 || depth>.1f || effort<=0)return;
        plantedFrame=frame?frame:chassis;plantedLocalPoint=plantedFrame.InverseTransformPoint(point);
        WakeToolSupport();
        var contact=plantedFrame.GetComponent<GroundedAttachment>();plantedAllowance=contact?contact.softSoilDepth:0;
        requestedEffort=Mathf.Clamp01(effort);requestRemaining=.04f;
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
        float command=requestRemaining>0?requestedEffort:0;
        float compression=Mathf.Clamp01(depth/Mathf.Max(.005f,toolCompliance*.4f));
        float targetLoad=command>0?Mathf.Max(compression,command):compression;
        toolSupportLoad=Mathf.MoveTowards(toolSupportLoad,targetLoad,dt/Mathf.Max(.04f,supportForceSmoothing));
        float cap=maximumHydraulicForce;
        var torque=Quaternion.Inverse(PhysicalBody.rotation)*Vector3.Cross(point-PhysicalBody.worldCenterOfMass,Vector3.up);
        var inertia=PhysicalBody.inertiaTensor;
        float effectiveMass=1/(1/PhysicalBody.mass+torque.x*torque.x/Mathf.Max(1,inertia.x)+torque.y*torque.y/Mathf.Max(1,inertia.y)+torque.z*torque.z/Mathf.Max(1,inertia.z));
        // Do not low-pass the velocity feedback: its delay made the blade/track supports drive
        // each other into an oscillation. Keep contact across a small unloading gap instead.
        float error=Mathf.Sign(depth)*Mathf.Max(0,Mathf.Abs(depth)-terrainContactSkin);
        float desiredSpeed=Mathf.Clamp(error/Mathf.Max(.06f,supportForceSmoothing)+command*plantedToolSpeed,-plantedToolSpeed,plantedToolSpeed);
        float force=Mathf.Clamp(toolSupportLoad*RequiredLiftForce+effectiveMass*(desiredSpeed-PhysicalBody.GetPointVelocity(point).y)/Mathf.Max(.001f,dt*1.5f),0,cap);
        if(Vector3.Angle(chassis.up,SupportNormal)>=maxHydraulicTilt && force>RequiredLiftForce)force=RequiredLiftForce;
        AppliedLiftForce=force;HydraulicLoad=Mathf.Max(HydraulicLoad,Mathf.Clamp01(force/maximumHydraulicForce));
        // Actual Newtons at the planted tool, balanced by independent track spring forces and gravity.
        PhysicalBody.AddForceAtPosition(Vector3.up*force,point,ForceMode.Force);
    }
}
