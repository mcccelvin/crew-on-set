using System.Collections.Generic;
using UnityEngine;

public sealed partial class StudioDeliveryBay
{
    private float guideDoorUntil,nextCollectionScan,collectionGraceUntil;
    private bool collectionGuideRequested;
    private Transform collectionTarget;
    private LineRenderer collectionLine;

    public static void ShowCollectionGuide()
    {
        if(active==null)return;
        active.collectionGuideRequested=true;
        active.collectionGraceUntil=Time.unscaledTime+5;
        active.nextCollectionScan=0;
    }

    private bool AtPickup(Vector3 world)
    {
        if(pickupSurface==null)return false;
        var p=pickupSurface.parent.InverseTransformPoint(world);
        return Mathf.Abs(p.x)<1.8f && p.z>-4.65f && p.z<-2.75f &&
            p.y>.35f/truckScale && p.y<3.2f;
    }

    public static bool TryGetGuideRoute(Vector3 start,Vector3 target,List<Vector3> route,out bool endsAtPickup)
    {
        endsAtPickup=false;
        if(active==null || active.frame==null || active.pickupSurface==null)return false;
        var bay=active;
        var p=bay.frame.InverseTransformPoint(start);
        var goal=bay.frame.InverseTransformPoint(target);
        bool startInGarage=p.z>.15f && p.x>bay.garageArea.min.x-.3f && p.x<bay.garageArea.max.x+.3f;
        bool targetInGarage=goal.z>.15f && goal.x>bay.garageArea.min.x-.3f && goal.x<bay.garageArea.max.x+.3f;
        bool exiting=startInGarage && goal.z<-.15f;
        bool entering=p.z<-.15f && targetInGarage;
        endsAtPickup=bay.AtPickup(target);
        var truck=bay.pickupSurface.parent;
        var t=truck.InverseTransformPoint(start);
        float foot=-4.4f-FloorRampRun/bay.truckScale;
        bool onRamp=Mathf.Abs(t.x)<bay.accessHalfWidth+.15f/bay.truckScale &&
            t.z>foot+.1f && t.z<-2.38f && t.y>.06f/bay.truckScale;
        bool onTruck=t.z>-4.4f && t.z<1.15f && Mathf.Abs(t.x)<1.3f && t.y>.65f/bay.truckScale;
        if(!endsAtPickup && !exiting && !entering && !onRamp && !onTruck)return false;

        if(!endsAtPickup && (onRamp || onTruck))
        {
            // Descend the same connected deck used to collect the order.
            // Never ask a route at floor height to drop off the liftgate.
            if(t.z>-2.7f)
                route.Add(truck.TransformPoint(new Vector3(0,1.10f+.055f/bay.truckScale,-2.70f)));
            if(t.z>-4.02f)
                route.Add(truck.TransformPoint(new Vector3(0,1.04f/bay.truckScale,-4.02f)));
            if(t.z>-4.4f)
                route.Add(truck.TransformPoint(new Vector3(0,1.04f/bay.truckScale,-4.4f)));
            route.Add(truck.TransformPoint(new Vector3(0,.04f/bay.truckScale,foot-.15f/bay.truckScale)));
        }
        if(exiting || entering || endsAtPickup && p.z<.9f)
        {
            bay.guideDoorUntil=Time.unscaledTime+2;
            // The gateway is bidirectional: after collecting equipment the
            // tutorial may immediately target an actor or station indoors.
            if(exiting)
            {
                if(p.z>.9f)route.Add(bay.frame.TransformPoint(new Vector3(0,.04f,1.4f)));
                route.Add(bay.frame.TransformPoint(new Vector3(0,.04f,-1.4f)));
            }
            else
            {
                if(p.z<-.9f)route.Add(bay.frame.TransformPoint(new Vector3(0,.04f,-1.4f)));
                route.Add(bay.frame.TransformPoint(new Vector3(0,.04f,1.4f)));
            }
        }
        if(!endsAtPickup)return route.Count>0;
        if(!onRamp && !(t.y>.65f/bay.truckScale && t.z>-4.4f && Mathf.Abs(t.x)<1.15f))
            route.Add(truck.TransformPoint(new Vector3(0,.04f/bay.truckScale,foot-.15f/bay.truckScale)));
        if(t.z<-4.4f || !onRamp && t.y<.65f/bay.truckScale)
            route.Add(truck.TransformPoint(new Vector3(0,1.04f/bay.truckScale,-4.4f)));
        route.Add(truck.TransformPoint(new Vector3(0,1.04f/bay.truckScale,-4.02f)));
        return true;
    }

    private bool Collectable(Transform target)
    {
        if(target==null || !target.gameObject.activeInHierarchy || !AtPickup(target.position) ||
            target.GetComponentInParent<Player.PlayerController.PlayerController>()!=null ||
            target.GetComponentInParent<MultiplayerPlayerController>()!=null)return false;
        foreach(var collider in target.GetComponentsInChildren<Collider>())
            if(collider.enabled && !collider.isTrigger)return true;
        return false;
    }

    private void LateUpdate()
    {
        if(frame==null || !collectionGuideRequested)return;
        if(Time.unscaledTime>=nextCollectionScan)
        {
            nextCollectionScan=Time.unscaledTime+.75f;
            collectionTarget=null;
            foreach(var item in FindObjectsOfType<Player.Equipment.Equipment>())
                if(item.gameObject.scene==gameObject.scene && Collectable(item.transform)){collectionTarget=item.transform;break;}
            if(collectionTarget==null)
                foreach(var item in FindObjectsOfType<NetworkStageObject>())
                    if(item.gameObject.scene==gameObject.scene && Collectable(item.transform)){collectionTarget=item.transform;break;}
            if(collectionTarget==null && Time.unscaledTime>collectionGraceUntil)collectionGuideRequested=false;
        }
        if(collectionLine==null && collectionTarget!=null)
        {
            var guide=new GameObject("Delivery collection guide",typeof(LineRenderer));
            guide.transform.SetParent(transform,false);collectionLine=guide.GetComponent<LineRenderer>();collectionLine.enabled=false;
        }
        if(collectionLine==null)return;
        bool show=collectionGuideRequested && Collectable(collectionTarget) && Application.isFocused &&
            !PauseManager.isPaused && Cursor.lockState==CursorLockMode.Locked &&
            !ProductionGuideLine.HasActiveGuide(collectionLine) &&
            (TutorialUIManager.Instance==null || !TutorialUIManager.Instance.IsBossDialogueOpen());
        collectionLine.enabled=false;
        if(!show)return;
        foreach(var player in players)
        {
            if(player==null || !player.gameObject.activeInHierarchy)continue;
            var owner=player.GetComponent<Photon.Pun.PhotonView>();
            if(owner!=null && !owner.IsMine)continue;
            var solo=player.GetComponent<Player.PlayerController.PlayerController>();
            var crew=player.GetComponent<MultiplayerPlayerController>();
            var camera=crew!=null?crew.playerCamera:solo!=null?solo.GameplayCamera:Camera.main;
            if(camera==null || !camera.isActiveAndEnabled)continue;
            collectionLine.enabled=true;ProductionGuideLine.Draw(collectionLine,player,collectionTarget,camera);break;
        }
    }
}
