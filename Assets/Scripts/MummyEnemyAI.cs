using UnityEngine;

public class MummyEnemyAI : MonoBehaviour
{
    private enum MummyState
    {
        Ghost,
        Normal
    }

    [SerializeField] private Transform target;
    [SerializeField] private SimplePlayerController playerController;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float ghostRiseSpeed = 0.8f;
    [SerializeField] private float attackDistance = 1f;
    [SerializeField] private float movementDirectionRetargetDelay = 0.5f;
    [SerializeField] private float fallbackPlayerHalfSpeed = 3f;
    [SerializeField] private float fallbackPlayerJumpVelocity = 6.5f;
    [SerializeField] private float playerGravityRatio = 0.5f;
    [SerializeField] private float normalJumpHeightMultiplier = 1.5f;
    [SerializeField] private float superJumpHeightMultiplier = 3f;
    [SerializeField] private float regularJumpMinInterval = 4f;
    [SerializeField] private float regularJumpMaxInterval = 5f;
    [SerializeField] private float blockingCollisionDuration = 1f;
    [SerializeField] private float slowMovementSuperJumpWindow = 3f;
    [SerializeField] private float slowMovementSuperJumpMaxDistance = 3f;
    [SerializeField] private float dizzyDuration = 1.5f;
    [SerializeField] private float minGroundNormalY = 0.45f;
    [SerializeField] private float capsuleHeight = 1.5f;
    [SerializeField] private float capsuleRadius = 0.45f;
    [SerializeField] private Vector3 capsuleCenter = new Vector3(0f, 0.725f, 0f);
    [SerializeField] private float maxGhostDuration = 8f;
    [SerializeField] private float maxHealth = 80f;
    [SerializeField] private float attackDamagePerSecond = 10f;
    [SerializeField] private float deathDestroyDelay = 0.1f;
    [SerializeField] private string flyClipName = "mummy_fly_up_loop";
    [SerializeField] private string walkClipName = "mummy_walk";
    [SerializeField] private string attackClipName = "mummy_attack_repeatedly";
    [SerializeField] private string jumpClipName = "mummy_jump";
    [SerializeField] private string dizzyClipName = "mummy_dizzy";
    [SerializeField] private string deathClipName = "mummy_die";
    [SerializeField] private MummyState state = MummyState.Ghost;

    private Animation animationComponent;
    private CapsuleCollider capsuleCollider;
    private Rigidbody body;
    private Collider targetCollider;
    private PlayerHealth playerHealth;
    private PhysicMaterial runtimeFrictionlessMaterial;
    private float currentHealth;
    private bool hasTouchedBlockingSpace;
    private float ghostStartedAt;
    private Quaternion ghostVisualRotation;
    private float nextRegularJumpTime = -1f;
    private float lastGroundedTime = -100f;
    private float blockingCollisionStartedAt = -1f;
    private float lastBlockingCollisionTime = -100f;
    private float jumpAnimationUntil = -100f;
    private float movementPausedUntil = -100f;
    private float superJumpStartedAt = -100f;
    private bool pendingDizzyAfterSuperJump;
    private bool superJumpLeftGround;
    private bool isAttacking;
    private bool isDead;
    private Vector3 movementWindowLastPosition;
    private float movementWindowStartedAt = -1f;
    private float movementWindowDistance;
    private Vector3 delayedMoveDirection;
    private float nextMoveDirectionRetargetTime = -1f;

    public void Initialize(Transform newTarget, SimplePlayerController newPlayerController, Transform newVisualRoot)
    {
        target = newTarget;
        playerController = newPlayerController;
        visualRoot = newVisualRoot;
        targetCollider = target != null ? target.GetComponent<Collider>() : null;
        playerHealth = target != null ? PlayerHealth.GetOrCreate(target.gameObject) : null;
        animationComponent = visualRoot != null ? visualRoot.GetComponent<Animation>() : GetComponentInChildren<Animation>();
        ConfigureAnimationClips();
        EnterGhostState();
    }

    private void Awake()
    {
        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null)
        {
            capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
        }

        body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        currentHealth = maxHealth;

        if (visualRoot == null && transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
        }

