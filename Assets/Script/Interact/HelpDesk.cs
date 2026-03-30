using UnityEngine;
using Player.Interactor;

public class HelpDesk : MonoBehaviour, IInteractable
{
    [Header("UI Settings")]
    public GameObject helpDeskUICanvas;

    [Header("Spawning Settings")]
    public GameObject objectToSpawnPrefab;
    public Transform stageSpawnPoint;

    private void Start()
    {
        if (helpDeskUICanvas != null) helpDeskUICanvas.SetActive(false);
    }

    public void OnInteract(GameObject player)
    {
        // GATEKEEPER CHECK
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanInteract("HelpDesk"))
        {
            TutorialManager.Instance.ShowWarning("Don't touch the Help Desk! I'm giving you your assignments right now.");
            return;
        }

        if (helpDeskUICanvas != null) helpDeskUICanvas.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void StartGameSequence()
    {
        if (helpDeskUICanvas != null) helpDeskUICanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (objectToSpawnPrefab != null && stageSpawnPoint != null)
        {
            Instantiate(objectToSpawnPrefab, stageSpawnPoint.position, stageSpawnPoint.rotation);
        }
    }

    public void OnDrop() { } // Unused for HelpDesk
}