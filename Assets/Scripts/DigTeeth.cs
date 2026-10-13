using UnityEngine;

public class DigTeeth : MonoBehaviour
{
    public Transform tip;                    // tuščias objektas ties dantų galiukais
    public float radius = 0.25f;
    public float maxDepthPerStep = 0.03f;
    public float minSpeed = 0.02f;           // m/s, kad stovintis kaušas nekastų

    Vector3 last;
    float carry;

    void Start() { last = tip.position; }

    void FixedUpdate()
    {
        var s = DigSystem.I;
        if (!s || !s.bucket) return;

        Vector3 p = tip.position;
        float speed = (p - last).magnitude / Time.fixedDeltaTime;
        last = p;

        float surf = s.SurfaceY(p);
        if (p.y >= surf - 0.01f || speed < minSpeed) return;
        if (s.bucket.IsFull || !s.CanSpawn) return;

        carry += s.Dig(p, radius, Mathf.Min(surf - p.y, maxDepthPerStep));

        while (carry >= s.BlockVolume && s.CanSpawn && !s.bucket.IsFull)
        {
            carry -= s.BlockVolume;
            s.Spawn(s.bucket.spawnPoint.position + Random.insideUnitSphere * 0.05f);
        }
    }
}