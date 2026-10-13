using UnityEngine;

public static class JoystickMotion
{
    static void Frame(Vector3 stem, out Vector3 pitchAxis, out Vector3 sideAxis)
    {
        stem.Normalize();
        if (stem.sqrMagnitude < 0.5f) stem = Vector3.forward;
        pitchAxis = Vector3.ProjectOnPlane(Vector3.right, stem).normalized;
        if (pitchAxis.sqrMagnitude < 0.5f) pitchAxis = Vector3.ProjectOnPlane(Vector3.up, stem).normalized;
        sideAxis = Vector3.Cross(stem, pitchAxis).normalized;
    }

    public static Vector2 HandAngles(Vector3 hand, Vector3 stem)
    {
        if (stem.sqrMagnitude < 0.001f) stem = Vector3.forward;
        stem.Normalize();
        Frame(stem, out var pitchAxis, out var sideAxis);
        float height = Vector3.Dot(hand, stem);
        return new Vector2(Mathf.Atan2(Vector3.Dot(hand, Vector3.Cross(pitchAxis, stem)), height),
            Mathf.Atan2(Vector3.Dot(hand, Vector3.Cross(sideAxis, stem)), height)) * Mathf.Rad2Deg;
    }

    public static Quaternion Tilt(float pitch, float side, Vector3 stem, float maxAngle)
    {
        Frame(stem, out var pitchAxis, out var sideAxis);
        var tilt = Vector2.ClampMagnitude(new Vector2(pitch, side), maxAngle);
        // A single rotation around an axis perpendicular to the stem has zero axial twist.
        Vector3 axis = pitchAxis * tilt.x + sideAxis * tilt.y;
        return axis.sqrMagnitude < 0.000001f ? Quaternion.identity : Quaternion.AngleAxis(tilt.magnitude, axis.normalized);
    }
}
