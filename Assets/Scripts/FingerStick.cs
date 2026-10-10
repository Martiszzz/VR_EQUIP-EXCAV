using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class FingerStick : LeverBase
{
    public Transform head;                 // tuščias objektas lazdelės viršuje
    public float touchRadius = 0.03f;      // metrais (pasaulio erdvėje)
    public float maxAngle = 25f;
    public float followSpeed = 300f;       // laipsniai per sekundę
    public float returnSpeed = 150f;
    public bool invertForward, invertSide;
    public Transform[] extraTips;          // jei pirštų galiukų automatiškai neranda

    Quaternion restLocalRot;
    float pitch, roll;
    XRPokeInteractor[] pokes;

    void Awake()
    {
        restLocalRot = transform.localRotation;
        pokes = FindObjectsByType<XRPokeInteractor>();
    }

    bool FindTip(out Vector3 tip)
    {
        tip = default;
        float best = touchRadius;
        bool found = false;

        foreach (var p in pokes)
        {
            if (!p) continue;
            Vector3 pos = p.attachTransform ? p.attachTransform.position : p.transform.position;
            float dist = Vector3.Distance(pos, head.position);
            if (dist < best) { best = dist; tip = pos; found = true; }
        }
        foreach (var t in extraTips)
        {
            if (!t) continue;
            float dist = Vector3.Distance(t.position, head.position);
            if (dist < best) { best = dist; tip = t.position; found = true; }
        }
        return found;
    }

    void Update()
    {
        if (!head) return;

        if (FindTip(out Vector3 tip))
        {
            Quaternion restFrame = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRot;
            Vector3 d = Quaternion.Inverse(restFrame) * (tip - transform.position);

            float targetPitch = Mathf.Clamp(Mathf.Atan2(d.z, d.y) * Mathf.Rad2Deg, -maxAngle, maxAngle);
            float targetRoll = Mathf.Clamp(Mathf.Atan2(-d.x, d.y) * Mathf.Rad2Deg, -maxAngle, maxAngle);

            pitch = Mathf.MoveTowards(pitch, targetPitch, followSpeed * Time.deltaTime);
            roll = Mathf.MoveTowards(roll, targetRoll, followSpeed * Time.deltaTime);
        }
        else
        {
            pitch = Mathf.MoveTowards(pitch, 0, returnSpeed * Time.deltaTime);
            roll = Mathf.MoveTowards(roll, 0, returnSpeed * Time.deltaTime);
        }

        transform.localRotation = restLocalRot * Quaternion.Euler(pitch, 0, roll);

        Forward = (invertForward ? -1 : 1) * pitch / maxAngle;
        Side = (invertSide ? -1 : 1) * roll / maxAngle;
    }
}