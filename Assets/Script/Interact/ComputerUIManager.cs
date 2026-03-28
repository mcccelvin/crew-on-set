using UnityEngine;
using System.Collections.Generic; // --- NEW: Needed for Lists! ---
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

    private string currentlyPlayingFile = "";
    public ContractGrader grader; // Link to our new grading script!

    // --- NEW: A link to the physical computer tower! ---
    private ComputerStation physicalComputer;

    private void Awake()
    {
        // Automatically find the physical computer tower in the room
        physicalComputer = FindObjectOfType<ComputerStation>();
    }

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

        currentlyPlayingFile = Path.GetFileName(filePath); // <-- NEW: Remember the file!

        if (playerTitleText != null) playerTitleText.text = Path.GetFileNameWithoutExtension(filePath);
        if (pixelPlayer != null) pixelPlayer.PlayTape(filePath);
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVideoPlayed();
    }

    public void DeleteClip(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"Deleted tape: {filePath}");

            // --- NEW: Also remove it from the ComputerStation's memory so it doesn't try to play a deleted file! ---
            if (physicalComputer != null) physicalComputer.RemoveDeletedFile(Path.GetFileName(filePath));

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

        // --- THE FIX: Only spawn cards for files currently plugged into the tower! ---
        if (physicalComputer != null)
        {
            // Get the list of inserted SD cards from the tower
            List<string> insertedTapes = physicalComputer.GetInsertedFiles();

            // Spawn a card ONLY for the tapes currently inserted
            foreach (string fileName in insertedTapes)
            {
                // We have to build the full path so the player can actually find the file
                string fullPath = Path.Combine(Application.persistentDataPath, fileName);

                if (File.Exists(fullPath))
                {
                    GameObject newCard = Instantiate(clipCardPrefab, gridContentContainer);
                    ClipUIItem clipScript = newCard.GetComponent<ClipUIItem>();

                    if (clipScript != null) clipScript.Setup(fullPath, this);
                }
            }

            if (insertedTapes.Count == 0)
            {
                Debug.Log("Computer UI: No SD Cards inserted!");
            }
        }
        else
        {
            Debug.LogError("Computer UI: Could not find the ComputerStation in the scene!");
        }
    }

    public void CloseComputerUI()
    {
        if (pixelPlayer != null) pixelPlayer.StopTape();
        gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Player.Interactor.EquipmentInteractor interactor = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        if (interactor != null) interactor.ClearActiveComputer();
    }

    public void SendToEditor()
    {
        // Find the actual computer station that holds the inserted SD cards
        ComputerStation station = FindObjectOfType<ComputerStation>();

        if (ProjectDataManager.Instance != null && station != null)
        {
            ProjectDataManager.Instance.ClearProject();

            // Access the list FROM the station script
            List<string> files = station.GetInsertedFiles();

            foreach (string file in files)
            {
                FootageData data = new FootageData();
                data.fileName = file;
                // Placeholder scores
                data.camScore = 70f;
                data.lightScore = 30f;

                ProjectDataManager.Instance.compiledFootage.Add(data);
            }
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene("Editor");
    }
}