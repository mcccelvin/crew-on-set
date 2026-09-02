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

    private List<GameObject> clonedLogos = new List<GameObject>();
    private List<Texture2D> generatedThumbnails = new List<Texture2D>();
    private Material exportMaterial;
    private float pendingCam = 0f;
    private float pendingLight = 0f;
    private float pendingSec = 0f;
    private GameObject titleSafeGuide;
    private PlayerEditTools playerEditTools;

    private void Awake() { Instance = this; }

    private void Start()
    {
        PauseManager.isPaused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentPhase = 0;
        BuildProfessionalPreview();
        SetupPlayerEditTools();
        UpdatePhaseUI();
        LoadClipsFromBridge();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f11Key.wasPressedThisFrame) GenerateCheatClip();
    }

    private void GenerateCheatClip()
    {
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

    private void LoadClipsFromBridge()
    {
        if (ProjectDataManager.Instance == null || ProjectDataManager.Instance.compiledFootage == null || clipPrefab == null || clipBankContainer == null) return;

        foreach (var data in ProjectDataManager.Instance.compiledFootage)
        {
            if (data == null || string.IsNullOrEmpty(data.fileName)) continue;

            string fullPath = Path.Combine(Application.persistentDataPath, data.fileName);
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
        if (clipBankContainer != null) clipBankContainer.gameObject.SetActive(currentPhase == 0);
        if (brandingBinPanel != null) brandingBinPanel.SetActive(currentPhase == 1);
        if (colorGradingBin != null) colorGradingBin.SetActive(currentPhase == 2);
        if (exportButton != null) exportButton.SetActive(currentPhase == 2);
        if (titleSafeGuide != null) titleSafeGuide.SetActive(currentPhase == 1 || currentPhase == 2);
        if (playerEditTools != null) playerEditTools.SetVisible(currentPhase == 1);

        if (tabButtonImages != null && tabButtonImages.Length > 0)
        {
            for (int i = 0; i < tabButtonImages.Length; i++)
            {
                if (tabButtonImages[i] != null)
                {
                    tabButtonImages[i].color = (i == currentPhase) ? new Color(0.12f, 0.62f, 0.92f, 1f) : new Color(0.09f, 0.12f, 0.16f, 1f);
                }
            }
        }
    }

    private void BuildProfessionalPreview()
    {
        if (gradingManager == null || gradingManager.computerScreen == null) return;

        RectTransform screenRect = gradingManager.computerScreen.rectTransform;
        int currentLevel = CampaignProgression.GetCurrentLevel();

        GameObject headerObject = CreatePreviewImage("Program Monitor Header", screenRect, new Color(0.02f, 0.035f, 0.055f, 0.92f));
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
        headerText.color = new Color(0.78f, 0.88f, 0.96f);
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
        if (timelineContainer == null || exportPlayer == null)
        {
            ShowEditorWarning("The export system is not ready. Please check the Editor setup and try again.");
            return;
        }

        float totalCam = 0, totalLight = 0, totalSeconds = 0;
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
            totalCam += clip.cameraScore * clipSeconds;
            totalLight += clip.lightScore * clipSeconds;
            totalSeconds += clipSeconds;

            RectTransform rt = clip.GetComponent<RectTransform>();
            float trueStartX = rt.anchoredPosition.x - (rt.rect.width * rt.pivot.x);

            sequence.Add(new ClipSegment
            {
                path = clip.clipFilePath,
                startFrame = clip.startFrame,
                endFrame = clip.endFrame,
                uiStartX = trueStartX,
                uiWidth = rt.rect.width
            });
        }

        if (totalSeconds > 0f)
        {
            totalCam /= totalSeconds;
            totalLight /= totalSeconds;
        }

        if (sequence.Count == 0 || totalSeconds <= 0f)
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

        if (reviewVideoPanel != null) reviewVideoPanel.SetActive(true);

        CommercialCompiler compiler = FindObjectOfType<CommercialCompiler>();
        if (compiler != null && compiler.editorPlayer != null) compiler.editorPlayer.StopTape();

        foreach (GameObject clone in clonedLogos) if (clone != null) Destroy(clone);
        clonedLogos.Clear();

        if (exportPlayer != null && exportPlayer.computerScreen != null)
        {
            if (gradingManager != null && gradingManager.computerScreen != null)
            {
                if (exportMaterial != null) Destroy(exportMaterial);
                exportMaterial = new Material(gradingManager.computerScreen.material);
                exportPlayer.computerScreen.material = exportMaterial;
            }

            DraggableOverlay[] allOverlays = FindObjectsOfType<DraggableOverlay>();
            foreach (var overlay in allOverlays)
            {
                if (overlay.isOnTimeline)
                {
                    GameObject clone = Instantiate(overlay.gameObject, exportPlayer.computerScreen.transform);
                    RectTransform cloneRT = clone.GetComponent<RectTransform>();
                    RectTransform origRT = overlay.GetComponent<RectTransform>();

                    RectTransform smallTV = overlay.transform.parent.GetComponent<RectTransform>();
                    RectTransform bigTV = exportPlayer.computerScreen.GetComponent<RectTransform>();

                    if (cloneRT == null || origRT == null || smallTV == null || bigTV == null || smallTV.rect.width <= 0f || smallTV.rect.height <= 0f)
                    {
                        Destroy(clone);
                        continue;
                    }

                    float ratioX = bigTV.rect.width / smallTV.rect.width;
                    float ratioY = bigTV.rect.height / smallTV.rect.height;

                    cloneRT.anchoredPosition = new Vector2(origRT.anchoredPosition.x * ratioX, origRT.anchoredPosition.y * ratioY);
                    cloneRT.sizeDelta = new Vector2(origRT.sizeDelta.x * ratioX, origRT.sizeDelta.y * ratioY);
                    cloneRT.localScale = origRT.localScale;

                    clonedLogos.Add(clone);
                }
            }
        }

        exportPlayer.PlaySequence(sequence, hasFadeIn);
        return true;
    }

    public void SubmitVideo()
    {
        if (grader == null)
        {
            ShowEditorWarning("The grading system is not ready. Your commercial has not been submitted.");
            return;
        }

        CrossSceneData.finalGrades = grader.GenerateGrades(pendingCam, pendingLight, pendingSec);
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnVideoSubmitted();

        foreach (GameObject clone in clonedLogos)
        {
            if (clone != null) Destroy(clone);
        }
        clonedLogos.Clear();

        if (reviewVideoPanel != null) reviewVideoPanel.SetActive(false);
        if (exportPlayer != null) exportPlayer.StopTape();

        SceneManager.LoadScene("ReviewScene");
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

public class PlayerEditTools : MonoBehaviour
{
    public enum CameraMotionMode { None, SlowPushIn, SlowPullOut, PanLeft, PanRight }
    public enum GraphicAnimationMode { Cut, Fade, SlideUp, Pop }
    public enum TransitionMode { Cut, FadeInOut, DipToBlack }
    public enum MusicMode { None, Clean, Energy, Cinematic }

    public static PlayerEditTools Instance;

    [HideInInspector] public CameraMotionMode selectedCameraMotion = CameraMotionMode.None;
    [HideInInspector] public GraphicAnimationMode selectedGraphicAnimation = GraphicAnimationMode.Cut;
    [HideInInspector] public TransitionMode selectedTransition = TransitionMode.Cut;
    [HideInInspector] public MusicMode selectedMusic = MusicMode.None;

    private GameObject toolsPanel;
    private TextMeshProUGUI cameraMotionText;
    private TextMeshProUGUI graphicAnimationText;
    private TextMeshProUGUI transitionText;
    private TextMeshProUGUI musicText;
    private RectTransform cameraMotionButtonRect;
    private RectTransform graphicAnimationButtonRect;
    private RectTransform transitionButtonRect;
    private RectTransform musicButtonRect;

    private void Awake()
    {
        Instance = this;
    }

    public void Initialize(GameObject brandingPanel)
    {
        if (brandingPanel == null || toolsPanel != null) return;

        MakeRoomForTools(brandingPanel.transform);

        toolsPanel = new GameObject("Player Edit Tools", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        toolsPanel.layer = brandingPanel.layer;
        toolsPanel.transform.SetParent(brandingPanel.transform, false);

        RectTransform panelRect = toolsPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, 12f);
        panelRect.sizeDelta = new Vector2(-24f, 174f);

        Image panelImage = toolsPanel.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.045f, 0.065f, 0.98f);

        Outline outline = toolsPanel.GetComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.72f, 0.95f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);

        TextMeshProUGUI header = CreateText("PLAYER-CREATED FINISH — NOTHING IS AUTO-APPLIED", toolsPanel.transform, 19f, TextAlignmentOptions.Center);
        SetRect(header.rectTransform, new Vector2(0f, 0.73f), new Vector2(1f, 1f), new Vector2(12f, 0f), new Vector2(-12f, 0f));
        header.color = new Color(0.42f, 0.88f, 1f);

        cameraMotionText = CreateToolButton("Camera Motion", toolsPanel.transform, new Vector2(0.01f, 0.08f), new Vector2(0.24f, 0.7f), CycleCameraMotion, out cameraMotionButtonRect);
        graphicAnimationText = CreateToolButton("Graphic Animation", toolsPanel.transform, new Vector2(0.255f, 0.08f), new Vector2(0.485f, 0.7f), CycleGraphicAnimation, out graphicAnimationButtonRect);
        transitionText = CreateToolButton("Transition", toolsPanel.transform, new Vector2(0.5f, 0.08f), new Vector2(0.73f, 0.7f), CycleTransition, out transitionButtonRect);
        musicText = CreateToolButton("Music", toolsPanel.transform, new Vector2(0.745f, 0.08f), new Vector2(0.99f, 0.7f), CycleMusic, out musicButtonRect);

        RefreshLabels();
    }

    public void SetVisible(bool visible)
    {
        if (toolsPanel != null) toolsPanel.SetActive(visible);
    }

    public RectTransform GetCameraMotionButtonRect() { return cameraMotionButtonRect; }
    public RectTransform GetGraphicAnimationButtonRect() { return graphicAnimationButtonRect; }
    public RectTransform GetTransitionButtonRect() { return transitionButtonRect; }
    public RectTransform GetMusicButtonRect() { return musicButtonRect; }

    public void CycleCameraMotion()
    {
        selectedCameraMotion = (CameraMotionMode)(((int)selectedCameraMotion + 1) % 5);
        RefreshLabels();
        NotifyEditChanged();
    }

    public void CycleGraphicAnimation()
    {
        selectedGraphicAnimation = (GraphicAnimationMode)(((int)selectedGraphicAnimation + 1) % 4);
        RefreshLabels();
        NotifyEditChanged();
    }

    public void CycleTransition()
    {
        selectedTransition = (TransitionMode)(((int)selectedTransition + 1) % 3);
        RefreshLabels();
        NotifyEditChanged();
    }

    public void CycleMusic()
    {
        selectedMusic = (MusicMode)(((int)selectedMusic + 1) % 4);
        RefreshLabels();
        NotifyEditChanged();
    }

    private void NotifyEditChanged()
    {
        TruePixelPlayer[] players = FindObjectsOfType<TruePixelPlayer>(true);
        foreach (TruePixelPlayer player in players)
        {
            if (player != null) player.RefreshPlayerCreatedEffects();
        }

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnPlayerEditToolChanged();
        }
    }

    private void RefreshLabels()
    {
        if (cameraMotionText != null) cameraMotionText.text = "CAMERA MOTION\n<color=#66D9FF>" + GetCameraMotionName() + "</color>";
        if (graphicAnimationText != null) graphicAnimationText.text = "GRAPHIC ANIMATION\n<color=#66D9FF>" + GetGraphicAnimationName() + "</color>";
        if (transitionText != null) transitionText.text = "TRANSITION\n<color=#66D9FF>" + GetTransitionName() + "</color>";
        if (musicText != null) musicText.text = "MUSIC\n<color=#66D9FF>" + selectedMusic.ToString().ToUpper() + "</color>";
    }

    private string GetCameraMotionName()
    {
        if (selectedCameraMotion == CameraMotionMode.SlowPushIn) return "SLOW PUSH IN";
        if (selectedCameraMotion == CameraMotionMode.SlowPullOut) return "SLOW PULL OUT";
        if (selectedCameraMotion == CameraMotionMode.PanLeft) return "PAN LEFT";
        if (selectedCameraMotion == CameraMotionMode.PanRight) return "PAN RIGHT";
        return "OFF";
    }

    private string GetGraphicAnimationName()
    {
        if (selectedGraphicAnimation == GraphicAnimationMode.SlideUp) return "SLIDE UP";
        return selectedGraphicAnimation.ToString().ToUpper();
    }

    private string GetTransitionName()
    {
        if (selectedTransition == TransitionMode.FadeInOut) return "FADE IN / OUT";
        if (selectedTransition == TransitionMode.DipToBlack) return "DIP TO BLACK";
        return "STRAIGHT CUT";
    }

    private void MakeRoomForTools(Transform brandingPanel)
    {
        RectTransform[] children = brandingPanel.GetComponentsInChildren<RectTransform>(true);
        foreach (RectTransform child in children)
        {
            if (child == null || child.transform == brandingPanel || child.name != "Assets") continue;

            Vector2 offsetMin = child.offsetMin;
            offsetMin.y = Mathf.Max(offsetMin.y, 198f);
            child.offsetMin = offsetMin;
            break;
        }
    }

    private TextMeshProUGUI CreateToolButton(string objectName, Transform parent, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action, out RectTransform buttonRect)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = parent.gameObject.layer;
        buttonObject.transform.SetParent(parent, false);

        buttonRect = buttonObject.GetComponent<RectTransform>();
        SetRect(buttonRect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);

        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.07f, 0.12f, 0.17f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.07f, 0.12f, 0.17f, 1f);
        colors.highlightedColor = new Color(0.12f, 0.3f, 0.42f, 1f);
        colors.pressedColor = new Color(0.08f, 0.55f, 0.75f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;
        button.onClick.AddListener(action);

        TextMeshProUGUI label = CreateText(objectName + " Label", buttonObject.transform, 16f, TextAlignmentOptions.Center);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 4f), new Vector2(-6f, -4f));
        return label;
    }

    private TextMeshProUGUI CreateText(string objectName, Transform parent, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = parent.gameObject.layer;
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
