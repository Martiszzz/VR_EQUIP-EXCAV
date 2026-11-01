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
    public Material screenMaterial;
    public bool requiresEngine;
    public bool powerOn;
    [Range(0, 1)] public float volume = 0.35f;
    public int stationIndex;

    AudioSource music, staticSource;
    AudioClip noise;
    TextMeshPro display;
    float tuningUntil;
    float volumeDisplayUntil;
    bool receiving;
    string lastText;

    void Awake()
    {
        BindModelControls();
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

        string text = requiresEngine && !Ignition.EngineRunning ? "ENGINE\nOFF" : !powerOn ? "RADIO\nOFF" :
            stations == null || stations.Length == 0 ? "NO STATIONS" :
            tuning ? "FM\n..." : Time.unscaledTime < volumeDisplayUntil ? "VOL\n" + Mathf.RoundToInt(volume * 100) :
            "FM\n" + stations[stationIndex].frequency;
        if (text != lastText) { display.text = text; lastText = text; }
    }

    public void TogglePower() { powerOn = !powerOn; }
    public void AdjustVolume(float degrees)
    {
        volume = Mathf.Clamp01(volume + degrees / 270);
        volumeDisplayUntil = Time.unscaledTime + 1.2f;
    }
    public void SelectStation(int index)
    {
        if (stations == null || stations.Length == 0) return;
        stationIndex = Mathf.Clamp(index, 0, stations.Length - 1);
        if (receiving) Tune();
    }
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

    void Control(string controlName, Vector3 position, Vector3 size, ExcavatorRadioControl.Action action)
    {
        var go = new GameObject(controlName);
        // Register with XRI only after its explicit collider list is configured.
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        var collider = go.AddComponent<BoxCollider>();
        collider.size = size;
        // XRI rays ignore triggers in these scenes. A queryable solid collider with
        // all contact layers excluded supports ray queries without vehicle forces.
        collider.excludeLayers = ~0;
        // Direct interactors may use OnTrigger events instead of overlap queries.
        // Keep a separate trigger with normal contact filtering for those hands.
        var touchCollider = go.AddComponent<BoxCollider>();
        touchCollider.size = size;
        touchCollider.isTrigger = true;
        var interactable = go.AddComponent<XRSimpleInteractable>();
        interactable.colliders.Clear();
        interactable.colliders.Add(collider);
        interactable.colliders.Add(touchCollider);
        var control = go.AddComponent<ExcavatorRadioControl>();
        control.Initialize(this, action, interactable);
        go.SetActive(true);
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
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    void BindModelControls()
    {
        // Positions measured from Head2.fbx's existing radio: two knobs, eight
        // bottom-row buttons and the original FM 100.1 display. No replacement case.
        Control("Original radio volume knob", new Vector3(-0.049818f, 0, -0.001f),
            new Vector3(0.018f, 0.018f, 0.012f), ExcavatorRadioControl.Action.Volume);
        Control("Original radio tuning knob", new Vector3(-0.033215f, 0.010695f, -0.001f),
            new Vector3(0.015f, 0.015f, 0.012f), ExcavatorRadioControl.Action.Tune);

        var actions = new[] { ExcavatorRadioControl.Action.Power, ExcavatorRadioControl.Action.Previous,
            ExcavatorRadioControl.Action.Next, ExcavatorRadioControl.Action.Preset1,
            ExcavatorRadioControl.Action.Preset2, ExcavatorRadioControl.Action.Preset3,
            ExcavatorRadioControl.Action.VolumeDown, ExcavatorRadioControl.Action.VolumeUp };
        for (int i = 0; i < actions.Length; i++)
            Control("Original radio button " + (i + 1) + " - " + actions[i],
                new Vector3(-0.05812f + i * 0.016604f, -0.016044f, -0.001f),
                new Vector3(0.011f, 0.011f, 0.012f), actions[i]);

        // Replace only the printed LCD content. The case and all buttons remain
        // the original mesh; a thin screen surface hides its baked '100.1' text.
        var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
        screen.name = "Original radio LCD content";
        screen.transform.SetParent(transform, false);
        screen.transform.localPosition = new Vector3(0.0332f, 0.005348f, 0.0005f);
        screen.transform.localScale = new Vector3(0.063f, 0.0287f, 1);
        screen.GetComponent<Collider>().enabled = false;
        Destroy(screen.GetComponent<Collider>());
        screen.GetComponent<Renderer>().sharedMaterial = screenMaterial;
        display = Label("Original radio frequency", "RADIO\nOFF", new Vector3(0.0332f, 0.005348f, 0.0003f),
            new Vector2(0.061f, 0.027f), 0.09f);
    }
}
