using UnityEngine;

public class DirtBlock : MonoBehaviour
{
    public Rigidbody rb;
    public float Volume { get; private set; }
    public bool IsHeld { get; private set; }
    public int FractureLevel { get; private set; }
    [Min(0.5f)] public float breakSpeed = 1.6f;
    DigSystem owner;
    BucketInside bucket;
    Collider[] colliders;
    Renderer[] renderers;
    BoxCollider grainCollider;
    SphereCollider soilCollider;
    Vector3 originalColliderSize;
    float calm, age;

    public void Activate(Vector3 pos, DigSystem system, float volume, int fractureLevel = 0)
    {
        owner = system;
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
        colliders = GetComponentsInChildren<Collider>();
        foreach (var renderer in renderers) renderer.enabled = true;
        SoilClodVisual.Apply(gameObject);
        if (grainCollider) grainCollider.size = originalColliderSize;
        transform.SetPositionAndRotation(pos, Quaternion.identity);
        gameObject.SetActive(true);
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.linearDamping = 0.15f;
        rb.angularDamping = 0.8f;
        rb.mass = Mathf.Max(0.01f, volume * 1600f);
        SetColliders(true);
        IsHeld = false;
        calm = age = 0;
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
        rb.linearVelocity = Vector3.ClampMagnitude(velocity, 5f);
        rb.WakeUp();
        calm = age = 0;
    }
    public void DropAsSoil(Vector3 position, Vector3 velocity)
    {
        Drop(velocity);
        rb.position = position;
        transform.position = position;
        rb.angularVelocity = Random.insideUnitSphere * 2.5f;
        // Retain the same visible clod and collision size after leaving the bucket.
    }
    void OnCollisionEnter(Collision collision)
    {
        if (age < 0.1f || IsHeld || collision.contactCount == 0) return;
        if (collision.collider.GetComponentInParent<DirtBlock>()) return;
        if (bucket && collision.transform.IsChildOf(bucket.transform.parent)) return;
        TryBreak(collision.GetContact(0).normal, collision.relativeVelocity.magnitude);
    }
    public bool TryBreak(Vector3 normal, float impactSpeed)
    {
        if (!owner || !owner.CanSpawn || Volume <= 0 || IsHeld || FractureLevel > 0 || impactSpeed < breakSpeed || owner.FreeBlocks < 2) return false;
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
            var fragment = owner.Spawn(center + radial * size * 0.7f, childVolume, 1);
            fragment.DropAsSoil(fragment.transform.position, inherited + radial * 0.12f + normal * 0.08f);
        }
        Volume = childVolume;
        FractureLevel = 1;
        var parentScale = transform.parent ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(size / Mathf.Abs(parentScale.x), size / Mathf.Abs(parentScale.y), size / Mathf.Abs(parentScale.z));
        rb.mass = Mathf.Max(0.01f, childVolume * 1600f);
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
    void FixedUpdate()
    {
        if (!owner || IsHeld) return;
        age += Time.fixedDeltaTime;
        bool inBucket = owner.bucket && owner.bucket.Contains(this);
        if (!owner.ContainsTerrainPoint(transform.position)) return;
        float surface = owner.SurfaceY(transform.position);
        bool touchingGround = transform.position.y - transform.lossyScale.y * 0.5f <= surface + 0.08f;
        calm = touchingGround && rb.linearVelocity.sqrMagnitude < 0.04f ? calm + Time.fixedDeltaTime : 0f;
        bool fellThrough = transform.position.y < surface - 0.3f;
        if (!inBucket && age > 0.3f && (calm > 1f || fellThrough))
        {
            float added = owner.Add(transform.position, Mathf.Max(0.2f, owner.blockSize * 2f), Volume);
            Volume -= added;
            if (Volume <= owner.BlockVolume * 0.001f) owner.Release(this);
        }
    }
}
