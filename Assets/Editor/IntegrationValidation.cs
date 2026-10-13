using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run with Unity -batchmode -executeMethod IntegrationValidation.Run.
public static class IntegrationValidation
{
    public static void Run()
    {
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0,
                    "Missing script: " + transform.name);

            var dig = UnityEngine.Object.FindFirstObjectByType<DigSystem>();
            var teeth = UnityEngine.Object.FindFirstObjectByType<DigTeeth>();
            Require(dig && dig.terrain && dig.terrain.terrainData && dig.blockPrefab && dig.bucket,
                $"DigSystem scene references are incomplete: system={dig}, terrain={dig?.terrain}, data={dig?.terrain?.terrainData}, prefab={dig?.blockPrefab}, bucket={dig?.bucket}.");
            Require(teeth && teeth.tip && dig.bucket.spawnPoint && dig.blockPrefab.rb,
                "Bucket, teeth or dirt rigidbody reference is missing.");
            Require(dig.terrain.GetComponent<TerrainCollider>(), "Terrain collider is missing.");
            foreach (var driver in UnityEngine.Object.FindObjectsByType<PartDriver>(FindObjectsSortMode.None))
                Require(driver.lever && driver.target, "PartDriver reference missing: " + driver.name);
            foreach (var tracks in UnityEngine.Object.FindObjectsByType<TrackDrive>(FindObjectsSortMode.None))
                Require(tracks.leftLever && tracks.rightLever && tracks.excavator,
                    "TrackDrive reference missing: " + tracks.name);

            var original = dig.terrain.terrainData;
            var copy = UnityEngine.Object.Instantiate(original);
            dig.terrain.terrainData = copy;
            try
            {
                var heights = copy.GetHeights(0, 0, copy.heightmapResolution, copy.heightmapResolution);
                for (int z = 0; z < heights.GetLength(0); z++)
                for (int x = 0; x < heights.GetLength(1); x++) heights[z, x] = 0.5f;
                copy.SetHeights(0, 0, heights);
                var point = dig.terrain.transform.position + copy.size * 0.5f;
                float before = dig.SurfaceY(point);
                float radius = Mathf.Max(copy.heightmapScale.x, copy.heightmapScale.z) * 2f;
                float volume = dig.Dig(point, radius, 0.03f);
                Require(volume > 0 && dig.SurfaceY(point) < before, "Dig did not lower terrain.");
                dig.Add(point, radius, volume);
                Require(Mathf.Abs(dig.SurfaceY(point) - before) < 0.001f,
                    "Returning dug volume did not restore terrain height.");
            }
            finally
            {
                dig.terrain.terrainData = original;
                UnityEngine.Object.DestroyImmediate(copy);
            }
            Debug.Log("INTEGRATION PASS: scene scripts, excavator references, digging and terrain refill.");
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
