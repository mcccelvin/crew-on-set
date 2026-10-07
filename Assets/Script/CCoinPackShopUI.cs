using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A child of the existing profile modal: it never takes/releases gameplay input.
// Display configuration is not payment authority. The website owns checkout.
public sealed class CCoinPackShopUI : MonoBehaviour
{
    static readonly Color Ink = new Color32(36,27,26,255);
    static readonly Color Cream = new Color32(255,243,211,255);
    static readonly Color Gold = new Color32(245,188,63,255);
    CCoinService service;
    RectTransform design;
    TMP_Text balance, status;
    readonly Button[] buy = new Button[4];
    readonly TMP_Text[] availability = new TMP_Text[4];
    readonly CCoinPackOffer[] offers = new CCoinPackOffer[4];

    public static CCoinPackShopUI Show(Transform profile)
    {
        var old = profile.Find("C-Coins pack shop");
        var view = old != null ? old.GetComponent<CCoinPackShopUI>()
            : CCoinShopUI.Rect(profile,"C-Coins pack shop",Vector2.zero,Vector2.zero).gameObject.AddComponent<CCoinPackShopUI>();
        if (view.service == null) view.Build();
        view.gameObject.SetActive(true); view.transform.SetAsLastSibling();
        view.Fit(); view.Refresh(); view.service.Refresh();
        return view;
    }
    void Build()
    {
        service = CCoinService.Ensure();
        var root = (RectTransform)transform;
        root.anchorMin=Vector2.zero; root.anchorMax=Vector2.one; root.offsetMin=root.offsetMax=Vector2.zero;
        var backdrop=gameObject.AddComponent<Image>(); backdrop.color=new Color32(30,18,19,250);
        design=CCoinShopUI.Rect(transform,"Pack shop design",Vector2.zero,new Vector2(1920,1080));
        var stage=Panel(design,"Stage backdrop",Vector2.zero,new Vector2(1920,1080),Color.white);
        ExportUIArt.Apply(stage,"profileBackground"); stage.color=new Color(1,1,1,.42f);
        Panel(design,"Shop ink outline",new Vector2(0,-5),new Vector2(1652,992),Ink);
        Panel(design,"Shop gold edge",Vector2.zero,new Vector2(1640,980),Gold);
        Panel(design,"Shop interior",Vector2.zero,new Vector2(1628,968),new Color32(50,34,31,255));
        Panel(design,"Cream header",new Vector2(0,386),new Vector2(1628,196),Cream);
        Label(design,"Shop heading","C-COIN SHOP",new Vector2(-435,417),new Vector2(650,70),54,Ink,TextAlignmentOptions.Left);
        Label(design,"Shop subtitle","A LITTLE GOLD. A LOT OF PERSONALITY.",new Vector2(-425,351),new Vector2(670,40),22,Ink,TextAlignmentOptions.Left);
        Panel(design,"Wallet capsule",new Vector2(408,402),new Vector2(370,86),Ink);
        balance=Label(design,"Account balance","",new Vector2(408,412),new Vector2(346,40),29,Gold);
        Label(design,"Wallet caption","YOUR ACCOUNT WALLET",new Vector2(408,380),new Vector2(340,25),16,Cream);
        Action(design,"Close packs","X",new Vector2(726,421),new Vector2(75,65),"redButton",()=>gameObject.SetActive(false));
        Action(design,"Top up C-Coins","TOP UP",new Vector2(685,345),new Vector2(150,53),"blueButton",()=>service.OpenTopUpWebsite());
        var configured=CCoinSettings.Packs;
        var defaults=CCoinPackOffer.Defaults();
        for(int i=0;i<4;i++)
        {
            offers[i]=configured!=null && i<configured.Length && configured[i]!=null ? configured[i] : defaults[i];
            BuildCard(i);
        }
        Label(design,"Earn coins note","EARN AS YOU CREATE  /  5 C-Coins per first successful contract completion",new Vector2(0,-370),new Vector2(1510,40),24,Cream);
        status=Label(design,"Payment availability","",new Vector2(0,-416),new Vector2(1480,50),20,Gold);
        Label(design,"Payment safety","Cosmetics only. B-Coins are separate. Payment provider: PayMongo.",new Vector2(0,-460),new Vector2(1490,30),18,new Color32(220,198,173,255));
        service.Changed+=Refresh;
    }
    void BuildCard(int index)
    {
        var offer=offers[index];
        var card=CCoinShopUI.Rect(design,"Coin pack "+index,new Vector2(index%2==0?-393:393,index<2?124:-183),new Vector2(750,282));
        Panel(card,"Card shadow",new Vector2(5,-7),new Vector2(756,288),Ink);
        Panel(card,"Card border",Vector2.zero,new Vector2(750,282),Gold);
        Panel(card,"Card paper",Vector2.zero,new Vector2(738,270),Cream);
        var accent=index==0?new Color32(34,91,137,255):index==1?new Color32(157,53,42,255):index==2?new Color32(122,78,45,255):new Color32(72,59,105,255);
        Panel(card,"Pack name ribbon",new Vector2(0,102),new Vector2(738,66),accent);
        Label(card,"Pack name",offer.title??"COIN PACK",new Vector2(-120,102),new Vector2(445,46),25,Cream,TextAlignmentOptions.Left);
        Label(card,"Pack tag",offer.Configured?"COSMETIC CURRENCY":"COMING SOON",new Vector2(231,102),new Vector2(242,40),16,Cream);
        var art=CCoinShopUI.Rect(card,"Coin illustration",new Vector2(198,-12),new Vector2(270,185)).gameObject.AddComponent<CCoinPackArt>();
        art.tier=index; art.color=accent; art.raycastTarget=false;
        Label(card,"Coin quantity",offer.Configured?offer.coins.ToString("N0"):"COMING SOON",new Vector2(-163,24),new Vector2(338,65),offer.Configured?56:28,Ink,TextAlignmentOptions.Left);
        Label(card,"Coin unit",offer.Configured?"C-COINS":"More ways to customize",new Vector2(-163,-26),new Vector2(338,35),22,Ink,TextAlignmentOptions.Left);
        buy[index]=Action(card,"Open pack website",offer.Configured?offer.PriceLabel:"NOT AVAILABLE",new Vector2(-164,-82),new Vector2(338,58),"blueButton",()=>OpenWebsite(index));
        availability[index]=Label(card,"Checkout state","",new Vector2(202,-118),new Vector2(280,24),14,Ink);
    }
    void OpenWebsite(int index)
    {
        // Recheck at click time, including after account changes. No pack price,
        // quantity, payment status or authentication token is sent in the URL.
        if (!CanOpen(index)) { Refresh(); return; }
        service.RequestCoinPurchase();
    }
    bool CanOpen(int i) => offers[i].Configured && CCoinSettings.WebsitePurchasesEnabled
        && !string.IsNullOrEmpty(CCoinSettings.WebsiteUrl) && service.Authenticated && !service.Busy;
    void Refresh()
    {
        if(service==null || !gameObject.activeInHierarchy)return;
        balance.text=service.Balance.ToString("N0")+" C-COINS";
        for(int i=0;i<4;i++)
        {
            buy[i].interactable=CanOpen(i);
            availability[i].text=!offers[i].Configured?"PACK DETAILS TO BE ANNOUNCED":CanOpen(i)?"CHOOSE PACK ON WEBSITE":"CHECKOUT NOT AVAILABLE";
        }
        status.text=!CCoinSettings.WebsitePurchasesEnabled || string.IsNullOrEmpty(CCoinSettings.WebsiteUrl)
            ? "SHOP PREVIEW  /  PayMongo checkout is not connected yet. No payments can be taken."
            : !service.Authenticated?"Sign in to your PlayFab account to continue on the website."
            : "Use the same account on the website. Your wallet refreshes automatically when you return.";
    }
    void OnEnable(){if(service!=null){Fit();Refresh();}}
    void OnRectTransformDimensionsChange(){Fit();}
    void LateUpdate(){Fit();}
    void Fit()
    {
        if(design==null)return;
        var size=((RectTransform)transform).rect.size;
        var scale=Vector3.one*Mathf.Min(size.x/1920f,size.y/1080f);
        if(design.localScale!=scale)design.localScale=scale;
    }
    void OnDestroy(){if(service!=null)service.Changed-=Refresh;}
    static Image Panel(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
    {
        var image=CCoinShopUI.Rect(parent,name,pos,size).gameObject.AddComponent<Image>();
        image.color=color; image.raycastTarget=false; return image;
    }
    static TMP_Text Label(Transform parent,string name,string text,Vector2 pos,Vector2 size,int font,Color color,TextAlignmentOptions align=TextAlignmentOptions.Center)
    {
        var label=CCoinShopUI.Rect(parent,name,pos,size).gameObject.AddComponent<TextMeshProUGUI>();
        label.font=TMP_Settings.defaultFontAsset; label.text=text; label.richText=false; label.fontSize=font;
        label.fontStyle=FontStyles.Bold; label.color=color; label.alignment=align;
        label.enableAutoSizing=true; label.fontSizeMin=font-2; label.fontSizeMax=font;
        label.enableWordWrapping=false; label.raycastTarget=false; return label;
    }
    static Button Action(Transform parent,string name,string text,Vector2 pos,Vector2 size,string art,UnityEngine.Events.UnityAction action)
    {
        var image=Panel(parent,name,pos,size,Color.white); ExportUIArt.Apply(image,art); image.raycastTarget=true;
        var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image; button.onClick.AddListener(action);
        var colors=button.colors; colors.disabledColor=new Color(.7f,.7f,.7f,1); colors.highlightedColor=new Color(1,.92f,.7f,1); button.colors=colors;
        Label(image.transform,"Button label",text,Vector2.zero,size-new Vector2(24,12),25,Color.white);
        return button;
    }
}
