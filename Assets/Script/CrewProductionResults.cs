using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable] public sealed class CrewProductionResult
{
    public string productionId, submissionId, playedUtc, nextStep;
    public int level;
    public ProductionGrades grades;
    public ProductionBudgetReview budget;
    // Photon actor numbers and roles only. Account IDs never enter the room payload.
    public List<CrewMember> participants = new List<CrewMember>();
}

public static class CrewProductionResults
{
    public static void Begin(CrewSession state)
    {
        state.productionId = Guid.NewGuid().ToString("N"); state.resultLog = null;
        state.budgetReview = new ProductionBudgetReview { available = true, complete = true, openingCash = state.budget, remainingCash = state.budget };
    }
    // Only called at the existing, successful shared-budget mutation. No price reconstruction.
    public static void BudgetChanged(CrewSession state, int amount)
    {
        if (state.budgetReview == null) return; // Old in-progress rooms did not track these values.
        if (amount >= 0) state.budgetReview.income += amount;
        else state.budgetReview.amountSpent -= (long)amount;
        state.budgetReview.remainingCash = state.budget;
        state.budgetReview.complete &= (long)state.budgetReview.openingCash + state.budgetReview.income - state.budgetReview.amountSpent == state.budget;
    }
    public static string BudgetFeedback(CrewSession state)
    {
        var text = new StringBuilder("SHARED CREW BUDGET\n");
        if (state.checkpoint != null) text.AppendLine("Opening contract balance: " + state.checkpoint.budget.ToString("N0") + " B-Coins.");
        if (state.budgetReview != null && state.budgetReview.available)
        {
            text.AppendLine("Income: " + state.budgetReview.income.ToString("N0") + " B-Coins.");
            text.AppendLine("Spent: " + state.budgetReview.amountSpent.ToString("N0") + " B-Coins.");
            if (!state.budgetReview.complete) text.AppendLine("Some balance changes were outside tracking; treat this budget summary as incomplete.");
        }
        else text.AppendLine("Detailed spending was not tracked in this older room.");
        text.AppendLine("Current crew balance: " + state.budget.ToString("N0") + " B-Coins.");
        text.Append("The room does not track categorized expenses. Owned equipment can be reused. This review never charges or pays again; only the host can advance or restart the contract.");
        return text.ToString();
    }
    public static CrewProductionResult Freeze(CrewSession state, string nextStep)
    {
        if (string.IsNullOrEmpty(state.productionId)) state.productionId = Guid.NewGuid().ToString("N");
        var budget = state.budgetReview == null ? new ProductionBudgetReview { remainingCash = state.budget } :
            JsonUtility.FromJson<ProductionBudgetReview>(JsonUtility.ToJson(state.budgetReview));
        budget.feedback = BudgetFeedback(state);
        var result = new CrewProductionResult { productionId = state.productionId, submissionId = Guid.NewGuid().ToString("N"),
            playedUtc = DateTime.UtcNow.ToString("o"), level = state.contractLevel, grades = state.result, budget = budget, nextStep = nextStep };
        foreach (var member in state.members)
            result.participants.Add(new CrewMember { id = member.id, roles = member.roles });
        return result;
    }
    // owner is the local, authenticated account captured when joining, never a Photon ID.
    public static ProductionLogRecord ForPlayer(CrewProductionResult result, int actor, string owner)
    {
        if (result == null || string.IsNullOrEmpty(owner) || owner == "guest") return null;
        var member = result.participants?.Find(m => m.id == actor && m.roles != 0);
        if (member == null) return null;
        var roles = new List<string>();
        foreach (var role in new[] { CrewRole.Director, CrewRole.Camera, CrewRole.AVTechnician, CrewRole.Editor })
            if ((member.roles & (int)role) != 0) roles.Add(MultiplayerContractManager.RoleName(role));
        return ProductionLogRecord.FromResult(result.productionId, result.submissionId, "", owner, "multiplayer", roles.ToArray(),
            result.level, result.playedUtc, result.grades, result.budget, result.nextStep);
    }
}
