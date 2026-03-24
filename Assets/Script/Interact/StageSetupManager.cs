using UnityEngine;

public class StageSetupManager : MonoBehaviour
{
    [Header("Spawning Setup")]
    public GameObject wallPrefab;
    public Transform spawnPoint;

    [Header("UI Elements")]
    public GameObject spawnWallButton;
    public GameObject colorButtonsContainer;

    [Header("Tracking")]
    private GameObject currentWall;

    // --- NEW: Variables for the Grader to read! ---
    public Color currentWallColor = Color.clear;
    public bool HasWall() { return currentWall != null; }

    private void Start()
    {
        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorButtonsContainer != null) colorButtonsContainer.SetActive(false);
    }

    public void SpawnWall()
    {
        if (currentWall == null && wallPrefab != null && spawnPoint != null)
        {
            currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);

            if (spawnWallButton != null) spawnWallButton.SetActive(false);
            if (colorButtonsContainer != null) colorButtonsContainer.SetActive(true);

            // Default wall color is usually white when spawned
            currentWallColor = Color.white;

            Debug.Log("Terminal: Spawned the single wall!");
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnStageWallBuilt();
        }
    }

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

                // --- NEW: Save the color to memory! ---
                currentWallColor = newColor;
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
        if (currentWall != null)
        {
            Destroy(currentWall);
            currentWall = null;
        }

        // --- NEW: Wipe the memory so they fail if the wall is gone! ---
        currentWallColor = Color.clear;

        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorButtonsContainer != null) colorButtonsContainer.SetActive(false);
    }
}