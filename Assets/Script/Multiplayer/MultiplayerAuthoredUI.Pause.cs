using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed partial class MultiplayerAuthoredUI
{
    private string panelBeforePause = "";
    private bool pausedTablet;
    private float nextPauseRefresh;
    private TMP_Text pauseRoom, pauseVacant;
    private readonly TMP_Text[] pauseNames = new TMP_Text[4], pauseRoles = new TMP_Text[4], pauseTags = new TMP_Text[4];
    private readonly Image[] pauseRows = new Image[4];
    private readonly CrewVoiceBadge[] pauseMicrophones = new CrewVoiceBadge[4];
    private readonly Button[] pauseRoleButtons = new Button[4];
    private static readonly CrewRole[] PauseRoles = { CrewRole.Director, CrewRole.Camera, CrewRole.AVTechnician, CrewRole.Editor };
    private Sprite pauseSlats;

    private GameObject ResolvePauseMenu()
    {
        var bound = refs.Get<GameObject>("PauseManager.pauseMenuCanvas");
        if (bound != null) return bound;
        // Older copies have no explicit binding. A playback button is also
        // called Pause: only the real canvas with native menu actions qualifies.
        var canvas = view.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c =>
            c.name == "Pause" && c.transform.Find("Panel/Resume") != null &&
            (c.transform.Find("Panel/Option") != null || c.transform.Find("Panel/Options") != null));
        return canvas != null ? canvas.gameObject : null;
    }

    private void SetupPause()
    {
        var root = panels["pause"];
        if (root == null) { Debug.LogError("The authored pause menu is missing from the studio UI copy."); return; }
        root.transform.localScale = Vector3.one;
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = root.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
        PauseManager.PutOverlayOnTop(root,30000);
        var native = root.transform.Find("Panel");
        Bind(native,"Resume",ResumeFromPause);
        Bind(native,"Option",OpenPauseOptions);
        Bind(native,"Options",OpenPauseOptions);
        Bind(native,"Exit",()=>{ Close(); PhotonNetwork.LeaveRoom(); });
        BuildPauseRoster(root.transform,native != null ? native.GetComponent<Image>() : null);
        root.SetActive(false);
    }

    private void OpenPauseOptions()
    {
        if (Panel != "pause") return;
        if (options == null) options = new SharedOptionsPanel(panels["pause"].transform,()=>{});
        options.Open();
    }

    private void OpenPauseMenu()
    {
        if (Panel == "pause" || !panels.TryGetValue("pause",out var root) || root == null) return;
        panelBeforePause = Panel;
        pausedTablet = stageCamera != null && stageCamera.enabled;
        if (stageCamera != null) stageCamera.enabled = false;
        playing = false;
        if (editorView != null && editorView.activeSelf)
        {
            foreach (var player in editorView.GetComponentsInChildren<TruePixelPlayer>(true))
                if (player.IsPlayingVideo) player.TogglePlayPause();
            editorView.SetActive(false);
        }
        Panel = "pause"; RoleSelectionUI.Open = true;
        MultiplayerPauseManager.isPaused = true;
        Activate(root);
        RefreshPauseRoster();
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        // Room simulation and teammates continue; this is a local menu only.
    }

    private void ResumeFromPause()
    {
        if (Panel != "pause") return;
        if (options != null && options.IsOpen) options.Close(false);
        panels["pause"].SetActive(false);
        Panel = panelBeforePause;
        if (stageCamera != null) stageCamera.enabled = pausedTablet && Panel == "tablet";
        if (editorView != null && Panel == "editor") editorView.SetActive(true);
        ResetPauseState();
        lastPlayback = Time.unscaledTime;
        RoleSelectionUI.Open = Panel != "";
        UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(null);
        Cursor.lockState = RoleSelectionUI.Open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = RoleSelectionUI.Open;
    }

    private void ResetPauseState()
    {
        MultiplayerPauseManager.isPaused = false;
        panelBeforePause = ""; pausedTablet = false;
    }

    private bool HandlePauseMenu(Keyboard keyboard)
    {
        bool escape = Application.isFocused && keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !DevCommandsPanel.BlocksInputThisFrame;
        if (escape)
        {
            if (Panel == "pause" && options != null && options.IsOpen) options.Close(false);
            else if (Panel == "pause") ResumeFromPause();
            else OpenPauseMenu();
            return true;
        }
        if (Panel != "pause") return false;
        if (Time.unscaledTime >= nextPauseRefresh) RefreshPauseRoster();
        RoleSelectionUI.Open = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        return true;
    }

    private void BuildPauseRoster(Transform root,Image nativeBackground)
    {
        var roster = PauseRect(root,"Multiplayer lobby",new Vector2(-340,0),new Vector2(800,660));
        var background = roster.gameObject.AddComponent<Image>();
        background.color = new Color32(13,21,23,255);
        // Reuse a blank section of the native shutter artwork behind the roster.
        // The original pause frame and its three painted action buttons stay intact.
        if (nativeBackground != null && nativeBackground.sprite != null)
        {
            var source = nativeBackground.sprite;
            var region = source.textureRect;
            region = new Rect(region.x+region.width*.79f,region.y+region.height*.27f,region.width*.1f,region.height*.45f);
            pauseSlats = Sprite.Create(source.texture,region,new Vector2(.5f,.5f),source.pixelsPerUnit);
            pauseSlats.name = "Pause shutter lobby background";
            background.sprite = pauseSlats; background.color = Color.white;
        }
        var shadow = roster.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0,0,0,.65f); shadow.effectDistance = new Vector2(6,-6);
        var border = roster.gameObject.AddComponent<Outline>();
        border.effectColor = new Color32(22,64,69,255); border.effectDistance = new Vector2(3,-3);
        PauseLabel(roster,"Lobby title","LOBBY",new Vector2(0,269),new Vector2(720,55),40,TextAlignmentOptions.Left);
        pauseRoom = PauseLabel(roster,"Room code","",new Vector2(0,220),new Vector2(720,35),24,TextAlignmentOptions.Left);
        var divider = PauseRect(roster,"Header edge",new Vector2(0,193),new Vector2(720,3));
        divider.gameObject.AddComponent<Image>().color = new Color32(70,105,109,255);
        for (int i=0;i<4;i++)
        {
            var row = PauseRect(roster,"Crew slot "+i,new Vector2(0,139-i*100),new Vector2(720,88));
            pauseRows[i] = row.gameObject.AddComponent<Image>();
            var stripe = PauseRect(row,"Edge highlight",new Vector2(-357,0),new Vector2(5,88));
            stripe.gameObject.AddComponent<Image>().color = new Color32(81,126,130,255);
            pauseNames[i] = PauseLabel(row,"Username","",new Vector2(-83,17),new Vector2(490,35),29,TextAlignmentOptions.Left);
            pauseNames[i].richText = false;
            pauseRoles[i] = PauseLabel(row,"Roles","",new Vector2(-65,-22),new Vector2(520,36),21,TextAlignmentOptions.Left);
            pauseMicrophones[i]=CrewVoiceBadge.Attach(PauseRect(row,"Crew microphone "+i,new Vector2(275,-22),new Vector2(115,27)));
            pauseTags[i] = PauseLabel(row,"Crew badge","",new Vector2(259,17),new Vector2(145,32),19,TextAlignmentOptions.Right);
        }
        pauseVacant = PauseLabel(roster,"Role availability","",new Vector2(0,-233),new Vector2(720,27),18,TextAlignmentOptions.Left);
        for (int i=0;i<4;i++)
        {
            var role = PauseRoles[i];
            var button = Button(roster,"Take vacant "+role,MultiplayerContractManager.RoleName(role).ToUpperInvariant(),
                ()=>Crew.Send(new CrewCommand { action="role",value=(int)role }));
            var rect = (RectTransform)button.transform;
            rect.anchoredPosition = new Vector2(-270+i*180,-278); rect.sizeDelta = new Vector2(174,46);
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontSize = 18; label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 18;
            ExportUIArt.OutlineText(label);
            pauseRoleButtons[i] = button;
        }
    }

    private void RefreshPauseRoster()
    {
        nextPauseRefresh = Time.unscaledTime+.25f;
        if (pauseRoom == null || !PhotonNetwork.InRoom || Crew?.State == null) return;
        var players = PhotonNetwork.PlayerList.OrderBy(p=>p.ActorNumber).Take(4).ToArray();
        pauseRoom.text = "ROOM "+PhotonNetwork.CurrentRoom.Name+"   •   "+players.Length+" / "+PhotonNetwork.CurrentRoom.MaxPlayers+" CREW";
        for (int i=0;i<4;i++)
        {
            var player = i<players.Length ? players[i] : null;
            pauseMicrophones[i]?.Bind(player);
            var member = player != null ? Crew.State.members.Find(m=>m.id==player.ActorNumber) : null;
            pauseNames[i].text = player != null ? CrewPlayerIdentity.Username(player) : "EMPTY SLOT";
            pauseRoles[i].text = member != null && member.roles != 0 ? MultiplayerContractManager.RoleName((CrewRole)member.roles) :
                player != null ? "Assigning roles…" : "No teammate connected";
            bool mine = player != null && player.IsLocal;
            pauseRows[i].color = mine ? new Color32(20,62,68,245) : new Color32(21,30,33,235);
            pauseTags[i].text = player == null ? "" : player.IsMasterClient ? (mine?"HOST • YOU":"HOST") : mine?"YOU":"CREW";
            pauseTags[i].color = mine ? new Color32(152,225,228,255) : Color.white;
            pauseNames[i].color = player != null ? Color.white : new Color32(145,158,161,255);
            pauseRoles[i].color = new Color32(189,203,206,255);
        }
        bool vacant = false;
        for (int i=0;i<4;i++)
        {
            bool available = !Crew.State.members.Any(m=>(m.roles&(int)PauseRoles[i])!=0);
            pauseRoleButtons[i].gameObject.SetActive(available);
            pauseRoleButtons[i].interactable = available && !Crew.State.recording && !Crew.LocalIsReadingBriefing;
            vacant |= available;
        }
        pauseVacant.text = vacant ? "AVAILABLE ROLES" : "ALL CREW ROLES ASSIGNED";
    }

    private static RectTransform PauseRect(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f,.5f);
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }

    private static TMP_Text PauseLabel(Transform parent,string name,string value,Vector2 position,Vector2 size,float fontSize,TextAlignmentOptions alignment)
    {
        var label = PauseRect(parent,name,position,size).gameObject.AddComponent<TextMeshProUGUI>();
        label.text = value; label.fontSize = fontSize; label.alignment = alignment;
        label.enableAutoSizing = true; label.fontSizeMin = fontSize-4; label.fontSizeMax = fontSize;
        label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false; ExportUIArt.OutlineText(label); return label;
    }
}
