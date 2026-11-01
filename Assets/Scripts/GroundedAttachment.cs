using UnityEngine;

// Rigid tools (the dozer blade) cannot excavate by passing through the heightmap.
public class GroundedAttachment : MonoBehaviour
{
    public bool includeChildren = true;
    public bool canPushSoil = true;
    public bool canScrapeSoil;
    [Range(1,2)] public int surfaceSampleSteps=2;
    [Range(0,.1f)] public float softSoilDepth;
    [System.NonSerialized] public GroundedAttachment aggregateParent;
    public bool CoveredByParent => aggregateParent && aggregateParent.isActiveAndEnabled && aggregateParent.includeChildren;
    Collider[] contacts;
    Collider[] Contacts => contacts ?? (contacts = includeChildren ? GetComponentsInChildren<Collider>() : GetComponents<Collider>());
    public Bounds WorkBounds
    {
        get
        {
            var contacts=Contacts;
            var bounds=new Bounds(Vector3.zero,Vector3.zero);bool first=true;
            foreach(var c in contacts)
            {
                if(!c || !c.enabled || c.isTrigger)continue;
                Bounds local=c is BoxCollider box?new Bounds(box.center,box.size):c is MeshCollider mesh && mesh.sharedMesh?mesh.sharedMesh.bounds:new Bounds(c.transform.InverseTransformPoint(c.bounds.center),c.bounds.size);
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    var p=transform.InverseTransformPoint(c.transform.TransformPoint(local.center+Vector3.Scale(local.extents,new Vector3(x,y,z))));
                    if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
    }
    public float Depth(Terrain terrain) => Depth(terrain,out _);
    public float Depth(Terrain terrain,out Vector3 contact)
    {
        contact=transform.position;
        if (!terrain) return 0;
        var contacts=Contacts;
        float depth = 0,contactCount=0;
        foreach (var c in contacts)
        {
            if (!c || !c.enabled || c.isTrigger) continue;
            Bounds local;
            if (c is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (c is MeshCollider mesh && mesh.sharedMesh) local = mesh.sharedMesh.bounds;
            else
            {
                Consider(terrain,c.bounds.min,ref depth,ref contact,ref contactCount);
                Consider(terrain,new Vector3(c.bounds.center.x,c.bounds.min.y,c.bounds.center.z),ref depth,ref contact,ref contactCount);continue;
            }
            int steps=Mathf.Clamp(surfaceSampleSteps,1,2);
            for (int x = -steps; x <= steps; x++)
            for (int y = -steps; y <= steps; y++)
            for (int z = -steps; z <= steps; z++)
            {
                if (Mathf.Abs(x) < steps && Mathf.Abs(y) < steps && Mathf.Abs(z) < steps) continue;
                Consider(terrain,c.transform.TransformPoint(local.center+Vector3.Scale(local.extents/steps,new Vector3(x,y,z))),ref depth,ref contact,ref contactCount);
            }
        }
        return depth;
    }
    static void Consider(Terrain terrain,Vector3 point,ref float depth,ref Vector3 contact,ref float count)
    {
        float candidate=Sample(terrain,point);
        // A broad blade rests on a contact patch. Choosing a different single corner for tiny
        // height differences made its lifting torque alternate left/right every physics tick.
        if(candidate>depth+.01f){depth=candidate;contact=point;count=1;}
        else if(candidate>0 && Mathf.Abs(candidate-depth)<=.01f)
        {
            if(candidate>depth)count*=Mathf.Clamp01(1-(candidate-depth)/.01f);
            depth=Mathf.Max(depth,candidate);
            float weight=Mathf.Clamp01(1-(depth-candidate)/.01f);
            if(weight>0){count+=weight;contact+=(point-contact)*(weight/count);}
        }
    }
    static float Sample(Terrain terrain, Vector3 p)
    {
        var local = p - terrain.transform.position;
        var size = terrain.terrainData.size;
        return local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z ? 0 :
            Mathf.Max(0, (DigSystem.I && DigSystem.I.terrain==terrain?DigSystem.I.SurfaceY(p):terrain.SampleHeight(p)+terrain.transform.position.y) - p.y);
    }
}
