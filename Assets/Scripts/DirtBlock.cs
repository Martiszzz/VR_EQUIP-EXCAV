using UnityEngine;

public class DirtBlock : MonoBehaviour
{
    public Rigidbody rb;
    public float Volume { get; private set; }
    public SoilKind Kind {get;private set;}
    public bool IsHeld { get; private set; }
    public int FractureLevel { get; private set; }
    [Min(0.5f)] public float breakSpeed = 1.6f;
    [Min(0.04f)] public float depositRadius = 0.12f;
    [Min(0.1f)] public float settleTime = 0.1f;
    DigSystem owner;
    BucketInside bucket;
    Collider[] colliders;
    Renderer[] renderers;
    BoxCollider grainCollider;
    SphereCollider soilCollider;
    Vector3 originalColliderSize;
    float calm, age;
    float supportClock;
    bool supported;

    public void Activate(Vector3 pos, DigSystem system, float volume, int fractureLevel = 0, SoilKind kind = SoilKind.Loam)
    {
        owner = system;Kind=kind;
        Volume = volume;
        FractureLevel = fractureLevel;
        if (!rb) rb = GetComponent<Rigidbody>();
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            grainCollider = GetComponent<BoxCollider>();
            if (grainCollider) originalColliderSize = grainCollider.size;
            soilCollider = gameObject.AddComponent<SphereCollider>();
            soilCollider.radius = 0.48f;
        }
        if(colliders==null)colliders = GetComponentsInChildren<Collider>();
        foreach (var renderer in renderers) renderer.enabled = true;
        SoilClodVisual.Apply(gameObject);
        if (grainCollider) grainCollider.size = originalColliderSize;
        transform.SetPositionAndRotation(pos, Quaternion.identity);
        gameObject.SetActive(true);
        rb.position=pos;rb.rotation=Quaternion.identity;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 0.8f;
        rb.mass = Mathf.Max(0.01f, volume * owner.PropertiesFor(Kind).density);
        rb.solverIterations=8;rb.solverVelocityIterations=2;rb.maxDepenetrationVelocity=2;
        SetColliders(true);
        IsHeld = false;
        calm = age = 0;
        supportClock = 0; supported = false;
    }
    public void Hold(BucketInside container)
    {
        bucket = container;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rb.isKinematic = true;
        SetColliders(false);
        transform.SetParent(container.transform, true);
        transform.rotation = container.transform.rotation;
        IsHeld = true;
        SetVisible(false);
    }
    public void Drop(Vector3 velocity)
    {
        transform.SetParent(owner.transform, true);
        IsHeld = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SetColliders(true);
        SetVisible(true);
        owner.IgnoreSoilCollisions(this);
        rb.linearVelocity = Vector3.ClampMagnitude(velocity, 5f);
        rb.WakeUp();
        calm = age = 0;
        supportClock = 0; supported = false;
    }
    public void DropAsSoil(Vector3 position, Vector3 velocity)
    {
        // Loose earth slides out; a fast bucket turn must not launch it sideways.
        var horizontal = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(velocity, Vector3.up), 0.25f);
        velocity = horizontal + Vector3.up * Mathf.Clamp(velocity.y, -1f, 0.15f);
        Drop(velocity);
        rb.position = position;
        transform.position = position;
        rb.angularVelocity = Random.insideUnitSphere * 0.3f;
        // Retain the same visible clod and collision size after leaving the bucket.
    }
    void OnCollisionEnter(Collision collision)
    {
        if (IsHeld || !gameObject.activeSelf || collision.contactCount == 0) return;
        if (owner && collision.collider == owner.terrain.GetComponent<TerrainCollider>())
        {
            Deposit(collision.GetContact(0).point);
            return;
        }
        if (age < 0.1f) return;
        if (collision.collider.GetComponentInParent<DirtBlock>()) return;
        if (bucket && collision.transform.IsChildOf(bucket.transform.parent)) return;
        TryBreak(collision.GetContact(0).normal, collision.relativeVelocity.magnitude);
    }
    public void IgnoreSoilCollision(DirtBlock other)
    {
        // Clods represent parcels of a continuous soil stream, not bouncing stones.
        if (other && other != this && soilCollider && other.soilCollider)
            Physics.IgnoreCollision(soilCollider, other.soilCollider);
    }
    void Deposit(Vector3 point)
    {
        if (!owner || IsHeld || !gameObject.activeSelf || !owner.ContainsTerrainPoint(point) ||
            (owner.bucket && owner.bucket.Contains(this))) return;
        float radius = Mathf.Max(depositRadius, Mathf.Pow(Volume, 1f / 3f) * 1.5f);
        float deposited=owner.Add(point, radius, Volume, true,Kind);
        Volume -= deposited;
        if(owner.feedback)owner.feedback.Report(point,deposited);
        if (Volume <= owner.BlockVolume * 0.001f) owner.Release(this);
    }
    public bool TryBreak(Vector3 normal, float impactSpeed)
    {
        if (!owner || !owner.CanSpawn || owner.AvailableLooseBodies<2 || Volume <= 0 || IsHeld || FractureLevel > 0 || impactSpeed < breakSpeed || owner.FreeBlocks < 2) return false;
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
        normal.Normalize();
        float childVolume = Volume / 3f;
        float size = Mathf.Pow(childVolume, 1f / 3f);
        Vector3 tangent = Vector3.Cross(normal, Vector3.forward).normalized;
        if (tangent.sqrMagnitude < 0.5f) tangent = Vector3.Cross(normal, Vector3.right).normalized;
        Vector3 bitangent = Vector3.Cross(normal, tangent);
        Vector3 center = rb.position + normal * size * 0.3f;
        Vector3 inherited = Vector3.ClampMagnitude(rb.linearVelocity, 1.5f);
        for (int i = 1; i <= 2; i++)
        {
            float angle = i * Mathf.PI * 2f / 3f;
            Vector3 radial = tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle);
            var fragment = owner.Spawn(center + radial * size * 0.7f, childVolume, 1,Kind);
            fragment.DropAsSoil(fragment.transform.position, inherited + radial * 0.12f + normal * 0.08f);
        }
        Volume = childVolume;
        FractureLevel = 1;
        var parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(size / Mathf.Abs(parentScale.x), size / Mathf.Abs(parentScale.y), size / Mathf.Abs(parentScale.z));
        rb.mass = Mathf.Max(0.01f, childVolume * owner.PropertiesFor(Kind).density);
        rb.position = center + tangent * size * 0.7f;
        rb.linearVelocity = inherited + tangent * 0.12f + normal * 0.08f;
        if (owner.bucket && owner.bucket.Sand) owner.bucket.Sand.EmitImpact(center, normal, 18);
        calm = age = 0;
        return true;
    }
    public void PrepareForPool()
    {
        if (bucket) bucket.Forget(this);
        bucket = null;
        IsHeld = false;
        transform.SetParent(owner.transform, true);
        gameObject.SetActive(false);
    }
    void SetVisible(bool value) { foreach (var renderer in renderers) renderer.enabled = value; }
    void SetColliders(bool value)
    {
        foreach (var c in colliders) c.enabled = value;
        if (grainCollider) grainCollider.enabled = false;
    }
    bool HasGroundSupport()
    {
        float radius = soilCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        // A resting clod may be supported by other soil, rather than touching the heightmap itself.
        if (Physics.Raycast(rb.position, Vector3.down, out var hit, radius + 0.035f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == owner.terrain.GetComponent<TerrainCollider>()) return true;
            var otherSoil = hit.collider.GetComponentInParent<DirtBlock>();
            return otherSoil && otherSoil != this && !otherSoil.IsHeld && rb.position.y - owner.SurfaceY(rb.position) < 1f;
        }
        return rb.position.y - radius <= owner.SurfaceY(rb.position) + 0.015f;
    }
    void FixedUpdate() { Step(Time.fixedDeltaTime); }
    public void Step(float dt)
    {
        if (!owner || IsHeld || !gameObject.activeSelf || dt <= 0) return;
        age += dt;
        bool inBucket = owner.bucket && owner.bucket.Contains(this);
        if (!owner.ContainsTerrainPoint(transform.position)) return;
        float surface = owner.SurfaceY(transform.position);
        float grainRadius = soilCollider.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        // Heightmap support also covers newly deposited soil before the collider's next sync.
        if (!inBucket && age >= 0.04f && rb.position.y - grainRadius <= surface + 0.01f)
        {
            Deposit(rb.position);
            return;
        }
        if (rb.linearVelocity.sqrMagnitude < 0.04f)
        {
            supportClock -= dt;
            if (supportClock <= 0) { supported = HasGroundSupport(); supportClock = 0.1f; }
            calm = supported ? calm + dt : 0;
        }
        else { calm = supportClock = 0; supported = false; }
        bool fellThrough = transform.position.y < surface - 0.3f;
        if (!inBucket && age > 0.3f && (calm > settleTime || fellThrough))
        {
            Deposit(rb.position);
        }
    }
}
