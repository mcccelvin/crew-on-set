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
    private ComputerStation physicalComputer;

    private void Awake() { physicalComputer = FindObjectOfType<ComputerStation>(); }

    private void OnEnable() { OpenGridView(); }

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
    }

    public void DeleteClip(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            if (physicalComputer != null) physicalComputer.RemoveDeletedFile(Path.GetFileName(filePath));
            RefreshGrid();
        }
    }

    private void RefreshGrid()
    {
        foreach (Transform child in gridContentContainer) Destroy(child.gameObject);
        if (physicalComputer != null)
        {
            List<FootageData> insertedTapes = physicalComputer.GetInsertedFiles();
            foreach (FootageData data in insertedTapes)
            {
                string fullPath = Path.Combine(Application.persistentDataPath, data.fileName);
                if (File.Exists(fullPath))
                {
                    GameObject newCard = Instantiate(clipCardPrefab, gridContentContainer);
                    ClipUIItem clipScript = newCard.GetComponent<ClipUIItem>();
                    if (clipScript != null) clipScript.Setup(fullPath, this);
                }
            }
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

    // --- BULLETPROOF EXPORT LOGIC ---
    public void SendToEditor()
    {
        // 1. Safety Check: Does the backpack exist?
        if (ProjectDataManager.Instance == null)
        {
            Debug.LogError("CRASH PREVENTED: ProjectDataManager is missing from the Studio scene! Please create an empty GameObject and attach ProjectDataManager.cs to it.");
            return;
        }

        // 2. Safety Check: Did the array somehow get wiped?
        if (ProjectDataManager.Instance.compiledFootage == null)
        {
            ProjectDataManager.Instance.compiledFootage = new List<FootageData>();
        }

        if (physicalComputer == null) physicalComputer = FindObjectOfType<ComputerStation>();

        // 3. Safely transfer the files
        ProjectDataManager.Instance.ClearProject();
        if (physicalComputer != null)
        {
            List<FootageData> files = physicalComputer.GetInsertedFiles();
            if (files != null)
            {
                foreach (FootageData data in files) ProjectDataManager.Instance.compiledFootage.Add(data);
            }
        }

        // 4. Grade the Stage before we leave
        EvaluateStagePreProduction();

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVideoSubmitted();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Editor");
    }

    private void EvaluateStagePreProduction()
    {
        float score = 100f;
        string feedback = "<color=white><b>--- PRE-PRODUCTION ---</b></color>\n";

        int progress = PlayerPrefs.GetInt("TutorialProgress", 0);
        StageSetupManager stage = FindObjectOfType<StageSetupManager>(true);
        RecordableSubject product = FindObjectOfType<RecordableSubject>(true);

        if (progress < 2)
        {
            if (stage != null && stage.currentWallColor.r > 0.5f && stage.currentWallColor.b > 0.5f)
                feedback += "<color=green>+ Good Pink Stage Design</color>\n";
            else { score -= 30f; feedback += "<color=yellow>- Backdrop needs to be Pink.</color>\n"; }
        }
        else
        {
            if (stage != null && product != null)
            {
                float depthDistance = Vector3.Distance(product.transform.position, stage.transform.position);
                if (depthDistance >= 1.5f) feedback += "<color=green>+ Great Stage Depth</color>\n";
                else { score -= 30f; feedback += $"<color=yellow>- The shot felt flat. Pull product away from wall.</color>\n"; }

                Color bg = stage.currentWallColor;
                if (bg.r > 0.5f && bg.g < 0.3f && bg.b < 0.3f) feedback += "<color=green>+ Correct Red Backdrop</color>\n";
                else { score -= 20f; feedback += "<color=yellow>- Wrong set color. We requested RED.</color>\n"; }
            }
            else { score -= 50f; feedback += "<color=red>- Error: Stage or Product missing.</color>\n"; }
        }

        ProjectDataManager.Instance.savedPreProdScore = score;
        ProjectDataManager.Instance.savedPreProdFeedback = feedback + "\n";
    }
}