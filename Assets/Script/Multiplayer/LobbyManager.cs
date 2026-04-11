using UnityEngine;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    public TMP_InputField roomInputField;
    public GameObject joinButton;
    public GameObject createButton;
    public TextMeshProUGUI statusText;

    void Start()
    {
        joinButton.SetActive(false);
        createButton.SetActive(false);
        statusText.text = "Connecting to Servers...";

        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        statusText.text = "Connected! Joining Lobby...";
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        statusText.text = "Create a room or enter a code to join!";
        joinButton.SetActive(true);
        createButton.SetActive(true);
    }

    // --- NEW: Triggered by your CREATE button ---
    public void CreateRandomRoom()
    {
        createButton.SetActive(false);
        joinButton.SetActive(false);
        statusText.text = "Generating Room...";

        // Generate a random 5-character code
        string randomCode = GenerateRoomCode(5);

        RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 };
        PhotonNetwork.CreateRoom(randomCode, roomOptions, TypedLobby.Default);
    }

    // --- NEW: Triggered by your JOIN button ---
    public void JoinExistingRoom()
    {
        if (string.IsNullOrEmpty(roomInputField.text))
        {
            statusText.text = "Please enter a Room Code first!";
            return;
        }

        createButton.SetActive(false);
        joinButton.SetActive(false);
        statusText.text = "Joining Room...";

        // Forces the input to uppercase so it matches the generated code perfectly
        PhotonNetwork.JoinRoom(roomInputField.text.ToUpper());
    }

    public override void OnJoinedRoom()
    {
        statusText.text = "Room Joined! Loading Sandbox...";
        PhotonNetwork.LoadLevel("MultiplayerSandbox");
    }

    // If the player types a wrong code, turn the buttons back on!
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        statusText.text = "Failed to join: Invalid Code!";
        joinButton.SetActive(true);
        createButton.SetActive(true);
    }

    // --- THE RANDOM GENERATOR ---
    private string GenerateRoomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < length; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }
}