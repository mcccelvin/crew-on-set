using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public class ClipUIItem : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI clipTitleText;
    public RawImage previewImage;

    private string fullFilePath;
    private ComputerUIManager uiManager;
    private Texture2D thumbnailTexture;
    private Button clipButton;

    public void Setup(string filePath, ComputerUIManager manager)
    {
        fullFilePath = filePath;
        uiManager = manager;

        if (clipTitleText != null)
        {
            clipTitleText.text = Path.GetFileNameWithoutExtension(filePath);
        }

        // Bind the thumbnail itself, never an arbitrary child (such as Delete).
        // Replace serialized callbacks too: reused cards must only open their own file.
        if (previewImage != null)
        {
            previewImage.raycastTarget = true;
            clipButton = previewImage.GetComponent<Button>();
            if (clipButton == null) clipButton = previewImage.gameObject.AddComponent<Button>();
            clipButton.targetGraphic = previewImage;
        }
        else clipButton = GetComponent<Button>();

        if (clipButton != null)
        {
            clipButton.onClick = new Button.ButtonClickedEvent();
            clipButton.onClick.AddListener(OnPlayButtonClicked);
            clipButton.interactable = true;
        }
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button == clipButton || !button.name.Equals("Play", System.StringComparison.OrdinalIgnoreCase)) continue;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OnPlayButtonClicked);
        }

        LoadThumbnail();
    }

    private void LoadThumbnail()
    {
        if (previewImage == null || !File.Exists(fullFilePath)) return;

        if (thumbnailTexture != null)
        {
            Destroy(thumbnailTexture);
            thumbnailTexture = null;
        }

        try
        {
            using (BinaryReader reader = new BinaryReader(new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                int frameCount = reader.ReadInt32();

                if (frameCount > 0)
                {
                    int frameSize = reader.ReadInt32();
                    byte[] frameBytes = reader.ReadBytes(frameSize);

                    thumbnailTexture = new Texture2D(2, 2);
                    thumbnailTexture.LoadImage(frameBytes, true);

                    previewImage.texture = thumbnailTexture;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load thumbnail for {fullFilePath}: {e.Message}");
        }
    }

    public void OnPlayButtonClicked()
    {
        if (uiManager == null || string.IsNullOrEmpty(fullFilePath) || !File.Exists(fullFilePath))
        {
            GameFeedback.Show("This recording file is unavailable. Reinsert its SD card and try again.");
            return;
        }
        // --- NEW: Tutorial Bouncer and Event Trigger ---
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanUseComputerFeature("VideoClip")) return;
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVideoClipClicked();

        uiManager.OpenPlayerView(fullFilePath);
    }

    public void OnDeleteButtonClicked()
    {
        uiManager.DeleteClip(fullFilePath);
    }

    private void OnDestroy()
    {
        if (clipButton != null) clipButton.onClick.RemoveListener(OnPlayButtonClicked);

        if (thumbnailTexture != null)
        {
            Destroy(thumbnailTexture);
        }
    }
}
