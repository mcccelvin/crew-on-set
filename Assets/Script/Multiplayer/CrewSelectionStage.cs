using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// A real perspective-lit studio for role selection, isolated from gameplay.
// It presents the equipped character; a role never substitutes another model.
public sealed class CrewSelectionStage : MonoBehaviour
{
    private const int StageLayer = 30;
    private readonly List<Material> materials = new List<Material>();
    private CCoinService cosmetics;
    private GameObject model;
    private PlayableGraph idleGraph;
    private AnimationClipPlayable idle;
    private float idleLength, yaw = -12, footAnchor;
    private Transform leftFoot, rightFoot;
    private string outfitKey;
    private Material wood, metal, gold;

    private void Awake()
    {
        transform.position = new Vector3(0,-2000,0);
        wood = Surface("Stage wood",new Color32(67,43,30,255));
        metal = Surface("Studio charcoal",new Color32(29,26,24,255));
        gold = Surface("Platform brass",new Color32(165,111,48,255));
        var red = Surface("Theatre velvet",new Color32(87,22,19,255));
        var redFold = Surface("Theatre velvet folds",new Color32(63,16,16,255));
        Shape("Studio floor",PrimitiveType.Cube,new Vector3(0,-.18f,0),new Vector3(28,.2f,24),wood);
        Shape("Brass platform rim",PrimitiveType.Cylinder,new Vector3(0,.045f,0),new Vector3(3.05f,.065f,2.3f),gold);
        Shape("Character platform",PrimitiveType.Cylinder,new Vector3(0,.14f,0),new Vector3(2.94f,.095f,2.2f),metal);
        for(int i=0;i<26;i++)
            Shape("Curtain fold "+i,PrimitiveType.Cylinder,new Vector3(-13+i,4,-5.3f+(i%2)*.15f),new Vector3(1.24f,4.5f,.65f),i%2==0?red:redFold);
        BuildLightStand(-4.6f); BuildLightStand(4.6f);
        var cameraObject = new GameObject("Selection perspective camera",typeof(Camera));
        cameraObject.transform.SetParent(transform,false);
        cameraObject.transform.localPosition=new Vector3(0,2.7f,8.9f);
        cameraObject.transform.LookAt(transform.position+new Vector3(0,1.85f,0));
        var camera=cameraObject.GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color32(35,23,22,255);
        camera.cullingMask=1<<StageLayer; camera.fieldOfView=35; camera.nearClipPlane=.1f; camera.farClipPlane=45; camera.depth=100;
        LightAt("Soft key",LightType.Spot,new Vector3(-3,6,5),new Vector3(0,1.6f,0),new Color(1,.89f,.73f),5,true);
        LightAt("Soft fill",LightType.Point,new Vector3(3,3,4),new Vector3(0,1.6f,0),new Color(.83f,.86f,1),2,false);
        LightAt("Gold rim",LightType.Spot,new Vector3(1.5f,5,-3),new Vector3(0,1.7f,0),new Color(1,.64f,.29f),5,false);
        cosmetics=CCoinService.Ensure(); cosmetics.Changed+=RefreshAppearance;
        RefreshAppearance();
    }

