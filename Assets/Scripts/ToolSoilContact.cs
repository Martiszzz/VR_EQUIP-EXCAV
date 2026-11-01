using UnityEngine;

public partial class DigSystem
{
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
        var destination=point+right*Mathf.Sign(side)*radius*3;
        var material=SoilAt(point);float cut=Mathf.Min(depth,.01f,motion.magnitude*.8f/Mathf.Max(.5f,material.resistance));
        float removed=Dig(point,radius,cut,float.PositiveInfinity,point.y);
        float deposited=Add(destination,radius,removed,true,KindAt(point));
        if(deposited<removed)Add(point,radius,removed-deposited,true,KindAt(point));
        return deposited;
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
        var cell=terrain.terrainData.heightmapScale;
        var destination=point+direction*(radius*2.5f+Mathf.Max(cell.x,cell.z)*2);
        if(!ContainsTerrainPoint(destination))return 0;
        float cut=Mathf.Min(depth,Mathf.Min(.01f,motion.magnitude*.8f/LastToolResistance));
        float budget=motion.magnitude*Mathf.Max(width,.04f)*Mathf.Min(depth,tool.softSoilDepth)*2;
        float removed=Dig(point,radius,cut,budget,point.y);
        float added=Add(destination,radius,removed,true,KindAt(point));
        if(added<removed)Add(point,radius,removed-added,true,KindAt(point));
        return added;
    }
}
