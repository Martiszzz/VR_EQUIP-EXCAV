using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity -batchmode -nographics -executeMethod IntegrationValidation.Run
public static class IntegrationValidation
{
    public static void InspectGround()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var dig = UnityEngine.Object.FindAnyObjectByType<DigSystem>();
        var p = dig.bucket.PourPosition(0);
        Physics.SyncTransforms();
        Debug.Log($"GROUND terrain={dig.terrain.name} position={dig.terrain.transform.position} surfaceAtBucket={dig.SurfaceY(p)} bucket={p}");
        foreach (var hit in Physics.RaycastAll(p + Vector3.up * 10, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore))
            Debug.Log($"GROUND HIT {hit.collider.name} {hit.collider.GetType().Name} at {hit.point}");
        EditorApplication.Exit(0);
    }
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
            CheckFracture(dig);
            CheckSoilDeposit(dig);
            CheckBucket(dig);
            CheckControls();
            CheckJoystickTilt();
            var randomState = UnityEngine.Random.state;
            try
            {
                foreach (int seed in new[] {17, 329, 4096})
                {
                    EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                    UnityEngine.Random.InitState(seed);
                    CheckBucket(UnityEngine.Object.FindAnyObjectByType<DigSystem>());
                }
            }
            finally { UnityEngine.Random.state = randomState; }
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
                    Require(Vector3.Distance(other.transform.position, position) >= dig.blockSize * 0.97f, "Soil physics bodies overlap.");
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
                Require(!block.GetComponent<Renderer>().enabled, "Individual chunk rows are still visible inside the bucket.");
                var mesh = block.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh && mesh.name.StartsWith("Earth clod") && mesh.triangles.Length > 36,
                    "Captured earth still uses the cube mesh.");
                Require(mesh.bounds.size.x <= 1 && mesh.bounds.size.y <= 1 && mesh.bounds.size.z <= 1,
                    "Earth clod protrudes outside its reserved slot.");
            }
            Require(bucket.FreeSlots(dig.blockSize) == 0 && !bucket.TryGetSpawnPosition(dig.blockSize, out _), "Full bucket permits more soil.");
            Require(bucket.Fill && bucket.Fill.IsVisible && bucket.Fill.Amount > 0.99f && bucket.Fill.SurfaceMesh.vertexCount > 100,
                "Bucket does not show its connected soil fill.");
            var distinctX = new HashSet<int>();
            foreach (var block in blocks) distinctX.Add(Mathf.RoundToInt(block.transform.localPosition.x * 1000));
            Require(distinctX.Count > 15, "Stored chunks still form regular columns.");
            var before = blocks[0].transform.position;
            rig.position += Vector3.right;
            Require(Vector3.Distance(blocks[0].transform.position, before + Vector3.right) < 0.0001f, "Soil does not follow moving bucket.");
            rig.position = originalPosition + Vector3.up * 2f;
            rig.rotation = originalRotation * Quaternion.Euler(85, 0, 0);
            Require(!bucket.IsDumping, "Bucket dumps into its back wall.");
            rig.rotation = originalRotation * Quaternion.Euler(-bucket.dumpAngle - 2f, 0, 0);
            Require(bucket.IsDumping, "Tipped bucket does not dump.");
            float shallowFlow = bucket.PourStrength;
            rig.rotation = originalRotation * Quaternion.Euler(-80, 0, 0);
            Require(bucket.PourStrength > shallowFlow, "Tipping further does not increase sand flow.");
            bucket.Step(Time.fixedDeltaTime);
            Require(bucket.CapturedCount == slots && bucket.Fill.IsVisible, "Sudden tipping deletes the whole load in one step.");
            for (int i = 0; i < 60; i++) bucket.Step(0.02f);
            int partial = bucket.CapturedCount;
            Require(partial > 0 && partial < slots && bucket.Fill.IsVisible && bucket.Fill.Amount > 0.1f && bucket.Fill.Amount < 0.99f,
                "Fill does not decrease progressively during partial pouring.");
            rig.rotation = originalRotation;
            for (int i = 0; i < 30; i++) bucket.Step(0.02f);
            Require(bucket.CapturedCount == partial && bucket.Fill.IsVisible, "Leveling the bucket does not stop pouring.");
            rig.rotation = originalRotation * Quaternion.Euler(-80, 0, 0);
            bool groupReleased = false;
            for (int i = 0; i < 1200 && bucket.CapturedCount > 0; i++)
            {
                int previous = bucket.CapturedCount;
                bucket.Step(0.02f);
                int released = previous - bucket.CapturedCount;
                Require(released <= bucket.maxBatchSize, "Pouring exceeds the bounded group size.");
                groupReleased |= released > 1;
            }
            for (int i = 0; i < 60; i++) bucket.Step(0.02f);
            Require(groupReleased && bucket.CapturedCount == 0 && !bucket.Fill.IsVisible, "Gradual group pouring never finishes.");
            foreach (var block in blocks)
            {
                Require(!block.IsHeld && !block.rb.isKinematic && block.GetComponent<SphereCollider>().enabled, "Dump did not restore physical soil.");
                Require(block.GetComponent<Renderer>().enabled && block.GetComponent<MeshFilter>().sharedMesh.name.StartsWith("Earth clod"),
                    "Earth clod changes into invisible sand immediately after leaving the bucket.");
            }
            Require(bucket.Sand && bucket.Sand.Particles, "Loose soil particles are missing.");
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
        Debug.Log("BUCKET PASS: random packing, gradual group flow, shrinking fill, pause/resume and pool reuse.");
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
    static void CheckJoystickTilt()
    {
        foreach (var stem in new[] {Vector3.forward, Vector3.up})
        {
            Require(JoystickMotion.HandAngles(stem, stem).sqrMagnitude < 0.0001f, "Neutral joystick input is not centered.");
            for (int pitch = -45; pitch <= 45; pitch += 5)
            for (int side = -45; side <= 45; side += 5)
            {
                var q = JoystickMotion.Tilt(pitch, side, stem, 25);
                Require(Mathf.Abs(Vector3.Dot(new Vector3(q.x, q.y, q.z), stem)) < 0.00001f,
                    "Joystick twists around its own stem.");
                Require(Vector3.Angle(stem, q * stem) <= 25.001f, "Joystick exceeds its tilt cone.");
            }
        }
        var right = JoystickMotion.HandAngles(new Vector3(0.1f, 0, 1), Vector3.forward);
        Require(right.y > 0 && Mathf.Abs(right.x) < 0.001f, "Z-axis joystick side input is incorrect.");
        Debug.Log("JOYSTICK PASS: native axes, centered input, no axial twist, bounded diagonal tilt.");
    }
    static void CheckFracture(DigSystem dig)
    {
        Vector3 position = dig.terrain.transform.position + dig.terrain.terrainData.size * 0.5f + Vector3.up * 3;
        var parent = dig.Spawn(position);
        float before = parent.Volume;
        parent.Hold(dig.bucket);
        Require(!parent.TryBreak(Vector3.up, 4), "Held clod fractures inside bucket.");
        parent.DropAsSoil(position, Vector3.zero);
        Require(!parent.TryBreak(Vector3.up, 0.5f), "Gentle contact shatters soil.");
        Require(parent.TryBreak(Vector3.up, 3f), "Hard impact did not split soil.");
        var fragments = dig.GetComponentsInChildren<DirtBlock>();
        Require(fragments.Length == 3, "Clod does not split into three fragments.");
        float total = 0;
        foreach (var fragment in fragments)
        {
            total += fragment.Volume;
            Require(fragment.FractureLevel == 1 && fragment.GetComponent<Renderer>().enabled, "Fragment depth/appearance is wrong.");
            Require(!fragment.TryBreak(Vector3.up, 10), "Fragments recursively multiply without a limit.");
            Require(fragment.transform.lossyScale.x < dig.blockSize, "Fragment has not become smaller.");
        }
        Require(Mathf.Abs(total - before) < before * 0.0001f, "Impact fracture creates or destroys soil volume.");
        foreach (var fragment in fragments) dig.Release(fragment);
        int limit = dig.maxBlocks;
        try
        {
            dig.maxBlocks = 1;
            parent = dig.Spawn(position);
            Require(!parent.TryBreak(Vector3.up, 5) && parent.Volume == before, "Pool exhaustion deletes or duplicates soil.");
            dig.Release(parent);
        }
        finally { dig.maxBlocks = limit; }
        Debug.Log("SOIL FRACTURE PASS: hard impacts, visible smaller clods, conserved volume and bounded fragment count.");
    }
    static void CheckSoilDeposit(DigSystem dig)
    {
        var original = dig.terrain.terrainData;
        var collider = dig.terrain.GetComponent<TerrainCollider>();
        var copy = UnityEngine.Object.Instantiate(original);
        var mode = Physics.simulationMode;
        var dropped = new List<DirtBlock>();
        dig.terrain.terrainData = copy;
        collider.terrainData = copy;
        try
        {
            var point = dig.terrain.transform.position + new Vector3(copy.size.x * 0.5f, 0, copy.size.z * 0.75f);
            point.y = dig.SurfaceY(point);
            float before = point.y;
            float expectedVolume = 0;
            for (int i = 0; i < 12; i++)
            {
                var pos = point + new Vector3((i%3-1)*0.07f, 1f+(i/6)*0.08f, ((i/3)%2-0.5f)*0.07f);
                var clod = i < 6 ? dig.Spawn(pos) : dig.Spawn(pos, dig.BlockVolume / 3f, 1);
                // Exercise settled fragments as well as intact clods, without allowing more fracture in this controlled drop.
                clod.breakSpeed = 100;
                dropped.Add(clod);
                expectedVolume += clod.Volume;
            }
            Physics.simulationMode = SimulationMode.Script;
            Physics.SyncTransforms();
            for (int i = 0; i < 500; i++)
            {
                Physics.Simulate(0.02f);
                foreach (var clod in dropped) if (clod.gameObject.activeSelf) clod.Step(0.02f);
                if (i%4 == 0) dig.FlushTerrainChanges();
            }
            dig.FlushTerrainChanges();
            Require(dropped.TrueForAll(b => !b.gameObject.activeSelf), "Resting soil never merges into the terrain.");
            Require(dig.SurfaceY(point) > before + 0.005f, "Dropped soil does not raise the landing area.");
            var remote = point + Vector3.right * 2f;
            Require(Mathf.Abs(dig.SurfaceY(remote) - before) < 0.001f, "Soil raises terrain away from the landing point.");
            Debug.Log($"DEPOSIT PASS: landing area rose {dig.SurfaceY(point)-before:F4}m from {expectedVolume:F6} cubic meters of soil.");
        }
        finally
        {
            foreach (var clod in dropped) if (clod) { clod.breakSpeed = 1.6f; dig.Release(clod); }
            Physics.simulationMode = mode;
            dig.terrain.terrainData = original;
            collider.terrainData = original;
            UnityEngine.Object.DestroyImmediate(copy);
        }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
