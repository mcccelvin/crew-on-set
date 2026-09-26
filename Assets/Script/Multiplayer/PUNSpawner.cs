using UnityEngine;
using Photon.Pun;
using System.Collections;

public class PUNSpawner : MonoBehaviour
{
    public GameObject playerPrefab;
    public string Status { get; private set; }
    private GameObject lobbyCamera;

    IEnumerator Start()
    {
        Application.runInBackground = true;
        Time.timeScale = 1f;
        MultiplayerPauseManager.isPaused = false;
        PauseManager.isPaused = false;
        foreach (var canvas in FindObjectsOfType<Canvas>())
            if (canvas.gameObject.scene == gameObject.scene && canvas.GetComponentInParent<PhotonView>() == null)
                canvas.gameObject.SetActive(false);
        foreach (var camera in FindObjectsOfType<Camera>())
            if (camera.gameObject.scene == gameObject.scene && camera.GetComponentInParent<PhotonView>() == null)
                camera.enabled = false;
        foreach (var listener in FindObjectsOfType<AudioListener>())
            if (listener.gameObject.scene == gameObject.scene && listener.GetComponentInParent<PhotonView>() == null)
                listener.enabled = false;
        foreach (var player in GameObject.FindGameObjectsWithTag("Player"))
            if (player.gameObject.scene == gameObject.scene && player.GetComponent<PhotonView>() == null) player.SetActive(false);
        if (MultiplayerRoleManager.Instance == null)
            new GameObject("Crew Session").AddComponent<MultiplayerRoleManager>();
        var stage = StageInterior.FindStagePlatform();
        var center = stage != null ? stage.bounds.center : Vector3.zero;
        float front = stage != null ? stage.bounds.min.z - 3 : -6;
        // Keep the lobby visible while Photon finishes joining or reports a spawn failure.
        if (FindLocalPlayer() == null)
        {
            lobbyCamera = new GameObject("Crew Lobby Camera");
            lobbyCamera.transform.position = new Vector3(center.x, center.y + 2, front);
            lobbyCamera.transform.LookAt(center + Vector3.up);
            lobbyCamera.AddComponent<Camera>();
            lobbyCamera.AddComponent<AudioListener>();
        }
        Status = "Connecting to the room...";
        while (!PhotonNetwork.InRoom) yield return null;
        MultiplayerRoleManager.Instance.RequestRoomState();
        var local = FindLocalPlayer();
        if (local == null)
        {
            if (playerPrefab == null) playerPrefab = Resources.Load<GameObject>("Player");
            if (playerPrefab == null) { Status = "Player prefab is missing. Leave and check the multiplayer Player prefab."; yield break; }
            Vector3 spawnPosition = new Vector3(center.x + (PhotonNetwork.LocalPlayer.ActorNumber % 4 - 1.5f) * 1.5f, center.y + 2, front);
            GameObject spawned = null;
            try { spawned = PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, Quaternion.identity); }
            catch (System.Exception ex) { Debug.LogException(ex); }
            local = spawned != null ? spawned.GetComponent<MultiplayerPlayerController>() : null;
        }
        if (local == null || !local.ActivateOwnedView())
        { Status = "Local player camera could not start. Leave and rejoin using the updated game."; yield break; }
        Status = "";
        if (lobbyCamera != null) { lobbyCamera.SetActive(false); Destroy(lobbyCamera); }
    }

    private MultiplayerPlayerController FindLocalPlayer()
    {
        foreach (var player in FindObjectsOfType<MultiplayerPlayerController>())
            if (player.photonView.IsMine) return player;
        return null;
    }

    private void OnDestroy() { if (lobbyCamera != null) Destroy(lobbyCamera); }
}
