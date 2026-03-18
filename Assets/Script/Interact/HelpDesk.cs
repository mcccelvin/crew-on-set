using UnityEngine;
using Player.Interactor;

public class HelpDesk : MonoBehaviour, IInteractable
{
    [Header("UI Settings")]
    [Tooltip("Drag your Help Desk Canvas here")]
    public GameObject helpDeskUICanvas;

    [Header("Spawning Settings")]
    [Tooltip("The prop or actor to spawn on the stage")]
    public GameObject objectToSpawnPrefab;
    [Tooltip("An empty object on your stage where the prefab will appear")]
    public Transform stageSpawnPoint;

    private void Start()
    {
        // Make sure the UI is hidden when the game starts
        if (helpDeskUICanvas != null) helpDeskUICanvas.SetActive(false);
    }

    // PRESS 'E' - Opens the Help Desk UI
    public void OnInteract(GameObject player)
    {
        Debug.Log("HELP DESK: The player pressed E and successfully clicked me!"); // <--- ADD THIS LINE

        if (helpDeskUICanvas != null) helpDeskUICanvas.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // LINK TO UI BUTTON: Spawns the object and closes the screen
    public void StartGameSequence()
    {
        if (helpDeskUICanvas != null) helpDeskUICanvas.SetActive(false);

        // Lock the mouse back to the game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Spawn the object on the stage!
        if (objectToSpawnPrefab != null && stageSpawnPoint != null)
        {
            Instantiate(objectToSpawnPrefab, stageSpawnPoint.position, stageSpawnPoint.rotation);
            Debug.Log("Help Desk: Game Started! Object spawned at stage.");

            // CRUCIAL: Tell the ReplayManager to find the newly spawned object so it gets recorded!
            ReplayManager replayManager = FindObjectOfType<ReplayManager>();
            if (replayManager != null)
            {
                replayManager.allRecordables = FindObjectsOfType<RecordableTransform>(true);
            }
        }
        else
        {
            Debug.LogWarning("Help Desk: Missing prefab or spawn point in the Inspector!");
        }
    }

    // Required by your IInteractable interface
    public void OnDrop() { }


}