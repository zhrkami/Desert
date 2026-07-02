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
    [SerializeField] private float attackInterval = 4f;
    [SerializeField] private string idleClipName = "dragon_idle";
    [SerializeField] private string attackClipName = "dragon_attack_repeatedly";
    [SerializeField] private bool heightEnough = true;

    private Animation animationComponent;
    private Rigidbody body;
    private CapsuleCollider capsuleCollider;
    private float heightShortSince = -1f;
    private float collisionRiseUntil = -100f;
    private float nextAttackTime;
    private float attackEndTime;
    private bool isAttacking;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        animationComponent = GetComponent<Animation>();
        body = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();

        ConfigurePhysicsBody();
        ConfigureColliderFromRenderers();

        idleClipName = ResolveClipName(idleClipName, "idle");
        attackClipName = ResolveClipName(attackClipName, "attack");

        if (animationComponent[idleClipName] != null)
        {
            animationComponent[idleClipName].wrapMode = WrapMode.Loop;
        }

        if (animationComponent[attackClipName] != null)
        {
            animationComponent[attackClipName].wrapMode = WrapMode.Once;
        }
    }

    private void Start()
    {
        nextAttackTime = Time.time + attackInterval;
        PlayIdle();
    }

    private void Update()
    {
        if (target == null)
        {
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
        if (target == null || isAttacking)
        {
            return;
        }

        MoveAroundPlayer(Time.fixedDeltaTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        RegisterObstacleCollision(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        RegisterObstacleCollision(collision);
    }

    private void ConfigurePhysicsBody()
    {
        body.isKinematic = true;
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void ConfigureColliderFromRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
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
        Vector3 movement = horizontalDirection * horizontalSpeed * deltaTime;

        if (shouldRise)
        {
            float targetHeight = target.position.y + desiredHeightAbovePlayer;
            if (shouldRiseForHeight && body.position.y < targetHeight)
            {
                float newY = Mathf.Min(body.position.y + verticalRiseSpeed * deltaTime, targetHeight);
                movement.y = newY - body.position.y;
            }
            else if (shouldRiseForCollision)
            {
                movement.y = verticalRiseSpeed * deltaTime;
            }
        }

        body.MovePosition(body.position + movement);

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

        collisionRiseUntil = Mathf.Max(collisionRiseUntil, Time.time + collisionRiseDuration);
        heightEnough = false;
        heightShortSince = Time.time - heightShortDelay;
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
