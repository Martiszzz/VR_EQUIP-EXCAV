using System.Collections.Generic;
using UnityEngine;

// A connected, rough soil surface hides the accounting chunks while they are in the bucket.
public class BucketSoilFill : MonoBehaviour
{
    const int Columns = 14, Rows = 12;
    Mesh mesh;
    MeshRenderer surface;
    Material material;
    Texture2D texture;
    Vector3[] vertices;
    Transform[] details;
    Vector2[] detailPoints;
    float[] detailSizes;
    Vector3 low, high;
    public Mesh SurfaceMesh => mesh;
    public bool IsVisible => surface && surface.enabled;
    public float Amount { get; private set; }

    public void Initialize(BucketInside bucket)
    {
        var box = bucket.GetComponent<BoxCollider>();
        var scale = bucket.transform.lossyScale;
        var padding = new Vector3(bucket.wallPadding / Mathf.Abs(scale.x), bucket.wallPadding / Mathf.Abs(scale.y),
            bucket.wallPadding / Mathf.Abs(scale.z));
        low = box.center - box.size * 0.5f + padding;
        high = box.center + box.size * 0.5f - padding;
        var go = new GameObject("Bucket soil mass");
        go.transform.SetParent(transform, false);
        surface = go.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "Uneven bucket soil fill" };
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(SoilClodVisual.EarthMaterial) { name = "Rough bucket earth" };
        texture = new Texture2D(128, 128, TextureFormat.RGB24, false) { name = "Soil grain", wrapMode = TextureWrapMode.Repeat };
        var pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
        {
            float grain = Mathf.PerlinNoise(x * 0.73f + 4, y * 0.73f + 9);
            float coarse = Mathf.PerlinNoise(x * 0.12f, y * 0.12f);
            float tone = 0.62f + grain * 0.22f + coarse * 0.16f;
            pixels[y * 128 + x] = new Color(tone, tone, tone);
        }
        texture.SetPixels(pixels); texture.Apply();
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
        surface.sharedMaterial = material;
        int layer = (Columns + 1) * (Rows + 1);
        vertices = new Vector3[layer * 2];
        var uv = new Vector2[vertices.Length];
        for (int y = 0; y <= Rows; y++)
        for (int x = 0; x <= Columns; x++)
        {
            int i = y * (Columns + 1) + x;
            uv[i] = uv[i + layer] = new Vector2(x / (float)Columns * 3, y / (float)Rows * 3);
        }
        var triangles = new List<int>();
        for (int y = 0; y < Rows; y++)
        for (int x = 0; x < Columns; x++)
        {
            int a = y * (Columns + 1) + x, b = a + 1, c = a + Columns + 1, d = c + 1;
            triangles.AddRange(new[] {a,b,c, b,d,c, a+layer,c+layer,b+layer, b+layer,c+layer,d+layer});
        }
        for (int x = 0; x < Columns; x++)
        {
            Side(x+1, x, layer, triangles);
            Side(Rows*(Columns+1)+x, Rows*(Columns+1)+x+1, layer, triangles);
        }
        for (int y = 0; y < Rows; y++)
        {
            Side(y*(Columns+1), (y+1)*(Columns+1), layer, triangles);
            Side((y+1)*(Columns+1)+Columns, y*(Columns+1)+Columns, layer, triangles);
        }
        details = new Transform[18];
        detailPoints = new Vector2[details.Length];
        detailSizes = new float[details.Length];
        var random = new System.Random(4381);
        for (int i = 0; i < details.Length; i++)
        {
            var clod = new GameObject("Surface soil clod");
            clod.transform.SetParent(go.transform, false);
            clod.AddComponent<MeshFilter>(); clod.AddComponent<MeshRenderer>();
            SoilClodVisual.Apply(clod);
            details[i] = clod.transform;
            detailPoints[i] = new Vector2(0.10f + (float)random.NextDouble()*0.8f, 0.08f + (float)random.NextDouble()*0.68f);
            detailSizes[i] = 0.016f + (float)random.NextDouble()*0.012f;
        }
        SetAmount(0);
        mesh.uv = uv;
        mesh.SetTriangles(triangles, 0);
    }
    static void Side(int a, int b, int layer, List<int> triangles)
    { triangles.AddRange(new[] {a,b,a+layer, b,b+layer,a+layer}); }

    public void SetAmount(float amount)
    {
        Amount = Mathf.Clamp01(amount);
        if (!mesh) return;
        surface.enabled = Amount > 0.001f;
        int layer = vertices.Length / 2;
        for (int y = 0; y <= Rows; y++)
        for (int x = 0; x <= Columns; x++)
        {
            int i = y * (Columns + 1) + x;
            float u = x / (float)Columns, v = y / (float)Rows;
            if (x > 0 && x < Columns) u += (Mathf.PerlinNoise(x*3.1f, y*7.3f)-0.5f)*0.035f;
            if (y > 0 && y < Rows) v += (Mathf.PerlinNoise(x*6.7f+2, y*3.9f)-0.5f)*0.035f;
            var p = SurfacePoint(u, v);
            vertices[i] = p;
            vertices[i+layer] = new Vector3(p.x,p.y,low.z);
        }
        mesh.vertices = vertices;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        for (int i = 0; i < details.Length; i++)
        {
            float size = detailSizes[i] * Mathf.Sqrt(Amount);
            Vector3 p = SurfacePoint(detailPoints[i].x, detailPoints[i].y);
            details[i].gameObject.SetActive(Amount > 0.03f && p.z - low.z > size * 0.5f);
            details[i].localScale = Vector3.one * size;
            details[i].localPosition = p - Vector3.forward * size * 0.25f;
        }
    }
    Vector3 SurfacePoint(float u, float v)
    {
        float mound = Mathf.Exp(-((u-0.48f)*(u-0.48f)*8f + (v-0.40f)*(v-0.40f)*7f));
        float roughness = (Mathf.PerlinNoise(u*19f+3, v*17f+11)-0.5f)*0.12f;
        float frontSlope = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f-v) / 0.65f));
        float height = Mathf.Clamp01(Amount * (0.48f + mound*0.48f + roughness) * frontSlope);
        return new Vector3(Mathf.Lerp(low.x,high.x,u), Mathf.Lerp(low.y,high.y,v), Mathf.Lerp(low.z,high.z,height));
    }
    void OnDestroy()
    {
        if (mesh) Dispose(mesh);
        if (material) Dispose(material);
        if (texture) Dispose(texture);
    }
    static void Dispose(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
}
