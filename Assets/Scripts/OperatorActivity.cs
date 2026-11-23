using UnityEngine;

public class OperatorActivity : MonoBehaviour
{
    public LeverBase[] levers;
    [Range(0.05f, 0.5f)] public float threshold = 0.2f;
    public bool IsActive { get; private set; }
    public float IdleTime { get; private set; }

    void Update()
    {
        bool active = false;
        if (Ignition.EngineRunning && levers != null)
            foreach (var l in levers)
                if (l && (Mathf.Abs(l.Forward) > threshold || Mathf.Abs(l.Side) > threshold)) { active = true; break; }
        IsActive = active;
        if (active) IdleTime = 0;
        else if (!RadioChannel.IsTalking) IdleTime += Time.deltaTime;   // kol kalba radijas, tylos neskaičiuojame
    }
    public void ResetIdle() { IdleTime = 0; }
}