using System.Collections.Generic;
using UnityEngine;

public class ExcavatorBody : MonoBehaviour
{
    public LayerMask obstacles = ~0;                 // su kokiais sluoksniais susiduriama
    [Min(0f)] public float tolerance = 0.01f;        // leidžiamas nežymus persidengimas (m)

    public static ExcavatorBody For(Transform t)
    {
        var body = t.GetComponentInParent<ExcavatorBody>();
        if (!body) body = t.root.gameObject.AddComponent<ExcavatorBody>();
        return body;
    }

    public Probe CreateProbe(Transform part) { return new Probe(this, part); }

    bool IsObstacle(Collider c)
    {
        if (!c || !c.enabled || c.isTrigger) return false;
        if (c.transform.IsChildOf(transform)) return false;                 // pats ekskavatorius
        if (c is TerrainCollider || c is CharacterController) return false; // terrain kasamas, žaidėjas ne kliūtis
        return !c.GetComponentInParent<DirtBlock>();                         // žemės gabalai ne kliūtis
    }

    public class Probe
    {
        static readonly Collider[] hits = new Collider[64];
        readonly ExcavatorBody body;
        readonly Transform part;
        readonly List<Collider> mine = new List<Collider>();
        readonly List<BucketInside> buckets = new List<BucketInside>();
        readonly List<GroundedAttachment> grounded = new List<GroundedAttachment>();
        readonly List<DigTeeth> teeth = new List<DigTeeth>();
        readonly List<Vector3> previousTips = new List<Vector3>();
        readonly List<Vector3> previousBodies = new List<Vector3>();
        float nextRefresh = -1f;

        public float Tolerance { get { return body.tolerance; } }

        public Probe(ExcavatorBody owner, Transform root) { body = owner; part = root; }
        public void BeginMove()
        {
            part.GetComponentsInChildren<DigTeeth>(false, teeth);
            previousTips.Clear();
            previousBodies.Clear();
            foreach (var tooth in teeth)
            {
                previousTips.Add(tooth.tip ? tooth.tip.position : Vector3.zero);
                previousBodies.Add(tooth.transform.position);
            }
        }
        public void WorkSoil(float dt)
        {
            for (int i = 0; i < teeth.Count; i++)
                if (teeth[i].tip) teeth[i].Sweep(previousTips[i], teeth[i].tip.position, dt, teeth[i].transform.position - previousBodies[i]);
        }
        public void CancelMove() { foreach (var tooth in teeth) tooth.ResetSweep(); }

        public float GroundDepth()
        {
            // Bucket contents change during digging, so check capacity on every proposed move.
            part.GetComponentsInChildren<BucketInside>(false, buckets);
            float depth = 0;
            foreach (var bucket in buckets) depth = Mathf.Max(depth, bucket.GroundPenetration(DigSystem.I));
            part.GetComponentsInChildren<GroundedAttachment>(false, grounded);
            var terrain = DigSystem.I ? DigSystem.I.terrain : Terrain.activeTerrain;
            foreach (var attachment in grounded) depth = Mathf.Max(depth, attachment.Depth(terrain));
            return depth;
        }

        // Bendras persidengimo gylis tarp šios dalies (su visais vaikais) ir pasaulio kliūčių.
        // ignoreFloor: ignoruoja kontaktus iš apačios (vikšrai stovi ant grindų).
        public float Depth(bool ignoreFloor)
        {
            if (Time.time >= nextRefresh)
            {
                part.GetComponentsInChildren<Collider>(false, mine);
                nextRefresh = Time.time + 0.25f;
            }
            float total = 0f;
            for (int k = 0; k < mine.Count; k++)
            {
                var a = mine[k];
                if (!a || !a.enabled || a.isTrigger || a is CharacterController) continue;
                var bounds = a.bounds;
                int n = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, hits,
                    Quaternion.identity, body.obstacles, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < n; i++)
                {
                    var b = hits[i];
                    if (b == a || !body.IsObstacle(b)) continue;
                    if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation, out Vector3 dir, out float dist))
                    {
                        if (ignoreFloor && dir.y > 0.6f) continue;
                        total += dist;
                    }
                }
            }
            return total;
        }
    }
}
