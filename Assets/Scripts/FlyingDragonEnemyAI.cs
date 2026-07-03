using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animation))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class FlyingDragonEnemyAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float normalHorizontalSpeed = 1.6f;
    [SerializeField] private float closeDistance = 7f;
    [SerializeField] private float facePlayerBoundaryRange = 0.75f;
    [SerializeField] private float desiredHeightAbovePlayer = 6f;
    [SerializeField] private float heightShortDelay = 2f;
    [SerializeField] private float verticalRiseSpeed = 1.2f;
    [SerializeField] private float ascendingHorizontalSpeedMultiplier = 0.5f;
    [SerializeField] private float collisionRiseDuration = 2f;
    [SerializeField] private float collisionSkin = 0.05f;
    [SerializeField] private float attackInterval = 4f;
    [SerializeField] private float maxHealth = 60f;
    [SerializeField] private float deathDestroyDelay = 0.1f;
    [SerializeField] private float deathFallGravityMultiplier = 2f;
    [SerializeField] private float deathGroundCheckExtraDistance = 0.35f;
    [SerializeField] private float deathLandingVelocityThreshold = 0.45f;
    [SerializeField] private float deathLandingTimeout = 8f;
    [SerializeField] private string idleClipName = "dragon_idle";
    [SerializeField] private string attackClipName = "dragon_attack_repeatedly";
    [SerializeField] private string deathClipName = "dragon_die";
    [SerializeField] private bool heightEnough = true;

    private Animation animationComponent;
    private Rigidbody body;
    private CapsuleCollider capsuleCollider;
    private Renderer[] cachedRenderers;
    private float currentHealth;
    private float heightShortSince = -1f;
    private float collisionRiseUntil = -100f;
    private float nextAttackTime;
    private float attackEndTime;
    private bool isAttacking;
    private bool isDead;
    private bool isGrowingFromSpawn;
    private bool deathHasLanded;
    private float spawnGrowStartedAt;
    private float spawnGrowDuration;
    private Vector3 fullSpawnScale;
    private Vector3 initialSpawnScale;
    private readonly RaycastHit[] movementHits = new RaycastHit[12];
    private readonly RaycastHit[] deathGroundHits = new RaycastHit[8];

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void BeginSpawnGrow(float duration, float initialScale)
    {
        if (isDead)
        {
            return;
        }

        fullSpawnScale = transform.localScale;
        spawnGrowDuration = Mathf.Max(0f, duration);
        spawnGrowStartedAt = Time.time;

        if (spawnGrowDuration <= 0f)
        {
            isGrowingFromSpawn = false;
            transform.localScale = fullSpawnScale;
            return;
        }

        isGrowingFromSpawn = true;
        float clampedInitialScale = Mathf.Clamp(initialScale, 0.01f, 1f);
        initialSpawnScale = fullSpawnScale * clampedInitialScale;
        transform.localScale = initialSpawnScale;

        if (body != null && !body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    private void Awake()
    {
        animationComponent = GetComponent<Animation>();
        body = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        cachedRenderers = GetComponentsInChildren<Renderer>();
        currentHealth = maxHealth;

        ConfigurePhysicsBody();
        ConfigureColliderFromRenderers();

        idleClipName = ResolveClipName(idleClipName, "idle");
        attackClipName = ResolveClipName(attackClipName, "attack");
        deathClipName = ResolveClipName(deathClipName, "die");

        if (animationComponent[idleClipName] != null)
        {
            animationComponent[idleClipName].wrapMode = WrapMode.Loop;
        }

        if (animationComponent[attackClipName] != null)
        {
            animationComponent[attackClipName].wrapMode = WrapMode.Once;
        }

        if (animationComponent[deathClipName] != null)
        {
            animationComponent[deathClipName].wrapMode = WrapMode.Once;
        }
    }

    private void Start()
    {
        nextAttackTime = Time.time + attackInterval;
        PlayIdle();
    }

    private void Update()
    {
        if (isDead)
        {
            return;
        }

        if (target == null)
        {
            UpdateSpawnGrow();
            return;
        }

        if (UpdateSpawnGrow())
        {
            FaceTowardsPlayer();
            PlayIdle();
            return;
        }

        if (isAttacking)
        {
            FaceTowardsPlayer();
            if (Time.time >= attackEndTime)
            {
                isAttacking = false;
                nextAttackTime = Time.time + attackInterval;
                PlayIdle();
            }

            return;
        }

        if (Time.time >= nextAttackTime)
        {
            StartAttack();
            return;
        }

        PlayIdle();
    }

    private void FixedUpdate()
    {
        if (isDead)
        {
            ApplyDeathFallGravity();
            return;
        }

        if (target == null || isAttacking || isGrowingFromSpawn)
        {
            return;
        }

        MoveAroundPlayer(Time.fixedDeltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead)
        {
            RegisterDeathLanding(collision);
            return;
        }

        RegisterObstacleCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (isDead)
        {
            RegisterDeathLanding(collision);
            return;
        }

        RegisterObstacleCollision(collision);
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        isAttacking = false;
        isGrowingFromSpawn = false;
        deathHasLanded = false;

        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.detectCollisions = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.isKinematic = true;
            body.useGravity = false;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = true;
            capsuleCollider.isTrigger = false;
        }

        if (animationComponent != null)
        {
            animationComponent.enabled = false;
        }

        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        StartDeathFall();
        yield return WaitForDeathLanding();
        StopDeathFall();

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

        PlayDeathAnimation();
        Destroy(gameObject, GetClipLength(deathClipName, 1f) + deathDestroyDelay);
    }

    private void StartDeathFall()
    {
        if (body == null)
        {
            return;
        }

        if (!body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.detectCollisions = true;
        body.isKinematic = false;
        body.useGravity = true;
        body.WakeUp();
    }

    private void ApplyDeathFallGravity()
    {
        if (body == null || body.isKinematic || !body.useGravity)
        {
            return;
        }

        float extraGravityMultiplier = Mathf.Max(0f, deathFallGravityMultiplier - 1f);
        if (extraGravityMultiplier <= 0f)
        {
            return;
        }

        body.AddForce(Physics.gravity * extraGravityMultiplier, ForceMode.Acceleration);
    }

    private IEnumerator WaitForDeathLanding()
    {
        if (body == null || capsuleCollider == null)
        {
            yield break;
        }

        float startedAt = Time.time;
        yield return null;

        while (Time.time - startedAt < deathLandingTimeout)
        {
            if (HasDeathLanded())
            {
                yield break;
            }

            yield return null;
        }
    }

    private bool HasDeathLanded()
    {
        if (body == null || capsuleCollider == null || !capsuleCollider.enabled)
        {
            return true;
        }

        if (deathHasLanded)
        {
            return body.velocity.y >= -deathLandingVelocityThreshold;
        }

        if (body.velocity.y < -deathLandingVelocityThreshold)
        {
            return false;
        }

        return IsGroundedForDeath();
    }

    private bool IsGroundedForDeath()
    {
        if (capsuleCollider == null || !capsuleCollider.enabled)
        {
            return true;
        }

        Bounds bounds = capsuleCollider.bounds;
        float checkDistance = Mathf.Max(0.1f, bounds.extents.y + deathGroundCheckExtraDistance);
        int hitCount = Physics.RaycastNonAlloc(
            bounds.center,
            Vector3.down,
            deathGroundHits,
            checkDistance,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = deathGroundHits[i].collider;
            if (hitCollider != null && !ShouldIgnoreCollider(hitCollider))
            {
                return true;
            }
        }

        return false;
    }

    private void StopDeathFall()
    {
        if (body == null)
        {
            return;
        }

        if (!body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        body.isKinematic = true;
        body.useGravity = false;
    }

    private void ConfigurePhysicsBody()
    {
        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = true;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void ConfigureColliderFromRenderers()
    {
        Renderer[] renderers = cachedRenderers;
        if (renderers.Length == 0)
        {
            capsuleCollider.center = new Vector3(0f, 0.8f, 0f);
            capsuleCollider.height = 1.6f;
            capsuleCollider.radius = 0.55f;
            capsuleCollider.direction = 1;
            capsuleCollider.isTrigger = false;
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        capsuleCollider.center = localCenter;
        capsuleCollider.height = Mathf.Max(1f, bounds.size.y);
        capsuleCollider.radius = Mathf.Max(0.35f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.35f);
        capsuleCollider.direction = 1;
        capsuleCollider.isTrigger = false;
    }

    private void MoveAroundPlayer(float deltaTime)
    {
        Vector3 toPlayer = target.position - body.position;
        toPlayer.y = 0f;

        Vector3 horizontalDirection = Vector3.zero;
        float horizontalDistance = toPlayer.magnitude;
        if (horizontalDistance > 0.01f)
        {
            horizontalDirection = horizontalDistance < closeDistance ? -toPlayer.normalized : toPlayer.normalized;
        }

        UpdateHeightState();

        bool shouldRiseForHeight = !heightEnough && Time.time - heightShortSince >= heightShortDelay;
        bool shouldRiseForCollision = Time.time < collisionRiseUntil;
        bool shouldRise = shouldRiseForHeight || shouldRiseForCollision;
        float horizontalSpeed = shouldRise ? normalHorizontalSpeed * ascendingHorizontalSpeedMultiplier : normalHorizontalSpeed;
        Vector3 horizontalMovement = horizontalDirection * horizontalSpeed * deltaTime;
        Vector3 moveStartPosition = body.position;
        horizontalMovement = ResolveMovement(moveStartPosition, horizontalMovement, out bool horizontalBlocked);
        if (horizontalBlocked)
        {
            StartCollisionRise();
        }

        float verticalMovement = 0f;
        if (shouldRise)
        {
            float targetHeight = target.position.y + desiredHeightAbovePlayer;
            if (shouldRiseForHeight && body.position.y < targetHeight)
            {
                float newY = Mathf.Min(body.position.y + verticalRiseSpeed * deltaTime, targetHeight);
                verticalMovement = newY - body.position.y;
            }
            else if (shouldRiseForCollision)
            {
                verticalMovement = verticalRiseSpeed * deltaTime;
            }
        }

        if (horizontalBlocked)
        {
            verticalMovement = Mathf.Max(verticalMovement, verticalRiseSpeed * deltaTime);
        }

        Vector3 verticalOffset = ResolveMovement(moveStartPosition + horizontalMovement, Vector3.up * verticalMovement, out bool verticalBlocked);
        if (verticalBlocked)
        {
            StartCollisionRise();
        }

        body.MovePosition(body.position + horizontalMovement + verticalOffset);

        Vector3 facingDirection = horizontalDirection;
        if (horizontalDistance > 0.01f && Mathf.Abs(horizontalDistance - closeDistance) <= facePlayerBoundaryRange)
        {
            facingDirection = toPlayer.normalized;
        }

        if (facingDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, 8f * deltaTime));
        }
    }

    private void UpdateHeightState()
    {
        float heightDifference = transform.position.y - target.position.y;
        if (heightDifference < desiredHeightAbovePlayer)
        {
            if (heightEnough)
            {
                heightEnough = false;
                heightShortSince = Time.time;
            }
        }
        else
        {
            heightEnough = true;
            heightShortSince = -1f;
        }
    }

    private void StartAttack()
    {
        isAttacking = true;
        FaceTowardsPlayer();

        AnimationState attackState = animationComponent[attackClipName];
        if (attackState != null)
        {
            attackState.time = 0f;
            animationComponent.CrossFade(attackClipName, 0.1f);
            attackEndTime = Time.time + Mathf.Max(attackState.length, 0.3f);
        }
        else
        {
            attackEndTime = Time.time + 0.8f;
        }
    }

    private void RegisterObstacleCollision(Collision collision)
    {
        if (collision == null || collision.collider == null || collision.collider.transform.IsChildOf(transform))
        {
            return;
        }

        FlyingDragonEnemyAI otherDragon = collision.collider.GetComponentInParent<FlyingDragonEnemyAI>();
        if (otherDragon != null && otherDragon != this)
        {
            return;
        }

        StartCollisionRise();
    }

    private void RegisterDeathLanding(Collision collision)
    {
        if (collision == null || collision.collider == null || ShouldIgnoreCollider(collision.collider))
        {
            return;
        }

        if (collision.contactCount == 0)
        {
            deathHasLanded = true;
            return;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            if (collision.GetContact(i).normal.y > 0.2f)
            {
                deathHasLanded = true;
                return;
            }
        }
    }

    private void StartCollisionRise()
    {
        collisionRiseUntil = Mathf.Max(collisionRiseUntil, Time.time + collisionRiseDuration);
        heightEnough = false;
        heightShortSince = Time.time - heightShortDelay;
    }

    private bool UpdateSpawnGrow()
    {
        if (!isGrowingFromSpawn)
        {
            return false;
        }

        if (spawnGrowDuration <= 0f)
        {
            isGrowingFromSpawn = false;
            transform.localScale = fullSpawnScale;
            return false;
        }

        float progress = Mathf.Clamp01((Time.time - spawnGrowStartedAt) / spawnGrowDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        transform.localScale = Vector3.Lerp(initialSpawnScale, fullSpawnScale, easedProgress);

        if (progress >= 1f)
        {
            isGrowingFromSpawn = false;
            transform.localScale = fullSpawnScale;
            return false;
        }

        return true;
    }

    private Vector3 ResolveMovement(Vector3 startPosition, Vector3 requestedMovement, out bool blocked)
    {
        blocked = false;
        float distance = requestedMovement.magnitude;
        if (distance <= 0.0001f)
        {
            return Vector3.zero;
        }

        Vector3 direction = requestedMovement / distance;
        if (!FindBlockingHit(startPosition, direction, distance, out RaycastHit hit))
        {
            return requestedMovement;
        }

        blocked = true;
        float allowedDistance = Mathf.Max(0f, hit.distance - collisionSkin);
        Vector3 allowedMovement = direction * Mathf.Min(allowedDistance, distance);
        Vector3 remainingMovement = requestedMovement - allowedMovement;
        Vector3 slideMovement = Vector3.ProjectOnPlane(remainingMovement, hit.normal);

        if (slideMovement.sqrMagnitude <= 0.0001f)
        {
            return allowedMovement;
        }

        slideMovement = Vector3.ClampMagnitude(slideMovement, remainingMovement.magnitude);
        if (FindBlockingHit(startPosition + allowedMovement, slideMovement.normalized, slideMovement.magnitude, out _))
        {
            return allowedMovement;
        }

        return allowedMovement + slideMovement;
    }

    private bool FindBlockingHit(Vector3 startPosition, Vector3 direction, float distance, out RaycastHit closestHit)
    {
        closestHit = default;
        if (!TryGetCapsuleWorldAtPosition(startPosition, out Vector3 pointA, out Vector3 pointB, out float radius))
        {
            return false;
        }

        int hitCount = Physics.CapsuleCastNonAlloc(
            pointA,
            pointB,
            radius,
            direction,
            movementHits,
            distance + collisionSkin,
            ~0,
            QueryTriggerInteraction.Ignore);

        bool foundHit = false;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = movementHits[i];
            if (hit.collider == null || ShouldIgnoreCollider(hit.collider))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                closestHit = hit;
                foundHit = true;
            }
        }

        return foundHit;
    }

    private bool TryGetCapsuleWorldAtPosition(Vector3 position, out Vector3 pointA, out Vector3 pointB, out float radius)
    {
        pointA = position;
        pointB = position;
        radius = 0f;

        if (capsuleCollider == null || !capsuleCollider.enabled)
        {
            return false;
        }

        Vector3 scale = transform.lossyScale;
        Vector3 scaledCenter = Vector3.Scale(capsuleCollider.center, scale);
        Vector3 center = position + transform.rotation * scaledCenter;
        Vector3 axis = GetCapsuleAxis();
        float heightScale = GetCapsuleHeightScale(scale);
        float radiusScale = GetCapsuleRadiusScale(scale);

        radius = Mathf.Max(0.01f, capsuleCollider.radius * radiusScale);
        float height = Mathf.Max(radius * 2f, capsuleCollider.height * heightScale);
        float halfLine = Mathf.Max(0f, (height * 0.5f) - radius);
        pointA = center + axis * halfLine;
        pointB = center - axis * halfLine;
        return true;
    }

    private Vector3 GetCapsuleAxis()
    {
        if (capsuleCollider.direction == 0)
        {
            return transform.right;
        }

        if (capsuleCollider.direction == 2)
        {
            return transform.forward;
        }

        return transform.up;
    }

    private float GetCapsuleHeightScale(Vector3 scale)
    {
        if (capsuleCollider.direction == 0)
        {
            return Mathf.Abs(scale.x);
        }

        if (capsuleCollider.direction == 2)
        {
            return Mathf.Abs(scale.z);
        }

        return Mathf.Abs(scale.y);
    }

    private float GetCapsuleRadiusScale(Vector3 scale)
    {
        if (capsuleCollider.direction == 0)
        {
            return Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        }

        if (capsuleCollider.direction == 2)
        {
            return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        }

        return Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
    }

    private bool ShouldIgnoreCollider(Collider other)
    {
        if (other == null || other == capsuleCollider || other.transform.IsChildOf(transform))
        {
            return true;
        }

        if (target != null && other.transform.IsChildOf(target))
        {
            return true;
        }

        FlyingDragonEnemyAI otherDragon = other.GetComponentInParent<FlyingDragonEnemyAI>();
        return otherDragon != null && otherDragon != this;
    }

    private void FaceTowardsPlayer()
    {
        Vector3 toPlayer = target.position - body.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        body.MoveRotation(Quaternion.Slerp(
            body.rotation,
            Quaternion.LookRotation(toPlayer.normalized, Vector3.up),
            10f * Time.deltaTime));
    }

    private void PlayIdle()
    {
        if (animationComponent[idleClipName] == null || animationComponent.IsPlaying(attackClipName))
        {
            return;
        }

        if (!animationComponent.IsPlaying(idleClipName))
        {
            animationComponent.CrossFade(idleClipName, 0.15f);
        }
    }

    private void PlayDeathAnimation()
    {
        if (animationComponent == null || animationComponent[deathClipName] == null)
        {
            return;
        }

        animationComponent.enabled = true;
        animationComponent.Stop();
        animationComponent[deathClipName].time = 0f;
        animationComponent.CrossFade(deathClipName, 0.08f);
    }

    private float GetClipLength(string clipName, float fallbackLength)
    {
        if (animationComponent == null || animationComponent[clipName] == null)
        {
            return fallbackLength;
        }

        return Mathf.Max(animationComponent[clipName].length, fallbackLength);
    }

    private string ResolveClipName(string preferredName, string fallbackContains)
    {
        if (animationComponent[preferredName] != null)
        {
            return preferredName;
        }

        foreach (AnimationState state in animationComponent)
        {
            if (state.name.ToLowerInvariant().Contains(fallbackContains))
            {
                return state.name;
            }
        }

        return preferredName;
    }
}
