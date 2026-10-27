using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class LevelControlValidation
{
    // Run with -batchmode -executeMethod LevelControlValidation.Run after activating Unity.
    public static void Run()
    {
        bool engine = Ignition.EngineRunning;
        try
        {
            foreach (string level in new[] { "Level 1", "Level 2" })
            {
                var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{level}.unity");
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, $"{level}: missing script on {t.name}");
                foreach (var throttle in UnityEngine.Object.FindObjectsByType<Throttle>(FindObjectsSortMode.None))
                {
                    float configured = throttle.Value;
                    var pose = throttle.transform.localRotation;
                    throttle.SendMessage("Awake");
                    throttle.SendMessage("Update");
                    Require(Mathf.Abs(throttle.Value - configured) < 0.0001f, $"{level}: initial throttle is reset to zero");
                    Require(Quaternion.Angle(pose, throttle.transform.localRotation) < 0.01f, $"{level}: throttle jumps at startup");
                    if (level == "Level 1") Require(throttle.Value > 0, "Level 1 starts with all hydraulic controls blocked by zero throttle");
                    // Level 2 intentionally starts at zero; emulate the player increasing its throttle.
                    if (throttle.Value == 0) { throttle.Value = 1; throttle.SendMessage("Awake"); }
                }
                foreach (var driver in UnityEngine.Object.FindObjectsByType<PartDriver>(FindObjectsSortMode.None))
                    CheckDriver(level, driver);
                foreach (var tracks in UnityEngine.Object.FindObjectsByType<TrackDrive>(FindObjectsSortMode.None))
                    CheckTracks(level, tracks);
                Debug.Log($"LEVEL CONTROLS PASS: {level}, startup throttle, joint movement, tracks and engine stop.");
            }
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally { Ignition.EngineRunning = engine; }
    }

    static void CheckDriver(string level, PartDriver driver)
    {
        Require(driver.lever && driver.target, $"{level}: missing lever or target on {driver.name}");
        var rotation = driver.target.localRotation;
        try
        {
            Ignition.EngineRunning = false;
            driver.Initialize();
            driver.lever.Forward = driver.lever.Side = 1;
            for (int i = 0; i < 30; i++) driver.Step(0.02f);
            Require(Quaternion.Angle(rotation, driver.target.localRotation) < 0.01f, "Stopped engine still drives joint");
            bool moved = false;
            foreach (float sign in new[] { 1f, -1f })
            {
                driver.target.localRotation = rotation;
                driver.Initialize();
                Ignition.EngineRunning = true;
                driver.lever.Forward = driver.lever.Side = sign;
                for (int i = 0; i < 30; i++) driver.Step(0.02f);
                moved |= Quaternion.Angle(rotation, driver.target.localRotation) > 0.1f;
            }
            Require(moved, $"{level}: {driver.target.name} does not respond to {driver.lever.name}/{driver.input}");
        }
        finally
        {
            driver.target.localRotation = rotation;
            driver.lever.Forward = driver.lever.Side = 0;
            Physics.SyncTransforms();
        }
    }

    static void CheckTracks(string level, TrackDrive tracks)
    {
        Require(tracks.leftLever && tracks.rightLever && tracks.excavator, $"{level}: track input references missing");
        var position = tracks.excavator.position;
        var rotation = tracks.excavator.rotation;
        try
        {
            Ignition.EngineRunning = true;
            tracks.SendMessage("Awake");
            tracks.leftLever.Forward = tracks.rightLever.Forward = 1;
            for (int i = 0; i < 50; i++) tracks.SendMessage("FixedUpdate");
            Require(Vector3.Distance(position, tracks.excavator.position) > 0.01f, $"{level}: tracks do not move");
        }
        finally
        {
            tracks.leftLever.Forward = tracks.rightLever.Forward = 0;
            tracks.excavator.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
        }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
