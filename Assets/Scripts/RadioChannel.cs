using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class RadioLine
{
    public AudioClip clip;
    [TextArea] public string subtitle;       // nebūtina; jei nėra garso, rodoma kaip tekstas
    [Min(0)] public float delayBefore = 0.3f;
}

public class RadioChannel : MonoBehaviour
{
    public static RadioChannel I;
    public static float EngineDuck = 1f;     // ExcavatorEngineAudio dauginasi iš šios reikšmės
    public static bool IsTalking => I && I.talking;

    [Range(0f, 1f)] public float engineVolumeWhileTalking = 0.3f;
    [Min(0.1f)] public float duckSpeed = 4f;
    [Range(0f, 1f)] public float radioVolume = 1f;
    public UnityEvent<string> onSubtitle;    // prijunkite prie UI teksto (SetText)

    readonly Queue<RadioLine> queue = new Queue<RadioLine>();
    AudioSource source;
    bool talking, running;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic() { I = null; EngineDuck = 1f; }

    void Awake()
    {
        I = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0;             // radijas girdimas kabinoje, 2D
        source.volume = radioVolume;
    }

    void OnDestroy() { if (I == this) { I = null; EngineDuck = 1f; } }

    void Update()
    {
        EngineDuck = Mathf.MoveTowards(EngineDuck, talking ? engineVolumeWhileTalking : 1f, duckSpeed * Time.deltaTime);
    }

    public void Say(params RadioLine[] lines)
    {
        if (lines == null) return;
        foreach (var l in lines) if (l != null) queue.Enqueue(l);
        if (!running) StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        running = true;
        talking = true;
        while (queue.Count > 0)
        {
            var line = queue.Dequeue();
            yield return new WaitForSeconds(line.delayBefore);
            onSubtitle?.Invoke(line.subtitle);
            float duration = line.clip ? line.clip.length : Mathf.Max(2f, (line.subtitle?.Length ?? 0) * 0.06f);
            if (line.clip) { source.volume = radioVolume; source.PlayOneShot(line.clip); }
            yield return new WaitForSeconds(duration);
        }
        onSubtitle?.Invoke("");
        yield return new WaitForSeconds(0.25f);  // trumpa uodega, kad variklis neatšoktų per staigiai
        talking = false;
        running = false;
        if (queue.Count > 0) { running = true; talking = true; StartCoroutine(Run()); } // jei kas nors įsiterpė
    }
}