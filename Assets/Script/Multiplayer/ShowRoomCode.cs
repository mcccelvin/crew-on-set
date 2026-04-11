using UnityEngine;
using TMPro;
using Photon.Pun;

public class ShowRoomCode : MonoBehaviour
{
    public TextMeshProUGUI codeText;

    void Start()
    {
        // When the scene loads, grab the room name from the Photon Network!
        if (PhotonNetwork.InRoom)
        {
            codeText.text = "ROOM CODE: " + PhotonNetwork.CurrentRoom.Name;
        }
    }
}