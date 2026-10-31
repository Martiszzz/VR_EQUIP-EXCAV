using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class TerrainDriveValidation
{
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Check()
    {
        bool running = Ignition.EngineRunning;
        var data = new TerrainData { heightmapResolution = 129, size = new Vector3(16, 8, 16) };
        var ground = Terrain.CreateTerrainGameObject(data); ground.transform.position = new Vector3(5000, 0, 5000);
        var rig = new GameObject("Grounding regression");
        var body = rig.transform;
        try
        {
            var grounding = rig.AddComponent<ExcavatorGrounding>(); grounding.terrain = ground.GetComponent<Terrain>();
            grounding.leftTrack = Track(body, -0.55f); grounding.rightTrack = Track(body, 0.55f);
            Set(data, (x,z) => 2);
            body.position = new Vector3(5008, 3, 5008);
            grounding.Initialize(body, Vector3.forward);
            var drive = rig.AddComponent<TrackDrive>(); drive.excavator = body; drive.grounding = grounding; drive.blockByWorld = false;
            drive.leftLever = Lever(body); drive.rightLever = Lever(body);
            drive.leftLever.Forward = drive.rightLever.Forward = 1;
            Ignition.EngineRunning = true;
            drive.Step(0.02f);
            Require(Mathf.Abs(body.position.z - 5008) < 0.001f, "Airborne chassis can drive");
            for (int i=0;i<100;i++) grounding.Settle(0.02f);
            Require(grounding.IsGrounded && Mathf.Abs(grounding.SupportPoint(0).y - 2.012f) < 0.002f, "Tracks fail to land on flat terrain");
            Set(data, (x,z) => 2 + 0.1f*(x-8) + 0.12f*(z-8));
            for (int i=0;i<100;i++) drive.Step(0.02f);
            var normal = new Vector3(-0.1f,1,-0.12f).normalized;
            Require(Vector3.Angle(body.up, normal)<0.5f, "Chassis does not follow compound slope");
            float before=body.position.y, start=body.position.z;
            for(int i=0;i<100;i++) drive.Step(0.02f);
            Require(drive.LeftSpeed>0 && Mathf.Abs(drive.LeftSpeed-drive.RightSpeed)<0.001f,"Track chain speeds do not follow forward travel");
            Require(body.position.z-start>0.7f && body.position.y-before>0.07f, "Tracks do not climb terrain");
            drive.leftLever.Forward=drive.rightLever.Forward=0;
            for(int i=0;i<100;i++) drive.Step(0.02f);
            var pivot=grounding.Pivot; var heading=body.forward;
            drive.leftLever.Forward=1; drive.rightLever.Forward=-1;
            for(int i=0;i<100;i++) drive.Step(0.02f);
            Require(drive.LeftSpeed>0 && drive.RightSpeed<0,"Track chain speeds do not follow differential steering");
            Require(Vector3.Angle(heading,body.forward)>15 && Vector2.Distance(new Vector2(pivot.x,pivot.z),new Vector2(grounding.Pivot.x,grounding.Pivot.z))<0.025f, "Slope turn drifts away from track center");
            Require(Vector3.Angle(body.up,normal)<0.5f, "Turning loses slope alignment");
            for(int i=0;i<12;i++) Require(grounding.SupportPoint(i).y>=ground.GetComponent<Terrain>().SampleHeight(grounding.SupportPoint(i))-0.005f,"Track penetrates terrain");
            CheckObstacle(drive, grounding, body, data);
            Ignition.EngineRunning=false;
            float oldY=body.position.y;
            Set(data, (x,z)=>1);
            drive.Step(0.02f);
            Require(body.position.y<oldY && body.position.y>oldY-0.02f,"Loss of support teleports chassis");
            for(int i=0;i<200;i++) drive.Step(0.02f);
            Require(grounding.IsGrounded && Mathf.Abs(grounding.SupportPoint(0).y-1.012f)<0.005f,"Engine-off chassis hovers after excavation");
            body.SetPositionAndRotation(new Vector3(5008,1.512f,5014.5f),Quaternion.identity);
            grounding.RefreshState(); Ignition.EngineRunning=true; drive.leftLever.Forward=drive.rightLever.Forward=1;
            for(int i=0;i<300;i++) drive.Step(0.02f);
            float edge=body.position.z;
            Require(edge<5016 && edge>5014.8f,$"Terrain edge does not stop unsafe travel: {edge:F4}, grounded={grounding.IsGrounded}, position={body.position}");
            drive.leftLever.Forward=drive.rightLever.Forward=-1;
            for(int i=0;i<150;i++) drive.Step(0.02f);
            Require(body.position.z<edge-0.5f,"Cannot reverse away from terrain edge");
            Set(data,(x,z)=>1+Mathf.Max(0,z-8));
            body.SetPositionAndRotation(new Vector3(5008,1.512f,5006.5f),Quaternion.identity); grounding.RefreshState();
            drive.leftLever.Forward=drive.rightLever.Forward=1;
            for(int i=0;i<400;i++) drive.Step(0.02f);
            Require(body.position.z<5008.5f,"Drives up unsafe 45 degree slope");
            CheckBridgeAndBlade(drive,grounding,body,data);
            CheckSlew(body);
            CheckRealTracks(grounding,body,data);
            Debug.Log("TERRAIN DRIVE PASS: track mesh footprint, compound slopes, uphill drive, centered turns, falling with engine off, terrain edge, steep slope and cab axis.");
        }
        finally { Ignition.EngineRunning=running; UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(ground); UnityEngine.Object.DestroyImmediate(data); }
    }
    static void CheckBridgeAndBlade(TrackDrive drive, ExcavatorGrounding grounding, Transform body, TerrainData data)
    {
        Set(data,(x,z)=>Mathf.Abs(z-8)<0.2f?1:2);
        body.SetPositionAndRotation(new Vector3(5008,2.512f,5008),Quaternion.identity);
        drive.leftLever.Forward=drive.rightLever.Forward=0; Ignition.EngineRunning=false;
        for(int i=0;i<100;i++) drive.Step(0.02f);
        Require(grounding.IsGrounded && Mathf.Abs(body.position.y-2.512f)<0.01f,"Undercarriage sinks into a narrow gap instead of bridging it");
        Set(data,(x,z)=>2);
        body.position=new Vector3(5008,3.012f,5008);
        var blade=GameObject.CreatePrimitive(PrimitiveType.Cube); blade.transform.SetParent(body,false);
        blade.transform.localPosition=new Vector3(0,-0.9f,0.9f);blade.transform.localScale=new Vector3(1,0.2f,0.2f);
        blade.AddComponent<GroundedAttachment>();
        try
        {
            Physics.SyncTransforms();
            for(int i=0;i<100;i++) drive.Step(0.02f);
            Require(body.position.y>3 && !grounding.IsGrounded,"Planted blade penetrates terrain during chassis settling");
            blade.transform.localPosition=Vector3.up;
            for(int i=0;i<100;i++) drive.Step(0.02f);
            Require(grounding.IsGrounded && Mathf.Abs(body.position.y-2.512f)<0.01f,"Chassis cannot lower after lifting the planted blade");
        }
        finally { UnityEngine.Object.DestroyImmediate(blade); }
        Ignition.EngineRunning=true;
    }
    static void CheckObstacle(TrackDrive drive, ExcavatorGrounding grounding, Transform body, TerrainData data)
    {
        drive.leftLever.Forward=drive.rightLever.Forward=0;
        for(int i=0;i<100;i++) drive.Step(0.02f);
        Set(data,(x,z)=>2); body.SetPositionAndRotation(new Vector3(5008,2.512f,5008),Quaternion.identity);
        grounding.RefreshState(); drive.leftLever.Forward=drive.rightLever.Forward=1;
        var hull=body.gameObject.AddComponent<BoxCollider>(); hull.center=Vector3.zero; hull.size=new Vector3(1,0.6f,1);
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position=new Vector3(5008,2.9f,5009.2f); wall.transform.localScale=new Vector3(3,2,0.2f);
        try
        {
            drive.blockByWorld=true;
            typeof(TrackDrive).GetMethod("Awake",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(drive,null);
            for(int i=0;i<150;i++) drive.Step(0.02f);
            Require(Mathf.Abs(drive.LeftSpeed)+Mathf.Abs(drive.RightSpeed)<0.001f,"Blocked chain animation still reports travel");
            Require(body.position.z<5008.65f,"Chassis drives through a solid wall");
        }
        finally { UnityEngine.Object.DestroyImmediate(hull); UnityEngine.Object.DestroyImmediate(wall); }
    }
    static Transform Track(Transform parent,float x)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(parent,false);
        go.transform.localPosition=new Vector3(x,-0.25f,0); go.transform.localScale=new Vector3(0.3f,0.5f,1.5f);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); return go.transform;
    }
    static LeverTilt Lever(Transform parent)
    {
        var go=new GameObject("Travel"); go.transform.SetParent(parent,false); go.AddComponent<XRSimpleInteractable>(); return go.AddComponent<LeverTilt>();
    }
    static void Set(TerrainData data,Func<float,float,float> height)
    {
        var h=new float[129,129]; for(int z=0;z<129;z++) for(int x=0;x<129;x++) h[z,x]=height(x/8f,z/8f)/8;
        data.SetHeights(0,0,h); Physics.SyncTransforms();
    }
    static void CheckSlew(Transform body)
    {
        body.rotation=Quaternion.Euler(10,30,8);
        var basis=new GameObject("Native FBX base").transform; basis.SetParent(body,false); basis.localRotation=Quaternion.Euler(-90,0,0);
        var cab=new GameObject("Cab").transform; cab.SetParent(basis,false);
        var driver=cab.gameObject.AddComponent<PartDriver>(); driver.target=cab; driver.lever=Lever(body); driver.lever.Forward=1;
        driver.localAxis=Vector3.forward; driver.useWorldAxis=true; driver.limit=false; driver.blockByWorld=false; driver.Initialize();
        for(int i=0;i<100;i++) driver.Step(0.02f);
        Require(Vector3.Angle(cab.forward,body.up)<0.01f,"Cab swings around world up instead of undercarriage axis");
    }
    static void CheckRealTracks(ExcavatorGrounding unused,Transform unusedBody,TerrainData data)
    {
        Set(data,(x,z)=>2);
        var meshes=AssetDatabase.LoadAllAssetsAtPath("Assets/Excav.fbx").OfType<Mesh>().ToArray();
        Require(meshes.Length>0,"Missing real undercarriage mesh fixture");
        var root=new GameObject("Actual model track calibration");
        try
        {
            root.transform.position=new Vector3(5008,3,5008); root.transform.localScale=Vector3.one*0.02f;
            var basis=new GameObject("Base").transform; basis.SetParent(root.transform,false); basis.localRotation=Quaternion.Euler(-90,0,0); basis.localScale=Vector3.one*100; basis.localPosition=new Vector3(-31.499996f,19.544098f,16.683117f);
            var g=root.AddComponent<ExcavatorGrounding>(); g.terrain=unused.terrain;
            g.leftTrack=RealTrack(basis,meshes,-7294526821872198993,new Vector3(-0.039056778f,-0.19814807f,0.18027942f));
            g.rightTrack=RealTrack(basis,meshes,-1638855797795967722,new Vector3(0.43765125f,-0.19814807f,0.18027942f));
            Require(g.Initialize(root.transform,Vector3.forward),"Actual tracks cannot initialize");
            float length=Vector3.Distance(g.SupportPoint(0),g.SupportPoint(2)),width=Vector3.Distance(g.SupportPoint(0),g.SupportPoint(6));
            Require(length>0.7f && length<5 && width>0.5f && width<5,$"Invalid real track footprint {length} x {width}");
            for(int i=0;i<200;i++) g.Settle(0.02f);
            Require(g.IsGrounded,"Actual model tracks do not settle on terrain");
            for(int i=0;i<12;i++) Require(Mathf.Abs(g.SupportPoint(i).y-2.012f)<0.005f,"Actual model track clearance is incorrect");
            foreach(string level in new[]{"Level 1","Level 2"})
            {
                string scene=System.IO.File.ReadAllText("Assets/Scenes/"+level+".unity");
                Require(scene.Contains("grounding: {fileID: 2048484093}") && scene.Contains("leftTrack: {fileID: 346980961889796245}") && scene.Contains("rightTrack: {fileID: 1292206066633544386}") && scene.Contains("terrain: {fileID: 302056367}"),level+" missing terrain drive wiring");
            }
            Debug.Log($"ACTUAL TRACK FOOTPRINT {length:F3} x {width:F3}m");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    static Transform RealTrack(Transform parent,Mesh[] meshes,long id,Vector3 position)
    {
        Mesh mesh=meshes.First(m=> {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long file);return file==id;});
        var t=new GameObject("Track").transform;t.SetParent(parent,false);t.localPosition=position;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;return t;
    }
}
