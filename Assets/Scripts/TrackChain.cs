using System.Collections.Generic;
using UnityEngine;

// Vikšro grandinė: grandys juda uždara trajektorija aplink ratus.
public class TrackChain : MonoBehaviour
{
    public TrackDrive drive;
    public bool rightSide;                       // false = kairysis vikšras, true = dešinysis
    public bool invert;                          // jei grandys juda ne ta kryptimi
    public Transform[] path;                     // taškai aplink ratus (uždara kilpa, eilės tvarka)
    public Transform linkPrefab;                 // viena grandis (forward = judėjimo kryptis, up = į išorę)
    [Min(4)] public int linkCount = 60;
    public Vector3 sideAxis = Vector3.right;     // ašis skersai vikšro (plotis)
    [Range(4, 32)] public int samplesPerSegment = 12;
    float outwardSign = 1f;
    Vector3[] samplePos;
    float[] cumulative;
    float total;
    Transform[] links;
    float offset;
    Vector3 centroid;

    void Start() { Build(); }

    public void Build()
    {
        if (links != null || path == null || path.Length < 3 || !linkPrefab || !drive) return;

        var pts = new Vector3[path.Length];
        centroid = Vector3.zero;
        for (int i = 0; i < pts.Length; i++)
        {
            pts[i] = transform.InverseTransformPoint(path[i].position);
            centroid += pts[i];
        }
        centroid /= pts.Length;

        var list = new List<Vector3>();
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            Vector3 p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n];
            for (int s = 0; s < samplesPerSegment; s++)
            {
                float t = s / (float)samplesPerSegment, t2 = t * t, t3 = t2 * t;
                list.Add(0.5f * (2f * p1 + (-p0 + p2) * t +
                    (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
            }
        }
        samplePos = list.ToArray();
        cumulative = new float[samplePos.Length + 1];
        for (int i = 0; i < samplePos.Length; i++)
            cumulative[i + 1] = cumulative[i] + Vector3.Distance(samplePos[i], samplePos[(i + 1) % samplePos.Length]);
        total = cumulative[samplePos.Length];

        links = new Transform[linkCount];
        for (int i = 0; i < linkCount; i++)
        {
            links[i] = Instantiate(linkPrefab, transform);
            links[i].gameObject.SetActive(true);
        }

        Vector3 sideN = sideAxis.normalized;
        float area = 0f;
        for (int i = 0; i < samplePos.Length; i++)
        {
            Vector3 a = samplePos[i], b = samplePos[(i + 1) % samplePos.Length];
            Vector3 pa = Vector3.ProjectOnPlane(a, sideN), pb = Vector3.ProjectOnPlane(b, sideN);
            area += Vector3.Dot(Vector3.Cross(pa, pb), sideN);
        }
        outwardSign = area >= 0f ? -1f : 1f;

        Place();
    }

    public void Step(float dt)
    {
        if (links == null || !drive) return;
        float speed = rightSide ? drive.RightSpeed : drive.LeftSpeed;
        float scale = Mathf.Max(0.0001f, transform.lossyScale.z);
        offset += (invert ? -speed : speed) * dt / scale;
        Place();
    }

    void Update() { Step(Time.deltaTime); }

    void Place()
    {
        Vector3 side = sideAxis.normalized;
        float spacing = total / links.Length;
        for (int i = 0; i < links.Length; i++)
        {
            float d = Mathf.Repeat(offset + i * spacing, total);
            Sample(d, out Vector3 pos, out Vector3 tangent);
            Vector3 up = Vector3.Cross(side, tangent).normalized * outwardSign;
            links[i].localPosition = pos;
            links[i].localRotation = Quaternion.LookRotation(tangent, up);
        }
    }

    void Sample(float d, out Vector3 pos, out Vector3 tangent)
    {
        int lo = 0, hi = samplePos.Length;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (cumulative[mid] <= d) lo = mid; else hi = mid;
        }
        Vector3 a = samplePos[lo], b = samplePos[(lo + 1) % samplePos.Length];
        float seg = Mathf.Max(0.00001f, cumulative[lo + 1] - cumulative[lo]);
        pos = Vector3.Lerp(a, b, (d - cumulative[lo]) / seg);
        tangent = (b - a).normalized;
    }

    void OnDrawGizmosSelected()
    {
        if (path == null) return;
        Gizmos.color = Color.yellow;
        for (int i = 0; i < path.Length; i++)
        {
            if (!path[i]) continue;
            var next = path[(i + 1) % path.Length];
            Gizmos.DrawSphere(path[i].position, 0.015f);
            if (next) Gizmos.DrawLine(path[i].position, next.position);
        }
    }
}