using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.IO;

// --- NEW: A custom container to hold Trim Data! ---
[System.Serializable]
public class ClipSegment
{
    public string path;
    public int startFrame;
    public int endFrame;
}

public class TruePixelPlayer : MonoBehaviour
{
    public RawImage computerScreen;
    public float framesPerSecond = 24f;

    [Header("UI Buttons")]
    public GameObject playButtonUI;
    public GameObject pauseButtonUI;

    [Header("Premiere Timeline")]
    public RectTransform playheadLine;
    public RectTransform timelineArea;

    private List<Texture2D> preloadedTextures = new List<Texture2D>();
    private bool isPaused = false;
    private bool isFinished = false;
    private List<ClipSegment> currentSequence = new List<ClipSegment>();

    public void PlayTape(string path)
    {
        // Default to playing the whole tape (max value)
        PlaySequence(new List<ClipSegment> { new ClipSegment { path = path, startFrame = 0, endFrame = int.MaxValue } });
    }

    public void PlaySequence(List<ClipSegment> sequence)
    {
        currentSequence = sequence;
        StopTape();
        isPaused = false;
        isFinished = false;
        UpdatePlayPauseUI();
        StartCoroutine(PlayMultipleVideosCoroutine(sequence));
    }

    public void StopTape()
    {
        StopAllCoroutines();
        isPaused = true;
        isFinished = true;
        if (computerScreen != null) computerScreen.texture = null;
        if (playheadLine != null) playheadLine.anchoredPosition = new Vector2(0, playheadLine.anchoredPosition.y);

        foreach (Texture2D tex in preloadedTextures) { if (tex != null) Destroy(tex); }
        preloadedTextures.Clear();
    }

    public void TogglePlayPause()
    {
        if (isFinished && currentSequence.Count > 0) { PlaySequence(currentSequence); return; }
        isPaused = !isPaused;
        UpdatePlayPauseUI();
    }

    private void UpdatePlayPauseUI()
    {
        if (playButtonUI != null) playButtonUI.SetActive(isPaused);
        if (pauseButtonUI != null) pauseButtonUI.SetActive(!isPaused);
    }

    private IEnumerator PlayMultipleVideosCoroutine(List<ClipSegment> sequence)
    {
        List<byte[]> allRawFrames = new List<byte[]>();

        foreach (ClipSegment clip in sequence)
        {
            if (File.Exists(clip.path))
            {
                using (BinaryReader reader = new BinaryReader(File.Open(clip.path, FileMode.Open)))
                {
                    int frameCount = reader.ReadInt32();
                    int safeEnd = Mathf.Min(clip.endFrame, frameCount); // Prevent reading past the end

                    for (int i = 0; i < frameCount; i++)
                    {
                        int size = reader.ReadInt32();
                        byte[] frameData = reader.ReadBytes(size);

                        // --- THE MAGIC: Only save the frame if it is inside the Trim Zone! ---
                        if (i >= clip.startFrame && i < safeEnd)
                        {
                            allRawFrames.Add(frameData);
                        }
                    }
                }
            }
        }

        if (allRawFrames.Count == 0) yield break;

        if (playheadLine != null) playheadLine.anchoredPosition = new Vector2(0, playheadLine.anchoredPosition.y);

        Texture2D thumbnail = new Texture2D(2, 2);
        thumbnail.LoadImage(allRawFrames[0]);
        thumbnail.Apply();
        preloadedTextures.Add(thumbnail);
        if (computerScreen != null) computerScreen.texture = thumbnail;

        for (int i = 1; i < allRawFrames.Count; i++)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(allRawFrames[i]);
            tex.Apply();
            preloadedTextures.Add(tex);
            if (i % 5 == 0) yield return null;
        }

        for (int i = 0; i < preloadedTextures.Count; i++)
        {
            while (isPaused) yield return null;
            if (computerScreen != null) computerScreen.texture = preloadedTextures[i];

            if (playheadLine != null && timelineArea != null)
            {
                float progress = (float)i / preloadedTextures.Count;
                float totalWidth = 0f;
                foreach (Transform child in timelineArea)
                {
                    if (child != playheadLine) totalWidth += child.GetComponent<RectTransform>().rect.width;
                }

                HorizontalLayoutGroup layout = timelineArea.GetComponent<HorizontalLayoutGroup>();
                if (layout != null) totalWidth += layout.spacing * (timelineArea.childCount - 2);

                playheadLine.anchoredPosition = new Vector2(progress * totalWidth, playheadLine.anchoredPosition.y);
            }

            yield return new WaitForSeconds(1f / framesPerSecond);
        }

        if (computerScreen != null) computerScreen.texture = thumbnail;
        isFinished = true;
        isPaused = true;
        UpdatePlayPauseUI();
    }
}