using UnityEngine;

public class MummyAI : MonoBehaviour
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
    [SerializeField] private float attackDistance = 3f;
    [SerializeField] private float fallbackPlayerHalfSpeed = 3f;
    [SerializeField] private float capsuleHeight = 2.25f;
    [SerializeField] private float capsuleRadius = 0.45f;
    [SerializeField] private Vector3 capsuleCenter = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private float maxGhostDuration = 8f;
    [SerializeField] private string flyClipName = "mummy_fly_up_loop";
    [SerializeField] private string walkClipName = "mummy_walk";
    [SerializeField] private string attackClipName = "mummy_attack_repeatedly";
    [SerializeField] private MummyState state = MummyState.Ghost;

    private Animation animationComponent;
    private CapsuleCollider capsuleCollider;
    private Rigidbody body;
    private bool hasTouchedBlockingSpace;
    private float ghostStartedAt;
    private Quaternion ghostVisualRotation;

    public void Initialize(Transform newTarget, SimplePlayerController newPlayerController, Transform newVisualRoot)
    {
        target = newTarget;
        playerController = newPlayerController;
        visualRoot = newVisualRoot;
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

        if (visualRoot == null && transform.childCount > 0)
        {
            visualRoot = transform.GetChild(0);
        }

        if (animationComponent == null)
        {
            animationComponent = visualRoot != null ? visualRoot.GetComponent<Animation>() : GetComponentInChildren<Animation>();
        }

        ghostVisualRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        ConfigurePhysicsBody();
        ConfigureAnimationClips();
    }

    private void Start()
    {
        EnterGhostState();
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            return;
        }

        if (state == MummyState.Ghost)
        {
            UpdateGhostState();
        }
        else
        {
            UpdateNormalState();
        }
    }

    private void ConfigurePhysicsBody()
    {
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        capsuleCollider.height = capsuleHeight;
        capsuleCollider.radius = capsuleRadius;
        capsuleCollider.center = capsuleCenter;
        capsuleCollider.direction = 1;
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

        SetClipWrapMode(flyClipName, WrapMode.Loop);
        SetClipWrapMode(walkClipName, WrapMode.Loop);
        SetClipWrapMode(attackClipName, WrapMode.Loop);
    }

    private void EnterGhostState()
    {
        state = MummyState.Ghost;
        ghostStartedAt = Time.time;
        hasTouchedBlockingSpace = false;
        capsuleCollider.isTrigger = true;

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
        capsuleCollider.isTrigger = false;

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
        Vector3 toPlayer = target.position - body.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        if (distance <= attackDistance)
        {
            FacePlayer();
            PlayLoop(attackClipName);
            return;
        }

        if (distance > 0.01f)
        {
            Vector3 direction = toPlayer / distance;
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, 10f * Time.fixedDeltaTime));

            float moveSpeed = playerController != null ? playerController.MoveSpeed * 0.5f : fallbackPlayerHalfSpeed;
            body.MovePosition(body.position + direction * moveSpeed * Time.fixedDeltaTime);
        }

        PlayLoop(walkClipName);
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

    private void SetClipWrapMode(string clipName, WrapMode wrapMode)
    {
        if (animationComponent != null && animationComponent[clipName] != null)
        {
            animationComponent[clipName].wrapMode = wrapMode;
        }
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
