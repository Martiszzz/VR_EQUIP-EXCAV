using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// Mount in the cab, with local -Z facing the listener. Dimensions are in metres.
[DefaultExecutionOrder(250)]
public class ExcavatorRadio : MonoBehaviour
{
    [Serializable]
    public class Station
    {
        public string name;
        public string frequency;
        public AudioClip clip;
    }

    public Station[] stations;
    public TMP_FontAsset displayFont;
    public Material panelMaterial;
    public Material controlMaterial;
    public Material screenMaterial;
    public bool requiresEngine = true;
    public bool powerOn;
    [Range(0, 1)] public float volume = 0.35f;
    public int stationIndex;

    AudioSource music, staticSource;
    AudioClip noise;
    TextMeshPro display;
    ExcavatorRadioControl volumeKnob, tuningKnob;
    float tuningUntil;
    bool receiving;
    string lastText;

    void Awake()
    {
        BuildPanel();
        music = CreateSource("Radio broadcast");
        staticSource = CreateSource("Radio tuning");
        // Short, deterministic noise buffer; never synthesize on the audio thread.
        var random = new System.Random(481);
        var samples = new float[11025];
        float previous = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            previous = Mathf.Lerp(previous, (float)random.NextDouble() * 2 - 1, 0.6f);
            samples[i] = previous * 0.18f;
        }
        noise = AudioClip.Create("Radio static", samples.Length, 1, 22050, false);
        noise.SetData(samples, 0);
        staticSource.clip = noise;
        stationIndex = stations != null && stations.Length > 0 ? Mathf.Clamp(stationIndex, 0, stations.Length - 1) : 0;
    }

    AudioSource CreateSource(string sourceName)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0;
        source.spatialBlend = 1;
        source.dopplerLevel = 0;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 0.7f;
        source.maxDistance = 5;
        source.priority = 128;
        child.AddComponent<AudioLowPassFilter>().cutoffFrequency = 6500;
        return source;
    }

    void Update()
    {
        bool powered = powerOn && (!requiresEngine || Ignition.EngineRunning);
        if (powered != receiving)
        {
            receiving = powered;
            if (powered) Tune();
            else { music.Stop(); staticSource.Stop(); }
        }
        bool tuning = powered && Time.unscaledTime < tuningUntil;
        if (tuning && !staticSource.isPlaying) staticSource.Play();
        if (!tuning && staticSource.isPlaying) staticSource.Stop();
        music.volume = Mathf.MoveTowards(music.volume, powered && !tuning ? volume * 0.7f : 0, Time.unscaledDeltaTime * 2);
        staticSource.volume = powered ? volume * 0.55f : 0;

        string text = requiresEngine && !Ignition.EngineRunning ? "IGNITION OFF" : !powerOn ? "RADIO OFF" :
            stations == null || stations.Length == 0 ? "NO STATIONS" :
            tuning ? "TUNING..." : stations[stationIndex].frequency + " FM\n" + stations[stationIndex].name;
        if (text != lastText) { display.text = text; lastText = text; }
        volumeKnob.SetAngle(Mathf.Lerp(-135, 135, volume));
        tuningKnob.SetAngle(stationIndex * 45);
    }

    public void TogglePower() { powerOn = !powerOn; }
    public void AdjustVolume(float degrees) { volume = Mathf.Clamp01(volume + degrees / 270); }
    public void ChangeStation(int direction)
    {
        if (stations == null || stations.Length == 0) return;
        stationIndex = (stationIndex + direction % stations.Length + stations.Length) % stations.Length;
        if (receiving) Tune();
    }

    void Tune()
    {
        tuningUntil = Time.unscaledTime + 0.28f;
        music.Stop();
        music.volume = 0;
        if (stations == null || stations.Length == 0 || !stations[stationIndex].clip) return;
        music.clip = stations[stationIndex].clip;
        // Stations continue their programme while the listener switches channels.
        if (music.clip.loadState == AudioDataLoadState.Loaded && music.clip.samples > 0)
            music.timeSamples = (int)((AudioSettings.dspTime * music.clip.frequency) % music.clip.samples);
        music.Play();
    }

    void OnDisable()
    {
        if (music) music.Stop();
        if (staticSource) staticSource.Stop();
        receiving = false;
    }

    void OnDestroy() { if (noise) Destroy(noise); }

    GameObject Shape(string shapeName, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(primitive);
        go.name = shapeName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        // Cab controls must never add solid contacts to the excavator body.
        go.GetComponent<Collider>().isTrigger = true;
        return go;
    }

    ExcavatorRadioControl Control(string controlName, Vector3 position, bool knob, ExcavatorRadioControl.Action action)
    {
        var go = Shape(controlName, knob ? PrimitiveType.Cylinder : PrimitiveType.Cube, position,
            knob ? new Vector3(0.035f, 0.009f, 0.035f) : new Vector3(0.032f, 0.018f, 0.014f), controlMaterial);
        if (knob) go.transform.localRotation = Quaternion.Euler(90, 0, 0);
        var interactable = go.AddComponent<XRSimpleInteractable>();
        interactable.colliders.Clear();
        interactable.colliders.Add(go.GetComponent<Collider>());
        var control = go.AddComponent<ExcavatorRadioControl>();
        control.Initialize(this, action, interactable);
        if (knob)
        {
            var mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = "Dial marker";
            mark.transform.SetParent(go.transform, false);
            mark.transform.localPosition = new Vector3(0, -0.55f, 0.28f);
            mark.transform.localScale = new Vector3(0.08f, 0.08f, 0.3f);
            mark.GetComponent<Renderer>().sharedMaterial = screenMaterial;
            // Remove only this visual marker's collider, not the interactive dial.
            mark.SetActive(false);
            Destroy(mark.GetComponent<Collider>());
            mark.SetActive(true);
        }
        return control;
    }

    TextMeshPro Label(string labelName, string text, Vector3 position, Vector2 size, float fontSize)
    {
        var go = new GameObject(labelName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        var label = go.AddComponent<TextMeshPro>();
        label.font = displayFont;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.72f, 0.95f, 0.72f);
        label.rectTransform.sizeDelta = size;
        label.enableWordWrapping = false;
        return label;
    }

    void BuildPanel()
    {
        var body = Shape("Radio fascia", PrimitiveType.Cube, Vector3.zero, new Vector3(0.30f, 0.095f, 0.012f), panelMaterial);
        body.GetComponent<Collider>().enabled = false;
        var screen = Shape("Radio LCD", PrimitiveType.Cube, new Vector3(0, 0.014f, -0.008f), new Vector3(0.165f, 0.043f, 0.003f), screenMaterial);
        screen.GetComponent<Collider>().enabled = false;
        display = Label("Frequency display", "RADIO OFF", new Vector3(0, 0.014f, -0.011f), new Vector2(0.16f, 0.04f), 0.065f);
        volumeKnob = Control("Volume - grab and twist", new Vector3(-0.119f, 0, -0.018f), true, ExcavatorRadioControl.Action.Volume);
        tuningKnob = Control("Station - grab and twist", new Vector3(0.119f, 0, -0.018f), true, ExcavatorRadioControl.Action.Tune);
        Control("Radio power", new Vector3(-0.047f, -0.027f, -0.014f), false, ExcavatorRadioControl.Action.Power);
        Control("Previous station", new Vector3(0, -0.027f, -0.014f), false, ExcavatorRadioControl.Action.Previous);
        Control("Next station", new Vector3(0.047f, -0.027f, -0.014f), false, ExcavatorRadioControl.Action.Next);
        Label("Button labels", "PWR       <          >", new Vector3(0, -0.027f, -0.022f), new Vector2(0.14f, 0.018f), 0.045f);
        Label("Volume label", "VOL", new Vector3(-0.119f, -0.032f, -0.012f), new Vector2(0.05f, 0.015f), 0.04f);
        Label("Tuner label", "TUNE", new Vector3(0.119f, -0.032f, -0.012f), new Vector2(0.05f, 0.015f), 0.04f);
    }
}
