using UnityEngine;
using System.Collections;
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

    [Header("UI References - Navigation")]
    public GameObject nextButton;
    public TextMeshProUGUI nextButtonText;
    public GameObject backButton;
    public GameObject exportButton;

    [Header("Color Grading References")]
    public ColorGradingManager gradingManager;
    public ContractGrader grader;

    [Header("UI References - Exporting")]
    public GameObject reviewVideoPanel;
    public GameObject finalGradePanel;
    public RawImage bigScreenRawImage;

    [Header("Premiere Settings")]
    public float pixelsPerSecond = 40f;

    private int currentPhase = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        currentPhase = 0;
        UpdatePhaseUI();
        LoadClipsFromBridge();
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

            if (File.Exists(fullPath))
            {
                LoadThumbnail(fullPath, newClip, dragScript, displayName, clipText);
            }
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

    public void GoToNextPhase()
    {
        if (currentPhase < 2) currentPhase++;
        UpdatePhaseUI();

        // --- NEW TUTORIAL PING ---
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnPhaseChanged(currentPhase);
    }

    public void GoToPreviousPhase()
    {
        if (currentPhase > 0) currentPhase--;
        UpdatePhaseUI();
    }

    private void UpdatePhaseUI()
    {
        if (clipBankContainer != null) clipBankContainer.gameObject.SetActive(currentPhase == 0);
        if (brandingBinPanel != null) brandingBinPanel.SetActive(currentPhase == 1);
        if (colorGradingBin != null) colorGradingBin.SetActive(currentPhase == 2);

        if (nextButton != null) nextButton.SetActive(currentPhase < 2);
        if (backButton != null) backButton.SetActive(currentPhase > 0);
        if (exportButton != null) exportButton.SetActive(currentPhase == 2);

        if (nextButtonText != null)
            nextButtonText.text = (currentPhase == 0) ? "Next: Add Branding" : "Next: Color Grade";
    }

    public void ExportCommercial()
    {
        // --- NEW TUTORIAL PING ---
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnExportClicked();

        float totalCam = 0, totalLight = 0, totalSeconds = 0;
        DraggableClip[] clips = timelineContainer.GetComponentsInChildren<DraggableClip>();

        foreach (var clip in clips)
        {
            totalCam += clip.cameraScore;
            totalLight += clip.lightScore;
            totalSeconds += (clip.endFrame - clip.startFrame) / 24f;
        }

        if (clips.Length > 0)
        {
            totalCam /= clips.Length;
            totalLight /= clips.Length;
        }

        CommercialCompiler compiler = FindObjectOfType<CommercialCompiler>();
        if (compiler != null)
        {
            StartCoroutine(PlayFinalReview(compiler, totalCam, totalLight, totalSeconds));
        }
    }

    private IEnumerator PlayFinalReview(CommercialCompiler compiler, float cam, float light, float sec)
    {
        if (clipBankContainer != null) clipBankContainer.parent.gameObject.SetActive(false);
        if (brandingBinPanel != null) brandingBinPanel.SetActive(false);
        if (colorGradingBin != null) colorGradingBin.SetActive(false);
        if (exportButton != null) exportButton.SetActive(false);
        if (backButton != null) backButton.SetActive(false);

        if (reviewVideoPanel != null) reviewVideoPanel.SetActive(true);

        RectTransform tvRect = null;
        Transform originalParent = null;
        int originalSiblingIndex = 0;
        Vector3 origWorldPos = Vector3.zero;
        Vector3 origScale = Vector3.one;

        if (compiler.editorPlayer != null && compiler.editorPlayer.computerScreen != null)
        {
            tvRect = compiler.editorPlayer.computerScreen.GetComponent<RectTransform>();

            originalParent = tvRect.parent;
            originalSiblingIndex = tvRect.GetSiblingIndex();
            origWorldPos = tvRect.position;
            origScale = tvRect.localScale;

            tvRect.SetParent(reviewVideoPanel.transform, true);

            RectTransform reviewRect = reviewVideoPanel.GetComponent<RectTransform>();
            Vector3 screenCenter = reviewRect.position;
            Vector3 tvCenter = tvRect.TransformPoint(tvRect.rect.center);
            tvRect.position += (screenCenter - tvCenter);

            float scaleX = reviewRect.rect.width / tvRect.rect.width;
            float scaleY = reviewRect.rect.height / tvRect.rect.height;
            float zoomMultiplier = Mathf.Min(scaleX, scaleY);

            tvRect.localScale = origScale * zoomMultiplier;

            Vector3 newTvCenter = tvRect.TransformPoint(tvRect.rect.center);
            tvRect.position += (screenCenter - newTvCenter);
        }

        compiler.PlayTimelineSequence();

        if (compiler.editorPlayer != null)
        {
            yield return new WaitUntil(() => compiler.editorPlayer.isFinished);
        }

        yield return new WaitForSeconds(0.5f);

        if (tvRect != null && originalParent != null)
        {
            tvRect.SetParent(originalParent, true);
            tvRect.SetSiblingIndex(originalSiblingIndex);
            tvRect.localScale = origScale;
            tvRect.position = origWorldPos;
        }

        if (reviewVideoPanel != null) reviewVideoPanel.SetActive(false);
        if (finalGradePanel != null) finalGradePanel.SetActive(true);

        if (grader != null)
        {
            grader.GenerateFinalReport(cam, light, sec);
        }
    }
    public void ReturnToStudio()
    {
        // --- FOOLPROOF SAVE ---
        // If the player's progress is 0, they MUST have just finished the Editor Tutorial.
        // We force it to 1 so the Studio Boss is guaranteed to wake up!
        if (PlayerPrefs.GetInt("TutorialProgress", 0) == 0)
        {
            PlayerPrefs.SetInt("TutorialProgress", 1);
            PlayerPrefs.Save();
            Debug.Log("Saved Tutorial Progress as 1! Studio Boss will now load.");
        }

        // Load the Studio
        UnityEngine.SceneManagement.SceneManager.LoadScene("Studio");
    }
}