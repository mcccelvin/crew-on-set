using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

// Appearance preferences only. This never uploads a wallet, entitlement or try-on.
public static class AccountAppearanceData
{
    public const string CloudKey = "character_appearance";
    public const string BaseCharacter = "DefaultCharacGirlRig";
    [Serializable] public sealed class Appearance
    {
        public int version = 1;
        public string player_id, base_character = BaseCharacter, selected_frame, change_id;
        public string[] equipped_parts = new string[0];
    }
    [Serializable] private sealed class Cache { public Appearance appearance; public bool pending; }
    private static Cache cache = new Cache();
    private static string owner = "", status = "";
    private static int generation;
    private static bool busy;
    private static float nextRefresh, deadline;
    public static event Action Changed;
    public static string Owner => owner;
    public static string SelectedFrame => cache.appearance?.selected_frame;
    public static string[] EquippedParts => cache.appearance == null ? new string[0] : (string[])cache.appearance.equipped_parts.Clone();
    public static bool Pending => cache.pending;
    public static bool Busy => busy;
    public static string Status => status;
    private static string Key => "SaveSystem.Appearance.v1." + owner;
    private static bool Authenticated => owner != "guest" && !string.IsNullOrEmpty(owner) &&
        PlayFabClientAPI.IsClientLoggedIn() && PlayFabSettings.staticPlayer.PlayFabId == owner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { generation++; owner = ""; cache = new Cache(); busy = false; Changed = null; }
    public static void Bind(string account, string frame, string[] parts)
    {
        generation++; busy = false; owner = CCoinRules.ValidId(account) ? account : "guest";
        try { cache = JsonUtility.FromJson<Cache>(PlayerPrefs.GetString(Key, "")); }
        catch (ArgumentException) { cache = null; }
        if (cache == null || !Valid(cache.appearance, owner))
        {
            var selection = Selection(owner, frame, parts);
            cache = new Cache { appearance = selection,
                pending = !string.IsNullOrEmpty(selection.selected_frame) || selection.equipped_parts.Length > 0 };
        }
        Store(); nextRefresh = Time.unscaledTime;
        Publish(owner == "guest" ? "Character saved on this device." : "Character saved locally; automatic account sync pending.");
    }
    public static void SetSelection(string frame, string[] parts)
    {
        if (string.IsNullOrEmpty(owner)) return;
        var selection = Selection(owner, frame, parts);
        if (SameSelection(cache.appearance, selection)) return;
        generation++; busy = false; cache.appearance = selection; cache.pending = true;
        Store(); nextRefresh = Time.unscaledTime;
        Publish(owner == "guest" ? "Character saved on this device." : "Character saved locally; automatic account sync pending.");
    }
    private static Appearance Selection(string account, string frame, string[] parts)
    {
        var selected = new List<string>();
        foreach (string kind in CharacterCosmetics.Slots)
        {
            string id = parts == null ? null : Array.Find(parts, x => CharacterCosmetics.Find(x)?.kind == kind);
            if (id != null) selected.Add(id);
        }
        return new Appearance { player_id = account, selected_frame = CCoinRules.ValidId(frame) ? frame : null,
            equipped_parts = selected.ToArray(), change_id = Guid.NewGuid().ToString("N") };
    }
    private static bool Valid(Appearance value, string account)
    {
        if (value == null || value.version != 1 || value.player_id != account || value.base_character != BaseCharacter ||
            !Guid.TryParseExact(value.change_id, "N", out _) || value.equipped_parts == null || value.equipped_parts.Length > CharacterCosmetics.Slots.Length ||
            (!string.IsNullOrEmpty(value.selected_frame) && !CCoinRules.ValidId(value.selected_frame))) return false;
        var slots = new HashSet<string>();
        foreach (string id in value.equipped_parts)
        {
            var item = CharacterCosmetics.Find(id);
            if (item == null || !slots.Add(item.kind)) return false;
        }
        return true;
    }
    private static bool SameSelection(Appearance a, Appearance b)
    {
        if (a == null || b == null || a.player_id != b.player_id || a.base_character != b.base_character ||
            (a.selected_frame ?? "") != (b.selected_frame ?? "") || a.equipped_parts.Length != b.equipped_parts.Length) return false;
        foreach (string id in a.equipped_parts) if (Array.IndexOf(b.equipped_parts, id) < 0) return false;
        return true;
    }
    private static void Store() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(cache)); PlayerPrefs.Save(); }
    private static void Publish(string message) { status = message; Changed?.Invoke(); }
    private static bool Current(int request, string account) => generation == request && owner == account && Authenticated;
    private static Appearance Parse(string json, string account)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 10000) return null;
        try { var value = JsonUtility.FromJson<Appearance>(json); return Valid(value, account) ? value : null; }
        catch (ArgumentException) { return null; }
    }
    public static void Tick()
    {
        if (!Authenticated) { if (busy) { generation++; busy = false; } return; }
        if (busy && Time.unscaledTime >= deadline) { generation++; Fail("Character sync timed out; saved locally and retrying automatically."); }
        if (!busy && Time.unscaledTime >= nextRefresh && Application.internetReachability != NetworkReachability.NotReachable) Refresh();
    }
    public static void Refresh()
    {
        if (busy || !Authenticated || Application.internetReachability == NetworkReachability.NotReachable) return;
        busy = true; int request = ++generation; string account = owner;
        Read(request, account, json => {
            Appearance remote = json == null ? null : Parse(json, account);
            if (json != null && remote == null)
            { Fail("Existing cloud character could not be read. It was not overwritten; local outfit kept."); return; }
            if (cache.pending || remote == null)
            {
                cache.pending = true; Store(); Upload(request, account); return;
            }
            cache.appearance = remote; Store(); Finish();
        });
    }
    private static void Read(int request, string account, Action<string> success)
    {
        deadline = Time.unscaledTime + 45f;
        try
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { Keys = new List<string> { CloudKey } }, result => {
                if (!Current(request, account)) return;
                success(result.Data != null && result.Data.TryGetValue(CloudKey, out var item) ? item.Value : null);
            }, error => { if (Current(request, account)) Fail("Character sync unavailable; saved locally and retrying automatically."); });
        }
        catch (Exception) { if (Current(request, account)) Fail("Character sync unavailable; saved locally and retrying automatically."); }
    }
    private static void Upload(int request, string account)
    {
        string json = JsonUtility.ToJson(cache.appearance), change = cache.appearance.change_id;
        deadline = Time.unscaledTime + 45f;
        try
        {
            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest {
                Permission = UserDataPermission.Private, Data = new Dictionary<string, string> { { CloudKey, json } }
            }, result => {
                if (!Current(request, account)) return;
                Read(request, account, uploaded => {
                    var verified = Parse(uploaded, account);
                    if (verified == null || verified.change_id != change || !SameSelection(verified, cache.appearance))
                    { Fail("Character read-back did not match. Saved locally; retrying automatically."); return; }
                    cache.pending = false; Store(); Finish();
                });
            }, error => { if (Current(request, account)) Fail("Character upload unavailable; saved locally and retrying automatically."); });
        }
        catch (Exception) { if (Current(request, account)) Fail("Character upload unavailable; saved locally and retrying automatically."); }
    }
    private static void Finish() { busy = false; nextRefresh = Time.unscaledTime + 60f; Publish("Character appearance verified in PlayFab."); }
    private static void Fail(string message) { busy = false; nextRefresh = Time.unscaledTime + 60f; Publish(message); }
}
