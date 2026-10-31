using UnityEngine;

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

    [Header("Judėjimas")]
    [Min(0.05f)] public float closeSpeed = 0.8f;
    [Range(0f, 0.4f)] public float deadzone = 0.12f;
    [Min(0f)] public float penetrationTolerance = 0.003f;

    [Header("Kolizijų tikrinimas")]
    [Min(0.1f)] public float maxAngleStep = 2f;      // maksimalus nago pasukimas vienu tikrinimu (°)
    [Min(0.001f)] public float maxPushPerStep = 0.03f; // kiek daugiausia nagas pastumia rąstą per žingsnį (m)
    public LayerMask ignoreLayers = 0;               // sluoksniai, kurių nagai nelaiko kliūtimi (pvz. grindys)

    public float Closed { get; private set; }
    public bool HasObject => held;

    Rigidbody held;
    bool heldWasKinematic;
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
        if (held) delta = 0f;                         // laikant rąstą toliau nespaudžiama

        if (Mathf.Abs(delta) > 0.000001f)
        {
            float range = 1f;
            foreach (var j in jaws) range = Mathf.Max(range, Mathf.Abs(j.closedAngle - j.openAngle));
            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta) * range / maxAngleStep), 1, 8);
            for (int s = 0; s < steps && !(held); s++) MoveJaws(delta / steps);
        }

        float max = 0f;
        foreach (var j in jaws) max = Mathf.Max(max, j.amount);
        Closed = max;
    }

    void MoveJaws(float d)
    {
        if (previous == null || previous.Length != jaws.Length) previous = new float[jaws.Length];
        for (int i = 0; i < jaws.Length; i++)
        {
            var j = jaws[i];
            if (!j.part) continue;
            previous[i] = j.amount;
            var terrain=DigSystem.I?DigSystem.I.terrain:Terrain.activeTerrain;
            float before=j.terrainContact?j.terrainContact.Depth(terrain):0;
            j.amount = Mathf.Clamp01(j.amount + d);
            j.blocked = false;
            j.touching = null;
            ApplyPose(j);
            float after=j.terrainContact?j.terrainContact.Depth(terrain):0;
            // Both opening and closing must respect fresh heightmap heights; allow escape from an existing overlap.
            if(after>penetrationTolerance && after>before+0.0001f)
            {j.amount=previous[i];ApplyPose(j);j.blocked=true;}
        }
        if (d <= 0f) return;

        Physics.SyncTransforms();

        // 1) nagai stumia laisvus daiktus
        for (int iter = 0; iter < 3; iter++)
        {
            bool pushed = false;
            foreach (var j in jaws) if (j.part && PushOut(j)) pushed = true;
            if (!pushed) break;
            Physics.SyncTransforms();
        }

        // 2) jei vis dar įlindę (statinė kliūtis arba rąstas suspaustas) - nagas lieka senoje pozicijoje
        for (int i = 0; i < jaws.Length; i++)
        {
            var j = jaws[i];
            if (!j.part) continue;
            if (Scan(j, out var rb))
            {
                j.amount = previous[i];
                ApplyPose(j);
                j.blocked = true;
                j.touching = rb;
            }
        }
        Physics.SyncTransforms();

        if (!held) TryGrab();
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
    bool PushOut(Jaw jaw)
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
                    Vector3 move = -dir * Mathf.Min(dist, maxPushPerStep);
                    rb.position += move;
                    rb.transform.position = rb.position;
                    rb.linearVelocity = Vector3.zero;
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
                    b, b.transform.position, b.transform.rotation, out _, out float depth)
                    && depth > penetrationTolerance)
                {
                    rb = b.attachedRigidbody;
                    return true;
                }
            }
        }
        return false;
    }

    void TryGrab()
    {
        if (!grip) return;
        Rigidbody candidate = null;
        int used = 0;
        foreach (var j in jaws)
        {
            if (!j.part) continue;
            used++;
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
        }
        if (!candidate || (requireAllJaws && used < 2)) return;
        if (((1 << candidate.gameObject.layer) & grabMask) == 0) return;

        // atstumas iki artimiausio rąsto taško (ilgas rąstas turi centrą toli nuo Grip)
        float best = float.MaxValue;
        foreach (var c in candidate.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled || c.isTrigger) continue;
            best = Mathf.Min(best, Vector3.Distance(c.ClosestPoint(grip.position), grip.position));
        }
        if (best > gripRadius) return;

        held = candidate;
        heldWasKinematic = candidate.isKinematic;
        candidate.linearVelocity = Vector3.zero;
        candidate.angularVelocity = Vector3.zero;
        candidate.isKinematic = true;
        candidate.transform.SetParent(grip, true);
    }

    void Release()
    {
        if (!held) return;
        held.transform.SetParent(null, true);
        held.isKinematic = heldWasKinematic;
        held.linearVelocity = Vector3.ClampMagnitude(gripVelocity, 3f);
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
