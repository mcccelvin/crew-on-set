using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class DirectorTerminal
{
    [Serializable] private sealed class StoredTabletItem
    {
        public string kind, name, model;
        public int index, count;
        public string Key => kind + "/" + name + "/" + index + "/" + model;
    }
    [Serializable] private sealed class TabletInventory { public List<StoredTabletItem> items = new List<StoredTabletItem>(); }
    private const string StorageKey = "Director.OwnedItems.v1";
    private readonly Dictionary<GameObject, string> placedStorageItems = new Dictionary<GameObject, string>();
    private bool placingStoredItem;
    private GameObject storagePanel;
    private RectTransform storageRows;
    private RectTransform elementsArea;
    private TMP_Text storageToggleLabel;

    private void SetStorageMode(bool showStorage)
    {
        if (storagePanel == null) return;
        if (showStorage) RefreshStorage();
        storagePanel.SetActive(showStorage);
        elementsArea.Find("Elements viewport").gameObject.SetActive(!showStorage);
        elementsArea.Find("Elements scrollbar").gameObject.SetActive(!showStorage);
        elementsArea.GetComponent<ScrollRect>().enabled = !showStorage;
        storageToggleLabel.text = showStorage ? "SHOP" : "STORAGE";
    }

    private void ConfigureElementsBalance()
    {
        if (bCoinsText == null) return;
        var wallet = bCoinsText.transform.parent as RectTransform;
        if (wallet == null) return;
        wallet.SetParent(elementsArea, false);
        wallet.anchorMin = wallet.anchorMax = Vector2.one;
        wallet.pivot = new Vector2(1, 0);
        wallet.anchoredPosition = new Vector2(-14, 10);
        wallet.sizeDelta = new Vector2(224, 46);
        wallet.localScale = Vector3.one;
        var label = bCoinsText.rectTransform;
        label.anchorMin = Vector2.zero; label.anchorMax = Vector2.one;
        label.offsetMin = new Vector2(60, 2); label.offsetMax = new Vector2(-8, -2);
        bCoinsText.enableWordWrapping = false;
        bCoinsText.enableAutoSizing = true;
        bCoinsText.fontSizeMin = 18; bCoinsText.fontSizeMax = 28;
        bCoinsText.alignment = TextAlignmentOptions.Center;
        bCoinsText.text = (CareerManager.Instance != null ? CareerManager.Instance.playerMoney : 0).ToString("N0");
    }

    private void EnsureElementsScroll()
    {
        if(elementsArea!=null || propUIContainer==null)return;
        var content=(RectTransform)propUIContainer;
        elementsArea=new GameObject("Elements panel controls",typeof(RectTransform)).GetComponent<RectTransform>();
        elementsArea.SetParent(content.parent,false);
        elementsArea.anchorMin=content.anchorMin;elementsArea.anchorMax=content.anchorMax;
        elementsArea.pivot=content.pivot;elementsArea.sizeDelta=content.sizeDelta;
        elementsArea.anchoredPosition=content.anchoredPosition;
        elementsArea.SetSiblingIndex(content.GetSiblingIndex());
        var viewport=new GameObject("Elements viewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(elementsArea,false);viewport.anchorMin=Vector2.zero;viewport.anchorMax=Vector2.one;
        viewport.offsetMin=new Vector2(4,24);viewport.offsetMax=new Vector2(-4,-4);
        viewport.GetComponent<Image>().color=Color.clear;
        content.SetParent(viewport,false);content.anchorMin=Vector2.zero;content.anchorMax=new Vector2(0,1);
        content.pivot=new Vector2(0,.5f);content.anchoredPosition=Vector2.zero;content.sizeDelta=Vector2.zero;
        var grid=content.GetComponent<GridLayoutGroup>()??content.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint=GridLayoutGroup.Constraint.FixedRowCount;grid.constraintCount=1;
        grid.startAxis=GridLayoutGroup.Axis.Horizontal;grid.childAlignment=TextAnchor.MiddleLeft;
        grid.cellSize=new Vector2(150,130);grid.spacing=new Vector2(12,0);grid.padding=new RectOffset(4,4,0,0);
        var fitter=content.GetComponent<ContentSizeFitter>()??content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;fitter.verticalFit=ContentSizeFitter.FitMode.Unconstrained;
        var scroll=elementsArea.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;
        scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
        var track=new GameObject("Elements scrollbar",typeof(RectTransform),typeof(Image),typeof(Scrollbar)).GetComponent<RectTransform>();
        track.SetParent(elementsArea,false);track.anchorMin=track.anchorMax=Vector2.zero;track.anchorMax=new Vector2(1,0);
        track.pivot=new Vector2(.5f,0);track.offsetMin=new Vector2(8,3);track.offsetMax=new Vector2(-8,19);
        track.GetComponent<Image>().color=new Color32(206,216,232,255);
        var handle=new GameObject("Handle",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();
        handle.SetParent(track,false);handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=handle.offsetMax=Vector2.zero;
        handle.GetComponent<Image>().color=new Color32(50,83,137,255);
        var bar=track.GetComponent<Scrollbar>();bar.handleRect=handle;bar.targetGraphic=handle.GetComponent<Image>();
        bar.direction=Scrollbar.Direction.LeftToRight;scroll.horizontalScrollbar=bar;
    }

    private TabletInventory ReadTabletInventory()
    {
        string json = GameSavePrefs.GetString(StorageKey, "");
        if (string.IsNullOrEmpty(json)) return new TabletInventory();
        try { return JsonUtility.FromJson<TabletInventory>(json) ?? new TabletInventory(); }
        catch (Exception e) { Debug.LogWarning("Unable to read tablet storage: " + e.Message); return new TabletInventory(); }
    }

    private void RememberTabletPurchase(string kind, string label, int index, string model, GameObject item)
    {
        if (item == null) return;
        var entry = new StoredTabletItem { kind = kind, name = label, index = index, model = model };
        placedStorageItems[item] = entry.Key;
        if (placingStoredItem || CareerManager.Instance == null) return;
        var inventory = ReadTabletInventory();
        var owned = inventory.items.Find(value => value.Key == entry.Key);
        if (owned == null) { owned = entry; inventory.items.Add(owned); }
        owned.count++;
        GameSavePrefs.SetString(StorageKey, JsonUtility.ToJson(inventory));
        GameSavePrefs.Save();
    }

    private int StorageOnSet(StoredTabletItem item)
    {
        int count = 0;
        foreach (var pair in placedStorageItems)
            if (pair.Key != null && pair.Key.activeInHierarchy && pair.Value == item.Key) count++;
        // Checkpoint-restored props retain their wrapper names but not runtime references.
        foreach (var body in FindObjectsOfType<Rigidbody>())
            if (!placedStorageItems.ContainsKey(body.gameObject) && body.name == item.name + "_Wrapper") count++;
        return count;
    }

    private void PlaceStoredItem(StoredTabletItem item)
    {
        if (draggedObject != null || IsActorPractice || !CanUseContract4Practice("tablet.storage")) return;
        var owned = ReadTabletInventory().items.Find(value => value.Key == item.Key);
        if (owned == null || owned.count <= StorageOnSet(owned)) { RefreshStorage(); return; }
        placingStoredItem = true;
        try
        {
            if (item.kind == "prefab")
            {
                GameObject prefab = null;
                foreach (var bank in propDatabase)
                    foreach (var candidate in bank.allowedProps)
                        if (candidate != null && candidate.name == item.model) prefab = candidate;
                if (prefab == null) { GameFeedback.Show("This stored item's model is unavailable."); return; }
                StartDraggingNewProp(prefab);
            }
            else if (item.kind == "product") StartDraggingCampaignProduct(item.name, item.index, item.model);
            else StartDraggingStageItem(item.name, item.kind == "actor", item.index);
            if (draggedObject != null) SetStorageMode(false);
        }
        finally { placingStoredItem = false; }
    }

    private Button StorageButton(Transform parent, string caption, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
        rect.pivot = new Vector2(.5f, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
        var button = go.GetComponent<Button>();
        CrewPaperStyle.ActionButton(button);
        var text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(rect, false);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(12, 6); text.rectTransform.offsetMax = new Vector2(-12, -6);
        text.font = TMP_Settings.defaultFontAsset; text.fontSize = 23; text.color = CrewPaperStyle.Ink;
        text.text = caption;
        text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = 23;
        text.raycastTarget = false; button.onClick.AddListener(action);
        return button;
    }

    private void ConfigureTabletCloseButton()
    {
        if (tabletUI == null || viewportUI == null) return;
        // The tablet animation root has zero dimensions; anchor to its full-size
        // artwork frame instead so top-right really means the screen's right edge.
        var frame=elementsArea!=null?elementsArea.parent:tabletUI.transform;
        var oldClose = viewportUI.Find("Terminal close button")??tabletUI.transform.Find("Terminal close button");
        if (oldClose != null) oldClose.SetParent(frame, false);
        UIMotionInstaller.EnsureTerminalClose(frame, CloseTerminal);
        var close = frame.Find("Terminal close button");
        if (close == null) return;
        var rect = (RectTransform)close;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-12, -12);
        rect.sizeDelta = new Vector2(64, 64);
        ExportUIArt.Apply(close.GetComponent<Image>(),"close");
        close.GetComponent<Image>().preserveAspect=false;
        rect.localScale=Vector3.one;
        foreach(var caption in close.GetComponentsInChildren<TMP_Text>(true))caption.gameObject.SetActive(false);
        foreach(var outline in close.GetComponents<Outline>())outline.enabled=false;
        foreach(var shadow in close.GetComponents<Shadow>())shadow.enabled=false;
        close.SetAsLastSibling();
    }

    private void BuildStorageUI()
    {
        if (tabletUI == null || viewportUI == null || storagePanel != null) return;
        EnsureElementsScroll();
        ConfigureElementsBalance();
        var open = StorageButton(elementsArea!=null?elementsArea:viewportUI, "STORAGE", new Vector2(14, 12), new Vector2(174, 42), () => {
            if (!CanUseContract4Practice("tablet.storage") || IsActorPractice) return;
            SetStorageMode(!storagePanel.activeSelf);
        });
        var openRect = (RectTransform)open.transform;
        // Place the action above the list, inside the blue Elements heading.
        openRect.anchorMin = openRect.anchorMax = new Vector2(0,1);
        openRect.pivot = new Vector2(0,0);
        var storageImage=open.GetComponent<Image>();
        ExportUIArt.Apply(storageImage,"blueButton");
        foreach(var outline in open.GetComponents<Outline>())outline.enabled=false;
        foreach(var shadow in open.GetComponents<Shadow>())shadow.enabled=false;
        var storageLabel=open.GetComponentInChildren<TMP_Text>();
        storageToggleLabel=storageLabel;
        storageLabel.color=Color.white;storageLabel.fontStyle=FontStyles.Bold;
        storageLabel.enableWordWrapping=false;storageLabel.fontSizeMin=18;storageLabel.fontSizeMax=23;
        if(storageLabel is TextMeshProUGUI outlinedLabel)ExportUIArt.OutlineText(outlinedLabel);
        var storageColors=open.colors;storageColors.normalColor=Color.white;
        storageColors.highlightedColor=new Color(.85f,.92f,1);storageColors.pressedColor=new Color(.65f,.76f,.94f);
        open.colors=storageColors;
        storagePanel = new GameObject("Tablet Storage", typeof(RectTransform), typeof(Image));
        storagePanel.transform.SetParent(elementsArea, false);
        var panelRect = (RectTransform)storagePanel.transform;
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        storagePanel.GetComponent<Image>().color=Color.white;
        storagePanel.GetComponent<Image>().raycastTarget = true;
        var view = new GameObject("Storage list",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
        view.transform.SetParent(panelRect,false);
        var viewRect = (RectTransform)view.transform; viewRect.anchorMin=Vector2.zero; viewRect.anchorMax=Vector2.one;
        viewRect.offsetMin=new Vector2(8,8); viewRect.offsetMax=new Vector2(-8,-8);
        view.GetComponent<Image>().color=new Color32(244,247,253,255);
        storageRows=new GameObject("Owned items",typeof(RectTransform)).GetComponent<RectTransform>();
        storageRows.SetParent(viewRect,false); storageRows.anchorMin=new Vector2(0,1);storageRows.anchorMax=Vector2.one;storageRows.pivot=new Vector2(.5f,1);
        storageRows.sizeDelta=Vector2.zero;
        var scroll=view.GetComponent<ScrollRect>();scroll.content=storageRows;scroll.viewport=viewRect;scroll.horizontal=false;scroll.scrollSensitivity=35;
        scroll.movementType=ScrollRect.MovementType.Clamped;
        storagePanel.SetActive(false);
    }

    private void RefreshStorage()
    {
        foreach (Transform child in storageRows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        var items=ReadTabletInventory().items;
        int row=0;
        for (int style=0;style<=2;style++)
        {
            if (!OwnsInterior(style)) continue;
            int selectedStyle=style;
            var setButton=StorageButton(storageRows,StageInterior.Title(style)+"\nOWNED SET — USE",new Vector2(0,-row*88),new Vector2(700,76),()=>{
                if (draggedObject != null || IsActorPractice || !CanUseContract4Practice("tablet.storage")) return;
                SelectInterior(selectedStyle); SetStorageMode(false);
            });
            var setRect=(RectTransform)setButton.transform;setRect.anchorMin=new Vector2(0,1);setRect.anchorMax=Vector2.one;setRect.sizeDelta=new Vector2(-20,76);
            row++;
        }
        foreach(var item in items)
        {
            int onSet=StorageOnSet(item), available=Mathf.Max(0,item.count-onSet);
            var button=StorageButton(storageRows,item.name+"  |  OWNED "+item.count+"  •  ON SET "+onSet+"  •  AVAILABLE "+available+
                (available>0 ? "\nPLACE FROM STORAGE" : "\nRemove from the set to store it"),new Vector2(0,-row*88),new Vector2(700,76),()=>PlaceStoredItem(item));
            var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.sizeDelta=new Vector2(-20,76);
            button.interactable=available>0;row++;
        }
        if(row==0) StorageButton(storageRows,"No stored purchases yet.\nBuy an element to add it here.",new Vector2(0,-20),new Vector2(650,90),()=>{}).interactable=false;
        storageRows.sizeDelta=new Vector2(0,Mathf.Max(120,row*88));
    }
}
