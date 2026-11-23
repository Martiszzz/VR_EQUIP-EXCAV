using UnityEngine;

// Namo dalys laikomos sugriautomis, kai pajuda / nuvirsta nuo pradinės vietos.
public class DemolitionObjective : Objective
{
    public Transform root;                           // namo tėvinis objektas (jei null – šis objektas)
    [Min(0.05f)] public float moveThreshold = 0.6f;  // m
    [Range(5f, 90f)] public float tiltThreshold = 40f;
    [Range(0.01f, 1f)] public float requiredFraction = 0.2f;

    Rigidbody[] pieces;
    Vector3[] startPos;
    Quaternion[] startRot;
    float progress, clock;
    bool started;

    public override float Progress => progress;

    void Awake()
    {
        if (!root) root = transform;
        pieces = root.GetComponentsInChildren<Rigidbody>();
        startPos = new Vector3[pieces.Length];
        startRot = new Quaternion[pieces.Length];
        for (int i = 0; i < pieces.Length; i++) { startPos[i] = pieces[i].position; startRot[i] = pieces[i].rotation; }
    }

    void Update()
    {
        clock -= Time.deltaTime;
        if (clock > 0 || pieces.Length == 0) return;
        clock = 0.25f;
        int broken = 0;
        foreach (var (rb, i) in Indexed())
            if (!rb || (rb.position - startPos[i]).sqrMagnitude > moveThreshold * moveThreshold ||
                Quaternion.Angle(rb.rotation, startRot[i]) > tiltThreshold) broken++;
        progress = Mathf.Clamp01(broken / (float)pieces.Length / requiredFraction);
        if (!started && broken > 0) { started = true; Raise("demolishing"); }
    }

    System.Collections.Generic.IEnumerable<(Rigidbody, int)> Indexed()
    { for (int i = 0; i < pieces.Length; i++) yield return (pieces[i], i); }
}