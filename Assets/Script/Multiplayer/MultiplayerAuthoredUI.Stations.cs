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
    private void SetupShop()
    {
        var root = panels["shop"]?.transform; if (root == null) return;
        SetupShopCards(root);
        var world = refs.Get<GameObject>("ShopTerminal.worldSpaceCanvas");
        if (world != null && world.transform != root) SetupShopCards(world.transform);
        Bind(root, "Confirm", () => { Crew.Send(new CrewCommand { action = "checkout", items = cart.ToArray() }); cart.Clear(); RefreshShop(); });
        Bind(root, "Cancel", Close);
    }
    private void SetupShopCards(Transform root)
    {
        ShopTerminal.HideRetiredShopCards(root.GetComponent<Canvas>());
        // Keep the authored cards; clicking the card selects it without an extra cart button.
        var template = Find(root, "Level 2 Camera") ?? Find(root, "Light");
        if (template != null && Find(root, "Crew Audio") == null)
        {
            var audio = Instantiate(template.gameObject, root, false);
            audio.name = "Crew Audio";
            var rect = (RectTransform)audio.transform;
            if (root.GetComponent<LayoutGroup>() == null && template.name == "Light")
                rect.anchoredPosition -= Vector2.up * (rect.rect.height + 12f);
            var icon = EquipmentIconArt.Get("AUDIO MONITOR");
            if (icon != null)
            {
                var image = audio.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.transform != audio.transform && i.GetComponentInParent<Button>() == null);
                if (image != null) image.sprite = icon;
            }
        }
        var selectedCards = new HashSet<string>();
        foreach (Transform child in root)
        {
            string kind = child.name == "Camera" ? "camera" : child.name == "Light" ? "light" : child.name == "SDCard" ? "sd" : child.name == "Director Megaphone" ? "megaphone" : child.name == "Crew Audio" ? "audio" : null;
            if (child.name.Contains("Level ") || child.name == "LIGHT STRIP") { child.gameObject.SetActive(false); continue; }
            if (kind == null) continue;
            bool first = selectedCards.Add(kind); child.gameObject.SetActive(first); if (!first) continue;
            Set(Find(child, "ItemName")?.GetComponent<TMP_Text>(), EquipmentName(kind));
            Set(Find(child, "ItemPrice")?.GetComponent<TMP_Text>(), "B " + MultiplayerContractManager.Price(kind));
            foreach (var button in child.GetComponentsInChildren<Button>(true))
                if (button.transform != child) button.gameObject.SetActive(false);
            var buy = child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
            buy.targetGraphic = child.GetComponent<Graphic>() ?? child.GetComponentInChildren<Graphic>(true);
            if (buy.targetGraphic != null) buy.targetGraphic.raycastTarget = true;
            buy.onClick = new Button.ButtonClickedEvent();
            shopButtons[buy] = kind;
            buy.onClick.AddListener(() => {
                if (!Crew.HasRole(MultiplayerContractManager.RoleFor(kind)) || Crew.State.recording) return;
                if (cart.Count < 5) cart.Add(kind);
                RefreshShop();
            });
        }
        if (root != panels["shop"]?.transform)
            foreach (var button in root.GetComponentsInChildren<Button>(true)) button.interactable = false;
    }
    private void RefreshShop()
    {
        foreach (var entry in shopButtons)
        {
            var button = entry.Key; string kind = entry.Value;
            button.interactable = button.transform.IsChildOf(panels["shop"].transform) && Crew.HasRole(MultiplayerContractManager.RoleFor(kind)) && !Crew.State.recording;
            int count = cart.Count(item => item == kind);
            Set(Find(button.transform, "ItemName")?.GetComponent<TMP_Text>(), EquipmentName(kind) + (count > 0 ? " ×" + count : ""));
        }
        Set(Find(panels["shop"]?.transform, "Total")?.GetComponent<TMP_Text>(), "Total: B " + cart.Sum(MultiplayerContractManager.Price));
    }
    private void SetupTablet()
    {
        var root = panels["tablet"]?.transform; if (root == null) return;
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
            foreach (string kind in new[] { "actor", "actor", "actor", "chair", "product", "cup", "table" })
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
        Bind(root, "WallButton", () => refs.Get<GameObject>("DirectorTerminal.interiorPicker")?.SetActive(true), "CHOOSE SET");
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
        if (key != null) { if (key.qKey.isPressed) placementYaw -= Time.deltaTime * 70; if (key.eKey.isPressed) placementYaw += Time.deltaTime * 70; }
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
            if (marker == null) { marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(marker.GetComponent<Collider>()); marker.transform.localScale = new Vector3(.5f,.02f,.5f); }
            marker.SetActive(true); marker.transform.position = surface.point + Vector3.up * .02f;
            Set(refs.Get<TMP_Text>("DirectorTerminal.selectionIndicatorText"), "Place " + pendingProp + " • Q/E rotate");
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
        var root = panels["boss"]?.transform; if (root == null) return;
        foreach (var button in root.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
        refs.Get<GameObject>("TutorialUIManager.okButton")?.SetActive(false);
        refs.Get<GameObject>("TutorialUIManager.skipButton")?.SetActive(false);
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text != bossText && (text.text.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0 || text.text.Trim().Equals("close", StringComparison.OrdinalIgnoreCase))) text.gameObject.SetActive(false);
        var portrait = refs.Get<Image>("TutorialUIManager.bossPortraitDisplay");
        var pose = refs.Get<Sprite>("TutorialUIManager.poseOpenHand") ?? refs.Get<Sprite>("TutorialUIManager.poseBoss");
        if (portrait != null && pose != null) { portrait.sprite = pose; portrait.enabled = true; }
    }
    private void ShowBoss()
    {
        Set(bossText, MultiplayerContractManager.Briefing[Mathf.Clamp(Crew.State.briefingPage, 0, MultiplayerContractManager.Briefing.Length - 1)]);
        if (bossText != null) bossText.maxVisibleCharacters = int.MaxValue;
    }
    private void NextBoss()
    {
        if (Crew.State.phase != "briefing") return;
        Crew.Send(new CrewCommand { action = "briefingNext", index = Crew.State.briefingPage });
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
    private void SetupContract()
    {
        var root = panels["contract"]?.transform; if (root == null) return;
        foreach (var button in root.GetComponentsInChildren<Button>(true)) button.onClick.AddListener(Close);
    }
    private void RefreshContract()
    {
        refs.Get<GameObject>("ContractUIManager.offerPanel")?.SetActive(false);
        var brief = refs.Get<GameObject>("ContractUIManager.qualificationsPanel"); if (brief != null) Activate(brief);
        Set(refs.Get<TMP_Text>("ContractUIManager.briefTitle"), "KAPE KULTURA — CREW COMMERCIAL");
        Set(refs.Get<TMP_Text>("ContractUIManager.briefBody"), "Create a coffee commercial together.\n\n" + string.Join("\n\n", new[] {CrewRole.Director,CrewRole.Camera,CrewRole.AVTechnician,CrewRole.Editor}.Select(r => MultiplayerContractManager.RoleName(r) + ": " + MultiplayerContractManager.Tasks(Crew.State,r))) + "\n\n" + Crew.Notice);
    }
    private void SetupPause()
    {
        var root = panels["pause"]?.transform; if (root == null) return;
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            string text = (button.GetComponentInChildren<TMP_Text>(true)?.text ?? button.name).ToLowerInvariant();
            if (text.Contains("exit") || text.Contains("quit")) button.onClick.AddListener(() => PhotonNetwork.LeaveRoom());
            else if (text.Contains("option") || text.Contains("setting")) button.onClick.AddListener(() => { if(options==null)options=new SharedOptionsPanel(view.transform,()=>{}); options.Open(); });
            else button.onClick.AddListener(Close);
        }
        foreach (var role in new[] {CrewRole.Director,CrewRole.Camera,CrewRole.AVTechnician,CrewRole.Editor})
        {
            int index=role==CrewRole.Director?0:role==CrewRole.Camera?1:role==CrewRole.AVTechnician?2:3;
            var button=Button(root,"Crew role "+role,MultiplayerContractManager.RoleName(role),()=>Crew.Send(new CrewCommand {action="role",value=(int)role}));
            Anchor(button,new Vector2(.05f+index*.225f,.025f),new Vector2(.265f+index*.225f,.08f));
        }
    }
}
