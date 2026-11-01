using System;
using System.Collections.Generic;
using UnityEngine;

public partial class DigSystem
{
    // Unity's GetHeights allocates. Keep one authoritative grid and reuse exact-size edit windows after warmup.
    float[,] soilHeights;
    byte[] modifiedCells;
    public uint TerrainRevision {get;private set;}
    public uint SyncedTerrainRevision {get;private set;}
    TerrainData cachedHeightData;
    bool heightsValid, writingHeights, observingHeights;
    readonly List<SoilPatch> patches=new List<SoilPatch>();
    public int CachedTerrainWindows => patches.Count;
    void OnEnable(){ObserveHeights();}
    void OnDisable(){StopObservingHeights();}
    void ObserveHeights(){if(observingHeights)return;TerrainCallbacks.heightmapChanged+=ExternalHeightChange;observingHeights=true;}
    void StopObservingHeights(){if(!observingHeights)return;TerrainCallbacks.heightmapChanged-=ExternalHeightChange;observingHeights=false;}
    void ExternalHeightChange(Terrain changed,RectInt region,bool synced)
    {if(changed==terrain && !writingHeights)heightsValid=false;}
    SoilPatch EditHeights(int x,int z,int width,int height)
    {
        EnsureHeights();
        SoilPatch patch=null;
        foreach(var candidate in patches)if(!candidate.busy && candidate.Width==width && candidate.Height==height){patch=candidate;break;}
        if(patch==null){patch=new SoilPatch(this,width,height);patches.Add(patch);}
        patch.busy=true;patch.x=x;patch.z=z;
        for(int row=0;row<height;row++)for(int col=0;col<width;col++)
        {patch.Heights[row,col]=patch.Original[row,col]=soilHeights[z+row,x+col];patch.Scratch[row,col]=0;}
        return patch;
    }
    void EnsureHeights()
    {
        ObserveHeights();
        var data=terrain.terrainData;
        if(cachedHeightData!=data)modifiedCells=new byte[data.heightmapResolution*data.heightmapResolution];
        if(cachedHeightData!=data || !heightsValid)
        {soilHeights=data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);cachedHeightData=data;heightsValid=true;}
    }
    float CachedSurfaceY(Vector3 point)
    {
        EnsureHeights();var p=point-terrain.transform.position;var scale=cachedHeightData.heightmapScale;int r=cachedHeightData.heightmapResolution;
        float x=Mathf.Clamp(p.x/scale.x,0,r-1),z=Mathf.Clamp(p.z/scale.z,0,r-1);int ix=Mathf.Min(r-2,(int)x),iz=Mathf.Min(r-2,(int)z);
        return terrain.transform.position.y+scale.y*TriangleHeight(soilHeights,ix,iz,x-ix,z-iz);
    }
    static float TriangleHeight(float[,] grid,int x,int z,float u,float v)
    {
        // Unity terrain samples its two cell triangles, not a bilinear quad. This matters at a sharp cut edge.
        float a=grid[z,x],b=grid[z,x+1],c=grid[z+1,x],d=grid[z+1,x+1];
        return u>=v?a+(b-a)*u+(d-b)*v:a+(c-a)*v+(d-c)*u;
    }
    sealed class SoilPatch:IDisposable
    {
        readonly DigSystem owner;
        public readonly float[,] Heights,Original,Scratch;
        public readonly int Width,Height;
        public bool busy;
        public int x,z;
        public SoilPatch(DigSystem system,int width,int height)
        {owner=system;Width=width;Height=height;Heights=new float[height,width];Original=new float[height,width];Scratch=new float[height,width];}
        public void Apply()
        {
            // A blade may deposit through a nested window before removing its source strip.
            // Apply deltas to the live grid so overlapping windows cannot overwrite the new pile.
            bool changed=false;
            for(int row=0;row<Height;row++)for(int col=0;col<Width;col++)
            {
                float delta=Heights[row,col]-Original[row,col];
                if(Mathf.Abs(delta)>1e-8f)owner.modifiedCells[x+col+(z+row)*owner.cachedHeightData.heightmapResolution]=1;
                float previous=owner.soilHeights[z+row,x+col],next=Mathf.Clamp01(previous+delta);
                changed|=next!=previous;
                Heights[row,col]=owner.soilHeights[z+row,x+col]=next;
            }
            if(!changed)return;
            owner.TerrainRevision++;
            owner.writingHeights=true;
            try{owner.terrain.terrainData.SetHeightsDelayLOD(x,z,Heights);owner.terrainDirty=true;}
            finally{owner.writingHeights=false;}
        }
        public void Dispose(){busy=false;}
    }
}
