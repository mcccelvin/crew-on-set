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

        if (singlePlayerToggle.isOn)
        {
            // 1. SINGLEPLAYER LOGIC
            Debug.Log("Starting Singleplayer...");
            if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
            var saves=GameSaveManager.Ensure();
            if(saves.Syncing){if(errorText!=null)errorText.text="Please wait for save sync to finish.";return;}
            saves.StartGame(saves.CreateGame(gameNameInput.text),true);
        }
        else if (multiPlayerToggle.isOn)
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
        // There are two menu components; only dispatch the host's scene load once.
        if (PhotonNetwork.IsMasterClient && loadingRoom != PhotonNetwork.CurrentRoom.Name)
        {
            loadingRoom = PhotonNetwork.CurrentRoom.Name;
            PhotonNetwork.LoadLevel(multiplayerScene);
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
