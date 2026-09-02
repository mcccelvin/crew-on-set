using UnityEngine;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Player.Equipment;

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

    // --- THE FIX: Make sure the panels are turned OFF when you boot up the computer! ---
    private void OnEnable()
    {
        if (recordingsGridPanel != null) recordingsGridPanel.SetActive(false);
        if (videoPlayerPanel != null) videoPlayerPanel.SetActive(false);
        if (pixelPlayer != null) pixelPlayer.StopTape();
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
        int cardIndex = 0;

        if (physicalComputer != null)
        {
            List<FootageData> insertedTapes = physicalComputer.GetInsertedFiles();
            foreach (FootageData data in insertedTapes)
            {
                string fullPath = Path.Combine(Application.persistentDataPath, data.fileName);
                if (File.Exists(fullPath))
                {
                    GameObject newCard;
                    if (cardIndex < gridContentContainer.childCount)
                    {
                        newCard = gridContentContainer.GetChild(cardIndex).gameObject;
                        newCard.SetActive(true);
                    }
                    else
                    {
                        newCard = Instantiate(clipCardPrefab, gridContentContainer);
                    }

                    ClipUIItem clipScript = newCard.GetComponent<ClipUIItem>();
                    if (clipScript != null) clipScript.Setup(fullPath, this);
                    cardIndex++;
                }
            }
        }

        for (int i = cardIndex; i < gridContentContainer.childCount; i++)
        {
            gridContentContainer.GetChild(i).gameObject.SetActive(false);
        }
    }

    public void CloseComputerUI()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanCloseUI("ComputerStation")) return;

        if (pixelPlayer != null) pixelPlayer.StopTape();

        if (physicalComputer == null) physicalComputer = FindObjectOfType<ComputerStation>();
        if (physicalComputer != null)
        {
            physicalComputer.CloseComputerUI();
            return;
        }

        gameObject.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Player.Interactor.EquipmentInteractor interactor = FindObjectOfType<Player.Interactor.EquipmentInteractor>();
        if (interactor != null) interactor.ClearActiveComputer();
    }

    // --- TUTORIAL UI BUTTON HOOKS ---

    public void OnRecordingsFolderClicked()
    {
        // 1. Ask the bouncer if we are allowed to click this
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseComputerFeature("RecordingsFolder")) return;

        // 2. Tell the Tutorial Manager the task is complete FIRST!
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnRecordingsFolderOpened();

        // 3. Then open the grid view
        OpenGridView();
    }

    public void OnVideoPlayButtonClicked()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseComputerFeature("PlayVideo")) return;

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVideoPlayed();
    }

    public void OnBackButtonClicked()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseComputerFeature("BackButton")) return;

        // --- THE FIX: Hide the panels to return to the Desktop view! ---
        if (recordingsGridPanel != null) recordingsGridPanel.SetActive(false);
        if (videoPlayerPanel != null) videoPlayerPanel.SetActive(false);
        if (pixelPlayer != null) pixelPlayer.StopTape();

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnComputerBackClicked();
    }

    public void OnEditorAppClicked()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseComputerFeature("EditorApp")) return;

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnEditorAppClicked();
    }

    public RectTransform GetTutorialHighlightTarget(string targetName)
    {
        if (targetName == "VideoClip" && gridContentContainer != null)
        {
            for (int i = 0; i < gridContentContainer.childCount; i++)
            {
                RectTransform clipRect = gridContentContainer.GetChild(i) as RectTransform;
                if (clipRect != null && clipRect.gameObject.activeInHierarchy) return clipRect;
            }
        }

        string objectName = targetName == "Folder" ? "FolderLogo" : targetName;
        RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);

        foreach (RectTransform rectTransform in rectTransforms)
        {
            if (rectTransform.gameObject.activeInHierarchy && rectTransform.gameObject.name == objectName) return rectTransform;
        }

        return null;
    }

    public void SendToEditor()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseComputerFeature("ConfirmEditor")) return;

        if (ProjectDataManager.Instance == null)
        {
            Debug.LogError("CRASH PREVENTED: ProjectDataManager is missing from the Studio scene! Please create an empty GameObject and attach ProjectDataManager.cs to it.");
            return;
        }

        if (ProjectDataManager.Instance.compiledFootage == null)
        {
            ProjectDataManager.Instance.compiledFootage = new List<FootageData>();
        }

        if (physicalComputer == null) physicalComputer = FindObjectOfType<ComputerStation>();

        List<FootageData> files = physicalComputer != null ? physicalComputer.GetInsertedFiles() : null;
        List<FootageData> usableFiles = GetUsableTapeFiles(files);
        if (usableFiles.Count == 0)
        {
            const string warningMessage = "Insert at least one recorded SD Card before opening the Editor.";
            if (TutorialManager.Instance != null) TutorialManager.Instance.ShowTimedWarning(warningMessage, 3f);
            else Debug.LogWarning(warningMessage);
            return;
        }

        ProjectDataManager.Instance.ClearProject();
        foreach (FootageData data in usableFiles)
        {
            ProjectDataManager.Instance.compiledFootage.Add(data);
        }

        EvaluateStagePreProduction();

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnEditorConfirmed();
        UnityEngine.SceneManagement.SceneManager.LoadScene("Editor");
    }

    private List<FootageData> GetUsableTapeFiles(List<FootageData> files)
    {
        List<FootageData> usableFiles = new List<FootageData>();
        if (files == null) return usableFiles;

        foreach (FootageData data in files)
        {
            if (IsTapeFileUsable(data)) usableFiles.Add(data);
        }

        return usableFiles;
    }

    private bool IsTapeFileUsable(FootageData data)
    {
        if (data == null || string.IsNullOrEmpty(data.fileName)) return false;

        if (!string.Equals(Path.GetExtension(data.fileName), ".tape", System.StringComparison.OrdinalIgnoreCase)) return false;

        string fullPath = Path.Combine(Application.persistentDataPath, data.fileName);
        if (!File.Exists(fullPath)) return false;

        try
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (reader.BaseStream.Length < sizeof(int)) return false;

                int frameCount = reader.ReadInt32();
                if (frameCount <= 0) return false;

                for (int i = 0; i < frameCount; i++)
                {
                    if (reader.BaseStream.Position + sizeof(int) > reader.BaseStream.Length) return false;

                    int frameSize = reader.ReadInt32();
                    if (frameSize <= 0 || reader.BaseStream.Position + frameSize > reader.BaseStream.Length) return false;
                    reader.BaseStream.Seek(frameSize, SeekOrigin.Current);
                }

                return true;
            }
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Skipped unreadable tape " + data.fileName + ": " + exception.Message);
            return false;
        }
    }

    private void EvaluateStagePreProduction()
    {
        float score = 0f;
        string feedback = "<color=white><b>--- PRE-PRODUCTION ---</b></color>\n";
        ProjectDataManager.Instance.savedRequiredSetupMet = true;

        int currentLevel = CampaignProgression.GetCurrentLevel();
        DirectorTerminal stage = FindObjectOfType<DirectorTerminal>(true);
        RecordableSubject product = FindObjectOfType<RecordableSubject>(true);

        if (currentLevel == 1)
        {
            GradeLevel1Stage(stage, product, ref score, ref feedback);
        }
        else if (currentLevel == 2)
        {
            GradeLevel2Stage(stage, product, ref score, ref feedback);
        }
        else if (currentLevel == 3)
        {
            GradeLevel3Stage(ref score, ref feedback);
        }
        else if (currentLevel == 4)
        {
            GradeLevel4Stage(stage, ref score, ref feedback);
        }
        else
        {
            GradeLevel5Stage(stage, ref score, ref feedback);
        }

        ProjectDataManager.Instance.savedPreProdScore = Mathf.Clamp(score, 0f, 100f);
        ProjectDataManager.Instance.savedPreProdFeedback = feedback + "\n";
    }

    private void GradeLevel1Stage(DirectorTerminal stage, RecordableSubject product, ref float score, ref string feedback)
    {
        if (stage != null && stage.HasWall())
        {
            score += 25f;
            feedback += "<color=green>+ Backdrop placed.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Add a backdrop before recording.</color>\n";
        }

        if (product != null)
        {
            score += 25f;
            feedback += "<color=green>+ Product placed on the set.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- The flower vase is missing from the set.</color>\n";
        }

        if (stage != null && stage.HasWall() && stage.currentWallColor.r > 0.5f && stage.currentWallColor.g < 0.7f && stage.currentWallColor.b > 0.5f)
        {
            score += 50f;
            feedback += "<color=green>+ Good Pink Stage Design.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=yellow>- Backdrop needs to be Pink.</color>\n";
        }

        FilmLightItem[] lights = FindObjectsOfType<FilmLightItem>();
        if (!HasPoweredLight(lights))
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Power at least one production light before recording.</color>\n";
        }
    }

    private void GradeLevel2Stage(DirectorTerminal stage, RecordableSubject product, ref float score, ref string feedback)
    {
        GameObject wall = stage != null ? stage.GetCurrentWall() : null;

        if (wall != null)
        {
            score += 15f;
            feedback += "<color=green>+ Backdrop placed.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Add a backdrop for the Goke set.</color>\n";
        }

        if (product != null)
        {
            score += 20f;
            feedback += "<color=green>+ Goke product placed.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Goke Cola is missing from the set.</color>\n";
        }

        if (stage != null)
        {
            Color background = stage.currentWallColor;
            if (background.r > 0.5f && background.g < 0.3f && background.b < 0.3f)
            {
                score += 35f;
                feedback += "<color=green>+ Correct Red Backdrop.</color>\n";
            }
            else
            {
                MarkRequiredSetupMissing();
                feedback += "<color=yellow>- Wrong set color. The client requested RED.</color>\n";
            }
        }

        if (wall != null && product != null)
        {
            float depthDistance = GetDistanceFromObject(product.transform.position, wall);
            if (depthDistance >= 1.5f)
            {
                score += 30f;
                feedback += "<color=green>+ Great Stage Depth.</color>\n";
            }
            else
            {
                MarkRequiredSetupMissing();
                feedback += "<color=yellow>- Pull Goke Cola farther away from the backdrop.</color>\n";
            }
        }

        FilmLightItem[] lights = FindObjectsOfType<FilmLightItem>();
        if (GetPoweredLightCount(lights) < 3)
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Power the Key, Fill, and Back lights before recording.</color>\n";
        }
    }

    private void GradeLevel3Stage(ref float score, ref string feedback)
    {
        CubeVehicle[] vehicles = FindObjectsOfType<CubeVehicle>();
        FilmLightItem[] lights = FindObjectsOfType<FilmLightItem>();

        if (vehicles.Length == 1)
        {
            score += 35f;
            feedback += "<color=green>+ Lambormini vehicle placed.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += $"<color=red>- Place exactly one Lambormini vehicle. Found {vehicles.Length}.</color>\n";
        }

        FilmLightItem softLight = null;
        Vector3 vehicleCenter = vehicles.Length == 1 ? vehicles[0].transform.position : Vector3.zero;
        if (vehicles.Length == 1 && TryGetObjectBounds(vehicles[0].gameObject, out Bounds vehicleBounds)) vehicleCenter = vehicleBounds.center;

        foreach (FilmLightItem light in lights)
        {
            if (light != null && light.IsPoweredOn() && vehicles.Length == 1 &&
                (light.EquipmentName == "Level 3 Soft Light" || !light.forcesHardLight) &&
                Vector3.Distance(light.transform.position, vehicleCenter) <= 12f)
            {
                softLight = light;
                break;
            }
        }

        if (softLight != null)
        {
            score += 35f;
            feedback += "<color=green>+ Level 3 Soft Light is positioned on the set.</color>\n";

            if (softLight.intensityPercent >= 65f && softLight.intensityPercent <= 85f)
            {
                score += 15f;
                feedback += "<color=green>+ Soft Light intensity preserves reflective detail.</color>\n";
            }
            else
            {
                feedback += "<color=yellow>- Set the Soft Light between 65% and 85% for controlled highlights.</color>\n";
            }

            if (softLight.GetCurrentTilt() >= -20f && softLight.GetCurrentTilt() <= 0f)
            {
                score += 15f;
                feedback += "<color=green>+ Soft Light tilt shapes the vehicle cleanly.</color>\n";
            }
            else
            {
                feedback += "<color=yellow>- Keep Soft Light tilt between -20 and 0 degrees.</color>\n";
            }
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Bring the Level 3 Soft Light close to the vehicle.</color>\n";
        }
    }

    private void GradeLevel4Stage(DirectorTerminal stage, ref float score, ref string feedback)
    {
        GetCampaignProduct(4, out int productCount);
        CubeActor[] actors = FindObjectsOfType<CubeActor>();
        FilmLightItem[] lights = FindObjectsOfType<FilmLightItem>();

        if (stage != null && stage.HasWall())
        {
            score += 10f;
            feedback += "<color=green>+ Kape Kultura set and backdrop prepared.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Build a backdrop for the Kape Kultura daily-story set.</color>\n";
        }

        if (stage != null && stage.HasWall() && IsWarmBrown(stage.currentWallColor))
        {
            score += 20f;
            feedback += "<color=green>+ Warm brown backdrop supports the coffee story.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=yellow>- Change the backdrop to a warm brown color.</color>\n";
        }

        if (productCount == 1)
        {
            score += 20f;
            feedback += "<color=green>+ Kape Kultura product placed.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += $"<color=red>- Place exactly one Kape Kultura product. Found {productCount}.</color>\n";
        }

        if (actors.Length == 1)
        {
            score += 15f;
            feedback += "<color=green>+ One actor hired for the daily routine.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += $"<color=red>- Hire exactly one actor. Found {actors.Length}.</color>\n";
        }

        if (actors.Length == 1 && actors[0].GetPoseName() != "Neutral")
        {
            score += 10f;
            feedback += "<color=green>+ Actor performance pose prepared.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=yellow>- Select a clear action pose for the actor.</color>\n";
        }

        if (HasPoweredSoftLight(lights))
        {
            score += 25f;
            feedback += "<color=green>+ Level 3 Soft Light is powered and ready.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Use and power the Level 3 Soft Light for this contract.</color>\n";
        }
    }

    private void GradeLevel5Stage(DirectorTerminal stage, ref float score, ref string feedback)
    {
        GetCampaignProduct(5, out int productCount);
        CubeVehicle[] vehicles = FindObjectsOfType<CubeVehicle>();
        CubeActor[] actors = FindObjectsOfType<CubeActor>();

        if (stage != null && stage.HasWall())
        {
            score += 10f;
            feedback += "<color=green>+ Final campaign set prepared.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=red>- Build a complete backdrop for the Haraya campaign.</color>\n";
        }

        if (stage != null && stage.HasWall() && IsTeal(stage.currentWallColor))
        {
            score += 15f;
            feedback += "<color=green>+ Teal backdrop matches the Haraya campaign brief.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=yellow>- Change the backdrop to teal for the Haraya campaign.</color>\n";
        }

        if (productCount == 1)
        {
            score += 15f;
            feedback += "<color=green>+ Haraya campaign product placed.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += $"<color=red>- Place exactly one Haraya campaign product. Found {productCount}.</color>\n";
        }

        if (vehicles.Length == 1)
        {
            score += 15f;
            feedback += "<color=green>+ One campaign vehicle placed.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += $"<color=red>- Place exactly one campaign vehicle. Found {vehicles.Length}.</color>\n";
        }

        if (actors.Length == 1)
        {
            score += 10f;
            feedback += "<color=green>+ One campaign actor hired.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += $"<color=red>- Hire exactly one campaign actor. Found {actors.Length}.</color>\n";
        }

        if (actors.Length == 1 && actors[0].GetPoseName() != "Neutral")
        {
            score += 10f;
            feedback += "<color=green>+ Actor direction is prepared.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += "<color=yellow>- Direct the campaign actor into a non-neutral pose.</color>\n";
        }

        int poweredLightCount = GetPoweredLightCount(FindObjectsOfType<FilmLightItem>());
        if (poweredLightCount >= 3)
        {
            score += 25f;
            feedback += "<color=green>+ Three powered lights are prepared for the campaign.</color>\n";
        }
        else
        {
            MarkRequiredSetupMissing();
            feedback += $"<color=red>- Prepare three powered lights before recording. Found {poweredLightCount}.</color>\n";
        }
    }

    private CampaignProduct GetCampaignProduct(int campaignLevel, out int productCount)
    {
        CampaignProduct selectedProduct = null;
        productCount = 0;

        foreach (CampaignProduct product in FindObjectsOfType<CampaignProduct>())
        {
            if (product == null || product.campaignLevel != campaignLevel) continue;

            productCount++;
            if (selectedProduct == null) selectedProduct = product;
        }

        return selectedProduct;
    }

    private bool HasPoweredLight(FilmLightItem[] lights)
    {
        return GetPoweredLightCount(lights) > 0;
    }

    private bool IsWarmBrown(Color color)
    {
        return color.r >= 0.3f && color.r > color.g && color.g > color.b && color.b <= 0.4f;
    }

    private bool IsTeal(Color color)
    {
        return color.r <= 0.35f && color.g >= 0.35f && color.b >= 0.35f && color.g > color.r && color.b > color.r;
    }

    private void MarkRequiredSetupMissing()
    {
        if (ProjectDataManager.Instance != null) ProjectDataManager.Instance.savedRequiredSetupMet = false;
    }

    private int GetPoweredLightCount(FilmLightItem[] lights)
    {
        int poweredLightCount = 0;

        foreach (FilmLightItem light in lights)
        {
            if (light != null && light.IsPoweredOn()) poweredLightCount++;
        }

        return poweredLightCount;
    }

    private bool HasPoweredSoftLight(FilmLightItem[] lights)
    {
        foreach (FilmLightItem light in lights)
        {
            if (light != null && light.IsPoweredOn() &&
                (light.EquipmentName == "Level 3 Soft Light" || !light.forcesHardLight)) return true;
        }

        return false;
    }

    private float GetDistanceFromObject(Vector3 worldPosition, GameObject targetObject)
    {
        Collider targetCollider = targetObject.GetComponentInChildren<Collider>();
        if (targetCollider != null) return Vector3.Distance(worldPosition, targetCollider.ClosestPoint(worldPosition));

        Renderer targetRenderer = targetObject.GetComponentInChildren<Renderer>();
        if (targetRenderer != null) return Vector3.Distance(worldPosition, targetRenderer.bounds.ClosestPoint(worldPosition));
        return Vector3.Distance(worldPosition, targetObject.transform.position);
    }

    private bool AreSubjectsBesideEachOther(GameObject actor, GameObject vehicle)
    {
        if (!TryGetObjectBounds(actor, out Bounds actorBounds) || !TryGetObjectBounds(vehicle, out Bounds vehicleBounds)) return false;

        Vector2 actorPosition = new Vector2(actorBounds.center.x, actorBounds.center.z);
        Vector2 vehiclePosition = new Vector2(vehicleBounds.center.x, vehicleBounds.center.z);
        float horizontalDistance = Vector2.Distance(actorPosition, vehiclePosition);
        float floorDifference = Mathf.Abs(actorBounds.min.y - vehicleBounds.min.y);
        return horizontalDistance >= 1f && horizontalDistance <= 4.5f && floorDifference <= 0.75f;
    }

    private bool TryGetObjectBounds(GameObject targetObject, out Bounds objectBounds)
    {
        objectBounds = new Bounds();
        Renderer[] renderers = targetObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return false;

        objectBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) objectBounds.Encapsulate(renderers[i].bounds);
        return true;
    }
}
