using UnityEngine;

public class DirtBlock : MonoBehaviour
{
    public Rigidbody rb;
    public float Volume { get; private set; }
    public bool IsHeld { get; private set; }
    DigSystem owner;
    BucketInside bucket;
    Collider[] colliders;
    Renderer[] renderers;
    BoxCollider grainCollider;
    Vector3 originalColliderSize;
    float calm, age;

    public void Activate(Vector3 pos, DigSystem system, float volume)
    {
        owner = system;
        Volume = volume;
        if (!rb) rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        if (renderers == null)
        {
            renderers = GetComponentsInChildren<Renderer>();
            grainCollider = GetComponent<BoxCollider>();
            if (grainCollider) originalColliderSize = grainCollider.size;
        }
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
    }
    public void Drop(Vector3 velocity)
    {
        transform.SetParent(owner.transform, true);
        IsHeld = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        SetColliders(true);
        rb.linearVelocity = Vector3.ClampMagnitude(velocity, 5f);
        rb.WakeUp();
        calm = age = 0;
    }
    public void DropAsSand(Vector3 position, Vector3 velocity)
    {
        Drop(velocity);
        // This small body carries the excavated volume; the visible stream is granular.
        rb.position = position;
        transform.position = position;
        foreach (var renderer in renderers) renderer.enabled = false;
        if (grainCollider) grainCollider.size = originalColliderSize * 0.35f;
    }
    public void PrepareForPool()
    {
        if (bucket) bucket.Forget(this);
        bucket = null;
        IsHeld = false;
        transform.SetParent(owner.transform, true);
        gameObject.SetActive(false);
    }
    void SetColliders(bool value) { foreach (var c in colliders) c.enabled = value; }
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
