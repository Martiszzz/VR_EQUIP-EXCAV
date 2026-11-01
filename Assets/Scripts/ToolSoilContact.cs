using UnityEngine;

public partial class DigSystem
{
    float MoveSoilLocally(Vector3 point,float radius,float depth,float budget,float floorY,Vector3 destination,
        Vector3? secondDestination=null,bool allowUnderMachine=false)
    {
        if(depth<=0 || budget<=0 || !GetWindow(point,radius,out int x0,out int z0,out int w,out int h,out float cx,out float cz,out float rx,out float rz))return 0;
        var kind=KindAt(point);
        using var source=EditHeights(x0,z0,w,h);
        var data=terrain.terrainData;var origin=terrain.transform.position;var cell=data.heightmapScale;
        float floor=Mathf.Max(0,(floorY-origin.y)/data.size.y),sum=0;
        for(int z=0;z<h;z++)for(int x=0;x<w;x++)
        {
            var vertex=origin+new Vector3((x0+x)*cell.x,0,(z0+z)*cell.z);
            if(!allowUnderMachine && !CanDepositAt(vertex))continue;
            float cut=Mathf.Min(depth*Weight(x0,z0,x,z,cx,cz,rx,rz)/data.size.y,Mathf.Max(0,source.Original[z,x]-floor));
            source.Scratch[z,x]=cut;sum+=cut;
        }
        float available=sum*data.size.y*CellArea,removed=Mathf.Min(available,budget);
        if(removed<=0)return 0;
        // Deposit first, then remove only the accepted volume. A blocked destination must leave
        // the original ground intact, not create a replacement heap beneath the chassis.
        float added=Add(destination,radius,removed*(secondDestination.HasValue ? .5f : 1f),true,kind);
        if(secondDestination.HasValue)added+=Add(secondDestination.Value,radius,removed*.5f,true,kind);
        if(added<=0)return 0;
        float ratio=Mathf.Clamp01(added/available);
        for(int z=0;z<h;z++)for(int x=0;x<w;x++)source.Heights[z,x]=source.Original[z,x]-source.Scratch[z,x]*ratio;
        source.Apply();Disturb(point,radius);PaintDumped(point,radius*1.1f,1);
        return added;
    }
    public float LastToolResistance {get;private set;}
    public float ScrapeBucketSides(BucketInside container,Vector3 motion,float maxDepth)
    {
        if(!container || !Ignition.EngineRunning || motion.sqrMagnitude<1e-12f)return 0;
        var horizontal=Vector3.ProjectOnPlane(motion,Vector3.up);
        if(horizontal.magnitude<motion.magnitude*.15f)return 0;
        float depth=container.GroundPenetration(this,false,out var point);
        if(depth<=.003f || depth>maxDepth)return 0;
        var box=container.GetComponent<BoxCollider>();
        var center=container.transform.TransformPoint(box.center);var right=container.transform.right;
        float side=Vector3.Dot(point-center,right);
        float halfWidth=box.size.x*Mathf.Abs(container.transform.lossyScale.x)*.5f;
        // Side cutters loosen the trench wall. The middle of the bucket floor cannot scoop by pressing down.
        if(Mathf.Abs(side)<halfWidth*.65f)return 0;
        var cell=terrain.terrainData.heightmapScale;float radius=new Vector2(cell.x,cell.z).magnitude*1.1f;
        var destination=point+right*Mathf.Sign(side)*(radius+looseSoilEdgeGap);
        var material=SoilAt(point);float cut=Mathf.Min(depth,.01f,motion.magnitude*.8f/Mathf.Max(.5f,material.resistance));
        return MoveSoilLocally(point,radius,cut,float.PositiveInfinity,point.y,destination);
    }
    public float ScrapeAttachmentSoil(GroundedAttachment tool,Vector3 motion)
    {
        LastToolResistance=1;
        if(!tool || !tool.canScrapeSoil || !Ignition.EngineRunning || motion.sqrMagnitude<1e-12f)return 0;
        float depth=tool.Depth(terrain,out var point);
        if(depth<=0 || !ContainsTerrainPoint(point))return 0;
        var material=SoilAt(point);LastToolResistance=Mathf.Clamp(material.resistance*(1+depth/Mathf.Max(.01f,tool.softSoilDepth)*.4f),.5f,4);
        if(depth>tool.softSoilDepth+.025f)return 0;
        var b=tool.WorkBounds;float width=b.size.x*Mathf.Abs(tool.transform.lossyScale.x);
        float radius=Mathf.Max(.025f,Mathf.Min(.15f,width*.5f));
        var direction=Vector3.ProjectOnPlane(motion,Vector3.up);
        if(direction.sqrMagnitude<1e-8f)direction=Vector3.ProjectOnPlane(tool.transform.forward,Vector3.up);
        if(direction.sqrMagnitude<1e-8f)direction=Vector3.forward;
        direction.Normalize();
        var destination=point+direction*(radius+looseSoilEdgeGap);
        if(!ContainsTerrainPoint(destination))return 0;
        float cut=Mathf.Min(depth,Mathf.Min(.01f,motion.magnitude*.8f/LastToolResistance));
        float budget=motion.magnitude*Mathf.Max(width,.04f)*Mathf.Min(depth,tool.softSoilDepth)*2;
        return MoveSoilLocally(point,radius,cut,budget,point.y,destination);
    }
}
