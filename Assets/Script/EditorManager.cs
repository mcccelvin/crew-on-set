using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

public class EditorManager : MonoBehaviour
{
    public static EditorManager Instance;
    public int EditingLevel { get; private set; }
    private bool submissionStarted;
    public List<FootageData> RoomFootage { get; set; }
    public bool IsRoomEditor => RoomFootage != null;
    public bool ReviewIsOpen => submissionStarted || reviewVideoPanel != null && reviewVideoPanel.activeInHierarchy;

    [Header("UI References - Graphics Track")]
    public Transform[] brandingTracks;
    public GameObject brandClipPrefab;

    [Header("UI References - Phase Panels")]
    public Transform clipBankContainer;
    public GameObject brandingBinPanel;
    public GameObject colorGradingBin;
    public GameObject clipPrefab;
    public Transform timelineContainer;

    [Header("UI References - Navigation (Tabs)")]
    public Image[] tabButtonImages;
    public Color activeTabColor = new Color(1f, 1f, 1f, 1f);
    public Color inactiveTabColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public GameObject exportButton;

    [Header("Color Grading References")]
    public ColorGradingManager gradingManager;
    public ContractGrader grader;

    [Header("UI References - Exporting")]
    public GameObject reviewVideoPanel;
    public TruePixelPlayer exportPlayer;
    public GameObject finalGradePanel;

    [Header("Premiere Settings")]
    public float pixelsPerSecond = 40f;

    private int currentPhase = 0;
    private int cheatClipCounter = 0;

    private Button reviewBackButton;
    private List<Texture2D> generatedThumbnails = new List<Texture2D>();
    private Material exportMaterial;
    private float pendingCam = 0f;
    private float pendingLight = 0f;
    private float pendingSec = 0f;
    [SerializeField] private GameObject titleSafeGuide;
    private PlayerEditTools playerEditTools;
    private Coroutine phaseRevealCoroutine;
    [SerializeField] private TMP_Text emptyPreviewMessage;

    private void Awake() { Instance = this; EditingLevel = CampaignProgression.GetCurrentLevel(); }

    private void Start()
    {
        PauseManager.isPaused = false;
        DraggableClip.ResetSplitHistory();
        if (GetComponent<EditorUndoHistory>() == null) gameObject.AddComponent<EditorUndoHistory>();
        if (!IsRoomEditor) PauseManager.EnsureEditorPause();
        if (!IsRoomEditor)
        {
        var contractReference = ContractUIManager.Instance;
        if (contractReference == null)
            contractReference = new GameObject("Editor contract reference").AddComponent<ContractUIManager>();
        contractReference.ConfigureEditorReference();
        }
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentPhase = 0;
        activeTabColor = EditorWorkspaceUI.Accent;
        inactiveTabColor = EditorWorkspaceUI.Control;
        BuildProfessionalPreview();
        BuildReviewBackButton();
        SetupPlayerEditTools();
        // Keep the clip bank sprite, CLIPS heading, and typography authored in the scene.
        EditorWorkspaceUI.Surface(brandingBinPanel != null ? brandingBinPanel.transform : null);
        EditorWorkspaceUI.Surface(colorGradingBin != null ? colorGradingBin.transform : null);
        UpdatePhaseUI();
        ConfigureClipBank();
        LoadClipsFromBridge();
        if (CampaignProgression.GetCurrentLevel() == 2)
        {
            AddProvidedGokeClip("GokeIntro", "GOKE INTRO", ProvidedClipRole.GokeIntro);
            AddProvidedGokeClip("GokeOutro", "GOKE OUTRO", ProvidedClipRole.GokeOutro);
            if (!IsRoomEditor) gameObject.AddComponent<GokeIntroOutroLesson>();
        }
        else if (CampaignProgression.GetCurrentLevel() == 3)
        {
            AddGeneratedTerrariClip("TerrariIntro", "TERRARI INTRO", ProvidedClipRole.TerrariIntro);
            AddGeneratedTerrariClip("TerrariOutro", "TERRARI OUTRO", ProvidedClipRole.TerrariOutro);
            // Terrari editing is independent; supplied clips remain available without a lesson.
        }
        else if (EditingLevel == 4 && !IsRoomEditor) gameObject.AddComponent<CoffeeStoryEditLesson>();
    }

