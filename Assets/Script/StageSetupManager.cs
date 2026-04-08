using UnityEngine;

public class StageSetupManager : MonoBehaviour
{
    [Header("Spawning Setup")]
    public GameObject wallPrefab;
    public Transform spawnPoint;

    [Header("UI Elements")]
    public GameObject spawnWallButton;
    public GameObject colorControlPanel;

    [Header("Tracking")]
    private GameObject currentWall;

    public Color currentWallColor = Color.white;
    public bool HasWall() { return currentWall != null; }

    private void Start()
    {
        if (spawnWallButton != null) spawnWallButton.SetActive(true);

        // FIX: Permanently lock the color sliders to ALWAYS be visible!
        if (colorControlPanel != null) colorControlPanel.SetActive(true);
    }

    public void SpawnWall()
    {
        if (currentWall == null && wallPrefab != null && spawnPoint != null)
        {
            if (CareerManager.Instance != null)
            {
                if (CareerManager.Instance.playerMoney >= 50)
                {
                    CareerManager.Instance.playerMoney -= 50;
                    CareerManager.Instance.UpdateMoneyUI();
                }
                else
                {
                    Debug.LogWarning("Not enough B-Coins to spawn the wall!");
                    if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("The wall costs 50 B-Coins!");
                    return;
                }
            }

            currentWall = Instantiate(wallPrefab, spawnPoint.position, spawnPoint.rotation);

            if (spawnWallButton != null) spawnWallButton.SetActive(false);

            // Apply whatever color the sliders are CURRENTLY set to, the moment the wall spawns!
            ApplyColorToWall(currentWallColor);

        }
    }

    public void SetCustomColor(float r, float g, float b)
    {
        currentWallColor = new Color(r, g, b, 1f);

        // Only try to paint the wall if it actually exists!
        if (currentWall != null)
        {
            ApplyColorToWall(currentWallColor);
        }
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

        currentWallColor = Color.white;
        if (spawnWallButton != null) spawnWallButton.SetActive(true);

        // FIX: Ensure sliders stay on even when the stage is cleared!
        if (colorControlPanel != null) colorControlPanel.SetActive(true);

        DirectorTerminal terminal = FindObjectOfType<DirectorTerminal>();
        if (terminal != null)
        {
            terminal.ClearAllProps();
        }
    }
}