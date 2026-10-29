using UnityEngine;

// Rigid tools (the dozer blade) cannot excavate by passing through the heightmap.
public class GroundedAttachment : MonoBehaviour
{
    Collider[] contacts;
    public Bounds WorkBounds
    {
        get
        {
            if(contacts==null)contacts=GetComponentsInChildren<Collider>();
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
    public float Depth(Terrain terrain)
    {
        if (!terrain) return 0;
        if (contacts == null) contacts = GetComponentsInChildren<Collider>();
        float depth = 0;
        foreach (var c in contacts)
        {
            if (!c || !c.enabled || c.isTrigger) continue;
            Bounds local;
            if (c is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (c is MeshCollider mesh && mesh.sharedMesh) local = mesh.sharedMesh.bounds;
            else { depth = Mathf.Max(depth, Sample(terrain, c.bounds.min), Sample(terrain, new Vector3(c.bounds.center.x, c.bounds.min.y, c.bounds.center.z))); continue; }
            for (int x = -2; x <= 2; x++)
            for (int y = -2; y <= 2; y++)
            for (int z = -2; z <= 2; z++)
            {
                if (Mathf.Abs(x) < 2 && Mathf.Abs(y) < 2 && Mathf.Abs(z) < 2) continue;
                depth = Mathf.Max(depth, Sample(terrain, c.transform.TransformPoint(local.center + Vector3.Scale(local.extents * 0.5f, new Vector3(x, y, z)))));
            }
        }
        return depth;
    }
    static float Sample(Terrain terrain, Vector3 p)
    {
        var local = p - terrain.transform.position;
        var size = terrain.terrainData.size;
        return local.x < 0 || local.z < 0 || local.x > size.x || local.z > size.z ? 0 :
            Mathf.Max(0, terrain.SampleHeight(p) + terrain.transform.position.y - p.y);
    }
}
