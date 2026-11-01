using System.Collections.Generic;
using UnityEngine;

public enum SoilKind { Sand, Loam, Clay, Compacted }

public readonly struct SoilProperties
{
    public readonly float density, resistance, repose, cohesion, friction, softness, adhesion;
    public SoilProperties(float density,float resistance,float repose,float cohesion,float friction,float softness,float adhesion)
    {this.density=density;this.resistance=resistance;this.repose=repose;this.cohesion=cohesion;this.friction=friction;this.softness=softness;this.adhesion=adhesion;}
    public static SoilProperties For(SoilKind kind)
    {
        switch(kind)
        {
            case SoilKind.Sand: return new SoilProperties(1500,0.65f,32,0,0.62f,0.9f,0);
            case SoilKind.Clay: return new SoilProperties(1800,2.1f,52,0.035f,0.58f,0.35f,0.75f);
            case SoilKind.Compacted: return new SoilProperties(1950,3.5f,65,0.07f,0.95f,0.06f,0.25f);
            default: return new SoilProperties(1600,1,34,0.012f,0.82f,0.55f,0.2f);
        }
    }
}

// Optional explicit soil volumes override the terrain's depth layers. No collision object is required.
public class SoilRegion : MonoBehaviour
{
    public SoilKind kind = SoilKind.Sand;
    public Bounds localBounds = new Bounds(Vector3.zero,new Vector3(3,3,3));
    internal static readonly List<SoilRegion> Active = new List<SoilRegion>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRegistry(){Active.Clear();}
    public void Register(){if(!Active.Contains(this))Active.Add(this);}
    void OnEnable(){Register();}
    void OnDisable(){Active.Remove(this);}
    public bool Contains(Vector3 world)=>localBounds.Contains(transform.InverseTransformPoint(world));
    void OnDrawGizmosSelected(){Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=new Color(0.8f,0.6f,0.2f,0.4f);Gizmos.DrawWireCube(localBounds.center,localBounds.size);}
}
