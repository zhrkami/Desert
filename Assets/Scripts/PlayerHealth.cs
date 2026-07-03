using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float gameOverDuration = 2f;
    [SerializeField] private Vector2 healthBarOffset = new Vector2(24f, 34f);
    [SerializeField] private Vector2 healthBarSize = new Vector2(260f, 22f);
    [SerializeField] private Color healthBarColor = new Color(0.08f, 0.86f, 0.22f, 1f);

    private SimplePlayerController playerController;
    private Transform cameraTransform;
    private Texture2D solidPixel;
    private GUIStyle gameOverStyle;
    private GUIStyle restartButtonStyle;
    private Vector3 initialPlayerPosition;
    private Quaternion initialPlayerRotation;
    private Quaternion initialCameraLocalRotation;
    private Quaternion gameOverStartCameraRotation;
    private float currentHealth;
    private float gameOverElapsed;
    private bool isGameOver;
    private bool gameOverComplete;

    public float CurrentHealth
    {
        get { return currentHealth; }
    }

    public float MaxHealth
    {
        get { return maxHealth; }
    }

    public static PlayerHealth GetOrCreate(GameObject playerObject)
    {
        if (playerObject == null)
        {
            return null;
        }

        PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
        if (health == null)
        {
            health = playerObject.AddComponent<PlayerHealth>();
        }

        return health;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsurePlayerHealthExists()
    {
        SimplePlayerController controller = FindFirstObjectByType<SimplePlayerController>();
        if (controller != null)
        {
            GetOrCreate(controller.gameObject);
        }
    }

    private void Awake()
    {
        playerController = GetComponent<SimplePlayerController>();
        cameraTransform = playerController != null ? playerController.CameraTransform : null;
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        currentHealth = maxHealth;
        CaptureInitialPose();
        EnsurePixelTexture();
    }

    private void Update()
    {
        if (!isGameOver)
        {
            return;
        }

        UpdateGameOverSequence();
    }

    private void OnGUI()
    {
        EnsurePixelTexture();
        EnsureGuiStyles();

        if (ShouldDrawHealthBar())
        {
            DrawHealthBar();
        }

        if (isGameOver)
        {
            DrawGameOverOverlay();
        }
    }

    private void OnDestroy()
    {
        if (solidPixel != null)
        {
            Destroy(solidPixel);
        }
    }

    public void ApplyDamage(float damage)
    {
        if (isGameOver || damage <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth <= 0f)
        {
            BeginGameOver();
        }
    }

    private void BeginGameOver()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        gameOverComplete = false;
        gameOverElapsed = 0f;

        if (cameraTransform == null && playerController != null)
        {
            cameraTransform = playerController.CameraTransform;
        }

        if (cameraTransform != null)
        {
            gameOverStartCameraRotation = cameraTransform.rotation;
        }

        if (playerController != null)
        {
            playerController.SetControlEnabled(false);
        }

        if (cameraTransform != null)
        {
            cameraTransform.rotation = gameOverStartCameraRotation;
        }
    }

    private void UpdateGameOverSequence()
    {
        gameOverElapsed += Time.deltaTime;
        float duration = Mathf.Max(0.01f, gameOverDuration);
        float progress = Mathf.Clamp01(gameOverElapsed / duration);

        if (cameraTransform != null)
        {
            Vector3 skyUp = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward;
            Quaternion skyRotation = Quaternion.LookRotation(Vector3.up, skyUp);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            cameraTransform.rotation = Quaternion.Slerp(gameOverStartCameraRotation, skyRotation, smoothProgress);
        }

        if (progress >= 1f)
        {
            gameOverComplete = true;
        }
    }

    private void RestartGame()
    {
        ClearRuntimeEnemies();
        ResetSpawnerTimers();

        currentHealth = maxHealth;
        isGameOver = false;
        gameOverComplete = false;
        gameOverElapsed = 0f;

        if (playerController != null)
        {
            playerController.SetControlEnabled(true);
            playerController.ResetToPose(initialPlayerPosition, initialPlayerRotation, initialCameraLocalRotation);
        }
        else
        {
            transform.SetPositionAndRotation(initialPlayerPosition, initialPlayerRotation);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = initialCameraLocalRotation;
            }
        }
    }

    private void CaptureInitialPose()
    {
        initialPlayerPosition = transform.position;
        initialPlayerRotation = transform.rotation;
        initialCameraLocalRotation = cameraTransform != null ? cameraTransform.localRotation : Quaternion.identity;
    }

    private bool ShouldDrawHealthBar()
    {
        if (isGameOver)
        {
            return true;
        }

        return playerController == null || playerController.IsControlEnabled;
    }

    private void DrawHealthBar()
    {
        float ratio = maxHealth <= 0f ? 0f : Mathf.Clamp01(currentHealth / maxHealth);
        float width = Mathf.Min(healthBarSize.x, Screen.width - healthBarOffset.x * 2f);
        float height = healthBarSize.y;
        Rect backgroundRect = new Rect(healthBarOffset.x, Screen.height - healthBarOffset.y - height, width, height);
        Rect fillRect = new Rect(backgroundRect.x, backgroundRect.y, backgroundRect.width * ratio, backgroundRect.height);

        DrawSolidRect(backgroundRect, new Color(0f, 0f, 0f, 0.5f));
        DrawSolidRect(fillRect, healthBarColor);
    }

    private void DrawGameOverOverlay()
    {
        float alpha = Mathf.Clamp01(gameOverElapsed / Mathf.Max(0.01f, gameOverDuration));
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, alpha));

        if (!gameOverComplete)
        {
            return;
        }

        GUI.Label(new Rect(0f, Screen.height * 0.36f, Screen.width, 80f), "game over", gameOverStyle);

        float buttonWidth = Mathf.Min(220f, Screen.width - 48f);
        Rect buttonRect = new Rect((Screen.width - buttonWidth) * 0.5f, Screen.height * 0.55f, buttonWidth, 54f);
        if (GUI.Button(buttonRect, "restart", restartButtonStyle))
        {
            RestartGame();
        }
    }

    private void ClearRuntimeEnemies()
    {
        HashSet<GameObject> enemyObjects = new HashSet<GameObject>();
        CollectEnemyObjects<MummyEnemyAI>(enemyObjects);
        CollectEnemyObjects<MummyAI>(enemyObjects);
        CollectEnemyObjects<FlyingDragonEnemyAI>(enemyObjects);
        CollectEnemyObjects<FlyingDragonAI>(enemyObjects);

        foreach (GameObject enemyObject in enemyObjects)
        {
            if (enemyObject == null)
            {
                continue;
            }

            enemyObject.SetActive(false);
            Destroy(enemyObject);
        }
    }

    private static void CollectEnemyObjects<T>(HashSet<GameObject> enemyObjects) where T : Component
    {
        T[] enemies = FindObjectsByType<T>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            T enemy = enemies[i];
            if (enemy != null)
            {
                enemyObjects.Add(enemy.gameObject);
            }
        }
    }

    private static void ResetSpawnerTimers()
    {
        MummySpawner[] mummySpawners = FindObjectsByType<MummySpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < mummySpawners.Length; i++)
        {
            mummySpawners[i].ResetSpawnTimer();
        }

        DragonSpawner[] dragonSpawners = FindObjectsByType<DragonSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < dragonSpawners.Length; i++)
        {
            dragonSpawners[i].ResetSpawnTimer();
        }
    }

    private void EnsurePixelTexture()
    {
        if (solidPixel != null)
        {
            return;
        }

        solidPixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        solidPixel.SetPixel(0, 0, Color.white);
        solidPixel.Apply();
    }

    private void EnsureGuiStyles()
    {
        if (gameOverStyle == null)
        {
            gameOverStyle = new GUIStyle(GUI.skin.label);
            gameOverStyle.alignment = TextAnchor.MiddleCenter;
            gameOverStyle.fontSize = 52;
            gameOverStyle.normal.textColor = Color.white;
        }

        if (restartButtonStyle == null)
        {
            restartButtonStyle = new GUIStyle(GUI.skin.button);
            restartButtonStyle.fontSize = 26;
        }
    }

    private void DrawSolidRect(Rect rect, Color color)
    {
        Color previousColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, solidPixel);
        GUI.color = previousColor;
    }
}