        if (animationComponent == null)
        {
            animationComponent = visualRoot != null ? visualRoot.GetComponent<Animation>() : GetComponentInChildren<Animation>();
        }

        if (targetCollider == null && target != null)
        {
            targetCollider = target.GetComponent<Collider>();
        }

        ghostVisualRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        ConfigureSharedPhysicsShape();
        ConfigureGhostPhysics();
        ConfigureAnimationClips();
    }

    private void Start()
    {
        EnterGhostState();
    }

    private void FixedUpdate()
    {
        if (isDead || target == null)
        {
            return;
        }

        if (state == MummyState.Ghost)
        {
            UpdateGhostState();
            return;
        }

        ApplyMummyGravity();
        UpdateNormalState();
        ClearExpiredBlockingCollision();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead)
        {
            return;
        }

        RegisterCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (isDead)
        {
            return;
        }

        RegisterCollision(collision);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (isDead)
        {
            return;
        }

        if (!IsSameMummy(collision.collider))
        {
            lastBlockingCollisionTime = -100f;
            blockingCollisionStartedAt = -1f;
        }
    }

    private void ConfigureSharedPhysicsShape()
    {
        capsuleCollider.height = capsuleHeight;
        capsuleCollider.radius = capsuleRadius;
        capsuleCollider.center = capsuleCenter;
        capsuleCollider.direction = 1;
    }

    private void ConfigureGhostPhysics()
    {
        if (!body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        capsuleCollider.isTrigger = true;
    }

    private void ConfigureNormalPhysics()
    {
        Rigidbody playerBody = target != null ? target.GetComponent<Rigidbody>() : null;
        CapsuleCollider playerCollider = target != null ? target.GetComponent<CapsuleCollider>() : null;

        body.isKinematic = false;
        body.useGravity = false;
        body.mass = playerBody != null ? playerBody.mass : (playerController != null ? playerController.PlayerMass : 160f);
        body.drag = playerBody != null ? playerBody.drag : 0f;
        body.angularDrag = playerBody != null ? playerBody.angularDrag : 0f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        capsuleCollider.isTrigger = false;
        capsuleCollider.sharedMaterial = playerCollider != null && playerCollider.sharedMaterial != null
            ? playerCollider.sharedMaterial
            : GetRuntimeFrictionlessMaterial();
    }

    private void ConfigureAnimationClips()
    {
        if (animationComponent == null)
        {
            return;
        }

        flyClipName = ResolveClipName(flyClipName, "fly");
        walkClipName = ResolveClipName(walkClipName, "walk");
        attackClipName = ResolveClipName(attackClipName, "attack");
        jumpClipName = ResolveClipName(jumpClipName, "jump");
        dizzyClipName = ResolveClipName(dizzyClipName, "dizzy");
        deathClipName = ResolveClipName(deathClipName, "die");

        SetClipWrapMode(flyClipName, WrapMode.Loop);
        SetClipWrapMode(walkClipName, WrapMode.Loop);
        SetClipWrapMode(attackClipName, WrapMode.Loop);
        SetClipWrapMode(jumpClipName, WrapMode.Once);
        SetClipWrapMode(dizzyClipName, WrapMode.Loop);
        SetClipWrapMode(deathClipName, WrapMode.Once);
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
        pendingDizzyAfterSuperJump = false;
        superJumpLeftGround = false;
        movementPausedUntil = -100f;
        jumpAnimationUntil = -100f;

        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            body.useGravity = false;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.enabled = false;
        }

        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        PlayDeathAnimation();
        Destroy(gameObject, GetClipLength(deathClipName, 1f) + deathDestroyDelay);
    }

    private void EnterGhostState()
    {
        state = MummyState.Ghost;
        ghostStartedAt = Time.time;
        hasTouchedBlockingSpace = false;
        pendingDizzyAfterSuperJump = false;
        superJumpLeftGround = false;
        isAttacking = false;
        ResetMovementWindow();
        ResetDelayedMoveDirection();
        ConfigureGhostPhysics();

        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = ghostVisualRotation;
        }

        PlayLoop(flyClipName);
    }

    private void EnterNormalState()
    {
        state = MummyState.Normal;
        ConfigureNormalPhysics();
        ScheduleRegularJump();
        isAttacking = false;
        ResetMovementWindow();
        ResetDelayedMoveDirection();

        if (visualRoot != null)
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
        }

        FacePlayer();
        PlayLoop(walkClipName);
    }

    private void UpdateGhostState()
    {
        bool overlapsBlockingSpace = HasBlockingOverlap();
        hasTouchedBlockingSpace |= overlapsBlockingSpace;

        if (!overlapsBlockingSpace && (hasTouchedBlockingSpace || Time.time - ghostStartedAt >= maxGhostDuration))
        {
            EnterNormalState();
            return;
        }

        body.MovePosition(body.position + Vector3.up * ghostRiseSpeed * Time.fixedDeltaTime);
        PlayLoop(flyClipName);
    }

    private void UpdateNormalState()
    {
        Vector3 toPlayer3D = target.position - body.position;
        bool shouldAttack = IsInAttackRange(toPlayer3D);
        isAttacking = shouldAttack;

        if (shouldAttack)
        {
            ResetMovementWindow();
            ResetDelayedMoveDirection();
            FacePlayer();
            StopHorizontalMovement();
            jumpAnimationUntil = -100f;
            PlayLoop(attackClipName);
            DealAttackDamage(Time.fixedDeltaTime);
            return;
        }

        UpdateSuperJumpLanding();

        if (Time.time < movementPausedUntil)
        {
            isAttacking = false;
            ResetMovementWindow();
            ResetDelayedMoveDirection();
            StopHorizontalMovement();
            PlayLoop(dizzyClipName);
            return;
        }

        toPlayer3D = target.position - body.position;
        Vector3 toPlayer = toPlayer3D;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (TrySlowMovementSuperJump())
        {
            return;
        }

        TryRegularJump();

        if (distance > 0.01f)
        {
            Vector3 direction = GetDelayedMoveDirection(toPlayer, distance);
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, 10f * Time.fixedDeltaTime));

            float moveSpeed = playerController != null ? playerController.MoveSpeed * 0.5f : fallbackPlayerHalfSpeed;
            Vector3 velocity = body.velocity;
            velocity.x = direction.x * moveSpeed;
            velocity.z = direction.z * moveSpeed;
            body.velocity = velocity;
        }
        else
        {
            ResetDelayedMoveDirection();
            StopHorizontalMovement();
        }

        if (Time.time >= jumpAnimationUntil)
        {
            PlayLoop(walkClipName);
        }
    }

    private void ApplyMummyGravity()
    {
        body.AddForce(Physics.gravity * GetMummyGravityScale(), ForceMode.Acceleration);
    }

    private float GetMummyGravityScale()
    {
        float playerGravityScale = playerController != null ? Mathf.Max(1f, playerController.FallGravityMultiplier) : 1f;
        return Mathf.Max(0.01f, playerGravityScale * playerGravityRatio);
    }

    private void TryRegularJump()
    {
        if (isAttacking || Time.time < nextRegularJumpTime || !IsGrounded() || pendingDizzyAfterSuperJump)
        {
            return;
        }

        StartJump(normalJumpHeightMultiplier, false);
        ScheduleRegularJump();
    }

    private void StartJump(float playerHeightMultiplier, bool superJump)
    {
        Vector3 velocity = body.velocity;
        velocity.y = CalculateJumpVelocity(playerHeightMultiplier);
        body.velocity = velocity;

        lastGroundedTime = -100f;
        jumpAnimationUntil = Time.time + GetClipLength(jumpClipName, 0.45f);
        PlayOnce(jumpClipName);

        if (superJump)
        {
            pendingDizzyAfterSuperJump = true;
            superJumpLeftGround = false;
            superJumpStartedAt = Time.time;
            blockingCollisionStartedAt = -1f;
            lastBlockingCollisionTime = -100f;
        }

        ResetMovementWindow();
        ResetDelayedMoveDirection();
    }

    private float CalculateJumpVelocity(float playerHeightMultiplier)
    {
        float playerJumpVelocity = playerController != null ? playerController.JumpVelocity : fallbackPlayerJumpVelocity;
        return playerJumpVelocity * Mathf.Sqrt(Mathf.Max(0.01f, playerHeightMultiplier * GetMummyGravityScale()));
    }

    private void UpdateSuperJumpLanding()
    {
        if (!pendingDizzyAfterSuperJump)
        {
            return;
        }

        if (!IsGrounded() && Time.time - superJumpStartedAt > 0.2f)
        {
            superJumpLeftGround = true;
        }

        if (superJumpLeftGround && IsGrounded() && Time.time - superJumpStartedAt > 0.5f)
        {
            StartDizzyPause();
        }
    }

    private void StartDizzyPause()
    {
        pendingDizzyAfterSuperJump = false;
        superJumpLeftGround = false;
        movementPausedUntil = Time.time + dizzyDuration;
        jumpAnimationUntil = -100f;
        isAttacking = false;
        StopHorizontalMovement();
        ScheduleRegularJump();
        ResetMovementWindow();
        ResetDelayedMoveDirection();
        PlayLoop(dizzyClipName);
    }

    private void ScheduleRegularJump()
    {
        nextRegularJumpTime = Time.time + Random.Range(regularJumpMinInterval, regularJumpMaxInterval);
    }

    private void RegisterCollision(Collision collision)
    {
        if (state != MummyState.Normal || collision == null || IsSameMummy(collision.collider))
        {
            return;
        }

        bool hasGroundContact = false;
        bool hasBlockingContact = false;
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minGroundNormalY)
            {
                hasGroundContact = true;
            }
            else
            {
                hasBlockingContact = true;
            }
        }

        if (hasGroundContact)
        {
            lastGroundedTime = Time.time;
        }

        if (!hasBlockingContact || !CanTriggerSuperJump())
        {
            return;
        }

        lastBlockingCollisionTime = Time.time;
        if (blockingCollisionStartedAt < 0f)
        {
            blockingCollisionStartedAt = Time.time;
        }

        if (Time.time - blockingCollisionStartedAt >= blockingCollisionDuration)
        {
            StartJump(superJumpHeightMultiplier, true);
        }
    }

    private bool CanTriggerSuperJump()
    {
        if (isAttacking || pendingDizzyAfterSuperJump || Time.time < movementPausedUntil || !IsGrounded())
        {
            return false;
        }

        if (target == null)
        {
            return false;
        }

        return Vector3.Distance(target.position, body.position) >= attackDistance;
    }

    private bool IsInAttackRange(Vector3 targetOffset)
    {
        float range = Mathf.Max(0.01f, attackDistance);
        if (targetOffset.magnitude <= range)
        {
            return true;
        }

        if (targetCollider == null && target != null)
        {
            targetCollider = target.GetComponent<Collider>();
        }

        if (targetCollider == null)
        {
            return false;
        }

        Vector3 closestPlayerPoint = targetCollider.ClosestPoint(body.position);
        return Vector3.Distance(body.position, closestPlayerPoint) <= range;
    }

    private bool TrySlowMovementSuperJump()
    {
        if (!CanTriggerSuperJump())
        {
            ResetMovementWindow();
            return false;
        }

        Vector3 currentPosition = body.position;
        if (movementWindowStartedAt < 0f)
        {
            ResetMovementWindow();
            return false;
        }

        Vector3 movementDelta = currentPosition - movementWindowLastPosition;
        movementDelta.y = 0f;
        movementWindowDistance += movementDelta.magnitude;
        movementWindowLastPosition = currentPosition;

        if (Time.time - movementWindowStartedAt < slowMovementSuperJumpWindow)
        {
            return false;
        }

        bool shouldSuperJump = movementWindowDistance <= slowMovementSuperJumpMaxDistance;
        ResetMovementWindow();

        if (!shouldSuperJump)
        {
            return false;
        }

        StartJump(superJumpHeightMultiplier, true);
        return true;
    }

    private void ResetMovementWindow()
    {
        movementWindowStartedAt = Time.time;
        movementWindowLastPosition = body != null ? body.position : transform.position;
        movementWindowDistance = 0f;
    }

    private void ClearExpiredBlockingCollision()
    {
        if (Time.time - lastBlockingCollisionTime > 0.25f)
        {
            blockingCollisionStartedAt = -1f;
        }
    }

    private bool HasBlockingOverlap()
    {
        Vector3 center = transform.TransformPoint(capsuleCollider.center);
        Vector3 up = transform.up;
        float halfHeight = Mathf.Max(0f, capsuleCollider.height * 0.5f - capsuleCollider.radius);
        Vector3 pointA = center + up * halfHeight;
        Vector3 pointB = center - up * halfHeight;
        Collider[] overlaps = Physics.OverlapCapsule(pointA, pointB, capsuleCollider.radius, ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider other = overlaps[i];
            if (other == null || other == capsuleCollider || other.transform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsSameMummy(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        MummyEnemyAI otherMummy = other.GetComponentInParent<MummyEnemyAI>();
        return otherMummy != null && otherMummy != this;
    }

    private bool IsGrounded()
    {
        return Time.time - lastGroundedTime <= 0.15f;
    }

    private Vector3 GetDelayedMoveDirection(Vector3 currentFlatOffset, float distance)
    {
        if (distance <= 0.01f)
        {
            return Vector3.zero;
        }

        Vector3 currentDirection = currentFlatOffset / distance;
        if (delayedMoveDirection.sqrMagnitude <= 0.001f || Time.time >= nextMoveDirectionRetargetTime)
        {
            delayedMoveDirection = currentDirection;
            nextMoveDirectionRetargetTime = Time.time + Mathf.Max(0f, movementDirectionRetargetDelay);
        }

        return delayedMoveDirection;
    }

    private void ResetDelayedMoveDirection()
    {
        delayedMoveDirection = Vector3.zero;
        nextMoveDirectionRetargetTime = -1f;
    }

    private void StopHorizontalMovement()
    {
        Vector3 velocity = body.velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        body.velocity = velocity;
    }

    private void DealAttackDamage(float deltaTime)
    {
        if (attackDamagePerSecond <= 0f || target == null)
        {
            return;
        }

        if (playerHealth == null)
        {
            playerHealth = PlayerHealth.GetOrCreate(target.gameObject);
        }

        if (playerHealth != null)
        {
            playerHealth.ApplyDamage(attackDamagePerSecond * deltaTime);
        }
    }

    private void FacePlayer()
    {
        Vector3 toPlayer = target.position - body.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        body.MoveRotation(Quaternion.LookRotation(toPlayer.normalized, Vector3.up));
    }

    private void PlayLoop(string clipName)
    {
        if (animationComponent == null || animationComponent[clipName] == null)
        {
            return;
        }

        if (!animationComponent.IsPlaying(clipName))
        {
            animationComponent.CrossFade(clipName, 0.1f);
        }
    }

    private void PlayOnce(string clipName)
    {
        if (animationComponent == null || animationComponent[clipName] == null)
        {
            return;
        }

        animationComponent[clipName].time = 0f;
        animationComponent.CrossFade(clipName, 0.08f);
    }

    private void PlayDeathAnimation()
    {
        if (animationComponent == null || animationComponent[deathClipName] == null)
        {
            return;
        }

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

    private void SetClipWrapMode(string clipName, WrapMode wrapMode)
    {
        if (animationComponent != null && animationComponent[clipName] != null)
        {
            animationComponent[clipName].wrapMode = wrapMode;
        }
    }

    private PhysicMaterial GetRuntimeFrictionlessMaterial()
    {
        if (runtimeFrictionlessMaterial == null)
        {
            runtimeFrictionlessMaterial = new PhysicMaterial("Mummy_Frictionless_Runtime");
            runtimeFrictionlessMaterial.staticFriction = 0f;
            runtimeFrictionlessMaterial.dynamicFriction = 0f;
            runtimeFrictionlessMaterial.bounciness = 0f;
            runtimeFrictionlessMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
            runtimeFrictionlessMaterial.bounceCombine = PhysicMaterialCombine.Minimum;
        }

        return runtimeFrictionlessMaterial;
    }

    private string ResolveClipName(string preferredName, string fallbackContains)
    {
        if (animationComponent == null)
        {
            return preferredName;
        }

        if (animationComponent[preferredName] != null)
        {
            return preferredName;
        }

        foreach (AnimationState stateItem in animationComponent)
        {
            if (stateItem.name.ToLowerInvariant().Contains(fallbackContains))
            {
                return stateItem.name;
            }
        }

        return preferredName;
    }
}
