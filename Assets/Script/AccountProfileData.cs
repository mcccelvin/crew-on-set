using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Identity is account-wide, not a career value. Never activate a save to view a profile.
public static class AccountProfileData
{
    [Serializable] private sealed class Profile { public string name, bio; public bool pendingName, pendingBio; }
    private static Profile profile = new Profile();
    private static string owner = "", status = "";
    private static int generation;
    private static bool busy;
    public static event Action Changed;
    public static string Owner => owner;
    public static string Name => string.IsNullOrWhiteSpace(profile.name) ? (owner == "guest" ? "Guest" : "Player") : profile.name;
    public static string Bio => profile.bio ?? "";
    public static string Status => status;
    public static bool Busy => busy;
    private static string Key => "SaveSystem.Profile.v1." + owner;
    private static bool Authenticated => owner != "guest" && PlayFabSettings.staticPlayer.PlayFabId == owner && PlayFabClientAPI.IsClientLoggedIn();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { generation++; owner = ""; profile = new Profile(); busy = false; Changed = null; }
    public static void EnsureBound()
    {
        string account = PlayerPrefs.GetString("PlayFabId", "guest");
        if (string.IsNullOrWhiteSpace(account)) account = "guest";
        if (owner != account) Bind(account);
    }
    public static void Bind(string account)
    {
        generation++; busy = false; owner = string.IsNullOrWhiteSpace(account) ? "guest" : account;
        try { profile = JsonUtility.FromJson<Profile>(PlayerPrefs.GetString(Key, "")) ?? new Profile(); }
        catch (ArgumentException) { profile = new Profile(); }
        string loginName = PlayerPrefs.GetString("PlayerName", "");
        if (!profile.pendingName && !string.IsNullOrWhiteSpace(loginName)) profile.name = loginName.Trim();
        profile.bio = Limit(profile.bio, 500);
        PlayerPrefs.SetString("PlayerName", Name);
        Store(); Publish(owner == "guest" ? "Guest profile · saved on this device" : "Account profile · cached on this device");
    }
    private static string Limit(string value, int maximum) => (value ?? "").Substring(0, Math.Min((value ?? "").Length, maximum));
    private static void Store() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile)); PlayerPrefs.Save(); }
    private static void Publish(string message) { status = message; Changed?.Invoke(); }
    private static bool Current(int request, string account) => generation == request && owner == account && Authenticated;

    public static void Refresh()
    {
        EnsureBound();
        if (busy || !Authenticated) return;
        if (profile.pendingName || profile.pendingBio) { Upload(); return; }
        busy = true; int request = ++generation; string account = owner;
        PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { "ProfileBio" } }, result =>
        {
            if (!Current(request, account)) return;
            profile.bio = result.Data != null && result.Data.TryGetValue("ProfileBio", out var bio) ? Limit(bio.Value, 500) : "";
            Store();
            PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), info =>
            {
                if (!Current(request, account)) return;
                string name = info.AccountInfo?.TitleInfo?.DisplayName;
                if (!string.IsNullOrWhiteSpace(name)) { profile.name = name; PlayerPrefs.SetString("PlayerName", Name); }
                busy = false; Store(); Publish("Account profile synced");
            }, error => { if (Current(request, account)) { busy = false; Publish("Bio synced · name uses your login profile"); } });
        }, error => { if (Current(request, account)) { busy = false; Publish("Offline · showing your saved profile"); } });
    }
    public static bool Save(string name, string bio)
    {
        EnsureBound(); name = (name ?? "").Trim();
        if (name.Length < (owner == "guest" ? 1 : 3) || name.Length > 25 || Array.Exists(name.ToCharArray(), char.IsControl))
        { Publish(owner == "guest" ? "Use a name from 1 to 25 characters." : "Use an account name from 3 to 25 characters."); return false; }
        // Invalidates any read already in flight; a late read cannot overwrite the new draft.
        generation++; busy = false;
        profile.name = name; profile.bio = Limit(bio, 500);
        profile.pendingName = profile.pendingBio = owner != "guest";
        PlayerPrefs.SetString("PlayerName", Name); Store();
        Publish(owner == "guest" ? "Profile saved on this device" : "Profile saved on this device · account sync pending");
        if (Authenticated) Upload();
        return true;
    }
    private static void Upload()
    {
        if (busy || !Authenticated) return;
        busy = true; int request = ++generation; string account = owner;
        string name = profile.name, bio = profile.bio;
        Action uploadBio = () =>
        {
            if (!Current(request, account)) return;
            if (!profile.pendingBio) { busy = false; Publish("Account profile saved"); return; }
            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest {
                Data = new Dictionary<string, string> { { "ProfileBio", bio ?? "" } }, Permission = UserDataPermission.Private
            }, result => { if (Current(request, account)) { profile.pendingBio = false; busy = false; Store(); Publish("Account profile saved"); } },
            error => { if (Current(request, account)) { busy = false; Publish("Saved locally · Bio will sync when you reconnect"); } });
        };
        if (!profile.pendingName) { uploadBio(); return; }
        PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = name }, result =>
        { if (Current(request, account)) { profile.pendingName = false; Store(); uploadBio(); } },
        error => { if (Current(request, account)) { busy = false; Publish("Saved locally · account name not accepted or connection unavailable. Edit the name or retry Save."); } });
    }
}
