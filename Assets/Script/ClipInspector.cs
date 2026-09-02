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

    public bool OpenInspector(DraggableClip clip)
    {
        if (clip == null)
        {
            ShowInspectorWarning("The selected clip is missing. Choose a valid recorded clip from the media bin.");
            return false;
        }

        if (!TryGetTapeFrameCount(clip.clipFilePath, out int frameCount))
        {
            ShowInspectorWarning("This recording cannot be opened because its tape file is missing or damaged.");
            return false;
        }

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            float maximumDuration = frameCount / TapeSettings.framesPerSecond;
            if (maximumDuration < 9.95f)
            {
                ShowInspectorWarning("This recording is only " + maximumDuration.ToString("F1") + " seconds long. The tutorial edit needs at least 10.0 seconds of footage. Record a new take before opening the Editor.");
                return false;
            }
        }

        currentClip = clip;
        totalRawFrames = 0;
        needsToLoad = true;

        if (transform.parent != null && !transform.parent.gameObject.activeSelf)
        {
            transform.parent.gameObject.SetActive(true);
        }

        gameObject.SetActive(true);
        return true;
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
        if (totalRawFrames <= 0 || currentClip == null || trimTrack == null || leftHandle == null || rightHandle == null) return;
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

        if (currentClip == null)
        {
            ShowInspectorWarning("There is no clip loaded in the Trim Inspector.");
            return;
        }

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            if (EditorTutorialManager.Instance.currentStep == EditorTutorialManager.EditorStep.TrimTo10Seconds && EditorTutorialManager.Instance.isTaskPhaseActive)
            {
                float maximumDuration = totalRawFrames / TapeSettings.framesPerSecond;
                if (totalRawFrames <= 0 || maximumDuration < 9.95f)
                {
                    ShowInspectorWarning("This recording is only " + maximumDuration.ToString("F1") + " seconds long. It cannot be trimmed to the required 10.0 seconds.");
                    return;
                }

                float duration = (currentClip.endFrame - currentClip.startFrame) / TapeSettings.framesPerSecond;

                if (Mathf.Abs(duration - 10f) > 0.05f)
                {
                    EditorTutorialManager.Instance.ShowWarning("It's not 10 seconds yet! Your duration is " + duration.ToString("F1") + " Sec. Adjust the pink handles until it says exactly 10.0 Sec!");
                    return;
                }
            }
        }

        gameObject.SetActive(false);
        if (EditorTutorialManager.Instance != null) EditorTutorialManager.Instance.OnTrimWindowClosed();
    }

    private bool TryGetTapeFrameCount(string path, out int frameCount)
    {
        frameCount = 0;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;

        try
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (reader.BaseStream.Length < sizeof(int)) return false;
                frameCount = reader.ReadInt32();
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
        catch (System.Exception e)
        {
            Debug.LogError("Trim Inspector Tape Error: " + e.Message);
            return false;
        }
    }

    private void ShowInspectorWarning(string message)
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
