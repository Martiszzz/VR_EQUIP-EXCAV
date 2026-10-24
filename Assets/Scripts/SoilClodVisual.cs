using System.Collections.Generic;
using UnityEngine;

// Shared, low-poly irregular clods replace the old cube mesh without adding physics bodies.
public static class SoilClodVisual
{
    static Mesh[] meshes;
    static Material[] materials;
    static TerrainLayer groundLayer;
    static Texture2D groundTexture;
    static TerrainLayer dumpedLayer;
    static Texture2D dumpedTexture;

    // iškasta / supilta žemė (tamsi, drėgna), sutampa su clod'ų spalvomis
    static readonly Color DarkEarth = new Color(0.20f, 0.125f, 0.065f);
    static readonly Color LightEarth = new Color(0.37f, 0.245f, 0.125f);
    // terrain paviršius (šviesus, sausas)
    static readonly Color GroundDark = new Color(0.42f, 0.35f, 0.25f);
    static readonly Color GroundLight = new Color(0.62f, 0.54f, 0.40f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic()
    {
        meshes = null; materials = null;
        groundLayer = null; groundTexture = null;
        dumpedLayer = null; dumpedTexture = null;
    }

    public static TerrainLayer GroundLayer
    {
        get { if (!groundLayer) BuildGround(); return groundLayer; }
    }

    public static TerrainLayer DumpedLayer
    {
        get { if (!dumpedLayer) BuildDumped(); return dumpedLayer; }
    }

    static float TileNoise(float u, float v, float freq, float offset)
    {
        float x = u * freq + offset, y = v * freq + offset;
        float a = Mathf.PerlinNoise(x, y);
        float b = Mathf.PerlinNoise(x - freq, y);
        float c = Mathf.PerlinNoise(x, y - freq);
        float d = Mathf.PerlinNoise(x - freq, y - freq);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v); // be siūlių
    }

    static void BuildGround()
    {
        const int size = 256;
        groundTexture = new Texture2D(size, size, TextureFormat.RGB24, true)
        {
            name = "Ground surface (runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float fine = TileNoise(u, v, 64f, 50f);
                float patches = TileNoise(u, v, 5f, 80f);
                float pebble = TileNoise(u, v, 110f, 120f);

                var c = Color.Lerp(GroundDark, GroundLight, Mathf.Clamp01(patches * 1.3f - 0.15f));
                c *= 0.85f + fine * 0.3f;
                if (pebble > 0.68f) c = Color.Lerp(c, new Color(0.72f, 0.70f, 0.64f), 0.45f);
                c.a = 1f;
                pixels[y * size + x] = c;
            }
        groundTexture.SetPixels(pixels);
        groundTexture.Apply(true);

        groundLayer = new TerrainLayer
        {
            name = "Ground surface layer (runtime)",
            diffuseTexture = groundTexture,
            tileSize = new Vector2(3f, 3f),
            smoothness = 0f,
            metallic = 0f
        };
    }

    static void BuildDumped()
    {
        const int size = 256;
        dumpedTexture = new Texture2D(size, size, TextureFormat.RGB24, true)
        {
            name = "Dumped earth (runtime)",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 4
        };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float fine = TileNoise(u, v, 64f, 20f);
                float coarse = TileNoise(u, v, 8f, 40f);
                var c = Color.Lerp(DarkEarth, LightEarth, Mathf.Clamp01(coarse * 1.2f - 0.1f));
                c *= 0.8f + fine * 0.4f;
                c.a = 1f;
                pixels[y * size + x] = c;
            }
        dumpedTexture.SetPixels(pixels);
        dumpedTexture.Apply(true);

        dumpedLayer = new TerrainLayer
        {
            name = "Dumped earth layer (runtime)",
            diffuseTexture = dumpedTexture,
            tileSize = new Vector2(1.5f, 1.5f),
            smoothness = 0.03f,
            metallic = 0f
        };
    }

    public static Material EarthMaterial
    {
        get { if (meshes == null || !meshes[0]) Build(); return materials[2]; }
    }

    public static void Apply(GameObject chunk)
    {
        if (meshes == null || !meshes[0]) Build();
        var filter = chunk.GetComponent<MeshFilter>();
        var renderer = chunk.GetComponent<MeshRenderer>();
        if (filter) filter.sharedMesh = meshes[Random.Range(0, meshes.Length)];
        if (renderer) renderer.sharedMaterial = materials[Random.Range(0, materials.Length)];
    }

    static void Build()
    {
        meshes = new Mesh[10];
        for (int i = 0; i < meshes.Length; i++) meshes[i] = Clod(i);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        materials = new Material[5];
        for (int i = 0; i < materials.Length; i++)
        {
            var color = Color.Lerp(DarkEarth, LightEarth, i / 4f);
            materials[i] = new Material(shader) { name = "Excavated earth " + i };
            if (materials[i].HasProperty("_BaseColor")) materials[i].SetColor("_BaseColor", color);
            if (materials[i].HasProperty("_Color")) materials[i].SetColor("_Color", color);
            materials[i].SetFloat("_Smoothness", 0.03f);
        }
    }

    static Mesh Clod(int seed)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var vertices = new List<Vector3> { new Vector3(-1,t,0), new Vector3(1,t,0), new Vector3(-1,-t,0),
            new Vector3(1,-t,0), new Vector3(0,-1,t), new Vector3(0,1,t), new Vector3(0,-1,-t),
            new Vector3(0,1,-t), new Vector3(t,0,-1), new Vector3(t,0,1), new Vector3(-t,0,-1), new Vector3(-t,0,1) };
        for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
        int[] faces = {0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6,
            7,1,8, 3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1};
        var triangles = new List<int>();
        var edges = new Dictionary<long, int>();
        for (int i = 0; i < faces.Length; i += 3)
        {
            int a = faces[i], b = faces[i + 1], c = faces[i + 2];
            int ab = Midpoint(a, b, vertices, edges), bc = Midpoint(b, c, vertices, edges), ca = Midpoint(c, a, vertices, edges);
            triangles.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }
        var random = new System.Random(731 + seed * 97);
        var rotation = Quaternion.Euler((float)random.NextDouble() * 180f, (float)random.NextDouble() * 180f, (float)random.NextDouble() * 180f);
        var bounds = new Bounds(Vector3.zero, Vector3.zero);
        for (int i = 0; i < vertices.Count; i++)
        {
            float radius = Mathf.Lerp(0.82f, 1.16f, (float)random.NextDouble());
            vertices[i] = rotation * vertices[i] * radius;
            bounds.Encapsulate(vertices[i]);
        }
        // Stay inside each reserved slot, while differing in shape and orientation.
        for (int i = 0; i < vertices.Count; i++)
        {
            var p = vertices[i] - bounds.center;
            vertices[i] = new Vector3(p.x / bounds.size.x * 0.98f, p.y / bounds.size.y * 0.92f,
                p.z / bounds.size.z * 0.98f);
        }
        var mesh = new Mesh { name = "Earth clod " + seed };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
    {
        long key = ((long)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
        if (cache.TryGetValue(key, out int found)) return found;
        int index = vertices.Count;
        vertices.Add((vertices[a] + vertices[b]).normalized);
        cache.Add(key, index);
        return index;
    }
}