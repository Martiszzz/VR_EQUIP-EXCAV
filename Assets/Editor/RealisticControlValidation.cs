using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class RealisticControlValidation
{
    public static void Run()
    {
        SessionState.SetBool("RunRealisticControlValidation", true);
        WaitForCompilation();
    }
    [InitializeOnLoadMethod]
    static void WaitForCompilation()
    {
        if (!SessionState.GetBool("RunRealisticControlValidation", false)) return;
        EditorApplication.update -= RunWhenReady;
        EditorApplication.update += RunWhenReady;
    }
    static void RunWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= RunWhenReady;
        SessionState.SetBool("RunRealisticControlValidation", false);
        try
        {
            CheckControls();
            BucketGroundValidation.Check();
            CheckSoilAndBlade();
            BucketPushValidation.Check();
            RealBucketWorkValidation.Check();
            EngineAudioValidation.Check();
            TerrainDriveValidation.Check();
            ExcavatorPhysicsValidation.Check();
            AdvancedSoilValidation.Check();
            Debug.Log("REALISTIC CONTROLS PASS: restart, idle power, track lever limits, thumbstick, ground contact, extended digging and conserved pushing.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static void CheckControls()
    {
        var go = new GameObject("Controls regression");
        bool running = Ignition.EngineRunning;
        try
        {
            go.AddComponent<XRSimpleInteractable>();
            var key = go.AddComponent<Ignition>();
            Initialize(key);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                key.Step(0, true, 0.02f);
                key.Step(70, true, 0.02f);
                Require(Ignition.EngineRunning, "Engine fails to restart");
                key.Step(0, false, 1);
                key.Step(0, true, 0.02f);
                key.Step(-50, true, 0.02f);
                Require(!Ignition.EngineRunning, "Key cannot stop engine");
                key.Step(0, false, 1);
            }
            Require(Mathf.Abs(Ignition.WristTwist(Quaternion.identity, Quaternion.AngleAxis(70, Vector3.forward), Vector3.forward) - 70) < 0.001f,
                "Key does not read controller wrist rotation");
            var throttle = go.AddComponent<Throttle>();
            throttle.Value = 0;
            Ignition.EngineRunning = true;
            Require(throttle.Power >= 0.25f && throttle.Power < 0.5f, "Idle throttle disables hydraulics");
            throttle.Value = 1;
            Require(Mathf.Abs(throttle.Power - 1) < 0.001f, "Full throttle is not full power");
            Ignition.EngineRunning = false;
            Require(throttle.Power == 0, "Stopped engine still supplies hydraulic power");

            var lever = go.AddComponent<LeverTilt>();
            Initialize(lever);
            lever.forwardOnly = true;
            lever.maxAngle = 15;
            for (int i = 0; i < 100; i++) lever.StepTilt(new Vector2(40, 40), true, 0.02f);
            Require(lever.Side == 0 && Mathf.Abs(lever.transform.localRotation.y) < 0.0001f && Mathf.Abs(lever.transform.localRotation.z) < 0.0001f,
                "Travel lever rotates sideways or twists");
            Require(Quaternion.Angle(Quaternion.identity, lever.transform.localRotation) <= 15.001f, "Travel lever exceeds cab clearance");
            var thumbObject = new GameObject("Thumb regression");
            thumbObject.transform.SetParent(go.transform, false);
            var thumb = thumbObject.AddComponent<FingerStick>();
            Initialize(thumb);
            for (int i = 0; i < 100; i++) thumb.StepThumbstick(Vector2.right, true, 0.02f);
            Require(thumb.Side > 0.95f && Mathf.Abs(thumb.Forward) < 0.001f, "Held thumb joystick does not drive attachment");
            thumb.StepThumbstick(Vector2.right, false, 0.02f);
            Require(thumb.Side == 0 && thumb.Forward == 0 && thumb.transform.localRotation == Quaternion.identity,
                "Thumb joystick does not stop and center immediately after releasing main grip");
            var tip = new GameObject("Ungrasped finger poke").transform;
            tip.SetParent(thumbObject.transform, false);
            thumb.head = tip;
            thumb.extraTips = new[] { tip };
            thumb.gripLever = lever;
            var updateThumb = typeof(FingerStick).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 5; i++)
            {
                tip.localPosition = new Vector3(i * 0.005f, 0.04f, 0.01f);
                updateThumb.Invoke(thumb, null);
                Require(!(bool)typeof(FingerStick).GetField("touching", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(thumb),
                    "Direct touch is accepted without gripping the main handle");
                Require(thumb.Forward == 0 && thumb.Side == 0 && thumb.transform.localRotation == Quaternion.identity,
                    "Direct touch bypasses the main handle grip requirement");
            }
            thumb.gripLever = null;
            updateThumb.Invoke(thumb, null);
            Require(thumb.Side == 0 && thumb.Forward == 0, "Unassigned main handle enables thumb input");
            thumb.StepThumbstick(Vector2.right, true, 0.5f);
            typeof(FingerStick).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(thumb, null);
            Require(thumb.Side == 0 && thumb.Forward == 0, "Disabled thumb joystick leaves stale attachment input");
            Debug.Log("CONTROL INPUT PASS: three engine cycles, wrist rotation, idle and stopped power, single-axis travel and held thumb input.");
        }
        finally { Ignition.EngineRunning = running; UnityEngine.Object.DestroyImmediate(go); }
    }
    static void CheckSoilAndBlade()
    {
        var previous = DigSystem.I;
        bool engine = Ignition.EngineRunning;
        var data = new TerrainData { heightmapResolution = 65, size = new Vector3(8, 4, 8) };
        var heights = new float[65, 65];
        for (int z = 0; z < 65; z++)
        for (int x = 0; x < 65; x++) heights[z, x] = 0.5f;
        data.SetHeights(0, 0, heights);
        var terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.transform.position = new Vector3(2000, 0, 2000);
        var root = new GameObject("Soil regression");
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var system = root.AddComponent<DigSystem>();
            system.terrain = terrainObject.GetComponent<Terrain>();
            var bucketObject = new GameObject("Extended bucket");
            bucketObject.transform.SetParent(root.transform, false);
            bucketObject.transform.position = new Vector3(2004, 1.96f, 2004);
            bucketObject.transform.rotation = Quaternion.Euler(90, 0, 0);
            bucketObject.AddComponent<BoxCollider>().size = Vector3.one * 0.4f;
            var bucket = bucketObject.AddComponent<BucketInside>();
            system.bucket = bucket;
            var rb = prefab.AddComponent<Rigidbody>();
            system.blockPrefab = prefab.AddComponent<DirtBlock>();
            system.blockPrefab.rb = rb;
            var edge=new GameObject("Forward cutting lip").transform;edge.SetParent(bucketObject.transform,false);edge.position=bucketObject.transform.position+Vector3.right*0.2f;
            bucket.pourPoint = edge;
            var teeth = bucketObject.AddComponent<DigTeeth>();
            teeth.tip = edge;
            DigSystem.I = system;
            Ignition.EngineRunning = true;
            Require(!bucket.IsDumping, "Extended bucket at soil is treated as unloading");
            double before = Volume(data);
            var p = teeth.tip.position;
            teeth.Sweep(p - Vector3.right * 0.05f, p, 0.02f);
            double removed = before - Volume(data);
            float captured = teeth.PendingVolume;
            foreach (var clod in bucket.GetComponentsInChildren<DirtBlock>()) captured += clod.Volume;
            Require(bucket.CapturedCount > 0 && removed > 0, "Extended teeth do not scoop soil");
            Require(Math.Abs(removed - captured) < removed * 0.01, "Digging creates or deletes soil");
            bucket.capacity = bucket.CapturedCount;
            p += Vector3.forward;
            before = Volume(data);
            int count = bucket.CapturedCount;
            float pushed = system.PushSoil(p, Vector3.forward * 0.08f, 0.25f, 0.03f);
            Require(pushed > 0 && bucket.CapturedCount == count, "Full bucket cannot push a shallow soil strip");
            Require(Math.Abs(Volume(data) - before) < pushed * 0.02, "Soil pushing does not conserve terrain volume");

            var blade = new GameObject("Blade");
            blade.transform.SetParent(root.transform, false);
            blade.transform.position = new Vector3(2002, 2.4f, 2002);
            var contact = new GameObject("Blade edge");
            contact.transform.SetParent(blade.transform, false);
            contact.transform.localPosition = Vector3.forward;
            contact.AddComponent<BoxCollider>().size = new Vector3(1, 0.2f, 0.2f);
            root.AddComponent<XRSimpleInteractable>();
            var lever = root.AddComponent<LeverTilt>();
            var driver = root.AddComponent<PartDriver>();
            driver.lever = lever; driver.target = blade.transform;
            driver.blockByTerrain = true; driver.limit = false;
            driver.speed = 90; driver.acceleration = driver.braking = 1000;
            driver.Initialize();
            lever.Forward = 1;
            for (int i = 0; i < 60; i++) driver.Step(0.02f);
            Require(driver.Angle > 0 && driver.Angle < 40 && blade.GetComponent<GroundedAttachment>().Depth(system.terrain) <= 0.012f,
                "Dozer blade passes through terrain");
            Debug.Log("SOIL AND BLADE PASS: extended scoop, conserved terrain pushing and rigid blade stop.");
        }
        finally
        {
            DigSystem.I = previous; Ignition.EngineRunning = engine;
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(prefab);
            UnityEngine.Object.DestroyImmediate(terrainObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }
    static double Volume(TerrainData data)
    {
        var heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        double sum = 0;
        foreach (float height in heights) sum += height;
        return sum * data.size.y * data.heightmapScale.x * data.heightmapScale.z;
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Initialize(MonoBehaviour behaviour)
    {
        behaviour.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(behaviour, null);
    }
}
