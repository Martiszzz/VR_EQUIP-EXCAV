using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class EngineAudioValidation
{
    public static void Check()
    {
        var start = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Excavator/engine-start.wav");
        var loop = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Excavator/engine-idle-loop.wav");
        Require(start && loop && start.channels == 1 && loop.channels == 1 && loop.length > 1, "Engine clips missing or invalid");
        loop.LoadAudioData(); var samples = new float[loop.samples];
        Require(loop.GetData(samples, 0), "Loop audio data is not readable");
        float peak = 0; double energy = 0;
        foreach (float sample in samples) { peak = Mathf.Max(peak, Mathf.Abs(sample)); energy += sample * sample; }
        Require(peak <= 0.91f && Math.Sqrt(energy / samples.Length) > 0.05 && Mathf.Abs(samples[0] - samples[samples.Length - 1]) < 0.04f,
            "Engine loop is clipped, silent or has a discontinuous seam");
        var go = new GameObject("Engine audio regression");
        try
        {
            var engine = go.AddComponent<ExcavatorEngineAudio>();
            engine.startupClip = start; engine.engineLoop = loop; engine.Initialize();
            engine.Step(false, 0, 0.02f);
            Require(engine.State == ExcavatorEngineAudio.Phase.Off && engine.LoopSource.volume == 0, "Engine sounds before ignition");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                engine.Step(true, 0, 0.02f);
                Require(engine.State == ExcavatorEngineAudio.Phase.Starting && engine.StartupSource.volume > 0 && engine.LoopSource.volume == 0,
                    "Restart does not play startup before the running loop");
                for (int i = 0; i < 130; i++) engine.Step(true, 0, 0.02f);
                Require(engine.State == ExcavatorEngineAudio.Phase.Running && Mathf.Abs(engine.LoopSource.pitch - engine.idlePitch) < 0.001f,
                    "Startup does not transition to idle");
                float idleVolume = engine.LoopSource.volume, previousPitch = engine.LoopSource.pitch;
                for (int i = 0; i < 60; i++)
                {
                    engine.Step(true, 1, 0.02f);
                    Require(Mathf.Abs(engine.LoopSource.pitch - previousPitch) < 0.03f, "Throttle makes engine pitch jump");
                    previousPitch = engine.LoopSource.pitch;
                }
                Require(engine.LoopSource.volume > idleVolume && Mathf.Abs(engine.LoopSource.pitch - engine.fullPitch) < 0.001f,
                    "Full throttle does not increase engine pitch/volume");
                engine.Step(false, 0, 0.02f);
                Require(engine.State == ExcavatorEngineAudio.Phase.Stopping && engine.LoopSource.volume > 0, "Shutdown cuts audio abruptly");
                for (int i = 0; i < 40; i++) engine.Step(false, 0, 0.02f);
                Require(engine.State == ExcavatorEngineAudio.Phase.Off && engine.LoopSource.volume == 0 && engine.StartupSource.volume == 0,
                    "Engine continues after shutdown");
            }
            engine.Step(true, 0, 0.02f); engine.Step(false, 0, 0.02f);
            for (int i = 0; i < 40; i++) engine.Step(false, 0, 0.02f);
            Require(engine.State == ExcavatorEngineAudio.Phase.Off, "Cancelled ignition leaves audio active");
            engine.Step(true, 1, 0.02f);
            typeof(ExcavatorEngineAudio).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(engine, null);
            Require(engine.State == ExcavatorEngineAudio.Phase.Off && engine.StartupSource.volume == 0, "Disabled engine leaves audio active");
            var before = engine.LoopSource.transform.position;
            go.transform.position += Vector3.right * 5;
            Require(Vector3.Distance(engine.LoopSource.transform.position, before + Vector3.right * 5) < 0.001f &&
                engine.LoopSource.spatialBlend == 1 && engine.LoopSource.loop && engine.LoopSource.dopplerLevel == 0,
                "Engine sound does not move with the excavator in 3D");
            Require(go.GetComponentsInChildren<AudioSource>().Length == 2, "Repeated ignition duplicates audio sources");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        string startGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(start));
        string loopGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(loop));
        foreach (string level in new[] { "Level 1", "Level 2" })
        {
            string scene = File.ReadAllText("Assets/Scenes/" + level + ".unity");
            var block = Regex.Match(scene, @"(?ms)^--- !u!114 &2048484092\r?\n.*?(?=^--- !u!|\z)").Value;
            string throttle = level == "Level 1" ? "7008082312208645429" : "9205069731105245680";
            Require(block.Contains("guid: " + startGuid) && block.Contains("guid: " + loopGuid) &&
                block.Contains("m_GameObject: {fileID: 6558627886208490360}") && block.Contains("throttle: {fileID: " + throttle + "}"),
                level + " engine audio clips, throttle or cab attachment missing");
        }
        Debug.Log("ENGINE AUDIO PASS: mono loop seam, three restart cycles, smooth throttle pitch, shutdown/cancel, 3D attachment and both scene references.");
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
