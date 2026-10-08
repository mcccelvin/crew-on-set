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
    [Serializable] private sealed class WebsiteCatalogData { public string assetKey, rarity; }
    public static CCoinService Instance { get; private set; }
    public event Action Changed;
    public CCoinWallet Wallet => state.wallet;
    public string SelectedCosmetic => state.selectedCosmetic;
    public string[] EquippedParts => state.equippedParts.ToArray();
    public bool IsEquipped(string id) => state.selectedCosmetic == id || state.equippedParts.Contains(id);
    public int PendingRewards => state.pendingRewards.Count;
    public long PendingCoins => (long)PendingRewards * CCoinRules.ContractReward;
    public bool IsGuest => owner == "guest" && !DevWalletActive;
    public string GuestShopStatus => PendingRewards > 0
        ? $"{PendingCoins:N0} C-Coins pending · sign in to verify and spend."
        : "Guest · equip free items, try on any item. Sign in to buy.";
    public int Balance => state.wallet?.balance ?? 0;
    public bool Busy { get; private set; }
    public bool Verified { get; private set; }
    public string Status { get; private set; } = "Sign in to sync your cosmetic wallet.";
    private CCoinLocalState state = new CCoinLocalState();
    private CCoinLocalState realState;
    public bool DevWalletActive => realState != null;
    private string owner = "guest";
    private int session, requestGeneration;
    private float nextRefresh, deadline;
    public bool Authenticated => owner != "guest" && PlayFabSettings.staticPlayer.PlayFabId == owner && PlayFabClientAPI.IsClientLoggedIn();
    public bool CanBuy => DevWalletActive || Verified && !Busy && Authenticated && state.pendingPurchase == null;
    private static CCoinCosmetic FindWebsiteCosmetic(CatalogItem item)
    {
        if(item==null)return null;
        var cosmetic=CharacterCosmetics.FindWebsiteItem(item.ItemId);
        if(cosmetic!=null)return cosmetic;
        try { return CharacterCosmetics.FindWebsiteAsset(JsonUtility.FromJson<WebsiteCatalogData>(item.CustomData)?.assetKey); }
        catch { return null; }
    }
    private static CCoinCosmetic LiveWebsiteCosmetic(CatalogItem product)
    {
        var mapped=FindWebsiteCosmetic(product);
        if(mapped==null)return null;
        int price=mapped.price;
        if(product.VirtualCurrencyPrices!=null && product.VirtualCurrencyPrices.TryGetValue(CCoinRules.Currency,out var currentPrice))
            price=(int)Math.Min(int.MaxValue,(long)currentPrice);
        // These bundled appearance choices are always free, even if a legacy
        // PlayFab catalog entry still has its previous price.
        if(Array.IndexOf(CharacterCosmetics.FreeAppearanceIds(),mapped.id)>=0)price=0;
        string rarity=mapped.rarity;
        try
        {
            var data=JsonUtility.FromJson<WebsiteCatalogData>(product.CustomData);
            if(!string.IsNullOrWhiteSpace(data?.rarity))rarity=data.rarity;
        }
        catch { }
        return new CCoinCosmetic { id=mapped.id,kind=mapped.kind,name=string.IsNullOrWhiteSpace(product.DisplayName)?mapped.name:product.DisplayName,
            description=string.IsNullOrWhiteSpace(product.Description)?mapped.description:product.Description,price=price,
            websiteItemId=product.ItemId,websiteAssetKey=mapped.websiteAssetKey,rarity=rarity };
    }

    public void DevAddCoins(int amount)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if(amount<=0)return;
        if(!DevWalletActive)
        {
            session++;requestGeneration++;Busy=Verified=false;realState=state;
            state=new CCoinLocalState { wallet=new CCoinWallet { accountId=owner,currency="CC",cosmetics=CharacterCosmetics.Items,owned=CharacterCosmetics.FreeAppearanceIds() } };
        }
        state.wallet.balance=(int)Math.Min(int.MaxValue,(long)state.wallet.balance+amount);
        Publish("TEST WALLET · "+state.wallet.balance+" C-Coins · session only, never synced or saved.");