    private void Update()
    {
        if (PauseManager.isPaused) return;
        if (emptyPreviewMessage != null && gradingManager != null && gradingManager.computerScreen != null)
        {
            Texture texture = gradingManager.computerScreen.texture;
            emptyPreviewMessage.gameObject.SetActive(texture == null || texture == Texture2D.blackTexture);
        }
    }

    public void GenerateCheatClip()
    {
        if (!DevCommandsPanel.CommandsAllowed || IsRoomEditor) return;
        // --- MODIFIED: Generates 12 seconds! ---
        Debug.Log("<color=red>DEV COMMAND: Generating 12-second fake clip for testing!</color>");
        cheatClipCounter++;
        string fileName = $"DEV_CheatClip_{cheatClipCounter}.tape";
        string fullPath = Path.Combine(Application.persistentDataPath, fileName);

        Texture2D dummyTex = new Texture2D(64, 64);
        Color randomColor = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);

        for (int x = 0; x < 64; x++)
        {
            for (int y = 0; y < 64; y++) dummyTex.SetPixel(x, y, randomColor);
        }
        dummyTex.Apply();
        byte[] jpgData = dummyTex.EncodeToJPG(50);

        // --- 12 seconds at the shared tape frame rate ---
        int totalFrames = Mathf.RoundToInt(12f * TapeSettings.framesPerSecond);

        using (BinaryWriter writer = new BinaryWriter(File.Open(fullPath, FileMode.Create)))
        {
            writer.Write(totalFrames);
            for (int i = 0; i < totalFrames; i++)
            {
                writer.Write(jpgData.Length);
                writer.Write(jpgData);
            }
        }
        Destroy(dummyTex);

        if (clipPrefab != null && clipBankContainer != null)
        {
            GameObject newClip = Instantiate(clipPrefab, clipBankContainer);
            DraggableClip dragScript = newClip.GetComponent<DraggableClip>();

            if (dragScript != null)
            {
                dragScript.clipFilePath = fullPath;
                dragScript.cameraScore = UnityEngine.Random.Range(50f, 100f);
                dragScript.lightScore = UnityEngine.Random.Range(50f, 100f);
                dragScript.campaignLevel = CampaignProgression.GetCurrentLevel();
                dragScript.shotType = 2;
                dragScript.screenDirection = 0f;
                dragScript.actorPose = "Neutral";
                dragScript.requiredSubjectsVisible = true;
                dragScript.usedSoftLight = dragScript.campaignLevel >= 3;
                dragScript.hasThreePointRoles = false;
            }

            TextMeshProUGUI clipText = newClip.GetComponentInChildren<TextMeshProUGUI>();
            LoadThumbnail(fullPath, newClip, dragScript, Path.GetFileNameWithoutExtension(fileName), clipText);
        }
    }

    public void ImportRoomFootage(List<FootageData> footage)
    {
        if (!IsRoomEditor) return;
        RoomFootage = footage;
        LoadClipsFromBridge();
    }

    private void LoadClipsFromBridge()
    {
        var footage = RoomFootage ?? ProjectDataManager.Instance?.compiledFootage;
        if (footage == null || clipPrefab == null || clipBankContainer == null) return;

        foreach (var data in footage)
        {
            if (data == null || string.IsNullOrEmpty(data.fileName)) continue;

            string fullPath = Path.Combine(Application.persistentDataPath, data.fileName);
            if (IsRoomEditor && System.Array.Exists(clipBankContainer.GetComponentsInChildren<DraggableClip>(true), c => c.clipFilePath == fullPath)) continue;
            if (!IsReadableTape(fullPath))
            {
                Debug.LogWarning("Editor skipped missing or unreadable footage: " + data.fileName);
                continue;
            }

            GameObject newClip = Instantiate(clipPrefab, clipBankContainer);

            DraggableClip dragScript = newClip.GetComponent<DraggableClip>();
            if (dragScript != null)
            {
                dragScript.clipFilePath = fullPath;
                dragScript.cameraScore = data.camScore;
                dragScript.lightScore = data.lightScore;
                dragScript.campaignLevel = data.campaignLevel;
                dragScript.shotType = data.shotType;
                dragScript.screenDirection = data.screenDirection;
                dragScript.actorPose = data.actorPose;
                dragScript.requiredSubjectsVisible = data.requiredSubjectsVisible;
                dragScript.usedSoftLight = data.usedSoftLight;
                dragScript.hasThreePointRoles = data.hasThreePointRoles;
            }

            TextMeshProUGUI clipText = newClip.GetComponentInChildren<TextMeshProUGUI>();
            string displayName = Path.GetFileNameWithoutExtension(data.fileName);
            if (data.campaignLevel >= 4) displayName += "\n[" + GetShotTypeName(data.shotType) + "]";

            LoadThumbnail(fullPath, newClip, dragScript, displayName, clipText);
        }
    }

