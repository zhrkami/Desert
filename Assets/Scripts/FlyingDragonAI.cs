using UnityEngine;

[RequireComponent(typeof(Animation))]
public class FlyingDragonAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float normalHorizontalSpeed = 1.6f;
    [SerializeField] private float closeDistance = 7f;
    [SerializeField] private float desiredHeightAbovePlayer = 6f;
    [SerializeField] private float heightShortDelay = 2f;
    [SerializeField] private float verticalRiseSpeed = 1.2f;
    [SerializeField] private float ascendingHorizontalSpeedMultiplier = 0.5f;
    [SerializeField] private float attackInterval = 4f;
    [SerializeField] private string idleClipName = "dragon_idle";
    [SerializeField] private string attackClipName = "dragon_attack_repeatedly";
    [SerializeField] private bool heightEnough = true;

    private Animation animationComponent;
    private float heightShortSince = -1f;
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

        MoveAroundPlayer();
        PlayIdle();
    }

    private void MoveAroundPlayer()
    {
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;

        Vector3 horizontalDirection = Vector3.zero;
        float horizontalDistance = toPlayer.magnitude;
        if (horizontalDistance > 0.01f)
        {
            horizontalDirection = horizontalDistance < closeDistance ? -toPlayer.normalized : toPlayer.normalized;
        }

        UpdateHeightState();

        bool shouldRise = !heightEnough && Time.time - heightShortSince >= heightShortDelay;
        float horizontalSpeed = shouldRise ? normalHorizontalSpeed * ascendingHorizontalSpeedMultiplier : normalHorizontalSpeed;
        Vector3 movement = horizontalDirection * horizontalSpeed * Time.deltaTime;

        if (shouldRise)
        {
            float targetHeight = target.position.y + desiredHeightAbovePlayer;
            float newY = Mathf.Min(transform.position.y + verticalRiseSpeed * Time.deltaTime, targetHeight);
            movement.y = newY - transform.position.y;
        }

        transform.position += movement;

        if (horizontalDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(horizontalDirection, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 8f * Time.deltaTime);
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

    private void FaceTowardsPlayer()
    {
        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(toPlayer.normalized, Vector3.up),
            10f * Time.deltaTime);
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
