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
            if (draggedObject != null) storagePanel.SetActive(false);
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
        text.alignment = TextAlignmentOptions.Center; text.enableAutoSizing = true; text.fontSizeMin = 16; text.fontSizeMax = 23;
        text.raycastTarget = false; button.onClick.AddListener(action);
        return button;
    }

    private void BuildStorageUI()
    {
        if (tabletUI == null || viewportUI == null || storagePanel != null) return;
        var open = StorageButton(viewportUI, "STORAGE", new Vector2(22, -22), new Vector2(160, 50), () => {
            if (!CanUseContract4Practice("tablet.storage") || IsActorPractice) return;
            RefreshStorage(); storagePanel.SetActive(true); storagePanel.transform.SetAsLastSibling();
        });
        var openRect = (RectTransform)open.transform;
        openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0, 1);
        storagePanel = new GameObject("Tablet Storage", typeof(RectTransform), typeof(Image));
        storagePanel.transform.SetParent(tabletUI.transform, false);
        var panelRect = (RectTransform)storagePanel.transform;
        panelRect.anchorMin = new Vector2(.13f,.15f); panelRect.anchorMax = new Vector2(.87f,.86f);
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        CrewPaperStyle.Card(storagePanel.GetComponent<Image>());
        storagePanel.GetComponent<Image>().raycastTarget = true;
        StorageButton(panelRect, "STORAGE — PURCHASED ITEMS", new Vector2(-80,-18), new Vector2(590,58), () => {}).interactable = false;
        var close = StorageButton(panelRect,"CLOSE",new Vector2(-20,-20),new Vector2(130,50),()=>storagePanel.SetActive(false));
        var closeRect = (RectTransform)close.transform; closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = Vector2.one;
        var view = new GameObject("Storage list",typeof(RectTransform),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));
        view.transform.SetParent(panelRect,false);
        var viewRect = (RectTransform)view.transform; viewRect.anchorMin=Vector2.zero; viewRect.anchorMax=Vector2.one;
        viewRect.offsetMin=new Vector2(24,24); viewRect.offsetMax=new Vector2(-24,-90);
        view.GetComponent<Image>().color=new Color32(239,228,202,255);
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
                SelectInterior(selectedStyle); storagePanel.SetActive(false);
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
