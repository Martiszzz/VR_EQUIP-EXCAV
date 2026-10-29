using System.Collections.Generic;
using UnityEngine;

public class DigSystem : MonoBehaviour
{
    public static DigSystem I;
    public Terrain terrain;
    public DirtBlock blockPrefab;
    public BucketInside bucket;
    [Min(0.01f)] public float blockSize = 0.06f;
    [Min(1)] public int maxBlocks = 250;
    readonly Queue<DirtBlock> pool = new Queue<DirtBlock>();
    readonly HashSet<DirtBlock> active = new HashSet<DirtBlock>();
    TerrainData original, runtimeData;
    TerrainData pushBaseData;
    float[,] pushBaseHeights;
    bool terrainDirty;
    float terrainSyncClock;
    void FixedUpdate()
    {
        terrainSyncClock += Time.fixedDeltaTime;
        if (terrainSyncClock >= 0.08f) { FlushTerrainChanges(); terrainSyncClock = 0; }
    }
    public void FlushTerrainChanges()
    {
        if (!terrainDirty || !terrain || !terrain.terrainData) return;
        terrain.terrainData.SyncHeightmap();
        terrainDirty = false;
    }
    public float BlockVolume => blockSize * blockSize * blockSize;
    void RememberSoilSurface()
    {
        var td = terrain.terrainData;
        if (pushBaseData == td && pushBaseHeights != null) return;
        pushBaseData = td;
        pushBaseHeights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
    }
    // Redistribute a shallow strip ahead of a full bucket instead of deleting soil or teleporting through it.
    public float PushSoil(Vector3 point, Vector3 motion, float radius, float maxDepth)
    {
        var horizontal = Vector3.ProjectOnPlane(motion, Vector3.up);
        if (horizontal.magnitude < 0.0001f || horizontal.magnitude < Mathf.Abs(motion.y) || !ContainsTerrainPoint(point)) return 0;
        var destination = point + horizontal.normalized * radius * 1.8f;
        if (!ContainsTerrainPoint(destination)) return 0;
        float penetration = SurfaceY(point) - point.y;
        if (penetration <= 0) return 0;
        float budget = horizontal.magnitude * radius * 2f * Mathf.Min(penetration, maxDepth);
        float removed = Dig(point, radius, Mathf.Min(penetration, maxDepth), budget, point.y);
        float deposited = Add(destination, radius, removed, true);
        if (deposited < removed) Add(point, radius, removed - deposited, true);
        return deposited;
    }
    public int FreeBlocks => Mathf.Max(0, maxBlocks - active.Count);
    public float PushBucketSoil(BucketInside container, Vector3 motion, float maxDepth)
    {
        var horizontal = Vector3.ProjectOnPlane(motion, Vector3.up);
        if (!container || container != bucket || !terrain || maxDepth <= 0 ||
            horizontal.magnitude <= 0.000001f) return 0;
        var box = container.GetComponent<BoxCollider>();
        if (!box) return 0;
        var td = terrain.terrainData;
        RememberSoilSurface();
        var origin = terrain.transform.position;
        var cell = td.heightmapScale;
        var work = container.WorkBounds;
        var frame = container.transform.localToWorldMatrix;
        var bounds = new Bounds(frame.MultiplyPoint3x4(work.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            bounds.Encapsulate(frame.MultiplyPoint3x4(work.center + Vector3.Scale(work.extents, new Vector3(x, y, z))));
        var direction = horizontal.normalized;
        float forwardExtent = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
        float width = Mathf.Abs(direction.z) * bounds.size.x + Mathf.Abs(direction.x) * bounds.size.z;
        float radius = Mathf.Max(width * (container.GradingContact ? 0.9f : 0.65f), Mathf.Max(cell.x, cell.z) * 2);
        var destination = bounds.center + direction * (forwardExtent + radius * 2 + Mathf.Max(cell.x, cell.z) * 2);
        if (!ContainsTerrainPoint(destination)) return 0;
        int res = td.heightmapResolution;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - origin.x) / cell.x) - 1, 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - origin.z) / cell.z) - 1, 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - origin.x) / cell.x) + 1, 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - origin.z) / cell.z) + 1, 0, res - 1);
        var heights = td.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
        var cuts = new float[z1 - z0 + 1, x1 - x0 + 1];
        var inverse = frame.inverse;
        var up = inverse.MultiplyVector(Vector3.up);
        var low = work.min;
        var high = work.max;
        float total = 0;
        for (int z = 0; z < heights.GetLength(0); z++)
        for (int x = 0; x < heights.GetLength(1); x++)
        {
            float wx = origin.x + (x0 + x) * cell.x, wz = origin.z + (z0 + z) * cell.z;
            float bottom = float.PositiveInfinity;
            // Clear one interpolation cell around the tool so the heightmap cannot form an invisible rim.
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (BoxBottom(inverse.MultiplyPoint3x4(new Vector3(wx + dx * cell.x, 0, wz + dz * cell.z)), up, low, high, out float y))
                    bottom = Mathf.Min(bottom, y);
            if (float.IsPositiveInfinity(bottom)) continue;
            float penetration = origin.y + heights[z, x] * td.size.y - bottom;
            float originalSurface = origin.y + pushBaseHeights[z0 + z, x0 + x] * td.size.y;
            // Deposited mounds remain pushable, while the underlying undisturbed soil stays protected.
            if (penetration <= 0.003f || originalSurface - bottom > maxDepth ||
                penetration > bounds.size.y + maxDepth) continue;
            float floor = Mathf.Clamp01((bottom + 0.003f - origin.y) / td.size.y);
            cuts[z, x] = Mathf.Max(0, heights[z, x] - floor);
            total += cuts[z, x];
        }
        float displaced = total * td.size.y * CellArea;
        if (displaced <= 0) return 0;
        // Deposit first and remove only what the destination accepts, preserving volume at terrain limits.
        float deposited = container.GradingContact ? FillLowSpots(destination, radius, bounds.min.y + 0.003f, displaced) : 0;
        deposited += Add(destination + (container.GradingContact ? direction * radius * 0.75f : Vector3.zero), radius, displaced - deposited, true);
        if (deposited <= 0) return 0;
        float ratio = Mathf.Clamp01(deposited / displaced);
        for (int z = 0; z < heights.GetLength(0); z++)
        for (int x = 0; x < heights.GetLength(1); x++) heights[z, x] -= cuts[z, x] * ratio;
        td.SetHeightsDelayLOD(x0, z0, heights);
        terrainDirty = true;
        PaintDumped(bounds.center, Mathf.Max(bounds.extents.x, bounds.extents.z), 1);
        return deposited;
    }
    float FillLowSpots(Vector3 point, float radius, float gradeY, float volume)
    {
        if (volume <= 0 || !GetWindow(point, radius, out int x0, out int z0, out int w, out int h,
            out float cx, out float cz, out float rx, out float rz)) return 0;
        var td = terrain.terrainData;
        var heights = td.GetHeights(x0, z0, w, h);
        var needed = new float[h, w];
        float target = Mathf.Clamp01((gradeY - terrain.transform.position.y) / td.size.y), sum = 0;
        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            needed[z, x] = Mathf.Max(0, target - heights[z, x]) * Weight(x0, z0, x, z, cx, cz, rx, rz);
            sum += needed[z, x];
        }
        if (sum <= 0) return 0;
        float ratio = Mathf.Min(1, volume / (sum * td.size.y * CellArea)), added = 0;
        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            float before = heights[z, x];
            heights[z, x] += needed[z, x] * ratio;
            added += heights[z, x] - before;
        }
        td.SetHeightsDelayLOD(x0, z0, heights); terrainDirty = true;
        PaintDumped(point, radius, 1);
        return Mathf.Min(volume, added * td.size.y * CellArea);
    }
    static bool BoxBottom(Vector3 point, Vector3 up, Vector3 low, Vector3 high, out float bottom)
    {
        bottom = float.NegativeInfinity;
        float top = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(up[axis]) < 0.000001f)
            {
                if (point[axis] < low[axis] || point[axis] > high[axis]) return false;
                continue;
            }
            float a = (low[axis] - point[axis]) / up[axis], b = (high[axis] - point[axis]) / up[axis];
            bottom = Mathf.Max(bottom, Mathf.Min(a, b));
            top = Mathf.Min(top, Mathf.Max(a, b));
            if (bottom > top) return false;
        }
        return true;
    }
    public bool CanSpawn => FreeBlocks > 0 && blockPrefab;
    float CellArea => terrain.terrainData.heightmapScale.x * terrain.terrainData.heightmapScale.z;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { I = null; }
    void Awake()
    {
        if (!terrain || !terrain.terrainData)
        {
            Debug.LogError("DigSystem requires terrain", this);
            enabled = false;
            return;
        }
        I = this;
        original = terrain.terrainData;
        runtimeData = Instantiate(original);
        terrain.terrainData = runtimeData;
        RememberSoilSurface();
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider) collider.terrainData = runtimeData;
        ApplyEarthGround(runtimeData);
    }
    void OnDestroy()
    {
        if (I == this) I = null;
        if (!runtimeData) return;
        if (terrain)
        {
            terrain.terrainData = original;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider) collider.terrainData = original;
        }
        Destroy(runtimeData);
    }

    // Visas terrain padengiamas šviesia žeme; antras sluoksnis (tamsi žemė) pradžioje nematomas.
    static void ApplyEarthGround(TerrainData td)
    {
        td.terrainLayers = new[] { SoilClodVisual.GroundLayer, SoilClodVisual.DumpedLayer };
        int r = td.alphamapResolution;
        var alpha = new float[r, r, 2];
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++) alpha[y, x, 0] = 1f;
        td.SetAlphamaps(0, 0, alpha);
    }

    // Užtepa tamsią žemę ten, kur supilta arba iškasta.
    void PaintDumped(Vector3 world, float radius, float strength)
    {
        var td = terrain.terrainData;
        if (td.alphamapLayers < 2) return;
        int res = td.alphamapResolution;
        Vector3 local = world - terrain.transform.position;
        float cx = local.x / td.size.x * (res - 1), cz = local.z / td.size.z * (res - 1);
        float rx = Mathf.Max(1.5f, radius / td.size.x * (res - 1));
        float rz = Mathf.Max(1.5f, radius / td.size.z * (res - 1));
        int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx)), z0 = Mathf.Max(0, Mathf.FloorToInt(cz - rz));
        int w = Mathf.Min(res - 1, Mathf.CeilToInt(cx + rx)) - x0 + 1;
        int h = Mathf.Min(res - 1, Mathf.CeilToInt(cz + rz)) - z0 + 1;
        if (w <= 0 || h <= 0) return;
        var maps = td.GetAlphamaps(x0, z0, w, h);
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x0 + x - cx) / rx, dz = (z0 + z - cz) / rz;
                float weight = Mathf.Max(0f, 1f - dx * dx - dz * dz);
                if (weight <= 0f) continue;
                float a = Mathf.Min(1f, maps[z, x, 1] + strength * weight);
                maps[z, x, 1] = a;
                maps[z, x, 0] = 1f - a;
            }
        td.SetAlphamaps(x0, z0, maps);
    }

    public bool ContainsTerrainPoint(Vector3 point)
    {
        if (!terrain || !terrain.terrainData) return false;
        Vector3 local = point - terrain.transform.position, size = terrain.terrainData.size;
        return local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z;
    }
    public float SurfaceY(Vector3 point) => terrain.SampleHeight(point) + terrain.transform.position.y;

    bool GetWindow(Vector3 world, float radius, out int x0, out int z0, out int w, out int h,
        out float cx, out float cz, out float rx, out float rz)
    {
        x0 = z0 = w = h = 0; cx = cz = rx = rz = 0;
        if (radius <= 0 || !ContainsTerrainPoint(world)) return false;
        var td = terrain.terrainData;
        int res = td.heightmapResolution;
        Vector3 local = world - terrain.transform.position;
        // Keep a brush smaller than a heightmap cell from falling between all samples.
        radius = Mathf.Max(radius, Mathf.Max(td.heightmapScale.x, td.heightmapScale.z) * 0.8f);
        cx = local.x / td.size.x * (res - 1);
        cz = local.z / td.size.z * (res - 1);
        rx = radius / td.size.x * (res - 1);
        rz = radius / td.size.z * (res - 1);
        x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
        z0 = Mathf.Max(0, Mathf.FloorToInt(cz - rz));
        w = Mathf.Min(res - 1, Mathf.CeilToInt(cx + rx)) - x0 + 1;
        h = Mathf.Min(res - 1, Mathf.CeilToInt(cz + rz)) - z0 + 1;
        return w > 0 && h > 0;
    }
    static float Weight(int x0, int z0, int x, int z, float cx, float cz, float rx, float rz)
    {
        float dx = (x0 + x - cx) / rx, dz = (z0 + z - cz) / rz;
        return Mathf.Max(0f, 1f - dx * dx - dz * dz);
    }
    public float Dig(Vector3 point, float radius, float depth, float maxVolume = float.PositiveInfinity,
        float floorY = float.NegativeInfinity)
    {
        if (depth <= 0 || maxVolume <= 0 || !GetWindow(point, radius, out int x0, out int z0,
            out int w, out int h, out float cx, out float cz, out float rx, out float rz)) return 0f;
        var td = terrain.terrainData;
        RememberSoilSurface();
        var heights = td.GetHeights(x0, z0, w, h);
        var cuts = new float[h, w];
        float sum = 0;
        float floor = Mathf.Max(0, (floorY - terrain.transform.position.y) / td.size.y);
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float cut = Mathf.Min(depth * Weight(x0, z0, x, z, cx, cz, rx, rz) / td.size.y,
                    Mathf.Max(0, heights[z, x] - floor));
                cuts[z, x] = cut;
                sum += cut;
            }
        float available = sum * td.size.y * CellArea;
        if (available <= 0) return 0;
        float ratio = Mathf.Min(1f, maxVolume / available);
        float removed = 0;
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float before = heights[z, x];
                heights[z, x] -= cuts[z, x] * ratio;
                removed += before - heights[z, x];
            }
        td.SetHeights(x0, z0, heights);
        if (removed > 0f) PaintDumped(point, radius * 1.1f, 1f);
        return removed * td.size.y * CellArea;
    }
    // Return the accepted volume so a saturated terrain never silently deletes soil.
    public float Add(Vector3 point, float radius, float volume, bool deferUpdates = false)
    {
        if (volume <= 0 || !GetWindow(point, radius, out int x0, out int z0, out int w, out int h,
            out float cx, out float cz, out float rx, out float rz)) return 0f;
        var td = terrain.terrainData;
        RememberSoilSurface();
        var heights = td.GetHeights(x0, z0, w, h);
        float remaining = volume / (td.size.y * CellArea), accepted = 0;
        // Redistribute overflow among cells still below the maximum height.
        for (int pass = 0; pass < w * h && remaining > 0.00000001f; pass++)
        {
            float sumW = 0;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    if (heights[z, x] < 1f) sumW += Weight(x0, z0, x, z, cx, cz, rx, rz);
            if (sumW <= 0) break;
            float added = 0;
            bool clipped = false;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float desired = remaining * Weight(x0, z0, x, z, cx, cz, rx, rz) / sumW;
                    float before = heights[z, x];
                    heights[z, x] = Mathf.Min(1f, before + desired);
                    added += heights[z, x] - before;
                    clipped |= desired > 1f - before;
                }
            accepted += added;
            remaining = Mathf.Max(0, remaining - added);
            if (!clipped || added <= 0) break;
        }
        if (deferUpdates)
        {
            td.SetHeightsDelayLOD(x0, z0, heights);
            terrainDirty = true;
        }
        else td.SetHeights(x0, z0, heights);
        if (accepted > 0f) PaintDumped(point, radius * 1.3f, 0.5f);
        return Mathf.Min(volume, accepted * td.size.y * CellArea);
    }
    public DirtBlock Spawn(Vector3 pos, float? volume = null, int fractureLevel = 0)
    {
        float amount = volume ?? BlockVolume;
        if (!CanSpawn || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return null;
        var block = pool.Count > 0 ? pool.Dequeue() : Instantiate(blockPrefab, transform);
        block.transform.SetParent(null, false);
        block.transform.localScale = Vector3.one * Mathf.Pow(amount, 1f / 3f);
        block.transform.SetParent(transform, true);
        active.Add(block);
        block.Activate(pos, this, amount, fractureLevel);
        IgnoreSoilCollisions(block);
        return block;
    }
    public void IgnoreSoilCollisions(DirtBlock block)
    {
        foreach (var other in active) block.IgnoreSoilCollision(other);
    }
    public void Release(DirtBlock block)
    {
        if (!block || !active.Remove(block)) return;
        block.PrepareForPool();
        pool.Enqueue(block);
    }
}
