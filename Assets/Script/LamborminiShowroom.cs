using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Scenery is separate from the movable vehicle and its grading bounds.
public sealed class LamborminiShowroom : MonoBehaviour
{
    private readonly List<Material> materials = new List<Material>();
    private Material backdropMaterial;
    public Color BackdropColor => backdropMaterial != null ? backdropMaterial.color : Color.black;
    public void SetBackdropColor(Color color) { if (backdropMaterial != null) backdropMaterial.color = color; }
    public static LamborminiShowroom Install(Renderer stage, Vector3 front)
    {
        if(stage==null)return null;
        var existing=FindObjectOfType<LamborminiShowroom>();if(existing!=null)return existing;
        var root=new GameObject("Terrari Showroom Wall Set");var bounds=stage.bounds;
        root.transform.position=new Vector3(bounds.center.x,bounds.max.y+.018f,bounds.center.z);
        front.y=0;if(front.sqrMagnitude<.01f)front=Vector3.back;
        root.transform.rotation=Quaternion.LookRotation(front.normalized,Vector3.up);
        float width=Mathf.Abs(front.normalized.z)*bounds.size.x+Mathf.Abs(front.normalized.x)*bounds.size.z;
        float depth=Mathf.Abs(front.normalized.x)*bounds.size.x+Mathf.Abs(front.normalized.z)*bounds.size.z;
        var showroom=root.AddComponent<LamborminiShowroom>();
        showroom.Build(Mathf.Max(width,4),Mathf.Max(depth,4));
        return showroom;
    }
    private Material Make(string name,Color color,float gloss,float metal,bool glow=false)
    {
        var material=new Material(Shader.Find("Standard")){name=name,color=color};
        material.SetFloat("_Glossiness",gloss);material.SetFloat("_Metallic",metal);
        if(glow){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*2);}
        materials.Add(material);return material;
    }
    private void Build(float width,float depth)
    {
        var floor=Make("Polished charcoal floor",new Color(.065f,.068f,.075f),.9f,.38f);
        var wall=Make("Charcoal backdrop",new Color(.022f,.025f,.032f),.3f,.1f);
        backdropMaterial=wall;
        var strip=Make("Showroom light strips",new Color(.85f,.92f,1),.5f,.15f,true);
        Box("Glossy platform",Vector3.zero,new Vector3(width,.025f,depth),floor);
        Box("Rear backdrop",new Vector3(0,1.7f,-depth*.46f),new Vector3(width,3.4f,.08f),wall);
        for(int side=-1;side<=1;side+=2)
        {
            Box("Side backdrop",new Vector3(side*width*.48f,1.7f,-depth*.22f),new Vector3(.08f,3.4f,depth*.5f),wall);
            Box("Vertical accent",new Vector3(side*width*.42f,1.7f,-depth*.449f),new Vector3(.045f,2.6f,.02f),strip);
            Box("Overhead accent",new Vector3(side*width*.26f,3.2f,-depth*.18f),new Vector3(.1f,.04f,depth*.56f),strip);
        }
        var p=new GameObject("Showroom reflection");p.transform.SetParent(transform,false);p.transform.localPosition=new Vector3(0,1,0);
        var probe=p.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
        probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.size=new Vector3(width,5,depth);probe.boxProjection=true;
        probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.03f,.035f,.04f);probe.intensity=.8f;
        probe.RenderProbe();
    }
    private void Box(string name,Vector3 position,Vector3 scale,Material material)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(transform,false);
        part.transform.localPosition=position;part.transform.localScale=scale;part.GetComponent<Renderer>().sharedMaterial=material;
        // The platform supports placed props; backdrop colliders allow tablet selection.
        if(name.Contains("accent")){var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}
    }
    private void OnDestroy(){foreach(var material in materials)if(material!=null)Destroy(material);}
}
