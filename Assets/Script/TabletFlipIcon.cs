using UnityEngine;
using UnityEngine.UI;

// Vector artwork: two curved arrows, with the tablet's dark edge and white face.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TabletFlipIcon : MaskableGraphic
{
    public static Button Create(Transform parent,string name,UnityEngine.Events.UnityAction action)
    {
        var root=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
        root.transform.SetParent(parent,false);
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(58,44);
        var image=root.GetComponent<Image>();image.color=Color.clear;
        var button=root.GetComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(action);
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=button.colors;colors.highlightedColor=new Color(.85f,.94f,1);colors.pressedColor=new Color(.65f,.78f,.94f);button.colors=colors;
        var icon=new GameObject("Flip arrows",typeof(RectTransform),typeof(CanvasRenderer),typeof(TabletFlipIcon)).GetComponent<TabletFlipIcon>();
        icon.transform.SetParent(root.transform,false);icon.rectTransform.anchorMin=Vector2.zero;icon.rectTransform.anchorMax=Vector2.one;
        icon.rectTransform.offsetMin=new Vector2(11,7);icon.rectTransform.offsetMax=new Vector2(-11,-7);
        icon.color=new Color32(27,42,59,255);icon.raycastTarget=false;button.targetGraphic=icon;
        return button;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();var rect=rectTransform.rect;
        Shape(mesh,rect.center+new Vector2(1.2f,-1.4f),rect.size,new Color32(255,255,255,190));
        Shape(mesh,rect.center,rect.size,color);
    }
    private static void Shape(VertexHelper mesh,Vector2 centre,Vector2 size,Color32 tint)
    {
        Vector2 scale=new Vector2(size.x*.46f,size.y*.42f);
        for(int arrow=0;arrow<2;arrow++)
        {
            float start=(arrow*180-35)*Mathf.Deg2Rad,end=start+132*Mathf.Deg2Rad;
            const int steps=24;
            for(int i=0;i<steps;i++)
            {
                float a=Mathf.Lerp(start,end,(float)i/steps),b=Mathf.Lerp(start,end,(float)(i+1)/steps);
                Vector2 da=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),db=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                int first=mesh.currentVertCount;
                mesh.AddVert(centre+Vector2.Scale(da*1.02f,scale),tint,Vector2.zero);
                mesh.AddVert(centre+Vector2.Scale(da*.66f,scale),tint,Vector2.zero);
                mesh.AddVert(centre+Vector2.Scale(db*.66f,scale),tint,Vector2.zero);
                mesh.AddVert(centre+Vector2.Scale(db*1.02f,scale),tint,Vector2.zero);
                mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
            }
            var radial=new Vector2(Mathf.Cos(end),Mathf.Sin(end));
            var tangent=new Vector2(-radial.y,radial.x);
            Vector2 joint=radial*.84f;
            int head=mesh.currentVertCount;
            mesh.AddVert(centre+Vector2.Scale(joint+tangent*.52f,scale),tint,Vector2.zero);
            mesh.AddVert(centre+Vector2.Scale(joint-tangent*.14f+radial*.47f,scale),tint,Vector2.zero);
            mesh.AddVert(centre+Vector2.Scale(joint-tangent*.14f-radial*.47f,scale),tint,Vector2.zero);
            mesh.AddTriangle(head,head+1,head+2);
        }
    }
}
