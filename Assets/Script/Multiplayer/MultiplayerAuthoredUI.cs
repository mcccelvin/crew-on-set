using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class MultiplayerAuthoredUI : MonoBehaviour
{
    public static MultiplayerAuthoredUI Instance { get; private set; }
    public bool Ready { get; private set; }
    public string Panel { get; private set; } = "";
    public string Message { get; private set; } = "";
    private MultiplayerUIReferences refs;
    private GameObject view;
    private readonly Dictionary<string, GameObject> panels = new Dictionary<string, GameObject>();
    private readonly List<string> cart = new List<string>();
    private readonly Dictionary<Button, string> shopButtons = new Dictionary<Button, string>();
    private TMP_Text notice, bossText, promptTitle, promptKey, money, guide;
    private CanvasGroup promptGroup;
    private Camera stageCamera;
    private RenderTexture stageTexture;
    private RawImage viewport;
    private int lastRevision = -1;
    private string pendingProp, setChoice = "backdrop";
    private int pendingTier;
    private float placementYaw, nextColor;
    private GameObject marker;
    private bool dragging;
    private string previousPhase = "";
    private int bookPage;
    private SharedOptionsPanel options;
    private GameObject cameraHud;
    private CameraHUDController dynamicCameraHud;
    private bool viewfinder;
    private readonly float[] audioSamples = new float[256];
    private List<KnowledgeEntry> bookEntries = new List<KnowledgeEntry>();
    private MultiplayerRoleManager Crew => MultiplayerRoleManager.Instance;
    private MultiplayerCrewController Control => MultiplayerCrewController.Local;
    private void Awake() { Instance = this; }
    private IEnumerator Start()
    {
        // PUNSpawner first hides the old scene canvases. These are the independent view copies.
        yield return null;
        var prefab = Resources.Load<GameObject>("CrewUI/Studio");
        if (prefab == null) { Message = "Exit Play Mode so Unity can generate the multiplayer UI copies, then start again."; Debug.LogError(Message); yield break; }
        view = Instantiate(prefab); refs = view.GetComponent<MultiplayerUIReferences>();
        refs.RestoreArtwork();
        view.name = "Multiplayer authored station UI";
        panels["tablet"] = refs.Get<GameObject>("DirectorTerminal.tabletUI");
        panels["shop"] = refs.Get<GameObject>("ShopTerminal.screenSpaceCanvas");
        panels["computer"] = refs.Get<GameObject>("ComputerStation.computerUICanvas");
        panels["almanac"] = refs.Get<GameObject>("AlmanacManager.almanacCanvas");
        panels["boss"] = refs.Get<GameObject>("TutorialUIManager.bossHUDCanvas");
        panels["contract"] = refs.Get<GameObject>("ContractUIManager.contractCanvas");
        panels["pause"] = Find(view.transform, "Pause")?.gameObject;
        foreach (var canvas in view.GetComponentsInChildren<Canvas>(true))
            if (canvas.transform.parent == null || canvas.transform.parent.GetComponentInParent<Canvas>() == null) canvas.gameObject.SetActive(false);
        foreach (var panel in panels.Values) if (panel != null) panel.SetActive(false);
        view.SetActive(true);
        var eventSystem = EventSystem.current;
        if (eventSystem == null) eventSystem = new GameObject("Crew UI Event System", typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (var legacy in eventSystem.GetComponents<StandaloneInputModule>()) legacy.enabled = false;
        var input = eventSystem.GetComponent<InputSystemUIInputModule>() ?? eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if (input.actionsAsset == null) input.AssignDefaultActions();
        input.enabled = true;
        var hud = Find(view.transform, "PlayerUI"); if (hud != null) Activate(hud.gameObject);
        promptTitle = refs.Get<TMP_Text>("HotbarUIManager.directorPromptTitleText");
        promptKey = refs.Get<TMP_Text>("HotbarUIManager.directorPromptKeyText");
        promptGroup = refs.Get<CanvasGroup>("HotbarUIManager.directorPromptGroup");
        guide = refs.Get<TMP_Text>("HotbarUIManager.equipmentGuideText");
        money = Find(hud, "Quantity")?.GetComponent<TMP_Text>();
        var day = Find(hud, "Day HUD")?.GetComponentInChildren<TMP_Text>(true); if (day != null) day.text = "CONTRACT 4";
        var controls = refs.Get<GameObject>("HotbarUIManager.equipmentControlsRoot"); if (controls != null) controls.SetActive(false);
        cameraHud = view.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => string.Equals(t.name,"Cam Pov",StringComparison.OrdinalIgnoreCase))?.gameObject;
        if(cameraHud != null)cameraHud.SetActive(false);
        notice = Text(hud, "Crew notification", new Vector2(.2f,.86f), new Vector2(.8f,.94f), 20);
        SetupShop(); SetupTablet(); SetupBook(); SetupComputer(); SetupBoss(); SetupContract(); SetupPause();
        var worldShop = refs.Get<GameObject>("ShopTerminal.worldSpaceCanvas"); if (worldShop != null) Activate(worldShop);
        foreach (var button in hud != null ? hud.GetComponentsInChildren<Button>(true) : new Button[0])
            if (button.name.Contains("Almanac")) button.onClick.AddListener(() => Open("almanac"));
        BindStations(); Ready = true;
    }
    private void BindStations()
    {
        foreach (var t in FindObjectsOfType<Transform>(true))
        {
            if (t.gameObject.scene.name != "MultiStudio" || t.GetComponentInParent<Canvas>() != null) continue;
            string n = t.name.ToLowerInvariant();
            string kind = n.Contains("director table") ? "tablet" : n.Contains("editor table") ? "computer" : n.Contains("kiosk table") || n == "shopterminal" ? "shop" : null;
            if (kind == null) continue;
            var station = t.GetComponent<MultiplayerStation>() ?? t.gameObject.AddComponent<MultiplayerStation>(); station.station = kind;
            if (t.GetComponentInChildren<Collider>() == null)
            {
                var bounds = NetworkStudioFactory.BoundsOf(t.gameObject); var box = t.gameObject.AddComponent<BoxCollider>();
                box.center = t.InverseTransformPoint(bounds.center);
                var size = t.InverseTransformVector(bounds.size); box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            }
        }
    }
    private bool Allowed(string panel) => panel == "tablet" ? Crew.HasRole(CrewRole.Director) : panel == "computer" || panel == "editor" ? Crew.HasRole(CrewRole.Editor) : true;
    public void Open(string panel)
    {
        if (!Ready || Crew.State == null) return;
        if (Crew.State.phase == "briefing" && panel != "boss") return;
        if (!Allowed(panel)) { Message = panel == "tablet" ? "DIRECTOR station" : "EDITOR station — hand your recorded SD card to this computer."; return; }
        Close();
        if (panel == "editor") { OpenEditor(); return; }
        if (!panels.TryGetValue(panel, out var root) || root == null) { Message = "This station view is not available."; return; }
        Panel = panel; RoleSelectionUI.Open = true; Activate(root);
        if (panel == "tablet") { stageCamera.enabled = true; refs.Get<GameObject>("DirectorTerminal.interiorPicker")?.SetActive(false); }
        if (panel == "shop") { cart.Clear(); RefreshShop(); }
        if (panel == "almanac") SetBook("all");
        if (panel == "computer") ComputerHome();
        if (panel == "contract") RefreshContract();
    }
    public void Close()
    {
        if (options != null && options.IsOpen) options.Close(false);
        foreach (var root in panels.Values) if (root != null) root.SetActive(false);
        if (editorView != null) editorView.SetActive(false);
        if (stageCamera != null) stageCamera.enabled = false;
        if (cameraHud != null) cameraHud.SetActive(false); viewfinder = false;
        if (dynamicCameraHud != null) dynamicCameraHud.Hide();
        pendingProp = null; dragging = false; if (marker != null) marker.SetActive(false);
        Panel = ""; RoleSelectionUI.Open = false; playing = false;
    }
    private void Update()
    {
        if (!Ready || Crew?.State == null || !PhotonNetwork.InRoom) return;
        var state = Crew.State; var key = Keyboard.current;
        if (state.phase != previousPhase)
        {
            previousPhase = state.phase;
            if (state.phase == "briefing") { Open("boss"); ShowBoss(); }
            else if (state.phase == "build" || state.phase == "review") { Close(); Message = state.message; }
        }
        if (state.phase == "lobby") return;
        if (state.phase == "briefing")
        {
            ShowBoss();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (Application.isFocused && key != null && key.spaceKey.wasPressedThisFrame) NextBoss();
            return;
        }
        if (!Allowed(Panel)) Close();
        if (key != null && key.escapeKey.wasPressedThisFrame) { if (Panel == "") Open("pause"); else Close(); }
        if (key != null && key.pKey.wasPressedThisFrame) { if (Panel == "almanac") Close(); else Open("almanac"); }
        if (key != null && key.tabKey.wasPressedThisFrame) { if (Panel == "contract") Close(); else Open("contract"); }
        if (lastRevision != state.revision)
        {
            lastRevision = state.revision; RefreshHUD();
            if (Panel == "shop") RefreshShop();
            if (Panel == "computer") RefreshClips();
            if (Panel == "editor") RefreshEditorClips();
        }
        if (Panel == "tablet") UpdateTablet();
        if (Panel == "") UpdateWorld();
        UpdatePlayback();
        if (notice != null) notice.text = Crew.Notice + (string.IsNullOrEmpty(Message) || Message == Crew.Notice ? "" : "\n" + Message);
        Cursor.lockState = RoleSelectionUI.Open ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = RoleSelectionUI.Open;
    }
    private void RefreshHUD()
    {
        if (money != null) money.text = Crew.State.budget.ToString("N0");
        Set(refs.Get<TMP_Text>("DirectorTerminal.bCoinsText"), Crew.State.budget + "\nB-Coins");
        var slots = Find(view.transform, "HotbarUI");
        var member = Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
        for (int i = 1; i <= 5; i++)
        {
            var slot = slots?.Find("Item (" + i + ")"); if (slot == null) continue;
            var item = Crew.State.objects.Find(o => o.holder == PhotonNetwork.LocalPlayer.ActorNumber && o.slot == i && o.loadedInto == 0);
            var icon = slot.GetComponentsInChildren<Image>(true).FirstOrDefault(img => img.transform != slot);
            if (icon != null) { icon.sprite = item != null ? EquipmentIconArt.Get(EquipmentName(item.kind, item.tape != 0)) : null; icon.gameObject.SetActive(icon.sprite != null); }
            var background = slot.GetComponent<Image>(); if (background != null) background.color = member != null && item != null && item.id == member.equipped ? new Color(.48f,.32f,.1f,.9f) : new Color(.08f,.07f,.06f,.85f);
        }
    }
    private void UpdateWorld()
    {
        if (Control?.View == null) return;
        var key = Keyboard.current; if (key == null) return;
        var member = Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
        var held = member != null ? Crew.Object(member.equipped) : null;
        for (int n = 1; n <= 5; n++)
            if (key[(Key)((int)Key.Digit1 + n - 1)].wasPressedThisFrame)
            {
                var item = Crew.State.objects.Find(o => o.holder == PhotonNetwork.LocalPlayer.ActorNumber && o.slot == n && o.loadedInto == 0);
                Crew.Send(new CrewCommand { action = "equip", id = item != null ? item.id : 0 });
            }
        if (held != null && key.gKey.wasPressedThisFrame)
        {
            var p = Control.transform.position + Control.transform.forward * 1.4f;
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var floor, 4)) p = floor.point + Vector3.up * .04f;
            Crew.Send(new CrewCommand { action = "drop", id = held.id, position = p, rotation = new Vector3(0, Control.transform.eulerAngles.y, 0) });
        }
        if (held?.kind == "camera" && key.cKey.wasPressedThisFrame) Crew.Send(new CrewCommand { action = "insertCard", id = held.id });
        if (held?.kind == "camera" && key.vKey.wasPressedThisFrame) Control.ShotSize = Control.ShotSize == "Wide" ? "Medium" : Control.ShotSize == "Medium" ? "Close" : "Wide";
        if (held?.kind == "camera" && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) viewfinder = !viewfinder;
        if (held?.kind != "camera") viewfinder = false;
        if(cameraHud != null) cameraHud.SetActive(false);
        if (viewfinder && Control.View != null)
        {
            if (dynamicCameraHud == null) dynamicCameraHud = CameraHUDController.Create();
            dynamicCameraHud.Show(Control.View);
            bool mine = Crew.State.recording && Crew.State.recorder == PhotonNetwork.LocalPlayer.ActorNumber;
            dynamicCameraHud.Refresh(new CameraHUDController.State {
                recording=mine, seconds=mine ? (float)(PhotonNetwork.Time-Crew.State.recordStarted) : 0,
                card=Crew.State.objects.Any(o=>o.kind=="sd" && o.loadedInto==held.id),
                level=1, fps=MultiplayerRecording.FPS, width=320, height=180
            });
        }
        else if (dynamicCameraHud != null) dynamicCameraHud.Hide();
        if (held?.kind == "megaphone" && key.enterKey.wasPressedThisFrame) Crew.Send(new CrewCommand { action = "action" });
        if (held?.kind == "audio")
        {
            float gain = held.intensity + (key.xKey.wasPressedThisFrame ? .1f : key.zKey.wasPressedThisFrame ? -.1f : 0);
            if (gain != held.intensity || key.fKey.wasPressedThisFrame) Crew.Send(new CrewCommand {action="audio",id=held.id,value=key.fKey.wasPressedThisFrame ? held.powered ? 0 : 1 : held.powered ? 1 : 0,number=gain});
            AudioListener.GetOutputData(audioSamples,0);
            float peak = held.powered ? audioSamples.Max(v=>Mathf.Abs(v)) * held.intensity : 0;
            Message = "AUDIO " + (held.powered ? "ON" : "OFF") + " • Peak " + (20*Mathf.Log10(Mathf.Max(.00001f,peak))).ToString("0") + " dB • Z/X gain • F power";
        }
        var ray = Control.View.ViewportPointToRay(new Vector3(.5f,.5f));
        var hits = Physics.RaycastAll(ray, 4, ~0, QueryTriggerInteraction.Collide).OrderBy(h => h.distance);
        MultiplayerStation station = null; NetworkStageObject equipment = null;
        Vector3 aimPoint = Vector3.zero; bool hasAim = false;
        foreach (var hit in hits)
        {
            if (hit.transform.GetComponentInParent<PhotonView>() != null) continue;
            aimPoint = hit.point; hasAim = true;
            equipment = hit.collider.GetComponentInParent<NetworkStageObject>();
            station = hit.collider.GetComponentInParent<MultiplayerStation>(); break;
        }
        if (station == null && equipment == null && hasAim)
            station = FindObjectsOfType<MultiplayerStation>().Where(s => NetworkStudioFactory.BoundsOf(s.gameObject).SqrDistance(aimPoint) < 1.5f)
                .OrderBy(s => NetworkStudioFactory.BoundsOf(s.gameObject).SqrDistance(aimPoint)).FirstOrDefault();
        string prompt = "";
        if (station != null)
        {
            bool card = station.station == "computer" && held?.kind == "sd" && held.tape != 0;
            prompt = card ? "INSERT RECORDED SD CARD" : station.station == "tablet" ? "DIRECTOR TABLET" : station.station == "shop" ? "EQUIPMENT SHOP" : "EDITOR COMPUTER";
            if (key.eKey.wasPressedThisFrame)
            { if (card) Crew.Send(new CrewCommand { action = "deliverTape", id = held.id }); else Open(station.station); }
        }
        else if (equipment != null && MultiplayerRoomActions.IsEquipment(Crew.Object(equipment.Id)?.kind))
        {
            prompt = "PICK UP " + EquipmentName(Crew.Object(equipment.Id).kind, Crew.Object(equipment.Id).tape != 0);
            if (key.eKey.wasPressedThisFrame) Crew.Send(new CrewCommand { action = "pickup", id = equipment.Id });
        }
        if (promptGroup != null) { promptGroup.gameObject.SetActive(prompt != ""); promptGroup.alpha = prompt != "" ? 1 : 0; }
        Set(promptTitle, prompt); Set(promptKey, "E");
        Set(guide, held?.kind == "camera" ? "[LMB] Viewfinder  [C] Insert SD  [R] Record  [V] " + Control.ShotSize + "  [G] Drop  Wheel zoom" : held?.kind == "megaphone" ? "[LMB] Select / cue  [T] Reposition  [Enter] ACTION  [Z] Cycle animations  [B/N] Marks  [K] Walk  [O] Stop" : "[E] Use station / collect  [P] Almanac  [Tab] Contract  [1–5] Hotbar");
    }
    public static string EquipmentName(string kind, bool recorded = false) => kind == "camera" ? "NONY FX" : kind == "megaphone" ? "DIRECTOR MEGAPHONE" : kind == "sd" ? recorded ? "RECORDED SD CARD" : "SD CARD" : kind == "light" ? "160 LED PANEL" : "AUDIO MONITOR";
    private void OnDestroy()
    {
        if (dynamicCameraHud != null) Destroy(dynamicCameraHud.gameObject);
        if (view != null) Destroy(view); if (editorView != null) Destroy(editorView);
        if (stageCamera != null) Destroy(stageCamera.gameObject);
        if (stageTexture != null) { stageTexture.Release(); Destroy(stageTexture); }
        if (previewTexture != null) Destroy(previewTexture); if (gradingMaterial != null) Destroy(gradingMaterial);
        if (marker != null) Destroy(marker); if (Instance == this) Instance = null;
    }
    internal static Transform Find(Transform root, string name) => root != null ? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name) : null;
    private static void Set(TMP_Text text, string value) { if (text != null) text.text = value; }
    private static void Activate(GameObject obj)
    {
        if (obj == null) return;
        for (var t = obj.transform; t != null; t = t.parent) { t.gameObject.SetActive(true); var c = t.GetComponent<Canvas>(); if (c != null) c.enabled = true; }
        foreach (var group in obj.GetComponentsInChildren<CanvasGroup>(true)) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
    }
    private static void Bind(Transform root, string name, Action action, string label = null)
    {
        var button = Find(root, name)?.GetComponent<Button>(); if (button == null) return;
        button.onClick = new Button.ButtonClickedEvent(); button.onClick.AddListener(() => action());
        if (label != null) Set(button.GetComponentInChildren<TMP_Text>(true), label);
    }
    private static TMP_Text Text(Transform parent, string name, Vector2 min, Vector2 max, float size = 20)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); obj.transform.SetParent(parent, false);
        var rect = (RectTransform)obj.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        var text = obj.GetComponent<TextMeshProUGUI>(); text.text = ""; text.fontSize = size; text.enableWordWrapping = true; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; text.color = Color.white; return text;
    }
    private static Button Button(Transform parent, string name, string label, Action action)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>(); image.sprite = ExportUIArt.Get("blueButton"); image.type = Image.Type.Sliced; if (image.sprite == null) image.color = new Color(.06f,.3f,.65f);
        var rect = (RectTransform)obj.transform; rect.sizeDelta = new Vector2(160, 42);
        var text = Text(obj.transform, "Text", Vector2.zero, Vector2.one, 17); text.text = label;
        var button = obj.GetComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action()); return button;
    }
    private static void Anchor(Component component, Vector2 min, Vector2 max)
    { var rect = component.transform as RectTransform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
}
