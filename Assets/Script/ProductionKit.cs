using UnityEngine;
using UnityEngine.InputSystem;
using Player.Equipment;
using Player.Manager;

// Purchased grip equipment uses the same inventory lifecycle as cameras and panels.
public sealed class ProductionKit : Equipment
{
    public int kind;
    public bool template;
    public bool placed;
    public int PowerChanges { get; private set; }
    public int ColorChanges { get; private set; }
    public int TiltChanges { get; private set; }
    private int colorChoice;
    public static readonly string[] Names={"LIGHT STRIP","OVERHEAD LIGHT BANK","BLACK FLAG + STAND","DIFFUSION FRAME","BOUNCE BOARD","TRACK DOLLY","EXPOSURE MONITOR"};
    public static readonly int[] Prices={900,1800,350,600,250,1600,500};
    public static readonly string[] Lessons={
        "Accent light: place beside or behind the car. It supplements the main light.",
        "Broad overhead illumination: keep the supported bank above the car and stands outside frame.",
        "Flag: put opaque cloth between a lamp and unwanted spill. It does not emit light.",
        "Diffusion: face the frame toward a nearby lamp, within 3m. It softens shadows and loses output in this approximation.",
        "Bounce: point the white face toward a nearby lamp to return weaker fill. It needs incident light.",
        "Dolly: put the track on a clear floor. Hold the camera within 2m and press J to mount/release; K runs an 8-second move.","Image check: after purchase, press M in the camera viewfinder for a luminance histogram. Check that product highlights and shadows retain detail."};
    GameObject visual;
    float heading;
    bool held;
    bool[] lightStates;
    void PackLights(bool pack){var ls=GetComponentsInChildren<Light>(true);if(pack){lightStates=new bool[ls.Length];for(int i=0;i<ls.Length;i++){lightStates[i]=ls[i].enabled;ls[i].enabled=false;}}else if(lightStates!=null){for(int i=0;i<ls.Length&&i<lightStates.Length;i++)ls[i].enabled=lightStates[i];}}
    Material surface;
    public Light bounce;
    public Transform carriage;
    Transform mounted;
    Transform originalCameraParent;
    Vector3 originalCameraPosition;
    bool moving;
    float travel;
    public static GameObject Template(int type)
    {
        var obj=new GameObject(Names[type]);obj.SetActive(false);
        var kit=obj.AddComponent<ProductionKit>();kit.kind=type;kit.template=true;
        return obj;
    }
    public void ActivateDelivery()
    {
        template=false;gameObject.SetActive(true);EnsureBuilt();PackLights(true);
        transform.localScale=Vector3.one*.18f; // Packed representation until deployed.
        EquipmentControls="[E] Pick up | [G] Deploy";
    }
    void EnsureBuilt()
    {
        if(visual!=null)return;
        EquipmentName=Names[kind];GetComponent<Rigidbody>().isKinematic=true;GetComponent<Rigidbody>().useGravity=false;
        visual=kind==0?StageLightStrip.Create():kind<=2?AutomotiveGrip.Create(kind==1):new GameObject(Names[kind]+" model");
        visual.transform.SetParent(transform,false);
        if(kind>=3)
        {
            surface=new Material(Shader.Find("Standard")){color=kind==3?new Color(.75f,.78f,.8f):Color.white};
            if(kind==6){Part("Monitor screen",new Vector3(0,1.1f,0),new Vector3(.65f,.4f,.05f));Part("Monitor stand",new Vector3(0,.55f,0),new Vector3(.06f,1.1f,.06f));Part("Base",new Vector3(0,.04f,0),new Vector3(.5f,.08f,.35f));}
            else if(kind==5)
            {
                Part("Left track",new Vector3(0,.04f,-.35f),new Vector3(3,.08f,.06f));
                Part("Right track",new Vector3(0,.04f,.35f),new Vector3(3,.08f,.06f));
                carriage=Part("Carriage",new Vector3(-1,.15f,0),new Vector3(.8f,.18f,.85f)).transform;
            }
            else
            {
                Part("Frame",new Vector3(0,1.4f,0),new Vector3(1.6f,1.6f,.025f));
                Part("Stand",new Vector3(0,.7f,0),new Vector3(.06f,1.4f,.06f));
                Part("Weighted base",new Vector3(0,.05f,0),new Vector3(.8f,.1f,.6f));
                if(kind==3)foreach(var r in visual.GetComponentsInChildren<Renderer>())r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                if(kind==4)
                {
                    var emitter=new GameObject("Approximate bounced fill");emitter.transform.SetParent(transform,false);emitter.transform.localPosition=new Vector3(0,1.4f,.08f);
                    bounce=emitter.AddComponent<Light>();bounce.type=LightType.Spot;bounce.spotAngle=120;bounce.range=6;bounce.intensity=0;bounce.shadows=LightShadows.Soft;
                }
            }
            var col=visual.AddComponent<BoxCollider>();col.center=kind==5?new Vector3(0,.15f,0):new Vector3(0,1.1f,0);col.size=kind==5?new Vector3(3,.3f,.85f):new Vector3(1.6f,2.2f,.6f);
        }
        foreach(var c in visual.GetComponentsInChildren<Collider>())if(c.enabled)c.isTrigger=true;
        EquipmentControls="[LMB] Power | [Q/E] Turn | [R] Strip tilt | [G] Deploy";
    }
    GameObject Part(string label,Vector3 pos,Vector3 scale)
    {
        var p=GameObject.CreatePrimitive(PrimitiveType.Cube);p.name=label;p.transform.SetParent(visual.transform,false);p.transform.localPosition=pos;p.transform.localScale=scale;p.GetComponent<Renderer>().sharedMaterial=surface;p.GetComponent<Collider>().enabled=false;return p;
    }
    public override void OnPickedUp(Transform hold)
    {
        EnsureBuilt();if(placed)PackLights(true);placed=false;held=true;ReleaseCamera();transform.localScale=Vector3.one;
        base.OnPickedUp(hold);transform.localScale=Vector3.one*.15f;transform.localPosition=new Vector3(.5f,-.4f,1);
        EquipmentControls=Lessons[kind]+"  [Q] Turn [C] Color [LMB] Power [R] Tilt [G] Deploy";
    }
    public override void OnDropped(Camera camera)
    {
        base.OnDropped(camera);held=false;transform.localScale=Vector3.one;transform.rotation=Quaternion.Euler(0,heading,0);visual.transform.localRotation=Quaternion.identity;
        Vector3 point=camera.transform.position+Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized*(kind==1?3f:2f);
        float closest=float.PositiveInfinity;
        Physics.SyncTransforms();
        foreach(var hit in Physics.RaycastAll(point+Vector3.up*2,Vector3.down,12))
        {
            if(hit.transform.IsChildOf(transform)||hit.transform.IsChildOf(camera.transform.root)||hit.normal.y<.8f||hit.collider.isTrigger||hit.transform.GetComponentInParent<Equipment>()!=null||hit.transform.GetComponentInParent<AutomotiveGrip>()!=null||hit.transform.GetComponentInParent<StageLightStrip>()!=null)continue;
            if(hit.distance<closest){closest=hit.distance;point=hit.point;}
        }
        // Respect raised stages rather than assuming the studio floor is world Y=0.
        if(float.IsPositiveInfinity(closest)) point.y=camera.transform.root.position.y;
        transform.position=point; placed=true; PackLights(false);
    }
    public override void OnUse(Camera camera)
    {
        PackLights(false);
        if(kind==0){visual.GetComponent<StageLightStrip>().CyclePower();PowerChanges++;}
        if(kind==1)visual.GetComponent<AutomotiveGrip>().CyclePower();
        PackLights(true);
    }
    public override void OnHeldUpdate(InputManager input)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        if(keyboard.qKey.wasPressedThisFrame)heading-=15;
        if(keyboard.eKey.wasPressedThisFrame)heading+=15;
        if(kind==0&&keyboard.rKey.wasPressedThisFrame){visual.GetComponent<StageLightStrip>().CycleTilt();TiltChanges++;}
        if(kind==0&&keyboard.cKey.wasPressedThisFrame){PackLights(false);colorChoice=(colorChoice+1)%3;visual.GetComponent<StageLightStrip>().SetColor(colorChoice==1?new Color(.4f,.8f,1):colorChoice==2?new Color(1,.65f,.35f):Color.white);ColorChanges++;PackLights(true);}
        visual.transform.localRotation=Quaternion.Euler(0,heading,0);
    }
    void Update()
    {
        if(template||held||!placed||kind!=5)return;
        if(mounted!=null && mounted.parent!=carriage){mounted=null;moving=false;}
        if(moving&&mounted!=null&&mounted.gameObject.activeInHierarchy&&!PauseManager.isPaused){travel=Mathf.Min(1,travel+Time.deltaTime/8);carriage.localPosition=new Vector3(Mathf.Lerp(-1,1,travel),.15f,0);if(travel>=1)moving=false;}
    }
    public static bool MountCamera(FilmCameraItem camera)
    {
        foreach(var kit in FindObjectsOfType<ProductionKit>())
        {
            if(kit.mounted==camera.transform){kit.ReleaseCamera();return true;}
        }
        ProductionKit nearest=null;float distance=2;
        foreach(var kit in FindObjectsOfType<ProductionKit>())if(kit.kind==5&&kit.placed){float d=Vector3.Distance(camera.transform.position,kit.transform.position);if(d<distance){distance=d;nearest=kit;}}
        if(nearest==null)return false;
        nearest.originalCameraParent=camera.transform.parent;nearest.originalCameraPosition=camera.transform.localPosition;nearest.mounted=camera.transform;camera.transform.SetParent(nearest.carriage,true);nearest.travel=0;
        camera.transform.position=nearest.carriage.position+Vector3.up*1.25f;return true;
    }
    public static void RunDolly(FilmCameraItem camera)
    {
        foreach(var kit in FindObjectsOfType<ProductionKit>())if(kit.mounted==camera.transform){if(kit.travel>=1){kit.travel=0;kit.carriage.localPosition=new Vector3(-1,.15f,0);}kit.moving=!kit.moving;}
    }
    public static void DetachCamera(FilmCameraItem camera)
    {
        foreach(var kit in FindObjectsOfType<ProductionKit>())if(kit.mounted==camera.transform)kit.ReleaseCamera();
    }
    void ReleaseCamera(){if(mounted!=null){mounted.SetParent(originalCameraParent,true);if(originalCameraParent!=null)mounted.localPosition=originalCameraPosition;mounted=null;}moving=false;}
    void OnDestroy(){ReleaseCamera();if(surface!=null)Destroy(surface);}
}




