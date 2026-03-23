using UnityEngine;

public class StageSetupManager : MonoBehaviour
{
    [Header("Spawning Setup")]
    public GameObject wallPrefab; // Back to just one wall!
    public Transform spawnPoint;

    [Header("UI Elements")]
    [Tooltip("Drag your Spawn Wall button here")]
    public GameObject spawnWallButton;
    [Tooltip("Drag your ColorButtonsPanel here")]
    public GameObject colorButtonsContainer;

    [Header("Tracking")]
    private GameObject currentWall;

    private void Start()
    {
        // When the game starts, show the Spawn button and hide the Colors
        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorButtonsContainer != null) colorButtonsContainer.SetActive(false);
    }

    public void SpawnWall()
    {
        // Only spawn if a wall does NOT exist right now
        if (currentWall == null && wallPrefab != null && spawnPoint != null)
        {
            // Spawn the wall exactly at the spawn point
            currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);

            // UI MAGIC: Hide the Spawn button, and reveal the Color buttons
            if (spawnWallButton != null) spawnWallButton.SetActive(false);
            if (colorButtonsContainer != null) colorButtonsContainer.SetActive(true);

            Debug.Log("Terminal: Spawned the single wall!");

            if (TutorialManager.Instance != null) TutorialManager.Instance.OnStageWallBuilt();
        }
    }

    // --- COLORING ---
    public void ColorWallRed() { SetWallColor(Color.red); }
    public void ColorWallBlue() { SetWallColor(Color.blue); }
    public void ColorWallWhite() { SetWallColor(Color.white); }
    public void ColorWallBlack() { SetWallColor(Color.black); }

    private void SetWallColor(Color newColor)
    {
        if (currentWall != null)
        {
            MeshRenderer[] renderers = currentWall.GetComponentsInChildren<MeshRenderer>();

            if (renderers.Length > 0)
            {
                foreach (MeshRenderer renderer in renderers)
                {
                    renderer.material.color = newColor;
                }
                Debug.Log("Terminal: Successfully changed ALL wall pieces to " + newColor.ToString());
            }
            else
            {
                Debug.LogWarning("Terminal Error: No MeshRenderers found on the Wall Prefab!");
            }
        }
    }

    public void ClearStage()
    {
        // If we have a wall, destroy it
        if (currentWall != null)
        {
            Destroy(currentWall);
            currentWall = null; // Clear the memory
        }

        // UI MAGIC: Bring back the Spawn button, and hide the Color buttons
        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorButtonsContainer != null) colorButtonsContainer.SetActive(false);
    }
}