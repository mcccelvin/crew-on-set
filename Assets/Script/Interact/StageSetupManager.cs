using UnityEngine;

public class StageSetupManager : MonoBehaviour
{
    [Header("Spawning Setup")]
    public GameObject wallPrefab;
    public Transform spawnPoint;

    [Header("UI Elements")]
    public GameObject spawnWallButton;
    public GameObject colorControlPanel; // Rename your container to this

    [Header("Tracking")]
    private GameObject currentWall;

    public Color currentWallColor = Color.white;
    public bool HasWall() { return currentWall != null; }

    private void Start()
    {
        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorControlPanel != null) colorControlPanel.SetActive(false);
    }

    public void SpawnWall()
    {
        if (currentWall == null && wallPrefab != null && spawnPoint != null)
        {
            currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);

            if (spawnWallButton != null) spawnWallButton.SetActive(false);
            if (colorControlPanel != null) colorControlPanel.SetActive(true);

            currentWallColor = Color.white;
            if (TutorialManager.Instance != null) TutorialManager.Instance.OnStageWallBuilt();
        }
    }

    // --- NEW: Function for the Sliders to call ---
    public void SetCustomColor(float r, float g, float b)
    {
        currentWallColor = new Color(r, g, b, 1f);
        ApplyColorToWall(currentWallColor);
    }

    private void ApplyColorToWall(Color newColor)
    {
        if (currentWall != null)
        {
            MeshRenderer[] renderers = currentWall.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                renderer.material.color = newColor;
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

        currentWallColor = Color.clear;
        if (spawnWallButton != null) spawnWallButton.SetActive(true);
        if (colorControlPanel != null) colorControlPanel.SetActive(false);
    }
}