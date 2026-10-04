using System;
using System.Collections.Generic;

// Website/game wallet protocol. B-Coins and career checkpoints are separate.
[Serializable] public sealed class CCoinCosmetic
{
    public string id, name, description, kind, color;
    public int price;
}
[Serializable] public sealed class CCoinWallet
{
    public string accountId, currency;
    public int balance;
    public long revision;
    public string[] owned = new string[0];
    public CCoinCosmetic[] cosmetics = new CCoinCosmetic[0];
}
[Serializable] public sealed class CCoinReward
{
    public string operationId, careerId, completionId;
    public int contractLevel;
}
[Serializable] public sealed class CCoinPurchase
{
    public string operationId, itemId;
}
[Serializable] public sealed class CCoinReceipt
{
    public string operationId, provider, productId, receipt;
}
[Serializable] public sealed class CCoinOperationResult
{
    public string operationId, status;
    public CCoinWallet wallet;
}
[Serializable] public sealed class CCoinLocalState
{
    public CCoinWallet wallet;
    public List<CCoinReward> pendingRewards = new List<CCoinReward>();
    public CCoinPurchase pendingPurchase;
    public string selectedCosmetic;
}
public static class CCoinRules
{
    public const int ContractReward = 5;
    public const string Currency = "CC";
    public static bool Passed(string rank) => rank == "S" || rank == "A" || rank == "B" || rank == "C";
    public static bool ValidId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 128) return false;
        foreach (char c in id) if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ':')) return false;
        return true;
    }
    public static CCoinReward Reward(string careerId,int level)
    {
        if (!ValidId(careerId) || careerId.Length > 80 || level < 1 || level > 5) return null;
        string completion = careerId + ":" + level;
        return new CCoinReward { careerId = careerId, contractLevel = level, completionId = completion, operationId = "contract:" + completion };
    }
    public static bool ValidWallet(CCoinWallet wallet,string owner)
    {
        if (wallet == null || wallet.accountId != owner || wallet.currency != Currency || wallet.balance < 0 || wallet.revision < 0
            || wallet.owned == null || wallet.cosmetics == null || wallet.owned.Length > 1000 || wallet.cosmetics.Length > 200) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in wallet.owned) if (!ValidId(id) || !ids.Add(id)) return false;
        ids.Clear();
        foreach (var item in wallet.cosmetics)
            if (item == null || !ValidId(item.id) || !ids.Add(item.id) || item.kind != "profile_frame" || item.price < 1
                || string.IsNullOrWhiteSpace(item.name) || item.name.Length > 80 || (item.description != null && item.description.Length > 300)
                || !ValidColor(item.color)) return false;
        return true;
    }
    public static bool ValidColor(string value)
    {
        if (value == null || value.Length != 7 || value[0] != '#') return false;
        for (int i = 1; i < value.Length; i++) if (!Uri.IsHexDigit(value[i])) return false;
        return true;
    }
    public static bool Owns(CCoinWallet wallet,string id) => wallet?.owned != null && Array.IndexOf(wallet.owned,id) >= 0;
    public static CCoinCosmetic Find(CCoinWallet wallet,string id) => wallet?.cosmetics == null ? null : Array.Find(wallet.cosmetics,x => x != null && x.id == id);
    public static bool Completed(CCoinOperationResult result,string id) => result != null && result.operationId == id
        && (result.status == "applied" || result.status == "already_applied");
}
