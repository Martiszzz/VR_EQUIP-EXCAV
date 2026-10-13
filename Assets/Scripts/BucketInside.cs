using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class BucketInside : MonoBehaviour
{
    public Transform spawnPoint;
    public Transform pourPoint;
    [Min(1)] public int capacity = 60;
    public Vector3 openingAxis = Vector3.forward;
    public Vector3 lipAxis = Vector3.up;
    [Range(30f, 150f)] public float dumpAngle = 60f;
    [Min(1f)] public float dumpRate = 16f;
    [Min(0f)] public float wallPadding = 0.015f;
    readonly List<DirtBlock> captured = new List<DirtBlock>();
    BoxCollider volume;
    Vector3 lastPosition, velocity;
    float dumpCarry;
    SandFlow sand;
    public SandFlow Sand => sand;
    public float PourStrength
    {
        get
        {
            if (!IsDumping) return 0;
            float openingUp = Vector3.Dot(transform.TransformDirection(openingAxis.normalized), Vector3.up);
            return Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(Mathf.Cos(dumpAngle * Mathf.Deg2Rad),
                Mathf.Cos((dumpAngle + 20f) * Mathf.Deg2Rad), openingUp));
        }
    }

    public bool IsDumping
    {
        get
        {
            float openingUp = Vector3.Dot(transform.TransformDirection(openingAxis.normalized), Vector3.up);
            float lipDown = Vector3.Dot(transform.TransformDirection(lipAxis.normalized), Vector3.down);
            return openingUp < Mathf.Cos(dumpAngle * Mathf.Deg2Rad) && (lipDown > 0.5f || openingUp < -0.2f);
        }
    }
    public bool IsFull => FreeSlots(DigSystem.I ? DigSystem.I.blockSize : 0.08f) == 0;
    void Awake() { volume = GetComponent<BoxCollider>(); lastPosition = transform.position; }
    public bool Contains(DirtBlock block)
    {
        if (!block) return false;
        if (captured.Contains(block)) return true;
        if (!volume) volume = GetComponent<BoxCollider>();
        Vector3 p = transform.InverseTransformPoint(block.transform.position) - volume.center;
        Vector3 half = volume.size * 0.5f;
        return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
    }

    public int FreeSlots(float size)
    {
        captured.RemoveAll(b => !b || !b.gameObject.activeSelf);
        Grid(size, out var counts, out _, out _);
        return Mathf.Max(0, Mathf.Min(capacity, counts.x * counts.y * counts.z) - captured.Count);
    }

    void Grid(float size, out Vector3Int counts, out Vector3 first, out Vector3 step)
    {
        if (!volume) volume = GetComponent<BoxCollider>();
        var scale = transform.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        var worldSize = Vector3.Scale(volume.size, scale) - Vector3.one * (2f * wallPadding);
        float spacing = Mathf.Max(0.001f, size) * 1.08f;
        counts = new Vector3Int(Mathf.Max(0, Mathf.FloorToInt(worldSize.x / spacing)),
            Mathf.Max(0, Mathf.FloorToInt(worldSize.y / spacing)),
            Mathf.Max(0, Mathf.FloorToInt(worldSize.z / spacing)));
        step = new Vector3(spacing / Mathf.Max(scale.x, 0.001f),
            spacing / Mathf.Max(scale.y, 0.001f), spacing / Mathf.Max(scale.z, 0.001f));
        first = volume.center - Vector3.Scale(step, (Vector3)counts - Vector3.one) * 0.5f;
    }

    public bool TryGetSpawnPosition(float size, out Vector3 position)
    {
        position = default;
        if (IsDumping || FreeSlots(size) == 0) return false;
        Grid(size, out var counts, out var first, out var step);
        // Fill bottom layers first, keeping a separate slot for every chunk.
        for (int z = 0; z < counts.z; z++)
        for (int y = 0; y < counts.y; y++)
        for (int x = 0; x < counts.x; x++)
        {
            var candidate = transform.TransformPoint(first + Vector3.Scale(step, new Vector3(x, y, z)));
            bool occupied = captured.Exists(b => (b.transform.position - candidate).sqrMagnitude < size * size * 0.5f);
            if (!occupied) { position = candidate; return true; }
        }
        return false;
    }

    public void Capture(DirtBlock block)
    {
        if (!block || captured.Contains(block)) return;
        captured.Add(block);
        block.Hold(this);
    }
    public void Forget(DirtBlock block) { captured.Remove(block); }
    public Vector3 PourPosition(float across)
    {
        if (!volume) volume = GetComponent<BoxCollider>();
        Vector3 lip = pourPoint ? pourPoint.position : transform.TransformPoint(volume.center +
            Vector3.Scale(volume.size * 0.5f, lipAxis.normalized));
        float width = volume.size.x * Mathf.Abs(transform.lossyScale.x);
        return lip + transform.right * (Mathf.Clamp(across, -0.5f, 0.5f) * width * 0.85f)
            + transform.TransformDirection(openingAxis.normalized) * 0.015f
            + transform.TransformDirection(lipAxis.normalized) * 0.025f;
    }

    void FixedUpdate() { Step(Time.fixedDeltaTime); }
    public void Step(float deltaTime)
    {
        if (deltaTime <= 0) return;
        velocity = Vector3.ClampMagnitude((transform.position - lastPosition) / deltaTime, 5f);
        lastPosition = transform.position;
        captured.RemoveAll(b => !b || !b.gameObject.activeSelf);
        if (!IsDumping) { dumpCarry = 0; return; }
        if (!sand)
        {
            sand = gameObject.AddComponent<SandFlow>();
            sand.Initialize(this);
        }
        float flow = PourStrength;
        dumpCarry += dumpRate * flow * deltaTime;
        while (dumpCarry >= 1f && captured.Count > 0)
        {
            var block = captured[captured.Count - 1];
            captured.RemoveAt(captured.Count - 1);
            Vector3 launch = velocity + transform.TransformDirection(lipAxis.normalized) * Mathf.Lerp(0.15f, 0.45f, flow);
            block.DropAsSand(PourPosition(Random.Range(-0.5f, 0.5f)), launch);
            sand.AddVolume(block.Volume);
            dumpCarry -= 1f;
        }
        if (captured.Count == 0) dumpCarry = 0;
    }
}
