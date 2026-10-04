using System;
using System.Collections.Generic;

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
    public string id, productionId, submissionId, careerId, mode, production, client, playerId, role, playedUtc, date;
    public string[] roles;
    public int level;
    public float score, preProductionScore, productionScore, postProductionScore;
    public string rank, preProductionFeedback, productionFeedback, postProductionFeedback, decision, decisionFeedback, nextStep;
    public ProductionBudgetReview budget;

    public static ProductionLogRecord FromResult(string productionId, string submissionId, string careerId,
        string playerId, string mode, string[] roles, int level, string playedUtc, ProductionGrades grades,
        ProductionBudgetReview budget, string nextStep)
    {
        var paper = new FeedbackReport(grades, budget?.feedback);
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
        return new Dictionary<string, object> {
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
    }
    private static Dictionary<string, object> Phase(float score, string feedback) =>
        new Dictionary<string, object> { { "score", score }, { "feedback", feedback } };

    public bool IsOwnedBy(string owner) => !string.IsNullOrEmpty(owner) && owner == playerId &&
        !string.IsNullOrEmpty(productionId) && !string.IsNullOrEmpty(submissionId) && id == submissionId + ":" + playerId &&
        (mode == "singleplayer" || mode == "multiplayer") && roles != null && roles.Length > 0 &&
        (mode != "multiplayer" || Array.TrueForAll(roles, r => r == "Director" || r == "Camera" || r == "AV Technician" || r == "Editor")) &&
        (decision == "passed" || decision == "failed");
}
