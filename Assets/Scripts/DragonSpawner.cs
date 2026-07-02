using UnityEngine;

public class DragonSpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private SimplePlayerController playerController;
    [SerializeField] private GameObject dragonPrefab;
    [SerializeField] private float spawnInterval = 3f;
    [SerializeField] private float minSpawnDistance = 18f;
    [SerializeField] private float maxSpawnDistance = 20f;
    [SerializeField] private float minSpawnWorldHeight = 7f;
    [SerializeField] private float maxSpawnWorldHeight = 8f;
    [SerializeField] private bool waitForPlayerControl = true;

    private float nextSpawnTime;

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
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 horizontalOffset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 spawnPosition = player.position + horizontalOffset;
        spawnPosition.y = Random.Range(minSpawnWorldHeight, maxSpawnWorldHeight);

        GameObject dragon = Instantiate(dragonPrefab, spawnPosition, Quaternion.identity);
        dragon.name = "FlyingFireDragon";

        FlyingDragonEnemyAI dragonAI = dragon.GetComponent<FlyingDragonEnemyAI>();
        if (dragonAI == null)
        {
            dragonAI = dragon.AddComponent<FlyingDragonEnemyAI>();
        }

        dragonAI.SetTarget(player);
    }
}
