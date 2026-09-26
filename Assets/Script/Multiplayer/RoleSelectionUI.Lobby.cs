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
    private readonly TMP_Text[] memberLabels = new TMP_Text[4];
    private TMP_Text roomCode, memberCount, lobbyNotice, helpDescription;
    private Button lockButton, readyButton, startButton, retryButton;
    private SharedOptionsPanel lobbyOptions;
    private static readonly CrewRole[] LobbyRoles = { CrewRole.Director, CrewRole.Camera, CrewRole.AVTechnician, CrewRole.Editor };
    private static readonly Color Paper = new Color32(235, 223, 167, 255);
    private static readonly Color Ink = new Color32(22, 35, 54, 255);

    private void CreateLobby()
    {
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
        // Cover only sample text baked into the artwork, retaining the frame, titles and tabs.
        var start = lobbyPages[0].transform;
        LobbyBlock(start, "Room content", 330, 330, 607, 400, Paper);
        LobbyBlock(start, "Crew content", 998, 330, 565, 400, Paper);
        LobbyBlock(start, "Room code tile", 342, 345, 585, 82, new Color32(116, 111, 80, 255));
        roomCode = LobbyText(start, "Room code", 342, 350, 585, 68, 44, TextAlignmentOptions.Center);
        roomCode.color = Color.white;
        memberCount = LobbyText(start, "Crew count", 1280, 277, 270, 48, 27, TextAlignmentOptions.Right);
        for (int i = 0; i < LobbyRoles.Length; i++)
        {
            var role = LobbyRoles[i];
            roleButtons[i] = LobbyButton(start, role + " role", MultiplayerContractManager.RoleName(role),
                342 + i % 2 * 300, 445 + i / 2 * 54, 285, 46, () => Send("role", (int)role));
            int row = i;
            LobbyBlock(start, "Crew row " + i, 1006, 337 + i * 85, 548, 78, new Color32(175, 167, 126, 255));
            memberLabels[i] = LobbyText(start, "Crew member " + i, 1018, 340 + i * 85, 420, 72, 23);
            unlockButtons[i] = LobbyButton(start, "Unlock crew " + i, "UNLOCK", 1440, 359 + i * 85, 104, 34, () =>
            {
                var members = Crew?.State?.members;
                if (members != null && row < members.Count)
                    Crew.Send(new CrewCommand { action = "lockRoles", id = members[row].id, value = 0 });
            });
            unlockButtons[i].GetComponentInChildren<TMP_Text>().fontSize = 17;
        }
        lockButton = LobbyButton(start, "Lock roles", "LOCK ROLES", 342, 559, 285, 47,
            () => Send("lockRoles", LocalMember()?.rolesLocked == true ? 0 : 1));
        readyButton = LobbyButton(start, "Ready", "READY", 642, 559, 285, 47, () => Send("ready"));
        lobbyNotice = LobbyText(start, "Lobby status", 342, 613, 585, 54, 21, TextAlignmentOptions.Center);
        startButton = LobbyButton(start, "Host start", "START COFFEE COMMERCIAL", 342, 678, 585, 44, () => Send("start"));
        retryButton = LobbyButton(start, "Retry connection", "RETRY CONNECTION", 342, 678, 585, 44, () => Crew.RequestRoomState());
        LobbyText(start, "Role help", 1006, 685, 548, 42, 21, TextAlignmentOptions.Center).text = "Choose roles  >  LOCK ROLES  >  READY";

        var help = lobbyPages[1].transform;
        LobbyBlock(help, "Current roles", 342, 328, 480, 392, Paper);
        LobbyBlock(help, "Role instructions", 868, 268, 696, 461, Paper);
        helpDescription = LobbyText(help, "Role lesson", 891, 290, 641, 408, 29, TextAlignmentOptions.TopLeft);
        for (int i = 0; i < LobbyRoles.Length; i++)
        {
            int index = i;
            LobbyButton(help, "Help " + LobbyRoles[i], MultiplayerContractManager.RoleName(LobbyRoles[i]),
                350, 340 + i * 91, 465, 70, () => ShowRoleHelp(index));
        }
        ShowRoleHelp(0);
        var settings = lobbyPages[2].transform;
        LobbyText(settings, "Settings explanation", 405, 320, 1110, 110, 32, TextAlignmentOptions.Center).text =
            "Adjust sound effects, music, controls and display for this device.\nYour crew roles and room stay connected.";
        LobbyButton(settings, "Game options", "OPEN GAME OPTIONS", 660, 477, 600, 76, () =>
        {
            if (lobbyOptions == null) lobbyOptions = new SharedOptionsPanel(lobbyRoot.transform, () => { Open = true; });
            lobbyOptions.Open();
        });
        LobbyText(settings, "Room rules", 435, 591, 1050, 104, 27, TextAlignmentOptions.Center).text =
            "2–4 crew members · Four roles · Shared B20,000 budget\nSmaller crews may select several roles. Only the host starts.";

        // Transparent click targets match the three painted navigation buttons and the red X.
        LobbyButton(frame, "Help tab", "", 505, 780, 192, 101, () => SelectLobbyPage(1), true);
        LobbyButton(frame, "Lobby tab", "", 862, 780, 193, 101, () => SelectLobbyPage(0), true);
        LobbyButton(frame, "Settings tab", "", 1228, 780, 194, 101, () => SelectLobbyPage(2), true);
        LobbyButton(frame, "Leave room", "", 1688, 106, 80, 79, () =>
        {
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            else UnityEngine.SceneManagement.SceneManager.LoadScene("Main Menu");
        }, true);
        var eventSystem = EventSystem.current;
        if (eventSystem == null) eventSystem = new GameObject("Crew UI Event System", typeof(EventSystem)).GetComponent<EventSystem>();
        foreach (var legacy in eventSystem.GetComponents<StandaloneInputModule>()) legacy.enabled = false;
        var input = eventSystem.GetComponent<InputSystemUIInputModule>() ?? eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        if (input.actionsAsset == null) input.AssignDefaultActions();
        input.enabled = true;
        SelectLobbyPage(0); lobbyRoot.SetActive(true); desk.enabled = true;
    }

    private CrewMember LocalMember() => PhotonNetwork.InRoom ? Crew?.State?.members.Find(m => m.id == PhotonNetwork.LocalPlayer.ActorNumber) : null;
    private void SelectLobbyPage(int index)
    {
        for (int i = 0; i < lobbyPages.Length; i++) lobbyPages[i].SetActive(i == index);
    }
    private void ShowRoleHelp(int index)
    {
        string[] lessons = {
            "DIRECTOR\n\nUse the tablet to choose the set, add actors and products. Buy and collect a megaphone to cue movement, sitting and holding coffee. Call ACTION when the crew is ready.",
            "CAMERA\n\nBuy a camera and blank SD cards. Collect them, hold the camera and press C to insert a card. After ACTION, use R to record Wide, Medium and Close shots. Deliver recorded cards to the computer.",
            "AV TECHNICIAN\n\nBuy and place lights and audio equipment. Select a light: Z/X adjusts Kelvin, C/V adjusts intensity, F switches power. T repositions equipment; Q/E turns it.",
            "EDITOR\n\nUse the computer to receive and download recordings. Trim and order shots, add branding and adjust color. Include five seconds each of Wide, Medium and Close coverage, then submit."
        };
        helpDescription.text = lessons[index];
    }
    private void UpdateLobby()
    {
        if (lobbyRoot == null) return;
        bool visible = Crew?.State == null || Crew.State.phase == "lobby";
        if (!visible && lobbyOptions != null && lobbyOptions.IsOpen) lobbyOptions.Close(false);
        if (lobbyRoot.activeSelf != visible) lobbyRoot.SetActive(visible);
        if (!visible) return;
        Open = true;
        if (lobbyOptions != null && lobbyOptions.IsOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) lobbyOptions.Close(false);
        var state = PhotonNetwork.InRoom ? Crew?.State : null; var local = LocalMember();
        roomCode.text = PhotonNetwork.CurrentRoom?.Name ?? "CONNECTING...";
        memberCount.text = (PhotonNetwork.CurrentRoom?.PlayerCount ?? 0) + " / 4 CREW";
        for (int i = 0; i < roleButtons.Length; i++)
        {
            var role = LobbyRoles[i];
            var owner = state?.members.Find(m => (m.roles & (int)role) != 0);
            bool mine = local != null && owner == local;
            roleButtons[i].interactable = local != null && !local.rolesLocked && (owner == null || mine);
            SetLobbyButton(roleButtons[i], MultiplayerContractManager.RoleName(role) + (mine ? "  [YOU]" : owner != null ? "  [TAKEN]" : ""));
            var member = state != null && i < state.members.Count ? state.members[i] : null;
            if (member == null) memberLabels[i].text = "Waiting for crew...";
            else
            {
                PhotonNetwork.CurrentRoom.Players.TryGetValue(member.id, out var player);
                string name = string.IsNullOrWhiteSpace(player?.NickName) ? "Player " + member.id : player.NickName;
                if (name.Length > 20) name = name.Substring(0, 19) + "…";
                memberLabels[i].text = name + (PhotonNetwork.MasterClient.ActorNumber == member.id ? " (HOST)" : "") + "\n" +
                    MultiplayerContractManager.RoleName((CrewRole)member.roles) + "\n" + (member.ready ? "READY" : member.rolesLocked ? "ROLES LOCKED" : "CHOOSING ROLES");
            }
            unlockButtons[i].gameObject.SetActive(PhotonNetwork.IsMasterClient && member != null && member != local && member.rolesLocked);
        }
        lockButton.interactable = local != null && local.roles != 0;
        readyButton.interactable = local != null && local.rolesLocked;
        SetLobbyButton(lockButton, local?.rolesLocked == true ? "UNLOCK ROLES" : "LOCK ROLES");
        SetLobbyButton(readyButton, local?.ready == true ? "NOT READY" : "READY");
        startButton.gameObject.SetActive(local != null);
        startButton.interactable = PhotonNetwork.IsMasterClient && state != null && MultiplayerContractManager.CanStart(state);
        SetLobbyButton(startButton, PhotonNetwork.IsMasterClient ? "START COFFEE COMMERCIAL" : "WAITING FOR HOST TO START");
        retryButton.gameObject.SetActive(local == null);
        retryButton.interactable = PhotonNetwork.InRoom;
        lobbyNotice.text = local == null ? (Time.unscaledTime - openedAt > 15 ? "Waiting for host. Retry or create a fresh room using the same updated game." : "Joining the host's crew lobby...") : Crew.Notice;
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
    private static TMP_Text LobbyText(Transform parent, string name, float x, float y, float width, float height, float size, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        var label = LobbyRect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = TMP_Settings.defaultFontAsset; label.fontSize = size; label.color = Ink;
        label.alignment = alignment; label.richText = false; label.raycastTarget = false;
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
            var outline = image.gameObject.AddComponent<Outline>(); outline.effectColor = Ink; outline.effectDistance = new Vector2(2, -2);
            var label = LobbyText(image.transform, "Label", 8, 2, width - 16, height - 4, 23, TextAlignmentOptions.Center);
            label.text = text; label.color = Color.white;
        }
        return button;
    }
    private static void SetLobbyButton(Button button, string text) => button.GetComponentInChildren<TMP_Text>().text = text;
}
