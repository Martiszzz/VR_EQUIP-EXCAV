using UnityEngine;
using UnityEngine.Rendering;

// One bounded particle system per bucket; grain collisions do not duplicate terrain volume.
public class SandFlow : MonoBehaviour
{
    [Range(8, 120)] public int grainsPerChunk = 80;
    [Min(0.001f)] public float grainSize = 0.006f;
    [Range(200, 3000)] public int particleBudget = 2400;
    ParticleSystem grains;
    Material material;
    Mesh mesh;


    public ParticleSystem Particles => grains;


    public void Initialize(BucketInside container)
    {
        if (grains) return;

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
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(grainSize * 0.55f, grainSize * 1.4f);
        main.startSpeed = 0;
        main.gravityModifier = 1f;
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.28f, 0.17f, 0.08f), new Color(0.48f, 0.31f, 0.16f));
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
        collision.bounce = 0;
        collision.lifetimeLoss = 1f;
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
        material.SetColor("_BaseColor", new Color(0.50f, 0.34f, 0.18f));
        material.SetColor("_Color", new Color(0.50f, 0.34f, 0.18f));
        material.SetFloat("_Smoothness", 0.05f);
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = mesh;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        grains.Play();
    }

    public void EmitLoose(Vector3 position, Vector3 velocity, float volume)
    {
        if (!grains || volume <= 0) return;
        float unit = DigSystem.I ? DigSystem.I.BlockVolume : 0.06f * 0.06f * 0.06f;
        int count = Mathf.Min(Mathf.RoundToInt(volume / unit * grainsPerChunk), particleBudget - grains.particleCount);
        for (int i = 0; i < count; i++)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position + Random.insideUnitSphere * 0.012f,
                velocity = velocity + Random.insideUnitSphere * 0.12f,
                applyShapeToPosition = false
            };
            grains.Emit(emit, 1);
        }
    }
    public void EmitImpact(Vector3 position, Vector3 normal, int count)
    {
        if (!grains) return;
        count = Mathf.Min(count, Mathf.Max(0, particleBudget - grains.particleCount));
        for (int i = 0; i < count; i++)
        {
            var emit = new ParticleSystem.EmitParams
            {
                position = position + Random.insideUnitSphere * 0.01f,
                velocity = normal * Random.Range(0.08f, 0.25f) + Vector3.ProjectOnPlane(Random.insideUnitSphere, normal) * 0.25f,
                startSize = grainSize * Random.Range(0.4f, 0.8f),
                startLifetime = Random.Range(0.5f, 1.1f),
                applyShapeToPosition = false
            };
            grains.Emit(emit, 1);
        }
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
