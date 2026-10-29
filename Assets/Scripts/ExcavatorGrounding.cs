using UnityEngine;

// A rigid tracked undercarriage: support comes from the track footprint, not the cab pivot.
[DefaultExecutionOrder(-50)]
public class ExcavatorGrounding : MonoBehaviour
{
    public Transform chassis, leftTrack, rightTrack;
    public Terrain terrain;
    [Range(5, 45)] public float maxDriveSlope = 32;
    [Range(5, 50)] public float maxBodyTilt = 40;
    [Min(1)] public float tiltSpeed = 18;
    [Min(0.01f)] public float groundClearance = 0.012f;
    [Min(0.03f)] public float maxStepHeight = 0.18f;
    [Min(0.05f)] public float bridgeGap = 0.25f;
    [Min(0.1f)] public float maxRiseSpeed = 2.5f;
    [Min(0.1f)] public float gravity = 9.81f;
    [Min(0.1f)] public float maxFallSpeed = 5;
    [Min(0.01f)] public float stabilityMargin = 0.08f;
    [Range(1,15)] public float maxHydraulicTilt = 10;
    [Min(1)] public float hydraulicTiltSpeed = 12;
    public float HydraulicLoad { get; private set; }
    public LayerMask groundLayers = ~0;
    public bool Ready { get; private set; }
    public bool IsGrounded { get; private set; }
    public float Slope { get; private set; }
    public float VerticalSpeed { get; private set; }
    public Vector3 SupportNormal { get; private set; } = Vector3.up;
    public Vector3 Pivot => chassis ? chassis.TransformPoint(localCenter) : transform.position;
    public float DriveFactor => IsGrounded ? Mathf.Lerp(1, 0.45f, Mathf.Clamp01(Slope / maxDriveSlope)) : 0;
    Vector3 forward = Vector3.forward, localCenter;
    readonly Vector3[] contacts = new Vector3[12];
    readonly Vector3[] feet = new Vector3[12];
    readonly Vector3[] surface = new Vector3[12];
    readonly bool[] found = new bool[12];
    readonly bool[] near = new bool[12];
    readonly RaycastHit[] hits = new RaycastHit[32];

