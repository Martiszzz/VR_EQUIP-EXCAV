using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class ExcavatorPhysicsValidation
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static double Volume(TerrainData td){double n=0;foreach(float h in td.GetHeights(0,0,td.heightmapResolution,td.heightmapResolution))n+=h;return n*td.size.y*td.heightmapScale.x*td.heightmapScale.z;}
    public static void Check()
    {
        var old=DigSystem.I;bool running=Ignition.EngineRunning;
        var data=new TerrainData {heightmapResolution=129,alphamapResolution=128,size=new Vector3(8,4,8)};
        var heights=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=0.5f;data.SetHeights(0,0,heights);
        data.terrainLayers=new[]{SoilClodVisual.GroundLayer,SoilClodVisual.DumpedLayer};
        var alpha=new float[128,128,2];for(int z=0;z<128;z++)for(int x=0;x<128;x++)alpha[z,x,0]=1;data.SetAlphamaps(0,0,alpha);
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(6000,0,6000);
        var owner=new GameObject("Soil volume regression");var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.transform.position=Vector3.up*100;
        var geometry=new RealBucketWorkValidation.Geometry(File.ReadAllText("Assets/Scenes/Level 1.unity"));
        try
        {
            geometry.AddShell("8785660220177547002");
            var root=geometry.Node("1113508163246883981");var bucket=geometry.Node("1249843221").gameObject.AddComponent<BucketInside>();
            bucket.capacity=180;bucket.pourPoint=geometry.Node("151281893");
            var instrument=geometry.Node("8785660220177547002");var teeth=instrument.gameObject.AddComponent<DigTeeth>();teeth.tip=bucket.pourPoint;
            var system=owner.AddComponent<DigSystem>();system.terrain=ground.GetComponent<Terrain>();system.bucket=bucket;system.maxBlocks=600;
            system.blockPrefab=prefab.AddComponent<DirtBlock>();system.blockPrefab.rb=prefab.AddComponent<Rigidbody>();
            DigSystem.I=system;Ignition.EngineRunning=true;
            float bottom=Bottom(bucket);root.position+=new Vector3(6004-bucket.transform.position.x,1.98f-bottom,6003-bucket.transform.position.z);
            var p=teeth.tip.position;double before=Volume(data);
            teeth.Sweep(p+Vector3.up*0.03f,p,0.02f);
            Require(bucket.CapturedCount==0 && teeth.PendingVolume==0 && Math.Abs(Volume(data)-before)<0.00001,"Pressing bucket floor into soil collects dirt");
            Require(bucket.GradingContact && bucket.GroundPenetration(system)>0.01f,"Bucket floor has no contact constraint");
            bucket.GradingContact=false;
            var passive=ExcavatorBody.For(root).CreateProbe(root);passive.BeginMove();
            Require(passive.GroundDepth()>0.01f,"Passive falling allows an empty bucket floor through terrain");
            typeof(DigTeeth).GetMethod("FixedUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(teeth,null);
            Require(Math.Abs(Volume(data)-before)<0.00001,"Passive bucket motion excavates soil");
            bucket.GradingContact=true;
            var box=bucket.GetComponent<BoxCollider>();float geometric=box.size.x*box.size.y*box.size.z*Mathf.Abs(bucket.transform.lossyScale.x*bucket.transform.lossyScale.y*bucket.transform.lossyScale.z);
            int count=0;
            while(bucket.TryGetSpawnPosition(system.blockSize,out var place)) {bucket.Capture(system.Spawn(place));count++;if(count>180)throw new Exception("Capacity overflow");}
            Require(count>60 && bucket.HeldVolume>0.013f,$"Actual bucket load remains too small: {count} parcels / {bucket.HeldVolume:F4}m3, cargo {geometric:F4}m3");
            float held=bucket.HeldVolume;
            Debug.Log($"ACTUAL BUCKET CAPACITY {count} parcels, {held:F4}m3, cargo envelope {geometric:F4}m3");
            root.position+=Vector3.up*3;
            bucket.transform.rotation=Quaternion.Euler(90,0,0);
            Require(bucket.IsDumping,"Roundtrip bucket is not tipped");
            double beforeDump=Volume(data);
            var simulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                for(int i=0;i<450;i++)
                {
                    bucket.Step(0.02f);Physics.SyncTransforms();Physics.Simulate(0.02f);
                    foreach(var clod in owner.GetComponentsInChildren<DirtBlock>().ToArray())clod.Step(0.02f);
                }
            }
            finally{Physics.simulationMode=simulation;}
            system.FlushTerrainChanges();
            float airborne=owner.GetComponentsInChildren<DirtBlock>().Where(c=>c.gameObject.activeSelf).Sum(c=>c.Volume);
            Require(bucket.CapturedCount==0 && airborne<0.00001f,$"Poured soil remains trapped: held={bucket.CapturedCount}, loose={airborne}");
            Require(Math.Abs(Volume(data)-beforeDump-held)<0.0003,$"Dumping loses soil: held={held}, deposited={Volume(data)-beforeDump}");
            // Tiny cuts cannot paint untouched terrain around the actual changed height samples.
            var point=new Vector3(6001,1.98f,6001);system.Dig(point,0.1f,0.02f);system.FlushTerrainChanges();
            var maps=data.GetAlphamaps(0,0,128,128);
            for(int z=0;z<128;z++)for(int x=0;x<128;x++)
            {
                float wx=6000+(x+0.5f)/128*8,wz=6000+(z+0.5f)/128*8;
                if(Mathf.Abs(data.GetInterpolatedHeight((wx-6000)/8,(wz-6000)/8)-2)<0.002f)
                    Require(maps[z,x,1]<0.001f,"Brown paint appears on unchanged terrain");
            }
            CheckBlade(system,owner.transform);
            CheckVisuals();
            CheckHydraulics(system,owner.transform);
            Debug.Log("EXCAVATOR PHYSICS PASS: no floor scooping, larger actual-model load, complete physical dump volume, no remote paint, blade pushing, visible animated treads and hydraulic chassis lift/release.");
        }
        finally{DigSystem.I=old;Ignition.EngineRunning=running;geometry.Dispose();UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(prefab);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    static float Bottom(BucketInside b)
    {
        float y=float.PositiveInfinity;var bounds=b.WorkBounds;
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)for(int k=-1;k<=1;k+=2)y=Mathf.Min(y,b.transform.TransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,k,z))).y);
        return y;
    }
    static void CheckBlade(DigSystem system,Transform owner)
    {
        var rig=new GameObject("Blade push rig");rig.transform.position=new Vector3(6005,2.05f,6005);
        try
        {
            var blade=new GameObject("Physical blade");blade.transform.SetParent(rig.transform,false);blade.transform.localPosition=Vector3.forward;
            blade.AddComponent<BoxCollider>().size=new Vector3(0.8f,0.2f,0.15f);var contact=blade.AddComponent<GroundedAttachment>();
            var probe=ExcavatorBody.For(rig.transform).CreateProbe(rig.transform);double before=Volume(system.terrain.terrainData);
            probe.BeginMove();rig.transform.position+=Vector3.forward*0.02f;float moved=probe.WorkSoil(0.02f);
            Require(moved>0 && contact.Depth(system.terrain)<0.012f,"Blade cannot push soil ahead of its physical edge");
            Require(Math.Abs(Volume(system.terrain.terrainData)-before)<0.0002,"Blade creates or deletes soil");
        }
        finally{UnityEngine.Object.DestroyImmediate(rig);}
    }
    static void CheckVisuals()
    {
        var root=new GameObject("Tread visuals regression");
        try
        {
            root.transform.localScale=Vector3.one*0.02f;
            var drive=root.AddComponent<TrackDrive>();var visuals=root.AddComponent<TrackVisuals>();visuals.drive=drive;
            foreach(bool right in new[]{false,true})
            {
                var track=GameObject.CreatePrimitive(PrimitiveType.Cube);track.transform.SetParent(root.transform,false);
                track.transform.localPosition=new Vector3(right?25:-25,0,0);track.transform.localScale=new Vector3(15,22,75);
                UnityEngine.Object.DestroyImmediate(track.GetComponent<Collider>());
                if(right)visuals.rightTrack=track.transform;else visuals.leftTrack=track.transform;
            }
            visuals.Initialize();Require(visuals.Left && visuals.Right,"Tread animation not built");
            Require(!visuals.leftTrack.GetComponent<MeshRenderer>().enabled && !visuals.rightTrack.GetComponent<MeshRenderer>().enabled,"Static tread mesh hides moving shoes");
            var shoes=visuals.Left.GetComponentsInChildren<MeshRenderer>();Require(shoes.Length==48,"Visible tread shoes missing");
            Vector3 start=shoes[0].transform.position;
            typeof(TrackDrive).GetProperty("LeftSpeed").SetValue(drive,0.5f);visuals.Left.Step(0.1f);
            Require(Vector3.Distance(start,shoes[0].transform.position)>0.035f,"Tread shoes do not move");
            var rightStart=visuals.Right.GetComponentsInChildren<MeshRenderer>()[0].transform.position;visuals.Right.Step(0.1f);
            Require(Vector3.Distance(rightStart,visuals.Right.GetComponentsInChildren<MeshRenderer>()[0].transform.position)<0.001f,"Stationary side tread moves");
            foreach(string level in new[]{"Level 1","Level 2"})Require(File.ReadAllText("Assets/Scenes/"+level+".unity").Contains("Assembly-CSharp::TrackVisuals"),level+" has no tread animation component");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    static void CheckHydraulics(DigSystem system,Transform owner)
    {
        var rig=new GameObject("Hydraulic reaction regression");rig.transform.position=new Vector3(6003,2.512f,6005);
        try
        {
            var g=rig.AddComponent<ExcavatorGrounding>();g.terrain=system.terrain;
            g.leftTrack=Track(rig.transform,-0.5f);g.rightTrack=Track(rig.transform,0.5f);g.Initialize(rig.transform,Vector3.forward);
            var arm=new GameObject("Arm pivot").transform;arm.SetParent(rig.transform,false);
            var blade=new GameObject("Ground contact tool");blade.transform.SetParent(arm,false);blade.transform.localPosition=new Vector3(0,-0.4f,2);
            blade.AddComponent<BoxCollider>().size=new Vector3(0.5f,0.2f,0.2f);
            var leverObject=new GameObject("Hydraulic lever");leverObject.transform.SetParent(rig.transform,false);leverObject.AddComponent<XRSimpleInteractable>();var lever=leverObject.AddComponent<LeverTilt>();lever.Forward=1;
            var driver=rig.AddComponent<PartDriver>();driver.target=arm;driver.lever=lever;driver.blockByTerrain=true;driver.limit=false;driver.Initialize();
            float y=rig.transform.position.y;
            for(int i=0;i<30;i++)driver.Step(0.02f);
            Require(rig.transform.position.y>y+0.005f && Vector3.Angle(rig.transform.up,Vector3.up)>0.5f,"Hydraulic contact freezes arm instead of loading/lifting chassis");
            Require(blade.GetComponentInParent<GroundedAttachment>().Depth(system.terrain)<0.012f,"Hydraulics force tool through terrain");
            Require(driver.HydraulicLoad>0.1f && g.HydraulicLoad>0.1f,"Hydraulic pressure is not reported");
            var sound=rig.AddComponent<ExcavatorEngineAudio>();sound.Initialize();
            for(int i=0;i<100;i++)sound.Step(true,1,0.02f);
            Require(sound.LoopSource.pitch<sound.fullPitch-0.03f && sound.LoopSource.volume>sound.fullVolume,"Engine sound does not react to hydraulic load");
            lever.Forward=-1;for(int i=0;i<70;i++){driver.Step(0.02f);g.Settle(0.02f);}
            Require(rig.transform.position.y<y+0.015f,"Machine does not settle after releasing ground pressure");
        }
        finally{UnityEngine.Object.DestroyImmediate(rig);}
    }
    static Transform Track(Transform parent,float x)
    {
        var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.SetParent(parent,false);t.localPosition=new Vector3(x,-0.25f,0);t.localScale=new Vector3(0.3f,0.5f,1.5f);UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());return t;
    }
}
