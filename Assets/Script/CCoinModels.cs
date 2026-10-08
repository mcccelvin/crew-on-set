using System;
using System.Collections.Generic;

// Website/game wallet protocol. B-Coins and career checkpoints are separate.
[Serializable] public sealed class CCoinCosmetic
{
    public string id, name, description, kind, color, websiteItemId, websiteAssetKey, rarity;
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
    public List<string> equippedParts = new List<string>();
}
public static class CCoinRules
{
    public const int ContractReward = 100;
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
            if (item == null || !ValidId(item.id) || !ids.Add(item.id) || !Supported(item) || item.price < 0
                || string.IsNullOrWhiteSpace(item.name) || item.name.Length > 80 || (item.description != null && item.description.Length > 300)
                || (item.kind == "profile_frame" && !ValidColor(item.color))) return false;
        return true;
    }
    public static bool Supported(CCoinCosmetic item) => item != null && (item.kind == "profile_frame" ||
        (CharacterCosmetics.Find(item.id) != null && CharacterCosmetics.Find(item.id).kind == item.kind));
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

// Stable server item IDs map only to bundled models, never a URL or client path.
public static class CharacterCosmetics
{
    public static readonly string[] Slots = { "accessory", "hair", "face", "body", "shirt", "pants", "shoe" };
    public static readonly CCoinCosmetic[] Items = Create();
    public static CCoinCosmetic Find(string id) => Array.Find(Items,x=>x.id==id);
    public static CCoinCosmetic FindWebsiteItem(string websiteItemId) => Array.Find(Items,x=>x.websiteItemId==websiteItemId);
    public static CCoinCosmetic FindWebsiteAsset(string websiteAssetKey) => Array.Find(Items,x=>x.websiteAssetKey==websiteAssetKey);
    public static string WebsiteImageResource(CCoinCosmetic item)
    {
        if (item == null || string.IsNullOrEmpty(item.websiteItemId)) return null;
        string folder, file;
        switch (item.kind)
        {
            case "accessory": folder = "accessories"; file = "accessory"; break;
            case "hair": folder = "hair"; file = "hair"; break;
            case "face":
                folder = "faces"; file = "face";
                int faceIndex = 0;
                foreach (var face in Items)
                {
                    if (face.kind != "face" || string.IsNullOrEmpty(face.websiteItemId)) continue;
                    faceIndex++;
                    if (face.id == item.id) return "ShopCosmetics/shop/faces/face" + faceIndex;
                }
                return null;
            case "shirt": folder = "tops"; file = "shirt"; break;
            case "pants": folder = "bottoms"; file = "pants"; break;
            case "shoe": folder = "shoe-wear"; file = "shoes"; break;
            default: return null;
        }
        string asset = item.websiteAssetKey ?? "";
        int separator = asset.LastIndexOf('-');
        string index = separator >= 0 ? asset.Substring(separator + 1) : "";
        return int.TryParse(index, out _) ? "ShopCosmetics/shop/" + folder + "/" + file + index : null;
    }
    public static string[] FreeFaceIds()
    {
        var ids=new List<string>();foreach(var item in Items)if(item.kind=="face" && item.price==0)ids.Add(item.id);return ids.ToArray();
    }
    public static string[] FreeAppearanceIds()
    {
        var ids=new List<string>();foreach(var item in Items)if(item.price==0 && (item.kind=="face" || item.kind=="body" || item.kind=="hair" || item.kind=="shirt" || item.kind=="pants" || item.kind=="shoe"))ids.Add(item.id);return ids.ToArray();
    }
    public static string[] GuestOutfit(string[] saved)
    {
        var starter = new[] { "character_body_girl", "character_face1", "character_hair1", "character_shirt5", "character_pants4", "character_shoe1" };
        var free = new HashSet<string>(FreeAppearanceIds());
        for (int i = 0; i < starter.Length; i++)
        {
            string kind = Find(starter[i]).kind;
            string selected = saved == null ? null : Array.Find(saved, id => free.Contains(id) && Find(id)?.kind == kind);
            if (selected != null) starter[i] = selected;
        }
        return starter;
    }
    public static string ModelKey(string id) => Find(id)==null ? null : id.Substring("character_".Length);
    private static CCoinCosmetic[] Create()
    {
        var items = new List<CCoinCosmetic>();
        int[] counts = {6,6,5,2,5,6,6};
        string[][] names = {
            new[] { "Crew Gear Backpack", "Rectangular Studio Frames", "Utility Belt", "Set Crew Gloves", "Call Sheet Pass", "On-Set Face Mask" },
            new[] { "Chestnut Studio Bun", "Golden Curtain Cut", "Low-Tied Chestnut", "Center-Part Shag", "Swept Chestnut Fringe", "Tousled Chestnut Crop" },
            new[] { "Neutral Focus", "Set-Day Scowl", "Half-Lidded", "Side-Eye Smirk", "Big Surprise" },
            new[] { "Crew Member · Boy", "Crew Member · Girl" },
            new[] { "Charcoal V-Neck", "Brown Field Jacket", "Set Utility Vest", "Open-Collar Layer", "Fresh White Crew Tee" },
            new[] { "Slate Wide-Leg Trousers", "Coral Track Shorts", "Slate Cargo Trousers", "Teal Cuff Joggers", "Brown Tailored Trousers", "Tan Cargo Pants" },
            new[] { "Everyday Slip-Ons", "Lace-Up Platform Boots", "Red Lace-Up Sneakers", "Charcoal Studio Slides", "Green Buckle Sandals", "Buckle Strap Flats" }
        };
        string[][] descriptions = {
            new[] { "A compact charcoal backpack with reinforced pockets and straps.", "Bold rectangular frames with a simple, clean dark outline.", "A sturdy dark belt with segmented panels and a clean buckle.", "A pair of practical dark gloves for handling gear between takes.", "A crew ID badge on a lanyard, ready for your next call time.", "A dark protective face mask with comfortable ear loops." },
            new[] { "A neat high bun with loose face-framing strands, made for a long day on set.", "Long golden locks part into soft curtain bangs with a bold silhouette.", "A practical low ponytail with a tidy center part and loose side locks.", "A full, center-parted fringe with chunky layers for extra character.", "A layered short cut with sweeping bangs and a relaxed finish.", "A textured crop with sweeping, piecey bangs and a playful edge." },
            new[] { "A calm, neutral expression for keeping your focus on set.", "A fierce scowl for when the shoot is getting intense.", "A relaxed, half-lidded look for a low-key day on set.", "A knowing side-eye paired with a sly smile.", "A wide-eyed, open-mouthed look for a big reveal." },
            new string[2],
            new[] { "A simple charcoal V-neck tee, an easy staple between takes.", "A rugged brown jacket with generous pockets over a dark base layer.", "A dark utility shirt layered with a brown multi-pocket crew vest.", "A clean short-sleeve overshirt worn open over a bright crew tee.", "A crisp white crew-neck T-shirt with dark contrast sleeve bands." },
            new[] { "Relaxed slate trousers with a wide, flowing leg and clean waistband.", "Bright red-coral athletic shorts with crisp white side panels.", "Tapered dark cargos with bright utility pockets at each thigh.", "Easy teal joggers gathered at the ankle for comfortable studio days.", "Straight brown trousers with a belt and subtle pressed seams.", "Roomy tan cargos with oversized side pockets for a utility look." },
            new[] { "Lightweight charcoal slip-ons with a bright, flexible sole.", "Chunky ankle boots with contrast laces and a sturdy platform sole.", "Red sneakers with white laces and a cushioned studio-ready sole.", "Simple charcoal slides for the quick break between setups.", "Comfortable green double-strap sandals with secure buckles.", "Rounded black flats with a polished buckle strap." }
        };
        int[][] prices = {
            new[] { 850, 650, 600, 450, 500, 450 },
            new[] { 0, 700, 550, 650, 0, 650 },
            new[] { 0, 0, 0, 0, 0 },
            new[] { 0, 0 },
            new[] { 400, 900, 850, 650, 0 },
            new[] { 700, 500, 900, 0, 650, 850 },
            new[] { 0, 950, 700, 400, 550, 600 }
        };
        string[][] websiteIds = {
            new[] { "accessory-crew-backpack", "accessory-rectangular-frames", "accessory-utility-belt", "accessory-set-gloves", "accessory-call-sheet-pass", "accessory-face-mask" },
            new[] { "hair-chestnut-bun", "hair-golden-curtains", "hair-low-tie", "hair-center-fringe", "hair-swept-fringe", "hair-tousled-fringe" },
            new[] { "face-neutral-focus", "face-set-day-scowl", "face-half-lidded", "face-side-eye-smirk", "face-big-surprise" },
            new string[2],
            new[] { "top-charcoal-vneck", "top-field-jacket", "top-utility-vest", "top-open-collar", "top-white-tee" },
            new[] { "bottom-wide-slate", "bottom-coral-shorts", "bottom-slate-cargo", "bottom-teal-joggers", "bottom-brown-tailored", "bottom-tan-cargo" },
            new[] { "shoe-slip-ons", "shoe-platform-boots", "shoe-red-sneakers", "shoe-charcoal-slides", "shoe-green-sandals", "shoe-buckle-flats" }
        };
        string[][] websiteAssets = {
            new[] { "accessory-3", "accessory-5", "accessory-1", "accessory-2", "accessory-4", "accessory-6" },
            new[] { "hair-1", "hair-2", "hair-3", "hair-5", "hair-4", "hair-6" },
            new[] { "face-neutral-focus", "face-set-day-scowl", "face-half-lidded", "face-side-eye-smirk", "face-big-surprise" },
            new string[2],
            new[] { "top-5", "top-4", "top-1", "top-2", "top-3" },
            new[] { "bottom-1", "bottom-4", "bottom-5", "bottom-3", "bottom-6", "bottom-2" },
            new[] { "shoe-3", "shoe-1", "shoe-4", "shoe-6", "shoe-5", "shoe-2" }
        };
        string[][] rarities = {
            new[] { "Epic", "Rare", "Rare", "Common", "Common", "Common" },
            new[] { "Common", "Rare", "Common", "Rare", "Common", "Rare" },
            new[] { "Common", "Common", "Common", "Common", "Common" },
            new string[2],
            new[] { "Common", "Epic", "Epic", "Rare", "Common" },
            new[] { "Rare", "Common", "Epic", "Rare", "Rare", "Epic" },
            new[] { "Common", "Epic", "Rare", "Common", "Common", "Rare" }
        };
        for(int slot=0;slot<Slots.Length;slot++) for(int n=1;n<=counts[slot];n++)
        {
            string key=Slots[slot]=="body" ? (n==1 ? "body_boy" : "body_girl") : Slots[slot]+n;
            string title=Slots[slot]=="body" ? (n==1 ? "Boy body" : "Girl body") : char.ToUpper(Slots[slot][0])+Slots[slot].Substring(1)+" "+n;
            int index=n-1;
            string websiteName=names[slot][index], description=descriptions[slot][index];
            items.Add(new CCoinCosmetic {id="character_"+key,kind=Slots[slot],name=websiteName??title,price=prices[slot][index],websiteItemId=websiteIds[slot][index],websiteAssetKey=websiteAssets[slot][index],rarity=rarities[slot][index],
                description=description??("Character customization · "+title+". Appearance only.")});
        }
        return items.ToArray();
    }
}
