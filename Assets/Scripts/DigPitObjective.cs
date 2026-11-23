using UnityEngine;

public class DigPitObjective : Objective
{
    public Transform zone;                                   // duobės centras (Y ašis į viršų, rotacija svarbi)
    public Vector2 size = new Vector2(3f, 2f);               // X = ilgis, Z = plotis
    [Min(0.01f)] public float targetVolume = 0.5f;            // m³
    [Min(0.1f)] public float targetDepth = 1.5f;
    [Min(1f)] public float tooDeepFactor = 1.35f;
    [Min(0.1f)] public float wideMargin = 0.5f;              // kiek už zonos jau laikoma "per plati"
    [Min(0.2f)] public float band = 1.2f;                    // aplink zoną stebimas ruožas
    [Range(0.1f, 0.5f)] public float cell = 0.25f;

    Terrain terrain;
    float[] baseline;
    float[] volumeBaseline;
    int nx, nz;
    int volumeNx, volumeNz;
    float volumeStepX, volumeStepZ;
    float progress, clock;
    bool began, complete, startedRaised;
    DigTeeth[] teeth;

    public override float Progress => complete ? 1f : progress;
    public override bool IsComplete => complete;

    public override void Begin()
    {
        terrain = DigSystem.I ? DigSystem.I.terrain : Terrain.activeTerrain;
        if (!zone) zone = transform;
        if (!terrain) return;
        float ox = size.x * 0.5f + band, oz = size.y * 0.5f + band;
        nx = Mathf.CeilToInt(ox * 2f / cell) + 1;
        nz = Mathf.CeilToInt(oz * 2f / cell) + 1;
        baseline = new float[nx * nz];
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
                baseline[iz * nx + ix] = Surface(Point(-ox + ix * cell, -oz + iz * cell));
        volumeNx = Mathf.Max(1, Mathf.CeilToInt(size.x / cell));
        volumeNz = Mathf.Max(1, Mathf.CeilToInt(size.y / cell));
        volumeStepX = size.x / volumeNx;
        volumeStepZ = size.y / volumeNz;
        volumeBaseline = new float[volumeNx * volumeNz];
        for (int iz = 0; iz < volumeNz; iz++)
            for (int ix = 0; ix < volumeNx; ix++)
            {
                float lx = -size.x * 0.5f + (ix + 0.5f) * volumeStepX;
                float lz = -size.y * 0.5f + (iz + 0.5f) * volumeStepZ;
                volumeBaseline[iz * volumeNx + ix] = Surface(Point(lx, lz));
            }
        teeth = FindObjectsByType<DigTeeth>(FindObjectsSortMode.None);
        began = true;
    }

    Vector3 Point(float lx, float lz) => zone.position + zone.rotation * new Vector3(lx, 0, lz);
    float Surface(Vector3 p) => terrain.SampleHeight(p) + terrain.transform.position.y;

    void Update()
    {
        if (!began || complete) return;
        clock -= Time.deltaTime;
        if (clock > 0) return;
        clock = 0.5f;
        Evaluate();
    }

    void Evaluate()
    {
        float hx = size.x * 0.5f, hz = size.y * 0.5f, ox = hx + band, oz = hz + band;
        int inside = 0, deep = 0, wide = 0;
        for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                float lx = -ox + ix * cell, lz = -oz + iz * cell;
                float depth = Mathf.Max(0, baseline[iz * nx + ix] - Surface(Point(lx, lz)));
                float ax = Mathf.Abs(lx), az = Mathf.Abs(lz);
                if (ax <= hx && az <= hz)
                {
                    inside++;
                    if (depth > targetDepth * tooDeepFactor) deep++;
                }
                else if ((ax > hx + wideMargin || az > hz + wideMargin) && depth > targetDepth * 0.35f) wide++;
            }
        if (inside == 0) return;
        float excavatedVolume = 0f;
        for (int iz = 0; iz < volumeNz; iz++)
            for (int ix = 0; ix < volumeNx; ix++)
            {
                float lx = -hx + (ix + 0.5f) * volumeStepX;
                float lz = -hz + (iz + 0.5f) * volumeStepZ;
                float depth = Mathf.Max(0f, volumeBaseline[iz * volumeNx + ix] - Surface(Point(lx, lz)));
                excavatedVolume += depth * volumeStepX * volumeStepZ;
            }
        float goalVolume = Mathf.Max(0.01f, targetVolume);
        progress = Mathf.Clamp01(excavatedVolume / goalVolume);
        if (!startedRaised && progress > 0.08f) { startedRaised = true; Raise("started"); }
        if (deep > inside * 0.05f) Raise("tooDeep");
        if (wide > 6) Raise("tooWide");
        CheckWrongPlace();
        if (excavatedVolume >= goalVolume) complete = true;
    }

    void CheckWrongPlace()
    {
        var system = DigSystem.I;
        if (!system || teeth == null || !Ignition.EngineRunning) return;
        foreach (var t in teeth)
        {
            if (!t || !t.tip) continue;
            var p = t.tip.position;
            if (!system.ContainsTerrainPoint(p) || p.y > system.SurfaceY(p) - 0.05f) continue;
            var l = Quaternion.Inverse(zone.rotation) * (p - zone.position);
            if (Mathf.Abs(l.x) > size.x * 0.5f + band + 1f || Mathf.Abs(l.z) > size.y * 0.5f + band + 1f)
            { Raise("wrongPlace"); return; }
        }
    }

    void OnDrawGizmos()
    {
        var z = zone ? zone : transform;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = Matrix4x4.TRS(z.position, z.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0, -targetDepth * 0.5f, 0), new Vector3(size.x, targetDepth, size.y));
    }
}