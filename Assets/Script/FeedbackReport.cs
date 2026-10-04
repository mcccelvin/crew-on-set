using System;
using System.Text;
using System.Text.RegularExpressions;

// Presentation only: never calculates a new grade or applies a payment.
public sealed class FeedbackReport
{
    public static readonly string[] Titles = { "PRE-PRODUCTION", "PRODUCTION", "POST-PRODUCTION", "BUDGET REVIEW", "CLIENT DECISION" };
    public readonly string[] bodies = new string[5];
    public readonly string[] scores = new string[5];
    public readonly bool passed;
    public readonly string rank;
    public readonly float overall;
    private static readonly Regex Tags = new Regex("<[^>]+>");

    public FeedbackReport(ProductionGrades grades, string budgetFallback)
    {
        rank = (grades.letterGrade ?? "F").ToUpperInvariant();
        passed = rank == "S" || rank == "A" || rank == "B" || rank == "C";
        overall = (grades.preProductionScore + grades.productionScore + grades.postProductionScore) / 3f;
        scores[0] = grades.preProductionScore.ToString("F1") + " / 100";
        scores[1] = grades.productionScore.ToString("F1") + " / 100";
        scores[2] = grades.postProductionScore.ToString("F1") + " / 100";
        scores[3] = grades.earnedBCoins > 0 ? "THIS SUBMISSION: +" + grades.earnedBCoins.ToString("N0") + " B-COINS" : "THIS SUBMISSION: NO NEW PAYMENT";
        scores[4] = "RANK " + rank + "   |   OVERALL " + overall.ToString("F1") + " / 100";
        var sections = new[] { new StringBuilder(), new StringBuilder(), new StringBuilder(), new StringBuilder(), new StringBuilder() };
        int section = 4;
        foreach (string line in (grades.feedback ?? "").Replace("\r", "").Split('\n'))
        {
            string plain = Tags.Replace(line, "").Trim().ToUpperInvariant();
            if (Heading(plain, "PRE-PRODUCTION")) { section = 0; continue; }
            if (Heading(plain, "POST-PRODUCTION")) { section = 2; continue; }
            if (Heading(plain, "PRODUCTION")) { section = 1; continue; }
            if (plain == "BUDGET REVIEW") { section = 3; continue; }
            if (plain.StartsWith("CONTRACT GATE:") || plain.StartsWith("QUALITY GATE:") || plain == "YOUR PROGRESS" || plain == "YOUR NEXT STEP") section = 4;
            sections[section].AppendLine(line);
        }
        for (int i = 0; i < bodies.Length; i++) bodies[i] = PaperInk(sections[i].ToString().Trim());
        for (int i = 0; i < 3; i++)
            if (string.IsNullOrWhiteSpace(bodies[i])) bodies[i] = "No additional department notes were saved for this submission. The score above is the submitted result.";
        if (string.IsNullOrWhiteSpace(bodies[3])) bodies[3] = PaperInk(budgetFallback ?? "No tracked budget breakdown is available for this submission.");
        bodies[4] = (passed ? "<b>CONTRACT PASSED</b>\nThe client approved this submission.\n\n" :
            "<color=#A32323><b>CONTRACT FAILED</b></color>\nA high overall score does not override a missing mandatory requirement or department minimum.\n\n") + bodies[4];
    }
    private static bool Heading(string value, string heading) =>
        value.StartsWith("--- " + heading + " ---", StringComparison.Ordinal) ||
        Regex.IsMatch(value, @"^[123]\.\s*" + Regex.Escape(heading) + @"\s*[—–-]");
    public static string PaperInk(string value) => (value ?? "").Replace("<color=white>", "<color=#241B14>")
        .Replace("<color=green>", "<color=#22652D>").Replace("<color=yellow>", "<color=#795012>")
        .Replace("<color=red>", "<color=#A32323>");
    public static string BossLine(int page, bool passed, bool tutorial)
    {
        switch (page)
        {
            case 0: return (tutorial ? "Let's read your first result, one paper at a time. " : "Let's review the preparation. ") + "Check the set and required products. Scroll for every note, then press <color=#B00020>NEXT</color>.";
            case 1: return "This is your camera and lighting result. Use the notes to improve framing, visibility and light setup. <color=#B00020>BACK</color> returns to the previous paper.";
            case 2: return "Now the edit: timing, graphics, sound and color. Red notes explain what must change. Read the whole checklist before moving on.";
            case 3: return "Here's the budget. Payment and spending are separate from technical quality. Reuse owned equipment and reserve cash for required props and SD cards.";
            default: return passed ? "Approved! The stamp confirms the contract passed. You can read any paper again before choosing what to do next." :
                "We need another take. The FAILED stamp follows the contract rules, not just the overall number. Read the red corrections before replaying.";
        }
    }
}
