using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class AlmanacManager
{
    private static readonly Color ShopBackground = new Color32(252, 245, 220, 255);
    private static readonly Color ShopCard = new Color32(255, 250, 234, 255);
    private static readonly Color ShopAccent = new Color32(153, 99, 52, 255);
    private static readonly Color ShopMuted = ProfileMuted;
    private static Sprite shopRoundedSprite;

    private Transform CreateProfileShopGrid(Transform parent)
    {
        var panel = CreatePanel("Shared shop items", parent, ShopBackground);
        SetStretchRect(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(2, 10), new Vector2(-2, -216));
        var scroll = panel.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true; scroll.scrollSensitivity = 35;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = CreatePanel("Viewport", panel.transform, Color.clear);
        SetStretchRect(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-28, -6));
        viewport.AddComponent<RectMask2D>();
        var content = new GameObject("Content", typeof(RectTransform), typeof(ProfileShopGrid), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var rect = content.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
        rect.anchoredPosition = rect.sizeDelta = Vector2.zero;
        var grid = content.GetComponent<ProfileShopGrid>();
        grid.padding = new RectOffset(8, 14, 8, 14); grid.spacing = new Vector2(18, 20);
        grid.childAlignment = TextAnchor.UpperLeft;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = rect; scroll.viewport = viewport.GetComponent<RectTransform>();
        AddProfileScrollbar(content.transform);
        panel.transform.Find("Scroll rail").GetComponent<Image>().color = new Color32(220, 207, 181, 255);
        panel.transform.Find("Scroll rail/Scroll handle").GetComponent<Image>().color = new Color32(117, 92, 67, 255);
        return content.transform;
    }

    private static Sprite ShopRoundedSprite()
    {
        if (shopRoundedSprite != null) return shopRoundedSprite;
        const int size = 48; const float radius = 10;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Shop rounded boxes", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            var nearest = new Vector2(Mathf.Clamp(x + .5f, radius, size - radius), Mathf.Clamp(y + .5f, radius, size - radius));
            float alpha = Mathf.Clamp01(radius + .5f - Vector2.Distance(new Vector2(x + .5f, y + .5f), nearest));
            pixels[y * size + x] = new Color(1, 1, 1, alpha);
        }
        texture.SetPixels(pixels); texture.Apply(false, true);
        shopRoundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 12);
        return shopRoundedSprite;
    }

    private void StyleShopBox(Image image, Color color, bool border = false)
    {
        image.sprite = ShopRoundedSprite(); image.type = Image.Type.Sliced; image.color = color;
        if (!border) return;
        var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color32(160, 123, 79, 255); outline.effectDistance = new Vector2(1, -1);
    }

    private TMP_Text ShopText(Transform parent, string name, string value, float size, float top, float height, Color color)
    {
        var text = ProfileText(parent, name, value, size, 14, top, height);
        text.color = color; text.enableWordWrapping = true; text.raycastTarget = false;
        return text;
    }

    private void StyleProfileShopTypography(TMP_Text text)
    {
        text.font = profileBodyFont; text.fontSharedMaterial = profileBodyMaterial;
        text.fontStyle = text.name == "Name" || text.name == "Category" || text.name == "Price" || text.name == "Item state" ||
            text.name == "Shop balance" || text.GetComponentInParent<Button>(true) != null ? FontStyles.Bold : FontStyles.Normal;
        text.characterSpacing = 0; text.UpdateMeshPadding();
    }

    private void StyleShopButton(Button button, bool primary)
    {
        var label = button.GetComponentInChildren<TMP_Text>();
        bool enabledPrimary = primary && button.interactable;
        bool equipped = primary && label.text == "EQUIPPED";
        var image = button.GetComponent<Image>();
        StyleShopBox(image, Color.white, !enabledPrimary);
        if (enabledPrimary)
        {
            ExportUIArt.Apply(image, "blueButton");
            image.type = Image.Type.Simple;
        }
        var colors = button.colors;
        colors.normalColor = enabledPrimary ? Color.white : ShopCard;
        colors.highlightedColor = enabledPrimary ? new Color(1, .93f, .75f) : new Color32(237, 222, 191, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = enabledPrimary ? new Color(.75f, .86f, 1) : new Color32(224, 207, 179, 255);
        colors.disabledColor = equipped ? new Color32(224, 234, 214, 255) : new Color32(231, 218, 193, 255); button.colors = colors;
        label.color = enabledPrimary ? Color.white : equipped ? ProfileGreen : button.interactable ? ProfileInk : ShopMuted;
        label.fontSize = label.fontSizeMax = 20; label.fontSizeMin = 14;
        label.enableAutoSizing = true; label.enableWordWrapping = false;
        SetStretchRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 3), new Vector2(-6, -3));
    }

    private void CreateProfileShopCard(CCoinCosmetic item, CharacterCosmeticCatalog art, bool isPart, bool available, bool listed)
    {
        bool owned = CCoinRules.Owns(profileWallet.Wallet, item.id), equipped = owned && profileWallet.IsEquipped(item.id);
        var card = CreatePanel("Shared cosmetic " + item.id, profileShopContent, ShopCard);
        StyleShopBox(card.GetComponent<Image>(), ShopCard, true);
        var shadow = card.AddComponent<Shadow>(); shadow.effectColor = new Color32(100, 66, 40, 45); shadow.effectDistance = new Vector2(3, -4);
        var badge = ShopText(card.transform, "Item state", equipped ? "EQUIPPED" : owned ? "OWNED" : "", 14, 14, 22, ProfileGreen);
        badge.enableWordWrapping = false; badge.alignment = TextAlignmentOptions.Right;
        var well = CreatePanel("Preview box", card.transform, new Color32(237, 222, 191, 255));
        SetStretchRect(well.GetComponent<RectTransform>(), new Vector2(0, 1), Vector2.one, new Vector2(14, -182), new Vector2(-14, -42));
        StyleShopBox(well.GetComponent<Image>(), new Color32(237, 222, 191, 255)); well.GetComponent<Image>().raycastTarget = false;
        if (isPart && available)
        {
            var thumb = CCoinShopUI.Rect(well.transform, "Item preview", Vector2.zero, new Vector2(132, 132)).gameObject.AddComponent<RawImage>();
            thumb.texture = art.Thumbnail(item.id); thumb.raycastTarget = false;
        }
        else if (!isPart)
        {
            ColorUtility.TryParseHtmlString(item.color, out var frameColor);
            var sample = CreatePanel("Frame sample", well.transform, frameColor);
            SetRect(sample.GetComponent<RectTransform>(), Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(112, 112));
            var portrait = CreatePanel("Frame portrait", sample.transform, Color.white).GetComponent<Image>();
            SetStretchRect(portrait.rectTransform, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            ExportUIArt.Apply(portrait, "profileIcon"); portrait.preserveAspect = true; portrait.raycastTarget = false;
        }
        else ShopText(well.transform, "Model unavailable", "NO PREVIEW", 20, 42, 40, ShopMuted).alignment = TextAlignmentOptions.Center;
        string category = item.kind == "profile_frame" ? "FRAME" : item.kind == "shirt" ? "TOP" : item.kind == "pants" ? "BOTTOM" : item.kind.ToUpperInvariant();
        ShopText(card.transform, "Category", category, 15, 14, 22, ShopAccent);
        var title = ShopText(card.transform, "Name", item.name, 26, 196, 30, ProfileInk); title.enableWordWrapping = false;
        string detail = !available ? "Model unavailable" : isPart ? "Appearance only" : item.description ?? "Profile portrait frame";
        var description = ShopText(card.transform, "Description", detail, 17, 232, 22, ShopMuted); description.enableWordWrapping = false;
        ShopText(card.transform, "Price", item.price + " C-COINS", 20, 258, 24, ProfileGreen);
        if (isPart && available)
        {
            var preview = CreateButton("Try cosmetic", card.transform, "TRY ON");
            SetStretchRect(preview.GetComponent<RectTransform>(), Vector2.zero, new Vector2(.5f, 0), new Vector2(14, 16), new Vector2(-6, 60));
            preview.onClick.AddListener(() => PreviewProfileCosmetic(item.id)); StyleShopButton(preview, false);
        }
        bool canAct = available && listed && !equipped && (owned || profileWallet.CanBuy && profileWallet.Balance >= item.price);
        string action = equipped ? "EQUIPPED" : owned ? "EQUIP" : !available || !listed || !profileWallet.CanBuy ? "UNAVAILABLE" : profileWallet.Balance < item.price ? "NEED COINS" : "BUY";
        var buy = CreateButton("Buy or equip cosmetic", card.transform, action);
        SetStretchRect(buy.GetComponent<RectTransform>(), new Vector2(isPart && available ? .5f : 0, 0), new Vector2(1, 0), new Vector2(isPart && available ? 6 : 14, 16), new Vector2(-14, 60));
        buy.onClick.AddListener(() => { if (owned) profileWallet.Equip(item.id); else profileWallet.BuyCosmetic(item.id); });
        buy.interactable = canAct;
        StyleShopButton(buy, true);
    }
}
