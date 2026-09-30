using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

public class GameSetupMenu : MonoBehaviourPunCallbacks
{
    [Header("Create Game UI")]
    public TMP_InputField gameNameInput; // The host's game name
    public Toggle singlePlayerToggle;
    public Toggle multiPlayerToggle;
    public TMP_Dropdown gameModeDropdown;

    public void ResetGameMode()
    {
        EnsureGameModeDropdown();
        if (gameModeDropdown != null)
        {
            gameModeDropdown.Hide();
            gameModeDropdown.SetValueWithoutNotify(0);
            gameModeDropdown.RefreshShownValue();
        }
        if (singlePlayerToggle != null) singlePlayerToggle.isOn = true;
    }

    private void EnsureGameModeDropdown()
    {
        if (singlePlayerToggle == null) return;
        var source = singlePlayerToggle.GetComponent<RectTransform>();
        if (gameModeDropdown == null)
        {
            var existing = source.parent.Find("Game Mode");
            if (existing != null) gameModeDropdown = existing.GetComponent<TMP_Dropdown>();
        }
        if (gameModeDropdown == null)
        {
            var root = ModeRect("Game Mode", source.parent, source.anchorMin, source.anchorMax);
            root.pivot = source.pivot;
            root.sizeDelta = source.sizeDelta;
            root.anchoredPosition = source.anchoredPosition;
            root.localScale = source.localScale;
            var background = root.gameObject.AddComponent<Image>();
            var art = singlePlayerToggle.targetGraphic as Image;
            if (art != null) { background.sprite = art.sprite; background.type = art.type; }
            background.color = Color.white;
            gameModeDropdown = root.gameObject.AddComponent<TMP_Dropdown>();
            gameModeDropdown.targetGraphic = background;
            var sourceText = singlePlayerToggle.GetComponentInChildren<TMP_Text>(true);
            var caption = ModeText("Selected Mode", root, new Vector2(.04f, 0), new Vector2(.87f, 1), sourceText);
            caption.text = "SINGLEPLAYER";
            var arrow = ModeText("Arrow", root, new Vector2(.88f, 0), new Vector2(.98f, 1), sourceText);
            arrow.text = "▼";

            var template = ModeRect("Template", root, Vector2.zero, new Vector2(1, 0));
            template.pivot = new Vector2(.5f, 1);
            template.sizeDelta = new Vector2(0, 100);
            template.gameObject.AddComponent<Image>().color = new Color32(255, 244, 210, 255);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = ModeRect("Viewport", template, Vector2.zero, Vector2.one);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = ModeRect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = new Vector2(0, 48);
            var item = ModeRect("Item", content, new Vector2(0, .5f), new Vector2(1, .5f));
            item.sizeDelta = new Vector2(0, 48);
            var itemBackground = item.gameObject.AddComponent<Image>();
            itemBackground.color = Color.white;
            var toggle = item.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = itemBackground;
            var colors = toggle.colors;
            colors.normalColor = new Color32(255, 244, 210, 255);
            colors.highlightedColor = new Color32(238, 210, 148, 255);
            colors.selectedColor = colors.highlightedColor;
            toggle.colors = colors;
            var check = ModeRect("Selected", item, new Vector2(.025f, .3f), new Vector2(.065f, .7f));
            toggle.graphic = check.gameObject.AddComponent<Image>();
            toggle.graphic.color = new Color32(110, 68, 20, 255);
            var itemText = ModeText("Mode", item, new Vector2(.09f, 0), new Vector2(.96f, 1), sourceText);
            itemText.color = new Color32(75, 43, 19, 255);
            scroll.viewport = viewport;
            scroll.content = content;
            gameModeDropdown.template = template;
            gameModeDropdown.captionText = caption;
            gameModeDropdown.itemText = itemText;
            gameModeDropdown.options = new System.Collections.Generic.List<TMP_Dropdown.OptionData>
            {
                new TMP_Dropdown.OptionData("SINGLEPLAYER"),
                new TMP_Dropdown.OptionData("MULTIPLAYER")
            };
            template.gameObject.SetActive(false);
            gameModeDropdown.RefreshShownValue();
        }
        singlePlayerToggle.gameObject.SetActive(false);
        if (multiPlayerToggle != null) multiPlayerToggle.gameObject.SetActive(false);
    }

