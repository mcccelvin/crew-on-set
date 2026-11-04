using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Gear = Player.Equipment.Equipment;

// Holds existing paid equipment; putting gear away never creates a purchase.
[DefaultExecutionOrder(-50)]
public sealed partial class EquipmentStorageCabinet : MonoBehaviour, IInteractable
{
    public static EquipmentStorageCabinet Active { get; private set; }
    public static bool IsOpen => Active != null && Active.menu != null && Active.menu.activeSelf;
    private static int closedFrame = -1;
    public static bool BlocksInputThisFrame => IsOpen || closedFrame == Time.frameCount;
    private readonly List<Gear> stored = new List<Gear>();
    private readonly List<Material> paints = new List<Material>();
    private Player.Interactor.EquipmentInteractor inventory;
    private GameObject visitor;
    private BoxCollider cabinetCollider;
    private bool previousVisible, previousRoles, openedInRoom;
    private CursorLockMode previousLock;
    private int roomRevision = -1;
    private bool Room => PhotonNetwork.InRoom && GameSavePrefs.IsRoomSession;

    public static void Install(Scene scene, Transform host)
    {
        if (Active != null && Active.gameObject.scene == scene) return;
        var roots = scene.GetRootGameObjects();
        var tables = roots.SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r=>r.gameObject.activeInHierarchy && r.enabled &&
                r.name.StartsWith("kiosk table",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(tables.Length==0)
        { Debug.LogWarning("Equipment storage: the shop-side table was not found."); return; }
        var table=tables[0];var bounds=table.bounds;
        foreach(var part in tables.Where(t=>(t.bounds.center-table.bounds.center).sqrMagnitude<1))bounds.Encapsulate(part.bounds);
        // Keep the drawers parallel to the authored table's long edge. Choosing
        // the dominant direction to the stage rotates this corner cabinet into
        // the wall because the stage is mostly sideways from the shop.
        var stage=StageInterior.FindStagePlatform();
        Vector3 inward=stage!=null ? stage.bounds.center-bounds.center : -bounds.center;
        bool wideAlongX=bounds.size.x>=bounds.size.z;
        Vector3 forward=wideAlongX?Vector3.forward:Vector3.right;
        if(Vector3.Dot(forward,inward)<0)forward=-forward;
        float tableHalfDepth=wideAlongX?bounds.extents.z:bounds.extents.x;
        // Reuse the table's wall-side edge, with the cabinet back 8cm clear.
        Vector3 position=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)
            -forward*tableHalfDepth+forward*(.475f+.08f);
        float floorHeight=float.NegativeInfinity;
        foreach(var hit in Physics.RaycastAll(position+Vector3.up,Vector3.down,2,~0,QueryTriggerInteraction.Ignore))
            if(hit.normal.y>.9f && hit.point.y<=bounds.min.y+.05f && hit.point.y>=bounds.min.y-.25f &&
                !hit.collider.name.StartsWith("kiosk table",StringComparison.OrdinalIgnoreCase))
                floorHeight=Mathf.Max(floorHeight,hit.point.y);
        if(!float.IsNegativeInfinity(floorHeight))position.y=floorHeight;
        var root=new GameObject("Shop equipment storage");root.transform.SetParent(host,false);
        root.transform.SetPositionAndRotation(position,Quaternion.LookRotation(forward));
        var cabinet=root.AddComponent<EquipmentStorageCabinet>();
        Active=cabinet;
        cabinet.BuildCabinet(Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z),2.8f,4));
        // Hide only the old table's own mesh parts, preserving shop canvas and station references.
        foreach(var part in tables.Where(t=>(t.bounds.center-table.bounds.center).sqrMagnitude<1))part.gameObject.SetActive(false);
        Physics.SyncTransforms();
    }

    private Material Finish(string title,Color color)
    {
        var shader=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline==null ? Shader.Find("Standard") :
            Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
        var material=new Material(shader){name="Equipment cabinet / "+title,color=color};
        if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
        if(material.HasProperty("_Glossiness"))material.SetFloat("_Glossiness",.25f);
        paints.Add(material);return material;
    }

    private void CabinetPart(string title,Vector3 point,Vector3 size,Material paint)
    {
        var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=title;
        part.transform.SetParent(transform,false);part.transform.localPosition=point;part.transform.localScale=size;
        part.GetComponent<Renderer>().sharedMaterial=paint;
        var collider=part.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
    }

    private void BuildCabinet(float width)
    {
        var teal=Finish("Painted steel",new Color32(20,70,83,255));
        var cream=Finish("Drawer fronts",new Color32(233,228,205,255));
        var dark=Finish("Recesses",new Color32(24,31,37,255));
        var metal=Finish("Handles",new Color32(151,164,169,255));
        CabinetPart("Floor plinth",new Vector3(0,.04f,0),new Vector3(width-.1f,.08f,.8f),dark);
        CabinetPart("Cabinet body",new Vector3(0,1.13f,0),new Vector3(width,2.18f,.85f),teal);
        CabinetPart("Raised top rim",new Vector3(0,2.26f,0),new Vector3(width+.1f,.12f,.95f),metal);
        CabinetPart("Dark drawer recess",new Vector3(0,1.05f,.44f),new Vector3(width-.16f,1.88f,.05f),dark);
        float drawerWidth=(width-.26f)*.5f;
        for(int row=0;row<3;row++)for(int side=-1;side<=1;side+=2)
        {
            float x=side*(drawerWidth+.06f)*.5f,y=.4f+row*.59f;
            CabinetPart("Equipment drawer",new Vector3(x,y,.48f),new Vector3(drawerWidth,.54f,.08f),cream);
            CabinetPart("Recessed handle",new Vector3(x,y+.12f,.535f),new Vector3(drawerWidth*.48f,.12f,.035f),dark);
            CabinetPart("Steel pull",new Vector3(x,y+.11f,.57f),new Vector3(drawerWidth*.4f,.045f,.06f),metal);
        }
        cabinetCollider=gameObject.AddComponent<BoxCollider>();cabinetCollider.center=new Vector3(0,1.15f,0);
        cabinetCollider.size=new Vector3(width,2.3f,.96f);
    }

    public bool CanReach(Vector3 feet) => cabinetCollider!=null &&
        cabinetCollider.bounds.SqrDistance(feet+Vector3.up)<16;

    public void OnInteract(GameObject player)
    {
        if(IsOpen || player==null || !CanReach(player.transform.position))return;
        if(!Room && !TutorialEquipmentGate.Allow("storage",null,false) && !stored.Any(item=>item!=null && TutorialEquipmentGate.Allow("pickup",item,false)))
        {TutorialEquipmentGate.Remind();return;}
        visitor=player;
        inventory=player.GetComponentInParent<Player.Interactor.EquipmentInteractor>() ?? player.GetComponentInChildren<Player.Interactor.EquipmentInteractor>();
        if(!Room && inventory==null)return;
        if(Room && (MultiplayerRoleManager.Instance==null || MultiplayerRoleManager.Instance.LocalIsReadingBriefing))return;
        if(!Room && pending?.items!=null && pending.items.Count>0)RestoreContents();
        previousLock=Cursor.lockState;previousVisible=Cursor.visible;previousRoles=RoleSelectionUI.Open;
        openedInRoom=Room;
        EnsureMenu();menu.SetActive(true);roomRevision=-1;
        if(Room)RoleSelectionUI.Open=true;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;RefreshContents();
    }
    public void OnDrop() { }

    public void CloseStorage()
    {
        if(menu==null || !menu.activeSelf)return;
        menu.SetActive(false);closedFrame=Time.frameCount;
        if(openedInRoom)RoleSelectionUI.Open=previousRoles;
        Cursor.lockState=previousLock;Cursor.visible=previousVisible;visitor=null;inventory=null;
    }

    private void Update()
    {
        if(!IsOpen)return;
        if(visitor==null || !CanReach(visitor.transform.position) || openedInRoom && !Room || Room &&
            (MultiplayerRoleManager.Instance==null || MultiplayerRoleManager.Instance.State?.phase!="build" && MultiplayerRoleManager.Instance.State?.phase!="review"))
        {CloseStorage();return;}
        var keys=Keyboard.current;
        if(Application.isFocused && keys!=null && (keys.escapeKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame))
        {CloseStorage();return;}
        if(Room)
        {
            RoleSelectionUI.Open=true;
            var state=MultiplayerRoleManager.Instance.State;
            if(state!=null && state.revision!=roomRevision){roomRevision=state.revision;RefreshContents();}
        }
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }

    private void Store(Gear item)
    {
        if(inventory==null || !inventory.MoveToStorage(item))return;
        stored.Add(item);item.transform.SetParent(transform,true);item.gameObject.SetActive(false);
        SaveContents();RefreshContents();
    }
    private void Retrieve(Gear item)
    {
        if(item==null || inventory==null || !inventory.TakeFromStorage(item))return;
        stored.Remove(item);SaveContents();RefreshContents();
    }
    private void OnDestroy()
    {
        if(Active==this){CloseStorage();Active=null;}
        if(menu!=null)Destroy(menu);
        foreach(var paint in paints)if(paint!=null)Destroy(paint);
    }
}
