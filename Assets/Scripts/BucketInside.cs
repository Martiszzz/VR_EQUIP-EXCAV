using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BucketInside : MonoBehaviour
{
    public Transform spawnPoint, pourPoint;
    [Min(1)] public int capacity = 60;
    public Vector3 openingAxis = Vector3.forward, lipAxis = Vector3.up;
    [Range(30f, 150f)] public float dumpAngle = 42f;
    [Range(0f, 0.4f)] public float finesFraction = 0.12f;
    [Min(0f)] public float wallPadding = 0.015f;
    [Min(2f)] public float maxPourRate = 70f;
    [Min(1f)] public float flowAcceleration = 200f;
    [Range(0.06f, 0.2f)] public float batchInterval = 0.08f;
    [Range(2, 8)] public int maxBatchSize = 6;
    readonly List<DirtBlock> captured = new List<DirtBlock>();
    BoxCollider volume;
    Vector3 lastPosition, velocity;
    Quaternion lastRotation;
    bool motionInitialized;
    float flowRate, releaseCredit, flowClock;
    float chunkSize = 0.06f;
    SandFlow sand;
    BucketSoilFill fill;
    public SandFlow Sand => sand;
    public BucketSoilFill Fill => fill;
    public int CapturedCount => captured.Count;
    public float FillFraction => Mathf.Clamp01(captured.Count / (float)Mathf.Max(1, SlotCapacity(chunkSize)));
    public float PourStrength => !IsDumping ? 0 : Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(
        Mathf.Cos(dumpAngle * Mathf.Deg2Rad), Mathf.Cos((dumpAngle + 20f) * Mathf.Deg2Rad),
        Vector3.Dot(transform.TransformDirection(openingAxis.normalized), Vector3.up)));
    public bool IsDumping
    {
        get
        {
            float up = Vector3.Dot(transform.TransformDirection(openingAxis.normalized), Vector3.up);
            float lipDown = Vector3.Dot(transform.TransformDirection(lipAxis.normalized), Vector3.down);
            return up < Mathf.Cos(dumpAngle * Mathf.Deg2Rad) && (lipDown > 0.5f || up < -0.2f);
        }
    }
    public bool IsFull => FreeSlots(chunkSize) == 0;
    public float GroundPenetration(DigSystem system)
    {
        if (!system || system.bucket != this || !system.terrain ||
            (FreeSlots(system.blockSize) > 0 && system.CanSpawn && !IsDumping && Ignition.EngineRunning)) return 0;
        if (!volume) volume = GetComponent<BoxCollider>();
        float depth = 0;
        // Sample the oriented bucket volume, including the floor, sides and lip.
        // This also reads newly dug/deposited heights before the terrain collider syncs.
        for (int x = -2; x <= 2; x++)
        for (int y = -2; y <= 2; y++)
        for (int z = -2; z <= 2; z++)
        {
            if (Mathf.Abs(x) < 2 && Mathf.Abs(y) < 2 && Mathf.Abs(z) < 2) continue;
            var p = transform.TransformPoint(volume.center + Vector3.Scale(volume.size * 0.25f, new Vector3(x, y, z)));
            if (system.ContainsTerrainPoint(p)) depth = Mathf.Max(depth, system.SurfaceY(p) - p.y);
        }
        if (pourPoint && system.ContainsTerrainPoint(pourPoint.position))
            depth = Mathf.Max(depth, system.SurfaceY(pourPoint.position) - pourPoint.position.y);
        return depth;
    }
    void Awake()
    {
        volume = GetComponent<BoxCollider>(); lastPosition = transform.position;
        lastRotation = transform.rotation; motionInitialized = true;
    }
    void GetBounds(out Vector3 low, out Vector3 high)
    {
        if (!volume) volume = GetComponent<BoxCollider>();
        var scale = transform.lossyScale;
        Vector3 inset = new Vector3(wallPadding / Mathf.Abs(scale.x), wallPadding / Mathf.Abs(scale.y), wallPadding / Mathf.Abs(scale.z));
        low = volume.center - volume.size * 0.5f + inset;
        high = volume.center + volume.size * 0.5f - inset;
    }
    int SlotCapacity(float size)
    {
        GetBounds(out var low, out var high);
        var s = transform.lossyScale;
        Vector3 world = Vector3.Scale(high - low, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)));
        float spacing = Mathf.Max(0.001f, size) * 1.08f;
        return Mathf.Min(capacity, Mathf.Max(0, Mathf.FloorToInt(world.x / spacing)) *
            Mathf.Max(0, Mathf.FloorToInt(world.y / spacing)) * Mathf.Max(0, Mathf.FloorToInt(world.z / spacing)));
    }
    public int FreeSlots(float size)
    {
        chunkSize = size;
        captured.RemoveAll(b => !b || !b.gameObject.activeSelf);
        return Mathf.Max(0, SlotCapacity(size) - captured.Count);
    }
    public bool Contains(DirtBlock block)
    {
        if (!block) return false;
        if (captured.Contains(block)) return true;
        if (!volume) volume = GetComponent<BoxCollider>();
        var p = transform.InverseTransformPoint(block.transform.position) - volume.center;
        var half = volume.size * 0.5f;
        return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
    }
    public bool TryGetSpawnPosition(float size, out Vector3 position)
    {
        position = default;
        if (IsDumping || FreeSlots(size) == 0) return false;
        GetBounds(out var low, out var high);
        var s = transform.lossyScale;
        var radius = new Vector3(size / Mathf.Abs(s.x), size / Mathf.Abs(s.y), size / Mathf.Abs(s.z)) * 0.5f;
        float bottom = low.z;
        low += radius; high -= radius;
        if (high.x < low.x || high.y < low.y || high.z < low.z) return false;
        var occupied = new Vector3[captured.Count];
        for (int i = 0; i < captured.Count; i++) occupied[i] = captured[i].transform.position;
        float spacingSquared = size * size * 0.98f * 0.98f;
        // Random packing, growing upwards from the floor. Expand the search if a lower layer is packed.
        float filled = (captured.Count + 1f) / Mathf.Max(1, SlotCapacity(size));
        float top = Mathf.Clamp(bottom + (high.z + radius.z - bottom) * filled - radius.z, low.z, high.z);
        for (int pass = 0; pass < 2; pass++)
        for (int attempt = 0; attempt < 256; attempt++)
        {
            var local = new Vector3(Random.Range(low.x, high.x), Random.Range(low.y, high.y), Random.Range(low.z, pass == 0 ? top : high.z));
            var candidate = transform.TransformPoint(local);
            bool free = true;
            foreach (var other in occupied) if ((candidate - other).sqrMagnitude < spacingSquared) { free = false; break; }
            if (free) { position = candidate; return true; }
        }
        return false;
    }
    public void Capture(DirtBlock block)
    {
        if (!block || captured.Contains(block)) return;
        captured.Add(block);
        block.Hold(this);
        RefreshFill();
    }
    public void Forget(DirtBlock block) { if (captured.Remove(block)) RefreshFill(); }
    void RefreshFill(bool gradual = false)
    {
        if (!fill)
        {
            fill = gameObject.AddComponent<BucketSoilFill>();
            fill.Initialize(this);
        }
        if (gradual) fill.SetTargetAmount(FillFraction);
        else fill.SetAmount(FillFraction);
    }
    public Vector3 PourPosition(float across)
    {
        if (!volume) volume = GetComponent<BoxCollider>();
        Vector3 lip = pourPoint ? pourPoint.position : transform.TransformPoint(volume.center + Vector3.Scale(volume.size * 0.5f, lipAxis.normalized));
        return lip + transform.right * (Mathf.Clamp(across, -0.5f, 0.5f) * volume.size.x * Mathf.Abs(transform.lossyScale.x) * 0.85f)
            + transform.TransformDirection(openingAxis.normalized) * 0.015f + transform.TransformDirection(lipAxis.normalized) * 0.025f;
    }
    void FixedUpdate() { Step(Time.fixedDeltaTime); }
    public void Step(float dt)
    {
        if (dt <= 0) return;
        dt = Mathf.Min(dt, 0.05f);
        if (!motionInitialized) { lastPosition = transform.position; lastRotation = transform.rotation; motionInitialized = true; }
        velocity = Vector3.ClampMagnitude((transform.position - lastPosition) / dt, 5f);
        var turn = transform.rotation * Quaternion.Inverse(lastRotation);
        turn.ToAngleAxis(out float angle, out var axis);
        if (angle > 180) angle -= 360;
        Vector3 angular = angle == 0 || axis.sqrMagnitude < 0.001f ? Vector3.zero :
            Vector3.ClampMagnitude(axis * (angle * Mathf.Deg2Rad / dt), 4f);
        lastPosition = transform.position;
        lastRotation = transform.rotation;
        captured.RemoveAll(b => !b || !b.gameObject.activeSelf);
        if (fill) fill.Step(dt);
        if (!IsDumping || captured.Count == 0)
        {
            flowRate = Mathf.MoveTowards(flowRate, 0, flowAcceleration * dt);
            releaseCredit = flowClock = 0;
            return;
        }
        if (!sand) { sand = gameObject.AddComponent<SandFlow>(); sand.Initialize(this); }
        float strength = PourStrength;
        // Soil accelerates into a bounded, irregular stream; sudden tipping cannot empty the load in one frame.
        float desired = maxPourRate * strength * Mathf.Lerp(0.75f, 1f, FillFraction);
        flowRate = Mathf.MoveTowards(flowRate, desired, flowAcceleration * dt);
        releaseCredit += flowRate * dt;
        flowClock += dt;
        if (flowClock < batchInterval || releaseCredit < Mathf.Min(2, captured.Count)) return;
        flowClock = 0;
        int count = Mathf.Min(captured.Count, maxBatchSize, Mathf.FloorToInt(releaseCredit));
        for (int n = 0; n < count; n++)
        {
            int next = 0; float best = float.NegativeInfinity;
            for (int i = 0; i < captured.Count; i++)
            {
                var p = captured[i].transform.localPosition;
                float score = p.y + p.z * 0.65f + Random.Range(-0.004f, 0.004f);
                if (score > best) { best = score; next = i; }
            }
            var block = captured[next];
            captured.RemoveAt(next);
            Vector3 sliding = transform.TransformDirection(lipAxis.normalized) * Mathf.Lerp(0.04f, 0.15f, strength);
            Vector3 inherited = velocity + Vector3.Cross(angular, block.transform.position - transform.position);
            Vector3 launch = inherited + sliding + transform.right * Random.Range(-0.04f, 0.04f);
            block.DropAsSoil(block.transform.position, launch);
            sand.EmitLoose(block.transform.position, launch, block.Volume * finesFraction);
        }
        releaseCredit -= count;
        RefreshFill(true);
    }
}
