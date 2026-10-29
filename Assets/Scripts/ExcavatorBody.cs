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

    Probe chassisProbe;
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
        readonly List<GroundedAttachment> pushing = new List<GroundedAttachment>();
        readonly List<Vector3> previousBlades = new List<Vector3>();
        readonly List<Quaternion> previousRotations = new List<Quaternion>();
        readonly List<float> previousMouthUp = new List<float>();
        readonly List<Vector3> previousTips = new List<Vector3>();
        readonly List<Vector3> previousBodies = new List<Vector3>();
        float nextRefresh = -1f;
        bool workingSoil;

        public float Tolerance { get { return body.tolerance; } }

        public Probe(ExcavatorBody owner, Transform root) { body = owner; part = root; }
        public void BeginMove()
        {
            workingSoil=false;
            part.GetComponentsInChildren<DigTeeth>(false, teeth);
            part.GetComponentsInChildren<GroundedAttachment>(false,pushing);
            previousBlades.Clear();
            foreach(var blade in pushing)previousBlades.Add(blade.transform.TransformPoint(blade.WorkBounds.center));
            previousRotations.Clear();previousMouthUp.Clear();
            previousTips.Clear();
            previousBodies.Clear();
            foreach (var tooth in teeth)
            {
                previousRotations.Add(tooth.transform.rotation);
                var bucket=DigSystem.I?DigSystem.I.bucket:null;
                previousMouthUp.Add(bucket?Vector3.Dot(bucket.transform.TransformDirection(bucket.openingAxis.normalized),Vector3.up):0);
                previousTips.Add(tooth.tip ? tooth.tip.position : Vector3.zero);
                previousBodies.Add(tooth.transform.position);
            }
        }
        public float WorkSoil(float dt)
        {
            workingSoil=true;
            float moved=0;
            for (int i = 0; i < teeth.Count; i++)
                if (teeth[i].tip)
                {
                    var bucket=DigSystem.I?DigSystem.I.bucket:null;
                    bool closing=bucket && part==teeth[i].transform && Vector3.Dot(bucket.transform.TransformDirection(bucket.openingAxis.normalized),Vector3.up)>previousMouthUp[i]+0.000001f && Quaternion.Angle(previousRotations[i],teeth[i].transform.rotation)>0.01f;
                    teeth[i].Sweep(previousTips[i],teeth[i].tip.position,dt,teeth[i].transform.position-previousBodies[i],closing);
                    moved+=teeth[i].LastWorkVolume;
                }
            if(DigSystem.I && Ignition.EngineRunning)
                for(int i=0;i<pushing.Count;i++) moved+=DigSystem.I.PushAttachmentSoil(pushing[i],pushing[i].transform.TransformPoint(pushing[i].WorkBounds.center)-previousBlades[i]);
            var grounding=body.GetComponent<ExcavatorGrounding>();
            if(grounding)grounding.ReportHydraulicWork(moved,dt);
            return moved;
        }
        public bool ReactToGround(float dt)
        {
            var grounding=body.GetComponent<ExcavatorGrounding>();
            if(!grounding || !grounding.Ready)return false;
            float depth=GroundDepth();
            if(depth<=Tolerance)return false;
            Vector3 position=grounding.chassis.position;Quaternion rotation=grounding.chassis.rotation;
            var full=body.chassisProbe ?? (body.chassisProbe=body.CreateProbe(grounding.chassis));float before=full.Depth(true);
            Vector3 contact=part.position;
            if(teeth.Count>0 && teeth[0].tip)contact=teeth[0].tip.position;
            else if(pushing.Count>0)contact=pushing[0].transform.TransformPoint(pushing[0].WorkBounds.center);
            bool reacted=grounding.TryHydraulicReaction(contact,depth,dt);
            Physics.SyncTransforms();
            if(reacted && GroundDepth()<=Tolerance && full.Depth(true)<=Mathf.Max(Tolerance,before) && full.GroundDepth()<=Tolerance) {CancelMove();return true;}
            grounding.chassis.SetPositionAndRotation(position,rotation);grounding.RefreshState();Physics.SyncTransforms();return false;
        }
        public void CancelMove() { foreach (var tooth in teeth) tooth.ResetSweep(); }

        public float GroundDepth()
        {
            // Bucket contents change during digging, so check capacity on every proposed move.
            part.GetComponentsInChildren<BucketInside>(false, buckets);
            float depth = 0;
            foreach (var bucket in buckets) depth = Mathf.Max(depth, bucket.GroundPenetration(DigSystem.I,workingSoil));
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
