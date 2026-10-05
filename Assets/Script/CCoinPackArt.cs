using UnityEngine;
using UnityEngine.UI;

// Resolution-independent illustration: C-stamped coins and production cases.
// Mesh art stays sharp at any canvas scale; no B-Coin imagery is reused.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class CCoinPackArt : MaskableGraphic
{
    public int tier;
    public bool singleCoin;
    static readonly Color Ink=new Color32(59,35,24,255), Gold=new Color32(247,178,35,255), Light=new Color32(255,224,115,255);
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (singleCoin) { Coin(vh, Vector2.zero, new Vector2(34, 38), true); return; }
        Ellipse(vh,new Vector2(0,-74),new Vector2(122,12),new Color(0,0,0,.14f));
        if(tier>0)
        {
            Box(vh,new Vector2(0,-27),new Vector2(206,87),Ink);
            Box(vh,new Vector2(0,-24),new Vector2(194,76),color);
            Box(vh,new Vector2(0,31),new Vector2(216,35),Ink);
            Box(vh,new Vector2(0,33),new Vector2(204,23),color*.85f);
            for(int x=-80;x<=80;x+=160)Box(vh,new Vector2(x,-25),new Vector2(12,75),Light);
            Box(vh,new Vector2(0,-24),new Vector2(28,30),Ink);
            Box(vh,new Vector2(0,-22),new Vector2(18,18),Gold);
        }
        int stacks=tier==0?3:tier+2;
        for(int s=0;s<stacks;s++)
        {
            float x=(s-(stacks-1)*.5f)*40;
            int count=2+(s+tier)%3;
            for(int n=0;n<count;n++)Coin(vh,new Vector2(x,(tier==0?-55:19)+n*11),new Vector2(29,12),false);
        }
        Coin(vh,new Vector2(tier==0?44:47,tier==0?-33:44),new Vector2(34,34),true);
        Sparkle(vh,new Vector2(-103,56),13);Sparkle(vh,new Vector2(109,27),9);Sparkle(vh,new Vector2(65,83),7);
    }
    void Coin(VertexHelper vh,Vector2 pos,Vector2 radius,bool stamp)
    {
        Ellipse(vh,pos,radius+Vector2.one*3,Ink); Ellipse(vh,pos,radius,Gold);
        Ellipse(vh,pos+new Vector2(-2,3),radius*.79f,Light);
        Ellipse(vh,pos+new Vector2(-2,3),radius*.64f,Gold);
        if(stamp)
        {
            // Open ring forms a C, not the B emblem used by production budgets.
            for(int a=50;a<310;a+=10)
            {
                float p=a*Mathf.Deg2Rad,q=(a+10)*Mathf.Deg2Rad;
                Quad(vh,pos+new Vector2(Mathf.Cos(p),Mathf.Sin(p))*18,pos+new Vector2(Mathf.Cos(q),Mathf.Sin(q))*18,
                    pos+new Vector2(Mathf.Cos(q),Mathf.Sin(q))*11,pos+new Vector2(Mathf.Cos(p),Mathf.Sin(p))*11,Ink);
            }
        }
    }
    void Sparkle(VertexHelper v,Vector2 p,float r)
    {
        Quad(v,p+new Vector2(-r,0),p+new Vector2(0,3),p+new Vector2(r,0),p+new Vector2(0,-3),Color.white);
        Quad(v,p+new Vector2(-3,0),p+new Vector2(0,r),p+new Vector2(3,0),p+new Vector2(0,-r),Color.white);
    }
    void Box(VertexHelper v,Vector2 p,Vector2 s,Color c){s*=.5f;Quad(v,p+new Vector2(-s.x,-s.y),p+new Vector2(-s.x,s.y),p+s,p+new Vector2(s.x,-s.y),c);}
    void Quad(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint)
    {
        int i=v.currentVertCount;v.AddVert(a,tint,Vector2.zero);v.AddVert(b,tint,Vector2.zero);v.AddVert(c,tint,Vector2.zero);v.AddVert(d,tint,Vector2.zero);
        v.AddTriangle(i,i+1,i+2);v.AddTriangle(i,i+2,i+3);
    }
    void Ellipse(VertexHelper v,Vector2 p,Vector2 r,Color tint)
    {
        int start=v.currentVertCount;v.AddVert(p,tint,Vector2.zero);
        for(int i=0;i<=48;i++){float a=i*Mathf.PI*2/48;v.AddVert(p+new Vector2(Mathf.Cos(a)*r.x,Mathf.Sin(a)*r.y),tint,Vector2.zero);if(i>0)v.AddTriangle(start,start+i,start+i+1);}
    }
}