#endif
    }
    public void DevEndWallet()
    {
        if(!DevWalletActive)return;
        session++;requestGeneration++;state=realState;realState=null;Busy=Verified=false;nextRefresh=Time.unscaledTime;
        ApplySavedAppearance();
        Publish("Real wallet restored. Test coins and test purchases discarded.");
    }

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
        AccountAppearanceData.Changed += OnAppearanceChanged;
        BindAccount(UnityEngine.PlayerPrefs.GetString("PlayFabId","guest"));
    }
    private void OnDestroy()
    {
        session++; requestGeneration++; Busy = Verified = false;
        AccountAppearanceData.Changed -= OnAppearanceChanged;
        if (Instance == this) Instance = null;
    }
    private static string CacheKey(string account) => "SaveSystem.CCoins.v1." + account;
    public void BindAccount(string account)
    {
        realState=null; // Never carry developer funds/equipment across accounts.
        session++; requestGeneration++; Busy = Verified = false;
        owner = CCoinRules.ValidId(account) ? account : "guest";
        try { state = JsonUtility.FromJson<CCoinLocalState>(UnityEngine.PlayerPrefs.GetString(CacheKey(owner),"")) ?? new CCoinLocalState(); }
        catch { state = new CCoinLocalState(); }
        if (!CCoinRules.ValidWallet(state.wallet,owner)) state.wallet = null;
        if(state.wallet==null)state.wallet=new CCoinWallet { accountId=owner,currency=CCoinRules.Currency,cosmetics=CharacterCosmetics.Items,owned=CharacterCosmetics.FreeAppearanceIds() };
        else
        {
            var owned=new HashSet<string>(state.wallet.owned,StringComparer.Ordinal);
            foreach(var id in CharacterCosmetics.FreeAppearanceIds())owned.Add(id);
            state.wallet.owned=new List<string>(owned).ToArray();
        }
        if (state.pendingRewards == null) state.pendingRewards = new List<CCoinReward>();
        state.pendingRewards.RemoveAll(x => x == null || CCoinRules.Reward(x.careerId,x.contractLevel)?.operationId != x.operationId
            || CCoinRules.Reward(x.careerId,x.contractLevel)?.completionId != x.completionId);
        if (state.pendingPurchase != null && (!CCoinRules.ValidId(state.pendingPurchase.itemId) || !CCoinRules.ValidId(state.pendingPurchase.operationId))) state.pendingPurchase = null;
        // A saved wallet must not freeze names/artwork from an older model mapping.
        // Live account prices/catalog will be fetched again after this local refresh.
        var bundled = new List<CCoinCosmetic>(CharacterCosmetics.Items);
        foreach (var item in state.wallet.cosmetics)
            if (item.kind == "profile_frame") bundled.Add(item);
        state.wallet.cosmetics = bundled.ToArray();
        if (owner == "guest")
        {
            state.wallet.balance = 0; // Guest rewards are claims, not spendable currency.
            state.wallet.owned = CharacterCosmetics.FreeAppearanceIds();
            state.pendingPurchase = null;
        }
        if (!CCoinRules.Owns(state.wallet,state.selectedCosmetic)) state.selectedCosmetic = null;
        ValidateEquipment();
        AccountAppearanceData.Bind(owner,state.selectedCosmetic,state.equippedParts.ToArray());
        Save();
        nextRefresh = Time.unscaledTime;
        Publish(owner == "guest" ? GuestShopStatus : "Cached wallet · waiting for account sync.");
    }
    private void Update()
    {
        AccountProfileData.Tick();
        if (!DevWalletActive) AccountAppearanceData.Tick();
        if (Busy && Time.unscaledTime > deadline)
        { requestGeneration++; Finish(false,"Wallet request timed out. Pending transactions will retry safely."); }
        if (!Busy && Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + 60; Refresh(); }
    }
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) return;
        nextRefresh = Time.unscaledTime;
        if (!DevWalletActive) AccountAppearanceData.Refresh();
    }
    private void Save() { if(DevWalletActive)return; UnityEngine.PlayerPrefs.SetString(CacheKey(owner),JsonUtility.ToJson(state)); UnityEngine.PlayerPrefs.Save(); }
    private void Publish(string message) { Status = message; Changed?.Invoke(); }

    public bool QueueContractReward(int level,string rank)
    {
        if(DevWalletActive)return false;
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
        Publish(IsGuest ? GuestShopStatus : "+"+CCoinRules.ContractReward+" C-Coins reward pending server confirmation."); return true;
    }
    // Invoke only after the existing save system has linked unclaimed guest careers.
    public void LinkGuestRewards()
    {
        if(DevWalletActive)return;
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
        if(DevWalletActive){Publish("TEST WALLET · "+Balance+" C-Coins · local testing, purchases are temporary. Use F12 to restore real wallet.");return;}
        if (Busy) return;
        if (!Authenticated) { Verified = false; Publish(IsGuest ? GuestShopStatus : "Offline wallet · sign in to sync rewards and buy cosmetics."); return; }
        Busy = true; Publish("Syncing account C-Coins…");
        ReadPlayFabWallet(loaded =>
        {
            if (!loaded) { Finish(false,"PlayFab wallet is unavailable. Rewards stay pending."); return; }
            SyncPurchase();
        });
    }
    private void SyncPurchase()
    {
        if (state.pendingPurchase == null) { SyncRewardBatch(state.pendingRewards.ToArray(),0); return; }
        var purchase = state.pendingPurchase;
        var item=CCoinRules.Find(state.wallet,purchase.itemId);
        if(item?.kind=="profile_frame")
        {
            Request("purchase",purchase,result =>
            {
                bool complete=CCoinRules.Completed(result,purchase.operationId);
                bool rejected=result?.operationId==purchase.operationId && result.status=="rejected";
                if((!complete && !rejected) || !AcceptWallet(result.wallet))
                { Finish(false,"Purchase confirmation pending. Reconnect to confirm it.");return; }
                state.pendingPurchase=null;Save();SyncRewardBatch(state.pendingRewards.ToArray(),0);
            });
            return;
        }
        if(item==null || string.IsNullOrWhiteSpace(item.websiteItemId))
        {
            Finish(false,"This cosmetic is not in the connected website catalog.");return;
        }
        if(CCoinRules.Owns(state.wallet,item.id))
        { state.pendingPurchase=null;Save();SyncRewardBatch(state.pendingRewards.ToArray(),0);return; }
        int version=session, generation=++requestGeneration;deadline=Time.unscaledTime+45;
        PlayFabClientAPI.GetCatalogItems(new GetCatalogItemsRequest(), catalog =>
        {
            if(session!=version || requestGeneration!=generation)return;
            var product=catalog.Catalog?.Find(x=>FindWebsiteCosmetic(x)?.id==item.id);
            if(product==null || product.VirtualCurrencyPrices==null || !product.VirtualCurrencyPrices.TryGetValue(CCoinRules.Currency,out var currentPrice))
            { state.pendingPurchase=null;Save();Finish(false,"Website catalog item or C-Coin price is unavailable.");return; }
            PlayFabClientAPI.PurchaseItem(new PurchaseItemRequest { ItemId=product.ItemId,Price=(int)Math.Min(int.MaxValue,(long)currentPrice),VirtualCurrency=CCoinRules.Currency }, result =>
            {
                if(session!=version || requestGeneration!=generation)return;
                state.pendingPurchase=null;Save();
                ReadPlayFabWallet(loaded => { if(loaded)SyncRewardBatch(state.pendingRewards.ToArray(),0);else Finish(false,"Purchase completed; reconnect to refresh your shared wallet."); });
            }, error =>
            {
                if(session!=version || requestGeneration!=generation)return;
                ReadPlayFabWallet(loaded =>
                {
                    if(loaded && CCoinRules.Owns(state.wallet,item.id)){state.pendingPurchase=null;Save();SyncRewardBatch(state.pendingRewards.ToArray(),0);}
                    else Finish(false,"Purchase was not confirmed. Reconnect to safely retry it.");
                });
            });
        }, error => { if(session==version && requestGeneration==generation)Finish(false,"Website catalog is unavailable. Purchase remains pending."); });
    }
    private void SyncRewardBatch(CCoinReward[] batch,int index)
    {
        if (index >= batch.Length || index >= 20)
        { Finish(true,state.pendingRewards.Count == 0 ? "Account wallet synced." : "Account wallet synced · " + state.pendingRewards.Count + " reward(s) awaiting verification."); return; }
        var reward = batch[index];
        Request("claimContract",reward,result =>
        {
            if (result == null) { Finish(true,"Wallet synced · C-Coin reward is pending server confirmation."); return; }
            if (CCoinRules.Completed(result,reward.operationId))
            {
                state.pendingRewards.RemoveAll(x => x.operationId == reward.operationId); Save();
                ReadPlayFabWallet(loaded =>
                {
                    if(!loaded){Finish(false,"Reward confirmed · reconnect to refresh the shared C-Coin balance.");return;}
                    SyncRewardBatch(batch,index+1);
                });
                return;
            }
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
        ValidateEquipment();
        ApplySavedAppearance();
        Save(); return true;
    }
    private void OnAppearanceChanged()
    {
        if (DevWalletActive || AccountAppearanceData.Owner != owner) return;
        ApplySavedAppearance(); Save(); Changed?.Invoke();
    }
    private void ApplySavedAppearance()
    {
        if (AccountAppearanceData.Owner != owner) return;
        string frame = AccountAppearanceData.SelectedFrame;
        state.selectedCosmetic = CCoinRules.Owns(state.wallet,frame) && CCoinRules.Find(state.wallet,frame)?.kind == "profile_frame" ? frame : null;
        state.equippedParts = new List<string>(AccountAppearanceData.EquippedParts);
        ValidateEquipment(); // Cloud appearance preferences never grant item ownership.
    }
    private void ValidateEquipment()
    {
        if(state.equippedParts==null)state.equippedParts=new List<string>();
        var slots=new HashSet<string>();
        state.equippedParts.RemoveAll(id=> {
            var item=CharacterCosmetics.Find(id);
            return item==null || !CCoinRules.Owns(state.wallet,id) || !slots.Add(item.kind);
        });
    }
    private void ReadPlayFabWallet(Action<bool> callback)
    {
        int version=session, generation=++requestGeneration; deadline=Time.unscaledTime+45;
        try
        {
            PlayFabClientAPI.GetCatalogItems(new GetCatalogItemsRequest(), catalog =>
            {
                if(session!=version || requestGeneration!=generation)return;
                if(!Authenticated){callback(false);return;}
                var catalogMappings=new Dictionary<string,CCoinCosmetic>(StringComparer.Ordinal);
                var liveCosmetics=new Dictionary<string,CCoinCosmetic>(StringComparer.Ordinal);
                foreach(var product in catalog.Catalog ?? new List<CatalogItem>())
                {
                    var mapped=LiveWebsiteCosmetic(product);
                    if(mapped!=null && !string.IsNullOrEmpty(product.ItemId))
                    {
                        catalogMappings[product.ItemId]=mapped;
                        liveCosmetics[mapped.id]=mapped;
                    }
                }
                PlayFabClientAPI.GetUserInventory(new GetUserInventoryRequest(), result =>
                {
                    if(session!=version || requestGeneration!=generation)return;
                    if(!Authenticated){callback(false);return;}
                var owned=new HashSet<string>(StringComparer.Ordinal);
                foreach(var instance in result.Inventory ?? new List<ItemInstance>())
                {
                    CCoinCosmetic cosmetic=null;
                    if(instance?.ItemId!=null)catalogMappings.TryGetValue(instance.ItemId,out cosmetic);
                    cosmetic=cosmetic ?? CharacterCosmetics.FindWebsiteItem(instance?.ItemId) ?? CharacterCosmetics.Find(instance?.ItemId);
                    if(cosmetic!=null)owned.Add(cosmetic.id);
                }
                foreach(var id in CharacterCosmetics.FreeAppearanceIds())owned.Add(id);
                var cosmetics=new List<CCoinCosmetic>();
                foreach(var fallback in CharacterCosmetics.Items)
                    cosmetics.Add(liveCosmetics.TryGetValue(fallback.id,out var live)?live:fallback);
                if(state.wallet?.cosmetics!=null)
                    foreach(var frame in state.wallet.cosmetics)if(frame?.kind=="profile_frame")cosmetics.Add(frame);
                if(state.wallet?.owned!=null)
                    foreach(var id in state.wallet.owned)if(CCoinRules.Find(state.wallet,id)?.kind=="profile_frame")owned.Add(id);
                int balance=result.VirtualCurrency!=null && result.VirtualCurrency.TryGetValue(CCoinRules.Currency,out var value) ? Math.Max(0,value) : 0;
                var wallet=new CCoinWallet { accountId=owner,currency=CCoinRules.Currency,balance=balance,
                    revision=(state.wallet?.revision ?? 0)+1,owned=new List<string>(owned).ToArray(),cosmetics=cosmetics.ToArray() };
                callback(AcceptWallet(wallet));
                }, error => { if(session==version && requestGeneration==generation)callback(false); });
            }, error => { if(session==version && requestGeneration==generation)callback(false); });
        }
        catch { if(session==version && requestGeneration==generation)callback(false); }
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
        if (Balance < item.price) { Publish("Insufficient C-Coins balance."); return; }
        if(DevWalletActive)
        {
            state.wallet.balance-=item.price;
            var owned=new List<string>(state.wallet.owned);owned.Add(id);state.wallet.owned=owned.ToArray();
            Publish("TEST PURCHASE · "+item.name+" · no real coins charged; equip to test the look.");return;
        }
        state.pendingPurchase = new CCoinPurchase { itemId = id, operationId = "buy:" + Guid.NewGuid().ToString("N") };
        Save(); Refresh(); // Backend determines price, settles atomically and returns ownership.
    }
    public void Equip(string id)
    {
        if (!string.IsNullOrEmpty(id) && (!CCoinRules.Owns(state.wallet,id) || CCoinRules.Find(state.wallet,id) == null)) return;
        var part=CharacterCosmetics.Find(id);
        if(part!=null)
        {
            state.equippedParts.RemoveAll(x=>CharacterCosmetics.Find(x)?.kind==part.kind);
            state.equippedParts.Add(id);
        }
        else { state.selectedCosmetic=id; if(string.IsNullOrEmpty(id))state.equippedParts.Clear(); }
        if (!DevWalletActive) AccountAppearanceData.SetSelection(state.selectedCosmetic,state.equippedParts.ToArray());
        Save(); Changed?.Invoke();
    }
    public void OpenTopUpWebsite()
    {
        if (IsGuest) { GameSaveManager.Ensure().SignInToSync(); return; }
        Publish("Website shop opened. Use the same account; your wallet refreshes automatically when you return.");
        Application.OpenURL(CCoinSettings.TopUpUrl);
    }
    public void RequestCoinPurchase()
    {
        if(DevWalletActive){Publish("Restore your real wallet in F12 before opening paid checkout.");return;}
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
        if(DevWalletActive){Publish("Store validation is disabled in the test wallet.");return;}
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
