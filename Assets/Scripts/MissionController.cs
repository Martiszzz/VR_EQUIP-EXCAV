using UnityEngine;
using UnityEngine.Events;

public abstract class Objective : MonoBehaviour
{
    public abstract float Progress { get; }          // 0..1
    public virtual bool IsComplete => Progress >= 1f;
    public virtual void Begin() { }
    public event System.Action<string> Raised;       // užduotis praneša įvykius: "tooWide", "missed"...
    protected void Raise(string key) { Raised?.Invoke(key); }
}

[System.Serializable]
public class RadioBeat
{
    [Range(0f, 1f)] public float atProgress = 0.5f;
    public RadioLine[] lines;
}

[System.Serializable]
public class Reaction
{
    public string key = "idle";
    public RadioLine[] variants;                     // kuo daugiau, tuo mažiau kartojasi
    public bool once;
    [Min(0)] public float cooldown = 20f;
    [System.NonSerialized] public bool used;
    [System.NonSerialized] public float lastTime = -999f;
    int last = -1;

    public bool Ready => variants != null && variants.Length > 0 && !(once && used) && Time.time - lastTime >= cooldown;
    public RadioLine Pick()
    {
        int n = variants.Length, i = Random.Range(0, n);
        if (n > 1 && i == last) i = (i + 1 + Random.Range(0, n - 1)) % n;
        last = i; used = true; lastTime = Time.time;
        return variants[i];
    }
}

[System.Serializable]
public class MissionStage
{
    public string name;
    public Objective objective;
    public RadioLine[] onStart;
    public RadioLine[] onFirstAction;                // jei tuščia – groja universalus "start"
    public RadioBeat[] beats;
    public Reaction[] reactions;                     // šio etapo reakcijos į įvykius
    public RadioLine[] onComplete;
}

public class MissionController : MonoBehaviour
{
    public OperatorActivity operatorActivity;
    public RadioLine[] intro;
    [Min(0)] public float introDelay = 2.5f;
    public MissionStage[] stages;
    public Reaction[] universal;                     // start, idle, slow, fast, good, mistake...

    [Header("Universalių frazių laikai")]
    [Min(5)] public float idleAfter = 25f;           // s be valdymo
    [Min(30)] public float slowAfter = 240f;         // s, jei progresas <60%
    [Range(0.05f, 1)] public float fastGain = 0.25f; // progreso prieaugis per langą = "dirba greitai"
    [Min(5)] public float fastWindow = 20f;
    [Min(0)] public float minGap = 8f;               // min. tarpas tarp reakcijų

    public UnityEvent onAllComplete;
    [Min(0)] public float finishDelay = 6f;

    int stage = -1;
    bool[] fired;
    bool engineWasOn, introStarted, finished, firstAction;
    float introClock, fastClock, fastStart, slowClock, lastReact = -999f;
    Objective current;

    public int CurrentStage => stage;

    void Update()
    {
        bool on = Ignition.EngineRunning;
        if (on && !engineWasOn && !introStarted) { introStarted = true; introClock = introDelay; }
        engineWasOn = on;

        if (introStarted && stage < 0)
        {
            introClock -= Time.deltaTime;
            if (introClock <= 0 && RadioChannel.I)
            {
                RadioChannel.I.Say(intro);
                BeginStage(0);
            }
            return;
        }
        if (stage < 0 || finished || stages == null || stage >= stages.Length) return;

        var s = stages[stage];
        float dt = Time.deltaTime;
        float progress = s.objective ? s.objective.Progress : 1f;

        if (s.beats != null)
            for (int i = 0; i < s.beats.Length; i++)
                if (!fired[i] && progress >= s.beats[i].atProgress)
                {
                    fired[i] = true;
                    RadioChannel.I?.Say(s.beats[i].lines);
                }

        if (!firstAction && operatorActivity && operatorActivity.IsActive)
        {
            firstAction = true;
            if (s.onFirstAction != null && s.onFirstAction.Length > 0) RadioChannel.I?.Say(s.onFirstAction);
            else Play("start", true);
        }
        if (operatorActivity && operatorActivity.IdleTime >= idleAfter)
        {
            operatorActivity.ResetIdle();
            Play("idle", false);
        }
        fastClock += dt;
        if (fastClock >= fastWindow)
        {
            if (progress - fastStart >= fastGain) Play("fast", false);
            fastClock = 0; fastStart = progress;
        }
        slowClock += dt;
        if (slowClock >= slowAfter)
        {
            slowClock = 0;
            if (progress < 0.6f) Play("slow", false);
        }

        if (!s.objective || s.objective.IsComplete)
        {
            EndStage();
            RadioChannel.I?.Say(s.onComplete);
            BeginStage(stage + 1);
        }
    }

    void BeginStage(int index)
    {
        stage = index;
        if (stages == null || index >= stages.Length)
        {
            finished = true;
            Invoke(nameof(Finish), finishDelay);
            return;
        }
        var s = stages[index];
        fired = new bool[s.beats != null ? s.beats.Length : 0];
        firstAction = false;
        fastClock = slowClock = fastStart = 0;
        if (operatorActivity) operatorActivity.ResetIdle();
        current = s.objective;
        if (current) { current.Raised += OnRaised; current.Begin(); }
        RadioChannel.I?.Say(s.onStart);
    }

    void EndStage()
    {
        if (current) current.Raised -= OnRaised;
        current = null;
    }

    void OnRaised(string key) { Play(key, false); }

    // Galima kviesti ir iš UnityEvent (pvz. "good" po sėkmingo veiksmo).
    public void React(string key) { Play(key, false); }

    void Play(string key, bool force)
    {
        var channel = RadioChannel.I;
        if (!channel || stages == null || stage < 0 || stage >= stages.Length) return;
        if (!force && (RadioChannel.IsTalking || Time.time - lastReact < minGap)) return;
        var r = Find(stages[stage].reactions, key) ?? Find(universal, key);
        if (r == null || !r.Ready) return;
        channel.Say(r.Pick());
        lastReact = Time.time;
    }

    static Reaction Find(Reaction[] list, string key)
    {
        if (list == null) return null;
        foreach (var r in list) if (r != null && r.key == key) return r;
        return null;
    }

    void Finish() { onAllComplete.Invoke(); }
}