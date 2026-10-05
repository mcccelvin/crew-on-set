using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CCoinProfileWidget : MonoBehaviour
{
    private CCoinService service;
    private TMP_Text balance;
    private TMP_Text walletStatus;
    private readonly Image[] border = new Image[4];
    public static void Attach(Transform stage,RawImage portrait,Transform profile)
    {
        var existing = stage.Find("Account cosmetic wallet");
        if (existing != null) return;
        var widget = CCoinShopUI.Rect(stage,"Account cosmetic wallet",Vector2.zero,new Vector2(1920,1080)).gameObject.AddComponent<CCoinProfileWidget>();
        widget.service = CCoinService.Ensure();
        var counter = CCoinShopUI.Rect(widget.transform,"C-Coin counter",new Vector2(-725,260),new Vector2(300,80));
        var bar = CCoinShopUI.Rect(counter,"Balance bar",new Vector2(25,0),new Vector2(250,66)).gameObject.AddComponent<Image>();
        bar.color = new Color32(180,180,180,180); bar.raycastTarget = false;
        var emblem = CCoinShopUI.Rect(counter,"Gold C coin",new Vector2(-112,0),new Vector2(80,84)).gameObject.AddComponent<CCoinPackArt>();
        emblem.singleCoin = true; emblem.raycastTarget = false;
        widget.balance = CCoinShopUI.Text(counter,"C-Coins", "",new Vector2(28,0),new Vector2(215,62),34);
        widget.balance.font = TMP_Settings.defaultFontAsset;
        widget.balance.fontStyle = FontStyles.Bold;
        widget.balance.color = Color.white;
        widget.balance.alignment = TextAlignmentOptions.Center;
        widget.balance.enableAutoSizing = true; widget.balance.fontSizeMin = 20; widget.balance.fontSizeMax = 34;
        widget.walletStatus = CCoinShopUI.Text(counter,"Wallet state","",new Vector2(0,-66),new Vector2(310,54),19);
        widget.walletStatus.font = TMP_Settings.defaultFontAsset;
        widget.walletStatus.color = Color.white;
        widget.walletStatus.alignment = TextAlignmentOptions.Center;
        widget.walletStatus.fontSizeMin = 14; widget.walletStatus.fontSizeMax = 19;
        CCoinShopUI.Button(widget.transform,"Open cosmetic shop","C-COINS SHOP",new Vector2(-455,-475),new Vector2(350,62),() => CCoinShopUI.Show(profile));
        Vector2[] positions = { new Vector2(0,353),new Vector2(0,-353),new Vector2(-283,0),new Vector2(283,0) };
        Vector2[] sizes = { new Vector2(574,6),new Vector2(574,6),new Vector2(6,712),new Vector2(6,712) };
        for (int i=0;i<4;i++)
        {
            widget.border[i] = CCoinShopUI.Rect(portrait.transform,"Cosmetic frame " + i,positions[i],sizes[i]).gameObject.AddComponent<Image>();
            widget.border[i].raycastTarget = false;
        }
        widget.service.Changed += widget.Refresh; widget.Refresh();
    }
    private void OnEnable() { if (service != null) { Refresh(); service.Refresh(); } }
    private void OnDestroy() { if (service != null) service.Changed -= Refresh; }
    private void Refresh()
    {
        if (balance == null) return;
        balance.text = service.Balance.ToString("N0");
        walletStatus.text = "C-COINS" + (service.Verified ? "" : " · cached")
            + (service.PendingRewards > 0 ? "\n+" + service.PendingRewards*CCoinRules.ContractReward + " pending" : "");
        var item = CCoinRules.Find(service.Wallet,service.SelectedCosmetic);
        bool equipped = item != null && CCoinRules.Owns(service.Wallet,item.id);
        Color color = Color.white; if (equipped) ColorUtility.TryParseHtmlString(item.color,out color);
        foreach (var edge in border) if (edge != null) { edge.gameObject.SetActive(equipped); edge.color = color; }
    }
}
