using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class EditorManager : MonoBehaviour
{
    [Header("UI References")]
    public Transform clipBankContainer;
    public Transform timelineContainer;
    public GameObject clipPrefab;

    [Header("Premiere Settings")]
    public float pixelsPerSecond = 40f;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        LoadClipsFromBridge();
    }

    private void LoadClipsFromBridge()
    {
        if (ProjectDataManager.Instance == null) return;

        foreach (string fileName in ProjectDataManager.Instance.rawFootagePaths)
        {
            GameObject newClip = Instantiate(clipPrefab, clipBankContainer);
            string fullPath = Path.Combine(Application.persistentDataPath, fileName);

            DraggableClip dragScript = newClip.GetComponent<DraggableClip>();
            if (dragScript != null) dragScript.clipFilePath = fullPath;

            TextMeshProUGUI clipText = newClip.GetComponentInChildren<TextMeshProUGUI>();
            string displayName = Path.GetFileNameWithoutExtension(fileName);

            if (File.Exists(fullPath))
            {
                try
                {
                    using (BinaryReader reader = new BinaryReader(File.Open(fullPath, FileMode.Open)))
                    {
                        int frameCount = reader.ReadInt32();
                        float duration = frameCount / 24f;

                        // --- THE CRITICAL FIX: Tell the clip exactly how long it is! ---
                        if (dragScript != null)
                        {
                            dragScript.totalFrames = frameCount;
                            dragScript.startFrame = 0;
                            dragScript.endFrame = frameCount;
                        }

                        if (frameCount > 0)
                        {
                            int frameSize = reader.ReadInt32();
                            byte[] frameBytes = reader.ReadBytes(frameSize);

                            Texture2D thumbTex = new Texture2D(2, 2);
                            thumbTex.LoadImage(frameBytes);
                            thumbTex.Apply();

                            RawImage thumbUI = newClip.GetComponentInChildren<RawImage>();
                            if (thumbUI != null)
                            {
                                thumbUI.texture = thumbTex;
                                thumbUI.color = Color.white;
                            }
                        }

                        if (clipText != null) clipText.text = $" {displayName} [V]\n <size=70%>{duration:F1}s</size>";

                        // --- LAYOUT FIX: Allow the clip to shrink when trimmed! ---
                        LayoutElement layout = newClip.GetComponent<LayoutElement>();
                        if (layout == null) layout = newClip.AddComponent<LayoutElement>();
                        layout.preferredWidth = Mathf.Max(duration * pixelsPerSecond, 120f);
                        layout.minWidth = 60f; // Let it shrink down to a tiny square if needed
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"THUMBNAIL CRASH on file {fileName}: {e.Message}");
                    if (clipText != null) clipText.text = displayName;
                }
            }
        }
    }
}