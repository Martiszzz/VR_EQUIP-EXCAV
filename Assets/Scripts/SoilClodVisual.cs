using System.Collections.Generic;
using UnityEngine;

// Shared, low-poly irregular clods replace the old cube mesh without adding physics bodies.
public static class SoilClodVisual
{
    static Mesh[] meshes;
    static Material[] materials;
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
            var color = Color.Lerp(new Color(0.20f, 0.125f, 0.065f), new Color(0.37f, 0.245f, 0.125f), i / 4f);
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
            int a = faces[i], b = faces[i+1], c = faces[i+2];
            int ab = Midpoint(a,b,vertices,edges), bc = Midpoint(b,c,vertices,edges), ca = Midpoint(c,a,vertices,edges);
            triangles.AddRange(new[] {a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca});
        }
        var random = new System.Random(731 + seed * 97);
        var rotation = Quaternion.Euler((float)random.NextDouble()*180f, (float)random.NextDouble()*180f, (float)random.NextDouble()*180f);
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

    static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<long,int> cache)
    {
        long key = ((long)Mathf.Min(a,b) << 32) | (uint)Mathf.Max(a,b);
        if (cache.TryGetValue(key, out int found)) return found;
        int index = vertices.Count;
        vertices.Add((vertices[a] + vertices[b]).normalized);
        cache.Add(key, index);
        return index;
    }
}
