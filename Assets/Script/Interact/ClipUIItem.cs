using UnityEngine;
using UnityEngine.UI; // Needed to talk to the RawImage!
using TMPro;
using System.IO;

public class ClipUIItem : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI clipTitleText;
    public RawImage previewImage; // The white box where the thumbnail goes

    private string fullFilePath;
    private ComputerUIManager uiManager;
    private Texture2D thumbnailTexture;

    public void Setup(string filePath, ComputerUIManager manager)
    {
        fullFilePath = filePath;
        uiManager = manager;

        // 1. Set the Text
        if (clipTitleText != null)
        {
            clipTitleText.text = Path.GetFileNameWithoutExtension(filePath);
        }

        // 2. Generate the Thumbnail
        LoadThumbnail();
    }

    private void LoadThumbnail()
    {
        if (previewImage == null || !File.Exists(fullFilePath)) return;

        try
        {
            // Open the .tape file to read the binary data
            using (BinaryReader reader = new BinaryReader(File.Open(fullFilePath, FileMode.Open)))
            {
                int frameCount = reader.ReadInt32(); // How many frames total?

                if (frameCount > 0)
                {
                    // Peek at the very first frame
                    int frameSize = reader.ReadInt32();
                    byte[] frameBytes = reader.ReadBytes(frameSize);

                    // Convert those bytes into an actual image
                    thumbnailTexture = new Texture2D(2, 2);
                    thumbnailTexture.LoadImage(frameBytes);
                    thumbnailTexture.Apply();

                    // Put the image on the UI card
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
        uiManager.OpenPlayerView(fullFilePath);
    }

    public void OnDeleteButtonClicked()
    {
        uiManager.DeleteClip(fullFilePath);
    }

    private void OnDestroy()
    {
        // CRITICAL: When the UI card is destroyed (or the grid refreshes), 
        // we MUST delete the thumbnail from the computer's RAM!
        if (thumbnailTexture != null)
        {
            Destroy(thumbnailTexture);
        }
    }
}