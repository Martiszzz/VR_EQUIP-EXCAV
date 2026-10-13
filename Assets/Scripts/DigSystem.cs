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
    public float BlockVolume => blockSize * blockSize * blockSize;
    public int FreeBlocks => Mathf.Max(0, maxBlocks - active.Count);
    public bool CanSpawn => FreeBlocks > 0 && blockPrefab;
    float CellArea => terrain.terrainData.heightmapScale.x * terrain.terrainData.heightmapScale.z;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { I = null; }
    void Awake()
    {
        if (!terrain || !terrain.terrainData || !blockPrefab || !bucket)
        {
            Debug.LogError("DigSystem requires terrain, a dirt prefab and a bucket.", this);
            enabled = false;
            return;
        }
        I = this;
        original = terrain.terrainData;
        runtimeData = Instantiate(original);
        terrain.terrainData = runtimeData;
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider) collider.terrainData = runtimeData;
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
        return removed * td.size.y * CellArea;
    }
    // Return the accepted volume so a saturated terrain never silently deletes soil.
    public float Add(Vector3 point, float radius, float volume)
    {
        if (volume <= 0 || !GetWindow(point, radius, out int x0, out int z0, out int w, out int h,
            out float cx, out float cz, out float rx, out float rz)) return 0f;
        var td = terrain.terrainData;
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
        td.SetHeights(x0, z0, heights);
        return Mathf.Min(volume, accepted * td.size.y * CellArea);
    }
    public DirtBlock Spawn(Vector3 pos)
    {
        if (!CanSpawn) return null;
        var block = pool.Count > 0 ? pool.Dequeue() : Instantiate(blockPrefab, transform);
        block.transform.SetParent(null, false);
        block.transform.localScale = Vector3.one * blockSize;
        block.transform.SetParent(transform, true);
        active.Add(block);
        block.Activate(pos, this, BlockVolume);
        return block;
    }
    public void Release(DirtBlock block)
    {
        if (!block || !active.Remove(block)) return;
        block.PrepareForPool();
        pool.Enqueue(block);
    }
}
