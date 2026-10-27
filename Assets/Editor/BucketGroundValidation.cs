using System;
using UnityEngine;

public static class BucketGroundValidation
{
    public static void Check()
    {
        var previousSystem = DigSystem.I;
        bool engine = Ignition.EngineRunning;
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(8, 4, 8) };
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
        for (int x = 0; x < 33; x++) heights[z, x] = 0.5f;
        data.SetHeights(0, 0, heights);
        var terrainObject = Terrain.CreateTerrainGameObject(data);
        var rig = new GameObject("Full bucket regression rig");
        var soilPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var systemObject = new GameObject("Ground validation system");
        try
        {
            terrainObject.transform.position = new Vector3(1000, 0, 1000);
            rig.transform.position = new Vector3(1004, 2.4f, 1004);
            var arm = new GameObject("Arm").transform;
            arm.SetParent(rig.transform, false);
            var inside = new GameObject("Inside");
            inside.transform.SetParent(arm, false);
            inside.transform.localPosition = Vector3.forward;
            inside.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            inside.AddComponent<BoxCollider>().size = Vector3.one * 0.4f;
            inside.GetComponent<BoxCollider>().isTrigger = true;
            var bucket = inside.AddComponent<BucketInside>();
            bucket.capacity = 1;
            var system = systemObject.AddComponent<DigSystem>();
            system.terrain = terrainObject.GetComponent<Terrain>();
            system.bucket = bucket;
            var prefabBody = soilPrefab.AddComponent<Rigidbody>();
            system.blockPrefab = soilPrefab.AddComponent<DirtBlock>();
            system.blockPrefab.rb = prefabBody;
            DigSystem.I = system;
            Ignition.EngineRunning = true;
            var lever = rig.AddComponent<LeverTilt>();
            var driver = rig.AddComponent<PartDriver>();
            driver.lever = lever;
            driver.target = arm;
            driver.localAxis = Vector3.right;
            driver.limit = false;
            driver.speed = 90;
            driver.acceleration = driver.braking = 1000;
            driver.Initialize();

            lever.Forward = 1;
            for (int i = 0; i < 30; i++) driver.Step(0.02f);
            Require(driver.Angle > 40 && bucket.GroundPenetration(system) == 0, "Empty bucket cannot enter diggable soil");
            arm.localRotation = Quaternion.identity;
            driver.Initialize();
            Require(bucket.TryGetSpawnPosition(system.blockSize, out var position), "Test bucket cannot accept soil");
            bucket.Capture(system.Spawn(position));
            for (int i = 0; i < 30; i++) driver.Step(0.02f);
            Require(driver.Angle > 0 && driver.Angle < 40, "Full bucket does not stop its lowering joint");
            Require(bucket.GroundPenetration(system) <= 0.012f, "Full bucket passes through the terrain");

            arm.localRotation = Quaternion.AngleAxis(35, Vector3.right);
            driver.Initialize();
            float buried = bucket.GroundPenetration(system);
            lever.Forward = -1;
            for (int i = 0; i < 30; i++) driver.Step(0.02f);
            Require(bucket.GroundPenetration(system) < buried - 0.1f, "Buried full bucket cannot be lifted out");

            arm.localRotation = Quaternion.AngleAxis(35, Vector3.right);
            bucket.capacity = 2;
            system.maxBlocks = 1;
            Require(bucket.FreeSlots(system.blockSize) > 0 && bucket.GroundPenetration(system) > 0.1f,
                "An exhausted soil pool allows digging through the terrain");
            var probe = ExcavatorBody.For(rig.transform).CreateProbe(rig.transform);
            Require(probe.GroundDepth() > 0.1f, "Track movement ignores the full bucket's terrain contact");
            Debug.Log("BUCKET GROUND PASS: empty digging, full load stop, escape movement, pool limit and chassis contact.");
        }
        finally
        {
            DigSystem.I = previousSystem;
            Ignition.EngineRunning = engine;
            UnityEngine.Object.DestroyImmediate(rig);
            UnityEngine.Object.DestroyImmediate(systemObject);
            UnityEngine.Object.DestroyImmediate(soilPrefab);
            UnityEngine.Object.DestroyImmediate(terrainObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
