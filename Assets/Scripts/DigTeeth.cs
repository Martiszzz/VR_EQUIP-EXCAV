using UnityEngine;

public class DigTeeth : MonoBehaviour
{
    public Transform tip;
    [Min(0.01f)] public float radius = 0.25f;
    [Min(0.001f)] public float maxDepthPerStep = 0.03f;
    [Min(0f)] public float minSpeed = 0.02f;
    [Range(0.1f, 2f)] public float cutPerMeter = 0.6f;
    [Min(0.1f)] public float maxSweepDistance = 1f;
    [Range(0.02f, 0.3f)] public float maxPushDepth = 0.16f;
    Vector3 last;
    float carry;
    public float PendingVolume => carry;
    public void ResetSweep() { if (tip) last = tip.position; }
    void OnEnable() { if (tip) last = tip.position; }
    void FixedUpdate() { if (tip) Sweep(last, tip.position, Time.fixedDeltaTime); }
    public void Sweep(Vector3 start, Vector3 p, float dt, Vector3? bodyMotion = null)
    {
        if (!tip) return;
        last = p;
        var s = DigSystem.I;
        var motion = p - start;
        if (bodyMotion.HasValue && motion.sqrMagnitude < bodyMotion.Value.sqrMagnitude * 0.1f) motion = bodyMotion.Value;
        float distance = motion.magnitude;
        if (!s || !s.bucket || !Ignition.EngineRunning) return;
        if (distance <= 0.000001f || distance > maxSweepDistance) return;
        s.bucket.GradingContact = IsBackDragging(s.bucket, motion);
        int slots = Mathf.Min(s.bucket.FreeSlots(s.blockSize), s.FreeBlocks);
        float budget = Mathf.Max(0f, slots * s.BlockVolume - carry);
        if (budget <= 0f || s.bucket.GradingContact)
        {
            s.PushBucketSoil(s.bucket, motion, maxPushDepth);
            return;
        }
        if (distance < minSpeed * dt) return;
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
    public static bool IsBackDragging(BucketInside container, Vector3 motion)
    {
        if (!container || !container.pourPoint) return false;
        var planar = Vector3.ProjectOnPlane(motion, Vector3.up);
        var box = container.GetComponent<BoxCollider>();
        var leading = Vector3.ProjectOnPlane(container.pourPoint.position - container.transform.TransformPoint(box.center), Vector3.up);
        return planar.sqrMagnitude > 0.000000000001f && leading.sqrMagnitude > 0.00001f &&
            Mathf.Abs(motion.y) < planar.magnitude &&
            Vector3.Dot(container.transform.TransformDirection(container.openingAxis.normalized), Vector3.up) > 0.7f &&
            Vector3.Dot(planar.normalized, leading.normalized) < -0.25f;
    }
}
