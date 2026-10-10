using UnityEngine;

public class CylinderLink : MonoBehaviour
{
    public Transform other;                       // poros cilindras
    public Vector3 aimAxis = Vector3.up;          // lokali ašis, einanti cilindro ilgiu
    void LateUpdate()
    {
        Vector3 dir = (other.position - transform.position).normalized;
        transform.rotation = Quaternion.FromToRotation(transform.TransformDirection(aimAxis), dir) * transform.rotation;
    }
}