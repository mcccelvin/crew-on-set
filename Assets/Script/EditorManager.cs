using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using System.IO;
using UnityEngine.SceneManagement;

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
    private float pendingCam = 0f;
    private float pendingLight = 0f;
    private float pendingSec = 0f;

    private void Awake() { Instance = this; }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentPhase = 0;
        UpdatePhaseUI();
        LoadClipsFromBridge();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F11)) GenerateCheatClip();
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

        // --- 12 seconds * 24 frames per second = 288 frames ---
        int totalFrames = 288;

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
            }

            TextMeshProUGUI clipText = newClip.GetComponentInChildren<TextMeshProUGUI>();
            LoadThumbnail(fullPath, newClip, dragScript, Path.GetFileNameWithoutExtension(fileName), clipText);
        }
    }

    private void LoadClipsFromBridge()
    {
        if (ProjectDataManager.Instance == null) return;

        foreach (var data in ProjectDataManager.Instance.compiledFootage)
        {
            GameObject newClip = Instantiate(clipPrefab, clipBankContainer);
            string fullPath = Path.Combine(Application.persistentDataPath, data.fileName);

            DraggableClip dragScript = newClip.GetComponent<DraggableClip>();
            if (dragScript != null)
            {
                dragScript.clipFilePath = fullPath;
                dragScript.cameraScore = data.camScore;
                dragScript.lightScore = data.lightScore;
            }

            TextMeshProUGUI clipText = newClip.GetComponentInChildren<TextMeshProUGUI>();
            string displayName = Path.GetFileNameWithoutExtension(data.fileName);

            if (File.Exists(fullPath)) LoadThumbnail(fullPath, newClip, dragScript, displayName, clipText);
        }
    }

    private void LoadThumbnail(string path, GameObject clipObj, DraggableClip script, string name, TextMeshProUGUI text)
    {
        try
        {
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                int frameCount = reader.ReadInt32();
                float duration = frameCount / 24f;

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
                    thumbTex.LoadImage(frameBytes);

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

        if (tabButtonImages != null && tabButtonImages.Length > 0)
        {
            for (int i = 0; i < tabButtonImages.Length; i++)
            {
                if (tabButtonImages[i] != null) tabButtonImages[i].color = (i == currentPhase) ? activeTabColor : inactiveTabColor;
            }
        }
    }

    public void ExportCommercial()
    {
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnExportClicked();

        float totalCam = 0, totalLight = 0, totalSeconds = 0;
        DraggableClip[] clips = timelineContainer.GetComponentsInChildren<DraggableClip>();
        List<ClipSegment> sequence = new List<ClipSegment>();

        List<DraggableClip> sortedClips = new List<DraggableClip>(clips);
        sortedClips.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));

        foreach (var clip in sortedClips)
        {
            totalCam += clip.cameraScore;
            totalLight += clip.lightScore;
            totalSeconds += (clip.endFrame - clip.startFrame) / 24f;

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

        if (clips.Length > 0)
        {
            totalCam /= clips.Length;
            totalLight /= clips.Length;
        }

        bool hasFadeIn = false;
        if (gradingManager != null && gradingManager.fadeInToggle != null) hasFadeIn = gradingManager.fadeInToggle.isOn;

        pendingCam = totalCam;
        pendingLight = totalLight;
        pendingSec = totalSeconds;

        PlayFinalReview(sequence, hasFadeIn);
    }

    private void PlayFinalReview(List<ClipSegment> sequence, bool hasFadeIn)
    {
        if (reviewVideoPanel != null) reviewVideoPanel.SetActive(true);

        CommercialCompiler compiler = FindObjectOfType<CommercialCompiler>();
        if (compiler != null && compiler.editorPlayer != null) compiler.editorPlayer.StopTape();

        foreach (GameObject clone in clonedLogos) if (clone != null) Destroy(clone);
        clonedLogos.Clear();

        if (exportPlayer != null && exportPlayer.computerScreen != null)
        {
            if (gradingManager != null && gradingManager.computerScreen != null)
                exportPlayer.computerScreen.material = new Material(gradingManager.computerScreen.material);

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

                    float ratioX = bigTV.rect.width / smallTV.rect.width;
                    float ratioY = bigTV.rect.height / smallTV.rect.height;

                    cloneRT.anchoredPosition = new Vector2(origRT.anchoredPosition.x * ratioX, origRT.anchoredPosition.y * ratioY);
                    cloneRT.sizeDelta = new Vector2(origRT.sizeDelta.x * ratioX, origRT.sizeDelta.y * ratioY);
                    cloneRT.localScale = origRT.localScale;

                    clonedLogos.Add(clone);
                }
            }
        }

        if (exportPlayer != null) exportPlayer.PlaySequence(sequence, hasFadeIn);
        else Debug.LogError("EXPORT FAILED: You did not assign the 'Export Player' in the Inspector!");
    }

    public void SubmitVideo()
    {
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnVideoSubmitted();

        foreach (GameObject clone in clonedLogos)
        {
            if (clone != null) Destroy(clone);
        }
        clonedLogos.Clear();

        if (reviewVideoPanel != null) reviewVideoPanel.SetActive(false);
        if (exportPlayer != null) exportPlayer.StopTape();

        if (grader != null)
        {
            CrossSceneData.finalGrades = grader.GenerateGrades(pendingCam, pendingLight, pendingSec);
            SceneManager.LoadScene("ReviewScene");
        }
    }
}