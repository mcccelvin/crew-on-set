using UnityEngine;
using System.Collections.Generic;

// Replace the procedural meshes with authored assets; keep the root and light anchors.
public sealed class AutomotiveGrip : MonoBehaviour
{
    public bool IsLightBank { get; private set; }
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Light> lights = new List<Light>();
    private int power = 2;
    public string Controls => IsLightBank ? "OVERHEAD BANK | T: move  Q/E: turn  F: " + new[]{"OFF","LOW","MEDIUM","HIGH"}[power] : "BLACK FLAG | T: move  Q/E: turn | Blocks direct light";
    public static int Cost(int index) => index == -2 ? 1800 : 350;
    public static GameObject Create(bool lightBank)
    {
        var root = new GameObject(lightBank ? "OVERHEAD LIGHT BANK_Wrapper" : "BLACK FLAG_Wrapper");
        var rig = root.AddComponent<AutomotiveGrip>();rig.IsLightBank = lightBank;rig.Build();return root;
    }
    private Material Material(string name, Color color)
    {
        Shader shader=Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null) shader=Shader.Find("Standard") ?? shader;
        var m=new Material(shader){name=name,color=color};materials.Add(m);return m;
    }
    private void Part(string name,Vector3 position,Vector3 size,Material material,bool cast=true)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(transform,false);part.transform.localPosition=position;part.transform.localScale=size;
        part.GetComponent<Renderer>().sharedMaterial=material;
        part.GetComponent<Renderer>().shadowCastingMode=cast ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        part.GetComponent<Collider>().enabled=false;
    }
    private void Build()
    {
        var metal=Material("Grip metal",new Color(.12f,.13f,.14f));var black=Material("Black fabric",new Color(.012f,.012f,.012f));
        if(IsLightBank)
        {
            var white=Material("Diffusion face",Color.white);
            // Self-supporting visual gantry; never imply that a giant bank floats unsupported.
            foreach(float x in new[]{-2.1f,2.1f})
            {
                Part("Upright",new Vector3(x,1.65f,0),new Vector3(.10f,3.3f,.10f),metal);
                Part("Foot",new Vector3(x,.07f,0),new Vector3(.45f,.14f,1.4f),metal);
                Part("Ballast",new Vector3(x,.18f,.25f),new Vector3(.4f,.16f,.5f),black);
            }
            Part("Top crossbar",new Vector3(0,3.3f,0),new Vector3(4.3f,.12f,.12f),metal);
            Part("Bank housing",new Vector3(0,3.08f,0),new Vector3(3.6f,.28f,2.2f),black,false);
            Part("White diffuser",new Vector3(0,2.93f,0),new Vector3(3.5f,.025f,2.1f),white,false);
            foreach(float x in new[]{-.95f,.95f})foreach(float z in new[]{-.55f,.55f})
            {
                var source=new GameObject("Downlight anchor");source.transform.SetParent(transform,false);source.transform.localPosition=new Vector3(x,2.88f,z);source.transform.localRotation=Quaternion.Euler(90,0,0);
                var light=source.AddComponent<Light>();light.type=LightType.Spot;light.spotAngle=105;light.range=8;light.color=Color.white;light.shadows=LightShadows.Soft;light.shadowBias=.02f;light.shadowNormalBias=.1f;light.renderMode=LightRenderMode.ForcePixel;lights.Add(light);
            }
            RefreshPower();
        }
        else
        {
            Part("Stand riser",new Vector3(0,.8f,0),new Vector3(.045f,1.6f,.045f),metal);
            Part("Leg A",new Vector3(0,.06f,0),new Vector3(.9f,.06f,.07f),metal);
            Part("Leg B",new Vector3(0,.09f,.25f),new Vector3(.07f,.06f,.7f),metal);
            Part("Sandbag",new Vector3(-.2f,.15f,0),new Vector3(.38f,.15f,.3f),black);
            Part("Grip head",new Vector3(0,1.6f,0),new Vector3(.12f,.12f,.12f),metal);
            Part("Flag frame",new Vector3(0,1.8f,0),new Vector3(1.24f,1.24f,.035f),metal);
            Part("Opaque black flag",new Vector3(0,1.8f,0),new Vector3(1.20f,1.20f,.05f),black);
        }
        var bounds=new Bounds(transform.position,Vector3.zero);foreach(var r in GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
        var selection=gameObject.AddComponent<BoxCollider>();selection.center=bounds.center-transform.position;selection.size=bounds.size;
    }
    public void CyclePower(){if(!IsLightBank)return;power=(power+1)%4;RefreshPower();}
    private void RefreshPower(){foreach(var light in lights){light.enabled=power>0;light.intensity=power*.65f;}}
    private void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);}
}
