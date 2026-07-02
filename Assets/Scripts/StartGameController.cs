using UnityEngine;
using UnityEngine.UI;

public class StartGameController : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Camera gameCamera;
    [SerializeField] private GameObject startSquare;
    [SerializeField] private Button startButton;
    [SerializeField] private SimplePlayerController playerController;

    private void Awake()
    {
        ApplyInitialState();

        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
            startButton.onClick.AddListener(StartGame);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }
    }

    public void StartGame()
    {
        if (startSquare != null)
        {
            startSquare.SetActive(false);
        }

        SetCameraActive(mainCamera, false);
        SetCameraActive(gameCamera, true);

        if (playerController != null)
        {
            playerController.SetControlEnabled(true);
        }
    }

    private void ApplyInitialState()
    {
        if (startSquare != null)
        {
            startSquare.SetActive(true);
        }

        SetCameraActive(mainCamera, true);
        SetCameraActive(gameCamera, false);

        if (playerController != null)
        {
            playerController.SetControlEnabled(false);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private static void SetCameraActive(Camera cameraToSet, bool active)
    {
        if (cameraToSet == null)
        {
            return;
        }

        cameraToSet.enabled = active;

        AudioListener listener = cameraToSet.GetComponent<AudioListener>();
        if (listener != null)
        {
            listener.enabled = active;
        }
    }
}
