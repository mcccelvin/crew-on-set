using UnityEngine;
using Photon.Pun;

public class PUNSpawner : MonoBehaviour
{
    public GameObject playerPrefab;

    void Start()
    {
        if (!PhotonNetwork.InRoom || playerPrefab == null) return;
        MultiplayerPauseManager.isPaused = false;
        PauseManager.isPaused = false;
        foreach (var canvas in FindObjectsOfType<Canvas>()) canvas.gameObject.SetActive(false);
        foreach (var camera in FindObjectsOfType<Camera>()) camera.enabled = false;
        foreach (var listener in FindObjectsOfType<AudioListener>()) listener.enabled = false;
        foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
            if (player.GetComponent<PhotonView>() == null) player.SetActive(false);
        if (MultiplayerRoleManager.Instance == null) gameObject.AddComponent<MultiplayerRoleManager>();
        var stage = StageInterior.FindStagePlatform();
        var center = stage != null ? stage.bounds.center : Vector3.zero;
        float front = stage != null ? stage.bounds.min.z - 3 : -6;
        Vector3 spawnPosition = new Vector3(center.x + (PhotonNetwork.LocalPlayer.ActorNumber % 4 - 1.5f) * 1.5f, center.y + 2, front);
        PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity);
    }
}
