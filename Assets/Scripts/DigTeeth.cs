using UnityEngine;

public class DigTeeth : MonoBehaviour
{
    public Transform tip;
    [Min(0.01f)] public float radius = 0.25f;
    [Min(0.001f)] public float maxDepthPerStep = 0.03f;
    [Min(0f)] public float minSpeed = 0.02f;
    [Range(0.1f, 2f)] public float cutPerMeter = 0.6f;
    [Min(0.1f)] public float maxSweepDistance = 1f;
    [Range(0.02f, 0.3f)] public float maxPushDepth = 0.16f;
    [Range(3,9)] public int toothCount = 5;
    readonly Vector3[] toothStart=new Vector3[9];
    bool toothSweepPrepared;
    SoilKind carriedKind=SoilKind.Loam;
    Vector3 last;
    public float LastResistance {get;private set;}
    public void BeginToothSweep()
    {
        if(!tip || !DigSystem.I || !DigSystem.I.bucket)return;
        for(int i=0;i<Mathf.Clamp(toothCount,3,9);i++)toothStart[i]=ToothPosition(i);
        toothSweepPrepared=true;
    }
    public Vector3 ToothPosition(int index)
    {
        var bucket=DigSystem.I?DigSystem.I.bucket:null;
        if(!bucket || !tip)return tip?tip.position:transform.position;
        var box=bucket.GetComponent<BoxCollider>();
        float width=box.size.x*Mathf.Abs(bucket.transform.lossyScale.x)*0.9f;
        return tip.position+bucket.transform.right*((index/(float)(Mathf.Clamp(toothCount,3,9)-1)-0.5f)*width);
    }
    float carry;
    public float PendingVolume => carry;
    public float LastWorkVolume { get; private set; }
    public void ResetSweep() { if (tip) last = tip.position; toothSweepPrepared=false; }
    void OnEnable() { if (tip) last = tip.position; }
    void FixedUpdate()
    {
        // Only commanded attachment/chassis sweeps excavate; gravity and tracking corrections must not scoop.
        ResetSweep();
        var s = DigSystem.I;
        if (carry > 0 && s && s.bucket && s.bucket.IsDumping && s.CanSpawn)
        {
            var clod = s.Spawn(s.bucket.PourPosition(0), carry,kind:carriedKind);
            if (clod) { clod.DropAsSoil(clod.transform.position, Vector3.down * 0.1f); carry = 0; }
        }
    }
    public void Sweep(Vector3 start, Vector3 p, float dt, Vector3? bodyMotion = null, bool closingCurl = false)
    {
        LastWorkVolume = 0; LastResistance=0;
        if (!tip) return;
        last = p;
        var s = DigSystem.I;
        var motion = p - start;
        if (bodyMotion.HasValue && motion.sqrMagnitude < bodyMotion.Value.sqrMagnitude * 0.1f) motion = bodyMotion.Value;
        float distance = motion.magnitude;
        if (!s || !s.bucket || !Ignition.EngineRunning) return;
        if (distance <= 0.000001f || distance > maxSweepDistance) return;
        s.bucket.GradingContact = !closingCurl && (!CanCut(s.bucket, motion) || IsBackDragging(s.bucket, motion));
        int slots = Mathf.Min(s.bucket.FreeSlots(s.blockSize), s.FreeBlocks);
        float budget = s.CanSpawn?Mathf.Max(0f,slots*s.BlockVolume-carry):0;
        if (budget <= 0f || s.bucket.GradingContact)
        {
            LastWorkVolume = s.PushBucketSoil(s.bucket, motion, maxPushDepth);
            return;
        }
        if (distance < minSpeed * dt) return;
        int count=Mathf.Clamp(toothCount,3,9);
        var box=s.bucket.GetComponent<BoxCollider>();
        float width=box.size.x*Mathf.Abs(s.bucket.transform.lossyScale.x)*0.9f;
        float toothRadius=Mathf.Min(radius,Mathf.Max(width/(count-1)*0.65f,0.015f));
        for(int tooth=0;tooth<count && budget>0;tooth++)
        {
            Vector3 end=ToothPosition(tooth);
            Vector3 begin=toothSweepPrepared?toothStart[tooth]:end-(p-start);
            float stroke=Vector3.Distance(begin,end);
            int steps=Mathf.Clamp(Mathf.CeilToInt(stroke/Mathf.Max(toothRadius*0.5f,0.01f)),1,16);
            for(int i=1;i<=steps && budget>0;i++)
            {
                var point=Vector3.Lerp(begin,end,i/(float)steps);
                if(!s.ContainsTerrainPoint(point))continue;
                float penetration=s.SurfaceY(point)-point.y;
                if(penetration<=0.005f)continue;
                var soil=s.SoilAt(point);carriedKind=s.KindAt(point);
                float attack=Mathf.Lerp(0.65f,1,Mathf.Clamp01(Vector3.Dot(motion.normalized,s.bucket.transform.TransformDirection(s.bucket.lipAxis.normalized))));
                float resistance=soil.resistance*(1+Mathf.Clamp01(penetration/0.25f))*(1+Mathf.Clamp(stroke/Mathf.Max(dt,0.001f),0,3)*0.25f)/attack;
                LastResistance=Mathf.Max(LastResistance,resistance);
                float depth=Mathf.Min(penetration,maxDepthPerStep,stroke/steps*cutPerMeter)/Mathf.Max(0.5f,resistance);
                float removed=s.Dig(point,toothRadius,depth,budget,point.y);
                LastWorkVolume+=removed;carry+=removed;budget-=removed;
            }
        }
        toothSweepPrepared=false;
        while (carry + s.BlockVolume * 0.0001f >= s.BlockVolume && s.CanSpawn)
        {
            if (!s.bucket.TryGetSpawnPosition(s.blockSize, out var position)) break;
            var block = s.Spawn(position,kind:carriedKind);
            if (!block) break;
            s.bucket.Capture(block);
            carry = Mathf.Max(0f, carry - s.BlockVolume);
        }
    }
    public static bool CanCut(BucketInside container, Vector3 motion)
    {
        if (!container || !container.pourPoint || motion.sqrMagnitude < 1e-12f) return false;
        var box = container.GetComponent<BoxCollider>();
        var leading = Vector3.ProjectOnPlane(container.pourPoint.position-container.transform.TransformPoint(box.center),Vector3.up);
        var horizontal = Vector3.ProjectOnPlane(motion,Vector3.up);
        if (leading.sqrMagnitude>1e-8f && Vector3.Dot(horizontal,leading.normalized)>motion.magnitude*0.15f) return true;
        var b=container.WorkBounds; float bottom=float.PositiveInfinity;
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            bottom=Mathf.Min(bottom,container.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))).y);
        // A downward bite requires the teeth to be the lowest edge, rather than pressing the bucket floor.
        return motion.y < -motion.magnitude*0.2f && container.pourPoint.position.y<=bottom+0.04f &&
            Vector3.Dot(container.transform.TransformDirection(container.openingAxis.normalized),Vector3.up)<0.75f;
    }
    public static bool IsBackDragging(BucketInside container, Vector3 motion)
    {
        if (!container || !container.pourPoint) return false;
        var planar = Vector3.ProjectOnPlane(motion, Vector3.up);
        var box = container.GetComponent<BoxCollider>();
        var leading = Vector3.ProjectOnPlane(container.pourPoint.position - container.transform.TransformPoint(box.center), Vector3.up);
        return planar.sqrMagnitude > 0.000000000001f && leading.sqrMagnitude > 0.00001f &&
            Mathf.Abs(motion.y) < planar.magnitude &&
            Vector3.Dot(container.transform.TransformDirection(container.openingAxis.normalized), Vector3.up) > 0.7f &&
            Vector3.Dot(planar.normalized, leading.normalized) < -0.25f;
    }
}
