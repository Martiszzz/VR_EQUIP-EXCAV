using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SoilInteractionValidation
{
    public static void Run()
    {
        try{Check();Debug.Log("SOIL INTERACTION PASS: bounded empty-bucket penetration, heaped volume, loose-body budget, reusable terrain edits, soil profiles, force threshold/rear-track pivot and both production contact setups.");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void Check()
    {
        var previous=DigSystem.I;bool engine=Ignition.EngineRunning;Ignition.EngineRunning=true;
        var data=new TerrainData{heightmapResolution=129,size=new Vector3(8,4,8)};
        var heights=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)heights[z,x]=.5f;data.SetHeights(0,0,heights);
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(8000,0,8000);
        var owner=new GameObject("Soil integration fixture");var prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.transform.position=Vector3.up*100;
        try
        {
            var soil=owner.AddComponent<DigSystem>();soil.terrain=ground.GetComponent<Terrain>();soil.simulateSlopes=false;
            soil.blockPrefab=prefab.AddComponent<DirtBlock>();soil.blockPrefab.rb=prefab.AddComponent<Rigidbody>();DigSystem.I=soil;
            var go=new GameObject("Volume-limited bucket");go.transform.SetParent(owner.transform,false);go.transform.SetPositionAndRotation(new Vector3(8004,2.15f,8004),Quaternion.Euler(-90,0,0));
            var box=go.AddComponent<BoxCollider>();box.size=Vector3.one*.4f;box.isTrigger=true;
            var bucket=go.AddComponent<BucketInside>();soil.bucket=bucket;
            var tip=new GameObject("Teeth").transform;tip.SetParent(go.transform,false);tip.localPosition=new Vector3(0,-.22f,-.2f);bucket.pourPoint=tip;
            go.AddComponent<DigTeeth>().tip=tip;
            go.transform.position-=Vector3.up*.6f;
            Require(bucket.GroundPenetration(soil,true)>.4f,"An empty bucket still bypasses deep ground contact");
            go.transform.position+=Vector3.up*3;
            while(bucket.TryGetSpawnPosition(soil.blockSize,out var position))bucket.Capture(soil.Spawn(position));
            Require(bucket.RatedFill>1.05f && bucket.RatedFill<=1+bucket.heapedFraction+.001f,"Heaped capacity is not measured in cubic metres");
            Require(bucket.Fill.SurfaceMesh.bounds.max.z>box.center.z+box.size.z*.5f,"Full soil never mounds above the rim");
            float held=bucket.HeldVolume;var oversized=soil.Spawn(go.transform.position,.1f);bucket.Capture(oversized);
            Require(!oversized.IsHeld && Mathf.Abs(bucket.HeldVolume-held)<.00001f,"Parcel count bypasses cubic-metre capacity");soil.Release(oversized);
            float total=soil.ActiveVolume;soil.maxLooseBodies=8;go.transform.rotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<200;i++){bucket.Step(.02f);Require(soil.LooseBodyCount<=8,"Dump exceeds loose-body physics budget");}
            Require(bucket.CapturedCount>0 && soil.LooseBodyCount==8 && Mathf.Abs(soil.ActiveVolume-total)<.00001f,"Backpressure deletes or duplicates bucket volume");
            var p=new Vector3(8001,2,8001);soil.Dig(p,.15f,.01f);
            int windows=soil.CachedTerrainWindows;
            for(int i=0;i<50;i++)soil.Dig(p,.15f,.001f);
            Require(soil.CachedTerrainWindows==windows,"Repeated terrain strokes allocate new edit windows");
            for(int z=-3;z<=3;z++)for(int x=-3;x<=3;x++)
            {
                var sample=p+new Vector3(x*.025f,0,z*.025f);
                Require(Mathf.Abs(soil.SurfaceY(sample)-soil.terrain.SampleHeight(sample)-soil.terrain.transform.position.y)<.002f,$"Contact cache differs: point {sample}, cached {soil.SurfaceY(sample)}, sample {soil.terrain.SampleHeight(sample)}, interpolated {data.GetInterpolatedHeight((sample.x-8000)/8,(sample.z-8000)/8)}, center {data.GetHeight(16,16)}");
            }
            // External editor/API changes must invalidate the grid used by contact sampling.
            data.SetHeights(0,0,heights);Require(Mathf.Abs(soil.SurfaceY(p)-2)<.0001f,"Contact cache ignores external heightmap changes");
            var profile=ScriptableObject.CreateInstance<SoilMaterialProfile>();profile.kind=SoilKind.Clay;profile.density=2100;profile.diggingResistance=4.5f;soil.soilMaterials=new[]{profile};
            Require(soil.PropertiesFor(SoilKind.Clay).density==2100 && soil.PropertiesFor(SoilKind.Clay).resistance==4.5f,"Editable soil profiles are not used");UnityEngine.Object.DestroyImmediate(profile);
            CheckLift(soil,owner.transform);
        }
        finally{DigSystem.I=previous;Ignition.EngineRunning=engine;UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(prefab);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
        CheckScenes();
    }
    static void CheckLift(DigSystem soil,Transform parent)
    {
        var support=new GameObject("Flat blade contact patch");support.transform.SetParent(parent,false);support.transform.position=new Vector3(8004,2.05f,8005);
        support.AddComponent<BoxCollider>().size=new Vector3(.8f,.2f,.2f);
        float contactDepth=support.AddComponent<GroundedAttachment>().Depth(soil.terrain,out var supportPoint);
        Require(contactDepth>.04f && Mathf.Abs(supportPoint.x-support.transform.position.x)<.002f && Mathf.Abs(supportPoint.z-support.transform.position.z)<.002f,"Flat blade support is biased toward one corner");
        var rig=new GameObject("Force-limited support");rig.transform.SetParent(parent,false);rig.transform.position=new Vector3(8004,2.512f,8003);
        var g=rig.AddComponent<ExcavatorGrounding>();g.terrain=soil.terrain;
        foreach(bool right in new[]{false,true})
        {
            var track=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;track.SetParent(rig.transform,false);track.localPosition=new Vector3(right?.5f:-.5f,-.25f,0);track.localScale=new Vector3(.3f,.5f,1.5f);UnityEngine.Object.DestroyImmediate(track.GetComponent<Collider>());
            if(right)g.rightTrack=track;else g.leftTrack=track;
        }
        g.Initialize(rig.transform,Vector3.forward);var mass=rig.AddComponent<ExcavatorMass>();mass.grounding=g;g.massModel=mass;mass.Step();
        var rear=g.SupportPoint(0);var front=g.SupportPoint(2);var pose=rig.transform.position;
        Require(!g.TryHydraulicReaction(g.Pivot+Vector3.forward*2,.02f,.02f,.001f) && rig.transform.position==pose,"Insufficient pressure lifts the excavator");
        Require(g.TryHydraulicReaction(g.Pivot+Vector3.forward*2,.02f,.02f,1),"Sufficient pressure cannot lift the front");
        Require(Vector3.Distance(g.SupportPoint(0),rear)<.002f && g.SupportPoint(2).y>front.y+.002f,"Hydraulic support translates all tracks instead of pitching about rear contact");
        for(int i=0;i<200;i++)g.Settle(.02f);
        Require(g.IsGrounded && Mathf.Abs(g.SupportPoint(0).y-rear.y)<.002f,"Released support does not settle onto tracks");
    }
    static void CheckScenes()
    {
        foreach(string level in new[]{"Level 1","Level 2"})
        {
            EditorSceneManager.OpenScene("Assets/Scenes/"+level+".unity");
            var soil=UnityEngine.Object.FindAnyObjectByType<DigSystem>();
            Require(soil && soil.feedback && soil.soilMaterials.Length==4 && soil.soilMaterials.All(p=>p),level+" lacks soil profiles/effects");
            Require(soil.terrain.GetComponent<TerrainCollider>().terrainData==soil.terrain.terrainData,level+" terrain collider has different terrain data");
            var contacts=UnityEngine.Object.FindAnyObjectByType<ExcavatorContacts>();Require(contacts,level+" has no collision setup");contacts.Initialize();
            Require(!contacts.GetComponent<Rigidbody>().isKinematic && contacts.GetComponent<ExcavatorGrounding>().UsesPhysicalSupport,level+" chassis lacks force-based support");
            foreach(var driver in contacts.GetComponentsInChildren<PartDriver>())
            {
                if(!driver.target || driver.useWorldAxis || !driver.limit || driver.target.GetComponent<DigTeeth>() || !driver.target.GetComponent<MeshFilter>())continue;
                var contact=driver.target.GetComponent<GroundedAttachment>();Require(contact && !contact.includeChildren,level+" arm contact missing or includes child bucket volume");
                Require(driver.target.GetComponents<Collider>().Any(c=>!c.isTrigger),level+" physical arm collider missing");
            }
            Require(soil.feedback.dustShader,level+" standalone dust shader reference missing");
            foreach(var claw in contacts.GetComponentsInChildren<Claw>())
            {
                typeof(Claw).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(claw,null);
                foreach(var jaw in claw.jaws)Require(jaw.terrainContact && jaw.contacts.Length>0,level+" referenced jaw has no physical terrain constraint");
            }
            typeof(SoilWorkFeedback).GetMethod("Start",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(soil.feedback,null);
            Require(!contacts.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.name=="Load in cubic metres"),level+" still has a capacity UI");
        }
    }
    static void Require(bool value,string message){if(!value)throw new Exception(message);}
}
