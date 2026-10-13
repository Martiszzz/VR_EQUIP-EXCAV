using UnityEngine;

public class DigTeeth : MonoBehaviour
{
    public Transform tip;
    [Min(0.01f)] public float radius = 0.25f;
    [Min(0.001f)] public float maxDepthPerStep = 0.03f;
    [Min(0f)] public float minSpeed = 0.02f;
    [Range(0.1f, 2f)] public float cutPerMeter = 0.6f;
    [Min(0.1f)] public float maxSweepDistance = 1f;
    Vector3 last;
    float carry;
    void OnEnable() { if (tip) last = tip.position; }
    void FixedUpdate()
    {
        if (!tip) return;
        Vector3 p = tip.position, start = last;
        last = p;
        var s = DigSystem.I;
        float distance = Vector3.Distance(start, p);
        if (!s || !s.bucket || !Ignition.EngineRunning || s.bucket.IsDumping) return;
        if (distance < minSpeed * Time.fixedDeltaTime || distance > maxSweepDistance) return;
        int slots = Mathf.Min(s.bucket.FreeSlots(s.blockSize), s.FreeBlocks);
        float budget = Mathf.Max(0f, slots * s.BlockVolume - carry);
        if (budget <= 0f) return;
        int steps = Mathf.Clamp(Mathf.CeilToInt(distance / (radius * 0.5f)), 1, 8);
        for (int i = 1; i <= steps && budget > 0f; i++)
        {
            var point = Vector3.Lerp(start, p, i / (float)steps);
            if (!s.ContainsTerrainPoint(point)) continue;
            float penetration = s.SurfaceY(point) - point.y;
            if (penetration <= 0.005f) continue;
            float depth = Mathf.Min(penetration, maxDepthPerStep, distance / steps * cutPerMeter);
            float removed = s.Dig(point, radius, depth, budget, point.y);
            carry += removed;
            budget -= removed;
        }
        while (carry + s.BlockVolume * 0.0001f >= s.BlockVolume && s.CanSpawn)
        {
            if (!s.bucket.TryGetSpawnPosition(s.blockSize, out var position)) break;
            var block = s.Spawn(position);
            if (!block) break;
            s.bucket.Capture(block);
            carry = Mathf.Max(0f, carry - s.BlockVolume);
        }
    }
}
