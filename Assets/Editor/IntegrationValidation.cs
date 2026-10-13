using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity -batchmode -nographics -executeMethod IntegrationValidation.Run
public static class IntegrationValidation
{
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + t.name);
            var dig = UnityEngine.Object.FindFirstObjectByType<DigSystem>();
            var teeth = UnityEngine.Object.FindFirstObjectByType<DigTeeth>();
            Require(dig && dig.terrain && dig.terrain.terrainData && dig.blockPrefab && dig.bucket, "DigSystem references missing.");
            Require(teeth && teeth.tip && dig.bucket.spawnPoint && dig.blockPrefab.rb, "Bucket / dirt references missing.");
            Require(dig.terrain.GetComponent<TerrainCollider>(), "Terrain collider missing.");
            foreach (var driver in UnityEngine.Object.FindObjectsByType<PartDriver>(FindObjectsSortMode.None))
                Require(driver.lever && driver.target, "PartDriver reference missing: " + driver.name);
            foreach (var tracks in UnityEngine.Object.FindObjectsByType<TrackDrive>(FindObjectsSortMode.None))
                Require(tracks.leftLever && tracks.rightLever && tracks.excavator, "TrackDrive references missing.");
            CheckTerrain(dig);
            CheckBucket(dig);
            CheckControls();
            Debug.Log("INTEGRATION PASS: scene, volume-limited digging, refill, terrain edges, bucket packing/dumping/pooling, smooth movement and joint limits.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void CheckTerrain(DigSystem dig)
    {
        var original = dig.terrain.terrainData;
        var copy = UnityEngine.Object.Instantiate(original);
        dig.terrain.terrainData = copy;
        try
        {
            var heights = copy.GetHeights(0, 0, copy.heightmapResolution, copy.heightmapResolution);
            for (int z = 0; z < heights.GetLength(0); z++)
            for (int x = 0; x < heights.GetLength(1); x++) heights[z, x] = 0.5f;
            copy.SetHeights(0, 0, heights);
            var point = dig.terrain.transform.position + copy.size * 0.5f;
            float before = dig.SurfaceY(point);
            float radius = Mathf.Max(copy.heightmapScale.x, copy.heightmapScale.z) * 2f;
            float volume = dig.Dig(point, radius, 0.03f);
            Require(volume > 0 && dig.SurfaceY(point) < before, "Dig did not lower terrain.");
            float added = dig.Add(point, radius, volume);
            Require(Mathf.Abs(added - volume) < volume * 0.002f, "Refill lost soil volume.");
            Require(Mathf.Abs(dig.SurfaceY(point) - before) < 0.001f, "Refill did not restore terrain.");
            float budget = dig.BlockVolume * 0.5f;
            float cut = dig.Dig(point, radius, 0.3f, budget);
            Require(cut > 0 && cut <= budget * 1.002f, "Dig exceeded free bucket volume.");
            point.y = dig.SurfaceY(point) - 0.01f;
            dig.Dig(point, radius, 2f, float.PositiveInfinity, point.y);
            Require(dig.SurfaceY(point) >= point.y - 0.00001f, "Dig went below the teeth.");
            var outside = dig.terrain.transform.position - Vector3.one;
            Require(dig.Dig(outside, radius, 1) == 0 && dig.Add(outside, radius, 1) == 0, "Terrain edge check failed.");
            Require(dig.Dig(point, 0, 1) == 0 && dig.Dig(point, radius, -1) == 0, "Invalid brush check failed.");
            var edge = dig.terrain.transform.position + new Vector3(0, copy.size.y * 0.5f, 0);
            Require(dig.Dig(edge, radius, 0.01f) > 0, "Brush cannot dig terrain border.");
            for (int z = 0; z < heights.GetLength(0); z++)
            for (int x = 0; x < heights.GetLength(1); x++) heights[z, x] = 1f;
            copy.SetHeights(0, 0, heights);
            Require(dig.Add(point, radius, 1) == 0, "Saturated terrain silently accepted soil.");
        }
        finally { dig.terrain.terrainData = original; UnityEngine.Object.DestroyImmediate(copy); }
    }

    static void CheckBucket(DigSystem dig)
    {
        var bucket = dig.bucket;
        var collider = bucket.GetComponent<BoxCollider>();
        var rig = bucket.transform.parent;
        var originalRotation = rig.rotation;
        var originalPosition = rig.position;
        var blocks = new List<DirtBlock>();
        int slots = bucket.FreeSlots(dig.blockSize);
        Require(slots > 0 && !bucket.IsDumping, "Bucket has no usable slots or is dumping in its initial pose.");
        try
        {
            for (int i = 0; i < slots; i++)
            {
                Require(bucket.TryGetSpawnPosition(dig.blockSize, out var position), "Missing soil slot.");
                foreach (var other in blocks)
                    Require(Vector3.Distance(other.transform.position, position) >= dig.blockSize, "Soil chunks overlap.");
                Vector3 local = collider.transform.InverseTransformPoint(position) - collider.center;
                Vector3 scale = collider.transform.lossyScale;
                Vector3 margin = new Vector3(dig.blockSize / scale.x, dig.blockSize / scale.y, dig.blockSize / scale.z) * 0.5f;
                Vector3 half = collider.size * 0.5f - margin;
                Require(Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z,
                    "Soil spawns through a bucket wall.");
                var block = dig.Spawn(position);
                Require(block, "Dirt pool rejected valid spawn.");
                bucket.Capture(block);
                blocks.Add(block);
                Require(block.IsHeld && block.rb.isKinematic && bucket.Contains(block), "Soil not retained in bucket.");
            }
            Require(bucket.FreeSlots(dig.blockSize) == 0 && !bucket.TryGetSpawnPosition(dig.blockSize, out _), "Full bucket permits more soil.");
            var before = blocks[0].transform.position;
            rig.position += Vector3.right;
            Require(Vector3.Distance(blocks[0].transform.position, before + Vector3.right) < 0.0001f, "Soil does not follow moving bucket.");
            rig.position = originalPosition + Vector3.up * 2f;
            rig.rotation = originalRotation * Quaternion.Euler(85, 0, 0);
            Require(!bucket.IsDumping, "Bucket dumps into its back wall.");
            rig.rotation = originalRotation * Quaternion.Euler(-65, 0, 0);
            Require(bucket.IsDumping, "Tipped bucket does not dump.");
            float shallowFlow = bucket.PourStrength;
            rig.rotation = originalRotation * Quaternion.Euler(-80, 0, 0);
            Require(bucket.PourStrength > shallowFlow, "Tipping further does not increase sand flow.");
            int pourSteps = Mathf.CeilToInt(slots / (bucket.dumpRate * bucket.PourStrength * Time.fixedDeltaTime)) + 5;
            for (int i = 0; i < pourSteps; i++) bucket.Step(Time.fixedDeltaTime);
            foreach (var block in blocks)
            {
                Require(!block.IsHeld && !block.rb.isKinematic && block.GetComponent<Collider>().enabled, "Dump did not restore physical soil.");
                Require(!block.GetComponent<Renderer>().enabled, "Pouring sand still shows large cubes.");
            }
            Require(bucket.Sand && bucket.Sand.Particles && bucket.Sand.PendingGrains > 0, "Sand stream is missing.");
            bucket.Sand.Step(0.05f);
            var particles = bucket.Sand.Particles;
            Require(particles.particleCount > 0 && particles.particleCount <= particles.main.maxParticles, "Sand emission budget failed.");
            Require(particles.main.simulationSpace == ParticleSystemSimulationSpace.World && particles.collision.enabled,
                "Sand does not use world gravity and surface collisions.");
            var samples = new ParticleSystem.Particle[particles.main.maxParticles];
            int sampleCount = particles.GetParticles(samples);
            Vector3 grainPosition = samples[0].position;
            rig.position += Vector3.right;
            particles.GetParticles(samples);
            Require(samples[0].position == grainPosition, "Falling sand moves with the bucket.");
            rig.position -= Vector3.right;
            var collision = particles.collision;
            collision.enabled = false;
            float initialYVelocity = samples[0].velocity.y;
            particles.Simulate(0.1f, false, false, true);
            particles.GetParticles(samples);
            Require(samples[0].velocity.y < initialYVelocity - 0.5f, "Sand grains do not accelerate under gravity.");
            collision.enabled = true;
            var simulationMode = Physics.simulationMode;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.SyncTransforms();
                for (int i = 0; i < 250; i++) Physics.Simulate(0.02f);
                int remaining = blocks.FindAll(bucket.Contains).Count;
                Require(remaining == 0, "Dumped soil stays trapped inside the bucket: " + remaining);
            }
            finally { Physics.simulationMode = simulationMode; }
            foreach (var block in blocks) { dig.Release(block); dig.Release(block); }
            Require(dig.FreeBlocks == dig.maxBlocks, "Double release corrupted dirt pool capacity.");
            var reused = dig.Spawn(originalPosition);
            Require(blocks.Contains(reused) && reused.Volume == dig.BlockVolume, "Dirt pool failed to reuse/reset a chunk.");
            Require(reused.GetComponent<Renderer>().enabled && reused.GetComponent<BoxCollider>().size == Vector3.one,
                "Pooled sand does not restore its captured appearance/collider.");
            dig.Release(reused);
        }
        finally
        {
            rig.SetPositionAndRotation(originalPosition, originalRotation);
            foreach (var block in blocks) if (block) UnityEngine.Object.DestroyImmediate(block.gameObject);
        }
        Debug.Log("BUCKET PASS: " + slots + " slots, gradual sand flow, grain gravity, collisions and pool reuse.");
    }

    static void CheckControls()
    {
        foreach (var driver in UnityEngine.Object.FindObjectsByType<PartDriver>(FindObjectsSortMode.None))
        {
            bool engine = Ignition.EngineRunning;
            var rotation = driver.target.localRotation;
            float forward = driver.lever.Forward, side = driver.lever.Side;
            try
            {
                Ignition.EngineRunning = true;
                driver.Initialize();
                driver.lever.Forward = driver.lever.Side = 1;
                driver.Step(0.02f);
                Require(Mathf.Abs(driver.AngularVelocity) <= driver.acceleration * 0.02f + 0.001f, "Joint snaps to full speed.");
                for (int i = 0; i < 1000; i++) driver.Step(0.02f);
                if (driver.limit) Require(driver.Angle <= driver.maxAngle + 0.001f && driver.Angle >= driver.minAngle - 0.001f, "Joint exceeded upper limit.");
                driver.lever.Forward = driver.lever.Side = -1;
                for (int i = 0; i < 1000; i++) driver.Step(0.02f);
                if (driver.limit) Require(driver.Angle <= driver.maxAngle + 0.001f && driver.Angle >= driver.minAngle - 0.001f, "Joint exceeded lower limit.");
                Ignition.EngineRunning = false;
                for (int i = 0; i < 200; i++) driver.Step(0.02f);
                Require(Mathf.Abs(driver.AngularVelocity) < 0.001f, "Stopped engine leaves joint moving.");
            }
            finally
            {
                Ignition.EngineRunning = engine;
                driver.target.localRotation = rotation;
                driver.lever.Forward = forward; driver.lever.Side = side;
            }
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
