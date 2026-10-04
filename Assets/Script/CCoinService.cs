using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Account-wide wallet. Local data is a display/offline cache, never authority to
// mint paid currency or debit it. Every mutation is settled by the existing title.
public sealed class CCoinService : MonoBehaviour
{
    public static CCoinService Instance { get; private set; }
    public event Action Changed;
    public CCoinWallet Wallet => state.wallet;
    public string SelectedCosmetic => state.selectedCosmetic;
    public int PendingRewards => state.pendingRewards.Count;
    public int Balance => state.wallet?.balance ?? 0;
    public bool Busy { get; private set; }
    public bool Verified { get; private set; }
    public string Status { get; private set; } = "Sign in to sync your cosmetic wallet.";
    private CCoinLocalState state = new CCoinLocalState();
    private string owner = "guest";
    private int session, requestGeneration;
    private float nextRefresh, deadline;
    public bool Authenticated => owner != "guest" && PlayFabSettings.staticPlayer.PlayFabId == owner && PlayFabClientAPI.IsClientLoggedIn();
    public bool CanBuy => Verified && !Busy && Authenticated && state.pendingPurchase == null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install() { Ensure(); }
    public static CCoinService Ensure()
    {
        if (Instance == null) new GameObject("Account C-Coins").AddComponent<CCoinService>();
        return Instance;
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
        BindAccount(UnityEngine.PlayerPrefs.GetString("PlayFabId","guest"));
    }
    private void OnDestroy()
    {
        session++; requestGeneration++; Busy = Verified = false;
        if (Instance == this) Instance = null;
    }
    private static string CacheKey(string account) => "SaveSystem.CCoins.v1." + account;
    public void BindAccount(string account)
    {
        session++; requestGeneration++; Busy = Verified = false;
        owner = CCoinRules.ValidId(account) ? account : "guest";
        try { state = JsonUtility.FromJson<CCoinLocalState>(UnityEngine.PlayerPrefs.GetString(CacheKey(owner),"")) ?? new CCoinLocalState(); }
        catch { state = new CCoinLocalState(); }
        if (!CCoinRules.ValidWallet(state.wallet,owner)) state.wallet = null;
        if (state.pendingRewards == null) state.pendingRewards = new List<CCoinReward>();
        state.pendingRewards.RemoveAll(x => x == null || CCoinRules.Reward(x.careerId,x.contractLevel)?.operationId != x.operationId
            || CCoinRules.Reward(x.careerId,x.contractLevel)?.completionId != x.completionId);
        if (state.pendingPurchase != null && (!CCoinRules.ValidId(state.pendingPurchase.itemId) || !CCoinRules.ValidId(state.pendingPurchase.operationId))) state.pendingPurchase = null;
        if (!CCoinRules.Owns(state.wallet,state.selectedCosmetic)) state.selectedCosmetic = null;
        nextRefresh = Time.unscaledTime;
        Publish(owner == "guest" ? "Sign in to sync C-Coins. Offline rewards stay pending." : "Cached wallet · waiting for account sync.");
    }
    private void Update()
    {
        if (Busy && Time.unscaledTime > deadline)
        { requestGeneration++; Finish(false,"Wallet request timed out. Pending transactions will retry safely."); }
        if (!Busy && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 60; Refresh(); }
    }
    private void OnApplicationFocus(bool focused) { if (focused) nextRefresh = Time.unscaledTime; }
    private void Save() { UnityEngine.PlayerPrefs.SetString(CacheKey(owner),JsonUtility.ToJson(state)); UnityEngine.PlayerPrefs.Save(); }
    private void Publish(string message) { Status = message; Changed?.Invoke(); }

