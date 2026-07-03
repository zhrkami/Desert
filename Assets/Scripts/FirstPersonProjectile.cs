using System;
using UnityEngine;

public class FirstPersonProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 70f;
    [SerializeField] private float damage = 1f;
    [SerializeField] private float hitRadius = 0.08f;
    [SerializeField] private float lifetime = 4f;
    [SerializeField] private float impactForce = 16f;
    [SerializeField] private LayerMask hitLayers = ~0;

    private static readonly Color ProjectileColor = new Color(1f, 0.88f, 0.08f, 1f);
    private static readonly Color ProjectileEmissionColor = new Color(4f, 2.5f, 0.12f, 1f);
    private static readonly Color ProjectileLightColor = new Color(1f, 0.78f, 0.08f, 1f);
    private static readonly Color ImpactSparkColor = new Color(1f, 0.68f, 0.05f, 1f);
    private static Material runtimeProjectileMaterial;

    private Vector3 direction;
    private Transform ownerRoot;
    private float spawnTime;
    private bool initialized;

    public void Initialize(
        Vector3 shotDirection,
        Transform newOwnerRoot,
        float newSpeed,
        float newDamage,
        float newHitRadius,
        float newLifetime,
        LayerMask newHitLayers,
        Material visualMaterial)
    {
        direction = shotDirection.sqrMagnitude > 0.0001f ? shotDirection.normalized : transform.forward;
        ownerRoot = newOwnerRoot;
        speed = Mathf.Max(0.01f, newSpeed);
        damage = Mathf.Max(0f, newDamage);
        hitRadius = Mathf.Max(0.01f, newHitRadius);
        lifetime = Mathf.Max(0.05f, newLifetime);
        hitLayers = newHitLayers;
        spawnTime = Time.time;
        initialized = true;

        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        ApplyMaterial(visualMaterial);
        ConfigureLight();
    }

    private void Awake()
    {
        ConfigureCollider();
    }

    private void Start()
    {
        if (!initialized)
        {
            Initialize(transform.forward, null, speed, damage, hitRadius, lifetime, hitLayers, null);
        }
    }

    private void Update()
    {
        float stepDistance = speed * Time.deltaTime;
        if (TryGetHit(stepDistance, out RaycastHit hit))
        {
            transform.position = hit.point;
            HandleHit(hit);
            return;
        }

        transform.position += direction * stepDistance;

        if (Time.time - spawnTime >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private bool TryGetHit(float distance, out RaycastHit selectedHit)
    {
        selectedHit = default;
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            hitRadius,
            direction,
            distance,
            hitLayers,
            QueryTriggerInteraction.Ignore);

        if (hits.Length == 0)
        {
            return false;
        }

        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.collider == null || ShouldIgnoreCollider(hit.collider))
            {
                continue;
            }

            selectedHit = hit;
            return true;
        }

        return false;
    }

    private bool ShouldIgnoreCollider(Collider other)
    {
        if (other.transform.IsChildOf(transform))
        {
            return true;
        }

        return ownerRoot != null && other.transform.IsChildOf(ownerRoot);
    }

    private void HandleHit(RaycastHit hit)
    {
        Rigidbody hitBody = hit.collider.attachedRigidbody;
        if (hitBody != null && (ownerRoot == null || !hitBody.transform.IsChildOf(ownerRoot)))
        {
            hitBody.AddForceAtPosition(direction * impactForce, hit.point, ForceMode.Impulse);
        }

        hit.collider.SendMessageUpwards("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        SpawnImpactSpark(hit.point, hit.normal);
        Destroy(gameObject);
    }

    private void SpawnImpactSpark(Vector3 position, Vector3 normal)
    {
        GameObject impact = new GameObject("BlasterImpact");
        impact.transform.position = position + normal * 0.02f;
        impact.transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0.0001f ? normal : -direction);

        ParticleSystem particles = impact.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.18f;
        main.startLifetime = 0.18f;
        main.startSpeed = 1.8f;
        main.startSize = 0.05f;
        main.startColor = ImpactSparkColor;
        main.maxParticles = 16;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.08f;

        particles.Play();
        Destroy(impact, 0.6f);
    }

    private void ConfigureCollider()
    {
        Collider projectileCollider = GetComponent<Collider>();
        if (projectileCollider != null)
        {
            projectileCollider.isTrigger = true;
        }
    }

    private void ApplyMaterial(Material visualMaterial)
    {
        Renderer projectileRenderer = GetComponentInChildren<Renderer>();
        if (projectileRenderer == null)
        {
            return;
        }

        projectileRenderer.sharedMaterial = visualMaterial != null ? visualMaterial : GetRuntimeProjectileMaterial();
    }

    private void ConfigureLight()
    {
        Light projectileLight = GetComponent<Light>();
        if (projectileLight == null)
        {
            projectileLight = gameObject.AddComponent<Light>();
        }

        projectileLight.type = LightType.Point;
        projectileLight.color = ProjectileLightColor;
        projectileLight.intensity = 1.8f;
        projectileLight.range = 1.4f;
        projectileLight.shadows = LightShadows.None;
    }

    private static Material GetRuntimeProjectileMaterial()
    {
        if (runtimeProjectileMaterial != null)
        {
            return runtimeProjectileMaterial;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Diffuse");
        }

        runtimeProjectileMaterial = new Material(shader);
        runtimeProjectileMaterial.name = "Runtime_BlasterProjectile";
        runtimeProjectileMaterial.color = ProjectileColor;
        runtimeProjectileMaterial.EnableKeyword("_EMISSION");
        runtimeProjectileMaterial.SetColor("_EmissionColor", ProjectileEmissionColor);
        return runtimeProjectileMaterial;
    }
}
