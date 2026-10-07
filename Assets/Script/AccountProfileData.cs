using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Account identity and biography share the website's profile_metadata record.
// Password-confirmed username changes update both that record and the title name.
public static class AccountProfileData
{
    private const string MetadataKey = "profile_metadata";
    [Serializable] private sealed class Profile { public string name, bio; public bool pendingName, pendingBio; }
    private static Profile profile = new Profile();
    private static string owner = "", status = "";
    private static int generation;
    private static bool busy;
    private static float nextRefresh, deadline;
    public static event Action Changed;
    public static string Owner => owner;
    public static string Name => string.IsNullOrWhiteSpace(profile.name) ? (owner == "guest" ? "Guest" : "Player") : profile.name;
    public static string Bio => profile.bio ?? "";
    public static string Status => status;
    public static bool Busy => busy;
    public static bool Authenticated => owner != "guest" && PlayFabSettings.staticPlayer.PlayFabId == owner && PlayFabClientAPI.IsClientLoggedIn();
    private static string Key => "SaveSystem.Profile.v1." + owner;

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
        // A display name never has an offline/pending write: changing it requires the password.
        profile.pendingName = false;
        profile.bio = Limit(profile.bio, 500);
        if (owner == "guest") { profile.name = "Guest"; profile.bio = ""; profile.pendingBio = false; }
        PlayerPrefs.SetString("PlayerName", Name); Store();
        Publish(owner == "guest" ? "Guest profile · sign in to edit your profile" : "Account profile · syncing website profile");
        nextRefresh = Time.unscaledTime;
    }
    private static string Limit(string value, int maximum) => (value ?? "").Substring(0, Math.Min((value ?? "").Length, maximum));
    private static void Store() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile)); PlayerPrefs.Save(); }
    private static void Publish(string message) { status = message; Changed?.Invoke(); }
    private static bool Current(int request, string account) => generation == request && owner == account && Authenticated;
    private static Dictionary<string, object> ReadMetadata(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, object>();
        var parsed = PlayFab.Json.PlayFabSimpleJson.DeserializeObject(json) as IDictionary<string, object>;
        if (parsed == null) throw new FormatException("The website profile data is not valid JSON.");
        return new Dictionary<string, object>(parsed);
    }
    private static string StringValue(Dictionary<string, object> values, string key)
        => values.TryGetValue(key, out var value) ? value as string ?? "" : "";
    private static string MetadataJson(Dictionary<string, object> values)
        => PlayFab.Json.PlayFabSimpleJson.SerializeObject(values);

    public static void Tick()
    {
        EnsureBound();
        if (!Authenticated) { if (busy) { generation++; busy = false; } return; }
        if (busy && Time.unscaledTime >= deadline)
        { generation++; Finish("Profile sync timed out; saved locally and retrying automatically."); }
        if (!busy && Time.unscaledTime >= nextRefresh && Application.internetReachability != NetworkReachability.NotReachable) Refresh();
    }
    private static void Finish(string message)
    {
        busy = false; nextRefresh = Time.unscaledTime + 60f; Publish(message);
    }

    public static void Refresh()
    {
        EnsureBound();
        if (busy || !Authenticated || Application.internetReachability == NetworkReachability.NotReachable) return;
        if (profile.pendingBio) { UploadBio(); return; }
        busy = true; int request = ++generation; string account = owner; deadline = Time.unscaledTime + 45f;
        try
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { MetadataKey, "ProfileBio" } }, result =>
            {
                if (!Current(request, account)) return;
                try
                {
                    string raw = result.Data != null && result.Data.TryGetValue(MetadataKey, out var record) ? record.Value : "";
                    var metadata = ReadMetadata(raw);
                    string websiteName = StringValue(metadata, "username");
                    string websiteBio = StringValue(metadata, "bio");
                    if (string.IsNullOrWhiteSpace(websiteBio) && result.Data != null && result.Data.TryGetValue("ProfileBio", out var legacyBio))
                        websiteBio = legacyBio.Value ?? "";
                    ReadAccountName(request, account, websiteName, websiteBio);
                }
                catch (Exception) { if (Current(request, account)) Finish("Website profile data could not be read; keeping the saved profile."); }
            }, error => { if (Current(request, account)) Finish("Offline · showing your saved profile; retrying automatically."); });
        }
        catch (Exception) { if (Current(request, account)) Finish("Profile sync unavailable; saved locally and retrying automatically."); }
    }
    private static void ReadAccountName(int request, string account, string websiteName, string websiteBio)
    {
        deadline = Time.unscaledTime + 45f;
        try
        {
            PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), info =>
            {
                if (!Current(request, account)) return;
                string name = websiteName;
                if (string.IsNullOrWhiteSpace(name)) name = info.AccountInfo?.TitleInfo?.DisplayName;
                if (string.IsNullOrWhiteSpace(name) || name == "Guest") name = info.AccountInfo?.Username;
                if (!string.IsNullOrWhiteSpace(name)) profile.name = name.Trim();
                profile.bio = Limit(websiteBio, 500);
                PlayerPrefs.SetString("PlayerName", Name); Store(); Finish("Website profile synced");
            }, error =>
            {
                if (!Current(request, account)) return;
                if (!string.IsNullOrWhiteSpace(websiteName)) profile.name = websiteName.Trim();
                profile.bio = Limit(websiteBio, 500); PlayerPrefs.SetString("PlayerName", Name); Store();
                Finish("Website profile synced · account name is temporarily unavailable");
            });
        }
        catch (Exception) { if (Current(request, account)) Finish("Account profile unavailable; retrying automatically."); }
    }

    // Kept for the profile screen's existing caller; only the biography is editable here.
    public static bool Save(string name, string bio) => SaveBio(bio);
    public static bool SaveBio(string bio)
    {
        EnsureBound();
        if (!Authenticated) { Publish("Sign in to edit your website profile."); return false; }
        generation++; busy = false; profile.bio = Limit(bio, 500); profile.pendingBio = true;
        Store(); nextRefresh = Time.unscaledTime; Publish("Biography saved on this device · website sync pending");
        if (Application.internetReachability != NetworkReachability.NotReachable) UploadBio();
        return true;
    }
    private static void UploadBio()
    {
        if (busy || !Authenticated || Application.internetReachability == NetworkReachability.NotReachable) return;
        busy = true; int request = ++generation; string account = owner; deadline = Time.unscaledTime + 45f;
        string bio = profile.bio;
        try
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { MetadataKey } }, result =>
            {
                if (!Current(request, account)) return;
                try
                {
                    string raw = result.Data != null && result.Data.TryGetValue(MetadataKey, out var record) ? record.Value : "";
                    var metadata = ReadMetadata(raw); metadata["bio"] = bio;
                    deadline = Time.unscaledTime + 45f;
                    PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest {
                        Data = new Dictionary<string, string> { { MetadataKey, MetadataJson(metadata) } },
                        Permission = UserDataPermission.Private
                    }, saved =>
                    {
                        if (!Current(request, account)) return;
                        profile.pendingBio = false; Store(); Finish("About you synced with your website profile.");
                    }, error => { if (Current(request, account)) Finish("Saved locally · About you will sync automatically when you reconnect."); });
                }
                catch (Exception) { if (Current(request, account)) Finish("Website profile data could not be updated; your biography remains saved locally."); }
            }, error => { if (Current(request, account)) Finish("Saved locally · About you will sync automatically when you reconnect."); });
        }
        catch (Exception) { if (Current(request, account)) Finish("Saved locally · About you will sync automatically when you reconnect."); }
    }

    public static void ChangeUsername(string newUsername, string currentPassword, Action<bool, string> completed)
    {
        EnsureBound(); newUsername = (newUsername ?? "").Trim();
        if (!Authenticated) { completed?.Invoke(false, "Sign in to change your username."); return; }
        if (!Regex.IsMatch(newUsername, "^[A-Za-z][A-Za-z0-9_]{2,19}$"))
        { completed?.Invoke(false, "Use 3–20 characters: start with a letter, then letters, numbers, or underscores."); return; }
        if (newUsername.Equals(Name, StringComparison.OrdinalIgnoreCase))
        { completed?.Invoke(false, "That is already your username."); return; }
        if (string.IsNullOrEmpty(currentPassword)) { completed?.Invoke(false, "Enter your current password to confirm the change."); return; }
        if (busy) { completed?.Invoke(false, "Profile sync is busy. Try again in a moment."); return; }

        busy = true; int request = ++generation; string account = owner, oldName = Name;
        deadline = Time.unscaledTime + 45f; Publish("Verifying your password…");
        try
        {
            PlayFabClientAPI.GetAccountInfo(new GetAccountInfoRequest(), info =>
            {
                if (!Current(request, account)) return;
                string playFabUsername = info.AccountInfo?.Username;
                string loginEmail = PlayerPrefs.GetString("LastEmail", "").Trim();
                if (string.IsNullOrWhiteSpace(playFabUsername) && string.IsNullOrWhiteSpace(loginEmail)) { Finish("This account has no password login available. Change the username on the website."); completed?.Invoke(false, Status); return; }
                try
                {
                    Action<LoginResult> verified = login =>
                    {
                        if (!Current(request, account)) return;
                        if (login.PlayFabId != account) { Finish("Current password was not accepted for this account."); completed?.Invoke(false, Status); return; }
                        UpdateTitleAndWebsiteName(request, account, oldName, newUsername, completed);
                    };
                    Action<PlayFabError> rejected = error =>
                    {
                        if (!Current(request, account)) return;
                        Finish("Current password was not accepted. Your profile was not changed."); completed?.Invoke(false, Status);
                    };
                    if (!string.IsNullOrWhiteSpace(playFabUsername))
                        PlayFabClientAPI.LoginWithPlayFab(new LoginWithPlayFabRequest { Username = playFabUsername, Password = currentPassword }, verified, rejected);
                    else
                        PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest { Email = loginEmail, Password = currentPassword }, verified, rejected);
                }
                catch (Exception) { if (Current(request, account)) { Finish("Password verification is unavailable. Your profile was not changed."); completed?.Invoke(false, Status); } }
            }, error => { if (Current(request, account)) { Finish("Could not verify the account. Your profile was not changed."); completed?.Invoke(false, Status); } });
        }
        catch (Exception) { if (Current(request, account)) { Finish("Password verification is unavailable. Your profile was not changed."); completed?.Invoke(false, Status); } }
    }
    private static void UpdateTitleAndWebsiteName(int request, string account, string oldName, string newName, Action<bool, string> completed)
    {
        deadline = Time.unscaledTime + 45f;
        PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = newName }, renamed =>
        {
            if (!Current(request, account)) return;
            deadline = Time.unscaledTime + 45f;
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { MetadataKey } }, result =>
            {
                if (!Current(request, account)) return;
                try
                {
                    string raw = result.Data != null && result.Data.TryGetValue(MetadataKey, out var record) ? record.Value : "";
                    var metadata = ReadMetadata(raw); metadata["username"] = newName;
                    PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest {
                        Data = new Dictionary<string, string> { { MetadataKey, MetadataJson(metadata) } }, Permission = UserDataPermission.Private
                    }, saved =>
                    {
                        if (!Current(request, account)) return;
                        profile.name = newName; profile.pendingName = false; PlayerPrefs.SetString("PlayerName", newName); Store();
                        Finish("Username updated. It will appear on your website profile and in the game."); completed?.Invoke(true, Status);
                    }, error => RollBackTitle(request, account, oldName, completed));
                }
                catch (Exception) { RollBackTitle(request, account, oldName, completed); }
            }, error => { if (Current(request, account)) RollBackTitle(request, account, oldName, completed); });
        }, error =>
        {
            if (!Current(request, account)) return;
            Finish(error.Error == PlayFabErrorCode.NameNotAvailable ? "That username is already in use." : "PlayFab rejected the username change. Your profile was not changed.");
            completed?.Invoke(false, Status);
        });
    }
    private static void RollBackTitle(int request, string account, string oldName, Action<bool, string> completed)
    {
        if (generation != request || owner != account) return;
        PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = oldName }, result =>
        { if (Current(request, account)) { Finish("Website profile could not be updated; the game display name was restored."); completed?.Invoke(false, Status); } },
        error => { if (Current(request, account)) { Finish("Website profile sync failed. Reopen the website and game profile to reconcile the name."); completed?.Invoke(false, Status); } });
    }
}
