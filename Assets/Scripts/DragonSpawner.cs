using UnityEngine;

public class DragonSpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private SimplePlayerController playerController;
    [SerializeField] private GameObject dragonPrefab;
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private float minSpawnDistance = 18f;
    [SerializeField] private float maxSpawnDistance = 20f;
    [SerializeField] private float minSpawnWorldHeight = 7f;
    [SerializeField] private float maxSpawnWorldHeight = 8f;
    [SerializeField] private float spawnGrowDuration = 1f;
    [SerializeField] private float spawnInitialScale = 0.05f;
    [SerializeField] private int spawnPositionAttempts = 30;
    [SerializeField] private float spawnClearanceRadius = 2.25f;
    [SerializeField] private float spawnClearanceHeight = 3.5f;
    [SerializeField] private LayerMask spawnBlockLayers = -1;
    [SerializeField] private bool waitForPlayerControl = true;

    private float nextSpawnTime;
    private readonly Collider[] spawnOverlapBuffer = new Collider[16];

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        if (playerController == null && player != null)
        {
            playerController = player.GetComponent<SimplePlayerController>();
        }
    }

    private void Start()
    {
        ResetSpawnTimer();
    }

    public void ResetSpawnTimer()
    {
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        if (player == null || dragonPrefab == null)
        {
            return;
        }

        if (waitForPlayerControl && playerController != null && !playerController.IsControlEnabled)
        {
            nextSpawnTime = Time.time + spawnInterval;
            return;
        }

        if (Time.time < nextSpawnTime)
        {
            return;
        }

        SpawnDragon();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void SpawnDragon()
    {
        if (!TryGetOpenSpawnPosition(out Vector3 spawnPosition))
        {
            return;
        }

        GameObject dragon = Instantiate(dragonPrefab, spawnPosition, Quaternion.identity);
        dragon.name = "FlyingFireDragon";

        FlyingDragonEnemyAI dragonAI = dragon.GetComponent<FlyingDragonEnemyAI>();
        if (dragonAI == null)
        {
            dragonAI = dragon.AddComponent<FlyingDragonEnemyAI>();
        }

        dragonAI.SetTarget(player);
        dragonAI.BeginSpawnGrow(spawnGrowDuration, spawnInitialScale);
    }

    private bool TryGetOpenSpawnPosition(out Vector3 spawnPosition)
    {
        int attempts = Mathf.Max(1, spawnPositionAttempts);
        for (int i = 0; i < attempts; i++)
        {
            spawnPosition = GetRandomSpawnPosition();
            if (IsSpawnPositionOpen(spawnPosition))
            {
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private Vector3 GetRandomSpawnPosition()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 horizontalOffset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 spawnPosition = player.position + horizontalOffset;
        spawnPosition.y = Random.Range(minSpawnWorldHeight, maxSpawnWorldHeight);
        return spawnPosition;
    }

    private bool IsSpawnPositionOpen(Vector3 spawnPosition)
    {
        float radius = Mathf.Max(0.05f, spawnClearanceRadius);
        float height = Mathf.Max(radius * 2f, spawnClearanceHeight);
        float capsuleHalfLine = Mathf.Max(0f, (height * 0.5f) - radius);
        Vector3 pointA = spawnPosition + Vector3.up * capsuleHalfLine;
        Vector3 pointB = spawnPosition - Vector3.up * capsuleHalfLine;

        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            pointA,
            pointB,
            radius,
            spawnOverlapBuffer,
            spawnBlockLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = spawnOverlapBuffer[i];
            if (overlap == null || ShouldIgnoreSpawnOverlap(overlap))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool ShouldIgnoreSpawnOverlap(Collider overlap)
    {
        if (overlap.transform.IsChildOf(transform))
        {
            return true;
        }

        return player != null && overlap.transform.IsChildOf(player);
    }
}