#if UNITY_EDITOR
    public void BakeHierarchyUI()
    {
        ConfigureClipBank();
        BuildProfessionalPreview();
        SetupPlayerEditTools();
    }
#endif

    private void ConfigureClipBank()
    {
        var content = clipBankContainer as RectTransform;
        if (content == null || content.parent == null) return;
        if (content.parent.GetComponent<ScrollRect>() != null && content.parent.GetComponent<ScrollRect>().content == content) return;
        Canvas.ForceUpdateCanvases();
        float minimumHeight = content.rect.height;
        var viewportObject = new GameObject("Clip Bank Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.layer = content.gameObject.layer;
        var viewport = (RectTransform)viewportObject.transform;
        viewport.SetParent(content.parent, false);
        viewport.SetSiblingIndex(content.GetSiblingIndex());
        viewport.anchorMin = content.anchorMin;
        viewport.anchorMax = content.anchorMax;
        viewport.pivot = content.pivot;
        viewport.sizeDelta = content.sizeDelta;
        viewport.anchoredPosition = content.anchoredPosition;
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = new Vector2(0f, minimumHeight);
        content.anchoredPosition = Vector2.zero;
        var fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var layout = content.GetComponent<LayoutElement>();
        if (layout == null) layout = content.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = minimumHeight;
        var scroll = viewportObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;
    }

    private void AddProvidedGokeClip(string resource, string label, ProvidedClipRole role)
    {
        if (clipPrefab == null || clipBankContainer == null) return;
        TextAsset tape = Resources.Load<TextAsset>(resource);
        if (tape == null)
        {
            ShowEditorWarning("The supplied Goke clip is missing: " + label);
            return;
        }
        string folder = Path.Combine(Application.persistentDataPath, "ProvidedGoke");
        string path = Path.Combine(folder, resource + ".tape");
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(path, tape.bytes);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Could not prepare " + label + ": " + e.Message);
            return;
        }
        GameObject clip = Instantiate(clipPrefab, clipBankContainer);
        clip.name = label;
        DraggableClip data = clip.GetComponent<DraggableClip>();
        data.clipFilePath = path;
        data.campaignLevel = 2;
        data.providedRole = role;
        LoadThumbnail(path, clip, data, label, clip.GetComponentInChildren<TextMeshProUGUI>());
        // Bank thumbnails remain readable; dropping uses the actual 2-second width.
        var rect = (RectTransform)clip.transform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(200f, rect.rect.width));
        var layout = clip.GetComponent<LayoutElement>();
        if (layout != null) layout.preferredWidth = rect.rect.width;
    }

    private void AddGeneratedTerrariClip(string resource, string label, ProvidedClipRole role)
    {
        if (clipPrefab == null || clipBankContainer == null) return;
        string folder = Path.Combine(Application.persistentDataPath, "ProvidedTerrari");
        string path = Path.Combine(folder, resource + ".tape");
        try
        {
            Directory.CreateDirectory(folder);
            Texture2D card = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            Color32[] pixels = new Color32[1920 * 1080];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 0, 255);
            PaintTerrariArt(pixels, ExportUIArt.GetWhite("terrariMark"), new Rect(780, 430, 360, 400));
            PaintTerrariArt(pixels, ExportUIArt.GetWhite("terrariWordmark"), new Rect(660, 260, 600, 136));
            card.SetPixels32(pixels);
            card.Apply();
            byte[] jpg = card.EncodeToJPG(95);
            Destroy(card);
            int frames = Mathf.RoundToInt(LamborminiBrief.CardSeconds * TapeSettings.framesPerSecond);
            using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
            {
                writer.Write(frames);
                for (int i = 0; i < frames; i++) { writer.Write(jpg.Length); writer.Write(jpg); }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Could not prepare " + label + ": " + e.Message);
            return;
        }
        GameObject clip = Instantiate(clipPrefab, clipBankContainer);
        clip.name = label;
        DraggableClip data = clip.GetComponent<DraggableClip>();
        data.clipFilePath = path;
        data.campaignLevel = 3;
        data.providedRole = role;
        LoadThumbnail(path, clip, data, label, clip.GetComponentInChildren<TextMeshProUGUI>());
        var rect = (RectTransform)clip.transform;
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(200f, rect.rect.width));
        var layout = clip.GetComponent<LayoutElement>();
        if (layout != null) layout.preferredWidth = rect.rect.width;
    }

    private static void PaintTerrariArt(Color32[] card, Sprite sprite, Rect bounds)
    {
        if (sprite == null) throw new System.InvalidOperationException("Terrari title artwork is missing.");
        Texture2D texture = sprite.texture;
        float scale = Mathf.Min(bounds.width / texture.width, bounds.height / texture.height);
        int width = Mathf.Max(1, Mathf.RoundToInt(texture.width * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(texture.height * scale));
        int left = Mathf.RoundToInt(bounds.center.x - width * .5f);
        int bottom = Mathf.RoundToInt(bounds.center.y - height * .5f);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            byte value = (byte)Mathf.RoundToInt(texture.GetPixelBilinear((x + .5f) / width, (y + .5f) / height).a * 255f);
            card[(bottom + y) * 1920 + left + x] = new Color32(value, value, value, 255);
        }
    }

    private bool IsReadableTape(string path)
    {
        if (!File.Exists(path)) return false;

        try
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                return reader.BaseStream.Length >= sizeof(int) && reader.ReadInt32() > 0;
            }
        }
        catch (System.Exception)
        {
            return false;
        }
    }

    private string GetShotTypeName(int shotType)
    {
        if (shotType == 1) return "WIDE";
        if (shotType == 3) return "CLOSE-UP";
        return "MEDIUM";
    }

    private void LoadThumbnail(string path, GameObject clipObj, DraggableClip script, string name, TextMeshProUGUI text)
    {
        try
        {
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                int frameCount = reader.ReadInt32();
                float duration = frameCount / TapeSettings.framesPerSecond;

                if (script != null)
                {
                    script.totalFrames = frameCount;
                    script.startFrame = 0;
                    script.endFrame = frameCount;
                }

                float trueWidth = duration * pixelsPerSecond;
                LayoutElement layout = clipObj.GetComponent<LayoutElement>();
                if (layout != null) layout.preferredWidth = trueWidth;

                RectTransform rect = clipObj.GetComponent<RectTransform>();
                if (rect != null) rect.sizeDelta = new Vector2(trueWidth, rect.sizeDelta.y);

                if (frameCount > 0)
                {
                    int frameSize = reader.ReadInt32();
                    byte[] frameBytes = reader.ReadBytes(frameSize);
                    Texture2D thumbTex = new Texture2D(2, 2);
                    thumbTex.LoadImage(frameBytes, true);
                    generatedThumbnails.Add(thumbTex);

                    RawImage thumbUI = clipObj.GetComponentInChildren<RawImage>();
                    if (thumbUI != null) thumbUI.texture = thumbTex;
                }

                if (text != null) text.text = $" {name}\n <size=70%>{duration:F1}s</size>";
            }
        }
        catch (System.Exception e) { Debug.LogError("Thumbnail Error: " + e.Message); }
    }

    public void GoToVideoEditing() { currentPhase = 0; UpdatePhaseUI(); NotifyTutorial(); }
    public void GoToBranding() { currentPhase = 1; UpdatePhaseUI(); NotifyTutorial(); }
    public void GoToColorGrading() { currentPhase = 2; UpdatePhaseUI(); NotifyTutorial(); }

    private void NotifyTutorial() { if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnPhaseChanged(currentPhase); }

    private void UpdatePhaseUI()
    {
        if (clipBankContainer != null) SetPhaseVisible(clipBankContainer.gameObject, currentPhase == 0);
        SetPhaseVisible(brandingBinPanel, currentPhase == 1);
        SetPhaseVisible(colorGradingBin, currentPhase == 2);
        if (exportButton != null) exportButton.SetActive(currentPhase == 2 || CampaignProgression.GetCurrentLevel() == 2 || EditingLevel == 4);
        if (titleSafeGuide != null) titleSafeGuide.SetActive(currentPhase == 1 || currentPhase == 2);
        if (playerEditTools != null) playerEditTools.SetVisible(currentPhase == 1);

        GameObject activePanel = currentPhase == 0
            ? (clipBankContainer != null ? clipBankContainer.gameObject : null)
            : currentPhase == 1 ? brandingBinPanel : colorGradingBin;
        if (activePanel != null)
        {
            if (phaseRevealCoroutine != null) StopCoroutine(phaseRevealCoroutine);
            phaseRevealCoroutine = null;
            var group = activePanel.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }

        if (tabButtonImages != null && tabButtonImages.Length > 0)
        {
            for (int i = 0; i < tabButtonImages.Length; i++)
            {
                if (tabButtonImages[i] != null)
                {
                    var motion = tabButtonImages[i].GetComponent<UIButtonFeedback>();
                    if (motion != null) motion.enabled = false;
                    tabButtonImages[i].color = (i == currentPhase) ? EditorWorkspaceUI.Accent : EditorWorkspaceUI.Control;
                }
            }
        }
    }

    private static void SetPhaseVisible(GameObject panel, bool visible)
    {
        if (panel == null) return;
        var motion = panel.GetComponent<UITransition>();
        if (motion != null) motion.enabled = false;
        panel.SetActive(visible);
        var group = panel.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;
    }

    private void BuildProfessionalPreview()
    {
        if (emptyPreviewMessage != null && titleSafeGuide != null)
        {
            var label = emptyPreviewMessage.transform.parent.Find("Program Monitor Header/Program Monitor Text");
            if (label != null) label.GetComponent<TextMeshProUGUI>().text = "<b>PROGRAM MONITOR</b>     LEVEL " + CampaignProgression.GetCurrentLevel() + "     1920 × 1080     TITLE SAFE";
            return;
        }
        if (gradingManager == null || gradingManager.computerScreen == null) return;

        RectTransform screenRect = gradingManager.computerScreen.rectTransform;
        if (gradingManager.computerScreen.texture == null)
            gradingManager.computerScreen.texture = Texture2D.blackTexture;
        var emptyObject = new GameObject("Empty Preview Message", typeof(RectTransform), typeof(TextMeshProUGUI));
        emptyObject.transform.SetParent(screenRect, false);
        emptyPreviewMessage = emptyObject.GetComponent<TextMeshProUGUI>();
        emptyPreviewMessage.text = "NO FOOTAGE\n<size=65%>Record a take in the studio, then add a clip to the timeline.</size>";
        emptyPreviewMessage.fontSize = 28;
        emptyPreviewMessage.color = Color.white;
        emptyPreviewMessage.alignment = TextAlignmentOptions.Center;
        emptyPreviewMessage.raycastTarget = false;
        StretchPreviewRect(emptyPreviewMessage.rectTransform, Vector2.zero, Vector2.one, new Vector2(25, 35), new Vector2(-25, -35));
        int currentLevel = CampaignProgression.GetCurrentLevel();

        GameObject headerObject = CreatePreviewImage("Program Monitor Header", screenRect, EditorWorkspaceUI.Control);
        RectTransform headerRect = headerObject.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 38f);

        GameObject headerTextObject = new GameObject("Program Monitor Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        headerTextObject.layer = screenRect.gameObject.layer;
        headerTextObject.transform.SetParent(headerObject.transform, false);

        TextMeshProUGUI headerText = headerTextObject.GetComponent<TextMeshProUGUI>();
        headerText.text = "<b>PROGRAM MONITOR</b>     LEVEL " + currentLevel + "     1920 × 1080     TITLE SAFE";
        headerText.fontSize = 19f;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = Color.white;
        headerText.raycastTarget = false;
        StretchPreviewRect(headerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -2f));

        titleSafeGuide = new GameObject("Title Safe Guide", typeof(RectTransform));
        titleSafeGuide.layer = screenRect.gameObject.layer;
        titleSafeGuide.transform.SetParent(screenRect, false);
        StretchPreviewRect(titleSafeGuide.GetComponent<RectTransform>(), new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.94f), Vector2.zero, Vector2.zero);

        CreateSafeLine("Top", titleSafeGuide.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1f), new Vector2(0f, 2f));
        CreateSafeLine("Bottom", titleSafeGuide.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(0f, 2f));
        CreateSafeLine("Left", titleSafeGuide.transform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(2f, 0f));
        CreateSafeLine("Right", titleSafeGuide.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-1f, 0f), new Vector2(2f, 0f));
    }

    private void SetupPlayerEditTools()
    {
        if (brandingBinPanel == null) return;

        playerEditTools = GetComponent<PlayerEditTools>();
        if (playerEditTools == null) playerEditTools = gameObject.AddComponent<PlayerEditTools>();
        playerEditTools.Initialize(brandingBinPanel);
    }

    private GameObject CreatePreviewImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return imageObject;
    }

    private void CreateSafeLine(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        GameObject lineObject = CreatePreviewImage(objectName, parent, new Color(0.35f, 0.85f, 1f, 0.48f));
        RectTransform lineRect = lineObject.GetComponent<RectTransform>();
        lineRect.anchorMin = anchorMin;
        lineRect.anchorMax = anchorMax;
        lineRect.anchoredPosition = anchoredPosition;
        lineRect.sizeDelta = sizeDelta;
    }

    private void StretchPreviewRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    public void ExportCommercial()
    {
        var lesson=EditorTutorialManager.Instance;
        if(lesson!=null&&lesson.gameObject.activeInHierarchy&&
            (lesson.currentStep==EditorTutorialManager.EditorStep.ExplainColorGrading||
             lesson.currentStep==EditorTutorialManager.EditorStep.AdjustBrightness||
             lesson.currentStep==EditorTutorialManager.EditorStep.AdjustContrast||
             lesson.currentStep==EditorTutorialManager.EditorStep.AdjustSaturation||
             lesson.currentStep==EditorTutorialManager.EditorStep.ExplainColorSettings))
        {
            ShowEditorWarning("Finish trying brightness, contrast and saturation with the boss before exporting.");
            return;
        }
        if (timelineContainer == null || exportPlayer == null)
        {
            ShowEditorWarning("The export system is not ready. Please check the Editor setup and try again.");
            return;
        }

        float totalCam = 0, totalLight = 0, totalSeconds = 0, recordedSeconds = 0;
        DraggableClip[] clips = timelineContainer.GetComponentsInChildren<DraggableClip>();
        if (clips.Length == 0)
        {
            ShowEditorWarning("Place at least one recorded clip on the timeline before exporting.");
            return;
        }

        List<ClipSegment> sequence = new List<ClipSegment>();

        List<DraggableClip> sortedClips = new List<DraggableClip>(clips);
        sortedClips.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));

        foreach (var clip in sortedClips)
        {
            if (clip == null || !IsReadableTape(clip.clipFilePath) || clip.endFrame <= clip.startFrame) continue;

            float clipSeconds = Mathf.Max(0f, (clip.endFrame - clip.startFrame) / TapeSettings.framesPerSecond);
            if (clip.providedRole == ProvidedClipRole.None)
            {
                totalCam += clip.cameraScore * clipSeconds;
                totalLight += clip.lightScore * clipSeconds;
                recordedSeconds += clipSeconds;
            }
            totalSeconds += clipSeconds;

            RectTransform rt = clip.GetComponent<RectTransform>();
            float trueStartX = rt.anchoredPosition.x - (rt.rect.width * rt.pivot.x);

            sequence.Add(new ClipSegment
            {
                path = clip.clipFilePath,
                startFrame = clip.startFrame,
                endFrame = clip.endFrame,
                useClipGrade = EditingLevel == 4,
                brightness = clip.gradeBrightness, contrast = clip.gradeContrast, saturation = clip.gradeSaturation,
                uiStartX = trueStartX,
                uiWidth = rt.rect.width
            });
        }

        if (recordedSeconds > 0f)
        {
            totalCam /= recordedSeconds;
            totalLight /= recordedSeconds;
        }

        if (sequence.Count == 0 || recordedSeconds <= 0f)
        {
            ShowEditorWarning("The timeline does not contain readable footage. Return to the studio and record a new clip.");
            return;
        }

        bool hasFadeIn = false;
        if (gradingManager != null && gradingManager.fadeInToggle != null) hasFadeIn = gradingManager.fadeInToggle.isOn;

        pendingCam = totalCam;
        pendingLight = totalLight;
        pendingSec = totalSeconds;

        if (PlayFinalReview(sequence, hasFadeIn) && EditorTutorialManager.Instance != null)
        {
            EditorTutorialManager.Instance.OnExportClicked();
        }
    }

    private bool PlayFinalReview(List<ClipSegment> sequence, bool hasFadeIn)
    {
        if (reviewVideoPanel == null || exportPlayer == null)
        {
            Debug.LogError("EXPORT FAILED: Review panel or Export Player is missing!");
            return false;
        }

        UITransition.Show(reviewVideoPanel);
        BuildReviewBackButton();

        CommercialCompiler compiler = FindObjectOfType<CommercialCompiler>();
        if (compiler != null && compiler.editorPlayer != null) compiler.editorPlayer.StopTape();

        if (exportPlayer != null && exportPlayer.computerScreen != null)
        {
            if (gradingManager != null && gradingManager.computerScreen != null)
            {
                if (exportMaterial != null) Destroy(exportMaterial);
                exportMaterial = new Material(gradingManager.computerScreen.material);
                // Before/After only changes the monitor, never the delivered grade.
                if (gradingManager.brightnessSlider != null) exportMaterial.SetFloat("_Brightness", gradingManager.brightnessSlider.value);
                if (gradingManager.contrastSlider != null) exportMaterial.SetFloat("_Contrast", gradingManager.contrastSlider.value);
                if (gradingManager.saturationSlider != null) exportMaterial.SetFloat("_Saturation", gradingManager.saturationSlider.value);
                exportPlayer.computerScreen.material = exportMaterial;
            }

            exportPlayer.SetOverlaySource(gradingManager != null ? gradingManager.computerScreen : null);
        }

        exportPlayer.PlaySequence(sequence, hasFadeIn);
        return true;
    }

    public bool IsReviewBackControl(Transform item) => reviewBackButton != null &&
        (item == reviewBackButton.transform || item.IsChildOf(reviewBackButton.transform));
    private void BuildReviewBackButton()
    {
        if (reviewBackButton != null || reviewVideoPanel == null) return;
        var root = new GameObject("Back to edit", typeof(RectTransform), typeof(Image), typeof(Button));
        root.layer = reviewVideoPanel.layer;
        root.transform.SetParent(reviewVideoPanel.transform, false);
        reviewBackButton = root.GetComponent<Button>(); reviewBackButton.targetGraphic = root.GetComponent<Image>();
        reviewBackButton.onClick.AddListener(BackToEditor);
        StyleReviewButton(reviewBackButton, "BACK", false);

        // The old backdrop contains a gray picture placeholder. Do not let its
        // edges peek around the real video at fractional Canvas scales.
        var background = reviewVideoPanel.GetComponent<Image>();
        if (background != null) { background.sprite = null; background.color = new Color(.075f,.08f,.095f,1); }
        var submit = reviewVideoPanel.transform.Find("Submit");
        if (submit != null && submit.TryGetComponent<Button>(out var submitButton))
            StyleReviewButton(submitButton, "SUBMIT", true);

        var title = ReviewLabel(reviewVideoPanel.transform, "Preview heading", "FINAL PREVIEW", 24);
        title.color = new Color(.76f,.8f,.86f,1); title.characterSpacing = 4;
        title.rectTransform.anchorMin = new Vector2(0,1); title.rectTransform.anchorMax = Vector2.one;
        title.rectTransform.pivot = new Vector2(.5f,1);
        title.rectTransform.offsetMin = new Vector2(230,-78); title.rectTransform.offsetMax = new Vector2(-230,-24);

        if (exportPlayer != null && exportPlayer.computerScreen != null)
        {
            var viewport = new GameObject("Review picture area", typeof(RectTransform), typeof(Image));
            viewport.layer = reviewVideoPanel.layer; viewport.transform.SetParent(reviewVideoPanel.transform,false);
            viewport.GetComponent<Image>().color = Color.black; viewport.GetComponent<Image>().raycastTarget = false;
            var area = viewport.GetComponent<RectTransform>();
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(32,112); area.offsetMax = new Vector2(-32,-100);
            viewport.transform.SetAsFirstSibling();
            var screen = exportPlayer.computerScreen;
            screen.transform.SetParent(area,false);
            screen.rectTransform.localScale = Vector3.one;
            screen.raycastTarget = false;
            var fit = screen.GetComponent<AspectRatioFitter>() ?? screen.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f/9f;
        }
    }
    private static void StyleReviewButton(Button button, string caption, bool right)
    {
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0,1);
        rect.anchoredPosition = new Vector2(right ? -32 : 32,-24); rect.sizeDelta = new Vector2(176,54);
        var image = button.GetComponent<Image>();
        if (image != null)
        {
            CrewPaperStyle.Round(image);
            image.color = right ? new Color32(48,97,124,255) : new Color32(35,44,53,255);
            image.raycastTarget = true; button.targetGraphic = image;
            foreach (var effect in image.GetComponents<Shadow>()) effect.enabled = false;
            var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            outline.enabled = true;
            outline.effectColor = right ? new Color32(106,180,209,255) : new Color32(90,111,126,255);
            outline.effectDistance = new Vector2(1,-1);
        }
        foreach (var oldLabel in button.GetComponentsInChildren<TMP_Text>(true)) oldLabel.gameObject.SetActive(false);
        foreach (var oldLabel in button.GetComponentsInChildren<Text>(true)) oldLabel.gameObject.SetActive(false);
        var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.18f,1.18f,1.18f);
        colors.pressedColor = new Color(.75f,.82f,.88f); colors.selectedColor = colors.highlightedColor; colors.fadeDuration = .12f;
        button.colors = colors; button.transition = Selectable.Transition.ColorTint;
        var text = ReviewLabel(button.transform,"Review button label",caption,25);
        text.color = new Color32(230,240,246,255); text.fontStyle = FontStyles.Bold;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(12,6); text.rectTransform.offsetMax = new Vector2(-12,-6);
    }
    private static TextMeshProUGUI ReviewLabel(Transform parent, string name, string caption, float size)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        label.layer = parent.gameObject.layer; label.transform.SetParent(parent,false);
        var text = label.GetComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset;
        text.text = caption; text.color = Color.white; text.alignment = TextAlignmentOptions.Center;
        text.fontSize = text.fontSizeMax = size; text.fontSizeMin = 18; text.enableAutoSizing = true;
        text.enableWordWrapping = false; text.raycastTarget = false;
        return text;
    }
    public void BackToEditor()
    {
        if (submissionStarted || reviewVideoPanel == null || !reviewVideoPanel.activeInHierarchy || PauseManager.isPaused) return;
        if (exportPlayer != null) exportPlayer.StopTape();
        reviewVideoPanel.SetActive(false);
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnReviewBack();
        var compiler = FindObjectOfType<CommercialCompiler>();
        if (compiler != null && compiler.editorPlayer != null) compiler.editorPlayer.RefreshPlayerCreatedEffects();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    public void SubmitVideo()
    {
        if (submissionStarted || reviewVideoPanel == null || !reviewVideoPanel.activeInHierarchy || exportPlayer == null || !exportPlayer.CanStartPlayback) return;
        if (IsRoomEditor)
        {
            MultiplayerAuthoredUI.Instance?.SubmitSharedCommercial(pendingCam, pendingLight, pendingSec);
            return;
        }
        if (grader == null)
        {
            ShowEditorWarning("The grading system is not ready. Your commercial has not been submitted.");
            return;
        }

        submissionStarted=true;
        CrossSceneData.finalGrades = grader.GenerateGrades(pendingCam, pendingLight, pendingSec);
        CrossSceneData.submittedWithTutorial = EditorTutorialManager.Instance != null
            && EditorTutorialManager.Instance.isActiveAndEnabled && EditorTutorialManager.Instance.RestrictsEditor
            && EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.ReviewAndSubmit;
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnVideoSubmitted();

        if (reviewVideoPanel != null) reviewVideoPanel.SetActive(false);
        if (exportPlayer != null) exportPlayer.StopTape();

        LoadingScreenController.LoadScene("ReviewScene");
    }

    private void ShowEditorWarning(string message)
    {
        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.ShowWarning(message);
        }
        else
        {
            Debug.LogWarning(message);
        }
    }

    private void OnDestroy()
    {
        foreach (Texture2D thumbnail in generatedThumbnails)
        {
            if (thumbnail != null) Destroy(thumbnail);
        }

        if (exportMaterial != null) Destroy(exportMaterial);
        if (Instance == this) Instance = null;
    }
}
