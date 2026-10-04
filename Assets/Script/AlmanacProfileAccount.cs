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
    private TMP_Text profileSyncStatus, profileWalletStatus;
    private Button profileSaveButton, profileShopButton, profileReturnStats;
    private GameObject profileShopPanel;
    private Transform profileShopContent;
    private CCoinService profileWallet;
    private string profileInputOwner;
    private int profileShopCategory = 4;
    private bool ProfileInputFocused => (profileNameInput != null && profileNameInput.isFocused) || (profileBioInput != null && profileBioInput.isFocused);

    private CareerProfileProgress.Record ProfileRecord() => profileMenuOnly ? CareerProfileProgress.ReadStored(profileMenuSlot) : CareerProfileProgress.Read();
    private List<PlayerAnalytics.Attempt> ProfileHistory() => profileMenuOnly ? PlayerAnalytics.ProfileHistory(profileMenuSlot) : PlayerAnalytics.ProfileHistory();
    private int ProfilePreference(string key) => profileMenuOnly ? profileMenuSlot?.Int(key, 0) ?? 0 : GameSavePrefs.GetInt(key, 0);
    public void OpenMenuProfile(Action closed)
    {
        profileMenuOnly = true; profileMenuClosed = closed;
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
        profileCanvas.SetActive(true); profileInputsDirty = false; RefreshAccountFields(true); RenderProfileCharacter();
        AccountProfileData.Refresh(); CCoinService.Ensure().Refresh();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }
    private void NextProfileCareer()
    {
        if (!profileMenuOnly) return;
        var slots = GameSaveManager.Instance?.Repository?.Slots.FindAll(s => s.Int("SaveDeleted", 0) == 0);
        if (slots == null || slots.Count == 0) return;
        slots.Sort((a, b) => string.CompareOrdinal(b.updatedUtc, a.updatedUtc));
        int current = slots.FindIndex(s => s.id == profileMenuSlot?.id);
        profileMenuSlot = slots[(current + 1) % slots.Count];
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
            profileSyncStatus = BookText(stage, "Account sync status", new Vector2(475, -367), new Vector2(805, 36), 20);
            FeedbackTypography.Apply(profileSyncStatus); profileSyncStatus.color = ProfileMuted; profileSyncStatus.richText = false;
            profileSyncStatus.enableAutoSizing = true; profileSyncStatus.fontSizeMin = 16; profileSyncStatus.fontSizeMax = 20;
        }
        playerNameText.gameObject.SetActive(false);
        if (profileAccountId != null)
        {
            SetRect(profileAccountId.rectTransform, Vector2.one * .5f, Vector2.one * .5f, new Vector2(25, 135), new Vector2(460, 40));
            profileAccountId.alignment = TextAlignmentOptions.Left;
        }
        if (profileSaveButton == null)
        {
            profileSaveButton = BookButton(stage, "Save account profile", "", "profileSave", new Vector2(340, -425), new Vector2(235, 94));
            profileSaveButton.onClick.AddListener(SaveAccountProfile);
            var account = BookButton(playerInfoPanel.transform, "Account sign in or logout", "LOG OUT", "redButton", new Vector2(325, 135), new Vector2(160, 46));
            account.onClick.AddListener(() =>
            {
                GameSaveManager.Instance?.SaveCheckpoint();
                if (AccountProfileData.Owner == "guest") LoadingScreenController.LoadScene("Login");
                else GameSaveManager.Ensure().Logout();
            });
        }
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
        if (profileMenuOnly && stage.Find("Choose profile career") == null)
        {
            var choose = BookButton(stage, "Choose profile career", "SWITCH CAREER", "blueButton", new Vector2(-455, 309), new Vector2(250, 40));
            choose.onClick.AddListener(NextProfileCareer);
        }
        if (profileMenuOnly && GameSaveManager.Instance != null)
        { GameSaveManager.Instance.Changed -= OnProfileSavesChanged; GameSaveManager.Instance.Changed += OnProfileSavesChanged; }
        if (playerInfoTabBtn.onClick.GetPersistentEventCount() == 0)
        { playerInfoTabBtn.onClick.RemoveListener(OpenPlayerInfoTab); playerInfoTabBtn.onClick.AddListener(OpenPlayerInfoTab); }
        if (achievementsTabBtn.onClick.GetPersistentEventCount() == 0)
        { achievementsTabBtn.onClick.RemoveListener(OpenAchievementsTab); achievementsTabBtn.onClick.AddListener(OpenAchievementsTab); }
        AccountProfileData.Changed -= OnAccountProfileChanged; AccountProfileData.Changed += OnAccountProfileChanged;
        if (profileWallet == null) { profileWallet = CCoinService.Ensure(); profileWallet.Changed += RefreshProfileShop; }
        foreach (var button in new[] { profileSaveButton, profileBackButton, profileCloseButton, profileReturnStats, achievementsTabBtn,
            stage.Find("Buy account C-Coins")?.GetComponent<Button>(), playerInfoPanel.transform.Find("Account sign in or logout")?.GetComponent<Button>() })
        {
            if (button == null) continue;
            var colors = button.colors; colors.normalColor = colors.selectedColor = Color.white;
            colors.highlightedColor = new Color(1, 1, 1, 1); colors.pressedColor = new Color(.85f, .85f, .85f, 1); colors.disabledColor = new Color(.65f, .65f, .65f, 1); button.colors = colors;
        }
        RefreshAccountFields();
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
        if (AccountProfileData.Save(profileNameInput.text, profileBioInput.text))
        { profileInputsDirty = false; RefreshAccountFields(true); }
    }
    private void OnAccountProfileChanged() { if (this != null) RefreshAccountFields(); }
    private void RefreshAccountFields(bool force = false)
    {
        if (profileNameInput == null) return;
        AccountProfileData.EnsureBound();
        if (profileInputOwner != AccountProfileData.Owner) { profileInputOwner = AccountProfileData.Owner; force = true; profileInputsDirty = false; }
        if (force || !profileInputsDirty)
        { profileNameInput.SetTextWithoutNotify(AccountProfileData.Name); profileBioInput.SetTextWithoutNotify(AccountProfileData.Bio); }
        if (profileAccountId != null) profileAccountId.text = AccountProfileData.Owner == "guest" ? "GUEST · LOCAL PROFILE" : AccountProfileData.Owner;
        if (profileSyncStatus != null) profileSyncStatus.text = profileInputsDirty ? "Unsaved changes · click SAVE" : AccountProfileData.Status;
        var label = playerInfoPanel.transform.Find("Account sign in or logout")?.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = AccountProfileData.Owner == "guest" ? "SIGN IN" : "LOG OUT";
    }
    private void ApplySharedProfileTab(int tab)
    {
        if (profileCardImage == null) return;
        ExportUIArt.Apply(profileCardImage, tab == 0 ? "profileAccount" : tab == 4 ? "profileShop" : "profileStats");
        var stage = profileCanvas.transform.Find("Profile backdrop/Profile stage");
        var title = stage.Find("Player profile label")?.GetComponent<Image>();
        ExportUIArt.Apply(title, tab == 0 ? "profileLabel" : tab == 4 ? "profileShopLabel" : "profileStatsLabel");
        if (profileSaveButton != null) profileSaveButton.gameObject.SetActive(tab == 0);
        if (achievementsTabBtn != null) achievementsTabBtn.gameObject.SetActive(tab == 3);
        if (profileReturnStats != null) profileReturnStats.gameObject.SetActive(tab == 2);
        if (profileSyncStatus != null) profileSyncStatus.gameObject.SetActive(tab == 0);
        var buyCoins = stage.Find("Buy account C-Coins");
        if (buyCoins != null) buyCoins.gameObject.SetActive(tab == 4);
    }
    public void OpenProfileShop() { RefreshProfileShop(); OpenTab(4); }
    private void OnProfileSavesChanged()
    {
        if (!profileMenuOnly || !isProfileOpen) return;
        var slots = GameSaveManager.Instance.Repository?.Slots.FindAll(s => s.Int("SaveDeleted", 0) == 0);
        if (slots == null) return;
        slots.Sort((a, b) => string.CompareOrdinal(b.updatedUtc, a.updatedUtc));
        profileMenuSlot = slots.Find(s => s.id == profileMenuSlot?.id) ?? (slots.Count > 0 ? slots[0] : null);
        RefreshAllUI();
    }
    private void BuildProfileShop(Transform stage)
    {
        var card = stage.Find("Career card");
        profileShopPanel = CreatePanel("Shared cosmetic shop", card, new Color32(252, 245, 220, 255));
        SetStretchRect(profileShopPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(103, 19), new Vector2(-29, -19));
        string[] categories = { "HAT", "SHIRT", "PANTS", "SHOES", "FRAMES" };
        for (int i = 0; i < categories.Length; i++)
        {
            int category = i; var button = CreateButton("Cosmetic category " + i, profileShopPanel.transform, categories[i]);
            SetRect(button.GetComponent<RectTransform>(), new Vector2((i + .5f) / 5, 1), new Vector2((i + .5f) / 5, 1), new Vector2(0, -25), new Vector2(136, 48));
            button.GetComponent<Image>().sprite = null;
            var colors = button.colors; colors.normalColor = new Color32(252, 245, 220, 255); colors.highlightedColor = new Color32(224, 207, 179, 255);
            colors.pressedColor = colors.disabledColor = new Color32(181, 145, 112, 255); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            var border = button.gameObject.AddComponent<Outline>(); border.effectColor = ProfileInk; border.effectDistance = new Vector2(2, -2);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.color = ProfileInk; label.enableWordWrapping = false;
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 22;
            button.onClick.AddListener(() => { profileShopCategory = category; RefreshProfileShop(); });
        }
        profileWalletStatus = ProfileText(profileShopPanel.transform, "Shop wallet status", "", 19, 8, 60, 118);
        var sync = CreateButton("Sync shared wallet", profileShopPanel.transform, "SYNC");
        SetRect(sync.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -87), new Vector2(96, 42));
        sync.onClick.AddListener(() => profileWallet.Refresh());
        var reset = CreateButton("Default profile appearance", profileShopPanel.transform, "DEFAULT LOOK");
        SetRect(reset.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-86, -141), new Vector2(166, 40));
        var resetLabel = reset.GetComponentInChildren<TextMeshProUGUI>();
        resetLabel.enableWordWrapping = false; resetLabel.enableAutoSizing = true; resetLabel.fontSizeMin = 14; resetLabel.fontSizeMax = 18;
        reset.onClick.AddListener(() => profileWallet.Equip(null));
        profileWalletStatus.rectTransform.offsetMax = new Vector2(-181, -60);
        profileShopContent = CreateScrollList("Shared shop items", profileShopPanel.transform);
        var scroll = profileShopContent.GetComponentInParent<ScrollRect>(true);
        scroll.GetComponent<RectTransform>().offsetMax = new Vector2(-2, -186);
        scroll.GetComponent<Image>().color = new Color32(252, 245, 220, 255); AddProfileScrollbar(profileShopContent);
        var coins = BookButton(stage, "Buy account C-Coins", "BUY C-COINS", "blueButton", new Vector2(340, -425), new Vector2(265, 72));
        coins.onClick.AddListener(() => profileWallet.RequestCoinPurchase());
        profileShopPanel.SetActive(false);
    }
    private void RefreshProfileShop()
    {
        if (profileWallet == null || profileShopContent == null) return;
        if (profileWalletStatus != null)
            profileWalletStatus.text = $"{profileWallet.Balance:N0} C-Coins{(profileWallet.Verified ? "" : " · cached")}\n" + profileWallet.Status;
        foreach (Transform child in profileShopContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        for (int i = 0; i < 5; i++)
        {
            var button = profileShopPanel.transform.Find("Cosmetic category " + i)?.GetComponent<Button>();
            if (button != null) button.interactable = i != profileShopCategory;
        }
        var items = profileWallet.Wallet?.cosmetics ?? new CCoinCosmetic[0]; int shown = 0;
        foreach (var item in items)
        {
            if (item == null || profileShopCategory != 4 || item.kind != "profile_frame") continue;
            shown++; var row = ProfileRow(profileShopContent, "Shared cosmetic " + item.id, 168);
            ProfileText(row.transform, "Name", item.name, 26, 16, 6, 38);
            var detail = ProfileText(row.transform, "Description", item.description ?? "Profile frame", 20, 16, 48, 66);
            detail.rectTransform.offsetMax = new Vector2(-170, -48);
            bool owned = CCoinRules.Owns(profileWallet.Wallet, item.id), equipped = owned && profileWallet.SelectedCosmetic == item.id;
            var buy = CreateButton("Buy or equip cosmetic", row.transform, equipped ? "EQUIPPED" : owned ? "EQUIP" : "BUY");
            SetRect(buy.GetComponent<RectTransform>(), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-86, -4), new Vector2(145, 52));
            buy.onClick.AddListener(() => { if (owned) profileWallet.Equip(item.id); else profileWallet.BuyCosmetic(item.id); });
            buy.interactable = !equipped && (owned || profileWallet.CanBuy && profileWallet.Balance >= item.price);
            ProfileText(row.transform, "Price", owned ? "OWNED" : item.price + " C-Coins", 21, 16, 125, 32).color = ProfileGreen;
        }
        if (shown == 0)
        {
            var row = ProfileRow(profileShopContent, "Unavailable cosmetic category", 215);
            string message = profileShopCategory == 4 ? "Your profile-frame catalog appears here when the account shop is connected. No coins are charged while it is unavailable."
                : "No items are available in this clothing category yet. Your current account catalog supports profile frames; choose FRAMES to view them. No coins are charged.";
            ProfileText(row.transform, "Availability", message, 24, 18, 12, 185);
        }
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
        if (profileWallet != null) profileWallet.Changed -= RefreshProfileShop;
        if (GameSaveManager.Instance != null) GameSaveManager.Instance.Changed -= OnProfileSavesChanged;
        profileWallet = null; profileNameInput = profileBioInput = null; profileSyncStatus = profileWalletStatus = null;
        profileSaveButton = profileShopButton = profileReturnStats = null; profileShopPanel = null; profileShopContent = null; profileInputOwner = null;
    }
}
