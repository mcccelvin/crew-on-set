using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// One physical loading bay for the shared studio, independent of career/room economy.
public sealed partial class StudioDeliveryBay : MonoBehaviour
{
    private static StudioDeliveryBay active;
    private Transform frame, zone, pickupSurface;
    private Bounds garageArea;
    private Vector3 truckParking;
    private Vector3 truckForward = Vector3.forward;
    private const float PreferredTruckScale = 1.8f;
    private const float FloorRampRun = 3.4f;
    private float truckScale = 1;
    private float accessHalfWidth;
    private int garageFloorLayer;
    private Bounds exteriorOpening;
    private bool hasExteriorOpening;
    private readonly List<Transform> leaves = new List<Transform>();
    private readonly List<Vector3> closed = new List<Vector3>(), travel = new List<Vector3>();
    private readonly List<Material> materials = new List<Material>();
    private readonly List<Mesh> meshes = new List<Mesh>();
    private Transform[] players = new Transform[0];
    private float width, height, open, desired, nextPlayers, closeAfter;
    private bool notified;
    public static string CollectionMessage => active != null
        ? "Purchase confirmed. Collect your order from the truck in the garage."
        : "Purchase confirmed. Collect your equipment from the delivery table.";

    public void Build(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var shop = roots.SelectMany(r=>r.GetComponentsInChildren<ShopTerminal>(true)).FirstOrDefault();
        if (shop == null || shop.deliveryZone == null) return;
        var doors = roots.SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true))
            .Where(r=>r.gameObject.activeInHierarchy && r.name.StartsWith("door_",StringComparison.OrdinalIgnoreCase)).ToArray();
        var left = doors.Where(r=>r.name.StartsWith("door_left",StringComparison.OrdinalIgnoreCase))
            .OrderBy(r=>(r.bounds.center-shop.deliveryZone.position).sqrMagnitude).FirstOrDefault();
        var right = left != null ? doors.Where(r=>r.name.StartsWith("door_right",StringComparison.OrdinalIgnoreCase))
            .OrderBy(r=>(r.bounds.center-left.bounds.center).sqrMagnitude).FirstOrDefault() : null;
        if (left == null || right == null) { Debug.LogWarning("Delivery bay: the studio's paired doors were not found."); return; }
        var building = left.transform.parent;
        var garageFloor = building.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r=>r.gameObject.activeInHierarchy && r.enabled &&
                r.name.Equals("Floor.026",StringComparison.OrdinalIgnoreCase) &&
                r.bounds.size.y<.2f && Mathf.Min(r.bounds.size.x,r.bounds.size.z)>4)
            .OrderBy(r=>(r.bounds.center-left.bounds.center).sqrMagnitude).FirstOrDefault();
        if (garageFloor == null)
        { Debug.LogWarning("Delivery bay: the authored garage floor was not found; keeping the original delivery point."); return; }
        var bounds = left.bounds; bounds.Encapsulate(right.bounds);
        var across = bounds.size.x > bounds.size.z ? Vector3.right : Vector3.forward;
        var outward = Vector3.Cross(across,Vector3.up);
        // The garage floor, rather than the full building bounds, identifies
        // which side of the sliding doors is the loading room.
        if (Vector3.Dot(outward,garageFloor.bounds.center-bounds.center)<0) outward = -outward;
        width = Mathf.Max(bounds.size.x,bounds.size.z);
        height = bounds.size.y;
        float ground = garageFloor.bounds.max.y;
        garageFloorLayer = garageFloor.gameObject.layer;
        frame = new GameObject("Garage delivery bay").transform;
        frame.SetParent(transform,false);
        frame.SetPositionAndRotation(new Vector3(bounds.center.x,ground,bounds.center.z),Quaternion.LookRotation(outward,Vector3.up));
        across = frame.right;
        garageArea = LocalBounds(garageFloor.bounds);
        float front = Mathf.Max(.35f,garageArea.min.z);
        float availableDepth = garageArea.max.z-front-FloorRampRun-1.8f;
        truckScale = Mathf.Min(1,availableDepth/7.6f,(garageArea.size.x-2.4f)/2.9f,(height-.35f)/3.2f);
        if (truckScale<.65f)
        { Debug.LogWarning("Delivery bay: insufficient garage clearance for the delivery truck."); Destroy(frame.gameObject); frame=null; return; }
        // Keep an aisle behind the liftgate and clearance from the back wall.
        truckParking = new Vector3(
            Mathf.Clamp(0,garageArea.min.x+1.2f+1.45f*truckScale,garageArea.max.x-1.2f-1.45f*truckScale),
            0,front+FloorRampRun+1.2f+4.45f*truckScale);
        ParkAtGarageOpening(building);
        // Keep the authored floor and walls intact: no duplicate ground slab
        // (z-fighting) and no runtime wall subtraction across the garage.
        foreach (var door in new[] {left,right})
        {
            leaves.Add(door.transform); closed.Add(door.transform.position);
            float sign = Vector3.Dot(door.bounds.center-bounds.center,across)<0?-1:1;
            travel.Add(across*sign*(width*.5f+.12f));
        }
        BuildGarageTruck();
        BuildExteriorView();
        zone = shop.deliveryZone;
        Quaternion itemRotation = zone.rotation;
        zone.SetParent(frame,true);
        zone.position = pickupSurface.TransformPoint(new Vector3(0,.5f,0))+Vector3.up*.02f;
        zone.rotation = itemRotation;
        foreach(var legacy in roots.SelectMany(r=>r.GetComponentsInChildren<ShopManager>(true)))legacy.deliveryZone=zone;
        active = this;
        foreach (var camera in roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)))
            if (camera.name == "Delivery ShowCase")
            {
                camera.transform.position = zone.position-pickupSurface.forward*2.5f+Vector3.up*1.4f;
                camera.transform.LookAt(zone.position);
            }
        Physics.SyncTransforms();
    }

    private void ParkAtGarageOpening(Transform building)
    {
        // These authored wall ends bound the existing exterior garage opening.
        // Their visible bounds include the imported mesh's baked vertex offsets.
        var walls = building.GetComponentsInChildren<MeshRenderer>(true);
        var leftJamb = walls.FirstOrDefault(r=>r.enabled && r.gameObject.activeInHierarchy && r.name=="studio.043");
        var rightJamb = walls.FirstOrDefault(r=>r.enabled && r.gameObject.activeInHierarchy && r.name=="studio.047");
        if(leftJamb==null || rightJamb==null)
        { Debug.LogWarning("Delivery bay: garage opening wall ends not found; keeping the truck inside."); return; }
        var a=LocalBounds(leftJamb.bounds);var b=LocalBounds(rightJamb.bounds);
        float openingWidth=b.min.z-a.max.z;
        float wall=a.max.x;
        // The pedestrian sliding-door height does not describe the much taller
        // exterior garage opening. Size the truck against the actual headers.
        float openingHeight=float.PositiveInfinity;
        foreach(var header in walls.Where(r=>r.enabled && r.gameObject.activeInHierarchy &&
            (r.name=="studio.001" || r.name=="studio.003")))
        {
            float underside=LocalBounds(header.bounds).min.y;
            if(underside>2.5f)openingHeight=Mathf.Min(openingHeight,underside);
        }
        if(float.IsPositiveInfinity(openingHeight))openingHeight=height;
        float scale=Mathf.Min(PreferredTruckScale,(openingWidth-.6f)/2.9f,
            (garageArea.max.x-wall-FloorRampRun-1.2f)/5.65f,(openingHeight-.35f)/3.2f);
        if(scale<.65f)
        { Debug.LogWarning("Delivery bay: insufficient clearance at the garage opening."); return; }
        truckScale=scale;
        truckForward=Vector3.left;
        // The cargo front ends at truck Z=1.16. Put that edge just inside
        // the wall plane, with the cab ahead of it through the existing opening.
        var opening=new Vector3(wall,0,(a.max.z+b.min.z)*.5f);
        truckParking=opening-truckForward*(1.18f*truckScale);
        exteriorOpening=new Bounds(opening+Vector3.up*(openingHeight*.5f),new Vector3(.25f,openingHeight,openingWidth));
        hasExteriorOpening=true;
    }

    private Bounds LocalBounds(Bounds world)
    {
        var local = new Bounds(frame.InverseTransformPoint(world.center),Vector3.zero);
        for (int i=0;i<8;i++) local.Encapsulate(frame.InverseTransformPoint(world.center+
            Vector3.Scale(world.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
        return local;
    }

    private void Update()
    {
        if (frame == null || Time.deltaTime<=0) return;
        if (Time.unscaledTime>=nextPlayers)
        {
            nextPlayers=Time.unscaledTime+1;
            players=GameObject.FindGameObjectsWithTag("Player").Where(p=>p.scene==gameObject.scene && p.activeInHierarchy).Select(p=>p.transform).ToArray();
        }
        bool nearby=Time.unscaledTime<guideDoorUntil, localNearby=false;
        foreach (var player in players)
        {
            if (player==null || !player.gameObject.activeInHierarchy) continue;
            var p=frame.InverseTransformPoint(player.position);
            if (Mathf.Abs(p.x)<width*.5f+.7f && Mathf.Abs(p.z)<3.2f && p.y>-1 && p.y<height+1)
            {
                nearby=true;
                var owner=player.GetComponent<Photon.Pun.PhotonView>();
                if(owner==null || owner.IsMine)localNearby=true;
            }
        }
        if (nearby)
        {
            closeAfter=Time.time+1.5f; desired=1;
            if (!notified && localNearby)
            {
                notified=true;
                GameFeedback.Show("Delivery truck in the garage • Collect orders from the rear shelf.",GameFeedback.NoticeType.Instruction);
            }
        }
        else if (Time.time>=closeAfter) desired=0;
        float next=Mathf.MoveTowards(open,desired,Time.deltaTime*1.8f);
        if (Mathf.Approximately(next,open)) return;
        open=next;
        float eased=Mathf.SmoothStep(0,1,open);
        for (int i=0;i<leaves.Count;i++) if(leaves[i]!=null)leaves[i].position=closed[i]+travel[i]*eased;
    }

    public static Vector3 Position(Transform delivery,int slot,float lift=.3f)
    {
        if (active!=null && active.zone==delivery)
        {
            // Reserve the centre lane for walking, including on a full delivery.
            float outer=1.095f;
            float inner=Mathf.Min(outer-.12f,active.accessHalfWidth+.3f);
            int column=slot%4;
            float x=column==0?-outer:column==1?-inner:column==2?inner:outer;
            return delivery.position+active.pickupSurface.right*(x*active.truckScale)+
                active.pickupSurface.forward*((slot/4%3-1)*.38f*active.truckScale)+Vector3.up*lift;
        }
        return delivery.position+new Vector3((slot%3-1)*.45f,lift+.2f,slot/3*.25f);
    }

    public static bool Contains(Vector3 world)
    {
        if (active==null || active.frame==null)return false;
        var p=active.frame.InverseTransformPoint(world);
        return p.x>active.garageArea.min.x && p.x<active.garageArea.max.x &&
            p.z>active.garageArea.min.z-.4f && p.z<active.garageArea.max.z && p.y>-.5f && p.y<active.height+1;
    }

    public static void SettleEquipment(GameObject item,Vector3 position)
    {
        if (active==null || !Contains(position))return;
        if(!EquipmentModelVisuals.TryGetEquipmentBounds(item.transform,out var bounds))return;
        float surface=active.zone.position.y;
        item.transform.position+=new Vector3(position.x-bounds.center.x,surface+.02f-bounds.min.y,position.z-bounds.center.z);
        foreach(var body in item.GetComponentsInChildren<Rigidbody>(true))
        {
            if(!body.isKinematic)body.velocity=body.angularVelocity=Vector3.zero;
            body.useGravity=false;body.isKinematic=true;
        }
    }

    public static string RewriteDeliveryText(string text)
    {
        if(active==null || string.IsNullOrEmpty(text))return text;
        return text.Replace("delivery table","truck's pickup shelf")
            .Replace("delivery zone","delivery truck in the garage");
    }

    private void OnDestroy()
    {
        if(active==this)active=null;
        foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
        foreach(var material in materials)if(material!=null)Destroy(material);
    }
}
