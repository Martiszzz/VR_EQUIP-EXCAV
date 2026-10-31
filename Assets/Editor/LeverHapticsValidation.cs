using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class LeverHapticsValidation
{
    public static void Run()
    {
        try
        {
            var feedback = new LeverHapticFeedback();
            Require(!feedback.Step(1, false, .02f, out _, out _), "Ungrasped lever vibrates");
            Require(!feedback.Step(.3f, true, .02f, out _, out _), "Neutral region vibrates");
            Require(feedback.Step(.6f, true, .1f, out float low, out _), "Travel cue missing");
            Require(feedback.Step(.9f, true, .1f, out float high, out _ ) && high > low, "Travel cue does not strengthen");
            Require(feedback.Step(1, true, .02f, out float stop, out float duration) && stop > high && duration >= .08f, "Distinct end-stop click missing");
            int repeatedClicks = 0, holdingPulses = 0;
            for (int i = 0; i < 100; i++)
                if (feedback.Step(i % 2 == 0 ? .95f : 1, true, .01f, out float amplitude, out _))
                { if (amplitude > .5f) repeatedClicks++; else holdingPulses++; }
            Require(repeatedClicks == 0 && holdingPulses >= 3 && holdingPulses <= 5, "Limit jitter retriggers strong clicks or spams vibration");
            feedback.Step(.8f, true, .25f, out _, out _);
            Require(feedback.Step(1, true, .02f, out stop, out _) && stop > .5f, "Returning to end stop does not click again");
            Require(!feedback.Step(1, true, 0, out _, out _), "Paused game emits impulses");
            feedback.Step(1, false, .02f, out _, out _);
            Require(feedback.Step(1, true, .02f, out stop, out _) && stop > .5f, "New grip retains previous grip state");
            var go = new GameObject("Lever haptics fixture");
            try
            {
                go.AddComponent<XRSimpleInteractable>();
                var lever = go.AddComponent<LeverTilt>();
                typeof(LeverTilt).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lever, null);
                Require(lever.hapticsEnabled && lever.hapticStrength > 0, "Existing scene levers do not enable feedback by default");
                foreach (var direction in new[] {Vector2.up, Vector2.down, Vector2.right, new Vector2(1, 1).normalized})
                {
                    feedback.Reset(); bool clicked = false;
                    for (int i = 0; i < 100; i++)
                    {
                        lever.StepTilt(direction * lever.maxAngle * 2, true, .02f);
                        float travel = new Vector2(lever.Forward, lever.Side).magnitude;
                        Require(travel <= 1.001f, "Lever exceeds circular travel limit");
                        if (feedback.Step(travel, true, .02f, out float amplitude, out _) && amplitude > .5f) clicked = true;
                    }
                    Require(clicked, "Smoothed lever never reaches haptic end stop: " + direction);
                    for (int i = 0; i < 100; i++) lever.StepTilt(Vector2.zero, false, .02f);
                }
                lever.forwardOnly = true;
                for (int i = 0; i < 100; i++) lever.StepTilt(Vector2.one * 100, true, .02f);
                Require(lever.Side == 0 && lever.Forward > .96f, "Travel lever no longer has its forward-only stop");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            Debug.Log("LEVER HAPTICS PASS: progressive travel, circular/forward/reverse stops, jitter hysteresis, bounded hold pulses, release/regrip and pause.");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
