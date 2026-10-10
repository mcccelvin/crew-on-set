using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class ComputerUIManager
{
    private GameObject fileDialog;
    private TextMeshProUGUI storageLabel, emptyRecordingsLabel;
    private Button ejectCardsButton;
    private GameObject ejectMenu;
    private readonly Vector3[] ejectButtonCorners = new Vector3[4];
    private int fileDialogClosedFrame = -2;
    public bool BlocksCloseShortcut => (fileDialog != null && fileDialog.activeInHierarchy) ||
        (ejectMenu != null && ejectMenu.activeInHierarchy) ||
        Time.frameCount <= fileDialogClosedFrame + 1;

    public void RenameClip(string filePath) { OpenFileDialog(filePath, true); }

    private void OnDisable() { CloseEjectMenu(); CloseFileDialog(); }

    private void CloseEjectMenu()
    {
        if (ejectMenu == null) return;
        fileDialogClosedFrame = Time.frameCount;
        ejectMenu.SetActive(false); Destroy(ejectMenu); ejectMenu = null;
    }

    private void CloseFileDialog()
    {
        if (fileDialog == null) return;
        // Don't let the key used while editing leak into gameplay on dismissal.
        fileDialogClosedFrame = Time.frameCount;
        fileDialog.SetActive(false);
        Destroy(fileDialog);
        fileDialog = null;
    }

    private void OpenFileDialog(string path, bool rename)
    {
        CloseEjectMenu();
        CloseFileDialog();
        if (physicalComputer == null) physicalComputer = FindObjectOfType<ComputerStation>();
        string name = Path.GetFileName(path);
        if (physicalComputer == null || !physicalComputer.GetInsertedFiles().Exists(x => x.fileName == name))
        { GameFeedback.Show("Reinsert the SD card for this recording."); return; }

        fileDialog = new GameObject("Recording File Dialog", typeof(RectTransform), typeof(Image));
        fileDialog.transform.SetParent(transform, false);
        FileRect(fileDialog, Vector2.zero, Vector2.one);
        fileDialog.GetComponent<Image>().color = new Color(0, 0, 0, .68f);
        fileDialog.transform.SetAsLastSibling();
        var panel = new GameObject("File Window", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(fileDialog.transform, false);
        FileRect(panel, new Vector2(.27f, .29f), new Vector2(.73f, .71f));
        panel.GetComponent<Image>().color = new Color(.09f, .14f, .23f);
        FileLabel(panel.transform, "Heading", rename ? "RENAME RECORDING" : "DELETE RECORDING?", new Vector2(.055f, .78f), new Vector2(.95f, .93f), 30);
        FileLabel(panel.transform, "Description", rename ? "Choose a file name (up to 48 characters)." : "This removes the raw clip from its SD card and frees recording space. This cannot be undone.",
            new Vector2(.055f, .55f), new Vector2(.95f, .76f), 22);
        var errorLabel = FileLabel(panel.transform, "Message", "", new Vector2(.055f, .23f), new Vector2(.95f, .36f), 19);
        errorLabel.color = new Color(1, .71f, .43f);
        TMP_InputField input = null;
        if (rename)
        {
            var field = new GameObject("Recording Name", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            field.transform.SetParent(panel.transform, false);
            FileRect(field, new Vector2(.055f, .39f), new Vector2(.95f, .54f));
            field.GetComponent<Image>().color = new Color(.97f, .95f, .86f);
            var viewport = new GameObject("Name Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(field.transform, false);
            FileRect(viewport, new Vector2(.025f, .05f), new Vector2(.975f, .95f));
            var text = FileLabel(viewport.transform, "Name Text", "", Vector2.zero, Vector2.one, 24);
            text.color = new Color(.12f, .15f, .2f);
            text.enableWordWrapping = false;
            input = field.GetComponent<TMP_InputField>();
            input.textViewport = (RectTransform)viewport.transform;
            input.textComponent = text;
            input.characterLimit = 48;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = Path.GetFileNameWithoutExtension(name);
            input.caretColor = text.color;
            input.customCaretColor = true;
            input.ActivateInputField();
        }
        else
        {
            var title = FileLabel(panel.transform, "File Name", Path.GetFileNameWithoutExtension(name), new Vector2(.055f, .39f), new Vector2(.95f, .54f), 24);
            title.enableWordWrapping = false;
            title.overflowMode = TextOverflowModes.Ellipsis;
        }
        var cancel = FileButton(panel.transform, "Cancel", "CANCEL", new Vector2(.48f, .065f), new Vector2(.69f, .205f), false);
        cancel.onClick.AddListener(CloseFileDialog);
        var confirm = FileButton(panel.transform, "Confirm", rename ? "SAVE NAME" : "DELETE", new Vector2(.715f, .065f), new Vector2(.95f, .205f), !rename);
        confirm.onClick.AddListener(() =>
        {
            if (pixelPlayer != null) pixelPlayer.StopTape();
            replayStartedByPlayer = false;
            string error;
            bool success = rename ? physicalComputer.TryRenameClip(name, input.text, out error) : physicalComputer.TryDeleteClip(name, out error);
            if (!success) { errorLabel.text = error; return; }
            CloseFileDialog();
            RefreshGrid();
            GameFeedback.Show(rename ? "Recording renamed." : "Recording deleted. SD-card space freed.");
        });
    }

    private void RefreshStorageHeader(int visibleClips)
    {
        if (recordingsGridPanel == null) return;
        if (storageLabel == null)
        {
            storageLabel = FileLabel(recordingsGridPanel.transform, "SD Card Capacity", "", new Vector2(.28f, .789f), new Vector2(.745f, .835f), 24);
            storageLabel.enableWordWrapping = false;
            storageLabel.overflowMode = TextOverflowModes.Ellipsis;
            ejectCardsButton = FileButton(recordingsGridPanel.transform, "Eject SD Cards", "EJECT SD", new Vector2(.755f, .787f), new Vector2(.855f, .835f), false);
            CrewPaperStyle.ActionButton(ejectCardsButton, primary: true);
            ejectCardsButton.onClick.AddListener(ToggleEjectMenu);
            emptyRecordingsLabel = FileLabel(recordingsGridPanel.transform, "No Recordings", "NO RECORDINGS\nInsert a recorded SD card to view its clips.", new Vector2(.33f, .39f), new Vector2(.8f, .65f), 28);
            emptyRecordingsLabel.alignment = TextAlignmentOptions.Center;
        }
        storageLabel.text = physicalComputer != null ? physicalComputer.StorageSummary() : "Insert an SD card to view its recordings";
        ejectCardsButton.interactable = physicalComputer != null && physicalComputer.HasInsertedCards;
        emptyRecordingsLabel.gameObject.SetActive(visibleClips == 0);
    }

    private bool CanEjectSelectedCard()
    {
        if (TutorialManager.Instance == null || TutorialManager.Instance.currentStep >= TutorialManager.TutorialStep.OfferLevel1) return true;
        GameFeedback.Show("Keep your card inserted until the editing lesson is complete.");
        return false;
    }

    private void ToggleEjectMenu()
    {
        if (ejectMenu != null) { CloseEjectMenu(); return; }
        if (!CanEjectSelectedCard()) return;
        if (physicalComputer == null) physicalComputer = FindObjectOfType<ComputerStation>();
        if (physicalComputer == null || !physicalComputer.HasInsertedCards) return;
        var cards = physicalComputer.GetInsertedCards();
        if (cards.Count == 0) return;
        ejectMenu = new GameObject("SD Card Eject Dropdown", typeof(RectTransform), typeof(Image), typeof(Button));
        ejectMenu.transform.SetParent(recordingsGridPanel.transform, false);
        FileRect(ejectMenu, Vector2.zero, Vector2.one);
        var outside = ejectMenu.GetComponent<Image>(); outside.color = new Color(0, 0, 0, .001f);
        var outsideButton = ejectMenu.GetComponent<Button>(); outsideButton.targetGraphic = outside;
        outsideButton.transition = Selectable.Transition.None; outsideButton.onClick.AddListener(CloseEjectMenu);
        ejectMenu.transform.SetAsLastSibling();
        var panel = new GameObject("Choose SD Card", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(ejectMenu.transform, false);
        var panelRect = (RectTransform)panel.transform;
        const float width = 420, rowHeight = 64;
        float height = 52 + Mathf.Min(cards.Count, 6) * rowHeight;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1, 1);
        panelRect.sizeDelta = new Vector2(width, height);
        ((RectTransform)ejectCardsButton.transform).GetWorldCorners(ejectButtonCorners);
        var bounds = (RectTransform)ejectMenu.transform;
        Vector3 position = bounds.InverseTransformPoint(ejectButtonCorners[3]);
        position.x = Mathf.Clamp(position.x, bounds.rect.xMin + width + 12, bounds.rect.xMax - 12);
        position.y = Mathf.Clamp(position.y - 12, bounds.rect.yMin + height + 12, bounds.rect.yMax - 12);
        panelRect.position = bounds.TransformPoint(position);
        CrewPaperStyle.Card(panel.GetComponent<Image>());
        var heading = FileLabel(panel.transform, "Heading", "CHOOSE SD CARD TO EJECT", Vector2.zero, Vector2.one, 18);
        heading.color = CrewPaperStyle.Ink;
        var headingRect = heading.rectTransform; headingRect.anchorMin = headingRect.anchorMax = headingRect.pivot = new Vector2(0, 1);
        headingRect.anchoredPosition = new Vector2(16, -10); headingRect.sizeDelta = new Vector2(width - 32, 28);
        var viewport = new GameObject("Card List", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        viewport.transform.SetParent(panel.transform, false);
        FileRect(viewport, Vector2.zero, Vector2.one);
        ((RectTransform)viewport.transform).offsetMin = new Vector2(12, 12);
        ((RectTransform)viewport.transform).offsetMax = new Vector2(-12, -42);
        var content = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport.transform, false);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = new Vector2(0, cards.Count * rowHeight);
        var scroll = viewport.GetComponent<ScrollRect>(); scroll.content = content; scroll.viewport = (RectTransform)viewport.transform;
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var button = FileButton(content, "Eject " + card.DisplayName, card.DisplayName.ToUpperInvariant(), Vector2.zero, Vector2.one, false);
            var row = (RectTransform)button.transform;
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(.5f, 1);
            row.anchoredPosition = new Vector2(0, -i * rowHeight); row.sizeDelta = new Vector2(0, 56);
            var title = button.GetComponentInChildren<TMP_Text>();
            title.alignment = TextAlignmentOptions.MidlineLeft; title.fontSize = title.fontSizeMax = 20;
            title.rectTransform.anchorMin = new Vector2(.035f, .45f); title.rectTransform.anchorMax = new Vector2(.965f, .95f);
            title.rectTransform.offsetMin = title.rectTransform.offsetMax = Vector2.zero;
            var detail = FileLabel(button.transform, "Capacity", $"{card.GetRecordings().Count} clips  |  {card.UsedSeconds:0.#} / 60s used",
                new Vector2(.035f, .06f), new Vector2(.965f, .43f), 16);
            detail.color = CrewPaperStyle.MutedInk; detail.enableWordWrapping = false;
            button.onClick.AddListener(() => EjectSelectedCard(card));
        }
    }

    private void EjectSelectedCard(Player.Equipment.SDCardItem card)
    {
        if (!CanEjectSelectedCard()) return;
        string name = card != null ? card.DisplayName : "SD card";
        pixelPlayer?.StopTape(); replayStartedByPlayer = false;
        bool ejected = physicalComputer != null && physicalComputer.TryEjectCard(card);
        CloseEjectMenu(); RefreshGrid();
        GameFeedback.Show(ejected ? name + " ejected. Other cards stay inserted." : "That SD card is no longer inserted.");
    }

    private static void FileRect(GameObject obj, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)obj.transform;
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static TextMeshProUGUI FileLabel(Transform parent, string name, string text, Vector2 min, Vector2 max, int size)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        FileRect(obj, min, max);
        var label = obj.GetComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset;
        label.text = text; label.fontSize = size; label.color = new Color(.96f, .95f, .91f);
        label.richText = false; label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        return label;
    }

    private static Button FileButton(Transform parent, string name, string text, Vector2 min, Vector2 max, bool danger)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        FileRect(obj, min, max);
        var label = FileLabel(obj.transform, "Label", text, new Vector2(.04f, .04f), new Vector2(.96f, .96f), 22);
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 22;
        var button = obj.GetComponent<Button>();
        CrewPaperStyle.ActionButton(button, danger);
        return button;
    }
}
