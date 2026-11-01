using UnityEngine;
using System.Collections.Generic;

// Transform-driven hydraulics use explicit terrain constraints; CCD alone cannot stop a teleported pose.
[DefaultExecutionOrder(-100)]
public class ExcavatorContacts : MonoBehaviour
{
    public bool addKinematicBody = true;
    public bool forceBasedChassis=true;
    [Range(0,.1f)] public float softGripperDepth=.06f;
    PhysicsMaterial contactMaterial;
    readonly HashSet<MeshFilter> shapedJaws=new HashSet<MeshFilter>();
    public void Awake() { Initialize(); }
    public void Initialize()
    {
        if(!contactMaterial)contactMaterial=new PhysicsMaterial("Excavator zero-bounce contact")
        {bounciness=0,staticFriction=.8f,dynamicFriction=.65f,bounceCombine=PhysicsMaterialCombine.Minimum,frictionCombine=PhysicsMaterialCombine.Average};
        foreach (var driver in GetComponentsInChildren<PartDriver>(true))
        {
            var part=driver.target;
            if (!part) continue;
            var teeth=part.GetComponent<DigTeeth>();
            if (teeth)
            {
                // Existing compound shell boxes preserve the open bucket mouth.
                var body=part.GetComponent<Rigidbody>();
                if(body) Configure(body);
                continue;
            }
            // Never treat the slewing cab and its whole subtree as one solid ground tool.
            if(driver.useWorldAxis) continue;
            AddContact(part,driver.blockByTerrain);
            foreach(var filter in part.GetComponentsInChildren<MeshFilter>(true))
                if(!filter.GetComponentInParent<DigTeeth>())AddContact(filter.transform,driver.blockByTerrain);
        }
        foreach(var claw in GetComponentsInChildren<Claw>(true))
        {
            // Claw controllers live on Controls; the actual jaws are referenced elsewhere in the hierarchy.
            if(claw.jaws==null)continue;
            foreach(var jaw in claw.jaws)
            {
                if(!jaw.part)continue;
                foreach(var filter in jaw.part.GetComponentsInChildren<MeshFilter>(true))
                {AddContact(filter.transform,false);ShapeJawContacts(filter);}
                var contact=jaw.part.GetComponent<GroundedAttachment>();
                if(!contact)contact=jaw.part.gameObject.AddComponent<GroundedAttachment>();
                contact.includeChildren=true;contact.canPushSoil=false;
                contact.aggregateParent=null;
                contact.canScrapeSoil=true;contact.softSoilDepth=softGripperDepth;
                foreach(var part in jaw.part.GetComponentsInChildren<GroundedAttachment>())
                {part.canScrapeSoil=true;part.softSoilDepth=softGripperDepth;part.surfaceSampleSteps=1;if(part!=contact)part.aggregateParent=contact;}
            }
        }
        if(addKinematicBody)
        {
            var body=GetComponent<Rigidbody>();if(!body)body=gameObject.AddComponent<Rigidbody>();Configure(body);
            var grounding=GetComponent<ExcavatorGrounding>();
            if(forceBasedChassis && grounding && grounding.physicalSuspension)
            {
                // A nested kinematic bucket keeps its own world pose when its dynamic parent moves.
                // Animated shell/jaw colliders must belong to the same compound body as the chassis.
                foreach(var child in GetComponentsInChildren<Rigidbody>(true))
                    if(child!=body && child.isKinematic && !child.GetComponent<DirtBlock>())
                        DestroyImmediate(child);
                grounding.EnablePhysicalSupport(body);
            }
        }
        var terrain=GetComponent<ExcavatorGrounding>();var terrainCollider=terrain && terrain.terrain?terrain.terrain.GetComponent<TerrainCollider>():null;
        foreach(var collider in GetComponentsInChildren<Collider>(true))
        {
            if(!collider.enabled || collider.isTrigger)continue;
            collider.sharedMaterial=contactMaterial;
            // Terrain uses suspension/tool forces and deformation, not kinematic depenetration impulses.
            if(terrainCollider)Physics.IgnoreCollision(collider,terrainCollider,true);
        }
        foreach(var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            if(body.transform.IsChildOf(transform) || body.isKinematic)continue;
            body.maxDepenetrationVelocity=.35f;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping=Mathf.Max(body.linearDamping,.35f);body.angularDamping=Mathf.Max(body.angularDamping,.8f);
            foreach(var collider in body.GetComponentsInChildren<Collider>())collider.sharedMaterial=contactMaterial;
        }
    }
    void OnDestroy(){if(contactMaterial){if(Application.isPlaying)Destroy(contactMaterial);else DestroyImmediate(contactMaterial);}}
    void ShapeJawContacts(MeshFilter filter)
    {
        var mesh=filter.sharedMesh;
        if(!mesh || !mesh.isReadable || shapedJaws.Contains(filter))return;
        foreach(var collider in filter.GetComponents<Collider>())if(collider.enabled && !collider.isTrigger && !(collider is BoxCollider))return;
        var vertices=mesh.vertices;if(vertices.Length==0)return;
        const int sections=16;
        var bounds=new Bounds[sections];var used=new bool[sections];var innerRadius=new float[sections];
        var origin=mesh.bounds.center;
        for(int i=0;i<sections;i++)innerRadius[i]=float.PositiveInfinity;
        foreach(var vertex in vertices)
        {
            var offset=vertex-origin;
            float angle=Mathf.Atan2(offset.y,offset.z);
            int section=Mathf.Clamp(Mathf.FloorToInt((angle+Mathf.PI)/(2*Mathf.PI)*sections),0,sections-1);
            float middle=-Mathf.PI+(section+.5f)*(2*Mathf.PI/sections);
            var rotation=Quaternion.AngleAxis(-middle*Mathf.Rad2Deg,Vector3.right);
            var p=Quaternion.Inverse(rotation)*offset;
            if(!used[section]){bounds[section]=new Bounds(p,Vector3.zero);used[section]=true;}else bounds[section].Encapsulate(p);
            innerRadius[section]=Mathf.Min(innerRadius[section],new Vector2(offset.y,offset.z).magnitude);
        }
        foreach(var collider in filter.GetComponents<BoxCollider>())if(!collider.isTrigger)collider.enabled=false;
        var scale=filter.transform.lossyScale;
        var minimum=new Vector3(.001f/Mathf.Max(.0001f,Mathf.Abs(scale.x)),.001f/Mathf.Max(.0001f,Mathf.Abs(scale.y)),.001f/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
        for(int i=0;i<sections;i++)
        {
            if(!used[i])continue;
            float angle=-Mathf.PI+(i+.5f)*(2*Mathf.PI/sections);
            var rotation=Quaternion.AngleAxis(-angle*Mathf.Rad2Deg,Vector3.right);
            var b=bounds[i];var low=b.min;
            // Keep the curved inner opening clear, rather than bridging it with an enclosing box.
            low.z=Mathf.Min(innerRadius[i],b.max.z-minimum.z);b.SetMinMax(low,b.max);
            var go=new GameObject("Jaw contact "+i);go.layer=filter.gameObject.layer;go.transform.SetParent(filter.transform,false);
            go.transform.localPosition=origin+rotation*b.center;go.transform.localRotation=rotation;
            var box=go.AddComponent<BoxCollider>();box.size=Vector3.Max(b.size,minimum);box.contactOffset=.001f;
        }
        shapedJaws.Add(filter);
    }
    static void Configure(Rigidbody body)
    {
        body.isKinematic=true;body.useGravity=false;
        body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
        body.solverIterations=10;body.solverVelocityIterations=4;body.maxDepenetrationVelocity=2;
    }
    static void AddContact(Transform part,bool pushing)
    {
        var filter=part.GetComponent<MeshFilter>();
        if(!filter || !filter.sharedMesh)return;
        if(part.GetComponents<Collider>().Length==0)
        {
            var box=part.gameObject.AddComponent<BoxCollider>();box.center=filter.sharedMesh.bounds.center;box.size=filter.sharedMesh.bounds.size;
        }
        var contact=part.GetComponent<GroundedAttachment>();
        if(!contact)contact=part.gameObject.AddComponent<GroundedAttachment>();
        contact.includeChildren=false;contact.canPushSoil=pushing;
    }
}
