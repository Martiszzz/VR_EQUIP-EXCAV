using UnityEngine;

// Builds real moving tread shoes around the existing model's two track meshes.
public class TrackVisuals : MonoBehaviour
{
    public TrackDrive drive;
    public Transform leftTrack, rightTrack;
    public int linksPerTrack = 48;
    public TrackChain Left { get; private set; }
    public TrackChain Right { get; private set; }
    Mesh shoe;
    void Start() { Initialize(); }
    public void Initialize()
    {
        if (Left || !drive || !leftTrack || !rightTrack) return;
        // Prefer authored chains when a scene already supplies its own links and path.
        foreach(var chain in GetComponentsInChildren<TrackChain>())
        {
            var filter=chain.linkPrefab?chain.linkPrefab.GetComponentInChildren<MeshFilter>(true):null;
            if(!chain.enabled || chain.path==null || chain.path.Length<3 || !filter || !filter.sharedMesh)continue;
            chain.drive=drive;chain.Build();chain.linkPrefab.gameObject.SetActive(false);
            if(chain.rightSide)Right=chain;else Left=chain;
        }
        if(!Left)Left = Build(leftTrack, false);else HideStatic(leftTrack);
        if(!Right)Right = Build(rightTrack, true);else HideStatic(rightTrack);
    }
    static void HideStatic(Transform track){var renderer=track.GetComponent<MeshRenderer>();if(renderer)renderer.enabled=false;}
    TrackChain Build(Transform track, bool right)
    {
        var filter = track.GetComponent<MeshFilter>(); var renderer = track.GetComponent<MeshRenderer>();
        if (!filter || !filter.sharedMesh || !renderer) return null;
        var frame = transform.worldToLocalMatrix * track.localToWorldMatrix;
        var b = filter.sharedMesh.bounds; var bounds = new Bounds(frame.MultiplyPoint3x4(b.center), Vector3.zero);
        for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
            bounds.Encapsulate(frame.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));
        var scale = transform.lossyScale;
        Vector3 size = Vector3.Scale(bounds.size,scale);
        var go=new GameObject(right?"Moving right tread":"Moving left tread"); go.transform.SetParent(transform,false);
        go.transform.localPosition=bounds.center;
        go.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
        var chain=go.AddComponent<TrackChain>(); chain.drive=drive;chain.rightSide=right;chain.linkCount=linksPerTrack;
        float thickness=Mathf.Min(0.025f,size.y*0.1f),radius=(size.y-thickness)*0.5f;
        float straight=Mathf.Max(0.01f,(size.z-size.y)*0.5f);
        chain.path=new Transform[24];
        for(int i=0;i<24;i++)
        {
            float angle=(i%12)/11f*Mathf.PI + (i<12?0:Mathf.PI);
            var point=new GameObject("Track path").transform;point.SetParent(go.transform,false);
            point.localPosition=new Vector3(0,Mathf.Cos(angle)*radius,(i<12?straight:-straight)+Mathf.Sin(angle)*radius);
            chain.path[i]=point;
        }
        var template=new GameObject("Tread shoe template");template.transform.SetParent(go.transform,false);
        if(!shoe) shoe=MakeShoe();
        template.AddComponent<MeshFilter>().sharedMesh=shoe;
        var material=renderer.sharedMaterials;
        template.AddComponent<MeshRenderer>().sharedMaterial=material.Length>0?material[0]:SoilClodVisual.EarthMaterial;
        float perimeter=straight*4+Mathf.PI*radius*2;
        template.transform.localScale=new Vector3(size.x,thickness,perimeter/linksPerTrack*0.9f);
        template.SetActive(false);chain.linkPrefab=template.transform;chain.Build();
        renderer.enabled=false;
        return chain;
    }
    static Mesh MakeShoe()
    {
        var mesh=new Mesh {name="Track shoe with grouser"};
        // A broad shoe and a raised rib, both sharing one draw material.
        var vertices=new System.Collections.Generic.List<Vector3>();var triangles=new System.Collections.Generic.List<int>();
        AddBox(vertices,triangles,Vector3.zero,Vector3.one);
        AddBox(vertices,triangles,new Vector3(0,0.65f,0),new Vector3(1.02f,0.3f,0.22f));
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static void AddBox(System.Collections.Generic.List<Vector3> v,System.Collections.Generic.List<int> t,Vector3 center,Vector3 size)
    {
        int start=v.Count;
        foreach(var p in new[]{new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)})v.Add(center+Vector3.Scale(p,size)*0.5f);
        foreach(int i in new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5})t.Add(start+i);
    }
    void OnDestroy(){if(shoe){if(Application.isPlaying)Destroy(shoe);else DestroyImmediate(shoe);}}
}
