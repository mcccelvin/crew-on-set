using TMPro;
using UnityEngine;

public sealed partial class StudioDeliveryBay
{
    private Material Paint(string name,Color color,float gloss=.15f)
    {
        var shader=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline==null?Shader.Find("Standard"):
            Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var material=new Material(shader){name="Delivery / "+name,color=color};
        material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",gloss);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",gloss);
        materials.Add(material);return material;
    }

    private GameObject Part(Transform parent,string name,Vector3 center,Vector3 size,Material paint,bool solid=true,PrimitiveType shape=PrimitiveType.Cube)
    {
        var part=GameObject.CreatePrimitive(shape);part.name=name;part.transform.SetParent(parent,false);
        part.transform.localPosition=center;part.transform.localScale=size;
        part.GetComponent<Renderer>().sharedMaterial=paint;
        if(!solid)
        {var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);}
        return part;
    }

    private void Sign(Transform parent,string text,Vector3 position,Quaternion rotation,Vector2 size,float fontSize)
    {
        var sign=new GameObject(text,typeof(TextMeshPro));sign.transform.SetParent(parent,false);
        sign.transform.localPosition=position;sign.transform.localRotation=rotation;
        var label=sign.GetComponent<TextMeshPro>();label.text=text;label.fontSize=fontSize;
        label.fontStyle=FontStyles.Bold;label.color=Color.white;label.alignment=TextAlignmentOptions.Center;
        label.enableAutoSizing=true;label.fontSizeMin=fontSize*.6f;label.fontSizeMax=fontSize;
        label.rectTransform.sizeDelta=size;
    }

    private void BuildGarageTruck()
    {
        var teal=Paint("Studio teal",new Color32(20,70,83,255));
        var cream=Paint("Truck body",new Color32(233,228,205,255));
        var trim=Paint("Dark trim",new Color32(24,31,37,255));
        var metal=Paint("Loading shelf",new Color32(133,146,150,255),.3f);
        var glass=Paint("Cab glass",new Color32(51,93,114,255),.65f);
        var yellow=Paint("Safety yellow",new Color32(248,184,48,255));
        var red=Paint("Tail lamps",new Color32(205,36,29,255));
        var white=Paint("Head lamps",new Color32(255,246,201,255));
        var truck=new GameObject("Crew equipment delivery truck").transform;
        truck.SetParent(frame,false);truck.localPosition=truckParking;
        truck.localRotation=Quaternion.LookRotation(truckForward,Vector3.up);
        truck.localScale=Vector3.one*truckScale;
        Part(truck,"Chassis",new Vector3(0,.65f,0),new Vector3(2.25f,.26f,5.7f),trim);
        Part(truck,"Cargo floor",new Vector3(0,1.02f,-.77f),new Vector3(2.4f,.16f,3.75f),metal);
        Part(truck,"Cargo left wall",new Vector3(-1.2f,2.04f,-.77f),new Vector3(.12f,2.05f,3.75f),cream);
        Part(truck,"Cargo right wall",new Vector3(1.2f,2.04f,-.77f),new Vector3(.12f,2.05f,3.75f),cream);
        Part(truck,"Cargo front wall",new Vector3(0,2.04f,1.1f),new Vector3(2.4f,2.05f,.12f),cream);
        Part(truck,"Cargo roof",new Vector3(0,3.09f,-.77f),new Vector3(2.55f,.14f,3.9f),cream);
        // Rear stays open. The lowered liftgate is the actual order surface.
        // Keep the hydraulic liftgate at one metre even on the larger truck.
        float shelfCenter=1f/truckScale-.07f;
        float supportHeight=shelfCenter-.07f;
        accessHalfWidth=Mathf.Min(.7f/truckScale,.5f);
        // Preserve the original delivery anchor without a full-width plate
        // underneath the ramp. The two pickup decks meet the walkway at edges.
        pickupSurface=new GameObject("Rear pickup shelf").transform;
        pickupSurface.SetParent(truck,false);pickupSurface.localPosition=new Vector3(0,shelfCenter,-3.65f);
        pickupSurface.localScale=new Vector3(2.55f,.14f,1.5f);
        float deckWidth=1.275f-accessHalfWidth;
        float deckCenter=(1.275f+accessHalfWidth)*.5f;
        for(int side=-1;side<=1;side+=2)
        {
            // Metal stops before the yellow nose: no coplanar overlapping tops.
            Part(truck,"Side pickup deck",new Vector3(side*deckCenter,shelfCenter,-3.63f),new Vector3(deckWidth,.14f,1.46f),metal);
            Part(truck,"Pickup deck bright edge",new Vector3(side*deckCenter,shelfCenter,-4.38f),new Vector3(deckWidth,.14f,.04f),yellow);
            foreach(float z in new[]{-4.12f,-3.18f})
                Part(truck,"Liftgate support post",new Vector3(side*1.02f,supportHeight*.5f,z),new Vector3(.12f,supportHeight,.12f),trim);
        }
        BuildTruckWalkway(truck,metal,trim,yellow);
        Part(truck,"Rear bumper",new Vector3(0,.54f,-2.7f),new Vector3(2.6f,.18f,.18f),trim);
        Part(truck,"Cab body",new Vector3(0,1.31f,2.03f),new Vector3(2.35f,1.42f,1.85f),teal);
        Part(truck,"Cab roof",new Vector3(0,2.47f,1.92f),new Vector3(2.4f,.16f,1.72f),teal);
        Part(truck,"Windscreen",new Vector3(0,2.13f,2.82f),new Vector3(1.97f,.60f,.07f),glass,false)
            .transform.localRotation=Quaternion.Euler(-12,0,0);
        for(int side=-1;side<=1;side+=2)
        {
            Part(truck,"Cab side glass",new Vector3(side*1.18f,2.11f,1.98f),new Vector3(.04f,.58f,1.19f),glass,false);
            Part(truck,"Mirror",new Vector3(side*1.39f,2.03f,2.51f),new Vector3(.22f,.3f,.14f),trim);
            Part(truck,"Cab handle",new Vector3(side*1.2f,1.55f,1.64f),new Vector3(.06f,.08f,.27f),metal,false);
            Part(truck,"Tail lamp",new Vector3(side*.96f,1.01f,-2.69f),new Vector3(.25f,.3f,.07f),red,false);
            Part(truck,"Headlamp",new Vector3(side*.78f,1.15f,2.98f),new Vector3(.43f,.28f,.06f),white,false);
            Part(truck,"Side brand panel",new Vector3(side*1.27f,2.08f,-.77f),new Vector3(.04f,1.0f,2.6f),teal,false);
            Sign(truck,"CREW\nDELIVERY",new Vector3(side*1.30f,2.12f,-.77f),Quaternion.Euler(0,-side*90,0),new Vector2(2.4f,.8f),3.4f);
            foreach(float axle in new[]{-1.82f,1.88f})
            {
                var tire=Part(truck,"Low-poly tire",new Vector3(side*1.13f,.48f,axle),new Vector3(.96f,.16f,.96f),trim,false,PrimitiveType.Cylinder);
                tire.transform.localRotation=Quaternion.Euler(0,0,90);
                var hub=Part(truck,"Wheel hub",new Vector3(side*1.30f,.48f,axle),new Vector3(.5f,.025f,.5f),metal,false,PrimitiveType.Cylinder);
                hub.transform.localRotation=Quaternion.Euler(0,0,90);
            }
        }
        Part(truck,"Front bumper",new Vector3(0,.75f,3.01f),new Vector3(2.5f,.25f,.16f),metal);
        Part(truck,"Front grille",new Vector3(0,1.15f,3.01f),new Vector3(.78f,.34f,.06f),trim,false);
        Sign(truck,"COLLECT YOUR ORDER",new Vector3(0,2.9f,-2.75f),Quaternion.identity,new Vector2(2.2f,.32f),1.8f);
        var lamp=new GameObject("Delivery work light",typeof(Light)).GetComponent<Light>();
        lamp.transform.SetParent(frame,false);
        lamp.transform.localPosition=truckParking-truckForward*(3*truckScale)+Vector3.up*Mathf.Min(height-.2f,4.5f);
        lamp.transform.localRotation=Quaternion.LookRotation(truckForward,Vector3.up)*Quaternion.Euler(80,0,0);
        lamp.type=LightType.Spot;lamp.spotAngle=100;
        lamp.range=11;lamp.intensity=2.8f;lamp.color=new Color(1,.95f,.82f);lamp.shadows=LightShadows.Soft;
    }
}
