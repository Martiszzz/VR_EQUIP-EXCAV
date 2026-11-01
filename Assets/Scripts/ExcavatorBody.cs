using System.Collections.Generic;
using UnityEngine;

public class ExcavatorBody : MonoBehaviour
{
    public LayerMask obstacles = ~0;                 // su kokiais sluoksniais susiduriama
    [Min(0f)] public float tolerance = 0.01f;        // leidžiamas nežymus persidengimas (m)
    [Min(.001f)] public float dynamicContactSkin=.018f;
    [Min(.05f)] public float maxObjectPushSpeed=.35f;
    [Min(10)] public float maxObjectPushForce=1200;

    public static ExcavatorBody For(Transform t)
    {
        var body = t.GetComponentInParent<ExcavatorBody>();
        if (!body) body = t.root.gameObject.AddComponent<ExcavatorBody>();
        return body;
    }

    Probe chassisProbe;
    CylinderLink[] links;
    public void AlignLinkages()
    {if(links==null)links=GetComponentsInChildren<CylinderLink>();foreach(var link in links)if(link)link.Align();}
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
        static readonly Collider[] hits = new Collider[128];
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
        Vector3 groundContact;
        struct MovableContact { public Rigidbody body;public Vector3 direction,point;public float depth; }
        readonly List<MovableContact> movable=new List<MovableContact>();
        public void ResolveDynamicContacts(float dt)
        {
            foreach(var hit in movable)
            {
                if(!hit.body || hit.body.isKinematic)continue;
                float speed=Mathf.Min(body.maxObjectPushSpeed,hit.depth/Mathf.Max(.02f,dt));
                float force=Mathf.Clamp(hit.body.mass*(speed-Vector3.Dot(hit.body.GetPointVelocity(hit.point),hit.direction))/.08f,0,body.maxObjectPushForce);
                hit.body.maxDepenetrationVelocity=.35f;
                hit.body.AddForceAtPosition(hit.direction*force*Mathf.Min(1,dt/Time.fixedDeltaTime),hit.point,ForceMode.Force);
            }
        }

        public float CuttingResistance {get;private set;}=1;
        public float Tolerance { get { return body.tolerance; } }

