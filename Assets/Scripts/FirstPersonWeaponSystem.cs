using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class FirstPersonWeaponSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera aimCamera;
    [SerializeField] private SimplePlayerController playerController;
    [SerializeField] private Transform playerRoot;
    [SerializeField] private Transform weaponAimRoot;
    [SerializeField] private Transform weaponVisualRoot;
    [SerializeField] private Transform muzzle;
    [SerializeField] private ParticleSystem muzzleFlashVfx;
    [SerializeField] private ParticleSystem overheatVfx;
    [SerializeField] private AudioClip shootSfx;
    [SerializeField] private AudioClip coolingSfx;
    [SerializeField] private Renderer[] overheatRenderers;
    [SerializeField] private int[] overheatMaterialIndexes;
    [SerializeField] private Transform[] fuelCells;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Material projectileMaterial;

    [Header("Aiming")]
    [SerializeField] private float maxAimDistance = 250f;
    [SerializeField] private float weaponTrackSharpness = 28f;
    [SerializeField] private LayerMask aimLayers = ~0;
    [SerializeField] private LayerMask projectileHitLayers = ~0;

    [Header("Shooting")]
    [SerializeField] private float delayBetweenShots = 0.1f;
    [SerializeField] private float maxAmmo = 16f;
    [SerializeField] private float ammoReloadRate = 16f;
    [SerializeField] private float ammoReloadDelay = 1f;
    [SerializeField] private float projectileSpeed = 80f;
    [SerializeField] private float projectileDamage = 30f;
    [SerializeField] private float projectileRadius = 0.08f;
    [SerializeField] private float projectileLifetime = 4f;

    [Header("Animation")]
    [SerializeField] private Vector3 cameraLocalWeaponPosition = new Vector3(0.36f, -0.34f, 0.62f);
    [SerializeField] private Vector3 cameraLocalWeaponEuler = new Vector3(-4f, 0f, 0f);
    [SerializeField] private float recoilKickDistance = 0.055f;
    [SerializeField] private float recoilReturnSharpness = 20f;
    [SerializeField] private Vector3 fuelCellUsedOffset = new Vector3(0f, 0.1f, 0f);
    [SerializeField] private Vector3 fuelCellUnusedOffset;
    [SerializeField] private float overheatVfxEmissionRateMax = 8f;
    [SerializeField] private Color overheatCoolColor = new Color(0.314f, 3.482f, 0f, 1f);
    [SerializeField] private Color overheatHotColor = new Color(5.216f, 1.557f, 0f, 1f);

    [Header("Crosshair")]
    [SerializeField] private bool showCrosshair = true;
    [SerializeField] private float crosshairLength = 9f;
    [SerializeField] private float crosshairGap = 7f;
    [SerializeField] private float crosshairThickness = 2f;
    [SerializeField] private Color crosshairColor = new Color(1f, 1f, 1f, 0.75f);
    [SerializeField] private Color crosshairTargetColor = new Color(0.35f, 1f, 0.25f, 1f);

    private static readonly string[] FuelCellNames =
    {
        "Primary_Weapon_Container_04",
        "Primary_Weapon_Container_02",
        "Primary_Weapon_Container_03",
        "Primary_Weapon_Container_01",
    };

    private float currentAmmo;
    private float lastShotTime = Mathf.NegativeInfinity;
    private float lastFireInputTime = Mathf.NegativeInfinity;
    private float recoilDistance;
    private bool currentAimHit;
    private Vector3 currentAimPoint;
    private Vector3 weaponVisualDefaultLocalPosition;
    private Vector3[] fuelCellDefaultLocalPositions;
    private MaterialPropertyBlock overheatPropertyBlock;
    private AudioSource shootAudioSource;
    private AudioSource coolingAudioSource;
    private Texture2D crosshairPixel;

    public float CurrentAmmoRatio
    {
        get { return maxAmmo <= 0f ? 1f : Mathf.Clamp01(currentAmmo / maxAmmo); }
    }

    private void Awake()
    {
        AutoWireMissingReferences();
        currentAmmo = maxAmmo;
        CacheDefaultTransforms();
        ConfigureAudioSources();
        ConfigureVfxDefaults();
        overheatPropertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        bool canUseWeapon = CanUseWeapon();
        SetWeaponVisible(canUseWeapon);

        if (!canUseWeapon)
        {
            ReloadWhenIdle(false);
            UpdateWeaponAnimation(Time.deltaTime);
            return;
        }

        UpdateAimPoint();
        AimWeaponAtPoint(Time.deltaTime);

        bool fireHeld = Input.GetMouseButton(0);
        if (fireHeld)
        {
            lastFireInputTime = Time.time;
            TryShoot();
        }

        ReloadWhenIdle(fireHeld);
        UpdateWeaponAnimation(Time.deltaTime);
        UpdateOverheatFeedback();
    }

    private void OnGUI()
    {
        if (!showCrosshair || !CanUseWeapon())
        {
            return;
        }

        if (crosshairPixel == null)
        {
            crosshairPixel = CreatePixelTexture();
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float halfThickness = crosshairThickness * 0.5f;
        Color previousColor = GUI.color;
        GUI.color = currentAimHit ? crosshairTargetColor : crosshairColor;

        GUI.DrawTexture(new Rect(centerX - halfThickness, centerY - crosshairGap - crosshairLength, crosshairThickness, crosshairLength), crosshairPixel);
        GUI.DrawTexture(new Rect(centerX - halfThickness, centerY + crosshairGap, crosshairThickness, crosshairLength), crosshairPixel);
        GUI.DrawTexture(new Rect(centerX - crosshairGap - crosshairLength, centerY - halfThickness, crosshairLength, crosshairThickness), crosshairPixel);
        GUI.DrawTexture(new Rect(centerX + crosshairGap, centerY - halfThickness, crosshairLength, crosshairThickness), crosshairPixel);
        GUI.DrawTexture(new Rect(centerX - halfThickness, centerY - halfThickness, crosshairThickness, crosshairThickness), crosshairPixel);

        GUI.color = previousColor;
    }

    private void OnDestroy()
    {
        if (crosshairPixel != null)
        {
            Destroy(crosshairPixel);
        }
    }

    private bool CanUseWeapon()
    {
        if (aimCamera == null || !aimCamera.enabled)
        {
            return false;
        }

        return playerController == null || playerController.IsControlEnabled;
    }

    private void SetWeaponVisible(bool visible)
    {
        if (weaponAimRoot != null && weaponAimRoot.gameObject.activeSelf != visible)
        {
            weaponAimRoot.gameObject.SetActive(visible);
        }
    }

    private void UpdateAimPoint()
    {
        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(aimRay, maxAimDistance, aimLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

        currentAimHit = false;
        currentAimPoint = aimRay.origin + aimRay.direction * maxAimDistance;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || ShouldIgnoreAimCollider(hitCollider))
            {
                continue;
            }

            currentAimHit = true;
            currentAimPoint = hits[i].point;
            return;
        }
    }

    private bool ShouldIgnoreAimCollider(Collider hitCollider)
    {
        if (playerRoot != null && hitCollider.transform.IsChildOf(playerRoot))
        {
            return true;
        }

        return weaponAimRoot != null && hitCollider.transform.IsChildOf(weaponAimRoot);
    }

    private void AimWeaponAtPoint(float deltaTime)
    {
        if (weaponAimRoot == null || muzzle == null)
        {
            return;
        }

        Vector3 muzzleToTarget = currentAimPoint - muzzle.position;
        if (muzzleToTarget.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(muzzleToTarget.normalized, aimCamera.transform.up);
        float lerp = 1f - Mathf.Exp(-weaponTrackSharpness * deltaTime);
        weaponAimRoot.rotation = Quaternion.Slerp(weaponAimRoot.rotation, targetRotation, lerp);
    }

    private void TryShoot()
    {
        if (currentAmmo < 1f || Time.time < lastShotTime + delayBetweenShots || muzzle == null)
        {
            return;
        }

        currentAmmo = Mathf.Max(0f, currentAmmo - 1f);
        lastShotTime = Time.time;
        SpawnProjectile();
        PlayShootFeedback();
    }

    private void SpawnProjectile()
    {
        Vector3 shotDirection = muzzle.forward;
        GameObject projectileObject;
        if (projectilePrefab != null)
        {
            projectileObject = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(shotDirection));
        }
        else
        {
            projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "BlasterProjectile";
            projectileObject.transform.position = muzzle.position;
            projectileObject.transform.rotation = Quaternion.LookRotation(shotDirection);
            projectileObject.transform.localScale = Vector3.one * Mathf.Max(0.04f, projectileRadius * 2f);
        }

        FirstPersonProjectile projectile = projectileObject.GetComponent<FirstPersonProjectile>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<FirstPersonProjectile>();
        }

        projectile.Initialize(
            shotDirection,
            playerRoot,
            projectileSpeed,
            projectileDamage,
            projectileRadius,
            projectileLifetime,
            projectileHitLayers,
            projectileMaterial);
    }

    private void PlayShootFeedback()
    {
        recoilDistance = Mathf.Min(recoilDistance + recoilKickDistance, recoilKickDistance * 2.5f);

        if (shootAudioSource != null && shootSfx != null)
        {
            shootAudioSource.PlayOneShot(shootSfx);
        }

        if (muzzleFlashVfx != null)
        {
            muzzleFlashVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlashVfx.Play(true);
        }
    }

    private void ReloadWhenIdle(bool fireHeld)
    {
        if (fireHeld || currentAmmo >= maxAmmo || Time.time < lastFireInputTime + ammoReloadDelay)
        {
            UpdateCoolingSound(false);
            return;
        }

        currentAmmo = Mathf.Min(maxAmmo, currentAmmo + ammoReloadRate * Time.deltaTime);
        UpdateCoolingSound(currentAmmo < maxAmmo);
    }

    private void UpdateCoolingSound(bool shouldPlay)
    {
        if (coolingAudioSource == null || coolingSfx == null)
        {
            return;
        }

        if (shouldPlay)
        {
            if (!coolingAudioSource.isPlaying)
            {
                coolingAudioSource.Play();
            }

            coolingAudioSource.volume = Mathf.Clamp01(1f - CurrentAmmoRatio);
        }
        else if (coolingAudioSource.isPlaying)
        {
            coolingAudioSource.Stop();
        }
    }

    private void UpdateWeaponAnimation(float deltaTime)
    {
        recoilDistance = Mathf.MoveTowards(recoilDistance, 0f, recoilReturnSharpness * recoilKickDistance * deltaTime);

        if (weaponVisualRoot != null)
        {
            Vector3 targetLocalPosition = weaponVisualDefaultLocalPosition + Vector3.back * recoilDistance;
            float lerp = 1f - Mathf.Exp(-recoilReturnSharpness * deltaTime);
            weaponVisualRoot.localPosition = Vector3.Lerp(weaponVisualRoot.localPosition, targetLocalPosition, lerp);
        }

        UpdateFuelCells();
    }

    private void UpdateFuelCells()
    {
        if (fuelCells == null || fuelCellDefaultLocalPositions == null)
        {
            return;
        }

        float ammoRatio = CurrentAmmoRatio;
        for (int i = 0; i < fuelCells.Length; i++)
        {
            Transform fuelCell = fuelCells[i];
            if (fuelCell == null)
            {
                continue;
            }

            float segmentStart = i / (float)fuelCells.Length;
            float segmentEnd = (i + 1) / (float)fuelCells.Length;
            float segmentRatio = Mathf.Clamp01(Mathf.InverseLerp(segmentStart, segmentEnd, ammoRatio));
            Vector3 used = fuelCellDefaultLocalPositions[i] + fuelCellUsedOffset;
            Vector3 unused = fuelCellDefaultLocalPositions[i] + fuelCellUnusedOffset;
            fuelCell.localPosition = Vector3.Lerp(used, unused, segmentRatio);
        }
    }

    private void UpdateOverheatFeedback()
    {
        float heat = 1f - CurrentAmmoRatio;
        Color overheatColor = Color.Lerp(overheatCoolColor, overheatHotColor, heat);

        if (overheatRenderers != null && overheatPropertyBlock != null)
        {
            overheatPropertyBlock.SetColor("_EmissionColor", overheatColor);
            for (int i = 0; i < overheatRenderers.Length; i++)
            {
                Renderer targetRenderer = overheatRenderers[i];
                if (targetRenderer == null)
                {
                    continue;
                }

                int materialIndex = overheatMaterialIndexes != null && i < overheatMaterialIndexes.Length
                    ? overheatMaterialIndexes[i]
                    : 0;
                targetRenderer.SetPropertyBlock(overheatPropertyBlock, materialIndex);
            }
        }

        if (overheatVfx != null)
        {
            ParticleSystem.EmissionModule emission = overheatVfx.emission;
            emission.rateOverTimeMultiplier = overheatVfxEmissionRateMax * heat;
        }
    }

    private void AutoWireMissingReferences()
    {
        if (aimCamera == null)
        {
            aimCamera = GetComponent<Camera>();
        }

        if (playerController == null)
        {
            playerController = GetComponentInParent<SimplePlayerController>();
        }

        if (playerRoot == null && playerController != null)
        {
            playerRoot = playerController.transform;
        }

        if (weaponAimRoot == null)
        {
            weaponAimRoot = FindDeepChild(transform, "Weapon_Blaster");
        }

        if (weaponVisualRoot == null && weaponAimRoot != null)
        {
            weaponVisualRoot = FindDeepChild(weaponAimRoot, "GunRoot");
        }

        if (muzzle == null && weaponAimRoot != null)
        {
            muzzle = FindDeepChild(weaponAimRoot, "GunMuzzle");
        }

        if ((fuelCells == null || fuelCells.Length == 0) && weaponAimRoot != null)
        {
            fuelCells = FindFuelCells(weaponAimRoot);
        }

        if (muzzleFlashVfx == null && muzzle != null)
        {
            muzzleFlashVfx = muzzle.GetComponentInChildren<ParticleSystem>(true);
        }

        if (overheatVfx == null && weaponAimRoot != null)
        {
            overheatVfx = FindParticleSystemContaining(weaponAimRoot, "Overheat");
        }

        if ((overheatRenderers == null || overheatRenderers.Length == 0) && weaponAimRoot != null)
        {
            CollectOverheatRenderers();
        }
    }

    private void CacheDefaultTransforms()
    {
        if (weaponAimRoot != null)
        {
            weaponAimRoot.localPosition = cameraLocalWeaponPosition;
            weaponAimRoot.localRotation = Quaternion.Euler(cameraLocalWeaponEuler);
        }

        if (weaponVisualRoot != null)
        {
            weaponVisualDefaultLocalPosition = weaponVisualRoot.localPosition;
        }

        if (fuelCells != null)
        {
            fuelCellDefaultLocalPositions = new Vector3[fuelCells.Length];
            for (int i = 0; i < fuelCells.Length; i++)
            {
                fuelCellDefaultLocalPositions[i] = fuelCells[i] != null ? fuelCells[i].localPosition : Vector3.zero;
            }
        }
    }

    private void ConfigureAudioSources()
    {
        shootAudioSource = GetComponent<AudioSource>();
        if (shootAudioSource == null)
        {
            shootAudioSource = gameObject.AddComponent<AudioSource>();
        }

        shootAudioSource.playOnAwake = false;
        shootAudioSource.spatialBlend = 0f;

        coolingAudioSource = gameObject.AddComponent<AudioSource>();
        coolingAudioSource.playOnAwake = false;
        coolingAudioSource.loop = true;
        coolingAudioSource.spatialBlend = 0f;
        coolingAudioSource.clip = coolingSfx;
    }

    private void ConfigureVfxDefaults()
    {
        if (overheatVfx != null)
        {
            ParticleSystem.EmissionModule emission = overheatVfx.emission;
            emission.rateOverTimeMultiplier = 0f;
        }
    }

    private void CollectOverheatRenderers()
    {
        Renderer[] renderers = weaponAimRoot.GetComponentsInChildren<Renderer>(true);
        overheatRenderers = renderers;
        overheatMaterialIndexes = new int[renderers.Length];
    }

    private static Transform[] FindFuelCells(Transform root)
    {
        Transform[] cells = new Transform[FuelCellNames.Length];
        for (int i = 0; i < FuelCellNames.Length; i++)
        {
            cells[i] = FindDeepChild(root, FuelCellNames[i]);
        }

        return cells;
    }

    private static ParticleSystem FindParticleSystemContaining(Transform root, string partialName)
    {
        ParticleSystem[] particleSystems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            if (particleSystems[i].name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return particleSystems[i];
            }
        }

        return particleSystems.Length > 0 ? particleSystems[0] : null;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeepChild(root.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static Texture2D CreatePixelTexture()
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        return texture;
    }
}