    void Awake() { if (chassis) Initialize(chassis, Vector3.forward); }
    public bool Initialize(Transform root, Vector3 forwardAxis)
    {
        if (Ready) return true;
        chassis = root;
        if (!chassis || !leftTrack || !rightTrack) return false;
        forward = forwardAxis.sqrMagnitude > 0.01f ? forwardAxis.normalized : Vector3.forward;
        if (!TrackPoints(leftTrack, 0) || !TrackPoints(rightTrack, 6)) return false;
        localCenter = Vector3.zero;
        foreach (var point in contacts) localCenter += point / contacts.Length;
        Ready = true;
        Gather(chassis.position, chassis.rotation);
        if (Plane(false, out var normal))
        {
            var rotation = Rotation(chassis.rotation, LimitNormal(normal));
            float y = Height(rotation);
            // Small scene-placement errors are corrected before the first rendered frame; large gaps fall normally.
            if (Mathf.Abs(chassis.position.y - y) <= maxStepHeight)
                chassis.SetPositionAndRotation(new Vector3(chassis.position.x, y, chassis.position.z), rotation);
        }
        RefreshState();
        return true;
    }
    bool TrackPoints(Transform track, int offset)
    {
        var filter = track.GetComponent<MeshFilter>();
        if (!filter || !filter.sharedMesh) return false;
        var meshBounds = filter.sharedMesh.bounds;
        var frame = chassis.worldToLocalMatrix * track.localToWorldMatrix;
        var bounds = new Bounds(frame.MultiplyPoint3x4(meshBounds.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            bounds.Encapsulate(frame.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(x, y, z))));
        for (int side = 0; side < 2; side++)
        for (int row = 0; row < 3; row++)
            contacts[offset + side * 3 + row] = new Vector3(Mathf.Lerp(bounds.min.x, bounds.max.x, side == 0 ? 0.15f : 0.85f),
                bounds.min.y, Mathf.Lerp(bounds.min.z, bounds.max.z, 0.12f + row * 0.38f));
        return bounds.size.x > 0 && bounds.size.z > 0;
    }
    bool Ground(Vector3 foot, out float y)
    {
        bool valid = false; y = float.NegativeInfinity;
        var activeTerrain = terrain ? terrain : (DigSystem.I ? DigSystem.I.terrain : Terrain.activeTerrain);
        if (activeTerrain && activeTerrain.terrainData)
        {
            var p = foot - activeTerrain.transform.position; var size = activeTerrain.terrainData.size;
            if (p.x >= 0 && p.z >= 0 && p.x <= size.x && p.z <= size.z)
            { y = activeTerrain.SampleHeight(foot) + activeTerrain.transform.position.y; valid = true; }
        }
        int count = Physics.RaycastNonAlloc(foot + Vector3.up * 0.6f, Vector3.down, hits, 20,
            groundLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var c = hits[i].collider;
            if (!c || c is TerrainCollider || c is CharacterController || c.transform.IsChildOf(chassis) ||
                c.GetComponentInParent<DirtBlock>() || (c.attachedRigidbody && !c.attachedRigidbody.isKinematic) || hits[i].normal.y < 0.5f) continue;
            if (!valid || hits[i].point.y > y) { y = hits[i].point.y; valid = true; }
        }
        return valid;
    }
    void Gather(Vector3 position, Quaternion rotation)
    {
        for (int i = 0; i < contacts.Length; i++)
        {
            feet[i] = position + rotation * Vector3.Scale(contacts[i], chassis.lossyScale);
            found[i] = Ground(feet[i], out float height);
            surface[i] = new Vector3(feet[i].x, height, feet[i].z);
            near[i] = found[i] && feet[i].y - height <= bridgeGap;
        }
    }
    bool Plane(bool onlyNear, out Vector3 normal)
    {
        normal = Vector3.up;
        int left = 0, right = 0;
        for (int i = 0; i < contacts.Length; i++) if (found[i] && (!onlyNear || near[i])) { if (i < 6) left++; else right++; }
        if (left < 2 || right < 2 || left + right < 6) return false;
        var center = Vector3.zero;
        foreach (var point in feet) center += point / feet.Length;
        float minForward = float.PositiveInfinity, maxForward = float.NegativeInfinity;
        float minSide = float.PositiveInfinity, maxSide = float.NegativeInfinity;
        var ahead = Vector3.ProjectOnPlane(chassis.forward, Vector3.up).normalized;
        var side = Vector3.Cross(Vector3.up, ahead);
        for (int i = 0; i < 12; i++) if (found[i] && (!onlyNear || near[i]))
        {
            float f = Vector3.Dot(feet[i] - center, ahead), s = Vector3.Dot(feet[i] - center, side);
            minForward = Mathf.Min(minForward, f); maxForward = Mathf.Max(maxForward, f);
            minSide = Mathf.Min(minSide, s); maxSide = Mathf.Max(maxSide, s);
        }
        // Keep the center of the machine inside its supported footprint, with room to brake at an edge.
        if (minForward > -stabilityMargin || maxForward < stabilityMargin || minSide > -stabilityMargin || maxSide < stabilityMargin) return false;
        float lowest = float.PositiveInfinity; bool success = false;
        for (int a = 0; a < 10; a++)
        for (int b = a + 1; b < 11; b++)
        for (int c = b + 1; c < 12; c++)
        {
            if (!found[a] || !found[b] || !found[c] || (onlyNear && (!near[a] || !near[b] || !near[c]))) continue;
            var p = surface[a]; var u = surface[b] - p; var v = surface[c] - p;
            float determinant = u.x * v.z - v.x * u.z;
            if (Mathf.Abs(determinant) < 0.0001f) continue;
            float x = ((center.x - p.x) * v.z - v.x * (center.z - p.z)) / determinant;
            float z = (u.x * (center.z - p.z) - (center.x - p.x) * u.z) / determinant;
            if (x < -0.001f || z < -0.001f || x + z > 1.001f) continue;
            var n = Vector3.Cross(u, v).normalized; if (n.y < 0) n = -n;
            if (n.y < 0.05f) continue;
            float height = p.y - (n.x * (center.x - p.x) + n.z * (center.z - p.z)) / n.y;
            if (height >= lowest) continue;
            bool clear = true;
            for (int i = 0; i < 12; i++)
                if (found[i] && surface[i].y > p.y - (n.x * (surface[i].x - p.x) + n.z * (surface[i].z - p.z)) / n.y + 0.005f)
                { clear = false; break; }
            if (clear) { lowest = height; normal = n; success = true; }
        }
        return success;
    }
    Vector3 LimitNormal(Vector3 normal) => Vector3.RotateTowards(Vector3.up, normal, maxBodyTilt * Mathf.Deg2Rad, 0);
    Quaternion Rotation(Quaternion heading, Vector3 up)
    {
        var direction = Vector3.ProjectOnPlane(heading * forward, up).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.ProjectOnPlane(chassis.forward, up).normalized;
        return Quaternion.LookRotation(direction, up) * Quaternion.Inverse(Quaternion.LookRotation(forward, Vector3.up));
    }
    float Height(Quaternion rotation)
    {
        float y = float.NegativeInfinity;
        for (int i = 0; i < 12; i++) if (found[i])
            y = Mathf.Max(y, surface[i].y + groundClearance - (rotation * Vector3.Scale(contacts[i], chassis.lossyScale)).y);
        return y;
    }
    public void Settle(float dt)
    {
        HydraulicLoad = Mathf.MoveTowards(HydraulicLoad,0,dt*1.5f);
        if (!Ready || dt <= 0) return;
        Gather(chassis.position, chassis.rotation);
        bool supported = Plane(true, out var normal);
        if (!supported) supported = Plane(false, out normal);
        Quaternion rotation = chassis.rotation;
        if (supported) rotation = Rotation(chassis.rotation, Vector3.RotateTowards(chassis.up, LimitNormal(normal), tiltSpeed * Mathf.Deg2Rad * dt, 0));
        float floor = Height(rotation), y = chassis.position.y;
        if (floor > y)
        { y = Mathf.MoveTowards(y, floor, maxRiseSpeed * dt); VerticalSpeed = 0; }
        else
        {
            VerticalSpeed = Mathf.Max(VerticalSpeed - gravity * dt, -maxFallSpeed);
            y = Mathf.Max(floor, y + VerticalSpeed * dt);
            if (y <= floor + 0.001f) VerticalSpeed = 0;
        }
        chassis.SetPositionAndRotation(new Vector3(chassis.position.x, y, chassis.position.z), rotation);
        RefreshState();
    }
    public void RefreshState()
    {
        if (!Ready) return;
        Gather(chassis.position, chassis.rotation);
        bool stable = Plane(true, out var normal);
        SupportNormal = stable ? normal : chassis.up;
        Slope = Vector3.Angle(SupportNormal, Vector3.up);
        float gap = float.PositiveInfinity;
        for (int i = 0; i < 12; i++) if (found[i]) gap = Mathf.Min(gap, Mathf.Abs(feet[i].y - surface[i].y));
        IsGrounded = stable && gap <= 0.04f && Slope <= maxDriveSlope + 0.1f;
    }
    public bool TryMovePose(Vector3 position, Quaternion heading, float dt, out Vector3 fittedPosition, out Quaternion fittedRotation)
    {
        fittedPosition = position; fittedRotation = heading;
        if (!Ready) return false;
        Gather(position, heading);
        if (!Plane(true, out var normal) || Vector3.Angle(normal, Vector3.up) > maxDriveSlope) return false;
        // Settle handles pitch/roll once per physics tick; driving preserves that stance and changes heading.
        fittedRotation = Rotation(heading, chassis.up);
        float y = Height(fittedRotation);
        if (y - chassis.position.y > Mathf.Min(maxStepHeight, maxRiseSpeed * dt + 0.01f)) return false;
        if (chassis.position.y - y > bridgeGap) return false;
        // Descending a ledge loses contact and lets gravity settle the machine on subsequent ticks.
        fittedPosition.y = Mathf.Max(y, position.y);
        return true;
    }
    public void ReportHydraulicWork(float volume,float dt)
    {
        if(dt>0)HydraulicLoad=Mathf.Max(HydraulicLoad,Mathf.Clamp01(volume/dt/0.012f));
    }
    public bool TryHydraulicReaction(Vector3 tool,float depth,float dt)
    {
        if(!Ready || !Ignition.EngineRunning || depth<=0 || depth>0.08f || dt<=0)return false;
        Gather(chassis.position,chassis.rotation);
        var direction=Vector3.ProjectOnPlane(tool-Pivot,Vector3.up).normalized;
        if(direction.sqrMagnitude<0.5f)return false;
        float back=float.PositiveInfinity;
        for(int i=0;i<12;i++)if(found[i] && Mathf.Abs(feet[i].y-surface[i].y-groundClearance)<0.05f)
            back=Mathf.Min(back,Vector3.Dot(feet[i],direction));
        if(float.IsPositiveInfinity(back))return false;
        Vector3 pivot=Vector3.zero;int count=0;
        for(int i=0;i<12;i++)if(found[i] && Vector3.Dot(feet[i],direction)<=back+0.04f)
        {pivot+=new Vector3(feet[i].x,surface[i].y+groundClearance,feet[i].z);count++;}
        if(count==0)return false;pivot/=count;
        float arm=Vector3.Dot(tool-pivot,direction);
        if(arm<0.25f)return false;
        float angle=Mathf.Min(Mathf.Asin(Mathf.Clamp01((depth+0.001f)/arm))*Mathf.Rad2Deg,hydraulicTiltSpeed*dt);
        Quaternion turn=Quaternion.AngleAxis(angle,Vector3.Cross(direction,Vector3.up));
        Quaternion rotation=turn*chassis.rotation;
        if(Vector3.Angle(rotation*Vector3.up,SupportNormal)>maxHydraulicTilt || Vector3.Angle(rotation*Vector3.up,Vector3.up)>maxBodyTilt)return false;
        Vector3 oldPosition=chassis.position;Quaternion oldRotation=chassis.rotation;
        chassis.SetPositionAndRotation(pivot+turn*(chassis.position-pivot),rotation);
        Gather(chassis.position,chassis.rotation);
        for(int i=0;i<12;i++)if(found[i] && feet[i].y<surface[i].y-0.01f)
        {chassis.SetPositionAndRotation(oldPosition,oldRotation);RefreshState();return false;}
        HydraulicLoad=1;
        RefreshState();return true;
    }
    public Vector3 SupportPoint(int index) => chassis.TransformPoint(contacts[index]);
}
