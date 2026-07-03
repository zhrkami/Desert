using UnityEngine;

public class MummySpawner : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private SimplePlayerController playerController;
    [SerializeField] private GameObject mummyPrefab;
    [SerializeField] private float spawnInterval = 4f;
    [SerializeField] private float minSpawnDistance = 16.8f;
    [SerializeField] private float maxSpawnDistance = 18.6f;
    [SerializeField] private float spawnWorldY = -4f;
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
        ResetSpawnTimer();
    }

    public void ResetSpawnTimer()
    {
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        if (player == null || mummyPrefab == null)
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

        SpawnMummy();
        nextSpawnTime = Time.time + spawnInterval;
    }

    private void SpawnMummy()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minSpawnDistance, maxSpawnDistance);
        Vector3 horizontalOffset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        Vector3 spawnPosition = player.position + horizontalOffset;
        spawnPosition.y = spawnWorldY;

        GameObject mummyRoot = new GameObject("MummyEnemy");
        mummyRoot.transform.position = spawnPosition;
        mummyRoot.transform.rotation = Quaternion.identity;

        GameObject mummyVisual = Instantiate(mummyPrefab, mummyRoot.transform);
        mummyVisual.name = "micro_mummy";
        mummyVisual.transform.localPosition = Vector3.zero;
        mummyVisual.transform.localRotation = Quaternion.identity;
        mummyVisual.transform.localScale = Vector3.one;

        MummyEnemyAI mummyAI = mummyRoot.AddComponent<MummyEnemyAI>();
        mummyAI.Initialize(player, playerController, mummyVisual.transform);
    }
}