    private static RectTransform ModeRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI ModeText(string name, Transform parent, Vector2 min, Vector2 max, TMP_Text style)
    {
        var label = ModeRect(name, parent, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = style != null ? style.font : TMP_Settings.defaultFontAsset;
        label.fontSize = style != null ? style.fontSize : 24;
        label.color = style != null ? style.color : Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = label.fontSize;
        label.raycastTarget = false;
        return label;
    }

    [Header("Join Game UI")]
    public TMP_InputField joinCodeInput; // Where the friend types the 5-digit code

    [Header("Feedback UI")]
    public TextMeshProUGUI errorText;    // Text that says "Room not found"

    [Header("Scene Names")]
    public int singlePlayerScene = 2;
    public int multiplayerScene = 8;
    private static string loadingRoom;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRoomLoad() { loadingRoom = null; }

    private void Start()
    {
        ResetGameMode();
        if (!PhotonNetwork.InRoom) loadingRoom = null;
        // Clear the error text when the menu opens
        if (errorText != null) errorText.text = "";

        PhotonNetwork.AutomaticallySyncScene = true;

        // Connect to Photon in the background
        if (!PhotonNetwork.IsConnected)
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    // --- Wire this to your START / CREATE button ---
    public void OnCreateButtonPressed()
    {
        if (errorText != null) errorText.text = "";

        EnsureGameModeDropdown();
        bool multiplayer = gameModeDropdown != null ? gameModeDropdown.value == 1 :
            multiPlayerToggle != null && multiPlayerToggle.isOn;
        if (!multiplayer)
        {
            // 1. SINGLEPLAYER LOGIC
            Debug.Log("Starting Singleplayer...");
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            var saves=GameSaveManager.Ensure();
            if(saves.Syncing){if(errorText!=null)errorText.text="Please wait for save sync to finish.";return;}
            saves.StartGame(saves.CreateGame(gameNameInput.text),true);
        }
        else
        {
            // 2. MULTIPLAYER HOST LOGIC
            if (!PhotonNetwork.IsConnectedAndReady)
            {
                if (errorText != null) errorText.text = "Still connecting to servers...";
                return;
            }

            // Generate a random 5-character code for the room
            string randomRoomCode = GenerateRoomCode(5);
            Debug.Log("Creating Room with Code: " + randomRoomCode);

            // Create the room using that 5-digit code
            RoomOptions options = new RoomOptions { MaxPlayers = 4 };
            PhotonNetwork.CreateRoom(randomRoomCode, options, TypedLobby.Default);
        }
    }

    // --- Wire this to your new JOIN button ---
    public void OnJoinButtonPressed()
    {
        if (errorText != null) errorText.text = "";

        // Get the code the player typed and force it to uppercase
        string codeToJoin = joinCodeInput.text.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(codeToJoin))
        {
            if (errorText != null) errorText.text = "Please enter a code!";
            return;
        }

        if (!PhotonNetwork.IsConnectedAndReady)
        {
            if (errorText != null) errorText.text = "Connecting to servers...";
            return;
        }

        Debug.Log("Attempting to join room: " + codeToJoin);
        if (errorText != null) errorText.text = "Joining room " + codeToJoin + "...";
        if (!PhotonNetwork.JoinRoom(codeToJoin) && errorText != null)
            errorText.text = "Could not start joining. Wait for the connection, then try again.";
    }

    // --- PUN 2 CALLBACKS ---

    public override void OnJoinedRoom()
    {
        Debug.Log("Successfully connected to room!");
        if (errorText != null) errorText.text = "Joined. Opening the crew lobby...";
        Application.runInBackground = true;
        LoadingScreenController.ShowNetworkLoading();
        // There are two menu components; only dispatch the host's scene load once.
        if (PhotonNetwork.IsMasterClient && loadingRoom != PhotonNetwork.CurrentRoom.Name)
        {
            loadingRoom = PhotonNetwork.CurrentRoom.Name;
            LoadingScreenController.LoadNetworkScene(multiplayerScene);
        }
    }

    // If Photon cannot find the room code, this runs automatically!
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join: " + message);
        if (errorText != null) errorText.text = "Could not join: " + message;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (errorText != null) errorText.text = "Failed to create room. Try again.";
    }

    public override void OnLeftRoom() { loadingRoom = null; }
    public override void OnDisconnected(DisconnectCause cause)
    {
        loadingRoom = null;
        if (errorText != null) errorText.text = "Disconnected: " + cause + ". Reconnect and try again.";
    }

    // --- RANDOM CODE GENERATOR ---
    private string GenerateRoomCode(int length)
    {
        // Letters and numbers to make guessing harder!
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string code = "";
        for (int i = 0; i < length; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }
}
