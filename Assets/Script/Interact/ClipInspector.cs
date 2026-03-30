using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class ClipInspector : MonoBehaviour
{
    public static ClipInspector Instance;

    [Header("UI References")]
    public RawImage previewScreen;
    public RectTransform trimTrack;
    public RectTransform leftHandle;
    public RectTransform rightHandle;
    public TextMeshProUGUI frameDataText;

    [Header("Settings")]
    public float pixelsPerSecond = 40f;

    private DraggableClip currentClip;
    private List<Texture2D> preloadedFrames = new List<Texture2D>();
    private int totalRawFrames;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void OpenInspector(DraggableClip clip)
    {
        currentClip = clip;
        gameObject.SetActive(true);

        LoadFrames(clip.clipFilePath);
        UpdateHandlePositions();
        ShowFrame(currentClip.startFrame);
    }

    private void LoadFrames(string path)
    {
        foreach (var tex in preloadedFrames) if (tex != null) Destroy(tex);
        preloadedFrames.Clear();

        if (File.Exists(path))
        {
            using (BinaryReader reader = new BinaryReader(File.Open(path, FileMode.Open)))
            {
                totalRawFrames = reader.ReadInt32();
                if (totalRawFrames == 0) return;

                for (int i = 0; i < totalRawFrames; i++)
                {
                    byte[] data = reader.ReadBytes(reader.ReadInt32());
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(data);
                    tex.Apply();
                    preloadedFrames.Add(tex);
                }
            }
        }
    }

    private void UpdateHandlePositions()
    {
        if (totalRawFrames <= 0) return;
        float trackWidth = trimTrack.rect.width;
        float leftPct = (float)currentClip.startFrame / (totalRawFrames - 1);
        float rightPct = (float)currentClip.endFrame / (totalRawFrames - 1);

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
        int frame = Mathf.RoundToInt(pct * (totalRawFrames - 1));

        if (isLeft)
        {
            if (frame >= currentClip.endFrame) frame = currentClip.endFrame - 1;
            currentClip.startFrame = frame;
        }
        else
        {
            if (frame <= currentClip.startFrame) frame = currentClip.startFrame + 1;
            currentClip.endFrame = frame;
        }

        UpdateHandlePositions();
        ShowFrame(frame);
        UpdateClipUI();

        // --- UPDATED HYPER-SPECIFIC PING ---
        if (EditorTutorialManager.Instance != null)
        {
            if (isLeft) EditorTutorialManager.Instance.OnLeftHandleTrimmed();
            else EditorTutorialManager.Instance.OnRightHandleTrimmed();
        }
    }

    private void ShowFrame(int frameIndex)
    {
        if (preloadedFrames.Count > 0)
        {
            int safeIndex = Mathf.Clamp(frameIndex, 0, preloadedFrames.Count - 1);
            previewScreen.texture = preloadedFrames[safeIndex];

            // --- NEW: Calculate and display the exact Trimmed Duration in seconds! ---
            if (currentClip != null && frameDataText != null)
            {
                float duration = (currentClip.endFrame - currentClip.startFrame) / 24f; // Assuming 24 FPS
                frameDataText.text = $"Trimmed Duration: {duration:F1} Sec";
            }
        }
    }

    private void UpdateClipUI()
    {
        float duration = (currentClip.endFrame - currentClip.startFrame) / 24f;
        LayoutElement layout = currentClip.GetComponent<LayoutElement>();
        if (layout != null) layout.preferredWidth = Mathf.Max(duration * pixelsPerSecond, 60f);
    }

    public void CloseWindow() { gameObject.SetActive(false); }
}