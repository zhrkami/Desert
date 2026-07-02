using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class SimplePlayerController : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpVelocity = 6.5f;
    [SerializeField] private float playerMass = 160f;
    [SerializeField] private float fallGravityMultiplier = 2.5f;
    [SerializeField] private float mouseSensitivity = 2.5f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 75f;
    [SerializeField] private float minGroundNormalY = 0.45f;
    [SerializeField] private float groundedGraceTime = 0.12f;
    [SerializeField] private float steepContactMemoryTime = 0.18f;
    [SerializeField] private PhysicMaterial playerPhysicsMaterial;

    private Rigidbody body;
    private CapsuleCollider capsuleCollider;
    private PhysicMaterial runtimeFrictionlessMaterial;
    private float pitch;
    private float yaw;
    private float lastGroundedTime = -100f;
    private float lastSteepContactTime = -100f;
    private Vector3 steepContactNormal;
    private bool jumpRequested;
    private bool controlEnabled;

    public Transform CameraTransform
    {
        get { return cameraTransform; }
        set { cameraTransform = value; }
    }

    public bool IsControlEnabled
    {
        get { return controlEnabled; }
    }

    public float MoveSpeed
    {
        get { return moveSpeed; }
    }

    public float JumpVelocity
    {
        get { return jumpVelocity; }
    }

    public float PlayerMass
    {
        get { return playerMass; }
    }

    public float FallGravityMultiplier
    {
        get { return fallGravityMultiplier; }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        ConfigureRigidbody();
        ConfigureCollider();
        yaw = transform.eulerAngles.y;
        if (cameraTransform != null)
        {
            pitch = NormalizeAngle(cameraTransform.localEulerAngles.x);
        }
    }

    private void Update()
    {
        if (!controlEnabled)
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequested = true;
        }

        yaw += mouseX;
        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);

        if (cameraTransform != null)
        {
            cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
        }
    }

    private void FixedUpdate()
    {
        if (!controlEnabled)
        {
            return;
        }

        Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
        body.MoveRotation(yawRotation);
        body.rotation = yawRotation;
        body.angularVelocity = Vector3.zero;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        Vector3 forward = yawRotation * Vector3.forward;
        Vector3 right = yawRotation * Vector3.right;
        Vector3 move = (forward * vertical) + (right * horizontal);
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        Vector3 velocity = body.velocity;
        Vector3 targetHorizontalVelocity = move * moveSpeed;
        targetHorizontalVelocity = RemoveAirborneWallPush(targetHorizontalVelocity);
        velocity.x = targetHorizontalVelocity.x;
        velocity.z = targetHorizontalVelocity.z;

        if (jumpRequested && Time.time - lastGroundedTime <= groundedGraceTime)
        {
            velocity.y = jumpVelocity;
            lastGroundedTime = -100f;
        }

        jumpRequested = false;
        body.velocity = velocity;

        if (!IsGrounded() && body.velocity.y < 0f && fallGravityMultiplier > 1f)
        {
            body.AddForce(Physics.gravity * (fallGravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        RegisterCollisionContacts(collision);
    }

    private void OnCollisionEnter(Collision collision)
    {
        RegisterCollisionContacts(collision);
    }

    private void RegisterCollisionContacts(Collision collision)
    {
        Vector3 steepNormalSum = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (normal.y >= minGroundNormalY)
            {
                lastGroundedTime = Time.time;
            }
            else if (normal.y > -0.1f)
            {
                steepNormalSum += normal;
            }
        }

        steepNormalSum.y = 0f;
        if (steepNormalSum.sqrMagnitude > 0.0001f)
        {
            steepContactNormal = steepNormalSum.normalized;
            lastSteepContactTime = Time.time;
        }
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        yaw = transform.eulerAngles.y;

        if (body != null)
        {
            ConfigureRigidbody();
        }

        if (capsuleCollider != null)
        {
            ConfigureCollider();
        }

        Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !enabled;

        if (!enabled && body != null)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (!enabled)
        {
            jumpRequested = false;
        }
    }

    private void ConfigureRigidbody()
    {
        body.mass = playerMass;
        body.drag = 0f;
        body.angularDrag = 0f;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void ConfigureCollider()
    {
        if (playerPhysicsMaterial == null)
        {
            if (runtimeFrictionlessMaterial == null)
            {
                runtimeFrictionlessMaterial = new PhysicMaterial("Player_Frictionless_Runtime");
                ConfigureFrictionlessMaterial(runtimeFrictionlessMaterial);
            }

            playerPhysicsMaterial = runtimeFrictionlessMaterial;
        }

        ConfigureFrictionlessMaterial(playerPhysicsMaterial);
        capsuleCollider.sharedMaterial = playerPhysicsMaterial;
    }

    private static void ConfigureFrictionlessMaterial(PhysicMaterial material)
    {
        material.staticFriction = 0f;
        material.dynamicFriction = 0f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicMaterialCombine.Minimum;
        material.bounceCombine = PhysicMaterialCombine.Minimum;
    }

    private Vector3 RemoveAirborneWallPush(Vector3 targetHorizontalVelocity)
    {
        if (IsGrounded())
        {
            return targetHorizontalVelocity;
        }

        if (Time.time - lastSteepContactTime > steepContactMemoryTime)
        {
            return targetHorizontalVelocity;
        }

        Vector3 wallNormal = steepContactNormal;
        wallNormal.y = 0f;
        if (wallNormal.sqrMagnitude <= 0.0001f)
        {
            return targetHorizontalVelocity;
        }

        wallNormal.Normalize();
        float inwardSpeed = Vector3.Dot(targetHorizontalVelocity, wallNormal);
        if (inwardSpeed < 0f)
        {
            targetHorizontalVelocity -= wallNormal * inwardSpeed;
        }

        return targetHorizontalVelocity;
    }

    private bool IsGrounded()
    {
        return Time.time - lastGroundedTime <= groundedGraceTime;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }
}