    public bool QueueContractReward(int level,string rank)
    {
        if (GameSavePrefs.IsRoomSession || !CCoinRules.Passed(rank)) return false;
        // Reward identity follows checkpoint copies; returning to the result or
        // restoring the same checkpoint cannot create a different claim.
        string careerId = GameSavePrefs.GetString("CCoins.CareerRewardId","");
        if (!CCoinRules.ValidId(careerId))
        {
            careerId = GameSaveManager.Instance?.Active?.id ?? Guid.NewGuid().ToString("N");
            GameSavePrefs.SetString("CCoins.CareerRewardId",careerId);
        }
        var reward = CCoinRules.Reward(careerId,level);
        if (reward == null || GameSavePrefs.GetInt("CCoins.RewardQueued.Level" + level,0) == 1) return false;
        if (!state.pendingRewards.Exists(x => x.operationId == reward.operationId)) state.pendingRewards.Add(reward);
        Save(); // Durable before marking the career; retries retain the operation ID.
        GameSavePrefs.SetInt("CCoins.RewardQueued.Level" + level,1); GameSavePrefs.Save();
        nextRefresh = Time.unscaledTime;
        Publish("+5 C-Coins reward pending server confirmation."); return true;
    }
    // Invoke only after the existing save system has linked unclaimed guest careers.
    public void LinkGuestRewards()
    {
        if (owner == "guest") return;
        CCoinLocalState guest;
        try { guest = JsonUtility.FromJson<CCoinLocalState>(UnityEngine.PlayerPrefs.GetString(CacheKey("guest"),"")); }
        catch { return; }
        if (guest?.pendingRewards == null) return;
        foreach (var reward in guest.pendingRewards)
        {
            var expected = reward == null ? null : CCoinRules.Reward(reward.careerId,reward.contractLevel);
            if (expected != null && reward.operationId == expected.operationId && reward.completionId == expected.completionId
                && !state.pendingRewards.Exists(x => x.operationId == reward.operationId)) state.pendingRewards.Add(reward);
        }
        Save(); guest.pendingRewards.Clear();
        UnityEngine.PlayerPrefs.SetString(CacheKey("guest"),JsonUtility.ToJson(guest)); UnityEngine.PlayerPrefs.Save(); Changed?.Invoke();
    }
    public void Refresh()
    {
        if (Busy) return;
        if (!Authenticated) { Verified = false; Publish("Offline wallet · sign in to sync rewards and buy cosmetics."); return; }
        if (string.IsNullOrEmpty(CCoinSettings.FunctionName)) { Verified = false; Publish("C-Coins connection is not configured. Rewards are kept pending."); return; }
        Busy = true; Publish("Syncing account C-Coins…");
        Request("wallet",null,result =>
        {
            if (!AcceptWallet(result?.wallet)) { Finish(false,"C-Coins server is not ready or unavailable. Rewards stay pending."); return; }
            SyncPurchase();
        });
    }
    private void SyncPurchase()
    {
        if (state.pendingPurchase == null) { SyncRewardBatch(state.pendingRewards.ToArray(),0); return; }
        var purchase = state.pendingPurchase;
        Request("purchase",purchase,result =>
        {
            bool complete = CCoinRules.Completed(result,purchase.operationId);
            bool rejected = result?.operationId == purchase.operationId && result.status == "rejected";
            if ((!complete && !rejected) || !AcceptWallet(result.wallet))
            { Finish(false,"Purchase confirmation pending. Reconnect to confirm it; no second purchase ID will be created."); return; }
            state.pendingPurchase = null; Save(); SyncRewardBatch(state.pendingRewards.ToArray(),0);
        });
    }
    private void SyncRewardBatch(CCoinReward[] batch,int index)
    {
        if (index >= batch.Length || index >= 20)
        { Finish(true,state.pendingRewards.Count == 0 ? "Account wallet synced." : "Account wallet synced · " + state.pendingRewards.Count + " reward(s) awaiting verification."); return; }
        var reward = batch[index];
        Request("claimContract",reward,result =>
        {
            if (result == null) { Finish(false,"Reward sync unavailable. Rewards are kept pending."); return; }
            if (CCoinRules.Completed(result,reward.operationId) && AcceptWallet(result.wallet))
            { state.pendingRewards.RemoveAll(x => x.operationId == reward.operationId); Save(); }
            else
            {
                // An unverified older career must not starve later rewards when
                // the account has more than one bounded batch of pending claims.
                state.pendingRewards.RemoveAll(x => x.operationId == reward.operationId);
                state.pendingRewards.Add(reward); Save();
            }
            SyncRewardBatch(batch,index+1);
        });
    }
    private bool AcceptWallet(CCoinWallet wallet)
    {
        if (!CCoinRules.ValidWallet(wallet,owner) || (state.wallet != null && wallet.revision < state.wallet.revision)) return false;
        state.wallet = wallet;
        if (!CCoinRules.Owns(wallet,state.selectedCosmetic)) state.selectedCosmetic = null;
        Save(); return true;
    }
    private void Finish(bool verified,string message) { Busy = false; Verified = verified; nextRefresh = Time.unscaledTime + 60; Publish(message); }
    private void Request(string action,object data,Action<CCoinOperationResult> callback)
    {
        int version = session, generation = ++requestGeneration; deadline = Time.unscaledTime + 45;
        try
        {
            PlayFabClientAPI.ExecuteCloudScript<CCoinOperationResult>(new ExecuteCloudScriptRequest
            {
                FunctionName = CCoinSettings.FunctionName,
                FunctionParameter = new Dictionary<string,object> { {"action",action}, {"data",data} },
                GeneratePlayStreamEvent = false
            }, result =>
            {
                if (session != version || requestGeneration != generation) return;
                callback(result.Error == null ? result.FunctionResult as CCoinOperationResult : null);
            }, error => { if (session == version && requestGeneration == generation) callback(null); });
        }
        catch { if (session == version && requestGeneration == generation) callback(null); }
        // No custom HTTP destination, raw server errors, session tickets or receipts logged.
    }
    public void BuyCosmetic(string id)
    {
        var item = CCoinRules.Find(state.wallet,id);
        if (!CanBuy || item == null || CCoinRules.Owns(state.wallet,id)) return;
        if (Balance < item.price) { Publish("Not enough C-Coins. B-Coins cannot pay for cosmetics."); return; }
        state.pendingPurchase = new CCoinPurchase { itemId = id, operationId = "buy:" + Guid.NewGuid().ToString("N") };
        Save(); Refresh(); // Backend determines price, settles atomically and returns ownership.
    }
    public void Equip(string id)
    {
        if (!string.IsNullOrEmpty(id) && (!CCoinRules.Owns(state.wallet,id) || CCoinRules.Find(state.wallet,id) == null)) return;
        state.selectedCosmetic = id; Save(); Changed?.Invoke();
    }
    public void RequestCoinPurchase()
    {
        if (!Authenticated) { Publish("Sign in before buying C-Coins."); return; }
        if (!CCoinSettings.WebsitePurchasesEnabled || string.IsNullOrEmpty(CCoinSettings.WebsiteUrl))
        { Publish("PayMongo checkout is not enabled yet. The website's wallet and payment webhook must be connected first."); return; }
        Publish("Complete PayMongo checkout on the website using this same PlayFab account, then return to sync.");
        Application.OpenURL(CCoinSettings.WebsiteUrl); // No session ticket or account secret in the URL.
    }
    // A store SDK must supply the approved transaction's receipt. Neither amount
    // nor account ID comes from the client; the server validates and deduplicates it.
    public void ValidateStoreReceipt(string provider,string productId,string receipt)
    {
        if (string.Equals(provider,"paymongo",StringComparison.OrdinalIgnoreCase))
        { Publish("PayMongo coins are credited by the verified website webhook. Return to the game and sync your wallet."); return; }
        if (!CanBuy || !CCoinRules.ValidId(provider) || !CCoinRules.ValidId(productId) || string.IsNullOrWhiteSpace(receipt) || receipt.Length > 65536) return;
        string id;
        using (var hash = SHA256.Create()) id = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(provider + "\n" + productId + "\n" + receipt))).Replace("-","");
        var data = new CCoinReceipt { provider = provider, productId = productId, receipt = receipt, operationId = "receipt:" + id };
        Busy = true; Publish("Verifying store purchase…");
        Request("validateReceipt",data,result =>
        {
            bool accepted = CCoinRules.Completed(result,data.operationId) && AcceptWallet(result.wallet);
            Finish(accepted,accepted ? "Purchase verified. C-Coins updated." : "Purchase not confirmed. Restore through the store; no coins were granted locally.");
        });
    }
}