    private Material Surface(string name,Color color,bool glowing=false)
    {
        var shader=Shader.Find("Standard")??Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Sprites/Default");
        var material=new Material(shader){name=name,color=color};
        if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.22f);
        if(glowing){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*1.7f);}
        materials.Add(material); return material;
    }
    private GameObject Shape(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
    {
        var item=GameObject.CreatePrimitive(shape); item.name=name; item.layer=StageLayer;
        item.transform.SetParent(transform,false);item.transform.localPosition=position;item.transform.localScale=scale;
        item.GetComponent<Renderer>().sharedMaterial=material;
        var collider=item.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Destroy(collider);}
        return item;
    }
    private void BuildLightStand(float side)
    {
        Shape("Spotlight base",PrimitiveType.Cylinder,new Vector3(side,.02f,-1),new Vector3(.7f,.04f,.7f),metal);
        Shape("Spotlight stand",PrimitiveType.Cylinder,new Vector3(side,1.65f,-1),new Vector3(.07f,1.65f,.07f),metal);
        var housing=Shape("Studio spotlight",PrimitiveType.Cube,new Vector3(side,3.45f,-1),new Vector3(1,.8f,.5f),metal);
        housing.transform.localRotation=Quaternion.Euler(12,side<0?-25:25,0);
        var lens=Shape("Warm spotlight lens",PrimitiveType.Cube,Vector3.zero,new Vector3(.8f,.6f,.03f),Surface("Spotlight glow",new Color(1,.84f,.46f),true));
        lens.transform.SetParent(housing.transform,false); lens.transform.localPosition=new Vector3(0,0,.52f);
        lens.transform.localScale=new Vector3(.8f,.75f,.05f);
    }
    private void LightAt(string name,LightType type,Vector3 position,Vector3 aim,Color color,float intensity,bool shadows)
    {
        var source=new GameObject(name,typeof(Light));source.transform.SetParent(transform,false);
        source.transform.localPosition=position;source.transform.LookAt(transform.position+aim);
        var light=source.GetComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;
        light.range=22;light.spotAngle=65;light.cullingMask=1<<StageLayer;
        light.shadows=shadows?LightShadows.Soft:LightShadows.None;light.shadowStrength=.7f;light.shadowBias=.03f;
    }

    private void RefreshAppearance()
    {
        if(cosmetics==null)return;
        var parts=cosmetics.EquippedParts;string key=string.Join("|",parts);
        if(model!=null && key==outfitKey)return;
        if(idleGraph.IsValid())idleGraph.Destroy();
        if(model!=null){model.SetActive(false);Destroy(model);}
        leftFoot=rightFoot=null;
        var catalog=Resources.Load<ProductModelCatalog>("ProductModels");GameObject source=null;
        if(catalog!=null && catalog.coffeeActorModels!=null)
            foreach(var candidate in catalog.coffeeActorModels)
                if(candidate!=null && candidate.name=="DefaultCharacGirlRig"){source=candidate;break;}
        if(source==null){Debug.LogWarning("The crew selection character model is unavailable.");return;}
        model=Instantiate(source,transform);model.name="Your customized 3D character";
        model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.Euler(0,yaw,0);
        foreach(var component in model.GetComponentsInChildren<MonoBehaviour>(true))component.enabled=false;
        foreach(var body in model.GetComponentsInChildren<Rigidbody>(true))body.isKinematic=true;
        foreach(var collider in model.GetComponentsInChildren<Collider>(true))collider.enabled=false;
        foreach(var camera in model.GetComponentsInChildren<Camera>(true))camera.enabled=false;
        foreach(var light in model.GetComponentsInChildren<Light>(true))light.enabled=false;
        if(System.Array.Exists(parts,id=>CharacterCosmetics.Find(id)?.kind=="body"))CharacterCosmeticRig.Load()?.Apply(model,parts);
        Animator animator=null;
        foreach(var candidate in model.GetComponentsInChildren<Animator>(true))
        {
            candidate.enabled=false;
            if(animator==null && candidate.avatar!=null && candidate.avatar.isValid && candidate.avatar.isHuman)animator=candidate;
        }
        if(animator!=null)
        {
            animator.enabled=true;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var clip in Resources.LoadAll<AnimationClip>("Character/Animations/Stand--Idle.anim"))
            {
                if(clip.name.StartsWith("__preview__") || !clip.humanMotion)continue;
                idleGraph=PlayableGraph.Create("Crew selection idle");idleGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                idle=AnimationClipPlayable.Create(idleGraph,clip);idleLength=Mathf.Max(.01f,clip.length);
                AnimationPlayableOutput.Create(idleGraph,"Selection character",animator).SetSourcePlayable(idle);
                idleGraph.Play();idleGraph.Evaluate(0);break;
            }
            if(!idleGraph.IsValid())
                foreach(var bone in new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm})
                {
                    var arm=animator.GetBoneTransform(bone);
                    var elbow=animator.GetBoneTransform(bone==HumanBodyBones.LeftUpperArm?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                    if(arm!=null && elbow!=null)arm.rotation=Quaternion.FromToRotation(elbow.position-arm.position,Vector3.down)*arm.rotation;
                }
            leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot);
        }
        foreach(var node in model.GetComponentsInChildren<Transform>(true))node.gameObject.layer=StageLayer;
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))skin.updateWhenOffscreen=true;
        var bounds=ModelBounds();model.transform.localScale*=3.2f/Mathf.Max(.01f,bounds.size.y);
        bounds=ModelBounds();model.transform.position+=Vector3.up*(transform.position.y+.235f-bounds.min.y);
        footAnchor=FeetHeight();outfitKey=key;
    }
    private Bounds ModelBounds()
    {
        Bounds bounds=default;bool found=false;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            if(!renderer.enabled || !renderer.gameObject.activeInHierarchy)continue;
            if(!found){bounds=renderer.bounds;found=true;}else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }
    private float FeetHeight()=>leftFoot!=null && rightFoot!=null?Mathf.Min(leftFoot.position.y,rightFoot.position.y):model.transform.position.y;
    private void LateUpdate()
    {
        if(model==null || !idleGraph.IsValid())return;
        idle.SetTime(Time.unscaledTime%idleLength);idleGraph.Evaluate(0);
        model.transform.position+=Vector3.up*(footAnchor-FeetHeight());
    }
    public void Rotate(float degrees)
    {
        yaw+=degrees;if(model!=null)model.transform.localRotation=Quaternion.Euler(0,yaw,0);
    }
    private void OnDestroy()
    {
        if(cosmetics!=null)cosmetics.Changed-=RefreshAppearance;
        if(idleGraph.IsValid())idleGraph.Destroy();
        foreach(var material in materials)if(material!=null)Destroy(material);
    }
}
