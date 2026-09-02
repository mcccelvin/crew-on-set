using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class CommercialCompiler : MonoBehaviour
{
    public TruePixelPlayer editorPlayer;
    public Transform videoTimelineContainer;
    public Transform[] brandingTracks;

    public void PlayTimelineSequence(bool useFadeIn = false)
    {
        if (editorPlayer == null || videoTimelineContainer == null)
        {
            Debug.LogError("Compiler: Missing Player or Timeline references!");
            return;
        }

        List<ClipSegment> sequence = new List<ClipSegment>();
        DraggableClip[] clips = videoTimelineContainer.GetComponentsInChildren<DraggableClip>();

        if (clips.Length == 0)
        {
            Debug.LogWarning("Compiler: No clips found on timeline!");
            return;
        }

        List<DraggableClip> sortedClips = new List<DraggableClip>(clips);
        sortedClips.Sort((a, b) => a.transform.localPosition.x.CompareTo(b.transform.localPosition.x));

        foreach (var clip in sortedClips)
        {
            if (!IsPlayableClip(clip))
            {
                Debug.LogWarning("Compiler: Skipped a missing, damaged, or empty timeline clip.");
                continue;
            }

            RectTransform rt = clip.GetComponent<RectTransform>();
            // Bulletproof math to find the true left edge
            float trueStartX = rt.anchoredPosition.x - (rt.rect.width * rt.pivot.x);

            sequence.Add(new ClipSegment
            {
                path = clip.clipFilePath,
                startFrame = clip.startFrame,
                endFrame = clip.endFrame,

                // --- THE FIX: Send the UI dimensions to the player ---
                uiStartX = trueStartX,
                uiWidth = rt.rect.width
            });
        }

        if (sequence.Count == 0)
        {
            Debug.LogWarning("Compiler: No playable clips found on timeline!");
            if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
            {
                EditorTutorialManager.Instance.ShowWarning("The timeline does not contain a playable recording. Use a valid recorded clip before pressing Play.");
            }
            return;
        }

        editorPlayer.StopTape();

        DraggableOverlay[] overlays = FindObjectsOfType<DraggableOverlay>();
        foreach (var overlay in overlays)
        {
            if (overlay.isOnTimeline)
            {
                CanvasGroup cg = overlay.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 0f;
            }
        }

        editorPlayer.PlaySequence(sequence, useFadeIn);

        Debug.Log("Compiler: Sequence sent to Player. Clips: " + sortedClips.Count);

        if (EditorTutorialManager.Instance != null && EditorTutorialManager.Instance.gameObject.activeInHierarchy)
        {
            EditorTutorialManager.Instance.OnTimelinePlayed();
        }
    }

    private bool IsPlayableClip(DraggableClip clip)
    {
        if (clip == null || string.IsNullOrEmpty(clip.clipFilePath) || !File.Exists(clip.clipFilePath)) return false;
        if (clip.endFrame <= clip.startFrame) return false;

        try
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(clip.clipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                if (reader.BaseStream.Length < sizeof(int)) return false;
                int frameCount = reader.ReadInt32();
                if (frameCount <= 0 || clip.startFrame >= frameCount) return false;

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
            Debug.LogError("Compiler Tape Error: " + e.Message);
            return false;
        }
    }
}
