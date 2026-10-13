using UnityEngine;

public class DirtBlock : MonoBehaviour
{
    public Rigidbody rb;
    float calm;

    public void Activate(Vector3 pos)
    {
        transform.SetPositionAndRotation(pos, Random.rotation);
        gameObject.SetActive(true);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        calm = 0f;
    }

    void FixedUpdate()
    {
        var s = DigSystem.I;
        if (!s) return;

        calm = rb.linearVelocity.sqrMagnitude < 0.01f ? calm + Time.fixedDeltaTime : 0f;

        bool inBucket = s.bucket && s.bucket.Contains(this);
        bool fellThrough = transform.position.y < s.SurfaceY(transform.position) - 0.5f;

        if ((calm > 1.5f && !inBucket) || fellThrough)
        {
            s.Add(transform.position, 0.25f, s.BlockVolume);   // sugrįžta į Terrain
            s.Release(this);
        }
    }
}