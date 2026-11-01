using UnityEngine;

// Transform-driven hydraulics use explicit terrain constraints; CCD alone cannot stop a teleported pose.
[DefaultExecutionOrder(-100)]
public class ExcavatorContacts : MonoBehaviour
{
    public bool addKinematicBody = true;
    public bool forceBasedChassis=true;
    [Range(0,.1f)] public float softGripperDepth=.06f;
    PhysicsMaterial contactMaterial;
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
                foreach(var filter in jaw.part.GetComponentsInChildren<MeshFilter>(true))AddContact(filter.transform,false);
                var contact=jaw.part.GetComponent<GroundedAttachment>();
                if(!contact)contact=jaw.part.gameObject.AddComponent<GroundedAttachment>();
                contact.includeChildren=true;contact.canPushSoil=false;
                contact.aggregateParent=null;
                contact.canScrapeSoil=true;contact.softSoilDepth=softGripperDepth;
                foreach(var part in jaw.part.GetComponentsInChildren<GroundedAttachment>())
                {part.canScrapeSoil=true;part.softSoilDepth=softGripperDepth;if(part!=contact)part.aggregateParent=contact;}
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
            if(collider.isTrigger)continue;
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
