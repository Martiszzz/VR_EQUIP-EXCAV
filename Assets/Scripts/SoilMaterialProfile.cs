using UnityEngine;

[CreateAssetMenu(menuName="Excavator/Soil material")]
public class SoilMaterialProfile : ScriptableObject
{
    public SoilKind kind=SoilKind.Loam;
    [Min(100)] public float density=1600;
    [Min(0.1f)] public float diggingResistance=1;
    [Range(15,75)] public float reposeAngle=40;
    [Min(0)] public float cohesion=0.012f;
    [Range(0.1f,1.5f)] public float friction=0.82f;
    [Range(0,1)] public float softness=0.55f, adhesion=0.2f;
    public SoilProperties Properties => new SoilProperties(density,diggingResistance,reposeAngle,cohesion,friction,softness,adhesion);
}
