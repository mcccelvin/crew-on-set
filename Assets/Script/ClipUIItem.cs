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

    private void Awake()
    {
        // GridLayoutGroup assigns the slot size, but does not clear prefab scale.
        // Bake the legacy artwork scale into its children so the root hit area
        // occupies one slot instead of covering all neighbouring recordings.
        RectTransform rect = transform as RectTransform;
        if (rect == null || rect.localScale == Vector3.one) return;
        Vector3 artworkScale = rect.localScale;
        foreach (Transform child in rect)
        {
            if (!(child is RectTransform childRect)) continue;
            childRect.anchoredPosition = Vector2.Scale(childRect.anchoredPosition,
                new Vector2(artworkScale.x, artworkScale.y));
            childRect.localScale = Vector3.Scale(childRect.localScale, artworkScale);
        }
        rect.localScale = Vector3.one;
    }

    public void Setup(string filePath, ComputerUIManager manager)
    {
        fullFilePath = filePath;
        uiManager = manager;

        if (clipTitleText != null)
        {
            clipTitleText.text = Path.GetFileNameWithoutExtension(filePath);
            clipTitleText.raycastTarget = false;
        }

        // The card body opens this card's recording too. All listeners are
        // replaced when a pooled card is assigned a different SD-card file.
        Graphic cardGraphic = GetComponent<Graphic>();
        Button cardButton = GetComponent<Button>();
        if (cardGraphic != null)
        {
            cardGraphic.raycastTarget = true;
            if (cardButton == null) cardButton = gameObject.AddComponent<Button>();
            cardButton.targetGraphic = cardGraphic;
            BindPlay(cardButton);
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
            BindPlay(clipButton);
        }
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button == clipButton || button == cardButton) continue;
            if (button.name.Equals("Play", System.StringComparison.OrdinalIgnoreCase)) BindPlay(button);
            else if (button.name.Equals("Delete", System.StringComparison.OrdinalIgnoreCase))
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(OnDeleteButtonClicked);
                button.interactable = true;
            }
        }

        // The authored decorative RawImage is drawn above the thumbnail.
        // Keep only actual controls as raycast targets; artwork must not swallow clicks.
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == cardGraphic || graphic == previewImage) continue;
            Button owner = graphic.GetComponentInParent<Button>();
            graphic.raycastTarget = owner != null && owner != cardButton;
        }

        LoadThumbnail();
    }

    private void BindPlay(Button button)
    {
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(OnPlayButtonClicked);
        button.interactable = true;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
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
