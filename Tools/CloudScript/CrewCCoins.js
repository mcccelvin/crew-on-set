// Legacy PlayFab CloudScript handler for bounded, account-wide level rewards.
// Upload as a new revision in the Development title before deploying.
//
// IMPORTANT: the current game grades levels on the client. This handler cannot
// prove that the player passed. It limits claims to one 100-CC award per level
// per PlayFab account, but this is abuse-limiting rather than anti-cheat proof.
// The internal marker is written before credit so retries cannot normally
// duplicate an award; an interrupted "claiming" marker requires admin review.

var CC_REWARD_AMOUNT = 100;
var CC_CURRENCY_CODE = "CC";

handlers.CrewCCoins = function (args, context) {
    if (!args || args.action !== "claimContract" || !args.data) {
        return { status: "rejected" };
    }

    var data = args.data;
    var level = Number(data.contractLevel);
    var careerId = typeof data.careerId === "string" ? data.careerId : "";
    var completionId = careerId + ":" + level;
    var operationId = "contract:" + completionId;

    // Match the client's deterministic reward envelope. Never accept a
    // player ID or reward amount from the request.
    if (!currentPlayerId || !isFinite(level) || Math.floor(level) !== level ||
        level < 1 || level > 5 || careerId.length < 1 || careerId.length > 80 ||
        !/^[A-Za-z0-9_.:-]+$/.test(careerId) || data.completionId !== completionId ||
        data.operationId !== operationId) {
        return { operationId: data.operationId || "", status: "rejected" };
    }

    // Enforce a conservative cap independent of client-generated career IDs:
    // at most one award for each of the five campaign levels per account.
    var key = "CCoinReward.Level" + level;
    var stored = server.GetUserInternalData({ PlayFabId: currentPlayerId, Keys: [key] });
    var existing = stored.Data && stored.Data[key] ? stored.Data[key].Value : null;
    if (existing) {
        var prior;
        try { prior = JSON.parse(existing); } catch (e) { prior = null; }
        if (prior && prior.status === "applied") {
            return { operationId: operationId, status: "already_applied" };
        }
        // A previous call may have stopped after reserving this level. Do not
        // grant a second time automatically; retain the player's pending claim.
        return { operationId: operationId, status: "awaiting_verification" };
    }

    // Reserve before crediting. PlayFab legacy currency and Internal Data do
    // not share a transaction, so an interrupted reservation needs review.
    var marker = {};
    marker[key] = JSON.stringify({ status: "claiming", operationId: operationId, amount: CC_REWARD_AMOUNT });
    server.UpdateUserInternalData({
        PlayFabId: currentPlayerId,
        Data: marker
    });

    try {
        server.AddUserVirtualCurrency({
            PlayFabId: currentPlayerId,
            VirtualCurrency: CC_CURRENCY_CODE,
            Amount: CC_REWARD_AMOUNT,
            CustomTags: { source: "CrewCCoins", level: String(level), operationId: operationId }
        });
        marker[key] = JSON.stringify({ status: "applied", operationId: operationId, amount: CC_REWARD_AMOUNT });
        server.UpdateUserInternalData({
            PlayFabId: currentPlayerId,
            Data: marker
        });
        return { operationId: operationId, status: "applied" };
    } catch (e) {
        // Keep the reservation to prevent ambiguous network failures from
        // becoming duplicate grants. An admin can inspect the player's CC
        // balance and marker before resolving this rare state.
        log.error("CrewCCoins could not confirm a reserved level reward.", { playerId: currentPlayerId, level: level });
        return { operationId: operationId, status: "awaiting_verification" };
    }
};
