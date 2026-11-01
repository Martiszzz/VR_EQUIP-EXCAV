using UnityEngine;

using System.Collections.Generic;

public class Claw : MonoBehaviour
{
    [System.Serializable]
    public class Jaw
    {
        public Transform part;
        public Vector3 localAxis = Vector3.right;
        public float openAngle = 0f;
        public float closedAngle = 30f;
        public Collider[] contacts;                  // jei tuščia, ima iš Part ir jo vaikų
        [HideInInspector] public Quaternion rest;
        [HideInInspector] public float amount;       // 0 = atviras, 1 = uždarytas
        [HideInInspector] public Rigidbody touching; // kas sustabdė šį nagą
        [HideInInspector] public bool blocked;
        [System.NonSerialized] public GroundedAttachment terrainContact;
        [System.NonSerialized] public bool groundBlocked;
        [System.NonSerialized] public Collider touchingCollider,contactCollider;
        [System.NonSerialized] public Vector3 contactNormal;
    }

    public LeverBase lever;
    public PartDriver.Axis input = PartDriver.Axis.Forward;
    public bool invert;
    public Throttle throttle;
    public Jaw[] jaws;
    public bool requireAllJaws = true;

    [Header("Griebimas")]
    public Transform grip;
    [Min(0.02f)] public float gripRadius = 0.25f;
    public LayerMask grabMask = ~0;
    [Min(.02f)] public float gripConfirmTime=.08f;
    [Min(.001f)] public float gripContactSkin=.002f;
    Rigidbody pendingGrip;
    float pendingGripTime;

    [Header("Judėjimas")]
    [Min(0.05f)] public float closeSpeed = 0.8f;
    [Range(0f, 0.4f)] public float deadzone = 0.12f;
    [Min(0f)] public float penetrationTolerance = 0.003f;

    [Header("Kolizijų tikrinimas")]
    [Min(0.1f)] public float maxAngleStep = 2f;      // maksimalus nago pasukimas vienu tikrinimu (°)
    [Min(0.001f)] public float maxPushPerStep = 0.03f; // kiek daugiausia nagas pastumia rąstą per žingsnį (m)
    [Min(.05f)] public float maxObjectPushSpeed=.3f;
    [Min(10)] public float maxObjectPushForce=800;
    public LayerMask ignoreLayers = 0;               // sluoksniai, kurių nagai nelaiko kliūtimi (pvz. grindys)

    public float Closed { get; private set; }
    public bool HasObject => held;
    public float CarriedMass => held?held.mass:0;
    public Vector3 CarriedCenter => held?held.worldCenterOfMass:transform.position;
    Collider[] heldColliders;

    Rigidbody held;
    bool heldWasKinematic;
    RigidbodyInterpolation heldInterpolation;
    CollisionDetectionMode heldCollisionMode;
    Vector3 heldLocalPosition;
    Quaternion heldLocalRotation;
    struct HeldCollision {public Collider log,machine;public bool ignored;}
    readonly List<HeldCollision> heldCollisionPairs=new List<HeldCollision>();
    Vector3 lastGrip, gripVelocity;
    readonly Collider[] buffer = new Collider[32];
    float[] previous;

    void Awake()
    {
        foreach (var j in jaws)
        {
            if (!j.part) continue;
            j.rest = j.part.localRotation;
            if (j.contacts == null || j.contacts.Length == 0)
                j.contacts = j.part.GetComponentsInChildren<Collider>();
            j.terrainContact=j.part.GetComponent<GroundedAttachment>();
        }
        previous = new float[jaws.Length];
        if (grip) lastGrip = grip.position;
    }

