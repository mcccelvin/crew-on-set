using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Saved through the selected career, never a static cache shared between accounts.
public static class PlayerAnalytics
{
    private const string Key = "Analytics.Career.v1";
    [Serializable] public class Transaction
    {
        public string item, category;
        public int amount;
    }
    [Serializable] public class Attempt
    {
        public int level, opening, closing, failedPurchases, takes;
        public float pre, camera, lighting, post, score;
        public bool partial, closed, assisted;
        public string grade, date;
        public string submissionId, playedUtc, nextStep, completionFeedback;
        public ProductionBudgetReview budget;
        public ProductionLogRecord productionLog;
        public List<Transaction> transactions = new List<Transaction>();
    }
    [Serializable] private class History
    {
        public Attempt active;
        public List<Attempt> results = new List<Attempt>();
    }
    private static History Load()
    { return Load(GameSavePrefs.GetInt(Key + ".Count", 0), key => GameSavePrefs.GetString(key, "")); }
    private static History Load(int count, Func<string, string> read)
    {
        var json = new StringBuilder();
        for (int i = 0; i < Mathf.Clamp(count, 0, 1500); i++) json.Append(read(Key + "." + i));
        if (count == 0) json.Append(read(Key));
        try { return JsonUtility.FromJson<History>(json.ToString()) ?? new History(); }
        catch (ArgumentException) { return new History(); }
    }
    private static void Save(History data)
    {
        // Save values have a 10,000-character limit. Keep each history chunk below it.
        string json = JsonUtility.ToJson(data);
        int oldCount = GameSavePrefs.GetInt(Key + ".Count", 0);
        int count = (json.Length + 7999) / 8000;
        for (int i = 0; i < count; i++)
            GameSavePrefs.SetString(Key + "." + i, json.Substring(i * 8000, Math.Min(8000, json.Length - i * 8000)));
        for (int i = count; i < oldCount; i++) GameSavePrefs.DeleteKey(Key + "." + i);
        GameSavePrefs.SetInt(Key + ".Count", count);
        GameSavePrefs.DeleteKey(Key);
        GameSavePrefs.Save();
        GameSaveManager.Instance?.SaveCheckpoint();
    }
    private static Attempt Ensure(History data, int level)
    {
        if (data.active == null || data.active.closed || data.active.level != level)
            data.active = new Attempt { level = level, opening = GameSavePrefs.GetInt("PlayerMoney", 0), partial = true };
        return data.active;
    }
    public static void Begin(int level, bool retry = false)
    {
        CareerProfileProgress.Read();
        var data = Load();
        if (!retry && data.active != null && !data.active.closed && data.active.level == level) return;
        data.active = new Attempt { level = level, opening = GameSavePrefs.GetInt("PlayerMoney", 0) };
        Save(data);
    }
    public static string EquipmentCategory(string item)
    {
        return (item ?? "").IndexOf("SD", StringComparison.OrdinalIgnoreCase) >= 0 ? "Consumables" : "Reusable equipment";
    }
    public static void EnsureTracking()
    {
        var data = Load(); Ensure(data, CampaignProgression.GetCurrentLevel()); Save(data);
    }
    // Call before changing the balance, so a pre-existing career gets an honest opening snapshot.
    public static void TransactionMade(int amount, string category, string item)
    {
        CareerProfileProgress.RecordTransaction(amount, category);
        var data = Load();
        var a = Ensure(data, CampaignProgression.GetCurrentLevel());
        if (a.transactions.Count >= 64) item = "Additional " + category;
        var existing = a.transactions.Find(t => t.category == category && t.item == item && (t.amount < 0) == (amount < 0));
        if (existing == null) a.transactions.Add(new Transaction { amount = amount, category = category, item = item });
        else existing.amount = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, (long)existing.amount + amount));
        if (category == "Developer funds") a.assisted = true;
        Save(data);
    }
    public static void PurchaseRejected()
    {
        var data = Load(); Ensure(data, CampaignProgression.GetCurrentLevel()).failedPurchases++; Save(data);
    }
    public static void TakeRecorded(int level) { TakeRecorded(level, 0); }
    public static void TakeRecorded(int level, float duration)
    {
        CareerProfileProgress.RecordTake(duration);
        var data = Load(); Ensure(data, level).takes++; Save(data);
    }
    public static void CaptureScores(int level, float camera, float lighting)
    {
        var data = Load(); var a = Ensure(data, level);
        a.camera = camera; a.lighting = lighting; Save(data);
    }
    private static string BudgetReport(Attempt a)
    {
        long income = 0, spent = 0, investments = 0;
        var categories = new Dictionary<string, long>();
        foreach (var t in a.transactions)
        {
            if (t.amount >= 0) { income += t.amount; continue; }
            long cost = -(long)t.amount; spent += cost;
            if (t.category == "Reusable equipment" || t.category == "Reusable set") investments += cost;
            if (!categories.ContainsKey(t.category)) categories[t.category] = 0;
            categories[t.category] += cost;
        }
        var s = new StringBuilder();
        s.AppendLine("BUDGET REVIEW");
        if (a.partial) s.AppendLine("Tracking began during this contract. Earlier purchases are not included.");
        if (a.assisted) s.AppendLine("Developer funds used: this attempt is not an unassisted budget result.");
        s.AppendLine($"Opening cash: {a.opening:N0} B-Coins | Income: {income:N0}");
        s.AppendLine($"Spent: {spent:N0} | Cash remaining: {a.closing:N0}");
        foreach (var c in categories) s.AppendLine($"  {c.Key}: {c.Value:N0}");
        s.AppendLine($"Reusable investments: {investments:N0} | Other costs: {spent - investments:N0}");
        s.AppendLine("Owned equipment and sets are counted only when bought, not charged again in later contracts.");
        if ((long)a.opening + income - spent != a.closing)
            s.AppendLine("Some balance changes were outside tracking; treat this budget summary as incomplete.");
        if (a.failedPurchases > 0)
            s.AppendLine($"{a.failedPurchases} purchase attempts exceeded your balance. Reserve money for required props and SD cards before optional purchases.");
        if (a.grade == "F") s.AppendLine("The brief is not yet met. Use the technical corrections below before spending on more equipment.");
        else if (a.closing >= ProductionEconomy.SDCard)
            s.AppendLine("You completed the brief and retained enough cash for at least one new SD card. Reuse your setup where possible.");
        else s.AppendLine("You completed the brief with little cash left. Plan consumables before your next purchase.");
        s.AppendLine("Budget feedback is coaching; spending less alone does not improve your technical grade.");
        // The paper and cloud record share these exact totals from the existing ledger.
        a.budget = new ProductionBudgetReview { available = true, complete = !a.partial && !a.assisted && (long)a.opening + income - spent == a.closing,
            openingCash = a.opening, remainingCash = a.closing, income = income, amountSpent = spent, feedback = s.ToString() };
        return a.budget.feedback;
    }
    public static string Complete(int level, ProductionGrades grades)
    {
        var data = Load();
        if (data.active != null && data.active.closed && data.active.level == level)
            return data.active.completionFeedback ?? BudgetReport(data.active);
        var a = Ensure(data, level);
        a.pre = grades.preProductionScore; a.post = grades.postProductionScore;
        a.score = (a.pre + grades.productionScore + a.post) / 3f;
        a.grade = grades.letterGrade; a.closing = GameSavePrefs.GetInt("PlayerMoney", 0);
        a.playedUtc = DateTime.UtcNow.ToString("o"); a.date = a.playedUtc.Substring(0, 10);
        a.submissionId = Guid.NewGuid().ToString("N"); a.closed = true;
        Attempt previous = data.results.FindLast(x => x.level == level);
        string trend = previous == null ? "First tracked attempt." : $"Score change since your last attempt: {a.score - previous.score:+0.0;-0.0;0.0} points.";
        CareerProfileProgress.RecordResult(a, grades.productionScore);
        data.results.Add(a);
        if (data.results.Count > 50) data.results.RemoveAt(0);
        a.nextStep = NextStep(a.pre, grades.productionScore, a.post, a.camera, a.lighting);
        a.completionFeedback = "<b>YOUR NEXT STEP</b>\n" + a.nextStep + "\n" + trend + $"\nRecorded takes: {a.takes}\n\n" + BudgetReport(a);
        Save(data);
        return a.completionFeedback;
    }
    // Same coaching used by the result papers; an uploader must not generate its own advice.
    public static string NextStep(float pre, float production, float post, float camera, float lighting) =>
        pre <= production && pre <= post ? "PRE-PRODUCTION: revisit the required set, product and actor setup."
        : production <= post ? (camera / 70f <= lighting / 30f ? "CAMERA: reframe the required subjects and keep them visible throughout the take." : "LIGHTING: check power, aim and intensity before recording again.")
        : "POST-PRODUCTION: correct the timing, branding and color issues listed in the detailed feedback.";

    public static void SaveCompletedResult(int level, ProductionGrades grades)
    {
        if (GameSavePrefs.IsRoomSession) return;
        var data = Load(); var a = data.active;
        if (a == null || !a.closed || a.level != level || string.IsNullOrEmpty(a.submissionId)) return;
        if (a.productionLog == null)
        {
            string playerId = UnityEngine.PlayerPrefs.GetString("PlayFabId", "");
            if (playerId == "guest") playerId = "";
            string careerId = GameSaveManager.Instance?.Active?.id ?? "";
            a.productionLog = ProductionLogRecord.FromResult(a.submissionId, a.submissionId, careerId, playerId,
                "singleplayer", new[] { "Director", "Camera", "AV Technician", "Editor" }, level, a.playedUtc, grades, a.budget, a.nextStep);
            // active and results are independently deserialized instances after a scene change.
            int index = data.results.FindLastIndex(r => r.submissionId == a.submissionId);
            if (index >= 0) data.results[index] = a;
            Save(data);
        }
        GameSaveManager.Ensure().RecordProduction(a.productionLog);
    }
    // A fresh deserialized snapshot; callers cannot mutate the stored analytics history.
    public static List<Attempt> ProfileHistory()
    {
        var data = Load();
        var result = data.results ?? new List<Attempt>();
        if (data.active != null && !data.active.closed) result.Add(data.active);
        return result;
    }
    public static List<Attempt> ProfileHistory(GameSaveSlot slot)
    {
        if (slot == null) return new List<Attempt>();
        var data = Load(slot.Int(Key + ".Count", 0), key => slot.values.Find(v => v.key == key && v.kind == 2)?.text ?? "");
        var result = data.results ?? new List<Attempt>();
        if (data.active != null && !data.active.closed) result.Add(data.active);
        return result;
    }
    public static string Summary()
    {
        var data = Load();
        var s = new StringBuilder("YOUR CAREER ANALYTICS\n\n");
        if (data.results.Count == 0) return s.Append("Complete a contract to see tracked scores and budget history. Existing results are not invented retroactively.").ToString();
        s.AppendLine("Recent attempts (up to 50 retained):");
        for (int i = data.results.Count - 1; i >= Mathf.Max(0, data.results.Count - 10); i--)
        {
            var a = data.results[i];
            s.AppendLine($"\n{a.date} | Level {a.level} | {a.grade} | {a.score:F1}/100");
            s.AppendLine($"Set {a.pre:F0}/100 | Camera {a.camera:F0}/70 | Light {a.lighting:F0}/30 | Edit {a.post:F0}/100");
            long spent = 0; foreach (var t in a.transactions) if (t.amount < 0) spent -= (long)t.amount;
            s.AppendLine($"Spent {spent:N0} | Remaining {a.closing:N0} B-Coins | Takes {a.takes}" + (a.partial ? " | Partial tracking" : "") + (a.assisted ? " | Developer funds" : ""));
        }
        return s.ToString();
    }
}
