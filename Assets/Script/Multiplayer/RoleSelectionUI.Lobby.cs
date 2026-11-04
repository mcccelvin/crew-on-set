using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed partial class RoleSelectionUI
{
    private GameObject lobbyRoot;
    private readonly GameObject[] lobbyPages = new GameObject[3];
    private readonly Button[] roleButtons = new Button[4], unlockButtons = new Button[4];
    private readonly TMP_Text[] memberLabels = new TMP_Text[4], memberRoles = new TMP_Text[4], memberStates = new TMP_Text[4];
    private readonly Image[] memberCards = new Image[4];
    private readonly CrewVoiceBadge[] memberMicrophones = new CrewVoiceBadge[4];
    private TMP_Text roomCode, memberCount, lobbyNotice, helpDescription;
    private TMP_Text roleTitle, roleDescription, roleControls, localUsername, selectedRoles;
    private readonly TMP_Text[] roleOwners = new TMP_Text[4];
    private readonly Outline[] roleFocus = new Outline[4];
    private readonly bool[] roleWasSelected = new bool[4];
    private AccountAvatarPortrait localAvatar;
    private readonly AccountAvatarPortrait[] memberAvatars = new AccountAvatarPortrait[4];
    private readonly object[] avatarOutfits = new object[5];
    private readonly int[] avatarPlayers = new int[5];
    private int focusedRole;
    private bool fullscreenSelection;
    private CrewSelectionStage selectionStage;
    private Button lockButton, readyButton, startButton, retryButton;
    private Button contractChoice;
    private SharedOptionsPanel lobbyOptions;
    private static readonly CrewRole[] LobbyRoles = { CrewRole.Director, CrewRole.Camera, CrewRole.AVTechnician, CrewRole.Editor };
    private static readonly Color Paper = new Color32(248, 239, 204, 255);
    private static readonly Color Ink = new Color32(22, 35, 54, 255);

    private void CreateLobby()
    {
        if (MultiplayerRoleManager.IsLobbyScene) { CreateStageSelection(); return; }
        // Use the existing Help Desk canvas and its original full-screen artwork.
        // PUNSpawner has already hidden the legacy canvases before creating this controller.
        var desk = gameObject.scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Canvas>(true))
            .FirstOrDefault(c => c.name == "HelpDesk UI");
        if (desk == null) return; // Preserve the connection/error fallback in scenes without this view.
        lobbyRoot = desk.gameObject;
        lobbyRoot.transform.SetParent(null, false);
        lobbyRoot.transform.localScale = Vector3.one;
        desk.renderMode = RenderMode.ScreenSpaceOverlay;
        desk.overrideSorting = true; desk.sortingOrder = 200;
        var scaler = desk.GetComponent<CanvasScaler>() ?? desk.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (desk.GetComponent<GraphicRaycaster>() == null) desk.gameObject.AddComponent<GraphicRaycaster>();
        foreach (Transform child in desk.transform) child.gameObject.SetActive(false);
        if (MultiplayerRoleManager.IsLobbyScene) MainMenuMotion.AddLobbyBackdrop(desk.transform);
        var frame = new GameObject("Crew lobby layout", typeof(RectTransform)).GetComponent<RectTransform>();
        frame.SetParent(desk.transform, false);
        frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(.5f, .5f);
        frame.sizeDelta = new Vector2(1920, 1080);
        for (int i = 0; i < 3; i++)
        {
            var art = desk.transform.Find(new[] { "Start", "Help", "GameSetting" }[i]);
            if (art == null) { lobbyRoot.SetActive(false); lobbyRoot = null; return; }
            art.SetParent(frame, false);
            var rect = (RectTransform)art;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one;
            foreach (Transform old in art) old.gameObject.SetActive(false);
            foreach (var graphic in art.GetComponents<Graphic>()) graphic.raycastTarget = false;
            lobbyPages[i] = art.gameObject;
        }
        // Keep the authored tablet, while giving role selection a character-led layout.
        var start = lobbyPages[0].transform;
        LobbyBlock(start, "Lobby paper", 312, 238, 1296, 508, Paper);
        LobbyBlock(start, "Header cover", 314, 134, 1292, 92, new Color32(39, 78, 132, 255));
        var heading = LobbyText(start, "Lobby heading", 342, 146, 1212, 64, 48, TextAlignmentOptions.Center);
        heading.text = "CHOOSE YOUR CREW ROLE"; ExportUIArt.OutlineText(heading);
        roleTitle = LobbyText(start, "Focused role", 342, 263, 306, 42, 30);
        roleTitle.fontStyle = FontStyles.Bold;
        roleDescription = LobbyText(start, "Role description", 342, 320, 306, 148, 23, TextAlignmentOptions.TopLeft);
        roleControls = LobbyText(start, "Role controls", 342, 480, 306, 52, 19, TextAlignmentOptions.TopLeft);
        selectedRoles = LobbyText(start, "Your selected roles", 342, 535, 306, 44, 19, TextAlignmentOptions.TopLeft);
        roomCode = LobbyText(start, "Room code", 684, 578, 340, 22, 18, TextAlignmentOptions.Center);
        localUsername = LobbyText(start, "Your username", 684, 263, 340, 36, 28, TextAlignmentOptions.Center);
        localUsername.fontStyle = FontStyles.Bold; localUsername.richText = false;
        localAvatar = CreateCrewAvatar(start, "Your customized character", 749, 298, 210, 280);
        LobbyHeading(start, "SELECT ROLES", 1070, 263, 498);
        for (int i = 0; i < LobbyRoles.Length; i++)
        {
            int index = i;
            float x = 1070 + i % 2 * 256, y = 309 + i / 2 * 71;
            roleButtons[i] = LobbyButton(start, LobbyRoles[i] + " role", MultiplayerContractManager.RoleName(LobbyRoles[i]).ToUpperInvariant(),
                x, y, 242, 62, () => ChooseLobbyRole(index));
            var caption = roleButtons[i].GetComponentInChildren<TMP_Text>();
            caption.rectTransform.anchoredPosition = new Vector2(18,-5);
            caption.rectTransform.sizeDelta = new Vector2(206,28);
            caption.fontSize = caption.fontSizeMax = 22; caption.fontSizeMin = 18;
            caption.enableWordWrapping = false;
            roleOwners[i] = LobbyText(roleButtons[i].transform, "Role owner", 12, 34, 218, 22, 16, TextAlignmentOptions.Center);
            roleOwners[i].color = Color.white; roleOwners[i].richText = false;
            var border = roleButtons[i].gameObject.AddComponent<Outline>();
            border.effectColor = CrewPaperStyle.Gold; border.effectDistance = new Vector2(3,-3);
            roleFocus[i] = border;
        }
        lockButton = LobbyButton(start, "Lock roles", "LOCK ROLES", 1070, 454, 242, 50,
            () => Send("lockRoles", LocalMember()?.rolesLocked == true ? 0 : 1));
        readyButton = LobbyButton(start, "Ready", "READY", 1326, 454, 242, 50, () => Send("ready"));
        LobbyButtonArt(readyButton.GetComponent<Image>(), "greenButton");
        contractChoice = LobbyButton(start, "Contract choice", "CONTRACT", 1070, 519, 498, 49, () =>
        {
            if (PhotonNetwork.IsMasterClient && Crew?.State != null)
                Send("contract", MultiplayerContractManager.NextContractChoice(Crew.State.contractLevel));
        });
        LobbyText(start, "Crew heading", 342, 579, 300, 22, 20).text = "YOUR TEAM";
        memberCount = LobbyText(start, "Crew count", 1362, 578, 204, 22, 18, TextAlignmentOptions.Right);
        for (int i = 0; i < 4; i++)
        {
            int row = i; float x = 342 + i * 306;
            var card = LobbyBlock(start, "Crew row " + i, x, 605, 294, 92, new Color32(252,245,220,255));
            CrewPaperStyle.Round(card); UISurfaceDepth.Apply(card); memberCards[i] = card;
            memberAvatars[i] = CreateCrewAvatar(card.transform, "Customized teammate " + i, 8, 9, 66, 66);
            memberLabels[i] = LobbyText(card.transform, "Crew username " + i, 84, 8, 108, 23, 20);
            memberMicrophones[i] = CrewVoiceBadge.Attach(LobbyRect(card.transform,"Crew microphone "+i,196,8,90,23));
            memberLabels[i].fontStyle = FontStyles.Bold; memberLabels[i].richText = false;
            memberRoles[i] = LobbyText(card.transform, "Crew roles " + i, 84, 34, 198, 33, 16, TextAlignmentOptions.TopLeft);
            memberStates[i] = LobbyText(card.transform, "Crew status " + i, 84, 70, 104, 17, 14);
            unlockButtons[i] = LobbyButton(card.transform, "Unlock crew " + i, "UNLOCK", 200, 69, 82, 20, () =>
            {
                var members = Crew?.State?.members;
                if (members != null && row < members.Count)
                    Crew.Send(new CrewCommand { action = "lockRoles", id = members[row].id, value = 0 });
            });
            var unlockText = unlockButtons[i].GetComponentInChildren<TMP_Text>();
            unlockText.rectTransform.offsetMin = new Vector2(3,0); unlockText.rectTransform.offsetMax = new Vector2(-3,0);
            unlockText.fontSize = unlockText.fontSizeMax = 13; unlockText.fontSizeMin = 13;
        }
        startButton = LobbyButton(start, "Host start", "START CONTRACT", 1274, 704, 294, 40, () => Send("start"));
        LobbyButtonArt(startButton.GetComponent<Image>(), "greenButton");
        retryButton = LobbyButton(start, "Retry connection", "RETRY CONNECTION", 1274, 704, 294, 40, () => Crew.RequestRoomState());
        lobbyNotice = LobbyText(start, "Lobby status", 342, 708, 904, 28, 19, TextAlignmentOptions.MidlineLeft);

        var help = lobbyPages[1].transform;
        LobbyBlock(help, "Help paper", 312, 238, 1296, 508, Paper);
        LobbyHeading(help, "CREW ROLES", 342, 265, 480);
        LobbyHeading(help, "HOW TO PLAY", 868, 265, 670);
        helpDescription = LobbyText(help, "Role lesson", 891, 321, 641, 391, 27, TextAlignmentOptions.TopLeft);
        for (int i = 0; i < LobbyRoles.Length; i++)
        {
            int index = i;
            LobbyButton(help, "Help " + LobbyRoles[i], MultiplayerContractManager.RoleName(LobbyRoles[i]),
                350, 340 + i * 91, 465, 70, () => ShowRoleHelp(index));
        }
        ShowRoleHelp(0);
        var settings = lobbyPages[2].transform;
        LobbyBlock(settings, "Settings paper", 312, 238, 1296, 508, Paper);
        LobbyHeading(settings, "YOUR SETTINGS", 405, 265, 1110);
        LobbyText(settings, "Settings explanation", 405, 320, 1110, 110, 32, TextAlignmentOptions.Center).text =
            "Adjust sound effects, music, controls and display for this device.\nYour crew roles and room stay connected.";
        LobbyButton(settings, "Game options", "OPEN GAME OPTIONS", 660, 477, 600, 76, () =>
        {
            if (lobbyOptions == null) lobbyOptions = new SharedOptionsPanel(lobbyRoot.transform, () => { Open = true; });
            lobbyOptions.Open();
        });
        LobbyText(settings, "Room rules", 435, 591, 1050, 104, 27, TextAlignmentOptions.Center).text =
            "2–4 crew members · Four roles · Shared contract budget\nSmaller crews may select several roles. The host chooses a contract and starts.";

        // Transparent click targets match the three painted navigation buttons and the red X.
        LobbyButton(frame, "Help tab", "", 505, 780, 192, 101, () => SelectLobbyPage(1), true);
        LobbyButton(frame, "Lobby tab", "", 862, 780, 193, 101, () => SelectLobbyPage(0), true);
        LobbyButton(frame, "Settings tab", "", 1228, 780, 194, 101, () => SelectLobbyPage(2), true);
        LobbyButton(frame, "Leave room", "", 1688, 106, 80, 79, () =>
        {
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            else LoadingScreenController.LoadScene("Main Menu");
        }, true);
        var eventSystem = EventSystem.current;
        if (eventSystem == null) eventSystem = new GameObject("Crew UI Event System", typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (var legacy in eventSystem.GetComponents<StandaloneInputModule>()) legacy.enabled = false;
        var input = eventSystem.GetComponent<InputSystemUIInputModule>() ?? eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if (input.actionsAsset == null) input.AssignDefaultActions();
        input.enabled = true;
        SelectLobbyPage(0); UITransition.Show(lobbyRoot); desk.enabled = true;
    }

    private CrewMember LocalMember() => PhotonNetwork.InRoom ? Crew?.State?.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber) : null;
    private void SelectLobbyPage(int index)
    {
        for (int i = 0; i < lobbyPages.Length; i++) UITransition.SetVisible(lobbyPages[i], i == index);
    }
    private void ShowRoleHelp(int index)
    {
        string[] lessons = {
            "DIRECTOR\n\nUse the tablet to build the selected contract's set and add products or actors. R rotates a placement. For actor contracts, buy and hold the megaphone to cue movement or interactions inside the stage, then call ACTION.",
            "CAMERA\n\nBuy a camera and blank SD cards. Collect them, hold the camera and press C to insert a card. R records; Contracts 4 and 5 require ACTION first. Follow the selected brief's shots and duration. Deliver recorded cards to the computer.",
            "AV TECHNICIAN\n\nBuy and place lights and audio equipment. Select a light: Z/X adjusts Kelvin, C/V adjusts intensity, F switches power. T repositions equipment; Q/E turns it.",
            "EDITOR\n\nUse the same singleplayer editor to receive recordings, drag/trim/split clips, arrange branding, choose music and adjust color. Follow the selected contract's duration and shot requirements. Export, review and submit to the crew."
        };
        helpDescription.text = lessons[index];
    }
    private void UpdateLobby()
    {
        if (lobbyRoot == null) return;
        bool visible = Crew?.State == null || Crew.State.phase == "lobby";
        if (selectionStage != null && selectionStage.gameObject.activeSelf != visible) selectionStage.gameObject.SetActive(visible);
        if (!visible && lobbyOptions != null && lobbyOptions.IsOpen) lobbyOptions.Close(false);
        if (lobbyRoot.activeSelf != visible) UITransition.SetVisible(lobbyRoot, visible);
        if (!visible) return;
        Open = true;
        if (lobbyOptions != null && lobbyOptions.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) lobbyOptions.Close(false);
        var state = PhotonNetwork.InRoom ? Crew?.State : null; var local = LocalMember();
        roomCode.text = "ROOM  " + (PhotonNetwork.CurrentRoom?.Name ?? "CONNECTING…");
        memberCount.text = (PhotonNetwork.CurrentRoom?.PlayerCount ?? 0) + " / 4 CREW";
        localUsername.text = CrewPlayerIdentity.Username(PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer : null);
        SyncCrewAvatar(localAvatar, PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer : null, 0, true);
        roleTitle.text = MultiplayerContractManager.RoleName(LobbyRoles[focusedRole]).ToUpperInvariant();
        roleDescription.text = RoleSummaries[focusedRole];
        roleControls.text = RoleControls[focusedRole];
        selectedRoles.text = "YOUR ROLES\n" + MultiplayerContractManager.RoleName((CrewRole)(local?.roles ?? 0));
        for (int i = 0; i < roleButtons.Length; i++)
        {
            var role = LobbyRoles[i];
            var owner = state?.members.Find(m => (m.roles & (int)role) != 0);
            bool mine = local != null && owner == local;
            // Assigned/locked roles remain inspectable; only an available role
            // or the local player's unlocked selection sends a role command.
            roleButtons[i].interactable = local != null;
            roleFocus[i].enabled = i == focusedRole;
            Photon.Realtime.Player roleOwner = null;
            if (owner != null) PhotonNetwork.CurrentRoom.Players.TryGetValue(owner.id, out roleOwner);
            roleOwners[i].text = mine ? "SELECTED" : owner != null ? CrewPlayerIdentity.Username(roleOwner) : "AVAILABLE";
            if (roleWasSelected[i] != mine)
            {
                if (fullscreenSelection) roleButtons[i].GetComponent<Image>().color = mine ? (Color)CrewPaperStyle.Gold : (Color)CrewPaperStyle.Paper;
                else LobbyButtonArt(roleButtons[i].GetComponent<Image>(), mine ? "greenButton" : "blueButton");
                roleWasSelected[i] = mine;
            }
            var member = state != null && i < state.members.Count ? state.members[i] : null;
            if (member == null)
            {
                memberLabels[i].text = "OPEN SLOT";
                memberMicrophones[i]?.Bind(null);
                memberRoles[i].text = "No teammate connected";
                memberStates[i].text = "WAITING";
                memberCards[i].color = new Color32(235, 226, 195, 255);
                SyncCrewAvatar(memberAvatars[i], null, i + 1, false);
            }
            else
            {
                PhotonNetwork.CurrentRoom.Players.TryGetValue(member.id, out var player);
                memberMicrophones[i]?.Bind(player);
                string name = CrewPlayerIdentity.Username(player);
                memberLabels[i].text = name + (member == local ? " (YOU)" : PhotonNetwork.MasterClient.ActorNumber == member.id ? " (HOST)" : "");
                memberRoles[i].text = member.roles == 0 ? "Choosing roles…" : MultiplayerContractManager.RoleName((CrewRole)member.roles);
                memberStates[i].text = member.ready ? "READY" : member.rolesLocked ? "LOCKED" : "CHOOSING";
                memberStates[i].color = member.ready ? new Color32(39, 104, 69, 255) : Ink;
                memberCards[i].color = member == local ? new Color32(255, 248, 225, 255) : new Color32(252, 245, 220, 255);
                SyncCrewAvatar(memberAvatars[i], player, i + 1, false);
            }
            unlockButtons[i].gameObject.SetActive(PhotonNetwork.IsMasterClient && member != null && member != local && member.rolesLocked);
        }
        lockButton.interactable = local != null && local.roles != 0;
        readyButton.interactable = local != null && local.rolesLocked;
        SetLobbyButton(lockButton, local?.rolesLocked == true ? "UNLOCK ROLES" : "LOCK ROLES");
        SetLobbyButton(readyButton, local?.ready == true ? "NOT READY" : "READY");
        startButton.gameObject.SetActive(local != null);
        startButton.interactable = PhotonNetwork.IsMasterClient && state != null && MultiplayerContractManager.CanStart(state);
        SetLobbyButton(startButton, "START CONTRACT");
        contractChoice.interactable = PhotonNetwork.IsMasterClient && state != null;
        SetLobbyButton(contractChoice, state == null ? "CONTRACT" : MultiplayerContractManager.Title(state.contractLevel) + "  •  B " + state.budget.ToString("N0"));
        retryButton.gameObject.SetActive(local == null);
        retryButton.interactable = PhotonNetwork.InRoom;
        lobbyNotice.text = local == null ? "Connecting to your crew…" :
            !local.rolesLocked ? "1. Choose your roles, then lock them in." :
            !local.ready ? "2. Press READY when you are set." :
            state != null && state.members.Count < 2 ? "Invite at least one friend to begin." :
            state != null && MultiplayerContractManager.CanStart(state) ? (PhotonNetwork.IsMasterClient ? "Your crew is ready. Start the contract!" : "Ready. Waiting for the host to start.") :
            "Waiting for all four roles and everyone to ready up.";
    }

    private static readonly string[] RoleSummaries = {
        "Shape the commercial. Build the set, place the product and direct your actors. Cue performances and call ACTION when the crew is ready.",
        "Tell the story through the lens. Frame the required shots, manage SD cards and record clear takes. Deliver your footage to the editor.",
        "Give every shot the right light and sound. Place equipment, adjust brightness and color temperature, and support the camera team.",
        "Turn the crew's footage into a finished commercial. Trim and arrange clips, add branding and music, then grade, export and submit."
    };
    private static readonly string[] RoleControls = {
        "E  Tablet     R  Rotate\nENTER  Call ACTION",
        "C  Insert / remove SD\nR  Record     SCROLL  Zoom",
        "Z / X  Kelvin     C / V  Intensity\nF  Power     T  Reposition",
        "SPACE  Play / pause\nDrag clips, trim and arrange"
    };
    private void ChooseLobbyRole(int index)
    {
        focusedRole = index;
        var local = LocalMember();
        if (local == null || local.rolesLocked) return;
        int role = (int)LobbyRoles[index];
        var owner = Crew.State.members.Find(m => (m.roles & role) != 0);
        if (owner == null || owner == local) Send("role", role);
    }
    private static AccountAvatarPortrait CreateCrewAvatar(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = LobbyRect(parent, name, x, y, width, height);
        rect.gameObject.SetActive(false);
        var image = rect.gameObject.AddComponent<RawImage>(); image.raycastTarget = false;
        return rect.gameObject.AddComponent<AccountAvatarPortrait>();
    }
    private void SyncCrewAvatar(AccountAvatarPortrait avatar, Photon.Realtime.Player player, int slot, bool fullBody)
    {
        if (avatar == null) return;
        if (player == null)
        {
            avatar.gameObject.SetActive(false); avatarPlayers[slot] = 0; avatarOutfits[slot] = null; return;
        }
        object outfit = player.CustomProperties[CrewPlayerIdentity.OutfitKey];
        if (avatarPlayers[slot] != player.ActorNumber || !ReferenceEquals(avatarOutfits[slot], outfit))
        {
            avatar.SetCrewAppearance(CrewPlayerIdentity.Outfit(player), fullBody);
            avatarPlayers[slot] = player.ActorNumber; avatarOutfits[slot] = outfit;
        }
        if (!avatar.gameObject.activeSelf) avatar.gameObject.SetActive(true);
    }

    private static RectTransform LobbyRect(Transform parent, string name, float x, float y, float width, float height)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        return rect;
    }
    private static Image LobbyBlock(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var image = LobbyRect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }
    private static TextMeshProUGUI LobbyText(Transform parent, string name, float x, float y, float width, float height, float size, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var label = LobbyRect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto-Bold SDF") ?? TMP_Settings.defaultFontAsset;
        label.fontSize = size; label.color = Ink;
        label.alignment = alignment; label.richText = true; label.raycastTarget = false;
        label.enableWordWrapping = true;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.enableAutoSizing = true; label.fontSizeMin = size * .7f; label.fontSizeMax = size;
        return label;
    }
    private static Button LobbyButton(Transform parent, string name, string text, float x, float y, float width, float height, UnityAction action, bool transparent = false)
    {
        var image = LobbyBlock(parent, name, x, y, width, height, transparent ? Color.clear : new Color32(44, 83, 137, 255));
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; button.onClick.AddListener(action);
        if (!transparent)
        {
            LobbyButtonArt(image, "blueButton");
            var label = LobbyText(image.transform, "Label", 18, 4, width - 36, height - 8, 25, TextAlignmentOptions.Center);
            label.text = text; label.fontStyle = FontStyles.Bold;
            ExportUIArt.OutlineText(label);
            var colors = button.colors;
            colors.normalColor = Color.white; colors.highlightedColor = new Color(1f,.95f,.82f);
            colors.pressedColor = new Color(.78f,.84f,.94f); colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.67f,.69f,.73f,1f); button.colors = colors;
        }
        return button;
    }
    private static void LobbyHeading(Transform parent, string title, float x, float y, float width)
    {
        var label = LobbyText(parent, title, x, y, width, 38, 28);
        label.text = title; label.fontStyle = FontStyles.Bold;
    }
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> lobbyButtonSprites =
        new System.Collections.Generic.Dictionary<string, Sprite>();
    internal static void LobbyButtonArt(Image image, string key)
    {
        ExportUIArt.Apply(image, key);
        if (!lobbyButtonSprites.TryGetValue(key, out var sprite) || sprite == null)
        {
            var source = image.sprite;
            if (source == null) return;
            float border = Mathf.Min(source.rect.height * .45f, source.rect.width * .2f);
            sprite = Sprite.Create(source.texture, source.rect, Vector2.one * .5f, source.pixelsPerUnit,
                0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            lobbyButtonSprites[key] = sprite;
        }
        image.sprite = sprite; image.type = Image.Type.Sliced;
    }
    private static void SetLobbyButton(Button button, string text)
    {
        var label = button.GetComponentInChildren<TMP_Text>();
        if (label.text != text) label.text = text;
    }
}
