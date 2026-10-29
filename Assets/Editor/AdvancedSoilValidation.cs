using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Behavior checks use slopes, rolls, soil volumes and simultaneous inputs, not copied implementation values.
public static class AdvancedSoilValidation
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    static double Volume(TerrainData d){double sum=0;foreach(float h in d.GetHeights(0,0,d.heightmapResolution,d.heightmapResolution))sum+=h;return sum*d.size.y*d.heightmapScale.x*d.heightmapScale.z;}
    public static void Check()
    {
        var old=DigSystem.I;bool running=Ignition.EngineRunning;Ignition.EngineRunning=true;
        try {TeethAndMaterials();SlidingAndCollapse();BucketLoad();HydraulicsAndMass();TractionAndBlade();ProductionMass();CheckScenes();}
        finally {DigSystem.I=old;Ignition.EngineRunning=running;}
        Debug.Log("ADVANCED SOIL PASS: full-width teeth and roll, distinct materials, conserved landslides, shifting/overflowing load, shared pump and leverage, mass/blade support, traction/ruts and blade windrow side flow.");
    }
    sealed class World:IDisposable
    {
        public TerrainData data;public GameObject ground,owner,prefab,rig;public DigSystem soil;public ExcavatorGrounding grounding;public TrackDrive drive;
        public World()
        {
            data=new TerrainData {heightmapResolution=129,size=new Vector3(8,4,8)};Set((x,z)=>2);
            ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(7000,0,7000);
            owner=new GameObject("Advanced soil test");soil=owner.AddComponent<DigSystem>();soil.terrain=ground.GetComponent<Terrain>();soil.maxBlocks=700;
            prefab=GameObject.CreatePrimitive(PrimitiveType.Cube);prefab.transform.position=Vector3.up*100;
            soil.blockPrefab=prefab.AddComponent<DirtBlock>();soil.blockPrefab.rb=prefab.AddComponent<Rigidbody>();
            DigSystem.I=soil;
            rig=new GameObject("Advanced tracked rig");rig.transform.position=new Vector3(7004,2.512f,7003);
            grounding=rig.AddComponent<ExcavatorGrounding>();grounding.terrain=soil.terrain;
            grounding.leftTrack=Track(-0.5f);grounding.rightTrack=Track(0.5f);grounding.Initialize(rig.transform,Vector3.forward);
            drive=rig.AddComponent<TrackDrive>();drive.excavator=rig.transform;drive.grounding=grounding;drive.blockByWorld=false;
            drive.leftLever=Lever(rig.transform);drive.rightLever=Lever(rig.transform);
        }
        public void Set(Func<float,float,float> height)
        {var h=new float[129,129];for(int z=0;z<129;z++)for(int x=0;x<129;x++)h[z,x]=height(x/16f,z/16f)/4;data.SetHeights(0,0,h);}
        Transform Track(float x)
        {var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.SetParent(rig.transform,false);t.localPosition=new Vector3(x,-0.25f,0);t.localScale=new Vector3(0.3f,0.5f,1.5f);UnityEngine.Object.DestroyImmediate(t.GetComponent<Collider>());return t;}
        public SoilRegion Region(SoilKind kind)
        {var go=new GameObject("Explicit soil region");go.transform.SetParent(owner.transform,false);go.transform.position=new Vector3(7004,2,7004);var region=go.AddComponent<SoilRegion>();region.kind=kind;region.localBounds=new Bounds(Vector3.zero,Vector3.one*20);region.Register();return region;}
        public BucketInside Bucket()
        {
            var go=new GameObject("Toothed bucket");go.transform.SetParent(rig.transform,false);go.transform.position=new Vector3(7004,2.15f,7004);go.transform.rotation=Quaternion.Euler(-90,0,0);
            var box=go.AddComponent<BoxCollider>();box.size=new Vector3(0.6f,0.4f,0.4f);box.isTrigger=true;
            var bucket=go.AddComponent<BucketInside>();bucket.capacity=180;bucket.maxPourRate=120;
            var tip=new GameObject("Cutting lip").transform;tip.SetParent(go.transform,false);tip.localPosition=new Vector3(0,-0.22f,-0.2f);bucket.pourPoint=tip;
            var teeth=go.AddComponent<DigTeeth>();teeth.tip=tip;soil.bucket=bucket;return bucket;
        }
        public void Fill(BucketInside bucket,SoilKind kind,float fraction=1)
        {
            int total=bucket.FreeSlots(soil.blockSize),count=Mathf.FloorToInt(total*fraction);
            for(int i=0;i<count;i++){Require(bucket.TryGetSpawnPosition(soil.blockSize,out var p),"Load fixture cannot pack its cargo");bucket.Capture(soil.Spawn(p,kind:kind));}
        }
        public void Dispose(){UnityEngine.Object.DestroyImmediate(rig);UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(prefab);UnityEngine.Object.DestroyImmediate(ground);UnityEngine.Object.DestroyImmediate(data);}
    }
    static LeverTilt Lever(Transform parent)
    {var go=new GameObject("Input");go.transform.SetParent(parent,false);go.AddComponent<XRSimpleInteractable>();return go.AddComponent<LeverTilt>();}
    static void TeethAndMaterials()
    {
        using(var w=new World())
        {
            var bucket=w.Bucket();var teeth=bucket.GetComponent<DigTeeth>();
            bucket.transform.rotation=Quaternion.AngleAxis(20,Vector3.forward)*Quaternion.Euler(-90,0,0);
            bucket.transform.position+=Vector3.up*(1.98f-teeth.tip.position.y);
            var left=teeth.ToothPosition(0);var right=teeth.ToothPosition(teeth.toothCount-1);
            double before=Volume(w.data);teeth.Sweep(teeth.tip.position-Vector3.forward*0.07f,teeth.tip.position,0.02f);
            float removed=teeth.PendingVolume+bucket.HeldVolume;
            Require(removed>0 && Math.Abs(before-Volume(w.data)-removed)<0.0001,"Tooth width cutting loses soil volume");
            Require(w.soil.SurfaceY(left)<1.998f && w.soil.SurfaceY(right)>1.999f,"Rolled bucket does not cut first with its lower teeth");
            bucket.transform.rotation=Quaternion.Euler(-90,0,0);bucket.transform.position=new Vector3(7004,2.15f,7004);
            var region=w.Region(SoilKind.Sand);w.Set((x,z)=>2);
            teeth.Sweep(teeth.tip.position-Vector3.forward*0.08f,teeth.tip.position,0.02f);float sand=teeth.LastWorkVolume;
            region.kind=SoilKind.Compacted;w.Set((x,z)=>2);
            teeth.Sweep(teeth.tip.position-Vector3.forward*0.08f,teeth.tip.position,0.02f);float hard=teeth.LastWorkVolume;
            Require(sand>hard*2 && hard>0,$"Soil resistance is indistinguishable: sand {sand}, compacted {hard}");
            UnityEngine.Object.DestroyImmediate(region.gameObject);
            Require(w.soil.KindAt(new Vector3(7004,1.65f,7004))==SoilKind.Clay && w.soil.KindAt(new Vector3(7004,1.1f,7004))==SoilKind.Compacted,"Native soil depth layers missing");
        }
        Debug.Log("TOOTH/MATERIAL PASS: lower edge cuts first, volume retained, hard soil resists and depth layers differ.");
    }
    static void SlidingAndCollapse()
    {
        float sandDrop=Slide(SoilKind.Sand),clayDrop=Slide(SoilKind.Clay);
        Require(sandDrop>clayDrop+0.005f,"Cohesive clay slides like loose sand");
        using(var w=new World())
        {
            var p=new Vector3(7004,2,7004);w.soil.Dig(p,0.22f,0.35f);double before=Volume(w.data);float rim=w.soil.SurfaceY(p+Vector3.right*0.25f);
            for(int i=0;i<120;i++)w.soil.StepSoil(0.08f);
            Require(w.soil.SurfaceY(p+Vector3.right*0.25f)<rim-0.003f,$"Unstable excavation rim does not slump: before {rim}, after {w.soil.SurfaceY(p+Vector3.right*0.25f)}");
            Require(Math.Abs(Volume(w.data)-before)<0.0002,"Collapsing ground creates or deletes soil");
            Require(Mathf.Abs(w.soil.SurfaceY(new Vector3(7000.5f,2,7000.5f))-2)<0.001f,"Local slide changes distant terrain");
        }
        Debug.Log("LANDSLIDE PASS: material-dependent slumping and hole-edge collapse conserve soil and stay local.");
    }
    static float Slide(SoilKind kind)
    {
        using(var w=new World())
        {
            w.Region(kind);var p=new Vector3(7004,2,7004);w.soil.Add(p,0.2f,0.04f);double before=Volume(w.data);float peak=w.soil.SurfaceY(p);
            for(int i=0;i<60;i++)w.soil.StepSoil(0.08f);
            Require(Math.Abs(Volume(w.data)-before)<0.0002,"Slumping mound loses soil volume");return peak-w.soil.SurfaceY(p);
        }
    }
    static void BucketLoad()
    {
        int sandLeft=Spill(SoilKind.Sand),clayLeft=Spill(SoilKind.Clay);
        Require(clayLeft>sandLeft,"Sticky clay is not retained longer in a tilted bucket");
        Debug.Log("BUCKET LOAD PASS: mass shifts, partial side spillage and adhesion retain exact parcel volume.");
    }
    static int Spill(SoilKind kind)
    {
        using(var w=new World())
        {
            var bucket=w.Bucket();bucket.transform.position+=Vector3.up*3;w.Fill(bucket,kind);int full=bucket.CapturedCount;
            float volume=w.soil.ActiveVolume;bucket.Step(0.02f);
            var mesh=bucket.Fill.SurfaceMesh;double meshVolume=MeshVolume(mesh)*Mathf.Abs(bucket.transform.lossyScale.x*bucket.transform.lossyScale.y*bucket.transform.lossyScale.z);
            Require(Math.Abs(meshVolume-bucket.HeldVolume)<bucket.HeldVolume*0.06,$"Visible fill does not match cargo volume {meshVolume}/{bucket.HeldVolume}");
            bucket.transform.rotation=Quaternion.AngleAxis(40,Vector3.forward)*Quaternion.Euler(-90,0,0);
            for(int i=0;i<150;i++)bucket.Step(0.02f);
            Require(bucket.CapturedCount>0 && bucket.CapturedCount<full,"Moderate tilt empties all soil or spills none");
            Require(bucket.LoadSlope.magnitude>0.05f && (kind==SoilKind.Clay || bucket.LoadOffset.magnitude>0.02f),"Cargo does not redistribute with gravity");
            Require(Mathf.Abs(w.soil.ActiveVolume-volume)<0.00001f,"Side spill duplicates or loses dirt");
            int kept=bucket.CapturedCount;
            var extra=w.soil.Spawn(bucket.transform.position,kind:kind);bucket.capacity=kept;bucket.Capture(extra);
            Require(!extra.IsHeld,"Overfilled cargo cannot escape over the rim");
            return kept;
        }
    }
    static double MeshVolume(Mesh mesh)
    {double v=0;var p=mesh.vertices;var t=mesh.triangles;for(int i=0;i<t.Length;i+=3)v+=Vector3.Dot(p[t[i]],Vector3.Cross(p[t[i+1]],p[t[i+2]]))/6;return Math.Abs(v);}
    static void HydraulicsAndMass()
    {
        using(var w=new World())
        {
            var circuit=w.rig.AddComponent<ExcavatorHydraulics>();var drivers=new PartDriver[3];
            for(int i=0;i<3;i++)
            {
                var joint=new GameObject("Hydraulic joint").transform;joint.SetParent(w.rig.transform,false);joint.localPosition=new Vector3(0,1,i);
                var d=joint.gameObject.AddComponent<PartDriver>();d.target=joint;d.lever=Lever(w.rig.transform);d.blockByWorld=false;d.limit=false;d.speed=30;d.acceleration=d.braking=1000;d.Initialize();drivers[i]=d;
            }
            drivers[0].lever.Forward=1;circuit.Refresh();circuit.Step();drivers[0].Step(0.1f);float single=drivers[0].Angle;
            foreach(var d in drivers){d.lever.Forward=1;d.Initialize();}circuit.Step();drivers[0].Step(0.1f);
            Require(drivers[0].Angle<single*0.7f && circuit.Demand>=3,"Simultaneous valves do not share pump output");
            float near=circuit.ForceFactor(drivers[0].target,4);drivers[0].target.position+=Vector3.forward*4;
            Require(circuit.ForceFactor(drivers[0].target,4)<near,"Extended arm has unlimited hydraulic leverage");
            var bucket=w.Bucket();var mass=w.rig.AddComponent<ExcavatorMass>();mass.grounding=w.grounding;mass.baseMass=700;mass.bucket=bucket;
            mass.boomPivot=drivers[0].target;mass.stickPivot=drivers[1].target;mass.toolPivot=drivers[2].target;
            mass.boomPivot.localPosition=new Vector3(0,0.5f,0);mass.stickPivot.localPosition=new Vector3(0,1,0.5f);mass.toolPivot.localPosition=new Vector3(0,0.7f,0.8f);
            bucket.transform.SetParent(mass.toolPivot,false);bucket.transform.localPosition=Vector3.zero;bucket.transform.localRotation=Quaternion.Euler(-90,0,0);
            w.grounding.massModel=mass;mass.Step();float retracted=mass.CenterOfMass.z;
            mass.stickPivot.localPosition=new Vector3(0,1,2.5f);mass.toolPivot.localPosition=new Vector3(0,0.7f,3.5f);w.Fill(bucket,SoilKind.Loam);mass.Step();w.grounding.RefreshState();
            float margin=mass.Stability;
            Require(mass.CenterOfMass.z>retracted+0.25f && mass.TotalMass>1000,"Extended loaded tool does not move center of mass");
            Require(mass.PressureAt(w.grounding.SupportPoint(2))>mass.PressureAt(w.grounding.SupportPoint(0)),"Front/rear ground pressure ignores extended load");
            var blade=new GameObject("Stabilizing blade").transform;blade.SetParent(w.rig.transform,false);blade.localPosition=new Vector3(0,-0.45f,1.3f);blade.gameObject.AddComponent<BoxCollider>().size=new Vector3(1,0.1f,0.2f);blade.gameObject.AddComponent<GroundedAttachment>();mass.blade=blade;
            mass.Step();w.grounding.RefreshState();
            Require(mass.BladeSupported && mass.Stability>margin && w.grounding.IsGrounded,"Planted blade does not extend stability support");
        }
        Debug.Log("PUMP/MASS PASS: shared flow, limited extended-arm force, moving center of mass, pressure transfer and stabilizing blade.");
    }
    static void TractionAndBlade()
    {
        using(var w=new World())
        {
            w.Set((x,z)=>2+(z-3)*0.2f);w.soil.makeTrackRuts=false;var region=w.Region(SoilKind.Sand);
            w.rig.transform.position=new Vector3(7004,2.512f,7003);for(int i=0;i<100;i++)w.grounding.Settle(0.02f);
            w.drive.leftLever.Forward=w.drive.rightLever.Forward=1;
            for(int i=0;i<100;i++)w.drive.Step(0.02f);float sand=w.drive.LeftSlip;
            region.kind=SoilKind.Compacted;for(int i=0;i<100;i++)w.drive.Step(0.02f);
            Require(sand>w.drive.LeftSlip+0.03f,"Track traction ignores surface material");
            w.soil.makeTrackRuts=true;region.kind=SoilKind.Sand;w.Set((x,z)=>2);var point=new Vector3(7003,2,7003);double before=Volume(w.data);
            for(int i=0;i<50;i++)w.soil.DisplaceTrack(point,Vector3.forward,0.28f,0.1f,0.5f,1.5f,0.02f);
            Require(w.soil.SurfaceY(point)<1.995f && w.soil.SurfaceY(point)>1.95f,"Soft track rut is missing or infinitely deep");
            Require(Math.Abs(Volume(w.data)-before)<0.0002,"Track ruts delete soil instead of displacing it");
            w.Set((x,z)=>2);
            var blade=new GameObject("Windrow blade");blade.transform.SetParent(w.owner.transform,false);blade.transform.position=new Vector3(7004,2.02f,7004);blade.AddComponent<BoxCollider>().size=new Vector3(1,0.2f,0.15f);var contact=blade.AddComponent<GroundedAttachment>();
            before=Volume(w.data);float moved=w.soil.PushAttachmentSoil(contact,Vector3.forward*0.04f);
            Require(moved>0 && w.soil.LastBladeLoad>0,"Blade windrow does not load the machine");
            Require(w.soil.SurfaceY(new Vector3(7003.28f,2,7004.42f))>2.001f && w.soil.SurfaceY(new Vector3(7004.72f,2,7004.42f))>2.001f,"Blade does not spill material around both ends");
            Require(Math.Abs(Volume(w.data)-before)<0.0002,"Blade side flow loses soil volume");
        }
        Debug.Log("TRACTION/BLADE PASS: material-dependent slip, bounded conserved ruts, local windrow and side escape.");
    }
    static void ProductionMass()
    {
        foreach(string level in new[]{"Level 1","Level 2"})
        using(var w=new World())
        {
            var geometry=new RealBucketWorkValidation.Geometry(File.ReadAllText("Assets/Scenes/"+level+".unity"));
            try
            {
                var root=geometry.Node("1113508163246883981");root.position=new Vector3(7004,3,7003);
                var meshes=UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Excav.fbx").OfType<Mesh>().ToArray();
                var g=root.gameObject.AddComponent<ExcavatorGrounding>();g.terrain=w.soil.terrain;
                if(level=="Level 1")
                {
                    var mesh=UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/TrackChainPath.fbx").OfType<Mesh>().First(m=>{UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==-5495902117074765545L;});
                    var template=geometry.Node("1810314630");var shoe=geometry.Node("3849000088759553841");shoe.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;shoe.gameObject.AddComponent<MeshRenderer>().sharedMaterial=SoilClodVisual.EarthMaterial;
                    foreach(var pair in new[]{("1645427157",false),("868185390",true)})
                    {
                        var track=geometry.Node(pair.Item1);var chain=track.gameObject.AddComponent<TrackChain>();chain.rightSide=pair.Item2;chain.linkPrefab=template;
                        string source=File.ReadAllText("Assets/Scenes/"+level+".unity");string component=System.Text.RegularExpressions.Regex.Match(source,@"(?ms)^--- !u!114 &"+(pair.Item2?"868185391":"1645427158")+@"\r?\n.*?(?=^--- !u!|\z)").Value;
                        string paths=component.Substring(component.IndexOf("  path:"));paths=paths.Substring(0,paths.IndexOf("  linkPrefab:"));
                        chain.path=System.Text.RegularExpressions.Regex.Matches(paths,@"fileID: (\d+)").Cast<System.Text.RegularExpressions.Match>().Select(m=>geometry.Node(m.Groups[1].Value)).ToArray();
                        if(pair.Item2)g.rightTrack=track;else g.leftTrack=track;
                    }
                    var drive=root.gameObject.AddComponent<TrackDrive>();drive.excavator=root;
                    var visuals=root.gameObject.AddComponent<TrackVisuals>();visuals.drive=drive;visuals.leftTrack=g.leftTrack;visuals.rightTrack=g.rightTrack;visuals.Initialize();
                    Require(visuals.Left && visuals.Right && root.GetComponentsInChildren<TrackChain>().Length==2,"Authored tread chains are missing or duplicated");
                }
                else foreach(var pair in new[]{("346980961889796245",-7294526821872198993L),("1292206066633544386",-1638855797795967722L)})
                {
                    var track=geometry.Node(pair.Item1);var mesh=meshes.First(m=>{UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m,out string guid,out long id);return id==pair.Item2;});track.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                    if(pair.Item1=="346980961889796245")g.leftTrack=track;else g.rightTrack=track;
                }
                var mass=root.gameObject.AddComponent<ExcavatorMass>();mass.grounding=g;g.massModel=mass;
                mass.boomPivot=geometry.Node("2540124487336731028");mass.stickPivot=geometry.Node(level=="Level 1"?"5701651948974088746":"8101384908800261122");mass.toolPivot=geometry.Node(level=="Level 1"?"8785660220177547002":"3277301492896211551");
                g.Initialize(root,Vector3.forward);
                for(int i=0;i<200;i++){mass.Step();g.Settle(0.02f);}
                Require(g.IsGrounded && mass.Stability>0.2f,level+" real-model mass distribution destabilizes its parked tracks");
                if(level=="Level 1")
                {
                    geometry.AddShell("8785660220177547002");var bucket=geometry.Node("1249843221").gameObject.AddComponent<BucketInside>();bucket.pourPoint=geometry.Node("151281893");bucket.capacity=180;w.soil.bucket=bucket;mass.bucket=bucket;
                    w.Fill(bucket,SoilKind.Loam);mass.Step();g.RefreshState();
                    Require(g.IsGrounded && mass.TotalMass>2000,"Actual loaded machine loses valid ground support");
                }
            }
            finally{geometry.Dispose();}
        }
        Debug.Log("PRODUCTION MASS PASS: both scenes' original track meshes and actual arm transforms support the parked/full machine.");
    }
    static void CheckScenes()
    {
        foreach(string level in new[]{"Level 1","Level 2"})
        {
            string s=File.ReadAllText("Assets/Scenes/"+level+".unity");
            Require(s.Contains("Assembly-CSharp::ExcavatorMass") && s.Contains("Assembly-CSharp::ExcavatorHydraulics"),level+" lacks mass/pump integration");
        }
    }
}
