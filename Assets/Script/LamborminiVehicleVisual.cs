using System.Collections.Generic;
using UnityEngine;

// Original lightweight coupe. +X is the nose; the wrapper stays compatible with stage dragging.
public sealed class LamborminiVehicleVisual : MonoBehaviour
{
    private readonly List<Object> generated = new List<Object>();
    public static GameObject Create(string name)
    {
        var car = new GameObject(name + "_Wrapper");
        car.AddComponent<CubeVehicle>();
        car.AddComponent<LamborminiVehicleVisual>().Build();
        var collider = car.AddComponent<BoxCollider>();
        collider.center = new Vector3(0, .54f, 0);
        collider.size = new Vector3(2.72f, 1.08f, 1.64f);
        return car;
    }
    private Material Material(string name, Color color, float metallic, float gloss, bool emission = false)
    {
        var material = new Material(Shader.Find("Standard")) { name = name, color = color };
        material.SetFloat("_Metallic", metallic); material.SetFloat("_Glossiness", gloss);
        if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 2f); }
        generated.Add(material); return material;
    }
    private void Build()
    {
        var paint = Material("Terrari orange pearl", new Color(1f, .29f, .018f), .58f, .87f);
        var glass = Material("Tinted canopy", new Color(.025f, .045f, .057f), .62f, .96f);
        var carbon = Material("Carbon trim", new Color(.018f, .019f, .022f), .25f, .54f);
        var tire = Material("Tire rubber", new Color(.017f, .017f, .019f), .0f, .2f);
        var alloy = Material("Brushed alloy", new Color(.52f, .56f, .6f), .85f, .8f);
        var white = Material("White LED", new Color(.83f, .94f, 1f), .15f, .7f, true);
        var red = Material("Rear LED", new Color(1f, .018f, .005f), .2f, .7f, true);
        Hull("Sculpted body", new[] { -1.35f,-1.1f,-.65f,.3f,.9f,1.35f }, new[] { .6f,.75f,.7f,.63f,.57f,.38f }, new[] { .55f,.73f,.69f,.72f,.71f,.59f }, .23f, paint);
        Hull("Canopy", new[] { -.88f,-.49f,.02f,.52f }, new[] { .68f,1.02f,1.04f,.64f }, new[] { .51f,.5f,.47f,.56f }, .57f, glass);
        Box("Roof spine", new Vector3(-.24f,1.035f,0), new Vector3(.54f,.025f,.62f),paint);
        Box("Front splitter", new Vector3(1.1f,.22f,0),new Vector3(.55f,.055f,1.47f),carbon);
        Box("Rear diffuser",new Vector3(-1.3f,.28f,0),new Vector3(.15f,.19f,1.26f),carbon);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Side skirt", new Vector3(0,.22f,.76f*side),new Vector3(2.22f,.07f,.1f),carbon);
            Box("Side intake",new Vector3(-.42f,.49f,.705f*side),new Vector3(.43f,.2f,.025f),carbon);
            Box("Mirror stem",new Vector3(.23f,.77f,.66f*side),new Vector3(.08f,.04f,.19f),carbon);
            Box("Mirror",new Vector3(.25f,.8f,.78f*side),new Vector3(.19f,.09f,.11f),paint);
            var lamp=Box("Headlamp housing",new Vector3(1.15f,.48f,.46f*side),new Vector3(.31f,.045f,.3f),carbon);
            lamp.transform.localRotation=Quaternion.Euler(0,0,-17);
            for(int line=0;line<3;line++)
            {
                var led=Box("Headlight LED",new Vector3(1.17f-line*.065f,.51f+line*.018f,.47f*side),new Vector3(.018f,.018f,.23f),white);
                led.transform.localRotation=Quaternion.Euler(0,side*14,0);
            }
            Box("Front intake",new Vector3(1.354f,.32f,.4f*side),new Vector3(.016f,.13f,.24f),carbon);
            Box("Tail light",new Vector3(-1.36f,.56f,.38f*side),new Vector3(.025f,.04f,.4f),red);
            for(int axle=-1;axle<=1;axle+=2)
            {
                Vector3 hub=new Vector3(axle*.88f,.3f,side*.72f);
                Cylinder("Round tire",hub,new Vector3(.6f,.115f,.6f),tire);
                Cylinder("Alloy rim",hub+Vector3.forward*side*.121f,new Vector3(.44f,.012f,.44f),carbon);
                Cylinder("Brake disc",hub+Vector3.forward*side*.132f,new Vector3(.31f,.008f,.31f),alloy);
                for(int spoke=0;spoke<10;spoke++)
                {
                    float a=spoke*Mathf.PI/5;
                    var part=Box("Wheel spoke",hub+new Vector3(Mathf.Cos(a)*.115f,Mathf.Sin(a)*.115f,side*.147f),new Vector3(.22f,.026f,.018f),alloy);
                    part.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
                }
                Cylinder("Wheel center",hub+Vector3.forward*side*.156f,new Vector3(.09f,.013f,.09f),paint);
            }
        }
        for(int vent=0;vent<5;vent++) Box("Rear deck vent",new Vector3(-.93f-vent*.067f,.749f-vent*.02f,0),new Vector3(.035f,.015f,.7f),carbon);
    }
    private GameObject Box(string name,Vector3 position,Vector3 scale,Material material) => Part(PrimitiveType.Cube,name,position,scale,material);
    private void Cylinder(string name,Vector3 position,Vector3 scale,Material material)
    { var part=Part(PrimitiveType.Cylinder,name,position,scale,material); part.transform.localRotation=Quaternion.Euler(90,0,0); }
    private GameObject Part(PrimitiveType type,string name,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(type); part.name=name; part.transform.SetParent(transform,false);
        part.transform.localPosition=position; part.transform.localScale=scale; part.GetComponent<Renderer>().sharedMaterial=material;
        var collider=part.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
        return part;
    }
    private void Hull(string name,float[] xs,float[] heights,float[] widths,float bottom,Material material)
    {
        var vertices=new List<Vector3>(); var triangles=new List<int>();
        for(int i=0;i<xs.Length;i++)
        {
            float x=xs[i],h=heights[i],w=widths[i];
            vertices.AddRange(new[] { new Vector3(x,bottom,-w*.88f),new Vector3(x,h-.1f,-w),new Vector3(x,h,-w*.72f),new Vector3(x,h,w*.72f),new Vector3(x,h-.1f,w),new Vector3(x,bottom,w*.88f) });
        }
        for(int ring=0;ring<xs.Length-1;ring++) for(int side=0;side<6;side++)
        { int a=ring*6+side,b=ring*6+(side+1)%6,c=a+6,d=b+6;triangles.AddRange(new[]{a,b,c,b,d,c}); }
        for(int i=1;i<5;i++){triangles.AddRange(new[]{0,i+1,i});int end=(xs.Length-1)*6;triangles.AddRange(new[]{end,end+i,end+i+1});}
        var mesh=new Mesh{name=name}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();generated.Add(mesh);
        var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(transform,false);part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<Renderer>().sharedMaterial=material;
    }
    private void OnDestroy(){foreach(var asset in generated)if(asset!=null)Destroy(asset);}
}
