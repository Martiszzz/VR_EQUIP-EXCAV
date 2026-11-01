using System.Collections.Generic;
using UnityEngine;

public partial class DigSystem
{
    public SoilKind surfaceSoil = SoilKind.Loam;
    public SoilMaterialProfile[] soilMaterials;
    public Transform simulationObserver;
    [Min(5)] public float activeSoilDistance=30;
    public bool depthLayers = true;
    [Min(0.05f)] public float clayDepth = 1.5f, compactedDepth = 3.5f;
    public bool simulateSlopes = true, makeTrackRuts = true;
    [Range(1,8)] public int soilTilesPerStep = 2;
    [Min(0.01f)] public float soilSlideSpeed = 0.45f;
    const int TileSize=16;
    readonly Queue<int> soilQueue=new Queue<int>();
    readonly Dictionary<int,int> soilLife=new Dictionary<int,int>();
    readonly Dictionary<int,SoilKind> depositedMaterials=new Dictionary<int,SoilKind>();
    int slidePhase;
    public float ActiveVolume {get {float volume=0;foreach(var clod in active)if(clod && clod.gameObject.activeSelf)volume+=clod.Volume;return volume;}}
    public int ActiveSoilTiles=>soilLife.Count;
    public float LastBladeLoad {get;private set;}

