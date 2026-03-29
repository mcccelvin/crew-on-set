using UnityEngine;
using System.Collections.Generic;
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
    public ContractGrader grader;

    private ComputerStation physicalComputer;

    private void Awake()
    {
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

        currentlyPlayingFile = Path.GetFileName(filePath);

        if (playerTitleText != null) playerTitleText.text = Path.GetFileNameWithoutExtension(filePath);
        if (pixelPlayer != null) pixelPlayer.PlayTape(filePath);

        // --- NEW TUTORIAL PING ---
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVideoPlayed();
    }

    public void DeleteClip(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            Debug.Log($"Deleted tape: {filePath}");

            if (physicalComputer != null) physicalComputer.RemoveDeletedFile(Path.GetFileName(filePath));

            RefreshGrid();
        }
    }

    private void RefreshGrid()
    {
        foreach (Transform child in gridContentContainer)
        {
            Destroy(child.gameObject);
        }

        if (physicalComputer != null)
        {
            List<string> insertedTapes = physicalComputer.GetInsertedFiles();

            foreach (string fileName in insertedTapes)
            {
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
        ComputerStation station = FindObjectOfType<ComputerStation>();

        if (ProjectDataManager.Instance != null && station != null)
        {
            ProjectDataManager.Instance.ClearProject();

            List<string> files = station.GetInsertedFiles();

            foreach (string file in files)
            {
                FootageData data = new FootageData();
                data.fileName = file;
                data.camScore = 70f;
                data.lightScore = 30f;

                ProjectDataManager.Instance.compiledFootage.Add(data);
            }
        }

        // --- NEW TUTORIAL PING ---
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVideoSubmitted();

        UnityEngine.SceneManagement.SceneManager.LoadScene("Editor");
    }
}