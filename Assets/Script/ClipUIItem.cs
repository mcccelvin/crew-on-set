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

    public void Setup(string filePath, ComputerUIManager manager)
    {
        fullFilePath = filePath;
        uiManager = manager;

        if (clipTitleText != null)
        {
            clipTitleText.text = Path.GetFileNameWithoutExtension(filePath);
        }

        LoadThumbnail();
    }

    private void LoadThumbnail()
    {
        if (previewImage == null || !File.Exists(fullFilePath)) return;

        try
        {
            // THE FIX: Shared Read Access so it doesn't fight the player!
            using (BinaryReader reader = new BinaryReader(new FileStream(fullFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                int frameCount = reader.ReadInt32();

                if (frameCount > 0)
                {
                    int frameSize = reader.ReadInt32();
                    byte[] frameBytes = reader.ReadBytes(frameSize);

                    thumbnailTexture = new Texture2D(2, 2);
                    thumbnailTexture.LoadImage(frameBytes);
                    thumbnailTexture.Apply();

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
        if (thumbnailTexture != null)
        {
            Destroy(thumbnailTexture);
        }
    }
}