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

    private void Start()
    {
        // Clear the error text when the menu opens
        if (errorText != null) errorText.text = "";

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
            SceneManager.LoadScene(singlePlayerScene);
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
        string codeToJoin = joinCodeInput.text.ToUpper();

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
        PhotonNetwork.JoinRoom(codeToJoin);
    }

    // --- PUN 2 CALLBACKS ---

    public override void OnJoinedRoom()
    {
        Debug.Log("Successfully connected to room!");
        PhotonNetwork.LoadLevel(multiplayerScene);
    }

    // If Photon cannot find the room code, this runs automatically!
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log("Failed to join: " + message);
        if (errorText != null) errorText.text = "Room not found! Check the code.";
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (errorText != null) errorText.text = "Failed to create room. Try again.";
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