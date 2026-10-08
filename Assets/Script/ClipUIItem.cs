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
    private Button renameButton;
    private TextMeshProUGUI sourceCardLabel;

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
        SetSourceCard(0); // Pooled cards must not retain a previous video's source.

        if (clipTitleText != null)
        {
            LayoutFooter();
            clipTitleText.text = Path.GetFileNameWithoutExtension(filePath);
            clipTitleText.raycastTarget = false;
        }

        // The card body opens this card's recording too. All listeners are
        // replaced when a pooled card is assigned a different SD-card file.
        var paper = transform.Find("Clean Footer");
        Graphic cardGraphic = paper != null ? paper.GetComponent<Graphic>() : GetComponent<Graphic>();
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
            else if (button == renameButton)
            {
                button.onClick = new Button.ButtonClickedEvent();
                button.onClick.AddListener(() => uiManager.RenameClip(fullFilePath));
                button.interactable = true;
            }
        }

        // The authored decorative RawImage is drawn above the thumbnail.
        // Keep only actual controls as raycast targets; artwork must not swallow clicks.
        foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic == cardGraphic || graphic == previewImage) continue;
            Button owner = graphic.GetComponentInParent<Button>();
            graphic.raycastTarget = owner != null && owner != cardButton && graphic == owner.targetGraphic;
        }

        LoadThumbnail();
    }

    public void SetSourceCard(int number)
    {
        if (number <= 0)
        {
            if (sourceCardLabel != null) sourceCardLabel.transform.parent.gameObject.SetActive(false);
            return;
        }
        if (sourceCardLabel == null)
        {
            var badge = new GameObject("SD Card Source", typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(transform, false);
            SetCardRect((RectTransform)badge.transform, .085f, .105f, .40f, .18f);
            CrewPaperStyle.Card(badge.GetComponent<Image>());
            badge.GetComponent<Image>().color = CrewPaperStyle.Gold;
            FooterActionLabel(badge.transform, "Source Label", "", 16);
            sourceCardLabel = badge.GetComponentInChildren<TextMeshProUGUI>();
            sourceCardLabel.color = CrewPaperStyle.Ink;
        }
        sourceCardLabel.text = "SD CARD " + number;
        sourceCardLabel.transform.parent.gameObject.SetActive(true);
        sourceCardLabel.transform.parent.SetAsLastSibling();
    }

    private void LayoutFooter()
    {
        // Keep the original blue artwork intact; draw paper chrome at runtime.
        var cardGraphic = GetComponent<Graphic>();
        if (cardGraphic != null) cardGraphic.color = Color.clear;
        foreach (var artwork in GetComponentsInChildren<RawImage>(true))
            if (artwork != previewImage && artwork != cardGraphic) artwork.enabled = false;
        var footer = transform.Find("Clean Footer");
        if (footer == null)
        {
            footer = new GameObject("Clean Footer", typeof(RectTransform), typeof(Image)).transform;
            footer.SetParent(transform, false);
        }
        SetCardRect((RectTransform)footer, .055f, .055f, .945f, .94f);
        CrewPaperStyle.Card(footer.GetComponent<Image>());
        footer.SetAsFirstSibling();
        if (previewImage != null)
        {
            SetCardRect(previewImage.rectTransform, .075f, .33f, .925f, .915f);
            previewImage.color = Color.white;
        }

        // Filename has its own row, above the actions, with no shared space.
        SetCardRect(clipTitleText.rectTransform, .085f, .225f, .915f, .315f);
        clipTitleText.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? clipTitleText.font;
        clipTitleText.richText = false;
        clipTitleText.fontSize = 22;
        clipTitleText.enableAutoSizing = true;
        clipTitleText.fontSizeMin = 16;
        clipTitleText.fontSizeMax = 22;
        clipTitleText.margin = Vector4.zero;
        clipTitleText.color = CrewPaperStyle.Ink;
        clipTitleText.enableWordWrapping = false;
        clipTitleText.overflowMode = TextOverflowModes.Ellipsis;
        clipTitleText.alignment = TextAlignmentOptions.MidlineLeft;
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button.name.Equals("Play", System.StringComparison.OrdinalIgnoreCase))
                button.gameObject.SetActive(false); // The full thumbnail opens the clip.
            if (button.name.Equals("Delete", System.StringComparison.OrdinalIgnoreCase))
            {
                SetCardRect((RectTransform)button.transform, .755f, .09f, .915f, .195f);
                FooterActionLabel(button.transform, "Delete Label", "DELETE", 16);
                CrewPaperStyle.ActionButton(button, true);
            }
        }
        clipTitleText.transform.SetAsLastSibling();
        var delete = transform.Find("Delete");
        if (delete != null) delete.SetAsLastSibling();
        if (renameButton == null)
        {
            var rename = new GameObject("Rename", typeof(RectTransform), typeof(Image), typeof(Button));
            rename.transform.SetParent(transform, false);
            renameButton = rename.GetComponent<Button>();
        }
        SetCardRect((RectTransform)renameButton.transform, .575f, .09f, .735f, .195f);
        FooterActionLabel(renameButton.transform, "Edit Label", "EDIT", 16);
        CrewPaperStyle.ActionButton(renameButton);
    }

    private void FooterActionLabel(Transform parent, string name, string caption, int size)
    {
        var label = parent.Find(name);
        if (label == null)
        {
            label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).transform;
            label.SetParent(parent, false);
        }
        var rect = (RectTransform)label;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6, 3); rect.offsetMax = new Vector2(-6, -3);
        var text = label.GetComponent<TextMeshProUGUI>();
        text.font = clipTitleText.font; text.text = caption; text.fontSize = size;
        text.margin = Vector4.zero; text.enableWordWrapping = false;
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = size;
        text.color = CrewPaperStyle.Ink; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
    }

    private static void SetCardRect(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
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
        if (uiManager != null) uiManager.DeleteClip(fullFilePath);
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