    void FixedUpdate()
    {
        SyncHeldPose();
        float dt = Time.fixedDeltaTime;
        if (grip)
        {
            gripVelocity = (grip.position - lastGrip) / dt;
            lastGrip = grip.position;
        }

        float v = 0;
        if (lever && Ignition.EngineRunning)
            v = input == PartDriver.Axis.Forward ? lever.Forward : lever.Side;
        if (invert) v = -v;
        float a = Mathf.Clamp01((Mathf.Abs(v) - deadzone) / Mathf.Max(0.01f, 1f - deadzone));
        v = Mathf.Sign(v) * a * a;

        float t = throttle ? throttle.Power : 1f;
        float delta = v * closeSpeed * t * dt;

        if (held && delta < 0f) Release();
        if(delta<=0){pendingGrip=null;pendingGripTime=0;}
        if (held) delta = 0f;                         // laikant rąstą toliau nespaudžiama

        if (Mathf.Abs(delta) > 0.000001f)
        {
            float range = 1f;
            foreach (var j in jaws) range = Mathf.Max(range, Mathf.Abs(j.closedAngle - j.openAngle));
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta) * range / maxAngleStep), 1, 8);
            for (int s = 0; s < steps && !(held); s++) MoveJaws(delta / steps,dt/steps);
        }

        float max = 0f;
        foreach (var j in jaws) max = Mathf.Max(max, j.amount);
        Closed = max;
    }

    void LateUpdate(){SyncHeldPose();}
    void SyncHeldPose()
    {
        if(!held || !grip)return;
        held.position=grip.TransformPoint(heldLocalPosition);
        held.rotation=grip.rotation*heldLocalRotation;
    }
    void MoveJaws(float d,float stepTime)
    {
        if (previous == null || previous.Length != jaws.Length) previous = new float[jaws.Length];
        for (int i = 0; i < jaws.Length; i++)
        {
            var j = jaws[i];
            if (!j.part) continue;
            previous[i] = j.amount;
            var terrain=DigSystem.I?DigSystem.I.terrain:Terrain.activeTerrain;
            float before=j.terrainContact?j.terrainContact.Depth(terrain):0;
            var oldPoint=j.terrainContact?j.part.TransformPoint(j.terrainContact.WorkBounds.center):j.part.position;
            if(j.terrainContact)before=Mathf.Max(0,before-j.terrainContact.softSoilDepth);
            j.amount = Mathf.Clamp01(j.amount + d);
            j.blocked = false;
            j.groundBlocked=false;j.touchingCollider=j.contactCollider=null;
            j.touching = null;
            ApplyPose(j);
            if(j.terrainContact && j.terrainContact.canScrapeSoil && DigSystem.I)
                DigSystem.I.ScrapeAttachmentSoil(j.terrainContact,j.part.TransformPoint(j.terrainContact.WorkBounds.center)-oldPoint);
            float after=j.terrainContact?j.terrainContact.Depth(terrain):0;
            if(j.terrainContact)after=Mathf.Max(0,after-j.terrainContact.softSoilDepth);
            // Both opening and closing must respect fresh heightmap heights; allow escape from an existing overlap.
            if(after>penetrationTolerance && after>before+0.0001f)
            {j.amount=previous[i];ApplyPose(j);j.blocked=true;j.groundBlocked=true;}
        }
        if (d <= 0f) return;

        Physics.SyncTransforms();

        // 1) nagai stumia laisvus daiktus
        foreach(var j in jaws)if(j.part)PushOut(j,stepTime);

        // 2) jei vis dar įlindę (statinė kliūtis arba rąstas suspaustas) - nagas lieka senoje pozicijoje
        for (int i = 0; i < jaws.Length; i++)
        {
            var j = jaws[i];
            if (!j.part || j.groundBlocked) continue;
            if (Scan(j, out var rb))
            {
                if(rb && !rb.isKinematic)SeatAtContact(j,previous[i]);
                else {j.amount=previous[i];ApplyPose(j);}
                j.blocked = true;
                j.touching = rb;
            }
        }
        Physics.SyncTransforms();

        if (!held) TryGrab(stepTime);
    }

    void SeatAtContact(Jaw jaw,float start)
    {
        float low=start,high=jaw.amount;
        var a=jaw.contactCollider;var b=jaw.touchingCollider;
        if(!a || !b){jaw.amount=start;ApplyPose(jaw);return;}
        // Retain the admissible part of closing: a whole-step rollback left a visible gap even
        // though the proposed pose had already touched the log.
        for(int i=0;i<7;i++)
        {
            jaw.amount=(low+high)*.5f;ApplyPose(jaw);
            bool overlap=Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out _,out float depth);
            if(overlap && depth>penetrationTolerance)high=jaw.amount;else low=jaw.amount;
        }
        jaw.amount=low;ApplyPose(jaw);
    }
    static void ApplyPose(Jaw j)
    {
        float angle = Mathf.Lerp(j.openAngle, j.closedAngle, j.amount);
        j.part.localRotation = j.rest * Quaternion.AngleAxis(angle, j.localAxis.normalized);
    }

    bool IsIgnored(Collider b, Collider a)
    {
        if (!b || b == a || !b.enabled) return true;
        if (b.transform.IsChildOf(transform.root)) return true;
        if (((1 << b.gameObject.layer) & ignoreLayers) != 0) return true;
        if (b.GetComponentInParent<DirtBlock>()) return true;
        if (b is TerrainCollider || b is CharacterController) return true;
        if (held && b.attachedRigidbody == held) return true;
        return false;
    }

    // Pastumia laisvus Rigidbody, į kuriuos įlindo nagas. Grąžina true, jei ką nors pastūmė.
    bool PushOut(Jaw jaw,float stepTime)
    {
        bool any = false;
        foreach (var a in jaw.contacts)
        {
            if (!a || !a.enabled || !a.gameObject.activeInHierarchy) continue;
            var bounds = a.bounds;
            int n = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, buffer,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var b = buffer[i];
                if (IsIgnored(b, a)) continue;
                var rb = b.attachedRigidbody;
                if (!rb || rb.isKinematic) continue;
                if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                    b, b.transform.position, b.transform.rotation, out Vector3 dir, out float dist)
                    && dist > 0.0005f)
                {
                    // dir - kryptis, kuria turėtų judėti nagas; rąstą stumiame priešingai
                    Vector3 normal=-dir;
                    float speed=Mathf.Min(maxObjectPushSpeed,dist/Mathf.Max(.02f,Time.fixedDeltaTime));
                    float force=Mathf.Clamp(rb.mass*(speed-Vector3.Dot(rb.GetPointVelocity(bounds.center),normal))/.08f,0,maxObjectPushForce);
                    rb.maxDepenetrationVelocity=.35f;
                    rb.AddForceAtPosition(normal*force*Mathf.Clamp01(stepTime/Time.fixedDeltaTime),bounds.center,ForceMode.Force);
                    any = true;
                }
            }
        }
        return any;
    }

    // Ar nagas vis dar įlindęs? rb = to daikto Rigidbody (statiniam null).
    bool Scan(Jaw jaw, out Rigidbody rb)
    {
        rb = null;
        foreach (var a in jaw.contacts)
        {
            if (!a || !a.enabled || !a.gameObject.activeInHierarchy) continue;
            var bounds = a.bounds;
            int n = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, buffer,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var b = buffer[i];
                if (IsIgnored(b, a)) continue;
                if (Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                    b, b.transform.position, b.transform.rotation, out var direction, out float depth)
                    && depth > penetrationTolerance)
                {
                    rb = b.attachedRigidbody;
                    jaw.contactNormal=-direction;jaw.touchingCollider=b;jaw.contactCollider=a;
                    return true;
                }
            }
        }
        return false;
    }

    void TryGrab(float dt)
    {
        var previousCandidate=pendingGrip;float previousTime=pendingGripTime;
        pendingGrip=null;pendingGripTime=0;
        if (!grip) return;
        Rigidbody candidate = null;
        int used = 0,contacts=0;Vector3 firstNormal=Vector3.zero;bool opposed=false;
        foreach (var j in jaws)
        {
            if (!j.part) continue;
            used++;
            if(j.groundBlocked)return;
            if (!j.blocked || !j.touching || j.touching.isKinematic)
            {
                if (requireAllJaws) return;
                continue;
            }
            if (candidate && candidate != j.touching)
            {
                if (requireAllJaws) return;
                continue;
            }
            candidate = j.touching;
            if(!j.touchingCollider || !j.contactCollider)return;
            var onObject=j.touchingCollider.ClosestPoint(j.contactCollider.bounds.center);
            if(Vector3.Distance(onObject,j.contactCollider.ClosestPoint(onObject))>gripContactSkin)return;
            if(contacts==0)firstNormal=j.contactNormal;
            else if(Vector3.Dot(firstNormal,j.contactNormal)<-.2f)opposed=true;
            contacts++;
        }
        // A single jaw touching a log while the other is stopped by soil is not a grip.
        if (!candidate || contacts<2 || !opposed || (requireAllJaws && contacts<used)) return;
        if (((1 << candidate.gameObject.layer) & grabMask) == 0) return;
        pendingGrip=candidate;pendingGripTime=previousCandidate==candidate?previousTime+dt:dt;
        if(pendingGripTime<gripConfirmTime)return;

        // atstumas iki artimiausio rąsto taško (ilgas rąstas turi centrą toli nuo Grip)
        float best = float.MaxValue;
        foreach (var c in candidate.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            best = Mathf.Min(best, Vector3.Distance(c.ClosestPoint(grip.position), grip.position));
        }
        if (best > gripRadius) return;

        held = candidate;
        pendingGrip=null;pendingGripTime=0;
        heldWasKinematic = candidate.isKinematic;
        heldInterpolation=candidate.interpolation;heldCollisionMode=candidate.collisionDetectionMode;
        candidate.linearVelocity = Vector3.zero;
        candidate.angularVelocity = Vector3.zero;
        candidate.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
        candidate.interpolation=RigidbodyInterpolation.None;candidate.isKinematic = true;
        candidate.transform.SetParent(grip, true);
        heldLocalPosition=candidate.transform.localPosition;heldLocalRotation=candidate.transform.localRotation;
        var contact=candidate.GetComponent<GroundedAttachment>();if(!contact)contact=candidate.gameObject.AddComponent<GroundedAttachment>();
        contact.canPushSoil=false;
        heldColliders=candidate.GetComponentsInChildren<Collider>();
        var machine=GetComponentInParent<ExcavatorGrounding>();
        if(machine)foreach(var collider in machine.GetComponentsInChildren<Collider>())
        {
            if(!collider.enabled || collider.isTrigger || collider.attachedRigidbody==held)continue;
            foreach(var log in heldColliders)
            {
                if(!log.enabled || log.isTrigger)continue;
                heldCollisionPairs.Add(new HeldCollision{log=log,machine=collider,ignored=Physics.GetIgnoreCollision(log,collider)});
                Physics.IgnoreCollision(log,collider,true);
            }
        }
        var terrain=DigSystem.I && DigSystem.I.terrain?DigSystem.I.terrain.GetComponent<TerrainCollider>():null;
        if(terrain)foreach(var collider in heldColliders)Physics.IgnoreCollision(collider,terrain,true);
    }

    void Release()
    {
        if (!held) return;
        SyncHeldPose();
        held.transform.SetParent(null, true);
        held.isKinematic = heldWasKinematic;
        held.interpolation=heldInterpolation;held.collisionDetectionMode=heldCollisionMode;
        foreach(var pair in heldCollisionPairs)if(pair.log && pair.machine)Physics.IgnoreCollision(pair.log,pair.machine,pair.ignored);
        heldCollisionPairs.Clear();
        var terrain=DigSystem.I && DigSystem.I.terrain?DigSystem.I.terrain.GetComponent<TerrainCollider>():null;
        if(terrain && heldColliders!=null)foreach(var collider in heldColliders)if(collider)Physics.IgnoreCollision(collider,terrain,false);
        held.linearVelocity = Vector3.ClampMagnitude(gripVelocity, 1.2f);held.angularVelocity=Vector3.zero;
        held.WakeUp();
        held = null;
    }

    void OnDisable() { Release(); }

    void OnDrawGizmosSelected()
    {
        if (!grip) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(grip.position, gripRadius);
    }
}
