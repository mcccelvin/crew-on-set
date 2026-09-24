using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MultiplayerCrewController : MonoBehaviour
{
    public static MultiplayerCrewController Local { get; private set; }
    public Camera View;
    public int Selected;
    public string ShotSize = "Wide";
    public string Placement { get; private set; }
    public string Hint { get; private set; } = "Aim and click to select an object.";
    private bool reposition;
    private float yaw, nextSample;
    private GameObject marker;
    private MultiplayerRoleManager Crew => MultiplayerRoleManager.Instance;
    private void Awake() { Local = this; }
    public void BeginPlace(string kind)
    {
        Cancel(); Placement = kind; yaw = 0;
        RoleSelectionUI.Open = false;
    }
    public void Cancel()
    {
        if (Crew != null && Crew.Studio.Objects.TryGetValue(Selected, out var old) && old.Bot != null) old.Bot.HidePlacementPreview();
        Placement = null; reposition = false;
        if (marker != null) Destroy(marker);
    }
    public void Command(string action, int value = 0)
    { Crew?.Send(new CrewCommand { action = action, id = Selected, value = value, kind = ShotSize }); }
    private bool Aim(out RaycastHit hit)
    {
        hit = default;
        if (View == null) return false;
        foreach (var candidate in Physics.RaycastAll(View.ViewportPointToRay(new Vector3(.5f, .5f)), 35, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance))
        {
            if (candidate.transform.IsChildOf(transform) || candidate.transform.GetComponentInParent<PhotonView>() != null) continue;
            if (reposition && candidate.collider.GetComponentInParent<NetworkStageObject>()?.Id == Selected) continue;
            hit = candidate; return true;
        }
        return false;
    }
    private void Update()
    {
        var crew = Crew; var key = Keyboard.current; var mouse = Mouse.current;
        if (crew == null || crew.State == null || View == null || key == null || mouse == null) return;
        if (RoleSelectionUI.Open || crew.State.phase == "lobby" || !Application.isFocused) { if (Placement != null) Cancel(); return; }
        var selected = crew.Object(Selected);
        if (selected == null && reposition) Cancel();
        if (key.tKey.wasPressedThisFrame)
        {
            if (Placement != null) Cancel();
            else if (selected != null && crew.HasRole(MultiplayerContractManager.RoleFor(selected.kind)))
            { Placement = selected.kind; reposition = true; yaw = selected.rotation.y; }
        }
        bool hitFound = Aim(out var hit);
        if (Placement != null)
        {
            if (key.qKey.isPressed) yaw -= Time.deltaTime * 70;
            if (key.eKey.isPressed) yaw += Time.deltaTime * 70;
            bool valid = hitFound && hit.normal.y > .65f && crew.Studio.ValidPosition(hit.point) && !hit.collider.isTrigger;
            Hint = valid ? "LMB place | Q/E rotate | T cancel" : "Aim at a clear horizontal surface | T cancel";
            if (reposition && crew.Studio.Objects.TryGetValue(Selected, out var actor) && actor.Bot != null)
            { if (valid) actor.Bot.ShowCrewPlacementPreview(hit.point, yaw); else actor.Bot.HidePlacementPreview(); }
            else
            {
                if (marker == null)
                {
                    marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); marker.name = "Local placement marker";
                    Destroy(marker.GetComponent<Collider>()); marker.transform.localScale = new Vector3(.6f, .01f, .6f);
                }
                marker.SetActive(valid); if (valid) marker.transform.position = hit.point + Vector3.up * .015f;
            }
            if (valid && mouse.leftButton.wasPressedThisFrame)
            {
                crew.Send(new CrewCommand { action = reposition ? "move" : "spawn", id = Selected, kind = Placement,
                    position = hit.point, rotation = new Vector3(0, yaw, 0), number = yaw }); Cancel();
            }
            return;
        }
        var target = hitFound ? hit.collider.GetComponentInParent<NetworkStageObject>() : null;
        Hint = target != null ? "LMB select " + crew.Object(target.Id)?.kind + " | T reposition | Tab crew menu" : "Tab crew menu | LMB select | T reposition";
        if (mouse.leftButton.wasPressedThisFrame && target != null)
        {
            if (selected?.kind == "actor" && crew.HasRole(CrewRole.Director) && target.Id != Selected)
            {
                var interaction = hit.collider.GetComponentInParent<Contract4Interactable>();
                int index = System.Array.IndexOf(target.GetComponentsInChildren<Contract4Interactable>(), interaction);
                crew.Send(new CrewCommand { action = "interact", id = Selected, target = target.Id, index = index });
            }
            else Selected = target.Id;
        }
        if (key.hKey.wasPressedThisFrame) Selected = 0;
        if (key.deleteKey.wasPressedThisFrame && selected != null) Command("delete");
        if (selected?.kind == "actor" && crew.HasRole(CrewRole.Director))
        {
            if (key.zKey.wasPressedThisFrame) Command("pose", 0);
            if (key.xKey.wasPressedThisFrame) Command("pose", 1);
            if (key.cKey.wasPressedThisFrame) Command("pose", 2);
            if (key.bKey.wasPressedThisFrame) Command("startMark");
            if (key.nKey.wasPressedThisFrame) Command("endMark");
            if (key.kKey.wasPressedThisFrame) Command("walk");
            if (key.jKey.wasPressedThisFrame) Command("return");
            if (key.oKey.wasPressedThisFrame) Command("stop");
        }
        if (selected?.kind == "light" && crew.HasRole(CrewRole.Lighting))
        {
            float kelvin = selected.kelvin, intensity = selected.intensity;
            if (key.zKey.wasPressedThisFrame) kelvin -= 100;
            if (key.xKey.wasPressedThisFrame) kelvin += 100;
            if (key.cKey.wasPressedThisFrame) intensity -= .25f;
            if (key.vKey.wasPressedThisFrame) intensity += .25f;
            if (kelvin != selected.kelvin || intensity != selected.intensity || key.fKey.wasPressedThisFrame)
                crew.Send(new CrewCommand { action = "light", id = Selected, value = key.fKey.wasPressedThisFrame ? (selected.powered ? 0 : 1) : (selected.powered ? 1 : 0),
                    number = kelvin, number2 = intensity, index = (int)selected.diffusion, rotation = selected.rotation });
        }
        if (crew.HasRole(CrewRole.Camera))
        {
            View.fieldOfView = Mathf.Clamp(View.fieldOfView - mouse.scroll.ReadValue().y * .025f, 15, 90);
            if (key.rKey.wasPressedThisFrame) Command("record");
            if (crew.State.recording && crew.State.recorder == PhotonNetwork.LocalPlayer.ActorNumber && Time.unscaledTime >= nextSample)
            {
                nextSample = Time.unscaledTime + .5f;
                crew.Send(new CrewCommand { action = "sample", position = View.transform.position, rotation = View.transform.eulerAngles, number = View.fieldOfView });
            }
        }
    }
    private void OnDestroy() { Cancel(); if (Local == this) Local = null; }
}
