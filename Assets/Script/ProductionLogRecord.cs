using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

// A copy of the completed result, not a second grading or economy system.
[Serializable] public sealed class ProductionBudgetReview
{
    public bool available, complete;
    public int openingCash, remainingCash;
    public long income, amountSpent;
    public string feedback;
}

[Serializable] public sealed class ProductionLogRecord
{
    public const string NotRecorded = "Not recorded";
    public string id, productionId, submissionId, careerId, mode, production, client, playerId, role, playedUtc, date;
    public string[] roles;
    public int level;
    public float score, preProductionScore, productionScore, postProductionScore;
    public string rank, preProductionFeedback, productionFeedback, postProductionFeedback, decision, decisionFeedback, nextStep;
    public ProductionBudgetReview budget;
    public bool legacyDetails;
    public float legacyCameraScore, legacyLightingScore;

    // Older analytics have real scores, but no saved result papers. Never run a
    // grader, reconstruct a production total, invent a timestamp or generate advice.
    public static ProductionLogRecord FromLegacyAttempt(GameSaveSlot slot, PlayerAnalytics.Attempt attempt, string owner)
    {
        if (string.IsNullOrWhiteSpace(owner) || owner == "guest" || slot == null || slot.owner != owner || attempt == null || !attempt.closed ||
            attempt.level < 1 || attempt.level > 5 ||
            !(CareerProfileProgress.IsPassed(attempt.grade) || attempt.grade == "F") ||
            !DateTime.TryParseExact(attempt.date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            !Finite(attempt.score) || !Finite(attempt.pre) || !Finite(attempt.post) ||
            !Finite(attempt.camera) || !Finite(attempt.lighting)) return null;

        string origin = slot.values.Find(v => v.key == GameSaveRepository.ProfileOriginKey && v.kind == 2)?.text;
        if (string.IsNullOrEmpty(origin)) origin = slot.id;
        // Stable across login retries and cloud-conflict copies of the same career.
        string identity = string.Join("|", origin, attempt.date, attempt.level.ToString(CultureInfo.InvariantCulture),
            attempt.grade, attempt.score.ToString("R", CultureInfo.InvariantCulture), attempt.pre.ToString("R", CultureInfo.InvariantCulture),
            attempt.camera.ToString("R", CultureInfo.InvariantCulture), attempt.lighting.ToString("R", CultureInfo.InvariantCulture),
            attempt.post.ToString("R", CultureInfo.InvariantCulture), attempt.opening.ToString(CultureInfo.InvariantCulture),
            attempt.closing.ToString(CultureInfo.InvariantCulture), attempt.takes.ToString(CultureInfo.InvariantCulture));
        string submission;
        using (var hash = SHA256.Create())
            submission = "legacy-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
        return new ProductionLogRecord {
            id = submission + ":" + owner, productionId = submission, submissionId = submission,
            careerId = origin, mode = "singleplayer", playerId = owner,
            roles = new[] { "Director", "Camera", "AV Technician", "Editor" }, role = "Director, Camera, AV Technician, Editor",
            production = CampaignProgression.GetContractName(attempt.level), client = MultiplayerContractManager.Title(attempt.level),
            level = attempt.level, date = attempt.date, playedUtc = attempt.playedUtc,
            score = attempt.score, rank = attempt.grade, preProductionScore = attempt.pre, postProductionScore = attempt.post,
            legacyCameraScore = attempt.camera, legacyLightingScore = attempt.lighting, legacyDetails = true,
            preProductionFeedback = NotRecorded, productionFeedback = NotRecorded, postProductionFeedback = NotRecorded,
            decision = CareerProfileProgress.IsPassed(attempt.grade) ? "passed" : "failed", decisionFeedback = NotRecorded,
            nextStep = string.IsNullOrEmpty(attempt.nextStep) ? NotRecorded : attempt.nextStep,
            budget = attempt.budget ?? new ProductionBudgetReview {
                openingCash = attempt.opening, remainingCash = attempt.closing, feedback = NotRecorded
            }
        };
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static ProductionLogRecord FromResult(string productionId, string submissionId, string careerId,
        string playerId, string mode, string[] roles, int level, string playedUtc, ProductionGrades grades,
        ProductionBudgetReview budget, string nextStep)
    {
        var paper = new FeedbackReport(grades, budget?.feedback, level);
        return new ProductionLogRecord {
            id = submissionId + ":" + playerId, productionId = productionId, submissionId = submissionId,
            careerId = careerId ?? "", playerId = playerId ?? "", mode = mode, roles = roles,
            role = string.Join(", ", roles), level = level,
            production = CampaignProgression.GetContractName(level), client = MultiplayerContractManager.Title(level),
            playedUtc = playedUtc, date = playedUtc.Substring(0, 10), score = paper.overall, rank = paper.rank,
            preProductionScore = grades.preProductionScore, productionScore = grades.productionScore,
            postProductionScore = grades.postProductionScore, preProductionFeedback = paper.bodies[0],
            productionFeedback = paper.bodies[1], postProductionFeedback = paper.bodies[2],
            decision = paper.passed ? "passed" : "failed", decisionFeedback = paper.bodies[4],
            budget = budget, nextStep = nextStep ?? ""
        };
    }

    // Existing website card fields stay present; detailed fields are additive.
    public Dictionary<string, object> ToWire()
    {
        var budgetFields = new Dictionary<string, object> {
            { "available", budget != null && budget.available }, { "tracking_complete", budget != null && budget.complete },
            { "opening_cash", budget != null && budget.available ? (object)budget.openingCash : null },
            { "income", budget != null && budget.available ? (object)budget.income : null },
            { "amount_spent", budget != null && budget.available ? (object)budget.amountSpent : null },
            { "remaining_cash", budget != null ? (object)budget.remainingCash : null },
            { "feedback", budget?.feedback ?? "" }
        };
        var phases = new Dictionary<string, object> {
            { "pre_production", Phase(preProductionScore, preProductionFeedback) },
            { "production", Phase(productionScore, productionFeedback) },
            { "post_production", Phase(postProductionScore, postProductionFeedback) }
        };
        var wire = new Dictionary<string, object> {
            { "id", id }, { "production_id", productionId }, { "submission_id", submissionId }, { "career_id", careerId },
            { "mode", mode }, { "production", production }, { "contract_name", production }, { "product_name", production },
            { "client", client }, { "client_brand", client }, { "player_id", playerId }, { "playfab_id", playerId },
            { "role", role }, { "roles", roles }, { "level", level }, { "date", date }, { "played_at", playedUtc },
            { "score", score }, { "overall_score", score }, { "rank", rank }, { "letter_grade", rank },
            { "pre_production_score", preProductionScore }, { "production_score", productionScore },
            { "post_production_score", postProductionScore }, { "pre_production_feedback", preProductionFeedback },
            { "production_feedback", productionFeedback }, { "post_production_feedback", postProductionFeedback },
            { "phases", phases }, { "budget", budgetFields }, { "budget_feedback", budget?.feedback ?? "" },
            { "client_decision", decision }, { "passed", decision == "passed" }, { "result", decisionFeedback },
            { "summary", decisionFeedback }, { "setupNotes", preProductionFeedback },
            { "your_next_step", nextStep }, { "next_step", nextStep }
        };
        if (legacyDetails)
        {
            // Null means unknown, not a zero score/cost. Date-only history stays date-only.
            wire["played_at"] = string.IsNullOrEmpty(playedUtc) ? null : playedUtc;
            wire["production_score"] = null;
            ((Dictionary<string, object>)phases["production"])["score"] = null;
            budgetFields["opening_cash"] = budget.openingCash;
            wire["camera_score"] = legacyCameraScore;
            wire["lighting_score"] = legacyLightingScore;
            wire["history_source"] = "legacy_career";
            wire["details_complete"] = false;
            var missing = new List<string> { "production_score", "pre_production_feedback", "production_feedback", "post_production_feedback", "client_decision_feedback" };
            if (string.IsNullOrEmpty(playedUtc)) missing.Add("played_at");
            if (nextStep == NotRecorded) missing.Add("your_next_step");
            if (!budget.available) { missing.Add("budget.income"); missing.Add("budget.amount_spent"); }
            if (budget.feedback == NotRecorded || string.IsNullOrEmpty(budget.feedback)) missing.Add("budget.feedback");
            wire["missing_fields"] = missing;
        }
        return wire;
    }
    private static Dictionary<string, object> Phase(float score, string feedback) =>
        new Dictionary<string, object> { { "score", score }, { "feedback", feedback } };

    public bool IsOwnedBy(string owner) => !string.IsNullOrEmpty(owner) && owner == playerId &&
        !string.IsNullOrEmpty(productionId) && !string.IsNullOrEmpty(submissionId) && id == submissionId + ":" + playerId &&
        (mode == "singleplayer" || mode == "multiplayer") && roles != null && roles.Length > 0 &&
        (mode != "multiplayer" || Array.TrueForAll(roles, r => r == "Director" || r == "Camera" || r == "AV Technician" || r == "Editor")) &&
        (decision == "passed" || decision == "failed");
}
