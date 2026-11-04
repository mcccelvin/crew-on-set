using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class MultiplayerAuthoredUI
{
    private ShopTerminal sceneShop;
    private readonly List<Canvas> shopCanvases = new List<Canvas>();
    private int pendingShopReceipt;
    private float shopRequestDeadline;

    private void SetupShop()
    {
        var root = panels["shop"]?.transform; if (root == null) return;
        sceneShop = FindObjectsOfType<ShopTerminal>(true).FirstOrDefault(shop => shop.gameObject.scene == gameObject.scene);
        SetupShopCards(root.GetComponent<Canvas>());
        // The mounted display belongs to Equipment, outside the copied solo UI root.
        var world = sceneShop != null ? sceneShop.worldSpaceCanvas : null;
        if (world != null && world.transform != root)
        {
            SetupShopCards(world);
            Activate(world.gameObject);
            world.worldCamera = Control != null ? Control.View : Camera.main;
            var raycaster = world.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
        }
        Crew.CommandRejected += ShopCommandRejected;
        RefreshShop();
    }
    private void SetupShopCards(Canvas canvas)
    {
        if (canvas == null) return;
        shopCanvases.Add(canvas);
        if (sceneShop != null) sceneShop.PrepareSharedShopCanvas(canvas);
        else ShopTerminal.HideRetiredShopCards(canvas);
        foreach (string kind in new[] { "camera", "light", "sd", "softlight", "megaphone" })
        {
            foreach (var button in ShopTerminal.BindSharedShopItem(canvas, EquipmentName(kind), () => AddShopItem(kind)))
                shopButtons[button] = kind;
        }
        Bind(canvas.transform, "Confirm", CheckoutShop);
        Bind(canvas.transform, "Cancel", () => { if (pendingShopReceipt != 0) return; cart.Clear(); RefreshShop(); });
        ShopTerminal.CompactShopCards(canvas);
    }
    private void AddShopItem(string kind)
    {
        if (pendingShopReceipt != 0 || Crew.State.recording || !MultiplayerContractManager.ShopItemAvailable(kind, Crew.State.contractLevel) ||
            MultiplayerContractManager.ShopItemOwned(Crew.State, kind)) return;
        if (!Crew.HasRole(MultiplayerContractManager.RoleFor(kind)))
        { GameFeedback.Show("This equipment is purchased by its assigned crew role.", GameFeedback.NoticeType.Caution); return; }
        if (MultiplayerContractManager.UniqueShopItem(kind) && cart.Contains(kind))
        { GameFeedback.Show("Already in your cart: " + EquipmentName(kind)); return; }
        if (Crew.State.objects.Count + cart.Count >= 48)
        { GameFeedback.Show("Collect or use the studio's existing equipment first.", GameFeedback.NoticeType.Caution); return; }
        cart.Add(kind); RefreshShop();
    }
    private void CheckoutShop()
    {
        if (pendingShopReceipt != 0 || Crew.State.recording) return;
        if (cart.Count == 0) { GameFeedback.Show("Add equipment to your cart first."); return; }
        if (cart.Sum(MultiplayerContractManager.Price) > Crew.State.budget)
        { GameFeedback.Show("Not enough team B-Coins.", GameFeedback.NoticeType.Caution); return; }
        var member = Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
        if (member == null) return;
        pendingShopReceipt = member.shopReceipt + 1;
        shopRequestDeadline = Time.unscaledTime + 15;
        Crew.Send(new CrewCommand { action = "checkout", index = pendingShopReceipt, items = cart.ToArray() });
        RefreshShop();
    }
    private void ShopCommandRejected(string message)
    {
        pendingShopReceipt = 0;
        GameFeedback.Show(message, GameFeedback.NoticeType.Caution);
        RefreshShop();
    }
    private void UpdateShopRequest()
    {
        if (pendingShopReceipt == 0) return;
        var member = Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
        if (member != null && member.shopReceipt >= pendingShopReceipt)
        {
            pendingShopReceipt = 0; cart.Clear(); RefreshShop();
            GameFeedback.Show(StudioDeliveryBay.CollectionMessage, GameFeedback.NoticeType.Success);
            StudioDeliveryBay.ShowCollectionGuide();
        }
        else if (Time.unscaledTime >= shopRequestDeadline)
            ShopCommandRejected("The purchase hasn't been confirmed. Check your connection before retrying.");
    }
    private void RefreshShop()
    {
        if (Crew?.State == null) return;
        cart.RemoveAll(kind => MultiplayerContractManager.ShopItemOwned(Crew.State, kind));
        foreach (var entry in shopButtons)
        {
            var button = entry.Key; string kind = entry.Value;
            bool owned = MultiplayerContractManager.ShopItemOwned(Crew.State, kind);
            bool available = MultiplayerContractManager.ShopItemAvailable(kind, Crew.State.contractLevel) && Crew.Studio.CanCreate(kind);
            string caption = owned ? "OUT OF STOCK" : available ? "+ ADD TO CART" : "NOT AVAILABLE";
            ShopTerminal.SetSharedShopButton(button, caption, !owned && available && !Crew.State.recording && pendingShopReceipt == 0);
        }
        foreach (var canvas in shopCanvases)
        {
            foreach (var total in canvas.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name == "Total"))
                total.text = "Total: B " + cart.Sum(MultiplayerContractManager.Price);
            var confirm = Find(canvas.transform, "Confirm")?.GetComponent<Button>();
            var cancel = Find(canvas.transform, "Cancel")?.GetComponent<Button>();
            bool screen = canvas.gameObject == panels["shop"];
            if (confirm != null) confirm.interactable = screen && !Crew.State.recording && pendingShopReceipt == 0;
            if (cancel != null) cancel.interactable = screen && pendingShopReceipt == 0;
        }
    }
    private void SetupTablet()
    {
        var root = panels["tablet"]?.transform; if (root == null) return;
        var red=refs.Get<Slider>("DirectorTerminal.rSlider");var green=refs.Get<Slider>("DirectorTerminal.gSlider");var blue=refs.Get<Slider>("DirectorTerminal.bSlider");
        var colourPicker=root.GetComponent<DirectorColorPicker>()??root.gameObject.AddComponent<DirectorColorPicker>();
        colourPicker.Initialize(red,green,blue,colour=>{
            red.SetValueWithoutNotify(Mathf.Lerp(red.minValue,red.maxValue,colour.r));
            green.SetValueWithoutNotify(Mathf.Lerp(green.minValue,green.maxValue,colour.g));
            blue.SetValueWithoutNotify(Mathf.Lerp(blue.minValue,blue.maxValue,colour.b));
        },()=>Crew!=null && Crew.HasRole(CrewRole.Director));
        viewport = refs.Get<RawImage>("DirectorTerminal.viewportUI");
        if (viewport == null) viewport = Find(root, "Viewport")?.GetComponent<RawImage>();
        stageCamera = new GameObject("Crew tablet camera").AddComponent<Camera>();
        stageTexture = new RenderTexture(1280, 720, 24); stageTexture.Create(); stageCamera.targetTexture = stageTexture;
        stageCamera.fieldOfView = 58; stageCamera.enabled = false;
        var b = Crew.Studio.Stage;
        stageCamera.transform.position = b.center + new Vector3(0, 4, -b.extents.z - 7);
        stageCamera.transform.LookAt(b.center + Vector3.up);
        if (refs.hasTabletCamera)
        {
            stageCamera.transform.SetPositionAndRotation(refs.tabletCameraPosition, refs.tabletCameraRotation);
            stageCamera.fieldOfView = refs.tabletCameraFieldOfView;
            stageCamera.orthographic = refs.tabletCameraOrthographic;
            stageCamera.orthographicSize = refs.tabletCameraSize;
        }
        if (viewport != null) viewport.texture = stageTexture;
        var container = refs.Get<Transform>("DirectorTerminal.propUIContainer");
        if (container != null)
        {
            foreach (Transform child in container) child.gameObject.SetActive(false);
            int index = 0;
            foreach (string kind in new[] { "actor", "actor", "actor", "chair", "product", "cup", "table", "vehicle" })
            {
                int tier = kind == "actor" ? index : 0;
                string caption = kind == "actor" ? "ACTOR " + (char)('A' + tier) : kind.ToUpperInvariant();
                var template = refs.Get<GameObject>("DirectorTerminal.uiPropCardPrefab");
                Button card;
                if (template != null)
                {
                    var copy = Instantiate(template, container, false);
                    foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true))
                        if (script != null && !(script is UnityEngine.EventSystems.UIBehaviour)) { script.enabled = false; Destroy(script); }
                    copy.SetActive(true); copy.name = "Crew element " + index++;
                    card = copy.GetComponent<Button>() ?? copy.AddComponent<Button>();
                    card.targetGraphic = copy.GetComponent<Graphic>();
                    card.onClick = new Button.ButtonClickedEvent();
                    card.onClick.AddListener(() => { pendingProp = kind; pendingTier = tier; dragging = false; });
                    Set(copy.GetComponentInChildren<TMP_Text>(true), caption + "\nB " + MultiplayerContractManager.Price(kind));
                }
                else card = Button(container, "Crew element " + index++, caption + "\nB " + MultiplayerContractManager.Price(kind), () => { pendingProp = kind; pendingTier = tier; dragging = false; });
                if (container.GetComponent<LayoutGroup>() == null) ((RectTransform)card.transform).anchoredPosition = new Vector2((index - 1) * 105, 0);
            }
        }
        Bind(root, "WallButton", () => UITransition.Show(refs.Get<GameObject>("DirectorTerminal.interiorPicker")), "CHOOSE SET");
        Bind(root, "Clear Stage", () => Crew.Send(new CrewCommand { action = "clearStage" }));
        Bind(root, "Pose Actor Button", () => { Message = "Equip the megaphone to cue your actor."; Close(); }, "USE MEGAPHONE");
        var picker = refs.Get<GameObject>("DirectorTerminal.interiorPicker");
        if (picker != null)
        {
            var cafe = Find(picker.transform, "CAFE CORNER"); if (cafe != null) cafe.gameObject.SetActive(false);
            Bind(picker.transform, "PLAIN BACKDROP", () => { setChoice = "backdrop"; Set(refs.Get<TMP_Text>("DirectorTerminal.selectionIndicatorText"), "Selected: PLAIN BACKDROP"); });
            Bind(picker.transform, "COFFEE INTERIOR", () => { setChoice = "interior"; Set(refs.Get<TMP_Text>("DirectorTerminal.selectionIndicatorText"), "Selected: COFFEE INTERIOR"); });
            Bind(picker.transform, "Buy Interior", () => { var stage = Crew.Studio.Stage; Crew.Send(new CrewCommand { action = "spawn", kind = setChoice, position = new Vector3(stage.center.x, stage.max.y, stage.center.z) }); picker.SetActive(false); }, "USE SET");
            Bind(picker.transform, "Cancel Interior", () => picker.SetActive(false));
        }
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            if (button.name.StartsWith("Close", StringComparison.OrdinalIgnoreCase) || button.name == "Crew tablet close") button.gameObject.SetActive(false);
    }
    private void UpdateTablet()
    {
        if (viewport == null || Mouse.current == null) return;
        UpdateTabletColor();
        var mouse = Mouse.current; var key = Keyboard.current;
        if (key != null) { if (key.qKey.isPressed) placementYaw -= Time.deltaTime * 70; if (key.eKey.isPressed) placementYaw += Time.deltaTime * 70; if (key.rKey.wasPressedThisFrame) placementYaw += 45; }
        var rect = viewport.rectTransform;
        if (!RectTransformUtility.RectangleContainsScreenPoint(rect, mouse.position.ReadValue())) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, mouse.position.ReadValue(), null, out var local);
        var uv = new Vector2((local.x - rect.rect.xMin) / rect.rect.width, (local.y - rect.rect.yMin) / rect.rect.height);
        var ray = stageCamera.ViewportPointToRay(uv);
        var hits = Physics.RaycastAll(ray, 100, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
        if (pendingProp == null && mouse.leftButton.wasPressedThisFrame)
        {
            var selected = hits.Select(h => h.collider.GetComponentInParent<NetworkStageObject>()).FirstOrDefault(o => o != null);
            if (selected != null && Crew.HasRole(MultiplayerContractManager.RoleFor(Crew.Object(selected.Id).kind)))
            { Control.Selected = selected.Id; pendingProp = Crew.Object(selected.Id).kind; dragging = true; placementYaw = selected.transform.eulerAngles.y; }
        }
        var surface = hits.FirstOrDefault(h => h.normal.y > .65f && h.collider.GetComponentInParent<NetworkStageObject>() == null && h.collider.GetComponentInParent<PhotonView>() == null);
        if (pendingProp != null && surface.collider != null && Crew.Studio.ValidPosition(surface.point))
        {
            if (marker == null) { marker = new GameObject("Director placement square"); SquareGuideMarker.Apply(marker); }
            marker.SetActive(true); marker.transform.position = surface.point + Vector3.up * .02f;
            Set(refs.Get<TMP_Text>("DirectorTerminal.selectionIndicatorText"), "Place " + pendingProp + " • R rotate • Q/E fine rotate");
            if ((!dragging && mouse.leftButton.wasPressedThisFrame) || (dragging && mouse.leftButton.wasReleasedThisFrame))
            {
                Crew.Send(new CrewCommand { action = dragging ? "move" : "spawn", id = Control.Selected, kind = pendingProp,
                    value = pendingTier, position = surface.point, rotation = new Vector3(0,placementYaw,0), number = placementYaw });
                pendingProp = null; dragging = false; marker.SetActive(false);
            }
        }
    }
    private void UpdateTabletColor()
    {
        if (Time.unscaledTime >= nextColor)
        {
            nextColor = Time.unscaledTime + .15f;
            var r = refs.Get<Slider>("DirectorTerminal.rSlider"); var g = refs.Get<Slider>("DirectorTerminal.gSlider"); var b = refs.Get<Slider>("DirectorTerminal.bSlider");
            if (r != null && g != null && b != null)
            {
                var color = new Vector3(r.normalizedValue, g.normalizedValue, b.normalizedValue);
                var current = Crew.State.backdropColor;
                if (Vector3.Distance(color, new Vector3(current.r,current.g,current.b)) > .02f) Crew.Send(new CrewCommand { action = "setColor", position = color });
                Set(refs.Get<TMP_Text>("DirectorTerminal.rValueText"), Mathf.RoundToInt(color.x * 255).ToString());
                Set(refs.Get<TMP_Text>("DirectorTerminal.gValueText"), Mathf.RoundToInt(color.y * 255).ToString());
                Set(refs.Get<TMP_Text>("DirectorTerminal.bValueText"), Mathf.RoundToInt(color.z * 255).ToString());
            }
        }
    }
    private void SetupBoss()
    {
        bossText = refs.Get<TMP_Text>("TutorialUIManager.bossText");
        BossDialogueStyle.Apply(bossText);
        var root = panels["boss"]?.transform; if (root == null) return;
        foreach (var button in root.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
        refs.Get<GameObject>("TutorialUIManager.okButton")?.SetActive(false);
        refs.Get<GameObject>("TutorialUIManager.skipButton")?.SetActive(false);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text != bossText && (text.text.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0 || text.text.Trim().Equals("close", StringComparison.OrdinalIgnoreCase)))
            {
                if (bossContinue == null && text.text.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0) bossContinue = text;
                text.gameObject.SetActive(false);
            }
        if (bossContinue == null) bossContinue = Text(root, "Crew briefing continue", new Vector2(.34f,.035f), new Vector2(.91f,.078f), 24);
        bossContinue.gameObject.SetActive(true);
        var portrait = refs.Get<Image>("TutorialUIManager.bossPortraitDisplay");
        var pose = refs.Get<Sprite>("TutorialUIManager.poseOpenHand") ?? refs.Get<Sprite>("TutorialUIManager.poseBoss");
        if (portrait != null && pose != null) { portrait.sprite = pose; portrait.enabled = true; }
    }
    private void ShowBoss()
    {
        if (bossContinue != null) bossContinue.gameObject.SetActive(true);
        var member = Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
        int page = member?.briefingPage ?? 0;
        Set(bossText, BossDialogueStyle.HighlightControls(MultiplayerContractManager.Briefing[Mathf.Clamp(page, 0, MultiplayerContractManager.Briefing.Length - 1)] +
            (page == 0 ? "\n" + MultiplayerContractManager.Title(Crew.State.contractLevel) + " — team balance " + Crew.State.budget.ToString("N0") + " B-Coins." : "")));
        Set(bossContinue, page == MultiplayerContractManager.Briefing.Length - 1 ? "PRESS [SPACE] TO START WORKING" : "PRESS [SPACE] TO CONTINUE");
        if (bossText != null) bossText.maxVisibleCharacters = int.MaxValue;
    }
    private void NextBoss()
    {
        if (!Crew.LocalIsReadingBriefing) return;
        var member = Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber);
        if (member == null || member.briefed) return;
        Crew.Send(new CrewCommand { action = "briefingNext", index = member.briefingPage });
    }
    private void SetupBook()
    {
        var root = panels["almanac"]?.transform; if (root == null) return;
        Bind(root, "Close book", Close);
        Bind(root, "Next page", () => TurnBook(1)); Bind(root, "Previous page", () => TurnBook(-1));
        Bind(root, "Equipment tab", () => SetBook("Equipment")); Bind(root, "Techniques tab", () => SetBook("Technique"));
        foreach (string category in new[] { "DIRECTOR", "LIGHTING", "AUDIO", "CAMERA", "EDITING" }) Bind(root, "Category " + category, () => SetBook(category));
        Bind(root, "Director record", () => { SetBook("all"); Message = "Your role: " + MultiplayerContractManager.RoleName((CrewRole)Crew.State.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber).roles); });
        Bind(root, "Milestones", () => { SetBook("all"); Message = "Recorded takes: " + Crew.State.shots.Count + " • Edited cuts: " + Crew.State.cuts.Count; });
        var video = refs.Get<GameObject>("AlmanacManager.techniqueGuidePanel"); if (video != null) video.SetActive(false);
        var watch = Find(root, "Watch guide"); if (watch != null) watch.gameObject.SetActive(false);
    }
    private void SetBook(string category)
    {
        var root = panels["almanac"]?.transform;
        var pages = refs.Get<GameObject>("AlmanacManager.knowledgePanel"); if (pages != null) Activate(pages);
        refs.Get<GameObject>("AlmanacManager.playerInfoPanel")?.SetActive(false); refs.Get<GameObject>("AlmanacManager.achievementsPanel")?.SetActive(false);
        bookEntries = refs.knowledge.Where(e => category == "all" || (e.category ?? "").IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0 ||
            (e.id + " " + e.title + " " + e.description).IndexOf(category, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        CrewRole role = category == "DIRECTOR" ? CrewRole.Director : category == "CAMERA" ? CrewRole.Camera : category == "EDITING" ? CrewRole.Editor : CrewRole.AVTechnician;
        bookEntries.Insert(0, new KnowledgeEntry { title = category == "all" ? "YOUR CREW" : category, description = category == "all" ? string.Join("\n\n", new[] { CrewRole.Director,CrewRole.Camera,CrewRole.AVTechnician,CrewRole.Editor }.Select(r => MultiplayerContractManager.RoleName(r) + ": " + MultiplayerContractManager.Tasks(Crew.State,r))) : MultiplayerContractManager.Tasks(Crew.State, role) });
        bookPage = 0; ShowBook();
    }
    private void TurnBook(int direction) { if (bookEntries.Count == 0) return; bookPage = (bookPage + direction + bookEntries.Count) % bookEntries.Count; ShowBook(); StartCoroutine(FlipPage()); }
    private IEnumerator FlipPage()
    {
        var pages = refs.Get<Transform>("AlmanacManager.knowledgePanel"); if (pages == null) yield break;
        var scale = pages.localScale;
        for (float time = 0; time < .18f; time += Time.unscaledDeltaTime) { pages.localScale = Vector3.Scale(scale, new Vector3(Mathf.Lerp(.92f,1,time/.18f),1,1)); yield return null; }
        pages.localScale = scale;
    }
    private void ShowBook()
    {
        if (bookEntries.Count == 0) return;
        var entry = bookEntries[bookPage]; string body = entry.description ?? ""; int split = body.Length > 380 ? body.IndexOf(' ', body.Length / 2) : -1;
        Set(refs.Get<TMP_Text>("AlmanacManager.bookEntryTitle"), entry.title);
        Set(refs.Get<TMP_Text>("AlmanacManager.bookLeftText"), split > 0 ? body.Substring(0,split) : body);
        Set(refs.Get<TMP_Text>("AlmanacManager.bookRightText"), split > 0 ? body.Substring(split) : "Keep your role in mind and coordinate with your crew.");
        Set(refs.Get<TMP_Text>("AlmanacManager.bookPageNumber"), (bookPage + 1) + " / " + bookEntries.Count);
    }
    private void SetupContract() { BuildCrewContract(); }
    private void RefreshContract() { UpdateCrewContract(); }
    private void RequestCrewRetry()
    {
        if (!PhotonNetwork.IsMasterClient || Crew.State.recording) return;
        Open("boss");
        crewRetryPrompt = true;
        if (bossContinue != null) bossContinue.gameObject.SetActive(false);
        Set(bossText, "Restart this contract? Your crew's current footage and edit will be cleared. The starting budget and equipment will be restored. Your offline career is kept.");
        if (bossText != null) bossText.maxVisibleCharacters = int.MaxValue;
        var root = panels["boss"]?.transform; if (root == null) return;
        var confirm = Find(root,"Crew confirm retry")?.GetComponent<Button>() ?? Button(root,"Crew confirm retry","RETRY CONTRACT", () => { Close(); Crew.Send(new CrewCommand { action = "retryContract" }); });
        var cancel = Find(root,"Crew cancel retry")?.GetComponent<Button>() ?? Button(root,"Crew cancel retry","KEEP WORKING", Close);
        Anchor(confirm.transform,new Vector2(.36f,.02f),new Vector2(.59f,.085f));
        Anchor(cancel.transform,new Vector2(.64f,.02f),new Vector2(.88f,.085f));
        confirm.gameObject.SetActive(true); cancel.gameObject.SetActive(true);
    }
}
