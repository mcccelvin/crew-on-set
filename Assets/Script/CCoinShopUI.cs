using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Opens inside the existing profile modal, so movement/cursor ownership remains
// with AlmanacManager. No changes to the equipment shop or production budget.
public sealed class CCoinShopUI : MonoBehaviour
{
    private CCoinService service;
    private RectTransform cards;
    private TMP_Text balance, status, pageLabel;
    private Button previous, next;
    private int page;
    public static CCoinShopUI Show(Transform profile)
    {
        var existing = profile.Find("C-Coins cosmetic shop");
        var view = existing != null ? existing.GetComponent<CCoinShopUI>() : Rect(profile,"C-Coins cosmetic shop",Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<CCoinShopUI>();
        if (view.service == null) view.Build();
        view.gameObject.SetActive(true); view.transform.SetAsLastSibling(); view.Refresh(); view.service.Refresh();
        return view;
    }
    private void Build()
    {
        service = CCoinService.Ensure();
        var backdrop = gameObject.AddComponent<Image>(); backdrop.color = new Color(0,0,0,.86f);
        var stretch = (RectTransform)transform; stretch.anchorMin = Vector2.zero; stretch.anchorMax = Vector2.one;
        stretch.offsetMin = stretch.offsetMax = Vector2.zero;
        var design = Rect(transform,"Shop design frame",Vector2.zero,new Vector2(1920,1080));
        var paper = Rect(design,"Cosmetic shop paper",Vector2.zero,new Vector2(1740,980)).gameObject.AddComponent<Image>();
        paper.color = new Color32(248,239,215,255);
        Text(design,"Shop title","COSMETIC SHOP",new Vector2(0,424),new Vector2(1000,70),52);
        Text(design,"Shop description","C-Coins buy appearance only. Your B-Coins, equipment and grades are unchanged.",new Vector2(0,352),new Vector2(1600,60),27);
        balance = Text(design,"C-Coins balance","",new Vector2(-485,274),new Vector2(650,75),36);
        Button(design,"Sync wallet","SYNC",new Vector2(480,274),new Vector2(170,62),() => service.Refresh());
        Button(design,"Buy C-Coins","BUY C-COINS",new Vector2(697,274),new Vector2(230,62),() => CCoinPackShopUI.Show(transform.parent));
        cards = Rect(design,"Cosmetic cards",new Vector2(0,-10),new Vector2(1600,440));
        status = Text(design,"Wallet status","",new Vector2(0,-300),new Vector2(1550,130),27);
        status.richText = false;
        previous = Button(design,"Previous cosmetics","PREVIOUS",new Vector2(-400,-413),new Vector2(240,64),() => { page--; Refresh(); });
        next = Button(design,"Next cosmetics","NEXT",new Vector2(400,-413),new Vector2(240,64),() => { page++; Refresh(); });
        pageLabel = Text(design,"Catalog page","",new Vector2(0,-413),new Vector2(350,55),26);
        Button(design,"Close cosmetic shop","BACK TO PROFILE",new Vector2(659,-413),new Vector2(265,64),() => gameObject.SetActive(false));
        Button(design,"Default appearance","DEFAULT LOOK",new Vector2(-680,-413),new Vector2(250,64),() => service.Equip(null));
        service.Changed += Refresh;
    }
    private void OnEnable() { if (service != null) Refresh(); }
    private void OnDestroy() { if (service != null) service.Changed -= Refresh; }
    private void Refresh()
    {
        if (service == null || !gameObject.activeInHierarchy) return;
        var wallet = service.Wallet;
        balance.text = service.Balance.ToString("N0") + " C-COINS" + (service.Verified ? "" : "  (CACHED)");
        status.text = service.Status + "\n100 C-Coins per first successful contract completion. Pending rewards: " + service.PendingRewards;
        foreach (Transform child in cards) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        // Legacy frame-only modal; character items live in the shared profile shop.
        var items = System.Array.FindAll(wallet?.cosmetics ?? new CCoinCosmetic[0],x=>x!=null && x.kind=="profile_frame");
        int pages = Mathf.Max(1,Mathf.CeilToInt(items.Length / 4f)); page = Mathf.Clamp(page,0,pages-1);
        previous.interactable = page > 0; next.interactable = page + 1 < pages;
        pageLabel.text = (page+1) + " / " + pages;
        if (items.Length == 0)
        {
            Text(cards,"Catalog not connected","Your cosmetic catalog will appear here when the account service is connected.\nNo money or C-Coins will be charged while the shop is unavailable.",Vector2.zero,new Vector2(1300,240),32);
            return;
        }
        for (int i = page*4; i < Mathf.Min(items.Length,(page+1)*4); i++)
        {
            var item = items[i]; int slot = i-page*4;
            var card = Rect(cards,"Cosmetic " + item.id,new Vector2(-600+slot*400,0),new Vector2(370,440));
            var face = card.gameObject.AddComponent<Image>(); face.color = new Color32(234,222,194,255);
            ColorUtility.TryParseHtmlString(item.color,out var color);
            var sample = Rect(card,"Frame sample",new Vector2(0,115),new Vector2(145,145));
            var outer = sample.gameObject.AddComponent<Image>(); outer.color = color;
            var inner = Rect(sample,"Portrait",Vector2.zero,new Vector2(127,127)).gameObject.AddComponent<Image>();
            ExportUIArt.Apply(inner,"profileIcon"); inner.preserveAspect = true; inner.color = Color.white;
            var title = Text(card,"Name",item.name,new Vector2(0,11),new Vector2(338,67),30); title.richText = false;
            var detail = Text(card,"Description",item.description ?? "Profile portrait frame",new Vector2(0,-59),new Vector2(332,85),23); detail.richText = false;
            bool owned = CCoinRules.Owns(wallet,item.id), equipped = service.SelectedCosmetic == item.id;
            Text(card,"Price",owned ? "OWNED" : item.price + " C-COINS",new Vector2(0,-129),new Vector2(332,44),28);
            var button = Button(card,"Buy or equip",equipped ? "EQUIPPED" : owned ? "EQUIP" : "BUY",new Vector2(0,-184),new Vector2(300,60),() => { if (owned) service.Equip(item.id); else service.BuyCosmetic(item.id); });
            button.interactable = !equipped && (owned || service.CanBuy && service.Balance >= item.price);
        }
    }
    public static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f); rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }
    public static TMP_Text Text(Transform parent,string name,string value,Vector2 position,Vector2 size,int fontSize)
    {
        var text = Rect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        FeedbackTypography.Apply(text); text.text = value; text.fontSize = fontSize; text.color = new Color32(35,57,77,255);
        text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.enableAutoSizing = true;
        text.fontSizeMin = 20; text.fontSizeMax = fontSize; text.overflowMode = TextOverflowModes.Ellipsis; return text;
    }
    public static Button Button(Transform parent,string name,string label,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var image = Rect(parent,name,position,size).gameObject.AddComponent<Image>(); image.color = new Color32(37,68,106,255);
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        var text = Text(image.transform,"Label",label,Vector2.zero,size-new Vector2(16,8),28); text.color = Color.white;
        var colors = button.colors; colors.highlightedColor = new Color32(170,211,231,255); colors.disabledColor = new Color32(160,160,160,130); button.colors = colors;
        return button;
    }
}
