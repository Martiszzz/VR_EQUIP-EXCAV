using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class FollowupPhysicsValidation
{
    public static void RunScenes()
    {
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        try
        {
            foreach(string level in new[]{"Level 1","Level 2"})
            {
                DigSystem.I=null;EditorSceneManager.OpenScene("Assets/Scenes/"+level+".unity");
                var soil=UnityEngine.Object.FindAnyObjectByType<DigSystem>();DigSystem.I=soil;
                var contacts=UnityEngine.Object.FindAnyObjectByType<ExcavatorContacts>();contacts.Initialize();
                var g=contacts.GetComponent<ExcavatorGrounding>();Require(g.Initialize(g.chassis,Vector3.forward),"Production track footprint missing");
                var body=g.PhysicalBody;body.interpolation=RigidbodyInterpolation.None;
                Require(contacts.GetComponentsInChildren<Rigidbody>().All(rb=>rb==body || rb.GetComponent<DirtBlock>()),level+" has an independent nested attachment body");
                var bucket=contacts.GetComponentInChildren<BucketInside>();var instrument=bucket?bucket.GetComponentInParent<DigTeeth>().transform:null;
                Vector3 local=instrument?instrument.localPosition:Vector3.zero;Quaternion rotation=instrument?instrument.localRotation:Quaternion.identity;
                for(int i=0;i<350;i++)Tick(g);
                float minGap=float.PositiveInfinity,maxGap=float.NegativeInfinity;
                for(int i=0;i<12;i++){var p=g.SupportPoint(i);float gap=p.y-soil.SurfaceY(p);minGap=Mathf.Min(minGap,gap);maxGap=Mathf.Max(maxGap,gap);}
                Debug.Log($"PRODUCTION SUPPORT {level}: gaps={minGap:F4}..{maxGap:F4} speed={body.linearVelocity.magnitude:F4} root={body.position}");
                Require(minGap>-.04f && body.linearVelocity.magnitude<.15f,level+" chassis sinks into terrain or does not settle");
                if(instrument)
                {
                    body.position+=Vector3.right*.15f;Physics.SyncTransforms();for(int i=0;i<5;i++)Tick(g);
                    Require(Vector3.Distance(local,instrument.localPosition)<.0001f && Quaternion.Angle(rotation,instrument.localRotation)<.01f,"Bucket drifts off the arm when the native chassis moves");
                    Require(instrument.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger).All(c=>c.attachedRigidbody==body),"Bucket shell is not part of the chassis compound body");
                }
            }
            CheckGrappleContactBudget();
            Debug.Log("PRODUCTION BODY PASS: both scenes settle; bucket follows arm as a compound body.");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        finally{Physics.simulationMode=mode;}
    }
    static void CheckGrappleContactBudget()
    {
        var soil=UnityEngine.Object.FindAnyObjectByType<DigSystem>();var runtime=UnityEngine.Object.Instantiate(soil.terrain.terrainData);soil.terrain.terrainData=runtime;soil.terrain.GetComponent<TerrainCollider>().terrainData=runtime;
        var contacts=UnityEngine.Object.FindAnyObjectByType<ExcavatorContacts>();var claw=contacts.GetComponentInChildren<Claw>();
        var driver=contacts.GetComponentsInChildren<PartDriver>().First(d=>d.target && d.target.name=="Bone_2" && claw.jaws[0].part.IsChildOf(d.target));
        // This benchmark isolates soil work. Its intentionally lowered fixture overlaps existing scene props.
        ExcavatorBody.For(driver.target).obstacles=0;
        driver.Initialize();Ignition.EngineRunning=true;driver.lever.Forward=driver.lever.Side=1;if(driver.throttle)driver.throttle.Value=1;
        float bottom=driver.target.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger).Min(c=>c.bounds.min.y);var p=driver.target.position;driver.target.position+=Vector3.up*(soil.SurfaceY(p)-.08f-bottom);Physics.SyncTransforms();
        for(int i=0;i<30;i++)driver.Step(.02f);
        long allocated=GC.GetAllocatedBytesForCurrentThread();var timer=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<200;i++){driver.Step(.02f);soil.FlushTerrainChanges(false);}
        timer.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Debug.Log($"GRAPPLE CONTACT BUDGET avg={timer.Elapsed.TotalMilliseconds/200:F3}ms allocation={allocated/200} bytes/step windows={soil.CachedTerrainWindows}");
        Require(timer.Elapsed.TotalMilliseconds/200<8,"Blocked production grapple exceeds the desktop contact time budget");
        var jaws=driver.target.GetComponentsInChildren<GroundedAttachment>().Where(a=>a.canScrapeSoil).ToArray();
        var diagnostic=ExcavatorBody.For(driver.target).CreateProbe(driver.target);
        Debug.Log($"GRAPPLE SETUP target={driver.target.name} jaws={jaws.Length} world={diagnostic.Depth(false):F4} engine={Ignition.EngineRunning} angle={driver.Angle:F2} limit={driver.minAngle}..{driver.maxAngle}");
        Action<int> scrape=i=>
        {
            float depth=0;foreach(var jaw in jaws)depth=Mathf.Max(depth,jaw.Depth(soil.terrain));
            driver.target.position+=Vector3.up*(depth-.03f);driver.lever.Forward=driver.lever.Side=(i/20%2==0?1:-1);
            driver.Step(.02f);soil.FlushTerrainChanges(false);
        };
        for(int i=0;i<40;i++)scrape(i);
        Debug.Log($"GRAPPLE SOFT SETUP raw={jaws.Max(j=>j.Depth(soil.terrain)):F4} world={diagnostic.Depth(false):F4} soft={jaws[0].softSoilDepth} angle={driver.Angle:F2}");
        uint revision=soil.TerrainRevision;allocated=GC.GetAllocatedBytesForCurrentThread();timer.Restart();
        for(int i=0;i<200;i++)scrape(i);
        timer.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        Debug.Log($"GRAPPLE SCRAPE BUDGET avg={timer.Elapsed.TotalMilliseconds/200:F3}ms allocation={allocated/200} bytes/step commits={soil.TerrainRevision-revision} windows={soil.CachedTerrainWindows}");
        Require(soil.TerrainRevision>revision,"Production soft-contact benchmark did not deform any soil");
        Require(timer.Elapsed.TotalMilliseconds/200<8,"Soft production grapple exceeds the desktop contact time budget");
    }
    public static void Run()
    {
        try{CheckSuspension();CheckSuspension(.02f);CheckReach();CheckPitExit();CheckLocalTerrain();CheckSoftToolAndLog();CheckProductionBlade();Debug.Log("FOLLOWUP PHYSICS PASS: native point forces, individual track support, blade pitching, pit exit, terrain sync, local work guards, soft log contact and production blade.");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void CheckSuspension(float scale=1)
    {
        var old=DigSystem.I;DigSystem.I=null;
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(12,8,12)};
        var heights=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=.25f;data.SetHeights(0,0,heights);
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(10000,0,10000);
        var rig=new GameObject("Native suspension fixture");rig.transform.position=new Vector3(10006,2.512f,10006);rig.transform.localScale=Vector3.one*scale;
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        try
        {
            var g=rig.AddComponent<ExcavatorGrounding>();g.terrain=ground.GetComponent<Terrain>();
            foreach(bool right in new[]{false,true})
            {
                var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.SetParent(rig.transform,false);t.localPosition=new Vector3(right?.5f:-.5f,-.25f,0)/scale;t.localScale=new Vector3(.3f,.5f,1.5f)/scale;UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());
                if(right)g.rightTrack=t;else g.leftTrack=t;
            }
            var blade=new GameObject("Planted blade").transform;blade.SetParent(rig.transform,false);blade.localScale=Vector3.one/scale;blade.localPosition=new Vector3(0,0,1.8f)/scale;
            var contact=blade.gameObject.AddComponent<GroundedAttachment>();contact.includeChildren=false;blade.gameObject.AddComponent<BoxCollider>().size=new Vector3(.8f,.12f,.15f);
            Physics.IgnoreCollision(blade.GetComponent<Collider>(),ground.GetComponent<TerrainCollider>());
            g.Initialize(rig.transform,Vector3.forward);
            var mass=rig.AddComponent<ExcavatorMass>();g.massModel=mass;mass.grounding=g;mass.blade=blade;mass.Step();
            var body=rig.AddComponent<Rigidbody>();g.EnablePhysicalSupport(body);body.interpolation=RigidbodyInterpolation.None;
            for(int i=0;i<80;i++)Tick(g);
            float rear=g.SupportPoint(0).y,front=g.SupportPoint(2).y;
            Require(Mathf.Abs(rear-2.012f)<.02f && Mathf.Abs(front-rear)<.01f,"Track springs do not settle flat");
            Require(Vector3.Distance(body.worldCenterOfMass,mass.CenterOfMass)<.005f,"Scaled chassis center of mass does not match the mass model");
            blade.localPosition=new Vector3(0,-.5f,1.8f)/scale;
            for(int i=0;i<160;i++)Tick(g);
            float rearLift=g.SupportPoint(0).y-rear,frontLift=g.SupportPoint(2).y-front;
            Debug.Log($"NATIVE BLADE SUPPORT rear={rearLift:F4} front={frontLift:F4} force={g.AppliedLiftForce:F0} contacts={g.GroundedTrackPoints}");
            Require(frontLift>.012f && Mathf.Abs(rearLift)<.02f && frontLift>rearLift+.015f,"Blade raises whole chassis instead of pitching about opposite tracks");
            Require(body.linearVelocity.magnitude<.25f,"Planted machine keeps bouncing");
            blade.localPosition=new Vector3(0,0,1.8f)/scale;
            for(int i=0;i<200;i++)Tick(g);
            Require(Mathf.Abs(g.SupportPoint(2).y-front)<.02f && Mathf.Abs(g.SupportPoint(0).y-rear)<.02f,"Released machine does not settle");
        }
        finally{Physics.simulationMode=mode;DigSystem.I=old;UnityEngine.Object.DestroyImmediate(rig);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    static void Tick(ExcavatorGrounding g){g.Settle(.02f);Physics.SyncTransforms();Physics.Simulate(.02f);}
    public static void CheckPitExit()
    {
        var old=DigSystem.I;DigSystem.I=null;bool running=Ignition.EngineRunning;Ignition.EngineRunning=true;
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(12,8,12)};var h=new float[129,129];
        for(int z=0;z<129;z++)for(int x=0;x<129;x++){float wz=z/128f*12;h[z,x]=(wz<5?1.65f:Mathf.Min(2,1.65f+(wz-5)*Mathf.Tan(30*Mathf.Deg2Rad)))/8;}
        data.SetHeights(0,0,h);var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(11000,0,11000);
        var rig=new GameObject("Native pit exit");rig.transform.position=new Vector3(11006,2.162f,11002.5f);
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        try
        {
            var g=rig.AddComponent<ExcavatorGrounding>();g.terrain=ground.GetComponent<Terrain>();g.maxDriveSlope=35;
            foreach(bool right in new[]{false,true})
            {
                var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.SetParent(rig.transform,false);t.localPosition=new Vector3(right?.5f:-.5f,-.25f,0);t.localScale=new Vector3(.3f,.5f,1.5f);UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());
                if(right)g.rightTrack=t;else g.leftTrack=t;
            }
            g.Initialize(rig.transform,Vector3.forward);var body=rig.AddComponent<Rigidbody>();g.EnablePhysicalSupport(body);body.interpolation=RigidbodyInterpolation.None;
            var drive=rig.AddComponent<TrackDrive>();drive.excavator=rig.transform;drive.grounding=g;drive.blockByWorld=false;
            foreach(bool right in new[]{false,true}){var go=new GameObject("Track input");go.transform.SetParent(rig.transform,false);go.AddComponent<XRSimpleInteractable>();var lever=go.AddComponent<LeverTilt>();lever.Forward=1;if(right)drive.rightLever=lever;else drive.leftLever=lever;}
            float maxJump=0;
            for(int i=0;i<800;i++){float before=rig.transform.position.y;drive.Step(.02f);Physics.SyncTransforms();Physics.Simulate(.02f);maxJump=Mathf.Max(maxJump,Mathf.Abs(before-rig.transform.position.y));}
            Debug.Log($"PIT EXIT z={rig.transform.position.z-11000:F2} foot={g.SupportPoint(0).y:F3} jump={maxJump:F4}");
            Require(rig.transform.position.z>11006.7f && g.SupportPoint(0).y>1.95f && maxJump<.06f,"Physical tracks cannot climb out of a shallow pit along its 30 degree ramp");
        }
        finally{Physics.simulationMode=mode;Ignition.EngineRunning=running;DigSystem.I=old;UnityEngine.Object.DestroyImmediate(rig);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    public static void CheckLocalTerrain()
    {
        var old=DigSystem.I;var data=new TerrainData{heightmapResolution=129,alphamapResolution=128,size=new Vector3(8,4,8)};
        var h=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)h[z,x]=.5f+Mathf.Sin(x*.5f)*Mathf.Cos(z*.5f)*.025f;data.SetHeights(0,0,h);
        data.terrainLayers=new[]{SoilClodVisual.GroundLayer,SoilClodVisual.DumpedLayer};var alpha=new float[128,128,2];for(int z=0;z<128;z++)for(int x=0;x<128;x++)alpha[z,x,0]=1;data.SetAlphamaps(0,0,alpha);
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(12000,0,12000);var owner=new GameObject("Local soil bounds");
        try
        {
            var soil=owner.AddComponent<DigSystem>();soil.terrain=ground.GetComponent<Terrain>();DigSystem.I=soil;
            var machine=owner.AddComponent<ExcavatorGrounding>();machine.chassis=owner.transform;owner.transform.position=new Vector3(12004,2,12004);soil.workMachine=machine;soil.maximumWorkDistance=2;
            var p=new Vector3(12004,2,12004);var far=p+Vector3.right*3;
            Require(soil.Dig(far,.1f,.2f)==0 && soil.Add(far,.1f,.01f)==0,"Remote deformation is not rejected");
            float original=soil.SurfaceY(p);soil.Add(p,.12f,.01f);soil.FlushTerrainChanges(false);Physics.SyncTransforms();
            Require(soil.TerrainRevision==soil.SyncedTerrainRevision,"Visual/physics heightmap commit lags behind deformation");
            Require(Physics.Raycast(p+Vector3.up*5,Vector3.down,out var hit,10) && Mathf.Abs(hit.point.y-soil.SurfaceY(p))<.003f,"Terrain collider does not match the visible mound");
            soil.Dig(p,.12f,.02f);soil.FlushTerrainChanges(false);Physics.SyncTransforms();
            Require(Physics.Raycast(p+Vector3.up*5,Vector3.down,out hit,10) && Mathf.Abs(hit.point.y-soil.SurfaceY(p))<.003f,"Terrain collider does not match the cut");
            for(int i=0;i<100;i++)soil.StepSoil(.08f);for(int i=0;i<10;i++)soil.FlushTerrainChanges();
            var now=data.GetHeights(0,0,129,129);for(int z=0;z<129;z++)for(int x=0;x<129;x++)if(Mathf.Abs(x-64)>34 || Mathf.Abs(z-64)>34)Require(Mathf.Abs(now[z,x]-h[z,x])<.0001f,"Local work erodes distant original hills");
            var map=data.GetAlphamaps(0,0,128,128);for(int z=0;z<128;z++)for(int x=0;x<128;x++)if(Mathf.Abs(x-64)>34 || Mathf.Abs(z-64)>34)Require(map[z,x,1]<.001f,"Brown splats appear outside the work area");
        }
        finally{DigSystem.I=old;UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    public static void RunContacts()
    {try{CheckSoftToolAndLog();Debug.Log("SOFT TOOL/LOG PASS: conserved scraping, zero bounce and stable lifting contact.");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    public static void CheckSoftToolAndLog()
    {
        var previous=DigSystem.I;bool running=Ignition.EngineRunning;Ignition.EngineRunning=true;
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(8,4,8)};var h=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)h[z,x]=.5f;data.SetHeights(0,0,h);
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(15000,0,15000);
        var owner=new GameObject("Soft tool fixture");var tool=GameObject.CreatePrimitive(PrimitiveType.Capsule);UnityEngine.Object.DestroyImmediate(tool.GetComponent<CapsuleCollider>());var collider=tool.AddComponent<CapsuleCollider>();collider.direction=0;collider.radius=.035f;collider.height=.4f;tool.transform.position=new Vector3(15003,1.985f,15004);
        var log=GameObject.CreatePrimitive(PrimitiveType.Capsule);UnityEngine.Object.DestroyImmediate(log.GetComponent<CapsuleCollider>());var logCollider=log.AddComponent<CapsuleCollider>();logCollider.direction=2;logCollider.radius=.15f;logCollider.height=1.2f;log.transform.position=new Vector3(15004,2.16f,15004);
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        try
        {
            var soil=owner.AddComponent<DigSystem>();soil.terrain=ground.GetComponent<Terrain>();soil.simulateSlopes=false;DigSystem.I=soil;
            // Two longitudinal rounded prongs cradle the cylindrical log; a single round crossbar cannot prevent rolling.
            collider.direction=2;collider.height=1;collider.center=Vector3.right*-.12f;var second=tool.AddComponent<CapsuleCollider>();second.direction=2;second.radius=.035f;second.height=1;second.center=Vector3.right*.12f;
            var contact=tool.AddComponent<GroundedAttachment>();contact.canScrapeSoil=true;contact.softSoilDepth=.06f;
            var logBody=log.AddComponent<Rigidbody>();logBody.mass=30;
            var contacts=tool.AddComponent<ExcavatorContacts>();contacts.forceBasedChassis=false;contacts.Initialize();
            var body=tool.GetComponent<Rigidbody>();body.interpolation=RigidbodyInterpolation.None;
            Physics.IgnoreCollision(collider,ground.GetComponent<TerrainCollider>());
            Physics.IgnoreCollision(second,ground.GetComponent<TerrainCollider>());
            var probe=ExcavatorBody.For(tool.transform).CreateProbe(tool.transform);
            double before=TerrainVolume(data);float scraped=0,maxSpeed=0;
            for(int i=0;i<230;i++)
            {
                probe.BeginMove();var old=tool.transform.position;var next=old+Vector3.right*.006f;tool.transform.position=next;Physics.SyncTransforms();scraped+=probe.WorkSoil(.02f);probe.Depth(false);probe.ResolveDynamicContacts(.02f);
                body.position=old;body.MovePosition(next);soil.FlushTerrainChanges(false);Physics.Simulate(.02f);maxSpeed=Mathf.Max(maxSpeed,logBody.linearVelocity.magnitude);
            }
            Require(scraped>0 && Math.Abs(TerrainVolume(data)-before)<.0005,"Gripper cannot scrape loose ground without deleting soil");
            float initialY=logBody.position.y;
            // Bring the rounded finger beneath the resting log, then raise it with native collision/friction contact.
            var start=body.position;start.x=logBody.position.x;start.z=logBody.position.z;body.position=start;body.rotation=Quaternion.Euler(0,logBody.rotation.eulerAngles.y,0);Physics.SyncTransforms();
            for(int i=0;i<100;i++){body.MovePosition(body.position+Vector3.up*.004f);Physics.Simulate(.02f);maxSpeed=Mathf.Max(maxSpeed,logBody.linearVelocity.magnitude);}
            Debug.Log($"SOFT TOOL LOG scraped={scraped:F4} rise={logBody.position.y-initialY:F3} speed={maxSpeed:F3} tool={body.position} log={logBody.position} initial={initialY}");
            Require(logBody.position.y>initialY+.12f && maxSpeed<2.5f,"Finger contact launches the log or cannot lift it");
        }
        finally{Physics.simulationMode=mode;Ignition.EngineRunning=running;DigSystem.I=previous;UnityEngine.Object.DestroyImmediate(log);UnityEngine.Object.DestroyImmediate(tool);UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    static double TerrainVolume(TerrainData data){double total=0;foreach(float value in data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution))total+=value;return total*data.size.y*data.heightmapScale.x*data.heightmapScale.z;}
    public static void CheckProductionBlade()
    {
        var previous=DigSystem.I;bool running=Ignition.EngineRunning;Ignition.EngineRunning=true;
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(8,4,8)};var h=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)h[z,x]=.5f;data.SetHeights(0,0,h);
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(16000,0,16000);var owner=new GameObject("Production blade test");
        var geometry=new RealBucketWorkValidation.Geometry(File.ReadAllText("Assets/Scenes/Level 1.unity"));
        try
        {
            geometry.AddShell("8488504430534877925");var blade=geometry.Node("8488504430534877925");var root=geometry.Node("1113508163246883981");
            if(blade.GetComponentsInChildren<Collider>().Length==0)
            {
                var mesh=AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath("ea6e757fa7e3b98439011e55b0a7b144")).OfType<Mesh>().First(m=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==-7933065038289745687L;});
                var box=blade.gameObject.AddComponent<BoxCollider>();box.center=mesh.bounds.center;box.size=mesh.bounds.size;
            }
            var contact=blade.gameObject.AddComponent<GroundedAttachment>();var bounds=contact.WorkBounds;float bottom=float.PositiveInfinity;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)bottom=Mathf.Min(bottom,blade.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))).y);
            var center=blade.TransformPoint(bounds.center);root.position+=new Vector3(16004-center.x,1.94f-bottom,16003-center.z);
            var soil=owner.AddComponent<DigSystem>();soil.terrain=ground.GetComponent<Terrain>();soil.simulateSlopes=false;DigSystem.I=soil;
            var hole=new Vector3(16004,2,16004);soil.Dig(hole,.7f,.1f);float beforeHole=soil.SurfaceY(hole);double before=TerrainVolume(data);float moved=0,maxLoad=0;
            for(int i=0;i<100;i++){root.position+=Vector3.forward*.008f;moved+=soil.PushAttachmentSoil(contact,Vector3.forward*.008f);maxLoad=Mathf.Max(maxLoad,soil.LastBladeLoad);}
            Require(moved>0 && maxLoad>0,"Authored blade does not move soil or produce drive load");
            Require(soil.SurfaceY(hole)>beforeHole+.001f,"Authored blade does not fill the depression ahead");
            Require(Math.Abs(TerrainVolume(data)-before)<.0005,"Authored blade does not conserve soil volume");
            Debug.Log($"PRODUCTION BLADE moved={moved:F4} fill={soil.SurfaceY(hole)-beforeHole:F4} load={maxLoad:F2}");
        }
        finally{DigSystem.I=previous;Ignition.EngineRunning=running;geometry.Dispose();UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    public static void CheckReach()
    {
        var production=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/New Terrain 1.asset");if(production)Debug.Log($"PRODUCTION TERRAIN {production.heightmapResolution} size={production.size} cell={production.heightmapScale}");
        var geometry=new RealBucketWorkValidation.Geometry(File.ReadAllText("Assets/Scenes/Level 1.unity"));
        try
        {
            var a=geometry.Node("2540124487336731028");var b=geometry.Node("5701651948974088746");var tool=geometry.Node("8785660220177547002");var tip=geometry.Node("151281893");var track=geometry.Node("346980961889796245");
            var mesh=AssetDatabase.LoadAllAssetsAtPath("Assets/Excav.fbx").OfType<Mesh>().First(m=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==-7294526821872198993L;});
            float floor=float.PositiveInfinity;var bounds=mesh.bounds;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)floor=Mathf.Min(floor,track.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))).y);
            var ar=a.localRotation;var br=b.localRotation;var tr=tool.localRotation;
            float deepest=float.PositiveInfinity;Vector3 angles=Vector3.zero;
            for(int x=-20;x<=60;x+=5)for(int y=-40;y<=20;y+=5)for(int z=-80;z<=85;z+=15)
            {a.localRotation=ar*Quaternion.AngleAxis(x,Vector3.right);b.localRotation=br*Quaternion.AngleAxis(y,Vector3.right);tool.localRotation=tr*Quaternion.AngleAxis(z,Vector3.right);if(tip.position.y<deepest){deepest=tip.position.y;angles=new Vector3(x,y,z);}}
            Debug.Log($"ACTUAL DIGGING REACH depth={floor-deepest:F3}m angles={angles}");
            Require(floor-deepest>=2, "Actual authored arm cannot reach 2 m below tracks");
        }
        finally{geometry.Dispose();}
    }
    public static void RunDeep()
    {try{CheckDeepCycles();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
    public static void CheckDeepCycles()
    {
        var previous=DigSystem.I;bool running=Ignition.EngineRunning;Ignition.EngineRunning=true;
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(12,8,12)};var h=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)h[z,x]=.5f;data.SetHeights(0,0,h);
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(13000,0,13000);
        var owner=new GameObject("Deep digging cycles");var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.transform.position=Vector3.up*100;
        var production=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/New Terrain 1.asset");if(production)Debug.Log($"PRODUCTION TERRAIN {production.heightmapResolution} size={production.size} cell={production.heightmapScale}");
        var geometry=new RealBucketWorkValidation.Geometry(File.ReadAllText("Assets/Scenes/Level 1.unity"));
        var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
        try
        {
            geometry.AddShell("8785660220177547002");
            var root=geometry.Node("1113508163246883981");var a=geometry.Node("2540124487336731028");var b=geometry.Node("5701651948974088746");var tool=geometry.Node("8785660220177547002");var tip=geometry.Node("151281893");
            var bucket=geometry.Node("1249843221").gameObject.AddComponent<BucketInside>();bucket.pourPoint=tip;bucket.capacity=180;
            var teeth=tool.gameObject.AddComponent<DigTeeth>();teeth.tip=tip;
            var soil=owner.AddComponent<DigSystem>();soil.terrain=ground.GetComponent<Terrain>();soil.bucket=bucket;soil.maxBlocks=256;soil.simulateSlopes=false;soil.clayDepth=1.5f;soil.compactedDepth=3.5f;
            soil.blockPrefab=prefab.AddComponent<DirtBlock>();soil.blockPrefab.rb=prefab.AddComponent<Rigidbody>();DigSystem.I=soil;
            // The real rig is translated once onto the test bed; all subsequent digging uses its original joint transforms.
            var track=geometry.Node("346980961889796245");var mesh=AssetDatabase.LoadAllAssetsAtPath("Assets/Excav.fbx").OfType<Mesh>().First(m=>{AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==-7294526821872198993L;});
            float floor=float.PositiveInfinity;var bounds=mesh.bounds;Vector3 trackCenter=track.TransformPoint(bounds.center);
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)floor=Mathf.Min(floor,track.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))).y);
            root.position+=new Vector3(13006-trackCenter.x,4.012f-floor,13003-trackCenter.z);
            var aDriver=Driver(root,a,-20,60);var bDriver=Driver(root,b,-40,20);var cDriver=Driver(root,tool,-80,85);
            int dumps=0;float deepest=4;double collected=0;
            Action<PartDriver,float,int> move=(driver,target,ticks)=>
            {
                int idle=0;
                for(int i=0;i<ticks;i++)
                {
                    float oldAngle=driver.Angle;uint revision=soil.TerrainRevision;
                    driver.lever.Forward=Mathf.Clamp((target-driver.Angle)*.8f,-1,1);driver.Step(.02f);
                    if(soil.ContainsTerrainPoint(tip.position))deepest=Mathf.Min(deepest,soil.SurfaceY(tip.position));
                    if(bucket.FillFraction>=.8f)
                    {
                        collected+=bucket.HeldVolume;DumpFixture(bucket,soil,new Vector3(13001+(dumps%5)*.4f,4.15f,13009+(dumps/5%5)*.4f));dumps++;
                    }
                    idle=Mathf.Abs(driver.Angle-oldAngle)<.00001f && revision==soil.TerrainRevision?idle+1:0;
                    if(idle>=60 || (Mathf.Abs(driver.Angle-target)<.16f && Mathf.Abs(driver.AngularVelocity)<.2f))break;
                }
                driver.lever.Forward=0;
            };
            move(cDriver,-65,250);move(bDriver,-40,250);
            for(int angle=-10;angle<=60;angle+=5)
            {
                for(int stroke=0;stroke<(angle>=55?14:angle>=35?7:4);stroke++)
                {
                    // Extend the dipper while raised, bite down with the teeth, then pull towards the cab.
                    // On this authored rig -40 extends and +20 retracts; the reverse stroke pushes the bucket's back wall.
                    move(aDriver,-20,250);move(cDriver,-65,250);move(bDriver,-40,250);
                    move(aDriver,angle,600);move(bDriver,20,600);move(aDriver,angle,600);move(cDriver,-25,250);
                    if(angle>=55)Debug.Log($"DEEP STROKE target={angle} a/b/c={aDriver.Angle:F2}/{bDriver.Angle:F2}/{cDriver.Angle:F2} tip={tip.position} depth={bucket.GroundPenetration(soil,false,out var p):F4} contact={p}");
                    move(aDriver,-20,350);
                    if(bucket.CapturedCount>0){collected+=bucket.HeldVolume;DumpFixture(bucket,soil,new Vector3(13001+(dumps%5)*.4f,4.15f,13009+(dumps/5%5)*.4f));dumps++;}
                    if(4-deepest>=2.03f)break;
                }
                Debug.Log($"DEEP CYCLE angle={angle} actual={aDriver.Angle:F1}/{bDriver.Angle:F1} depth={4-deepest:F3} dumps={dumps} held={bucket.HeldVolume:F5} pending={teeth.PendingVolume:F5} grading={bucket.GradingContact} curl={cDriver.Angle:F1} tip={tip.position.y:F3} contact={bucket.GroundPenetration(soil,false):F3}");
                if(4-deepest>=2.03f)break;
            }
            Debug.Log($"DEEP DIG RESULT depth={4-deepest:F3}m dumps={dumps} collected={collected:F3}m3");
            Require(4-deepest>=1.98f && dumps>0,"Normal joint-driven cutting/dumping cycles do not excavate 2 m");
        }
        finally{Physics.simulationMode=mode;DigSystem.I=previous;Ignition.EngineRunning=running;geometry.Dispose();UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(prefab);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    static PartDriver Driver(Transform root,Transform target,float min,float max)
    {
        var input=new GameObject("Joint input");input.transform.SetParent(root,false);input.AddComponent<XRSimpleInteractable>();var lever=input.AddComponent<LeverTilt>();
        var d=input.AddComponent<PartDriver>();d.target=target;d.lever=lever;d.minAngle=min;d.maxAngle=max;d.blockByWorld=true;d.Initialize();return d;
    }
    static void DumpFixture(BucketInside bucket,DigSystem soil,Vector3 destination)
    {
        int watchdog=0;
        while(bucket.CapturedCount>0 || soil.LooseBodyCount>0)
        {
            foreach(var block in bucket.GetComponentsInChildren<DirtBlock>().Take(soil.AvailableLooseBodies).ToArray())
            {bucket.Forget(block);block.DropAsSoil(destination,Vector3.zero);}
            Physics.SyncTransforms();soil.FlushTerrainChanges(false);Physics.Simulate(.02f);
            foreach(var block in soil.GetComponentsInChildren<DirtBlock>())block.Step(.02f);
            if(++watchdog>600)throw new Exception("Dump fixture cannot settle its conserved cargo");
        }
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
