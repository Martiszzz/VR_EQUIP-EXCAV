using UnityEngine;

public class CylinderLink : MonoBehaviour
{
    public Transform other;
    public Vector3 aimAxis = Vector3.up;
    void LateUpdate() { Align(); }
    public void Align()
    {
        if (!other) return;
        Vector3 dir = (other.position - transform.position).normalized;
        if (dir.sqrMagnitude < .001f) return;
        transform.rotation = Quaternion.FromToRotation(transform.TransformDirection(aimAxis), dir) * transform.rotation;
    }
}
