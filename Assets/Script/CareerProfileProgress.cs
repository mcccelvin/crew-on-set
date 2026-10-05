using System;
using System.Collections.Generic;
using UnityEngine;

// No cached career state: changing a save/account must never inherit another player's stats.
public static class CareerProfileProgress
{
    public const string SaveKey = "Profile.Career.v1";
    [Serializable] public sealed class Record
    {
        public int version = 1, recordings, attempts, passed, failed, bestRank, completedMask, uncertainLevelsMask;
        public double recordedSeconds, scoreTotal;
        public float bestScore;
        public long spent, income;
        public bool partialHistory, partialDuration, firstTry, comeback, topRank, polished, balanced, frugal;
        public int[] attemptsByLevel = new int[5], failedByLevel = new int[5];
        public float[] bestByLevel = { -1, -1, -1, -1, -1 };
        public int CompletedCount
        {
            get { int count = 0; for (int i = 0; i < 5; i++) if (Completed(i + 1)) count++; return count; }
        }
        public bool Completed(int level) => (completedMask & (1 << (level - 1))) != 0;
        public string BestGrade => bestRank == 0 ? "—" : new[] { "—", "F", "C", "B", "A", "S" }[Mathf.Clamp(bestRank, 0, 5)];
    }
    public sealed class Achievement
    {
        public readonly string id, title, description;
        public readonly int goal;
        public readonly Func<Record, int> progress;
        public Achievement(string id, string title, string description, int goal, Func<Record, int> progress)
        { this.id = "career_" + id; this.title = title; this.description = description; this.goal = goal; this.progress = progress; }
    }
    public static readonly Achievement[] Achievements = {
        new Achievement("first_take", "First take", "Save your first camera recording to an SD card.", 1, r => r.recordings),
        new Achievement("ten_takes", "Behind the lens", "Save 10 camera recordings in this career.", 10, r => r.recordings),
        new Achievement("twenty_five_takes", "Rolling!", "Save 25 camera recordings in this career.", 25, r => r.recordings),
        new Achievement("first_client", "Signed, sealed, delivered", "Pass your first commercial contract, including its mandatory requirements.", 1, r => r.CompletedCount),
        Client("blooms", "Crystal Blooms", 1), Client("goke", "Goke Cola", 2), Client("terrari", "Terrari", 3),
        Client("coffee", "Kape Kultura", 4), Client("haraya", "Haraya", 5),
        new Achievement("campaign", "Full portfolio", "Pass all five different commercial contracts in this career.", 5, r => r.CompletedCount),
        new Achievement("top_rank", "Director's cut", "Earn an S grade on a passed contract.", 1, r => r.topRank ? 1 : 0),
        new Achievement("polished", "Finishing touch", "Pass a contract with a post-production score of at least 95/100.", 1, r => r.polished ? 1 : 0),
        new Achievement("balanced", "All-round filmmaker", "Pass with at least 90/100 in each of the three production departments.", 1, r => r.balanced ? 1 : 0),
        new Achievement("comeback", "Second act", "Pass a contract after a tracked failed attempt at the same contract.", 1, r => r.comeback ? 1 : 0),
        new Achievement("first_try", "Right first time", "Pass on the first fully tracked attempt at a contract, without developer funds.", 1, r => r.firstTry ? 1 : 0),
        new Achievement("frugal", "Budget keeper", "Pass a fully tracked contract without developer funds or rejected purchases, retaining at least 1,000 B-Coins.", 1, r => r.frugal ? 1 : 0)
    };
    private static Achievement Client(string id, string name, int level) =>
        new Achievement(id, name + " approved", "Pass the " + name + " contract and all its mandatory requirements.", 1, r => r.Completed(level) ? 1 : 0);
    public static bool IsBuiltIn(string id) => Array.Exists(Achievements, a => a.id == id);
    public static bool RetainOnContractRetry(string key) => key == SaveKey || key == GameSaveRepository.ProfileOriginKey ||
        key.StartsWith("AchivDone_", StringComparison.Ordinal) || key.StartsWith("AchivProg_", StringComparison.Ordinal);
    public static bool IsPassed(string grade) => grade == "S" || grade == "A" || grade == "B" || grade == "C";
    private static int Rank(string grade) => grade == "S" ? 5 : grade == "A" ? 4 : grade == "B" ? 3 : grade == "C" ? 2 : grade == "F" ? 1 : 0;
    private static int Add(int value, int amount) => (int)Math.Min(int.MaxValue, (long)value + Math.Max(0, amount));
    private static float Score(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Clamp(value, 0, 100);

    public static Record Read()
    {
        Record record = null;
        try { record = JsonUtility.FromJson<Record>(GameSavePrefs.GetString(SaveKey, "")); }
        catch (ArgumentException) { }
        if (record == null || record.version != 1 || record.attemptsByLevel == null || record.attemptsByLevel.Length != 5 ||
            record.failedByLevel == null || record.failedByLevel.Length != 5 || record.bestByLevel == null || record.bestByLevel.Length != 5)
        {
            record = SeedExistingCareer();
            MergeCompleted(record);
            Store(record, false);
            GameSavePrefs.Save();
            GameSaveManager.Instance?.SaveCheckpoint();
        }
        // Existing campaign flags are authoritative even for pre-feature saves.
        MergeCompleted(record);
        return record;
    }
    private static Record SeedExistingCareer()
    { return SeedExistingCareer(PlayerAnalytics.ProfileHistory(), key => GameSavePrefs.GetInt(key, 0), GameSavePrefs.HasKey, key => GameSavePrefs.GetFloat(key, 0)); }
    // Read-only menu projection. Viewing another career must not activate it or write its preferences.
    public static Record ReadStored(GameSaveSlot slot)
    {
        if (slot == null) return new Record();
        Record record = null;
        try { record = JsonUtility.FromJson<Record>(slot.values.Find(v => v.key == SaveKey && v.kind == 2)?.text ?? ""); }
        catch (ArgumentException) { }
        if (record == null || record.version != 1 || record.attemptsByLevel == null || record.attemptsByLevel.Length != 5 ||
            record.failedByLevel == null || record.failedByLevel.Length != 5 || record.bestByLevel == null || record.bestByLevel.Length != 5)
            record = SeedExistingCareer(PlayerAnalytics.ProfileHistory(slot), key => slot.Int(key, 0), key => slot.values.Exists(v => v.key == key), key => slot.values.Find(v => v.key == key && v.kind == 1)?.number ?? 0);
        for (int level = 1; level <= 5; level++) if (slot.Int(CampaignProgression.GetGradedKey(level), 0) == 1) record.completedMask |= 1 << (level - 1);
        return record;
    }
    private static Record SeedExistingCareer(List<PlayerAnalytics.Attempt> history, Func<string, int> integer, Func<string, bool> hasKey, Func<string, float> number)
    {
        var record = new Record();
        foreach (var attempt in history)
        {
            if (attempt == null) continue;
            record.partialHistory = true;
            if (attempt.level >= 1 && attempt.level <= 5) record.uncertainLevelsMask |= 1 << (attempt.level - 1);
            record.recordings = Add(record.recordings, attempt.takes);
            if (attempt.takes > 0) record.partialDuration = true;
            if (attempt.transactions != null)
                foreach (var t in attempt.transactions) if (t != null) ApplyTransaction(record, t.amount, t.category);
            if (attempt.closed && Rank(attempt.grade) > 0)
                ApplyResult(record, attempt, attempt.camera + attempt.lighting, true);
        }
        for (int level = 1; level <= 5; level++)
        {
            if (integer(CampaignProgression.GetGradedKey(level)) == 1)
            { record.partialHistory = true; record.uncertainLevelsMask |= 1 << (level - 1); }
            string key = "ContractBestScore_Level" + level;
            if (hasKey(key))
            {
                record.partialHistory = true;
                record.uncertainLevelsMask |= 1 << (level - 1);
                float score = Score(number(key));
                record.bestByLevel[level - 1] = Mathf.Max(record.bestByLevel[level - 1], score);
                record.bestScore = Mathf.Max(record.bestScore, score);
            }
        }
        return record;
    }
    private static void MergeCompleted(Record r)
    {
        for (int level = 1; level <= 5; level++)
            if (GameSavePrefs.GetInt(CampaignProgression.GetGradedKey(level), 0) == 1) r.completedMask |= 1 << (level - 1);
    }
    public static void RecordTake(float duration)
    {
        var r = Read(); r.recordings = Add(r.recordings, 1);
        if (duration > 0 && !float.IsNaN(duration) && !float.IsInfinity(duration)) r.recordedSeconds += duration;
        else r.partialDuration = true;
        Store(r, true);
    }
    public static void RecordTransaction(int amount, string category)
    { var r = Read(); ApplyTransaction(r, amount, category); Store(r, false); }
    private static void ApplyTransaction(Record r, int amount, string category)
    {
        if (category == "Developer funds") return;
        if (amount < 0) r.spent += -(long)amount;
        else r.income += amount;
    }
    public static void RecordResult(PlayerAnalytics.Attempt attempt, float production)
    {
        if (attempt == null || Rank(attempt.grade) == 0 || attempt.level < 1 || attempt.level > 5) return;
        var r = Read(); ApplyResult(r, attempt, production, false); Store(r, true);
    }
    private static void ApplyResult(Record r, PlayerAnalytics.Attempt a, float production, bool migrating)
    {
        if (a.level < 1 || a.level > 5) return;
        int i = a.level - 1;
        bool passed = IsPassed(a.grade);
        float score = Score(a.score);
        r.attempts = Add(r.attempts, 1); r.scoreTotal += score;
        r.bestScore = Mathf.Max(r.bestScore, score); r.bestRank = Mathf.Max(r.bestRank, Rank(a.grade));
        r.bestByLevel[i] = Mathf.Max(r.bestByLevel[i], score);
        if (passed)
        {
            r.passed = Add(r.passed, 1); r.completedMask |= 1 << i;
            r.topRank |= a.grade == "S";
            r.polished |= a.post >= 95;
            r.balanced |= a.pre >= 90 && production >= 90 && a.post >= 90;
            r.comeback |= r.failedByLevel[i] > 0;
            // Old retained history may omit earlier failures. Never invent a first-attempt award.
            r.firstTry |= !migrating && (r.uncertainLevelsMask & (1 << i)) == 0 && r.attemptsByLevel[i] == 0 && !a.partial && !a.assisted;
            long expected = a.opening;
            if (a.transactions != null) foreach (var t in a.transactions) if (t != null) expected += t.amount;
            r.frugal |= !a.partial && !a.assisted && a.failedPurchases == 0 && a.closing >= 1000 && expected == a.closing;
        }
        else { r.failed = Add(r.failed, 1); r.failedByLevel[i] = Add(r.failedByLevel[i], 1); }
        r.attemptsByLevel[i] = Add(r.attemptsByLevel[i], 1);
    }
    private static void Store(Record record, bool notify)
    {
        GameSavePrefs.SetString(SaveKey, JsonUtility.ToJson(record));
        var unlocked = new List<string>();
        foreach (var a in Achievements)
        {
            int progress = Mathf.Clamp(a.progress(record), 0, a.goal);
            bool done = progress == a.goal || GameSavePrefs.GetInt("AchivDone_" + a.id, 0) == 1;
            if (done && GameSavePrefs.GetInt("AchivDone_" + a.id, 0) == 0) unlocked.Add(a.title);
            GameSavePrefs.SetInt("AchivProg_" + a.id, done ? a.goal : progress);
            GameSavePrefs.SetInt("AchivDone_" + a.id, done ? 1 : 0);
        }
        // Gameplay's enclosing analytics save checkpoints these values with the event itself.
        if (notify && !GameSavePrefs.IsRoomSession && unlocked.Count > 0)
            GameFeedback.Show("ACHIEVEMENT UNLOCKED\n" + unlocked[0] + (unlocked.Count > 1 ? $" (+{unlocked.Count - 1} more)" : "") + ". Open your profile [I].", GameFeedback.NoticeType.Success);
    }
}
