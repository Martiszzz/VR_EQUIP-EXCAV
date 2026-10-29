using UnityEngine;

[DefaultExecutionOrder(200)]
public class ExcavatorEngineAudio : MonoBehaviour
{
    public enum Phase { Off, Starting, Running, Stopping }
    public AudioClip startupClip, engineLoop;
    public Throttle throttle;
    public Vector3 engineOffset = new Vector3(0, 0.2f, 0.05f);
    [Range(0.5f, 1.2f)] public float idlePitch = 0.9f;
    [Range(1f, 2f)] public float fullPitch = 1.5f;
    [Range(0f, 1f)] public float idleVolume = 0.35f;
    [Range(0f, 1f)] public float fullVolume = 0.65f;
    [Min(0.05f)] public float rpmResponse = 0.65f;
    [Min(0.05f)] public float crossfade = 0.25f;
    [Min(0.05f)] public float shutdownFade = 0.6f;
    public Phase State { get; private set; }
    public float Rpm { get; private set; }
    public AudioSource StartupSource => startupSource;
    public AudioSource LoopSource => loopSource;
    AudioSource startupSource, loopSource;
    float remainingStart, startupGain, loopGain;
    bool commandedOn, loopStarted;

    void Awake() { Initialize(); }
    public void Initialize()
    {
        if (startupSource && loopSource) return;
        startupSource = CreateSource("Engine starting", false, startupClip);
        loopSource = CreateSource("Engine running", true, engineLoop);
        State = Phase.Off;
    }
    AudioSource CreateSource(string name, bool loop, AudioClip clip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = engineOffset;
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop; source.clip = clip;
        source.volume = 0; source.spatialBlend = 1;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 2; source.maxDistance = 35;
        source.dopplerLevel = 0; source.priority = 64;
        return source;
    }
    void Update() { Step(Ignition.EngineRunning, throttle ? throttle.Value : 0, Time.deltaTime); }
    public void Step(bool running, float gas, float dt)
    {
        if (dt <= 0) return;
        if (!startupSource || !loopSource) Initialize();
        if (running && !commandedOn)
        {
            startupSource.Stop(); loopSource.Stop();
            remainingStart = startupClip ? startupClip.length : 0;
            startupGain = remainingStart > 0 ? 1 : 0;
            loopGain = Rpm = 0; loopStarted = false;
            startupSource.pitch = 1; loopSource.pitch = idlePitch;
            if (remainingStart > 0 && Application.isPlaying) startupSource.Play();
        }
        commandedOn = running;
        if (running)
        {
            remainingStart = Mathf.Max(0, remainingStart - dt);
            if (remainingStart <= crossfade && !loopStarted)
            {
                loopStarted = true;
                if (engineLoop && Application.isPlaying) loopSource.Play();
            }
            float blend = Mathf.Clamp01(1 - remainingStart / Mathf.Max(0.05f, crossfade));
            startupGain = startupClip ? 1 - blend : 0;
            loopGain = Mathf.MoveTowards(loopGain, blend, dt / Mathf.Max(0.05f, crossfade));
            Rpm = Mathf.MoveTowards(Rpm, loopStarted ? Mathf.Clamp01(gas) : 0, dt / Mathf.Max(0.05f, rpmResponse));
            loopSource.pitch = Mathf.Lerp(idlePitch, fullPitch, Rpm);
            State = remainingStart > 0 ? Phase.Starting : Phase.Running;
        }
        else
        {
            remainingStart = 0;
            startupGain = Mathf.MoveTowards(startupGain, 0, dt / Mathf.Max(0.05f, shutdownFade));
            loopGain = Mathf.MoveTowards(loopGain, 0, dt / Mathf.Max(0.05f, shutdownFade));
            Rpm = Mathf.MoveTowards(Rpm, 0, dt * 2 / Mathf.Max(0.05f, rpmResponse));
            startupSource.pitch = Mathf.MoveTowards(startupSource.pitch, 0.7f, dt * 0.8f);
            loopSource.pitch = Mathf.MoveTowards(loopSource.pitch, 0.65f, dt * 0.6f);
            State = startupGain > 0 || loopGain > 0 ? Phase.Stopping : Phase.Off;
        }
        startupSource.volume = fullVolume * startupGain;
        loopSource.volume = Mathf.Lerp(idleVolume, fullVolume, Rpm) * loopGain;
        if (startupGain == 0) startupSource.Stop();
        if (State == Phase.Off) { loopSource.Stop(); loopStarted = false; }
    }
    void OnDisable()
    {
        if (startupSource) { startupSource.Stop(); startupSource.volume = 0; }
        if (loopSource) { loopSource.Stop(); loopSource.volume = 0; }
        commandedOn = loopStarted = false;
        remainingStart = startupGain = loopGain = Rpm = 0;
        State = Phase.Off;
    }
}
