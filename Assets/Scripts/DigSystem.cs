using System.Collections.Generic;
using UnityEngine;

public class DigSystem : MonoBehaviour
{
    public static DigSystem I;

    public Terrain terrain;
    public DirtBlock blockPrefab;
    public BucketInside bucket;
    public float blockSize = 0.15f;
    public int maxBlocks = 250;

    readonly Queue<DirtBlock> pool = new Queue<DirtBlock>();
    int active;

    public float BlockVolume => blockSize * blockSize * blockSize;
    public bool CanSpawn => active < maxBlocks;

    void Awake()
    {
        I = this;
        // kopija, kad žaidimas nekeistų originalaus Terrain failo
        terrain.terrainData = Instantiate(terrain.terrainData);
        terrain.GetComponent<TerrainCollider>().terrainData = terrain.terrainData;
    }

    public float SurfaceY(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

    float CellArea
    {
        get
        {
            var td = terrain.terrainData; int res = td.heightmapResolution;
            return (td.size.x / (res - 1)) * (td.size.z / (res - 1));
        }
    }

    bool GetWindow(Vector3 world, float radius, out int x0, out int z0, out int w, out int h,
                   out float cx, out float cz, out float rx, out float rz)
    {
        var td = terrain.terrainData; int res = td.heightmapResolution;
        Vector3 l = world - terrain.transform.position;
        cx = l.x / td.size.x * (res - 1);
        cz = l.z / td.size.z * (res - 1);
        rx = radius / td.size.x * (res - 1);
        rz = radius / td.size.z * (res - 1);
        x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
        z0 = Mathf.Max(0, Mathf.FloorToInt(cz - rz));
        int x1 = Mathf.Min(res - 1, Mathf.CeilToInt(cx + rx));
        int z1 = Mathf.Min(res - 1, Mathf.CeilToInt(cz + rz));
        w = x1 - x0 + 1; h = z1 - z0 + 1;
        return w > 0 && h > 0;
    }

    static float Weight(int x0, int z0, int x, int z, float cx, float cz, float rx, float rz)
    {
        float dx = (x0 + x - cx) / rx, dz = (z0 + z - cz) / rz;
        float d2 = dx * dx + dz * dz;
        return d2 > 1f ? 0f : 1f - d2;      // glotni briauna
    }

    // iškasa duobę, grąžina išimtą tūrį m³
    public float Dig(Vector3 p, float radius, float depth)
    {
        if (!GetWindow(p, radius, out int x0, out int z0, out int w, out int h,
                       out float cx, out float cz, out float rx, out float rz)) return 0f;

        var td = terrain.terrainData;
        float[,] hs = td.GetHeights(x0, z0, w, h);    // indeksai [z, x]
        float removed = 0f;

        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float wt = Weight(x0, z0, x, z, cx, cz, rx, rz);
                if (wt <= 0f) continue;
                float nh = Mathf.Max(hs[z, x] - depth * wt / td.size.y, 0f);
                removed += hs[z, x] - nh;
                hs[z, x] = nh;
            }

        td.SetHeights(x0, z0, hs);
        return removed * td.size.y * CellArea;
    }

    // užpila duobę / padidina krūvą duotu tūriu m³
    public void Add(Vector3 p, float radius, float volume)
    {
        if (!GetWindow(p, radius, out int x0, out int z0, out int w, out int h,
                       out float cx, out float cz, out float rx, out float rz)) return;

        var td = terrain.terrainData;
        float[,] hs = td.GetHeights(x0, z0, w, h);

        float sumW = 0f;
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
                sumW += Weight(x0, z0, x, z, cx, cz, rx, rz);
        if (sumW <= 0f) return;

        float meters = volume / (sumW * CellArea);
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float wt = Weight(x0, z0, x, z, cx, cz, rx, rz);
                if (wt > 0f) hs[z, x] = Mathf.Min(hs[z, x] + meters * wt / td.size.y, 1f);
            }

        td.SetHeights(x0, z0, hs);
    }

    public DirtBlock Spawn(Vector3 pos)
    {
        if (!CanSpawn) return null;
        DirtBlock b = pool.Count > 0 ? pool.Dequeue() : Instantiate(blockPrefab, transform);
        b.transform.localScale = Vector3.one * blockSize;
        active++;
        b.Activate(pos);
        return b;
    }

    public void Release(DirtBlock b)
    {
        b.gameObject.SetActive(false);
        pool.Enqueue(b);
        active--;
    }
}