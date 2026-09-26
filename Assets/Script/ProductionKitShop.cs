using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Additional shop page uses the normal cart and checkout, including tutorial restrictions.
public sealed class ProductionKitShop : MonoBehaviour
{
    ShopTerminal shop;
    readonly List<GameObject> templates=new List<GameObject>();
    readonly List<GameObject> pages=new List<GameObject>();
    public static void Setup(ShopTerminal shop)
    {
        if(!HasEquipmentLesson(CampaignProgression.GetCurrentLevel())||shop.GetComponent<ProductionKitShop>()!=null)return;
        var catalog=shop.gameObject.AddComponent<ProductionKitShop>();catalog.shop=shop;catalog.Build();
    }
    // Reserved assets: enable only after a level has a dedicated guided lesson.
    public static bool HasEquipmentLesson(int level) => false;
    public static bool HasCameraGripLesson(int level) => false;
    void Build()
    {
        var indices=new List<int>();
        for(int kind=0;kind<ProductionKit.Names.Length;kind++)
        {
            if(kind != 0) continue; // Rim-light lesson introduces only the strip.
            int index=shop.availableItems.FindIndex(x=>x.itemName==ProductionKit.Names[kind]);
            if(index<0){var prefab=ProductionKit.Template(kind);templates.Add(prefab);index=shop.availableItems.Count;shop.availableItems.Add(new ShopItem{itemName=ProductionKit.Names[kind],price=ProductionKit.Prices[kind],prefabToSpawn=prefab});}
            indices.Add(index);
        }
        foreach(int index in indices){shop.CreateStripShopCard(shop.worldSpaceCanvas,index);shop.CreateStripShopCard(shop.screenSpaceCanvas,index);}
        if(FindObjectOfType<ProductionLightModifiers>()==null)new GameObject("Production light control").AddComponent<ProductionLightModifiers>();
    }
    RectTransform Box(Transform parent,string name,Vector2 min,Vector2 max,Color color)
    {
        var o=new GameObject(name,typeof(RectTransform),typeof(Image));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;o.GetComponent<Image>().color=color;return r;
    }
    void Label(Transform parent,string words)
    {
        var o=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));o.transform.SetParent(parent,false);var r=o.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(8,3);r.offsetMax=new Vector2(-8,-3);var t=o.GetComponent<TextMeshProUGUI>();t.text=words;t.fontSize=18;t.enableAutoSizing=true;t.fontSizeMin=10;t.fontSizeMax=20;t.color=Color.white;t.alignment=TextAlignmentOptions.MidlineLeft;t.raycastTarget=false;
    }
    void BuildPage(Canvas canvas,List<int> indices)
    {
        if(canvas==null)return;
        var page=Box(canvas.transform,"Lighting and grip catalog",new Vector2(.22f,.18f),new Vector2(.79f,.81f),new Color(.10f,.13f,.17f));pages.Add(page.gameObject);
        for(int k=0;k<indices.Count;k++)
        {
            int index=indices[k];float y=.98f-k*.132f;
            var row=Box(page,"Add "+ProductionKit.Names[k],new Vector2(.02f,y-.12f),new Vector2(.98f,y),new Color(.16f,.23f,.30f));
            Label(row,ProductionKit.Names[k]+"  |  "+ProductionKit.Prices[k]+" B  |  + ADD\n"+ProductionKit.Lessons[k]);
            row.gameObject.AddComponent<Button>().onClick.AddListener(()=>shop.AddItemToCartByIndex(index));
        }
        var toggle=Box(canvas.transform,"Lighting and grip tab",new Vector2(.23f,.82f),new Vector2(.51f,.875f),new Color(.1f,.3f,.48f));Label(toggle,"LIGHTING & GRIP / BACK");toggle.gameObject.AddComponent<Button>().onClick.AddListener(()=>{bool open=!page.gameObject.activeSelf;ClosePages();page.gameObject.SetActive(open);});page.gameObject.SetActive(false);
    }
    void BuildWorkflow(Canvas canvas)
    {
        string[] steps={"BRIEF: confirm message, duration and delivery format.","SHOT LIST: plan hero, detail and moving shots before recording.","SET CHECK: clean the product; secure stands; clear cable paths.","LIGHT & CAMERA: check reflections, focus and exposure on the product.","TAKES & MEDIA: review coverage and verify your recording before editing.","DELIVERY: check picture, sound, branding and contract requirements."};
        var panel=Box(canvas.transform,"Production checklist",new Vector2(.22f,.18f),new Vector2(.79f,.81f),new Color(.10f,.13f,.17f));pages.Add(panel.gameObject);
        for(int i=0;i<steps.Length;i++)
        {
            string key="ProductionChecklist."+CampaignProgression.GetCurrentLevel()+"."+i;string words=steps[i];float y=.96f-i*.145f;
            var row=Box(panel,"Self check",new Vector2(.02f,y-.13f),new Vector2(.98f,y),new Color(.16f,.23f,.30f));
            Label(row,(GameSavePrefs.GetInt(key,0)==1?"[DONE] ":"[  ] ")+words);var text=row.GetComponentInChildren<TextMeshProUGUI>();
            row.gameObject.AddComponent<Button>().onClick.AddListener(()=>{int done=1-GameSavePrefs.GetInt(key,0);GameSavePrefs.SetInt(key,done);GameSavePrefs.Save();text.text=(done==1?"[DONE] ":"[  ] ")+words;});
        }
        Label(Box(panel,"Note",new Vector2(.02f,.01f),new Vector2(.98f,.08f),Color.clear),"Self-check only: ticking a box does not award marks.");
        var toggle=Box(canvas.transform,"Checklist tab",new Vector2(.52f,.82f),new Vector2(.79f,.875f),new Color(.1f,.3f,.48f));Label(toggle,"PRODUCTION CHECKLIST");
        toggle.gameObject.AddComponent<Button>().onClick.AddListener(()=>{bool open=!panel.gameObject.activeSelf;ClosePages();panel.gameObject.SetActive(open);});panel.gameObject.SetActive(false);
    }
    public void ClosePages(){foreach(var p in pages)if(p!=null)p.SetActive(false);}
    void OnDestroy(){foreach(var t in templates)if(t!=null)Destroy(t);}
}


