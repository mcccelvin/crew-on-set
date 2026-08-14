using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using System.Collections;

public class ClipInspector : MonoBehaviour
{
    public static ClipInspector Instance;

    [Header("UI References")]
    public RawImage previewScreen;
    public RectTransform trimTrack;
    public RectTransform leftHandle;
    public RectTransform rightHandle;
    public TextMeshProUGUI frameDataText;

    [Header("Loading Screen")]
    public GameObject loadingSpinner;

    [Header("Settings")]
    public float pixelsPerSecond = 40f;

    private DraggableClip currentClip;
    private List<long> frameOffsets = new List<long>();
    private BinaryReader frameReader;
    private Texture2D previewTexture;
    private int totalRawFrames;
    private bool needsToLoad = false;

    private void Awake()
    {
        Instance = this;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    public void OpenInspector(DraggableClip clip)
    {
        currentClip = clip;
        needsToLoad = true;

        if (transform.parent != null && !transform.parent.gameObject.activeSelf)
        {
            transform.parent.gameObject.SetActive(true);
        }

        gameObject.SetActive(true);
    }

    private void OnEnable()
    {
        if (needsToLoad && currentClip != null)
        {
            needsToLoad = false;
            StartCoroutine(LoadFramesCoroutine(currentClip.clipFilePath));
        }
    }

    private IEnumerator LoadFramesCoroutine(string path)
    {
        if (loadingSpinner != null) loadingSpinner.SetActive(true);
        yield return null;

        try
        {
            CloseFrameReader();
            frameOffsets.Clear();
            totalRawFrames = 0;

            if (File.Exists(path))
            {
                frameReader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
                totalRawFrames = frameReader.ReadInt32();

                if (totalRawFrames > 0)
                {
                    for (int i = 0; i < totalRawFrames; i++)
                    {
                        frameOffsets.Add(frameReader.BaseStream.Position);
                        int frameSize = frameReader.ReadInt32();
                        frameReader.BaseStream.Seek(frameSize, SeekOrigin.Current);

                        if (i % 30 == 0) yield return null;
                    }
                }
            }
        }
        finally
        {
            if (loadingSpinner != null) loadingSpinner.SetActive(false);
            if (currentClip != null)
            {
                UpdateHandlePositions();
                ShowFrame(currentClip.startFrame);
            }
        }
    }

    private void UpdateHandlePositions()
    {
        if (totalRawFrames <= 0) return;
        float trackWidth = trimTrack.rect.width;
        float leftPct = (float)currentClip.startFrame / totalRawFrames;
        float rightPct = (float)currentClip.endFrame / totalRawFrames;

        leftHandle.anchorMin = new Vector2(0, 0.5f);
        leftHandle.anchorMax = new Vector2(0, 0.5f);
        rightHandle.anchorMin = new Vector2(0, 0.5f);
        rightHandle.anchorMax = new Vector2(0, 0.5f);

        leftHandle.anchoredPosition = new Vector2(leftPct * trackWidth, 0);
        rightHandle.anchoredPosition = new Vector2(rightPct * trackWidth, 0);
    }

    public void HandleDragged(bool isLeft, PointerEventData eventData)
    {
        if (currentClip == null || totalRawFrames <= 0) return;

        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(trimTrack, eventData.position, eventData.pressEventCamera, out localPos);

        float adjustedX = localPos.x + (trimTrack.pivot.x * trimTrack.rect.width);
        float pct = Mathf.Clamp01(adjustedX / trimTrack.rect.width);
        int frame = Mathf.RoundToInt(pct * totalRawFrames);

        if (isLeft)
        {
            frame = Mathf.Clamp(frame, 0, totalRawFrames - 1);
            if (frame >= currentClip.endFrame) frame = currentClip.endFrame - 1;
            currentClip.startFrame = frame;
        }
        else
        {
            frame = Mathf.Clamp(frame, 1, totalRawFrames);
            if (frame <= currentClip.startFrame) frame = currentClip.startFrame + 1;
            currentClip.endFrame = frame;
        }

        UpdateHandlePositions();
        ShowFrame(frame);
        UpdateClipUI();

        if (EditorTutorialManager.Instance != null)
        {
            if (isLeft) EditorTutorialManager.Instance.OnLeftHandleTrimmed();
            else EditorTutorialManager.Instance.OnRightHandleTrimmed();
        }
    }

    private void ShowFrame(int frameIndex)
    {
        if (frameReader != null && frameOffsets.Count > 0)
        {
            int safeIndex = Mathf.Clamp(frameIndex, 0, frameOffsets.Count - 1);
            frameReader.BaseStream.Position = frameOffsets[safeIndex];

            int frameSize = frameReader.ReadInt32();
            byte[] frameData = frameReader.ReadBytes(frameSize);

            if (previewTexture == null) previewTexture = new Texture2D(2, 2);
            previewTexture.LoadImage(frameData);

            if (previewScreen != null) previewScreen.texture = previewTexture;

            if (currentClip != null && frameDataText != null)
            {
                float duration = (currentClip.endFrame - currentClip.startFrame) / TapeSettings.framesPerSecond;
                frameDataText.text = $"Trimmed Duration: {duration:F1} Sec";
            }
        }
    }

    private void UpdateClipUI()
    {
        float duration = (currentClip.endFrame - currentClip.startFrame) / TapeSettings.framesPerSecond;
        LayoutElement layout = currentClip.GetComponent<LayoutElement>();

        // Update Layout Component if inside Bin
        if (layout != null) layout.preferredWidth = Mathf.Max(duration * pixelsPerSecond, 60f);

        // --- THE FIX: Force the physical timeline clip to sync dimensions! ---
        if (currentClip.isOnTimeline)
        {
            currentClip.ApplyTrimFromInspector();
        }
    }

    public void CloseWindow()
    {
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.TrimTo10Seconds && EditorTutorialManager.Instance.isTaskPhaseActive)
            {
                float duration = (currentClip.endFrame - currentClip.startFrame) / TapeSettings.framesPerSecond;
                string displayDuration = duration.ToString("F1");

                if (displayDuration != "10.0")
                {
                    EditorTutorialManager.Instance.ShowWarning("It's not 10 seconds yet! Your duration is " + displayDuration + " Sec. Adjust the pink handles until it says exactly 10.0 Sec!");
                    return;
                }
            }
        }

        gameObject.SetActive(false);
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnTrimWindowClosed();
    }

    private void CloseFrameReader()
    {
        if (frameReader != null)
        {
            frameReader.Dispose();
            frameReader = null;
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        CloseFrameReader();
    }

    private void OnDestroy()
    {
        CloseFrameReader();
        if (previewTexture != null) Destroy(previewTexture);
        if (Instance == this) Instance = null;
    }
}