    public float OriginalSurfaceY(Vector3 point)
    {
        if(!ContainsTerrainPoint(point))return point.y;
        RememberSoilSurface();var td=terrain.terrainData;var p=point-terrain.transform.position;int r=td.heightmapResolution;
        float x=Mathf.Clamp(p.x/td.size.x*(r-1),0,r-1),z=Mathf.Clamp(p.z/td.size.z*(r-1),0,r-1);
        int ix=Mathf.Min(r-2,(int)x),iz=Mathf.Min(r-2,(int)z);
        return terrain.transform.position.y+td.size.y*TriangleHeight(pushBaseHeights,ix,iz,x-ix,z-iz);
    }
    public SoilKind KindAt(Vector3 point)
    {
        for(int i=SoilRegion.Active.Count-1;i>=0;i--)if(SoilRegion.Active[i] && SoilRegion.Active[i].Contains(point))return SoilRegion.Active[i].kind;
        if(!ContainsTerrainPoint(point))return surfaceSoil;
        float depth=OriginalSurfaceY(point)-point.y;
        var local=point-terrain.transform.position;var cell=terrain.terrainData.heightmapScale;
        return KindAtDepth(depth,(int)(local.x/cell.x),(int)(local.z/cell.z));
    }
    SoilKind KindAtDepth(float depth,int x,int z)
    {
        if(depth<-.025f)
        {
            int tiles=(terrain.terrainData.heightmapResolution-2)/TileSize+1;
            if(depositedMaterials.TryGetValue(x/TileSize+z/TileSize*tiles,out var kind))return kind;
        }
        return KindByDepth(depth);
    }
    SoilKind KindByDepth(float depth)
    {
        if(depth < -0.025f)return SoilKind.Sand; // Disturbed, deposited earth is looser than the native bed.
        if(depthLayers && depth>compactedDepth)return SoilKind.Compacted;
        if(depthLayers && depth>clayDepth)return SoilKind.Clay;
        return surfaceSoil;
    }
    public SoilProperties PropertiesFor(SoilKind kind)
    {
        if(soilMaterials!=null)foreach(var profile in soilMaterials)if(profile && profile.kind==kind)return profile.Properties;
        return SoilProperties.For(kind);
    }
    public SoilProperties SoilAt(Vector3 point)=>PropertiesFor(KindAt(point));
    public void Disturb(Vector3 point,float radius)
    {
        if(!IsWorkPoint(point) || !simulateSlopes || !ContainsTerrainPoint(point))return;
        var td=terrain.terrainData;var p=point-terrain.transform.position;var scale=td.heightmapScale;
        int tiles=(td.heightmapResolution-2)/TileSize+1;
        int x0=Mathf.Clamp(Mathf.FloorToInt((p.x-radius)/scale.x/TileSize),0,tiles-1),x1=Mathf.Clamp(Mathf.FloorToInt((p.x+radius)/scale.x/TileSize),0,tiles-1);
        int z0=Mathf.Clamp(Mathf.FloorToInt((p.z-radius)/scale.z/TileSize),0,tiles-1),z1=Mathf.Clamp(Mathf.FloorToInt((p.z+radius)/scale.z/TileSize),0,tiles-1);
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)Wake(x+z*tiles,40);
    }
    void Wake(int key,int life)
    {
        if(soilLife.ContainsKey(key)){soilLife[key]=Mathf.Max(soilLife[key],life);return;}
        soilLife[key]=life;soilQueue.Enqueue(key);
    }
    public void StepSoil(float dt)
    {
        if(!simulateSlopes || !terrain || dt<=0)return;
        int count=Mathf.Min(soilTilesPerStep,soilQueue.Count);
        for(int n=0;n<count;n++)
        {
            int key=soilQueue.Dequeue();int life=soilLife[key]-1;soilLife.Remove(key);
            if(simulationObserver)
            {
                int tiles=(terrain.terrainData.heightmapResolution-2)/TileSize+1;
                var scale=terrain.terrainData.heightmapScale;
                var center=terrain.transform.position+new Vector3((key%tiles+.5f)*TileSize*scale.x,0,(key/tiles+.5f)*TileSize*scale.z);
                var delta=Vector3.ProjectOnPlane(center-simulationObserver.position,Vector3.up);
                if(delta.sqrMagnitude>activeSoilDistance*activeSoilDistance){Wake(key,life+1);continue;}
            }
            bool moving=RelaxTile(key,Mathf.Min(dt,0.1f));
            if(moving && life>0)Wake(key,life);
        }
        LastBladeLoad=Mathf.MoveTowards(LastBladeLoad,0,dt);
    }
    bool RelaxTile(int key,float dt)
    {
        RememberSoilSurface();
        var td=terrain.terrainData;int r=td.heightmapResolution,tiles=(r-2)/TileSize+1;
        int tx=key%tiles,tz=key/tiles,x0=Mathf.Max(0,tx*TileSize-1),z0=Mathf.Max(0,tz*TileSize-1);
        int w=Mathf.Min(r-x0,TileSize+3),h=Mathf.Min(r-z0,TileSize+3);using var patch=EditHeights(x0,z0,w,h);var heights=patch.Heights;
        var scale=td.heightmapScale;var origin=terrain.transform.position;bool moved=false;
        // Pair transfers are equal and opposite. Alternating order avoids a preferred downhill direction.
        for(int pass=0;pass<2;pass++)
        for(int zi=0;zi<h;zi++)for(int xi=0;xi<w;xi++)
        {
            int x=((slidePhase+pass)&1)==0?xi:w-1-xi,z=((slidePhase+pass)&1)==0?zi:h-1-zi;
            if(x+1<w)moved|=RelaxPair(heights,x,z,x+1,z,scale.x,origin,scale,x0,z0,dt);
            if(z+1<h)moved|=RelaxPair(heights,x,z,x,z+1,scale.z,origin,scale,x0,z0,dt);
        }
        slidePhase++;
        if(!moved)return false;
        patch.Apply();terrainDirty=true;
        var center=origin+new Vector3((x0+w*0.5f)*scale.x,0,(z0+h*0.5f)*scale.z);
        PaintDumped(center,Mathf.Max(w*scale.x,h*scale.z)*0.75f,1);
        // Allow a slide to cross tile boundaries, without simulating the rest of the landscape.
        if(tx>0 && EdgeMoved(heights,patch.Original,0))Wake(key-1,12);
        if(tx+1<tiles && EdgeMoved(heights,patch.Original,w-1))Wake(key+1,12);
        if(tz>0 && RowMoved(heights,patch.Original,0))Wake(key-tiles,12);
        if(tz+1<tiles && RowMoved(heights,patch.Original,h-1))Wake(key+tiles,12);
        return true;
    }
    // Neighbor tiles are only woken when their shared border actually changes.
    static bool EdgeMoved(float[,] h,float[,] original,int x){for(int z=0;z<h.GetLength(0);z++)if(Mathf.Abs(h[z,x]-original[z,x])>1e-6f)return true;return false;}
    static bool RowMoved(float[,] h,float[,] original,int z){for(int x=0;x<h.GetLength(1);x++)if(Mathf.Abs(h[z,x]-original[z,x])>1e-6f)return true;return false;}
    bool RelaxPair(float[,] h,int ax,int az,int bx,int bz,float distance,Vector3 origin,Vector3 scale,int x0,int z0,float dt)
    {
        float difference=(h[az,ax]-h[bz,bx])*scale.y;
        if(Mathf.Abs(difference)<0.005f)return false;
        int sx=difference>0?ax:bx,sz=difference>0?az:bz,dx=difference>0?bx:ax,dz=difference>0?bz:az;
        var point=origin+new Vector3((x0+sx)*scale.x,h[sz,sx]*scale.y,(z0+sz)*scale.z);var kind=KindAtDepth((pushBaseHeights[z0+sz,x0+sx]-h[sz,sx])*scale.y,x0+sx,z0+sz);
        for(int region=SoilRegion.Active.Count-1;region>=0;region--)
            if(SoilRegion.Active[region] && SoilRegion.Active[region].Contains(point)){kind=SoilRegion.Active[region].kind;break;}
        var soil=PropertiesFor(kind);
        if(!IsWorkPoint(point))return false;
        float naturalDifference=Mathf.Abs(pushBaseHeights[z0+az,x0+ax]-pushBaseHeights[z0+bz,x0+bx])*scale.y;
        float stableDifference=Mathf.Max(naturalDifference,Mathf.Tan(soil.repose*Mathf.Deg2Rad)*distance+soil.cohesion);
        float excess=Mathf.Abs(difference)-stableDifference;
        if(excess<=0.002f)return false;
        float transfer=Mathf.Min(excess*0.25f,soilSlideSpeed*dt/(1+soil.adhesion*3))/scale.y;
        transfer=Mathf.Min(transfer,h[sz,sx],1-h[dz,dx]);
        if(transfer<=1e-7f)return false;
        h[sz,sx]-=transfer;h[dz,dx]+=transfer;return true;
    }
    public float DisplaceTrack(Vector3 center,Vector3 forward,float width,float travel,float slip,float pressure,float dt)
    {
        if(!makeTrackRuts || !ContainsTerrainPoint(center) || dt<=0)return 0;
        var soil=SoilAt(new Vector3(center.x,SurfaceY(center),center.z));
        float floor=OriginalSurfaceY(center)-0.045f*soil.softness;
        float depth=Mathf.Min(0.003f,(Mathf.Abs(travel)*0.018f+Mathf.Abs(slip)*dt*0.008f)*soil.softness*Mathf.Clamp(pressure,0.3f,2));
        if(depth<=0.00001f)return 0;
        var side=Vector3.Cross(Vector3.up,forward).normalized;float radius=Mathf.Max(width*0.45f,terrain.terrainData.heightmapScale.x*0.8f);
        float removed=Dig(center,radius,depth,0.0005f,floor);
        float added=Add(center+side*(radius*2.5f),radius,removed*0.5f,true);
        added+=Add(center-side*(radius*2.5f),radius,removed-added,true);
        if(added<removed)Add(center,radius,removed-added,true);
        return added;
    }
}
