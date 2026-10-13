using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SandPreview
{
    public static void RenderLoaded()
    {
        Loaded();
        EditorApplication.Exit(0);
    }
    public static void RenderBoth()
    {
        Loaded();
        Render();
    }
    static void Loaded()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var dig = Object.FindAnyObjectByType<DigSystem>();
        var bucket = dig.bucket;
        for (int i = 0; i < bucket.capacity; i++)
        {
            if (!bucket.TryGetSpawnPosition(dig.blockSize, out var p)) break;
            bucket.Capture(dig.Spawn(p));
        }
        bucket.transform.root.position += Vector3.up * 0.8f;
        var earth = Object.FindAnyObjectByType<DirtBlock>();
        var material = earth.GetComponent<Renderer>().sharedMaterial;
        Debug.Log($"EARTH MATERIAL: {material.name}, shader={material.shader.name}, color={material.GetColor("_BaseColor")}");
        foreach (var light in Object.FindObjectsByType<Light>())
        {
            light.color = Color.white;
            if (light.type == LightType.Directional) light.intensity = 1.2f;
        }
        var center = bucket.GetComponent<BoxCollider>().bounds.center;
        var position = center + bucket.transform.TransformDirection(bucket.openingAxis) * 0.6f +
            bucket.transform.TransformDirection(bucket.lipAxis) * 0.75f + bucket.transform.right * 0.4f;
        Capture(position, center, 36, "C:/Temp/VR-earth-clods-preview.png");
    }
    public static void Render()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var dig = Object.FindAnyObjectByType<DigSystem>();
        var bucket = dig.bucket;
        for (int i = 0; i < bucket.capacity; i++)
        {
            if (!bucket.TryGetSpawnPosition(dig.blockSize, out var p)) break;
            bucket.Capture(dig.Spawn(p));
        }
        var rig = bucket.transform.parent;
        rig.position += Vector3.up * 1.7f;
        rig.localRotation *= Quaternion.Euler(-80, 0, 0);
        Physics.SyncTransforms();
        var previousSimulationMode = Physics.simulationMode;
        Physics.simulationMode = SimulationMode.Script;
        for (int i = 0; i < 60; i++)
        {
            bucket.Step(0.02f);
            bucket.Sand.Particles.Simulate(0.02f, false, false, true);
            Physics.SyncTransforms();
            Physics.Simulate(0.02f);
        }
        Physics.simulationMode = previousSimulationMode;
        Vector3 outlet = bucket.PourPosition(0);
        Vector3 target = outlet + Vector3.down * 0.45f;
        Capture(target + new Vector3(1.35f, 0.3f, 1.4f), target, 42, "C:/Temp/VR-soil-pouring-preview.png");
        Debug.Log("SAND PREVIEW: " + bucket.Sand.Particles.particleCount + " grains");
        EditorApplication.Exit(0);
    }
    static void Capture(Vector3 position, Vector3 target, float fieldOfView, string path)
    {
        var camera = new GameObject("Sand preview camera").AddComponent<Camera>();
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.05f;
        camera.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(1100, 800, 24);
        camera.targetTexture = rt;
        camera.Render();
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(camera.gameObject);
    }
}
