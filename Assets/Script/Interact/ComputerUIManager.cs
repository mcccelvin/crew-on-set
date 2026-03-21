using UnityEngine;
using System.IO;
using TMPro;

public class ComputerUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject recordingsGridPanel;
    public GameObject videoPlayerPanel;

    [Header("Grid Setup")]
    public Transform gridContentContainer;
    public GameObject clipCardPrefab;

    [Header("Player Setup")]
    public TruePixelPlayer pixelPlayer;
    public TextMeshProUGUI playerTitleText;

    private void OnEnable()
    {
        OpenGridView();
    }

    public void OpenGridView()
    {
        videoPlayerPanel.SetActive(false);
        recordingsGridPanel.SetActive(true);

        if (pixelPlayer != null) pixelPlayer.StopTape();

        RefreshGrid();
    }

    public void OpenPlayerView(string filePath)
    {
        recordingsGridPanel.SetActive(false);
        videoPlayerPanel.SetActive(true);

        if (playerTitleText != null)
        {
            playerTitleText.text = Path.GetFileNameWithoutExtension(filePath);
        }

        if (pixelPlayer != null)
        {
            pixelPlayer.PlayTape(filePath);
        }
    }

    public void DeleteClip(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"Deleted tape: {filePath}");
            RefreshGrid();
        }
    }

    private void RefreshGrid()
    {
        // 1. Destroy all old clip cards
        foreach (Transform child in gridContentContainer)
        {
            Destroy(child.gameObject);
        }

        // 2. Read the hard drive for ALL .tape files
        string[] tapeFiles = Directory.GetFiles(Application.persistentDataPath, "*.tape");

        // 3. Spawn a card for every single tape found
        foreach (string filePath in tapeFiles)
        {
            GameObject newCard = Instantiate(clipCardPrefab, gridContentContainer);
            ClipUIItem clipScript = newCard.GetComponent<ClipUIItem>();

            if (clipScript != null) clipScript.Setup(filePath, this);
        }
    }

    public void CloseComputerUI()
    {
        if (pixelPlayer != null) pixelPlayer.StopTape();
        gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Force the player's hands to unlock so they can pick things up again!
        Player.Interactor.EquipmentInteractor interactor = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        if (interactor != null) interactor.ClearActiveComputer();
    }
}