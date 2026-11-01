using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(500)]
public partial class DigSystem : MonoBehaviour
{
    public static DigSystem I;
    public Terrain terrain;
    public DirtBlock blockPrefab;
    public BucketInside bucket;
    public SoilWorkFeedback feedback;
    public ExcavatorGrounding workMachine;
    [Min(2)] public float maximumWorkDistance=8;
    [Range(.01f,.15f)] public float looseSoilEdgeGap=.025f;
    bool CanDepositAt(Vector3 point) => !workMachine || !workMachine.CoversSoilPoint(point);
    float DepositWeight(int x0,int z0,int x,int z,float cx,float cz,float rx,float rz)
    {
        if(!workMachine)return Weight(x0,z0,x,z,cx,cz,rx,rz);
        var origin=terrain.transform.position;var cell=terrain.terrainData.heightmapScale;
        var point=origin+new Vector3((x0+x)*cell.x,0,(z0+z)*cell.z);
        return CanDepositAt(point)?Weight(x0,z0,x,z,cx,cz,rx,rz):0;
    }
    public bool IsWorkPoint(Vector3 point)
    {
        if(!float.IsFinite(point.x) || !float.IsFinite(point.y) || !float.IsFinite(point.z))return false;
        return !workMachine || Vector3.ProjectOnPlane(point-workMachine.Pivot,Vector3.up).sqrMagnitude<=maximumWorkDistance*maximumWorkDistance;
    }
    [Min(0.01f)] public float blockSize = 0.06f;
    [Min(1)] public int maxBlocks = 250;
    [Range(8,64)] public int maxLooseBodies = 32;
    public int LooseBodyCount {get {int count=0;foreach(var block in active)if(block && block.gameObject.activeSelf && !block.IsHeld)count++;return count;}}
    public int AvailableLooseBodies => Mathf.Max(0,maxLooseBodies-LooseBodyCount);
    readonly Queue<DirtBlock> pool = new Queue<DirtBlock>();
    readonly HashSet<DirtBlock> active = new HashSet<DirtBlock>();
    TerrainData original, runtimeData;
    TerrainData pushBaseData;
    float[,] pushBaseHeights;
    bool terrainDirty, paintDirty;
    Vector2 paintMin, paintMax;
    float terrainSyncClock;
    readonly Queue<int> paintQueue=new Queue<int>();
    readonly HashSet<int> pendingPaint=new HashSet<int>();
    float[,,] paintTile;
    [Range(1,8)] public int paintTilesPerFlush=2;
    void FixedUpdate()
    {
        terrainSyncClock += Time.fixedDeltaTime;
        if (terrainSyncClock >= .08f) {StepSoil(terrainSyncClock);terrainSyncClock=0;}
        // One commit after all arm/track/jaw producers, before the native physics simulation.
        FlushTerrainChanges(terrainSyncClock==0);
    }
    public void FlushTerrainChanges(bool paint=true)
    {
        if (!terrain || !terrain.terrainData) return;
        if (terrainDirty)
        {writingHeights=true;try{terrain.terrainData.SyncHeightmap();SyncedTerrainRevision=TerrainRevision;}finally{writingHeights=false;}}
        terrainDirty = false;
        if (paint && (paintDirty || paintQueue.Count>0)) { PaintChangedSurface(); paintDirty = false; }
    }
    public float BlockVolume => blockSize * blockSize * blockSize;
    void RememberSoilSurface()
    {
        var td = terrain.terrainData;
        if (pushBaseData == td && pushBaseHeights != null) return;
        pushBaseData = td;
        pushBaseHeights = td.GetHeights(0, 0, td.heightmapResolution, td.heightmapResolution);
    }
    // Redistribute a shallow strip ahead of a full bucket instead of deleting soil or teleporting through it.
    public float PushSoil(Vector3 point, Vector3 motion, float radius, float maxDepth)
    {
        var horizontal = Vector3.ProjectOnPlane(motion, Vector3.up);
        if (horizontal.magnitude < 0.0001f || horizontal.magnitude < Mathf.Abs(motion.y) || !ContainsTerrainPoint(point)) return 0;
        var destination = point + horizontal.normalized * (radius + looseSoilEdgeGap);
        if (!ContainsTerrainPoint(destination)) return 0;
        float penetration = SurfaceY(point) - point.y;
        if (penetration <= 0) return 0;
        float budget = horizontal.magnitude * radius * 2f * Mathf.Min(penetration, maxDepth);
        return MoveSoilLocally(point,radius,Mathf.Min(penetration,maxDepth),budget,point.y,destination);
    }
    public int FreeBlocks => Mathf.Max(0, maxBlocks - active.Count);
    public float PushBucketSoil(BucketInside container, Vector3 motion, float maxDepth)
    {
        if (!container || container != bucket) return 0;
        return PushToolSoil(container.transform.localToWorldMatrix,container.WorkBounds,motion,maxDepth,container.GradingContact);
    }
    public float PushAttachmentSoil(GroundedAttachment attachment, Vector3 motion, float maxDepth = 0.12f)
    {
        if(!attachment || !attachment.canPushSoil || !Ignition.EngineRunning || Vector3.ProjectOnPlane(motion,Vector3.up).magnitude < Mathf.Abs(motion.y)*0.25f) return 0;
        return PushToolSoil(attachment.transform.localToWorldMatrix,attachment.WorkBounds,motion,maxDepth,true,true);
    }
    float PushToolSoil(Matrix4x4 frame, Bounds work, Vector3 motion, float maxDepth, bool grading, bool blade = false)
    {
        var horizontal = Vector3.ProjectOnPlane(motion,Vector3.up);
        if(!terrain || maxDepth<=0 || horizontal.magnitude<=0.000001f) return 0;
        var td = terrain.terrainData;
        RememberSoilSurface();
        var origin = terrain.transform.position;
        var cell = td.heightmapScale;
        var bounds = new Bounds(frame.MultiplyPoint3x4(work.center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
            bounds.Encapsulate(frame.MultiplyPoint3x4(work.center + Vector3.Scale(work.extents, new Vector3(x, y, z))));
        if(!IsWorkPoint(bounds.min) || !IsWorkPoint(bounds.max))return 0;
        var direction = horizontal.normalized;
        float forwardExtent = Mathf.Abs(direction.x) * bounds.extents.x + Mathf.Abs(direction.z) * bounds.extents.z;
        float width = Mathf.Abs(direction.z) * bounds.size.x + Mathf.Abs(direction.x) * bounds.size.z;
        float radius = Mathf.Max(width * (grading ? 0.45f : 0.65f), Mathf.Max(cell.x, cell.z) * 2);
        var destination = bounds.center + direction * (forwardExtent + radius + looseSoilEdgeGap);
        if (!ContainsTerrainPoint(destination)) return 0;
        int res = td.heightmapResolution;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - origin.x) / cell.x) - 1, 0, res - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - origin.z) / cell.z) - 1, 0, res - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - origin.x) / cell.x) + 1, 0, res - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - origin.z) / cell.z) + 1, 0, res - 1);
        using var patch=EditHeights(x0,z0,x1-x0+1,z1-z0+1);
        var heights=patch.Heights;var cuts=patch.Scratch;
        var inverse = frame.inverse;
        var up = inverse.MultiplyVector(Vector3.up);
        var low = work.min;
        var high = work.max;
        float total = 0;
        for (int z = 0; z < heights.GetLength(0); z++)
        for (int x = 0; x < heights.GetLength(1); x++)
        {
            float wx = origin.x + (x0 + x) * cell.x, wz = origin.z + (z0 + z) * cell.z;
            if(!CanDepositAt(new Vector3(wx,0,wz)))continue;
            float bottom = float.PositiveInfinity;
            // Clear one interpolation cell around the tool so the heightmap cannot form an invisible rim.
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
                if (BoxBottom(inverse.MultiplyPoint3x4(new Vector3(wx + dx * cell.x, 0, wz + dz * cell.z)), up, low, high, out float y))
                    bottom = Mathf.Min(bottom, y);
            if (float.IsPositiveInfinity(bottom)) continue;
            float penetration = origin.y + heights[z, x] * td.size.y - bottom;
            float originalSurface = origin.y + pushBaseHeights[z0 + z, x0 + x] * td.size.y;
            // Deposited mounds remain pushable, while the underlying undisturbed soil stays protected.
            if (penetration <= 0.003f || (penetration>maxDepth && origin.y+heights[z,x]*td.size.y<=originalSurface+.003f) ||
                penetration > bounds.size.y + maxDepth) continue;
            float floor = Mathf.Clamp01((bottom + 0.003f - origin.y) / td.size.y);
            cuts[z, x] = Mathf.Max(0, heights[z, x] - floor);
            total += cuts[z, x];
        }
        float displaced = total * td.size.y * CellArea;
        if (displaced <= 0) return 0;
        // Deposit first and remove only what the destination accepts, preserving volume at terrain limits.
        float deposited;
        if(blade)
        {
            // A growing windrow escapes around the two ends of the blade instead of teleporting far ahead.
            var side=Vector3.Cross(Vector3.up,direction);
            float pile=Mathf.Max(0,SurfaceY(bounds.center)-bounds.min.y);
            float sideFraction=Mathf.Clamp(0.2f+pile*2,0.2f,0.6f);
            float sideRadius=Mathf.Max(width*0.22f,Mathf.Max(cell.x,cell.z)*1.5f);
            var edge=bounds.center+direction*(forwardExtent+sideRadius+looseSoilEdgeGap);
            deposited=Add(edge+side*(width*0.5f+looseSoilEdgeGap),sideRadius,displaced*sideFraction*0.5f,true);
            deposited+=Add(edge-side*(width*0.5f+looseSoilEdgeGap),sideRadius,displaced*sideFraction*0.5f,true);
            deposited+=FillLowSpots(destination,radius,bounds.min.y+0.003f,displaced-deposited);
            deposited+=Add(destination,radius,displaced-deposited,true);
            float resistance=SoilAt(new Vector3(bounds.center.x,SurfaceY(bounds.center),bounds.center.z)).resistance;
            LastBladeLoad=Mathf.Clamp01(displaced*resistance/Mathf.Max(0.0001f,horizontal.magnitude*width*0.15f));
        }
        else
        {
            deposited=grading?FillLowSpots(destination,radius,bounds.min.y+0.003f,displaced):0;
            deposited+=Add(destination,radius,displaced-deposited,true);
        }
        if (deposited <= 0) return 0;
        float ratio = Mathf.Clamp01(deposited / displaced);
        for (int z = 0; z < heights.GetLength(0); z++)
        for (int x = 0; x < heights.GetLength(1); x++) heights[z, x] -= cuts[z, x] * ratio;
        patch.Apply();
        terrainDirty = true;
        Disturb(bounds.center,Mathf.Max(bounds.extents.x,bounds.extents.z)+Mathf.Max(cell.x,cell.z));
        PaintDumped(bounds.center, Mathf.Max(bounds.extents.x, bounds.extents.z), 1);
        return deposited;
    }
    float FillLowSpots(Vector3 point, float radius, float gradeY, float volume)
    {
        if (volume <= 0 || !CanDepositAt(point) || !GetWindow(point, radius, out int x0, out int z0, out int w, out int h,
            out float cx, out float cz, out float rx, out float rz)) return 0;
        var td = terrain.terrainData;
        using var patch=EditHeights(x0,z0,w,h);
        var heights=patch.Heights;var needed=patch.Scratch;
        float target = Mathf.Clamp01((gradeY - terrain.transform.position.y) / td.size.y), sum = 0;
        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            needed[z, x] = Mathf.Max(0, target - heights[z, x]) * DepositWeight(x0, z0, x, z, cx, cz, rx, rz);
            sum += needed[z, x];
        }
        if (sum <= 0) return 0;
        float ratio = Mathf.Min(1, volume / (sum * td.size.y * CellArea)), added = 0;
        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            float before = heights[z, x];
            heights[z, x] += needed[z, x] * ratio;
            added += heights[z, x] - before;
        }
        patch.Apply(); terrainDirty = true;
        Disturb(point,radius);
        PaintDumped(point, radius, 1);
        return Mathf.Min(volume, added * td.size.y * CellArea);
    }
    static bool BoxBottom(Vector3 point, Vector3 up, Vector3 low, Vector3 high, out float bottom)
    {
        bottom = float.NegativeInfinity;
        float top = float.PositiveInfinity;
        for (int axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(up[axis]) < 0.000001f)
            {
                if (point[axis] < low[axis] || point[axis] > high[axis]) return false;
                continue;
            }
            float a = (low[axis] - point[axis]) / up[axis], b = (high[axis] - point[axis]) / up[axis];
            bottom = Mathf.Max(bottom, Mathf.Min(a, b));
            top = Mathf.Min(top, Mathf.Max(a, b));
            if (bottom > top) return false;
        }
        return true;
    }
    public bool CanSpawn => FreeBlocks > 0 && AvailableLooseBodies>0 && blockPrefab;
    float CellArea => terrain.terrainData.heightmapScale.x * terrain.terrainData.heightmapScale.z;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { I = null; }
    void Awake()
    {
        if (!terrain || !terrain.terrainData)
        {
            Debug.LogError("DigSystem requires terrain", this);
            enabled = false;
            return;
        }
        I = this;
        if(!workMachine)workMachine=FindAnyObjectByType<ExcavatorGrounding>();
        if(!simulationObserver && Camera.main)simulationObserver=Camera.main.transform;
        original = terrain.terrainData;
        runtimeData = Instantiate(original);
        terrain.terrainData = runtimeData;
        RememberSoilSurface();
        var collider = terrain.GetComponent<TerrainCollider>();
        if (collider) collider.terrainData = runtimeData;
        ApplyEarthGround(runtimeData);
    }
    void OnDestroy()
    {
        StopObservingHeights();
        if (I == this) I = null;
        if (!runtimeData) return;
        if (terrain)
        {
            terrain.terrainData = original;
            var collider = terrain.GetComponent<TerrainCollider>();
            if (collider) collider.terrainData = original;
        }
        Destroy(runtimeData);
    }

    // Visas terrain padengiamas šviesia žeme; antras sluoksnis (tamsi žemė) pradžioje nematomas.
    static void ApplyEarthGround(TerrainData td)
    {
        td.terrainLayers = new[] { SoilClodVisual.GroundLayer, SoilClodVisual.DumpedLayer };
        td.alphamapResolution = Mathf.Max(td.alphamapResolution,Mathf.Min(1024,td.heightmapResolution-1));
        int r = td.alphamapResolution;
        var alpha = new float[r, r, 2];
        for (int y = 0; y < r; y++)
            for (int x = 0; x < r; x++) alpha[y, x, 0] = 1f;
        td.SetAlphamaps(0, 0, alpha);
    }

    // Užtepa tamsią žemę ten, kur supilta arba iškasta.
    void PaintDumped(Vector3 world, float radius, float strength)
    {
        var min=new Vector2(world.x-radius,world.z-radius); var max=new Vector2(world.x+radius,world.z+radius);
        if(!paintDirty) {paintMin=min;paintMax=max;}else{paintMin=Vector2.Min(paintMin,min);paintMax=Vector2.Max(paintMax,max);}
        paintDirty=true;
    }
    void PaintChangedSurface()
    {
        var td=terrain.terrainData;
        if(td.alphamapLayers!=2 || pushBaseHeights==null) return;
        int r=td.alphamapResolution, hr=td.heightmapResolution; var origin=terrain.transform.position;
        int x0=Mathf.Clamp(Mathf.FloorToInt((paintMin.x-origin.x)/td.size.x*r),0,r-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((paintMin.y-origin.z)/td.size.z*r),0,r-1);
        int x1=Mathf.Clamp(Mathf.CeilToInt((paintMax.x-origin.x)/td.size.x*r),0,r-1);
        int z1=Mathf.Clamp(Mathf.CeilToInt((paintMax.y-origin.z)/td.size.z*r),0,r-1);
        int tileSize=Mathf.Min(32,r),tiles=r/tileSize;
        if(paintDirty)
            for(int tz=z0/tileSize;tz<=z1/tileSize;tz++)for(int tx=x0/tileSize;tx<=x1/tileSize;tx++)
            {int key=tx+tz*tiles;if(pendingPaint.Add(key))paintQueue.Enqueue(key);}
        if(paintTile==null || paintTile.GetLength(0)!=tileSize)paintTile=new float[tileSize,tileSize,2];
        int count=Mathf.Min(paintTilesPerFlush,paintQueue.Count);
        for(int n=0;n<count;n++)
        {
            int key=paintQueue.Dequeue();pendingPaint.Remove(key);int px=key%tiles*tileSize,pz=key/tiles*tileSize;
            for(int z=0;z<tileSize;z++)for(int x=0;x<tileSize;x++)
            {
                float u=(px+x+0.5f)/r,v=(pz+z+0.5f)/r;
                float hx=u*(hr-1),hz=v*(hr-1);int ix=Mathf.Min(hr-2,(int)hx),iz=Mathf.Min(hr-2,(int)hz);
                bool touched=modifiedCells!=null && (modifiedCells[ix+iz*hr]!=0 || modifiedCells[ix+1+iz*hr]!=0 || modifiedCells[ix+(iz+1)*hr]!=0 || modifiedCells[ix+1+(iz+1)*hr]!=0);
                float original=TriangleHeight(pushBaseHeights,ix,iz,hx-ix,hz-iz)*td.size.y;
                // Paint the rendered terrain's interpolation, not the contact grid's interpolation at a sharp rim.
                float changed=Mathf.Abs(td.GetInterpolatedHeight(u,v)-original);
                float dark=touched?Mathf.SmoothStep(0,0.85f,Mathf.InverseLerp(0.003f,0.05f,changed)):0;
                paintTile[z,x,1]=dark;paintTile[z,x,0]=1-dark;
            }
            td.SetAlphamaps(px,pz,paintTile);
        }
    }

    public bool ContainsTerrainPoint(Vector3 point)
    {
        if (!terrain || !terrain.terrainData) return false;
        Vector3 local = point - terrain.transform.position, size = terrain.terrainData.size;
        return local.x >= 0 && local.z >= 0 && local.x <= size.x && local.z <= size.z;
    }
    public float SurfaceY(Vector3 point) => CachedSurfaceY(point);

    bool GetWindow(Vector3 world, float radius, out int x0, out int z0, out int w, out int h,
        out float cx, out float cz, out float rx, out float rz)
    {
        x0 = z0 = w = h = 0; cx = cz = rx = rz = 0;
        if (radius <= 0 || !float.IsFinite(radius) || !IsWorkPoint(world) || !ContainsTerrainPoint(world)) return false;
        if(workMachine && Vector3.ProjectOnPlane(world-workMachine.Pivot,Vector3.up).magnitude+radius>maximumWorkDistance)return false;
        var td = terrain.terrainData;
        int res = td.heightmapResolution;
        Vector3 local = world - terrain.transform.position;
        // Keep a brush smaller than a heightmap cell from falling between all samples.
        radius = Mathf.Max(radius, Mathf.Max(td.heightmapScale.x, td.heightmapScale.z) * 0.8f);
        cx = local.x / td.size.x * (res - 1);
        cz = local.z / td.size.z * (res - 1);
        rx = radius / td.size.x * (res - 1);
        rz = radius / td.size.z * (res - 1);
        x0 = Mathf.Max(0, Mathf.FloorToInt(cx - rx));
        z0 = Mathf.Max(0, Mathf.FloorToInt(cz - rz));
        w = Mathf.Min(res - 1, Mathf.CeilToInt(cx + rx)) - x0 + 1;
        h = Mathf.Min(res - 1, Mathf.CeilToInt(cz + rz)) - z0 + 1;
        return w > 0 && h > 0;
    }
    static float Weight(int x0, int z0, int x, int z, float cx, float cz, float rx, float rz)
    {
        float dx = (x0 + x - cx) / rx, dz = (z0 + z - cz) / rz;
        return Mathf.Max(0f, 1f - dx * dx - dz * dz);
    }
    public float Dig(Vector3 point, float radius, float depth, float maxVolume = float.PositiveInfinity,
        float floorY = float.NegativeInfinity)
    {
        if (!IsWorkPoint(point) || depth <= 0 || maxVolume <= 0 || !GetWindow(point, radius, out int x0, out int z0,
            out int w, out int h, out float cx, out float cz, out float rx, out float rz)) return 0f;
        var td = terrain.terrainData;
        RememberSoilSurface();
        using var patch=EditHeights(x0,z0,w,h);
        var heights=patch.Heights;var cuts=patch.Scratch;
        float sum = 0;
        float floor = Mathf.Max(0, (floorY - terrain.transform.position.y) / td.size.y);
        var cell=td.heightmapScale;var origin=terrain.transform.position;
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                var vertex=origin+new Vector3((x0+x)*cell.x,0,(z0+z)*cell.z);
                if(!CanDepositAt(vertex)){cuts[z,x]=0;continue;}
                float cut = Mathf.Min(depth * Weight(x0, z0, x, z, cx, cz, rx, rz) / td.size.y,
                    Mathf.Max(0, heights[z, x] - floor));
                cuts[z, x] = cut;
                sum += cut;
            }
        float available = sum * td.size.y * CellArea;
        if (available <= 0) return 0;
        float ratio = Mathf.Min(1f, maxVolume / available);
        float removed = 0;
        for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float before = heights[z, x];
                heights[z, x] -= cuts[z, x] * ratio;
                removed += before - heights[z, x];
            }
        patch.Apply();
        if (removed > 0f) {Disturb(point,radius); PaintDumped(point, radius * 1.1f, 1f);}
        return removed * td.size.y * CellArea;
    }
    // Return the accepted volume so a saturated terrain never silently deletes soil.
    public float Add(Vector3 point, float radius, float volume, bool deferUpdates = false, SoilKind? material = null)
    {
        if (!IsWorkPoint(point) || !CanDepositAt(point) || volume <= 0 || !GetWindow(point, radius, out int x0, out int z0, out int w, out int h,
            out float cx, out float cz, out float rx, out float rz)) return 0f;
        var td = terrain.terrainData;
        RememberSoilSurface();
        using var patch=EditHeights(x0,z0,w,h);
        var heights=patch.Heights;
        float remaining = volume / (td.size.y * CellArea), accepted = 0;
        // Redistribute overflow among cells still below the maximum height.
        for (int pass = 0; pass < w * h && remaining > 0.00000001f; pass++)
        {
            float sumW = 0;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                    if (heights[z, x] < 1f) sumW += DepositWeight(x0, z0, x, z, cx, cz, rx, rz);
            if (sumW <= 0) break;
            float added = 0;
            bool clipped = false;
            for (int z = 0; z < h; z++)
                for (int x = 0; x < w; x++)
                {
                    float desired = remaining * DepositWeight(x0, z0, x, z, cx, cz, rx, rz) / sumW;
                    float before = heights[z, x];
                    heights[z, x] = Mathf.Min(1f, before + desired);
                    added += heights[z, x] - before;
                    clipped |= desired > 1f - before;
                }
            accepted += added;
            remaining = Mathf.Max(0, remaining - added);
            if (!clipped || added <= 0) break;
        }
        if (deferUpdates)
        {
            patch.Apply();
            terrainDirty = true;
        }
        else patch.Apply();
        if (accepted > 0f)
        {
            // Coarse material tiles bound memory independently of the number of falling parcels.
            if(material.HasValue)
            {
                int tiles=(td.heightmapResolution-2)/TileSize+1;
                for(int z=0;z<h;z++)for(int x=0;x<w;x++)if(heights[z,x]>patch.Original[z,x]+1e-7f)
                    depositedMaterials[(x0+x)/TileSize+(z0+z)/TileSize*tiles]=material.Value;
            }
            Disturb(point,radius); PaintDumped(point, radius * 1.3f, 0.5f);
        }
        return Mathf.Min(volume, accepted * td.size.y * CellArea);
    }
    public DirtBlock Spawn(Vector3 pos, float? volume = null, int fractureLevel = 0, SoilKind kind = SoilKind.Loam)
    {
        float amount = volume ?? BlockVolume;
        if (!IsWorkPoint(pos) || !CanSpawn || amount <= 0 || float.IsNaN(amount) || float.IsInfinity(amount)) return null;
        var block = pool.Count > 0 ? pool.Dequeue() : Instantiate(blockPrefab, transform);
        block.transform.SetParent(null, false);
        block.transform.localScale = Vector3.one * Mathf.Pow(amount, 1f / 3f);
        block.transform.SetParent(transform, true);
        active.Add(block);
        block.Activate(pos, this, amount, fractureLevel,kind);
        IgnoreSoilCollisions(block);
        return block;
    }
    public void IgnoreSoilCollisions(DirtBlock block)
    {
        foreach (var other in active) block.IgnoreSoilCollision(other);
    }
    public void Release(DirtBlock block)
    {
        if (!block || !active.Remove(block)) return;
        block.PrepareForPool();
        pool.Enqueue(block);
    }
}
