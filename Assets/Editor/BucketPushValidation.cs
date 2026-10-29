using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class BucketPushValidation
{
    public static void Check()
    {
        var previous = DigSystem.I;
        bool engine = Ignition.EngineRunning;
        var data = new TerrainData { heightmapResolution = 65, size = new Vector3(8, 4, 8) };
        var heights = new float[65, 65];
        for (int z = 0; z < 65; z++)
        for (int x = 0; x < 65; x++) heights[z, x] = 0.5f;
        data.SetHeights(0, 0, heights);
        var ground = Terrain.CreateTerrainGameObject(data);
        ground.transform.position = new Vector3(3000, 0, 3000);
        var rig = new GameObject("Full bucket pushing regression");
        var owner = new GameObject("Pushing soil system");
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            rig.transform.position = new Vector3(3004, 2.15f, 3002);
            var inside = new GameObject("Bucket");
            inside.transform.SetParent(rig.transform, false);
            var box = inside.AddComponent<BoxCollider>();
            box.size = Vector3.one * 0.4f; box.isTrigger = true;
            var bucket = inside.AddComponent<BucketInside>(); bucket.capacity = 1;
            var tip = new GameObject("Teeth").transform;
            tip.SetParent(inside.transform, false); tip.localPosition = new Vector3(0, -0.2f, 0.2f);
            bucket.pourPoint = tip;
            var teeth = inside.AddComponent<DigTeeth>(); teeth.tip = tip;
            var system = owner.AddComponent<DigSystem>();
            system.terrain = ground.GetComponent<Terrain>(); system.bucket = bucket;
            var rb = prefab.AddComponent<Rigidbody>();
            system.blockPrefab = prefab.AddComponent<DirtBlock>(); system.blockPrefab.rb = rb;
            DigSystem.I = system; Ignition.EngineRunning = true;
            Require(bucket.TryGetSpawnPosition(system.blockSize, out var pos), "Cannot fill test bucket");
            bucket.Capture(system.Spawn(pos));
            Require(bucket.IsFull, "Test bucket must start full");
            var left = new GameObject("Left travel lever"); left.transform.SetParent(rig.transform, false);
            left.AddComponent<XRSimpleInteractable>();
            var right = new GameObject("Right travel lever"); right.transform.SetParent(rig.transform, false);
            right.AddComponent<XRSimpleInteractable>();
            var tracks = rig.AddComponent<TrackDrive>();
            tracks.excavator = rig.transform;
            tracks.leftLever = left.AddComponent<LeverTilt>(); tracks.rightLever = right.AddComponent<LeverTilt>();
            tracks.leftLever.Forward = tracks.rightLever.Forward = 1;
            typeof(TrackDrive).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tracks, null);
            var step = typeof(TrackDrive).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            double before = Volume(data);
            Vector3 start = rig.transform.position;
            for (int i = 0; i < 80; i++) step.Invoke(tracks, null);
            system.FlushTerrainChanges();
            float travel = rig.transform.position.z - start.z;
            Require(travel > 0.65f, $"Full bucket still locks chassis movement: {travel:F4}m");
            Require(bucket.CapturedCount == 1 && system.FreeBlocks == system.maxBlocks - 1, "Pushing adds soil to a full bucket");
            Require(Math.Abs(Volume(data) - before) < 0.0002, "Pushing creates or deletes terrain volume");
            Require(bucket.GroundPenetration(system) <= 0.012f, "Pushing ghosts through the terrain");
            var after = data.GetHeights(0, 0, 65, 65);
            float maximum = 0, minimum = 1;
            foreach (float h in after) { maximum = Mathf.Max(maximum, h); minimum = Mathf.Min(minimum, h); }
            Require(maximum > 0.502f && minimum < 0.498f, "Full bucket does not move soil into a mound");
            Debug.Log($"FULL BUCKET PUSH PASS: drove {travel:F3}m, moved soil ahead, conserved volume, retained full load.");

            rig.transform.position = new Vector3(3002, 2.15f, 3002);
            var pile = rig.transform.position; pile.y = 2;
            system.Add(pile, 0.3f, 0.02f);
            Require(system.SurfaceY(pile) - (rig.transform.position.y - 0.2f) > teeth.maxPushDepth,
                "Loose pile must exceed the protected native-soil cutting depth");
            before = Volume(data); start = rig.transform.position;
            for (int i = 0; i < 30; i++) step.Invoke(tracks, null);
            Require(rig.transform.position.z > start.z + 0.1f, "A deposited mound blocks the full bucket like undisturbed ground");
            Require(Math.Abs(Volume(data) - before) < 0.0002 && bucket.CapturedCount == 1,
                "Pushing a loose mound loses soil or changes the bucket load");
            Debug.Log("LOOSE MOUND PUSH PASS: full bucket pushes deposited earth beyond native-soil depth limit without overfilling.");

            rig.transform.position = new Vector3(3006, 1.5f, 3002);
            start = rig.transform.position;
            for (int i = 0; i < 20; i++) step.Invoke(tracks, null);
            Require(Vector3.Distance(start, rig.transform.position) < 0.001f, "Deeply buried bucket tunnels through ground");
            rig.transform.position += Vector3.up;
            start = rig.transform.position;
            for (int i = 0; i < 40; i++) step.Invoke(tracks, null);
            Require(rig.transform.position.z > start.z + 0.05f, "Raised bucket stays stuck after ground contact");
            Debug.Log("PUSH LIMITS PASS: deep burial blocks movement; lifting the bucket restores driving.");
        }
        finally
        {
            DigSystem.I = previous; Ignition.EngineRunning = engine;
            UnityEngine.Object.DestroyImmediate(rig); UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(ground);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }
    static double Volume(TerrainData data)
    {
        double sum = 0;
        foreach (float h in data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution)) sum += h;
        return sum * data.size.y * data.heightmapScale.x * data.heightmapScale.z;
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
