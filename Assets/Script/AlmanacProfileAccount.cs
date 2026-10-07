using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AlmanacManager
{
    private bool profileMenuOnly, profileInputsDirty;
    private GameSaveSlot profileMenuSlot;
    private Action profileMenuClosed;
    private TMP_InputField profileNameInput, profileBioInput;
    private TMP_InputField profileNewUsernameInput, profileCurrentPasswordInput;
    private TMP_Text profileSyncStatus, profileWalletStatus, profileShopBalance;
    private Button profileSaveButton, profileShopButton, profileReturnStats, profileChangeUsernameButton;
    private GameObject profileUsernameChangeOverlay;
    private TMP_Text profileUsernameChangeStatus;
    private GameObject profileShopPanel;
    private Transform profileShopContent;
    private CCoinService profileWallet;
    private string profileInputOwner;
    private int profileShopCategory = 0;
    private static readonly string[] ProfileShopKinds = { "all", "face", "hair", "shirt", "pants", "shoe", "accessory" };
    private AccountProductionProfile.Snapshot profileAccountStats;
    private Button profileAccountSyncButton;
    private TMP_Text profileProgressSyncStatus;
    private bool profileAccountSyncRequested;
    private bool ProfileInputFocused => (profileNameInput != null && profileNameInput.isFocused) || (profileBioInput != null && profileBioInput.isFocused);

    private AccountProductionProfile.Snapshot ProfileSnapshot()
    {
        AccountProfileData.EnsureBound();
        if(profileAccountStats!=null && profileAccountStats.owner==AccountProfileData.Owner)return profileAccountStats;
        var saves=GameSaveManager.Ensure();
        try { saves.OpenRepository(); } catch(Exception) { }
        return profileAccountStats=AccountProductionProfile.Capture(AccountProfileData.Owner,
            saves.Repository?.Slots,saves.Active,GameSavePrefs.Values);
    }
    private CareerProfileProgress.Record ProfileRecord() => ProfileSnapshot().record;
    private List<PlayerAnalytics.Attempt> ProfileHistory() => ProfileSnapshot().history;
    private int ProfilePreference(string key) => ProfileSnapshot().Preference(key);
    public void OpenMenuProfile(Action closed)
    {
        profileClosing = false;
        profileMenuOnly = true; profileMenuClosed = closed; profileAccountStats=null;profileAccountSyncRequested=false;
        var saves = GameSaveManager.Ensure();
        try
        {
            saves.OpenRepository();
            var slots = saves.Repository.Slots.FindAll(s => s.Int("SaveDeleted", 0) == 0);
            slots.Sort((a, b) => string.CompareOrdinal(b.updatedUtc, a.updatedUtc));
            profileMenuSlot = slots.Count == 0 ? null : slots[0];
        }
        catch (Exception) { profileMenuSlot = null; }
        BuildProfileUI(); ApplyAccountProfileStyle();
        CaptureInputState(); isProfileOpen = true; profileInputsDirty = false;
        RefreshAllUI(); RefreshAccountFields(true); OpenTab(0);
        UITransition.Show(profileCanvas);
        var card = profileCanvas.transform.Find("Profile backdrop/Profile stage/Career card");
        if (card != null) UITransition.ConfigurePanel(card.gameObject);
        profileInputsDirty = false; RefreshAccountFields(true); RenderProfileCharacter();
        AccountProfileData.Refresh(); CCoinService.Ensure().Refresh();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void NextProfileCareer()
    {
        // Compatibility for an older baked button; profiles no longer select careers.
        profileAccountStats=null;
        RefreshAllUI();
    }
    private void StyleProfileSideTab(Button button, int index)
    {
        SetRect(button.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(92, 255 - index * 244), new Vector2(82, 210));
        button.GetComponent<Image>().sprite = null; button.GetComponent<Image>().color = Color.clear;
        var colors = button.colors; colors.normalColor = colors.highlightedColor = colors.pressedColor = colors.selectedColor = colors.disabledColor = Color.white; button.colors = colors;
        foreach (var text in button.GetComponentsInChildren<TMP_Text>(true)) text.gameObject.SetActive(false);
    }
    private void EnsureSharedProfileUI()
    {
        if (profileCanvas == null || playerInfoPanel == null) return;
        var stage = profileCanvas.transform.Find("Profile backdrop/Profile stage");
        if (stage == null) return;
        AccountProfileData.EnsureBound();
        if (profileNameInput == null)
        {
            profileNameInput = ProfileInput("Shared account name", playerInfoPanel.transform, new Vector2(60, 235), new Vector2(640, 66), false);
            profileBioInput = ProfileInput("Shared account Bio", playerInfoPanel.transform, new Vector2(60, -165), new Vector2(640, 360), true);
            profileChangeUsernameButton = BookButton(playerInfoPanel.transform, "Change website username", "CHANGE", "blueButton", new Vector2(250, 159), new Vector2(140, 58));
            profileChangeUsernameButton.onClick.AddListener(OpenUsernameChangePrompt);
            profileSyncStatus = BookText(stage, "Account sync status", new Vector2(475, -367), new Vector2(805, 36), 20);
            FeedbackTypography.Apply(profileSyncStatus); profileSyncStatus.color = ProfileMuted; profileSyncStatus.richText = false;
            profileSyncStatus.enableAutoSizing = true; profileSyncStatus.fontSizeMin = 16; profileSyncStatus.fontSizeMax = 20;
        }
        playerNameText.gameObject.SetActive(false);
        if (profileSaveButton == null)
        {
            profileSaveButton = BookButton(stage, "Save account profile", "", "profileSave", new Vector2(340, -425), new Vector2(235, 94));
            profileSaveButton.onClick.AddListener(SaveAccountProfile);
            var account = BookButton(playerInfoPanel.transform, "Account sign in or logout", "LOG OUT", "redButton", new Vector2(325, 135), new Vector2(160, 46));
            account.onClick.AddListener(() =>
            {
                GameSaveManager.Instance?.SaveCheckpoint();
                if (!GameSaveManager.Ensure().HasCloudSession) GameSaveManager.Ensure().SignInToSync();
                else GameSaveManager.Ensure().Logout();
            });
        }
        EnsureProfileIdentityLayout();
        if (profileShopButton == null)
        {
            profileShopButton = ProfileTab(stage, "Shop sidebar tab", "SHOP", Vector2.zero);
            StyleProfileSideTab(profileShopButton, 2); profileShopButton.onClick.AddListener(OpenProfileShop);
        }
        if (profileShopPanel == null) BuildProfileShop(stage);
        SetStretchRect(achievementsPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(100, 20), new Vector2(-24, -20));
        achievementsPanel.GetComponent<Image>().color = new Color32(252, 245, 220, 255);
        SetRect(achievementsTabBtn.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(340, -425), new Vector2(265, 72));
        ExportUIArt.Apply(achievementsTabBtn.GetComponent<Image>(), "blueButton");
        var achievementLabel = achievementsTabBtn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (achievementLabel != null)
        {
            achievementLabel.gameObject.SetActive(true); achievementLabel.text = "ACHIEVEMENTS";
            achievementLabel.enableAutoSizing = true; achievementLabel.fontSizeMin = 16; achievementLabel.fontSizeMax = 24;
            if (achievementLabel.fontSharedMaterial == null || !achievementLabel.fontSharedMaterial.IsKeywordEnabled("OUTLINE_ON")) ExportUIArt.OutlineText(achievementLabel);
        }
        if (profileReturnStats == null)
        {
            profileReturnStats = BookButton(stage, "Back to skill statistics", "STATS", "blueButton", new Vector2(340, -425), new Vector2(235, 72));
            profileReturnStats.onClick.AddListener(OpenStatsTab); profileReturnStats.gameObject.SetActive(false);
        }
        var oldCareerButton=stage.Find("Choose profile career");
        if(oldCareerButton!=null)oldCareerButton.gameObject.SetActive(false);
        var oldCareerBadge=stage.Find("Career save badge");
        if(oldCareerBadge!=null)oldCareerBadge.gameObject.SetActive(false);
        // Login, checkpoints, wallet and saved appearance already sync automatically.
        // Do not create a manual control; also retire older authored/runtime copies.
        profileAccountSyncButton=stage.Find("Sync account progress")?.GetComponent<Button>();
        if(profileAccountSyncButton!=null)profileAccountSyncButton.gameObject.SetActive(false);
        profileProgressSyncStatus=stage.Find("Progress and wallet sync status")?.GetComponent<TMP_Text>();
        var statusPaper=stage.Find("Account sync paper");
        if(statusPaper!=null)statusPaper.gameObject.SetActive(false);
        if(profileSyncStatus!=null)profileSyncStatus.gameObject.SetActive(false);
        if(profileProgressSyncStatus!=null)profileProgressSyncStatus.gameObject.SetActive(false);
        if (GameSaveManager.Instance != null)
        { GameSaveManager.Instance.Changed -= OnProfileSavesChanged; GameSaveManager.Instance.Changed += OnProfileSavesChanged; }
        if (playerInfoTabBtn.onClick.GetPersistentEventCount() == 0)
        { playerInfoTabBtn.onClick.RemoveListener(OpenPlayerInfoTab); playerInfoTabBtn.onClick.AddListener(OpenPlayerInfoTab); }
        if (achievementsTabBtn.onClick.GetPersistentEventCount() == 0)
        { achievementsTabBtn.onClick.RemoveListener(OpenAchievementsTab); achievementsTabBtn.onClick.AddListener(OpenAchievementsTab); }
        AccountProfileData.Changed -= OnAccountProfileChanged; AccountProfileData.Changed += OnAccountProfileChanged;
        if (profileWallet == null) { profileWallet = CCoinService.Ensure(); profileWallet.Changed += OnProfileWalletChanged; }
        foreach (var button in new[] { profileSaveButton, profileBackButton, profileCloseButton, profileReturnStats, achievementsTabBtn,profileAccountSyncButton,
            stage.Find("Buy account C-Coins")?.GetComponent<Button>(), playerInfoPanel.transform.Find("Account sign in or logout")?.GetComponent<Button>() })
        {
            if (button == null) continue;
            var colors = button.colors; colors.normalColor = colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(1, 1, 1, 1); colors.pressedColor = new Color(.85f, .85f, .85f, 1); colors.disabledColor = new Color(.65f, .65f, .65f, 1); button.colors = colors;
        }
        RefreshAccountFields();
    }

    private void EnsureProfileIdentityLayout()
    {
        // Cover only the old form artwork: its border and illustrated sidebar stay intact.
        // Reuse this layer when reopening an authored profile; never rebuild an input or its draft.
        var paper = playerInfoPanel.transform.Find("Crew identity paper");
        if (paper == null)
        {
            paper = CreatePanel("Crew identity paper", playerInfoPanel.transform, new Color32(252, 245, 220, 255)).transform;
            SetRect(paper.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(50, 0), new Vector2(712, 672));
            paper.GetComponent<Image>().raycastTarget = false;
            paper.SetAsFirstSibling();

            var header = CreatePanel("Crew file header", paper, new Color32(88, 57, 36, 255)).transform;
            SetRect(header.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, 280), new Vector2(664, 72));
            header.GetComponent<Image>().raycastTarget = false;
            ProfileIdentityLabel(header, "Crew file heading", "CREW PROFILE", new Vector2(-76, 0), new Vector2(468, 50), 32);
            var mark = ProfileIdentityLabel(header, "Production mark", "CREW-ON-SET", new Vector2(215, 0), new Vector2(192, 42), 17);
            mark.alignment = TextAlignmentOptions.Right;

            ProfileIdentityLabel(paper, "Display name heading", "DISPLAY NAME", new Vector2(0, 211), new Vector2(664, 30), 21);
            ProfileIdentityLabel(paper, "Biography heading", "ABOUT YOU", new Vector2(0, 96), new Vector2(664, 30), 21);
            ProfileIdentityLabel(paper, "Biography hint", "Your creative interests, skills and story.", new Vector2(0, -132), new Vector2(664, 30), 19).color = ProfileMuted;

            var account = CreatePanel("Account details paper", paper, new Color32(237, 222, 191, 255)).transform;
            SetRect(account.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(0, -239), new Vector2(664, 152));
            account.GetComponent<Image>().raycastTarget = false;
            var accent = CreatePanel("Account copper edge", account, new Color32(153, 99, 52, 255));
            SetRect(accent.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(-328, 0), new Vector2(8, 152));
            accent.GetComponent<Image>().raycastTarget = false;
            ProfileIdentityLabel(account, "Account heading", "PLAYFAB ACCOUNT", new Vector2(0, 45), new Vector2(612, 30), 21);
            ProfileIdentityLabel(account, "Account ID caption", "PLAYER ID", new Vector2(-64, 9), new Vector2(484, 26), 17).color = ProfileMuted;
        }
        StyleProfileIdentityInput(profileNameInput, new Vector2(-65, 159), new Vector2(450, 64), "Your website username", 29);
        StyleProfileIdentityInput(profileBioInput, new Vector2(50, -18), new Vector2(664, 172), "Tell your crew a little about yourself…", 24);
        profileNameInput.readOnly = true;
        if (profileAccountId != null)
        {
            SetRect(profileAccountId.rectTransform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(-43, -267), new Vector2(426, 42));
            profileAccountId.alignment = TextAlignmentOptions.Left;
            profileAccountId.enableWordWrapping = false;
            profileAccountId.fontSize = profileAccountId.fontSizeMax = 23;
            profileAccountId.fontSizeMin = 18;
        }
        var accountButton = playerInfoPanel.transform.Find("Account sign in or logout");
        if (accountButton != null)
            SetRect(accountButton.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, new Vector2(291, -266), new Vector2(166, 52));
    }

    private TMP_Text ProfileIdentityLabel(Transform parent, string name, string label, Vector2 position, Vector2 size, float fontSize)
    {
        var text = CreateText(name, parent, label, fontSize, TextAlignmentOptions.Left);
        SetRect(text.rectTransform, Vector2.one * .5f, Vector2.one * .5f, position, size);
        text.color = ProfileInk;
        text.richText = false;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.fontSizeMax = fontSize;
        text.fontSizeMin = Mathf.Min(18, fontSize);
        return text;
    }

    private void StyleProfileIdentityInput(TMP_InputField input, Vector2 position, Vector2 size, string hint, float fontSize)
    {
        if (input == null) return;
        var rect = input.GetComponent<RectTransform>();
        SetRect(rect, Vector2.one * .5f, Vector2.one * .5f, position, size);
        var background = input.GetComponent<Image>();
        background.color = new Color32(255, 251, 239, 255);
        var edge = input.GetComponent<Outline>();
        if (edge == null) edge = input.gameObject.AddComponent<Outline>();
        edge.effectColor = new Color32(160, 123, 79, 255);
        edge.effectDistance = new Vector2(1.5f, -1.5f);
        SetStretchRect(input.textViewport, Vector2.zero, Vector2.one, new Vector2(22, 14), new Vector2(-22, -14));
        SetStretchRect(input.textComponent.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        input.textComponent.fontSize = fontSize;
        input.textComponent.enableAutoSizing = false;
        if (input.placeholder == null)
        {
            var placeholder = ProfileIdentityLabel(input.textViewport, "Input placeholder", hint, Vector2.zero, Vector2.zero, fontSize - 2);
            placeholder.color = ProfileMuted;
            placeholder.alignment = input.textComponent.alignment;
            placeholder.enableAutoSizing = false;
            SetStretchRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            input.placeholder = placeholder;
        }
        input.ForceLabelUpdate();
    }

    private TMP_InputField ProfileInput(string name, Transform parent, Vector2 position, Vector2 size, bool multiline)
    {
        var rect = CCoinShopUI.Rect(parent, name, position, size);
        var background = rect.gameObject.AddComponent<Image>(); background.color = Color.clear;
        var viewport = CCoinShopUI.Rect(rect, "Text viewport", Vector2.zero, size - new Vector2(24, 12));
        viewport.gameObject.AddComponent<RectMask2D>();
        var text = CCoinShopUI.Text(viewport, "Text", "", Vector2.zero, viewport.sizeDelta, multiline ? 24 : 30);
        text.richText = false; text.enableAutoSizing = false; text.alignment = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
        text.color = ProfileInk; text.overflowMode = TextOverflowModes.Overflow;
        var input = rect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = background;
        input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)text;
        input.characterLimit = multiline ? 500 : 25;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        input.onValueChanged.AddListener(value => profileInputsDirty = true);
        return input;
    }
    private void SaveAccountProfile()
    {
        profileAccountSyncRequested=false;
        if (AccountProfileData.Authenticated && AccountProfileData.SaveBio(profileBioInput.text))
        { profileInputsDirty = false; RefreshAccountFields(true); }
    }

    private void OpenUsernameChangePrompt()
    {
        if (!AccountProfileData.Authenticated) return;
        if (profileUsernameChangeOverlay == null) BuildUsernameChangePrompt();
        profileNewUsernameInput.SetTextWithoutNotify(AccountProfileData.Name);
        profileCurrentPasswordInput.SetTextWithoutNotify("");
        profileUsernameChangeStatus.text = "Enter your new username and current password to confirm.";
        profileUsernameChangeOverlay.SetActive(true);
        profileUsernameChangeOverlay.transform.SetAsLastSibling();
        profileNewUsernameInput.Select(); profileNewUsernameInput.ActivateInputField();
    }

    private void BuildUsernameChangePrompt()
    {
        profileUsernameChangeOverlay = CreatePanel("Username change confirmation", profileCanvas.transform, new Color32(8, 12, 20, 220));
        SetStretchRect(profileUsernameChangeOverlay.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var card = CreatePanel("Username change paper", profileUsernameChangeOverlay.transform, new Color32(252, 245, 220, 255)).transform;
        SetRect(card.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(780, 490));
        ProfileIdentityLabel(card, "Username change heading", "CHANGE USERNAME", new Vector2(0, 190), new Vector2(690, 52), 34);
        ProfileIdentityLabel(card, "Username change explanation", "Confirm with your current password. The new name syncs to your website profile and game account.", new Vector2(0, 142), new Vector2(690, 52), 19).color = ProfileMuted;
        ProfileIdentityLabel(card, "New username label", "NEW USERNAME", new Vector2(-260, 91), new Vector2(520, 30), 18);
        profileNewUsernameInput = DialogInput("New username", card, new Vector2(0, 48), new Vector2(600, 58), "3–20 characters");
        profileNewUsernameInput.characterLimit = 20;
        ProfileIdentityLabel(card, "Current password label", "CURRENT PASSWORD", new Vector2(-260, -15), new Vector2(520, 30), 18);
        profileCurrentPasswordInput = DialogInput("Current password", card, new Vector2(0, -58), new Vector2(600, 58), "Enter current password");
        profileCurrentPasswordInput.contentType = TMP_InputField.ContentType.Password;
        profileCurrentPasswordInput.ForceLabelUpdate();
        profileUsernameChangeStatus = ProfileIdentityLabel(card, "Username change status", "", new Vector2(0, -115), new Vector2(690, 38), 17);
        profileUsernameChangeStatus.alignment = TextAlignmentOptions.Center;
        var confirm = BookButton(card, "Confirm username change", "SAVE", "blueButton", new Vector2(170, -195), new Vector2(190, 66));
        confirm.onClick.AddListener(SubmitUsernameChange);
        var cancel = BookButton(card, "Cancel username change", "CANCEL", "redButton", new Vector2(-170, -195), new Vector2(190, 66));
        cancel.onClick.AddListener(() => { profileCurrentPasswordInput.SetTextWithoutNotify(""); profileUsernameChangeOverlay.SetActive(false); });
        profileUsernameChangeOverlay.SetActive(false);
    }

    private TMP_InputField DialogInput(string name, Transform parent, Vector2 position, Vector2 size, string hint)
    {
        var rect = CCoinShopUI.Rect(parent, name, position, size);
        var background = rect.gameObject.AddComponent<Image>(); background.color = new Color32(255, 251, 239, 255);
        var edge = rect.gameObject.AddComponent<Outline>(); edge.effectColor = new Color32(160, 123, 79, 255); edge.effectDistance = new Vector2(1.5f, -1.5f);
        var viewport = CCoinShopUI.Rect(rect, "Text viewport", Vector2.zero, size - new Vector2(24, 12)); viewport.gameObject.AddComponent<RectMask2D>();
        var text = CCoinShopUI.Text(viewport, "Text", "", position, size - new Vector2(28, 16), 24);
        text.richText = false; text.enableAutoSizing = false; text.alignment = TextAlignmentOptions.Left; text.color = ProfileInk;
        var placeholder = ProfileIdentityLabel(viewport, "Input placeholder", hint, Vector2.zero, Vector2.zero, 21);
        placeholder.color = ProfileMuted; placeholder.enableAutoSizing = false; SetStretchRect(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var input = rect.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = background; input.textViewport = viewport;
        input.textComponent = text; input.placeholder = placeholder; input.characterLimit = 25;
        input.lineType = TMP_InputField.LineType.SingleLine; input.shouldHideMobileInput = true;
        SetStretchRect(viewport, Vector2.zero, Vector2.one, new Vector2(14, 7), new Vector2(-14, -7));
        SetStretchRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return input;
    }

    private void SubmitUsernameChange()
    {
        profileUsernameChangeStatus.text = "Verifying password and syncing username…";
        string password = profileCurrentPasswordInput.text;
        profileCurrentPasswordInput.SetTextWithoutNotify("");
        AccountProfileData.ChangeUsername(profileNewUsernameInput.text, password, (success, message) =>
        {
            if (profileUsernameChangeStatus == null) return;
            profileUsernameChangeStatus.text = message;
            if (!success) return;
            profileCurrentPasswordInput.SetTextWithoutNotify("");
            profileUsernameChangeOverlay.SetActive(false);
            profileInputsDirty = false; RefreshAccountFields(true);
        });
    }
    private void OnAccountProfileChanged()
    {
        if(this==null)return;
        if(profileAccountStats!=null && profileAccountStats.owner!=AccountProfileData.Owner)
        { profileAccountStats=null;profileAccountSyncRequested=false;if(isProfileOpen)RefreshAllUI(); }
        RefreshAccountFields();
    }
    private void SyncProfileAccount()
    {
        profileAccountSyncRequested=true;
        GameSaveManager.Ensure().SyncAccountProgress();
        profileAccountStats=null;RefreshAllUI();
    }
    private void OnProfileWalletChanged()
    {
        if(this==null)return;
        RefreshProfileShop();RefreshProfileSyncStatus();
        if(isProfileOpen){RefreshProfileStats();RenderProfileCharacter();}
    }
    private void RefreshProfileSyncStatus()
    {
        var saves=GameSaveManager.Instance;
        if(profileAccountSyncButton!=null)profileAccountSyncButton.interactable=!(saves?.Syncing ?? false) && !(profileWallet?.Busy ?? false);
        string progress=saves?.Status ?? "Stats are saved on this device.";
        string coins=profileWallet?.Status ?? "C-Coins: sign in to sync.";
        if(profileProgressSyncStatus!=null)profileProgressSyncStatus.text=progress;
        if(profileSyncStatus!=null && !profileInputsDirty)profileSyncStatus.text=profileAccountSyncRequested ? progress : AccountProfileData.Status;
        if(profileWalletStatus!=null)profileWalletStatus.text=$"{profileWallet?.Balance ?? 0:N0} C-Coins\n"+coins;
    }
    private void RefreshAccountFields(bool force = false)
    {
        if (profileNameInput == null) return;
        AccountProfileData.EnsureBound();
        if (profileInputOwner != AccountProfileData.Owner) { profileInputOwner = AccountProfileData.Owner; force = true; profileInputsDirty = false; }
        if (force || !profileInputsDirty)
        { profileNameInput.SetTextWithoutNotify(AccountProfileData.Name); profileBioInput.SetTextWithoutNotify(AccountProfileData.Bio); }
        bool canEdit = AccountProfileData.Authenticated;
        profileNameInput.readOnly = true;
        profileBioInput.interactable = canEdit;
        if (profileChangeUsernameButton != null) profileChangeUsernameButton.gameObject.SetActive(canEdit);
        if (profileSaveButton != null) profileSaveButton.interactable = canEdit && !AccountProfileData.Busy;
        if (profileAccountId != null) profileAccountId.text = AccountProfileData.Owner == "guest" ? "GUEST · LOCAL PROFILE" : AccountProfileData.Owner;
        if (profileSyncStatus != null) profileSyncStatus.text = profileInputsDirty ? "Unsaved changes · click SAVE" : AccountProfileData.Status;
        var label = playerInfoPanel.transform.Find("Account sign in or logout")?.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = GameSaveManager.Instance != null && GameSaveManager.Instance.HasCloudSession ? "LOG OUT" : "SIGN IN";
        RefreshProfileSyncStatus();
        ApplyProfileTypography();
    }
    private void ApplySharedProfileTab(int tab)
    {
        if (profileCardImage == null) return;
        if(tab!=4 && isProfileOpen)RenderProfileCharacter(); // Discard any unowned try-on preview.
        ExportUIArt.Apply(profileCardImage, tab == 0 ? "profileAccount" : tab == 4 ? "profileShop" : "profileStats");
        var stage = profileCanvas.transform.Find("Profile backdrop/Profile stage");
        var title = stage.Find("Player profile label")?.GetComponent<Image>();
        ExportUIArt.Apply(title, tab == 0 ? "profileLabel" : tab == 4 ? "profileShopLabel" : "profileStatsLabel");
        if (profileSaveButton != null) profileSaveButton.gameObject.SetActive(tab == 0 && AccountProfileData.Authenticated);
        if (achievementsTabBtn != null) achievementsTabBtn.gameObject.SetActive(tab == 3);
        if (profileReturnStats != null) profileReturnStats.gameObject.SetActive(tab == 2);
        if (profileSyncStatus != null) profileSyncStatus.gameObject.SetActive(false);
        if(profileProgressSyncStatus!=null)profileProgressSyncStatus.gameObject.SetActive(false);
        var buyCoins = stage.Find("Buy account C-Coins");
        if (buyCoins != null) buyCoins.gameObject.SetActive(false); // Top-up now lives beside the wallet balance.
        ApplyProfileTypography();
    }
    public void OpenProfileShop() { RefreshProfileShop(); OpenTab(4); }
    private void OnProfileSavesChanged()
    {
        profileAccountStats=null;
        if (!isProfileOpen) return;
        var slots = GameSaveManager.Instance.Repository?.Slots.FindAll(s => s.Int("SaveDeleted", 0) == 0);
        if (slots == null) return;
        slots.Sort((a, b) => string.CompareOrdinal(b.updatedUtc, a.updatedUtc));
        profileMenuSlot = slots.Find(s => s.id == profileMenuSlot?.id) ?? (slots.Count > 0 ? slots[0] : null);
        RefreshAllUI();
    }
    private void BuildProfileShop(Transform stage)
    {
        var card = stage.Find("Career card");
        profileShopPanel = CreatePanel("Shared cosmetic shop", card, ShopBackground);
        SetStretchRect(profileShopPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(103, 19), new Vector2(-29, -19));
        string[] categories = { "ALL", "FACE", "HAIR", "TOPS", "BOTTOMS", "SHOE WEAR", "ACCESSORIES" };
        for (int i = 0; i < categories.Length; i++)
        {
            int category = i; var button = CreateButton("Cosmetic category " + i, profileShopPanel.transform, categories[i]);
            float left = (i % 4) / 4f, right = (i % 4 + 1) / 4f;
            float top = 12 + 44 * (i / 4);
            SetStretchRect(button.GetComponent<RectTransform>(), new Vector2(left, 1), new Vector2(right, 1),
                new Vector2(16 - 24 * left, -top - 36), new Vector2(8 - 24 * right, -top));
            StyleShopButton(button, false);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = ProfileInk; label.enableWordWrapping = false;
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 22;
            button.onClick.AddListener(() => { profileShopCategory = category; RefreshProfileShop();
                profileShopContent.GetComponentInParent<ScrollRect>(true).verticalNormalizedPosition = 1; });
        }
        profileShopBalance = ProfileText(profileShopPanel.transform, "Shop balance", "", 22, 16, 110, 36);
        profileShopBalance.rectTransform.offsetMax = new Vector2(-196, -110);
        var topUp = CreateButton("Top up shared wallet", profileShopPanel.transform, "TOP UP");
        SetRect(topUp.GetComponent<RectTransform>(), Vector2.one, Vector2.one, new Vector2(-96, -128), new Vector2(160, 40));
        topUp.onClick.AddListener(() => profileWallet.OpenTopUpWebsite());
        StyleShopButton(topUp, true);
        var notice = CreatePanel("Shop availability notice", profileShopPanel.transform, new Color32(237, 222, 191, 255));
        SetStretchRect(notice.GetComponent<RectTransform>(), new Vector2(0, 1), Vector2.one, new Vector2(16, -208), new Vector2(-16, -164));
        StyleShopBox(notice.GetComponent<Image>(), new Color32(237, 222, 191, 255));
        notice.GetComponent<Image>().raycastTarget = false;
        profileWalletStatus = ProfileText(notice.transform, "Shop wallet status", "", 17, 12, 2, 40);
        profileWalletStatus.color = ShopMuted;
        profileShopContent = CreateProfileShopGrid(profileShopPanel.transform);
        profileShopPanel.SetActive(false);
    }
    private void RefreshProfileShop()
    {
        if (profileWallet == null || profileShopContent == null) return;
        if (profileShopBalance != null)
            profileShopBalance.text = $"{profileWallet.Balance:N0} C-COINS";
        if (profileWalletStatus != null) profileWalletStatus.text = profileWallet.Status;
        foreach (Transform child in profileShopContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        for (int i = 0; i < ProfileShopKinds.Length; i++)
        {
            var button = profileShopPanel.transform.Find("Cosmetic category " + i)?.GetComponent<Button>();
            if (button != null)
            {
                bool selected = i == profileShopCategory;
                button.interactable = !selected;
                StyleShopButton(button, false);
                var colors = button.colors; colors.disabledColor = ShopAccent; button.colors = colors;
                button.GetComponentInChildren<TMP_Text>().color = selected ? Color.white : ProfileInk;
            }
        }
        var items = new List<CCoinCosmetic>(profileWallet.Wallet?.cosmetics ?? CharacterCosmetics.Items);
        items.Sort((left, right) => Array.IndexOf(ProfileShopKinds, left.kind).CompareTo(Array.IndexOf(ProfileShopKinds, right.kind)));
        int shown = 0;
        var art = CharacterCosmeticCatalog.Load();
        foreach (var item in items)
        {
            if (item == null || string.IsNullOrEmpty(item.websiteItemId)) continue;
            string selectedKind = ProfileShopKinds[profileShopCategory];
            if (selectedKind != "all" && item.kind != selectedKind) continue;
            bool isPart=CharacterCosmetics.Find(item.id)!=null;
            bool available=!isPart || art?.Model(CharacterCosmetics.ModelKey(item.id))!=null;
            bool listed=CCoinRules.Find(profileWallet.Wallet,item.id)!=null;
            shown++;
            CreateProfileShopCard(item, art, isPart, available, listed);
        }
        if (shown == 0)
        {
            var row = CreatePanel("Unavailable cosmetic category", profileShopContent, ShopCard);
            StyleShopBox(row.GetComponent<Image>(), ShopCard, true);
            string message = "No cosmetics are available in this category.";
            ShopText(row.transform, "Availability", message, 24, 20, 290, ShopMuted);
        }
        ApplyProfileTypography();
    }
    private void PreviewProfileCosmetic(string id)
    {
        var rig=CharacterCosmeticRig.Load();var item=CharacterCosmetics.Find(id);
        if(item==null || profileCharacterImage==null)return;
        if(rig==null || !rig.Contains(id)){profileWalletStatus.text="This item's character fitting is not available in this build.";return;}
        if(profileTryOnParts.Count==0)profileTryOnParts.AddRange(profileWallet.EquippedParts);
        profileTryOnParts.RemoveAll(x=>CharacterCosmetics.Find(x)?.kind==item.kind);profileTryOnParts.Add(id);
        RenderDressedProfileCharacter(profileTryOnParts.ToArray());
        profileWalletStatus.text="Trying on: "+item.name+" · preview only, not purchased or equipped.";
    }
    private void CreateProfileSkillsRow()
    {
        var history = ProfileHistory().FindAll(a => a != null && a.closed); int n = history.Count;
        float[] scores = new float[5];
        foreach (var a in history) { scores[0] += a.pre; scores[2] += a.post; scores[3] += a.camera / 70 * 100; scores[4] += a.lighting / 30 * 100; }
        for (int i = 0; i < 5; i++) scores[i] = n > 0 ? Mathf.Clamp(scores[i] / n, 0, 100) : 0;
        var row = ProfileRow(profileStatsContent, "Recorded skill chart", 470);
        ProfileText(row.transform, "Chart heading", "Production skills", 30, 14, 2, 42, true);
        var plot = CCoinShopUI.Rect(row.transform, "Measured radar chart", new Vector2(0, -15), new Vector2(350, 290)).gameObject.AddComponent<ProfileSkillsChart>();
        plot.scores = scores; plot.raycastTarget = false;
        Vector2[] positions = { new Vector2(0, 161), new Vector2(223, 35), new Vector2(146, -143), new Vector2(-146, -143), new Vector2(-223, 35) };
        string[] labels = { "DIRECTOR", "SOUND", "EDITOR", "CAMERA", "LIGHTS" };
        for (int i = 0; i < 5; i++)
        {
            string value = i == 1 || n == 0 ? "—" : scores[i].ToString("F0") + "/100";
            var text = CCoinShopUI.Text(row.transform, labels[i], labels[i] + "\n" + value, positions[i], new Vector2(145, 84), 20);
            text.color = ProfileInk; text.richText = false; text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
        }
        var note = ProfileText(row.transform, "Chart source", n == 0 ? "Complete a contract to see measured skills. Sound is not scored in the current briefs."
            : $"Averages from {n} retained graded submissions. Director = pre-production. Camera and lights are normalized to 100. Sound is not scored.", 18, 14, 401, 65);
        note.color = ProfileMuted;
    }
    private void ResetSharedProfileUI()
    {
        AccountProfileData.Changed -= OnAccountProfileChanged;
        if (profileWallet != null) profileWallet.Changed -= OnProfileWalletChanged;
        if (GameSaveManager.Instance != null) GameSaveManager.Instance.Changed -= OnProfileSavesChanged;
        profileWallet = null; profileNameInput = profileBioInput = profileNewUsernameInput = profileCurrentPasswordInput = null; profileSyncStatus = profileWalletStatus = profileShopBalance = null;
        profileSaveButton = profileShopButton = profileReturnStats = profileChangeUsernameButton = null; profileUsernameChangeOverlay = null; profileUsernameChangeStatus = null; profileShopPanel = null; profileShopContent = null; profileInputOwner = null;
        profileAccountStats=null;profileAccountSyncButton=null;profileProgressSyncStatus=null;profileAccountSyncRequested=false;
    }
}
