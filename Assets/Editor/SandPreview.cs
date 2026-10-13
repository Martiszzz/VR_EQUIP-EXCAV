using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SandPreview
{
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
        for (int i = 0; i < 65; i++)
        {
            bucket.Step(0.02f);
            bucket.Sand.Step(0.02f);
            bucket.Sand.Particles.Simulate(0.02f, false, false, true);
        }
        var camera = new GameObject("Sand preview camera").AddComponent<Camera>();
        Vector3 outlet = bucket.PourPosition(0);
        Vector3 target = outlet + Vector3.down * 0.45f;
        camera.transform.position = target + new Vector3(1.35f, 0.3f, 1.4f);
        camera.transform.LookAt(target);
        camera.fieldOfView = 42;
        camera.nearClipPlane = 0.05f;
        camera.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(1100, 800, 24);
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture.Apply();
        File.WriteAllBytes("C:/Temp/VR-sand-preview.png", texture.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(texture);
        Debug.Log("SAND PREVIEW: " + bucket.Sand.Particles.particleCount + " grains");
        EditorApplication.Exit(0);
    }
}
