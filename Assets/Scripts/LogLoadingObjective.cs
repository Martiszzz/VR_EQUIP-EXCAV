using UnityEngine;

public class LogLoadingObjective : Objective
{
    [Header("Load target")]
    public Transform truck;
    public Rigidbody[] logs;
    [Min(1)] public int requiredLogs = 9;

    [Header("Truck bed zone (truck local space)")]
    public Vector3 loadingCenter = new Vector3(0f, 0.005f, 0.0015f);
    public Vector3 loadingSize = new Vector3(0.012f, 0.035f, 0.012f);
    [Min(0.01f)] public float settledSpeed = 0.25f;
    [Min(0.1f)] public float settleDuration = 0.75f;

    [Header("Radio events")]
    [Min(0.1f)] public float liftHeight = 0.35f;
    [Min(0.1f)] public float movementThreshold = 0.5f;

    Vector3[] startPositions;
    float[] settledTimes;
    float[] loadTimes;
    bool[] lifted;
    bool[] loaded;
    bool[] warned;
    int loadedCount;
    bool began, firstLiftRaised, firstLoadRaised, halfLoadRaised;

    public override float Progress => requiredLogs > 0 ? Mathf.Clamp01(loadedCount / (float)requiredLogs) : 0f;
    public override bool IsComplete => requiredLogs > 0 && loadedCount >= requiredLogs;

    public override void Begin()
    {
        if (!truck)
        {
            Debug.LogError("[LogLoadingObjective] Assign the truck transform.", this);
            return;
        }

        if (logs == null || logs.Length == 0)
        {
            Debug.LogError("[LogLoadingObjective] Assign the logs to load.", this);
            return;
        }

        int validLogs = 0;
        foreach (var log in logs)
            if (log) validLogs++;

        if (requiredLogs > validLogs)
        {
            Debug.LogError($"[LogLoadingObjective] requiredLogs ({requiredLogs}) exceeds the assigned logs ({validLogs}).", this);
            return;
        }

        startPositions = new Vector3[logs.Length];
        settledTimes = new float[logs.Length];
        loadTimes = new float[logs.Length];
        lifted = new bool[logs.Length];
        loaded = new bool[logs.Length];
        warned = new bool[logs.Length];
        loadedCount = 0;
        firstLiftRaised = firstLoadRaised = halfLoadRaised = false;

        for (int i = 0; i < logs.Length; i++)
            if (logs[i]) startPositions[i] = logs[i].position;

        began = true;
    }

    void Update()
    {
        if (!began || IsComplete) return;

        Bounds zone = new Bounds(loadingCenter, loadingSize);
        float speedLimit = settledSpeed * settledSpeed;
        float moveLimit = movementThreshold * movementThreshold;

        for (int i = 0; i < logs.Length; i++)
        {
            var log = logs[i];
            if (!log)
            {
                if (loaded[i])
                {
                    loaded[i] = false;
                    loadedCount--;
                }
                continue;
            }

            bool inside = zone.Contains(truck.InverseTransformPoint(log.worldCenterOfMass));
            bool settled = !log.isKinematic && log.linearVelocity.sqrMagnitude <= speedLimit;
            bool moved = (log.position - startPositions[i]).sqrMagnitude >= moveLimit;

            if (!lifted[i] && log.position.y - startPositions[i].y >= liftHeight)
            {
                lifted[i] = true;
                if (!firstLiftRaised)
                {
                    firstLiftRaised = true;
                    Raise("firstInAir");
                }
            }

            if (settled) settledTimes[i] += Time.deltaTime;
            else settledTimes[i] = 0f;

            if (inside && settled) loadTimes[i] += Time.deltaTime;
            else loadTimes[i] = 0f;

            bool isLoaded = loadTimes[i] >= settleDuration;
            if (isLoaded != loaded[i])
            {
                loaded[i] = isLoaded;
                loadedCount += isLoaded ? 1 : -1;

                if (isLoaded && !firstLoadRaised)
                {
                    firstLoadRaised = true;
                    Raise("firstDone");
                }

                if (isLoaded && !halfLoadRaised && loadedCount >= Mathf.CeilToInt(requiredLogs * 0.5f))
                {
                    halfLoadRaised = true;
                    Raise("halfLoad");
                }
            }

            bool nearGround = log.position.y <= startPositions[i].y + liftHeight * 0.5f;
            if (!warned[i] && moved && !inside && settled && nearGround && settledTimes[i] >= settleDuration)
            {
                warned[i] = true;
                Raise("wrongPlace");
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!truck) return;
        Gizmos.color = Color.yellow;
        var previousMatrix = Gizmos.matrix;
        Gizmos.matrix = truck.localToWorldMatrix;
        Gizmos.DrawWireCube(loadingCenter, loadingSize);
        Gizmos.matrix = previousMatrix;
    }
}