        public Probe(ExcavatorBody owner, Transform root) { body = owner; part = root; }
        public void AlignLinkages(){body.AlignLinkages();}
        void RefreshColliders()
        {
            if(Time.time<nextRefresh)return;
            part.GetComponentsInChildren<Collider>(false,mine);nextRefresh=Time.time+.25f;
        }
        public int MotionSteps(float degrees)
        {
            RefreshColliders();float reach=0;
            foreach(var collider in mine)
                if(collider && collider.enabled && !collider.isTrigger)
                    reach=Mathf.Max(reach,Vector3.Distance(part.position,collider.bounds.center)+collider.bounds.extents.magnitude);
            return Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(Mathf.Abs(degrees)/.25f,Mathf.Abs(degrees)*Mathf.Deg2Rad*reach/.008f)),1,128);
        }
        public void BeginMove()
        {
            workingSoil=false;
            part.GetComponentsInChildren<DigTeeth>(false, teeth);
            part.GetComponentsInChildren<GroundedAttachment>(false,pushing);
            previousBlades.Clear();
            foreach(var blade in pushing)previousBlades.Add(blade.CoveredByParent?Vector3.zero:blade.transform.TransformPoint(blade.WorkBounds.center));
            previousRotations.Clear();previousMouthUp.Clear();
            previousTips.Clear();
            previousBodies.Clear();
            foreach (var tooth in teeth)
            {
                tooth.BeginToothSweep();
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
            float moved=0;Vector3 workPoint=part.position;CuttingResistance=1;
            for (int i = 0; i < teeth.Count; i++)
                if (teeth[i].tip)
                {
                    var bucket=DigSystem.I?DigSystem.I.bucket:null;
                    bool closing=bucket && part==teeth[i].transform && Vector3.Dot(bucket.transform.TransformDirection(bucket.openingAxis.normalized),Vector3.up)>previousMouthUp[i]+0.000001f && Quaternion.Angle(previousRotations[i],teeth[i].transform.rotation)>0.01f;
                    teeth[i].Sweep(previousTips[i],teeth[i].tip.position,dt,teeth[i].transform.position-previousBodies[i],closing);
                    moved+=teeth[i].LastWorkVolume;
                    if(teeth[i].LastWorkVolume>0)workPoint=teeth[i].tip.position;
                    CuttingResistance=Mathf.Max(CuttingResistance,teeth[i].LastResistance);
                }
            if(DigSystem.I && Ignition.EngineRunning)
                for(int i=0;i<pushing.Count;i++)
                {
                    if(pushing[i].CoveredByParent)continue;
                    var point=pushing[i].transform.TransformPoint(pushing[i].WorkBounds.center);
                    float volume=pushing[i].canScrapeSoil?DigSystem.I.ScrapeAttachmentSoil(pushing[i],point-previousBlades[i]):DigSystem.I.PushAttachmentSoil(pushing[i],point-previousBlades[i]);moved+=volume;
                    if(pushing[i].canScrapeSoil)CuttingResistance=Mathf.Max(CuttingResistance,DigSystem.I.LastToolResistance);
                    if(volume>0)
                    {
                        workPoint=new Vector3(point.x,DigSystem.I.SurfaceY(point),point.z);
                        CuttingResistance=Mathf.Max(CuttingResistance,DigSystem.I.SoilAt(workPoint).resistance*(1+DigSystem.I.LastBladeLoad));
                    }
                }
            var grounding=body.GetComponent<ExcavatorGrounding>();
            if(grounding)
            {
                float resistance=1;foreach(var tooth in teeth)resistance=Mathf.Max(resistance,tooth.LastResistance);
                grounding.ReportHydraulicWork(moved*resistance,dt);
            }
            if(moved>0 && DigSystem.I && DigSystem.I.feedback)
                DigSystem.I.feedback.Report(workPoint,moved);
            return moved;
        }
        public bool ReactToGround(float dt,float effort=1)
        {
            var grounding=body.GetComponent<ExcavatorGrounding>();
            if(!grounding || !grounding.Ready)return false;
            float depth=GroundDepth();
            if(depth<=Tolerance)return false;
            Vector3 contact=groundContact;
            // Native suspension consumes this pressure next physics tick. It does not propose a new
            // root pose, so querying/rolling back every chassis collider here was both costly and wrong.
            if(grounding.UsesPhysicalSupport)
            {grounding.TryHydraulicReaction(contact,depth,dt,effort);return false;}
            Vector3 position=grounding.chassis.position;Quaternion rotation=grounding.chassis.rotation;
            var full=body.chassisProbe ?? (body.chassisProbe=body.CreateProbe(grounding.chassis));float before=full.Depth(true);
            bool reacted=grounding.TryHydraulicReaction(contact,depth,dt,effort);
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
            groundContact=part.position;
            foreach(var bucket in buckets)
            {float d=bucket.GroundPenetration(DigSystem.I,workingSoil,out var contact);if(d>depth){depth=d;groundContact=contact;}}
            part.GetComponentsInChildren<GroundedAttachment>(false, grounded);
            var terrain = DigSystem.I ? DigSystem.I.terrain : Terrain.activeTerrain;
            foreach(var attachment in grounded)
            {if(attachment.CoveredByParent)continue;float d=attachment.Depth(terrain,out var contact);if(workingSoil && attachment.canScrapeSoil)d=Mathf.Max(0,d-attachment.softSoilDepth);if(d>depth){depth=d;groundContact=contact;}}
            return depth;
        }

        // Bendras persidengimo gylis tarp šios dalies (su visais vaikais) ir pasaulio kliūčių.
        // ignoreFloor: ignoruoja kontaktus iš apačios (vikšrai stovi ant grindų).
        public float Depth(bool ignoreFloor)
        {
            RefreshColliders();
            movable.Clear();
            float total = 0f;
            for (int k = 0; k < mine.Count; k++)
            {
                var a = mine[k];
                if (!a || !a.enabled || a.isTrigger || a is CharacterController) continue;
                var bounds = a.bounds;
                int n = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, hits,
                    Quaternion.identity, body.obstacles, QueryTriggerInteraction.Ignore);
                if(n==hits.Length)return Mathf.Max(total,Tolerance+.001f); // Fail closed if the overlap query is saturated.
                for (int i = 0; i < n; i++)
                {
                    var b = hits[i];
                    if (b == a || !body.IsObstacle(b)) continue;
                    if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation, out Vector3 dir, out float dist))
                    {
                        if (ignoreFloor && dir.y > 0.6f) continue;
                        var rb=b.attachedRigidbody;
                        if(rb && !rb.isKinematic)
                        {
                            int index=-1;for(int contactIndex=0;contactIndex<movable.Count;contactIndex++)if(movable[contactIndex].body==rb){index=contactIndex;break;}
                            var entry=new MovableContact{body=rb,direction=-dir,point=b.ClosestPoint(a.bounds.center),depth=dist};
                            if(index<0)movable.Add(entry);else if(movable[index].depth<dist)movable[index]=entry;
                            dist=Mathf.Max(0,dist-body.dynamicContactSkin);
                        }
                        total += dist;
                    }
                }
            }
            return total;
        }
    }
}
