using UnityEngine;
using UnityEngine.Rendering;

// One bounded particle system per bucket; grain collisions do not duplicate terrain volume.
public class SandFlow : MonoBehaviour
{
    [Range(8, 120)] public int grainsPerChunk = 80;
    [Min(0.001f)] public float grainSize = 0.006f;
    [Range(200, 3000)] public int particleBudget = 2400;
    BucketInside bucket;
    ParticleSystem grains;
    Material material;
    Mesh mesh;
    float pending, fractional;
    Vector3 lastPosition, velocity;
    public ParticleSystem Particles => grains;
    public float PendingGrains => pending;

    public void Initialize(BucketInside container)
    {
        if (grains) return;
        bucket = container;
        lastPosition = bucket.transform.position;
        var go = new GameObject("Pouring sand");
        go.transform.SetParent(transform, false);
        grains = go.AddComponent<ParticleSystem>();
        grains.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = grains.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.maxParticles = particleBudget;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.7f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(grainSize * 0.55f, grainSize * 1.4f);
        main.startSpeed = 0;
        main.gravityModifier = 1f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.48f, 0.31f, 0.15f), new Color(0.76f, 0.58f, 0.33f));
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2f);
        var emission = grains.emission; emission.enabled = false;
        var shape = grains.shape; shape.enabled = false;
        var collision = grains.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.collidesWith = ~0;
        collision.enableDynamicColliders = true;
        collision.maxCollisionShapes = 128;
        collision.bounce = 0.04f;
        collision.dampen = 0.65f;
        collision.radiusScale = 0.5f;
        collision.sendCollisionMessages = false;
        var size = grains.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(0.75f, 1), new Keyframe(1, 0)));

        // Six vertices keep the stream inexpensive for VR, with no texture dependency.
        mesh = new Mesh { name = "Sand grain" };
        mesh.vertices = new[] { Vector3.up * 0.5f, Vector3.down * 0.5f,
            Vector3.right * 0.4f, Vector3.left * 0.4f, Vector3.forward * 0.45f, Vector3.back * 0.45f };
        mesh.triangles = new[] { 0,4,2, 0,3,4, 0,5,3, 0,2,5, 1,2,4, 1,4,3, 1,3,5, 1,5,2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var shader = Shader.Find("Universal Render Pipeline/Particles/Lit");
        if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        material = new Material(shader) { name = "Sand grains (runtime)" };
        material.SetColor("_BaseColor", new Color(0.68f, 0.49f, 0.26f));
        material.SetColor("_Color", new Color(0.68f, 0.49f, 0.26f));
        material.SetFloat("_Smoothness", 0.05f);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        grains.Play();
    }

    public void AddVolume(float volume)
    {
        float unit = DigSystem.I ? DigSystem.I.BlockVolume : 0.06f * 0.06f * 0.06f;
        pending = Mathf.Min(particleBudget, pending + volume / unit * grainsPerChunk);
    }

    void Update() { Step(Time.deltaTime); }
    public void Step(float dt)
    {
        if (!bucket || !grains || dt <= 0) return;
        velocity = Vector3.ClampMagnitude((bucket.transform.position - lastPosition) / dt, 5f);
        lastPosition = bucket.transform.position;
        float rate = Mathf.Max(120f, bucket.dumpRate * bucket.PourStrength * grainsPerChunk);
        fractional += Mathf.Min(pending, rate * dt);
        int count = Mathf.Min(Mathf.FloorToInt(fractional), Mathf.FloorToInt(pending));
        fractional -= count;
        pending -= count;
        for (int i = 0; i < count; i++)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = bucket.PourPosition((Random.value + Random.value - 1f) * 0.4f) + Random.insideUnitSphere * 0.007f,
                velocity = velocity + bucket.transform.TransformDirection(bucket.lipAxis.normalized) *
                    Random.Range(0.15f, 0.5f) + Random.insideUnitSphere * 0.13f,
                applyShapeToPosition = false
            };
            grains.Emit(emit, 1);
        }
        if (pending < 1f) { pending = 0; fractional = 0; }
    }

    void OnDestroy()
    {
        if (grains) Dispose(grains.gameObject);
        if (material) Dispose(material);
        if (mesh) Dispose(mesh);
    }
    static void Dispose(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
