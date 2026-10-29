using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Uses Level 1's actual hierarchy, cargo box, shell colliders and teeth instead of a substitute cube.
public static class RealBucketWorkValidation
{
    public static void Check()
    {
        var geometry = new Geometry(File.ReadAllText("Assets/Scenes/Level 1.unity"));
        var oldSystem = DigSystem.I; bool engine = Ignition.EngineRunning;
        var data = new TerrainData { heightmapResolution = 129, size = new Vector3(8, 4, 8) };
        var flat = new float[129, 129];
        for (int z = 0; z < 129; z++) for (int x = 0; x < 129; x++) flat[z, x] = 0.5f;
        data.SetHeights(0, 0, flat);
        var ground = Terrain.CreateTerrainGameObject(data); ground.transform.position = new Vector3(4000, 0, 4000);
        var owner = new GameObject("Real bucket soil test");
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            geometry.AddShell("8785660220177547002");
            var root = geometry.Node("1113508163246883981");
            var joint = geometry.Node("2540124487336731028");
            var neutralJoint = joint.localRotation;
            var instrument = geometry.Node("8785660220177547002");
            var bucket = geometry.Node("1249843221").gameObject.AddComponent<BucketInside>();
            var box = bucket.GetComponent<BoxCollider>();
            bucket.pourPoint = geometry.Node("151281893"); bucket.capacity = 60;
            var teeth = instrument.gameObject.AddComponent<DigTeeth>(); teeth.tip = bucket.pourPoint;
            var system = owner.AddComponent<DigSystem>(); system.terrain = ground.GetComponent<Terrain>(); system.bucket = bucket;
            var body = prefab.AddComponent<Rigidbody>(); system.blockPrefab = prefab.AddComponent<DirtBlock>(); system.blockPrefab.rb = body;
            DigSystem.I = system; Ignition.EngineRunning = true;
            Place(root, bucket, new Vector3(4004, 1.95f, 4002));
            var leading = Vector3.ProjectOnPlane(bucket.pourPoint.position - bucket.transform.TransformPoint(box.center), Vector3.up).normalized;
            double beforeCut = Volume(data); var toothPoint = bucket.pourPoint.position;
            teeth.Sweep(toothPoint - leading * 0.05f, toothPoint, 0.02f);
            float scooped = teeth.PendingVolume;
            foreach (var clod in bucket.GetComponentsInChildren<DirtBlock>()) scooped += clod.Volume;
            Check(bucket.CapturedCount > 0 && Math.Abs(beforeCut - Volume(data) - scooped) < 0.0002,
                "Actual forward-facing teeth do not collect the excavated soil");
            Debug.Log("REAL TEETH SCOOP PASS: actual tooth edge removes terrain and collects the same volume into the bucket.");
            while (bucket.CapturedCount < 60)
            {
                Check(bucket.TryGetSpawnPosition(system.blockSize, out var p), "Actual bucket cannot hold its configured load");
                bucket.Capture(system.Spawn(p));
            }
            Check(bucket.IsFull, "Real bucket test did not reach capacity");
            var left = Lever(root, "Travel L"); var right = Lever(root, "Travel R");
            var tracks = root.gameObject.AddComponent<TrackDrive>(); tracks.excavator = root;
            tracks.leftLever = left; tracks.rightLever = right;
            tracks.forwardAxis = root.InverseTransformDirection(leading);
            Invoke(tracks, "Awake"); left.Forward = right.Forward = 1;
            double volume = Volume(data); var start = root.position;
            for (int i = 0; i < 80; i++) Invoke(tracks, "FixedUpdate");
            float travelled = Vector3.Dot(root.position - start, leading);
            Check(travelled > 0.55f, $"Actual Level 1 full bucket stalls at its protruding teeth: {travelled:F4}m");
            Check(bucket.GroundPenetration(system) < 0.012f && bucket.CapturedCount == 60, "Actual bucket penetrates ground or changes its full load");
            Check(Math.Abs(Volume(data) - volume) < 0.0003, "Actual bucket pushing loses soil volume");
            Debug.Log($"REAL BUCKET PUSH PASS: {travelled:F3}m with 60 clods, original teeth and shell, conserved volume.");

            // Pick a real joint pose whose tooth movement is steeper than a horizontal-only push.
            float sign = 1; bool found = false;
            foreach (float pose in new[] { -15f, 0f, 20f })
            {
                joint.localRotation = neutralJoint * Quaternion.AngleAxis(pose, Vector3.right);
                var rest = joint.localRotation; var p = bucket.pourPoint.position;
                foreach (float direction in new[] { -1f, 1f })
                {
                    joint.localRotation = rest * Quaternion.AngleAxis(direction * 0.2f, Vector3.right);
                    var motion = bucket.pourPoint.position - p; joint.localRotation = rest;
                    if (motion.y < 0 && Mathf.Abs(motion.y) > Vector3.ProjectOnPlane(motion, Vector3.up).magnitude)
                    { sign = direction; found = true; break; }
                }
                if (found) break;
            }
            Check(found, "No steep real-arm push pose found for regression");
            Place(root, bucket, new Vector3(4002, 1.96f, 4002));
            var input = Lever(root, "Arm input");
            var driver = root.gameObject.AddComponent<PartDriver>(); driver.lever = input; driver.target = joint;
            driver.speed = 20; driver.acceleration = driver.braking = 100; driver.limit = false; driver.Initialize();
            input.Forward = sign;
            for (int i = 0; i < 20; i++) driver.Step(0.02f);
            Check(Mathf.Abs(driver.Angle) > 0.5f, "Full bucket cannot push through a real steep arm stroke");
            Debug.Log($"REAL ARM PUSH PASS: {Mathf.Abs(driver.Angle):F2} degrees with full load during steep tooth motion.");

            foreach (var clod in bucket.GetComponentsInChildren<DirtBlock>()) system.Release(clod);
            joint.localRotation = neutralJoint;
            Place(root, bucket, new Vector3(4006, 2.02f, 4002));
            var work = WorldBounds(bucket); leading = Vector3.ProjectOnPlane(bucket.pourPoint.position - bucket.transform.TransformPoint(box.center), Vector3.up).normalized;
            var grading = -leading;
            var hole = work.center + grading * 1.1f; hole.y = 2;
            system.Dig(hole, 0.7f, 0.15f, 0.08f);
            system.Add(work.center, 0.3f, 0.02f);
            float holeBefore = system.SurfaceY(hole); volume = Volume(data);
            tracks.forwardAxis = root.InverseTransformDirection(grading);
            for (int i = 0; i < 60; i++) Invoke(tracks, "FixedUpdate");
            Check(bucket.GradingContact && bucket.CapturedCount == 0, "Back dragging a flat bucket fills it instead of grading");
            Check(system.SurfaceY(hole) > holeBefore + 0.001f, "Grading does not fill the depression ahead");
            Check(Math.Abs(Volume(data) - volume) < 0.0003, "Grading creates or deletes soil");
            Debug.Log("REAL BUCKET GRADE PASS: back dragging spreads a pile into a depression without filling the bucket or losing soil.");
        }
        finally
        {
            DigSystem.I = oldSystem; Ignition.EngineRunning = engine;
            geometry.Dispose(); UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(ground); UnityEngine.Object.DestroyImmediate(data);
        }
    }
    static LeverTilt Lever(Transform parent, string name)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.AddComponent<XRSimpleInteractable>(); return go.AddComponent<LeverTilt>(); }
    static void Invoke(MonoBehaviour obj, string method) { obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(obj, null); }
    static Bounds WorldBounds(BucketInside bucket)
    {
        var b = bucket.WorkBounds; var world = new Bounds(bucket.transform.TransformPoint(b.center), Vector3.zero);
        for (int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
            world.Encapsulate(bucket.transform.TransformPoint(b.center + Vector3.Scale(b.extents, new Vector3(x,y,z))));
        return world;
    }
    static void Place(Transform root, BucketInside bucket, Vector3 bottomCenter)
    { var b = WorldBounds(bucket); root.position += bottomCenter - new Vector3(b.center.x, b.min.y, b.center.z); Physics.SyncTransforms(); }
    static double Volume(TerrainData td)
    { double total=0; foreach(float h in td.GetHeights(0,0,td.heightmapResolution,td.heightmapResolution)) total+=h; return total*td.size.y*td.heightmapScale.x*td.heightmapScale.z; }
    static void Check(bool ok, string message) { if(!ok) throw new InvalidOperationException(message); }
    class Geometry
    {
        readonly Dictionary<string,string> blocks;
        readonly Dictionary<string,Transform> nodes = new Dictionary<string,Transform>();
        public Geometry(string source) { blocks=Regex.Matches(source,@"(?ms)^--- !u!\d+ &(\d+).*?(?=^--- !u!|\z)").Cast<Match>().ToDictionary(m=>m.Groups[1].Value,m=>m.Value); }
        public Transform Node(string id)
        {
            if(nodes.TryGetValue(id,out var node)) return node;
            var b=blocks[id]; var go=new GameObject(id); node=go.transform; nodes[id]=node;
            string parent=Regex.Match(b,@"m_Father: \{fileID: (\d+)").Groups[1].Value;
            if(parent!="0") node.SetParent(Node(parent),false);
            node.localPosition=V(b,"m_LocalPosition"); node.localScale=V(b,"m_LocalScale");
            var q=Regex.Match(b,@"m_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)");
            node.localRotation=new Quaternion(P(q.Groups[1].Value),P(q.Groups[2].Value),P(q.Groups[3].Value),P(q.Groups[4].Value));
            return node;
        }
        public void AddShell(string id)
        {
            var node=Node(id); var t=blocks[id]; string goId=Regex.Match(t,@"m_GameObject: \{fileID: (\d+)").Groups[1].Value;
            foreach(Match c in Regex.Matches(blocks[goId],@"component: \{fileID: (\d+)"))
            {
                string b=blocks[c.Groups[1].Value]; if(!b.Contains("BoxCollider:")) continue;
                var box=node.gameObject.AddComponent<BoxCollider>(); box.center=V(b,"m_Center"); box.size=V(b,"m_Size"); box.isTrigger=b.Contains("m_IsTrigger: 1");
            }
            var children=Regex.Match(t,@"(?ms)  m_Children:.*?(?=  m_Father:)").Value;
            foreach(Match c in Regex.Matches(children,@"- \{fileID: (\d+)")) AddShell(c.Groups[1].Value);
        }
        public void Dispose() { foreach(var t in nodes.Values) if(t) UnityEngine.Object.DestroyImmediate(t.gameObject); }
        static float P(string s)=>float.Parse(s,CultureInfo.InvariantCulture);
        static Vector3 V(string b,string field) { var m=Regex.Match(b,field+@": \{x: ([^,]+), y: ([^,]+), z: ([^}]+)"); return new Vector3(P(m.Groups[1].Value),P(m.Groups[2].Value),P(m.Groups[3].Value)); }
    }
}
