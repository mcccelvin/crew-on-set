using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AlmanacManager
{
    private GameObject profileStatsPanel;
    private Button profileStatsButton;
    private Transform profileStatsContent;
    private TMP_Text achievementSummary, profileCareerCaption, profileStatsSubtitle;
    private float profileRefreshAt;
    private string profileProgressStamp;
    private static readonly Color ProfileInk = new Color32(47, 40, 35, 255);
    private static readonly Color ProfileMuted = new Color32(74, 62, 50, 255);
    private static readonly Color ProfileGreen = new Color32(35, 99, 65, 255);

    private void EnsureProfileProgressUI()
    {
        if (profileCanvas == null) return;
        var stage = profileCanvas.transform.Find("Profile backdrop/Profile stage");
        var card = stage != null ? stage.Find("Career card") : null;
        if (card == null) return;
        profileCardImage = card.GetComponent<Image>();
        if (profileStatsPanel == null)
        {
            var authored = card.Find("Career stats");
            profileStatsPanel = authored != null ? authored.gameObject : CreatePanel("Career stats", card, Color.clear);
            SetStretchRect(profileStatsPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(100, 20), new Vector2(-24, -20));
            profileStatsPanel.GetComponent<Image>().color = new Color32(252, 245, 220, 255);
            if (authored == null) ProfileText(profileStatsPanel.transform, "Stats heading", "Account statistics", 40, 12, 8, 48, true);
            profileStatsSubtitle = profileStatsPanel.transform.Find("Stats subtitle")?.GetComponent<TMP_Text>() ??
                ProfileText(profileStatsPanel.transform, "Stats subtitle", "Your saved production record", 20, 14, 54, 36);
            profileStatsContent = profileStatsPanel.transform.Find("Stats list/Viewport/Content") ?? CreateScrollList("Stats list", profileStatsPanel.transform);
            AddProfileScrollbar(profileStatsContent);
            profileStatsPanel.SetActive(false);
        }
        if (profileStatsButton == null)
        {
            var existing = stage.Find("Stats tab");
            profileStatsButton = existing != null ? existing.GetComponent<Button>() : ProfileTab(stage, "Stats tab", "STATS", new Vector2(475, 425));
        }
        var buttons = new[] { playerInfoTabBtn, profileStatsButton };
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            StyleProfileSideTab(buttons[i], i);
        }
        profileStatsButton.onClick.RemoveListener(OpenStatsTab);
        profileStatsButton.onClick.AddListener(OpenStatsTab);
        var oldCareerBadge=stage.Find("Career save badge");
        if(oldCareerBadge!=null)oldCareerBadge.gameObject.SetActive(false);
        if (achievementsPanel != null && achievementSummary == null)
        {
            var heading = achievementsPanel.transform.Find("Section Title")?.GetComponent<TMP_Text>();
            if (heading != null)
            {
                heading.text = "Achievements"; FeedbackTypography.Apply(heading, true); heading.color = ProfileInk;
                SetStretchRect(heading.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(12, -56), new Vector2(-12, -8));
                heading.fontSize = 40;
            }
            achievementSummary = achievementsPanel.transform.Find("Achievement summary")?.GetComponent<TMP_Text>() ??
                ProfileText(achievementsPanel.transform, "Achievement summary", "", 20, 14, 54, 36);
            AddProfileScrollbar(achievementListContainer);
        }
        EnsureCareerAchievements();
        EnsureSharedProfileUI();
    }

    private string ProfileCareerName()
    {
        return AccountProfileData.Owner=="guest" ? "Guest stats · saved on this device" : "Your PlayFab account production record";
    }
    private void EnsureCareerAchievements()
    {
        var record = ProfileRecord();
        foreach (var definition in CareerProfileProgress.Achievements)
        {
            var entry = achievements.Find(a => a.id == definition.id);
            if (entry == null) { entry = new AchievementEntry { id = definition.id }; achievements.Add(entry); }
            entry.title = definition.title; entry.description = definition.description.Replace("in this career","on this account"); entry.maxProgress = definition.goal;
            entry.currentProgress = Mathf.Clamp(definition.progress(record), 0, definition.goal);
            entry.isUnlocked = entry.currentProgress == definition.goal || ProfilePreference("AchivDone_" + entry.id) == 1;
            if (entry.isUnlocked) entry.currentProgress = definition.goal;
        }
        if (achievementSummary != null)
            achievementSummary.text = $"{achievements.FindAll(a => a.isUnlocked).Count} / {achievements.Count} unlocked · scroll to explore";
        if (profileCareerCaption != null) profileCareerCaption.text = ProfileCareerName();
    }
    public void OpenStatsTab() { RefreshProfileStats(); OpenTab(3); }

    private void PollProfileProgress()
    {
        if (!isProfileOpen || Time.unscaledTime < profileRefreshAt) return;
        profileRefreshAt = Time.unscaledTime + 1f;
        RefreshProfileSyncStatus();
        string stamp = GameSavePrefs.GetString(CareerProfileProgress.SaveKey, "") + GameSavePrefs.GetInt("PlayerMoney", 0) +
            AccountProfileData.Name + AccountProfileData.Bio + AccountProfileData.Owner + ProfileCareerName();
        if (stamp == profileProgressStamp) return;
        profileProgressStamp = stamp;
        profileAccountStats=null;
        RefreshAllUI();
    }
    private void ResetProfileProgressUI()
    {
        profileStatsPanel = null; profileStatsButton = null; profileStatsContent = null;
        achievementSummary = null; profileCareerCaption = null; profileStatsSubtitle = null;
        profileAccountId = null; profileCardImage = null; profileCharacterImage = null;
        profileProgressStamp = null;
        ResetSharedProfileUI();
    }

    private TMP_Text ProfileText(Transform parent, string name, string value, float size, float left, float top, float height, bool heading = false)
    {
        var label = CreateText(name, parent, value, size, TextAlignmentOptions.Left);
        FeedbackTypography.Apply(label, heading);
        label.color = ProfileInk; label.richText = false;
        label.enableAutoSizing = true; label.fontSizeMin = Mathf.Min(size, 18); label.fontSizeMax = size;
        label.overflowMode = TextOverflowModes.Ellipsis;
        SetStretchRect(label.rectTransform, new Vector2(0, 1), Vector2.one, new Vector2(left, -top - height), new Vector2(-left, -top));
        return label;
    }
    private void AddProfileScrollbar(Transform content)
    {
        if (content == null) return;
        var scroll = content.GetComponentInParent<ScrollRect>(true);
        if (scroll == null || scroll.verticalScrollbar != null) return;
        SetStretchRect(scroll.viewport, Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-28, -6));
        var rail = CreatePanel("Scroll rail", scroll.transform, new Color32(220, 207, 181, 255));
        SetStretchRect(rail.GetComponent<RectTransform>(), new Vector2(1, 0), Vector2.one, new Vector2(-18, 8), new Vector2(-7, -8));
        var handle = CreatePanel("Scroll handle", rail.transform, new Color32(117, 92, 67, 255));
        SetStretchRect(handle.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var bar = rail.AddComponent<Scrollbar>(); bar.direction = Scrollbar.Direction.BottomToTop;
        bar.handleRect = handle.GetComponent<RectTransform>(); bar.targetGraphic = handle.GetComponent<Image>();
        scroll.verticalScrollbar = bar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }
    private GameObject ProfileRow(Transform parent, string name, float height)
    {
        var row = CreatePanel(name, parent, new Color32(255, 250, 234, 255));
        row.AddComponent<LayoutElement>().preferredHeight = height;
        row.GetComponent<Image>().raycastTarget = false;
        return row;
    }
    private void CreateCareerAchievementCard(AchievementEntry achievement)
    {
        var row = ProfileRow(achievementListContainer, "Achievement · " + achievement.id, 158);
        var rule = CreatePanel("Status rule", row.transform, achievement.isUnlocked ? ProfileGreen : new Color32(181, 152, 112, 255));
        SetStretchRect(rule.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(5, 0));
        rule.GetComponent<Image>().raycastTarget = false;
        var title = ProfileText(row.transform, "Title", achievement.title, 28, 20, 8, 39);
        title.rectTransform.offsetMax = new Vector2(-155, title.rectTransform.offsetMax.y);
        var state = ProfileText(row.transform, "Status", achievement.isUnlocked ? "UNLOCKED" : "IN PROGRESS", 17, 20, 10, 34);
        state.rectTransform.anchorMin = new Vector2(1, 1); state.rectTransform.offsetMin = new Vector2(-155, -44);
        state.color = achievement.isUnlocked ? ProfileGreen : ProfileMuted; state.alignment = TextAlignmentOptions.Right;
        var description = ProfileText(row.transform, "Description", achievement.description, 21, 20, 48, 66);
        description.alignment = TextAlignmentOptions.TopLeft;
        var track = CreatePanel("Progress track", row.transform, new Color32(226, 215, 189, 255));
        SetStretchRect(track.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 20), new Vector2(-110, 29));
        track.GetComponent<Image>().raycastTarget = false;
        var fill = CreatePanel("Progress fill", track.transform, achievement.isUnlocked ? ProfileGreen : new Color32(148, 108, 61, 255));
        float progress = Mathf.Clamp01((float)achievement.currentProgress / Mathf.Max(1, achievement.maxProgress));
        SetStretchRect(fill.GetComponent<RectTransform>(), Vector2.zero, new Vector2(progress, 1), Vector2.zero, Vector2.zero);
        fill.GetComponent<Image>().raycastTarget = false;
        var count = ProfileText(row.transform, "Progress", $"{achievement.currentProgress} / {achievement.maxProgress}", 19, 20, 119, 31);
        count.alignment = TextAlignmentOptions.Right;
    }
    private void StatsTiles(string[] labels, string[] values)
    {
        var row = ProfileRow(profileStatsContent, "Statistics row", 112);
        row.GetComponent<Image>().color = Color.clear;
        for (int i = 0; i < labels.Length; i++)
        {
            var tile = CreatePanel(labels[i], row.transform, new Color32(255, 250, 234, 255));
            SetStretchRect(tile.GetComponent<RectTransform>(), new Vector2((float)i / labels.Length, 0), new Vector2((float)(i + 1) / labels.Length, 1), new Vector2(3, 0), new Vector2(-3, 0));
            tile.GetComponent<Image>().raycastTarget = false;
            var value = ProfileText(tile.transform, "Value", values[i], 36, 12, 10, 48); value.color = ProfileGreen;
            ProfileText(tile.transform, "Label", labels[i], 20, 12, 63, 36).color = ProfileMuted;
        }
    }
    private void RefreshProfileStats()
    {
        if (profileStatsContent == null) return;
        var scroll = profileStatsContent.GetComponentInParent<ScrollRect>(true);
        float position = profileStatsContent.childCount == 0 ? 1 : scroll.verticalNormalizedPosition;
        foreach (Transform child in profileStatsContent) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var r = ProfileRecord();
        if (profileStatsSubtitle != null) profileStatsSubtitle.text = ProfileCareerName();
        CreateProfileSkillsRow();
        StatsTiles(new[] { "Contracts completed", "Saved recordings", "Graded attempts" }, new[] { r.CompletedCount + " / 5", r.recordings.ToString("N0"), r.attempts.ToString("N0") });
        StatsTiles(new[] { "Passed attempts", "Failed attempts", "Best grade" }, new[] { r.passed.ToString("N0"), r.failed.ToString("N0"), r.BestGrade });
        StatsTiles(new[] { "Best overall score", "Average overall score" }, new[] {
            r.attempts > 0 || r.bestScore > 0 ? r.bestScore.ToString("F1") + " / 100" : "—",
            r.attempts > 0 ? (r.scoreTotal / r.attempts).ToString("F1") + " / 100" : "—" });
        double seconds = r.recordedSeconds;
        string duration = $"{(long)(seconds / 60):N0}m {(int)(seconds % 60):00}s" + (r.partialDuration ? "+" : "");
        StatsTiles(new[] { "Recorded footage", "B-Coins spent", "B-Coin income" }, new[] { duration, r.spent.ToString("N0"), r.income.ToString("N0") });
        var account=ProfileSnapshot();
        StatsTiles(new[] { account.currentBudget ? "Current production B-Coins" : "Latest saved B-Coin budget", "Account C-Coins" }, new[] {
            account.hasBudget ? account.bCoins.ToString("N0") : "—",
            profileWallet?.Verified==true ? profileWallet.Balance.ToString("N0") : (profileWallet?.Balance ?? 0).ToString("N0")+" · cached" });
        float noteHeight = r.partialHistory || account.conflictingHistory ? 270 : 180;
        var explanation = ProfileRow(profileStatsContent, "Tracking note", noteHeight);
        string note = "Stats combine this account's saved productions. B-Coins remain separate production budgets, not a combined wallet. C-Coins need server confirmation. Income excludes developer funds; replays are attempts, not new clients.";
        if(account.conflictingHistory)note+=" Conflicting checkpoint copies share history and are not counted twice; totals are conservative.";
        if (r.partialHistory) note += " Older totals are partial: only retained records are included. A + marks footage with unknown earlier duration.";
        ProfileText(explanation.transform, "Tracking details", note, 20, 14, 8, noteHeight-16).color = ProfileMuted;
        var title = ProfileRow(profileStatsContent, "Portfolio heading", 48);
        ProfileText(title.transform, "Heading", "Your client portfolio", 30, 14, 2, 42, true);
        for (int level = 1; level <= 5; level++)
        {
            var row = ProfileRow(profileStatsContent, "Contract " + level, 86);
            var name = ProfileText(row.transform, "Client", CampaignProgression.GetContractName(level), 25, 14, 3, 35);
            name.rectTransform.offsetMax = new Vector2(-150, -3);
            var status = ProfileText(row.transform, "Result", r.Completed(level) ? "PASSED" : "NOT PASSED", 18, 14, 4, 32);
            status.rectTransform.anchorMin = new Vector2(1, 1); status.rectTransform.offsetMin = new Vector2(-145, -36);
            status.alignment = TextAlignmentOptions.Right; status.color = r.Completed(level) ? ProfileGreen : ProfileMuted;
            string best = r.bestByLevel[level - 1] < 0 ? "not yet graded" : r.bestByLevel[level - 1].ToString("F1") + "/100";
            ProfileText(row.transform, "Details", $"Tracked attempts: {r.attemptsByLevel[level - 1]}   ·   Best score: {best}", 20, 14, 43, 32).color = ProfileMuted;
        }
        var history = ProfileHistory().FindAll(a => a != null && a.closed);
        if (history.Count > 0)
        {
            var titleRow = ProfileRow(profileStatsContent, "Recent results", 48);
            ProfileText(titleRow.transform, "Heading", "Recent submissions", 30, 14, 2, 42, true);
            for (int i = history.Count - 1; i >= Mathf.Max(0, history.Count - 5); i--)
            {
                var a = history[i]; var row = ProfileRow(profileStatsContent, "Submission " + i, 78);
                ProfileText(row.transform, "Result", $"{a.date} · Contract {a.level} · Grade {a.grade} · {a.score:F1}/100", 22, 14, 5, 34);
                ProfileText(row.transform, "Breakdown", $"Pre {a.pre:F0}   /   Production {a.camera + a.lighting:F0}   /   Post {a.post:F0}" + (a.assisted ? "   ·   Developer funds" : ""), 20, 14, 39, 32).color = ProfileMuted;
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)profileStatsContent);
        scroll.verticalNormalizedPosition = position;
        ApplyProfileTypography();
    }
}
