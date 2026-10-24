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
        float nextRefresh = -1f;

        public float Tolerance { get { return body.tolerance; } }

        public Probe(ExcavatorBody owner, Transform root) { body = owner; part = root; }

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