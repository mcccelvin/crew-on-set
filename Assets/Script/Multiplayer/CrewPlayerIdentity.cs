using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Share public display identity and equipped cosmetic IDs, never account tokens
// or cosmetic ownership. Role commands do not modify these properties.
public sealed class CrewPlayerIdentity : MonoBehaviourPunCallbacks
{
    public const string OutfitKey = "COS.Outfit.V1";
    private static CrewPlayerIdentity instance;
    private CCoinService cosmetics;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { instance = null; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var root = new GameObject("Crew public identity");
        DontDestroyOnLoad(root); instance = root.AddComponent<CrewPlayerIdentity>();
    }
    private void Start()
    {
        AccountProfileData.EnsureBound();
        cosmetics = CCoinService.Ensure();
        AccountProfileData.Changed += Publish;
        cosmetics.Changed += Publish;
        Publish();
    }
    private void Publish()
    {
        if (PhotonNetwork.LocalPlayer == null) return;
        string username = AccountProfileData.Name;
        if (string.IsNullOrWhiteSpace(username) || username == "Player") username = PlayerPrefs.GetString("PlayerName", "Guest");
        if (string.IsNullOrWhiteSpace(username)) username = "Guest";
        if (PhotonNetwork.NickName != username) PhotonNetwork.NickName = username;
        var parts = cosmetics != null ? cosmetics.EquippedParts : new string[0];
        var previous = PhotonNetwork.LocalPlayer.CustomProperties[OutfitKey] as string[];
        if (previous == null || !previous.SequenceEqual(parts))
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { OutfitKey, parts } });
    }
    public override void OnConnectedToMaster() { Publish(); }
    public override void OnJoinedRoom() { Publish(); }
    public static string Username(Photon.Realtime.Player player)
    {
        if (player == null) return "Connecting…";
        if (!string.IsNullOrWhiteSpace(player.NickName)) return player.NickName;
        return player.IsLocal ? AccountProfileData.Name : "Joining crew…";
    }
    public static string[] Outfit(Photon.Realtime.Player player)
    {
        var parts = player?.CustomProperties[OutfitKey] as string[];
        return parts == null ? new string[0] : parts.Where(id => CharacterCosmetics.Find(id) != null).Take(16).ToArray();
    }
    private void OnDestroy()
    {
        AccountProfileData.Changed -= Publish;
        if (cosmetics != null) cosmetics.Changed -= Publish;
        if (instance == this) instance = null;
    }
}
