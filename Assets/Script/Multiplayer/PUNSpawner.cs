using UnityEngine;
using Photon.Pun;

public class PUNSpawner : MonoBehaviour
{
    public GameObject playerPrefab;

    void Start()
    {
        // We are already in the room! Just spawn the player.
        Vector3 spawnPosition = new Vector3(0, 2f, 0);
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
    }
}