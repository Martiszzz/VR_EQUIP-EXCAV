using UnityEngine;
using UnityEngine.Rendering;

// Visual/audio effects never own soil volume. One reusable, bounded emitter serves the whole machine.
public class SoilWorkFeedback : MonoBehaviour
{
    public DigSystem soil;
    public AudioClip soilLoop;
    public Shader dustShader;
    [Range(0,1)] public float soundVolume=0.3f;
    [Range(16,256)] public int dustBudget=128;
    [Min(5)] public float effectDistance=20;
    ParticleSystem dust;
    AudioSource sound;
    Material dustMaterial;
    Texture2D dustTexture;
    AudioClip generatedLoop;
    Transform observer;
    float work,clock,activity;
    Vector3 workPosition;
    public int ParticleCount => dust?dust.particleCount:0;
    void Start()
    {
        if(!soil)soil=GetComponent<DigSystem>();
        var camera=Camera.main;observer=camera?camera.transform:null;
        var go=new GameObject("Soil dust");go.transform.SetParent(transform,false);
        dust=go.AddComponent<ParticleSystem>();dust.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main=dust.main;main.playOnAwake=false;main.loop=true;main.maxParticles=dustBudget;main.simulationSpace=ParticleSystemSimulationSpace.World;
        main.startLifetime=new ParticleSystem.MinMaxCurve(0.3f,0.65f);main.startSize=new ParticleSystem.MinMaxCurve(0.006f,0.025f);main.startSpeed=0;main.gravityModifier=-0.015f;
        main.startColor=new ParticleSystem.MinMaxGradient(new Color(.35f,.24f,.13f,.2f),new Color(.5f,.38f,.23f,.35f));
        var emission=dust.emission;emission.enabled=false;var shape=dust.shape;shape.enabled=false;
        // A serialized shader reference also retains this shader in standalone builds.
        var shader=dustShader?dustShader:Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if(!shader)shader=Shader.Find("Universal Render Pipeline/Lit");if(!shader)shader=Shader.Find("Sprites/Default");
        dustMaterial=new Material(shader);if(dustMaterial.HasProperty("_Surface"))dustMaterial.SetFloat("_Surface",1);
        dustMaterial.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);dustMaterial.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);dustMaterial.SetInt("_ZWrite",0);
        dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");dustMaterial.renderQueue=3000;
        dustTexture=new Texture2D(32,32,TextureFormat.RGBA32,false);var pixels=new Color[1024];
        for(int y=0;y<32;y++)for(int x=0;x<32;x++)
        {float dx=(x-15.5f)/15.5f,dy=(y-15.5f)/15.5f;float alpha=Mathf.Pow(Mathf.Clamp01(1-dx*dx-dy*dy),2);pixels[y*32+x]=new Color(1,1,1,alpha);}
        dustTexture.SetPixels(pixels);dustTexture.Apply();
        if(dustMaterial.HasProperty("_BaseMap"))dustMaterial.SetTexture("_BaseMap",dustTexture);else dustMaterial.mainTexture=dustTexture;
        var renderer=dust.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=dustMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        var fade=dust.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=gradient;
        dust.Play();
        var audio=new GameObject("Soil movement sound");audio.transform.SetParent(transform,false);sound=audio.AddComponent<AudioSource>();
        sound.playOnAwake=false;sound.loop=true;sound.spatialBlend=1;sound.dopplerLevel=0;sound.minDistance=1;sound.maxDistance=effectDistance;sound.volume=0;
        if(!soilLoop)
        {
            var samples=new float[12000];var random=new System.Random(5421);float smooth=0;
            for(int i=0;i<samples.Length;i++){smooth=smooth*.7f+((float)random.NextDouble()*2-1)*.3f;samples[i]=smooth*.7f;}
            generatedLoop=AudioClip.Create("Soil rustle",samples.Length,1,12000,false);generatedLoop.SetData(samples,0);
        }
        sound.clip=soilLoop?soilLoop:generatedLoop;
    }
    public void Report(Vector3 point,float volume)
    {if(volume<=0 || (soil && !soil.IsWorkPoint(point)))return;workPosition=point;work+=volume;}
    void Update()
    {
        float dt=Time.deltaTime;if(dt<=0)return;
        clock+=dt;
        if(!dust || !sound || clock<.06f)return;
        bool near=!observer || (observer.position-workPosition).sqrMagnitude<effectDistance*effectDistance;
        float strength=near?Mathf.Clamp01(work/Mathf.Max(.00001f,.012f*clock)):0;
        activity=Mathf.MoveTowards(activity,strength,clock*4);
        sound.transform.position=workPosition;sound.volume=activity*soundVolume;
        if(activity>.01f && !sound.isPlaying)sound.Play();else if(activity<=.001f && sound.isPlaying)sound.Stop();
        int count=near?Mathf.Min(12,Mathf.Min(Mathf.CeilToInt(work/.00015f),dustBudget-dust.particleCount)):0;
        if(count>0){var emit=new ParticleSystem.EmitParams{position=workPosition,velocity=Vector3.up*.12f};dust.Emit(emit,count);}
        work=clock=0;
    }
    void OnDestroy()
    {Dispose(dustMaterial);Dispose(dustTexture);Dispose(generatedLoop);}
    static void Dispose(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
}
