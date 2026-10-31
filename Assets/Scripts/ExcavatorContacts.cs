using UnityEngine;

// Transform-driven hydraulics use explicit terrain constraints; CCD alone cannot stop a teleported pose.
[DefaultExecutionOrder(-100)]
public class ExcavatorContacts : MonoBehaviour
{
    public bool addKinematicBody = true;
    public void Awake() { Initialize(); }
    public void Initialize()
    {
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
            }
        }
        if(addKinematicBody)
        {
            var body=GetComponent<Rigidbody>();if(!body)body=gameObject.AddComponent<Rigidbody>();Configure(body);
        }
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
