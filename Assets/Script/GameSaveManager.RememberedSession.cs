using System;
using System.Runtime.InteropServices;
using System.Text;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public sealed partial class GameSaveManager
{
    [Serializable] private sealed class RememberedSession
    {
        public string owner;
        public string ticket;
    }
    public bool RestoringSignIn { get; private set; }
    private int restoreGeneration;
    private bool stopSessionRestore;
    private float nextSessionRestore;
    private static string SessionVaultKey => "CrewOnSet/PlayFab/" + PlayFabSettings.TitleId;
    public void BeginManualSignIn() => CancelSessionRestore();

    private void RememberCurrentSession()
    {
        if (!HasCloudSession) return;
        var saved = new RememberedSession {
            owner = authenticatedId, ticket = PlayFabSettings.staticPlayer.ClientSessionTicket
        };
        bool remembered = false;
        try { remembered = SessionVault.Write(SessionVaultKey, JsonUtility.ToJson(saved)); }
        catch (Exception) { /* Secure storage failure must not interrupt a valid login. */ }
        if (!remembered)
            Debug.LogWarning("This device could not securely remember the sign-in. The current session is still active.");
    }

    private void CancelSessionRestore()
    {
        restoreGeneration++;
        RestoringSignIn = false;
        stopSessionRestore = true;
    }

    private void TryRestoreRememberedSession()
    {
        if (stopSessionRestore || RestoringSignIn || PlayFabClientAPI.IsClientLoggedIn() ||
            Time.unscaledTime < nextSessionRestore || string.IsNullOrEmpty(PlayFabSettings.TitleId) ||
            Application.internetReachability == NetworkReachability.NotReachable) return;
        nextSessionRestore = Time.unscaledTime + 30f;
        RememberedSession saved = null;
        try
        {
            string json = SessionVault.Read(SessionVaultKey);
            if (!string.IsNullOrEmpty(json)) saved = JsonUtility.FromJson<RememberedSession>(json);
        }
        catch (Exception) { /* Never print credential contents. */ }
        if (saved == null || string.IsNullOrEmpty(saved.owner) || string.IsNullOrEmpty(saved.ticket))
        { stopSessionRestore = true; return; }

        RestoringSignIn = true;
        int generation = ++restoreGeneration;
        // Validate privately first: a cached ID/ticket must not enable cloud writes.
        var context = new PlayFabAuthenticationContext { PlayFabId = saved.owner, ClientSessionTicket = saved.ticket };
        var client = new PlayFabClientInstanceAPI(context);
        client.GetAccountInfo(new GetAccountInfoRequest(), result => {
            if (this == null || generation != restoreGeneration) return;
            RestoringSignIn = false;
            if (PlayFabClientAPI.IsClientLoggedIn()) return;
            if (result.AccountInfo == null || result.AccountInfo.PlayFabId != saved.owner)
            {
                SessionVault.Delete(SessionVaultKey);
                stopSessionRestore = true;
                return;
            }
            PlayFabSettings.staticPlayer.CopyFrom(context);
            SetAccount(saved.owner);
            Status = "Signed in automatically.";
            Changed?.Invoke();
        }, error => {
            if (this == null || generation != restoreGeneration) return;
            RestoringSignIn = false;
            if (error.Error == PlayFabErrorCode.InvalidSessionTicket || error.Error == PlayFabErrorCode.AuthTokenExpired ||
                error.HttpCode == 401 || error.HttpCode == 403)
            {
                SessionVault.Delete(SessionVaultKey);
                stopSessionRestore = true;
                Status = "Your saved sign-in expired. Please sign in again.";
            }
            else Status = "Could not reconnect. Local progress is safe; sign-in will retry.";
            Changed?.Invoke();
        });
    }

    // Windows protects this generic credential for the current OS user. No passwords
    // or session tickets are written to PlayerPrefs, plain files, or logs.
    private static class SessionVault
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct Credential
        {
            public uint flags, type;
            public string target, comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME lastWritten;
            public uint size;
            public IntPtr blob;
            public uint persist, attributeCount;
            public IntPtr attributes;
            public string alias, username;
        }
        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredWrite(ref Credential credential, uint flags);
        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CredDelete(string target, uint type, uint flags);
        [DllImport("advapi32.dll")] private static extern void CredFree(IntPtr credential);
#endif
        public static bool Write(string key, string value)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > 2560) return false;
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, buffer, bytes.Length);
                var credential = new Credential { type = 1, target = key, size = (uint)bytes.Length,
                    blob = buffer, persist = 2, username = "CrewOnSet session" };
                return CredWrite(ref credential, 0);
            }
            finally { Array.Clear(bytes, 0, bytes.Length); Marshal.Copy(bytes, 0, buffer, bytes.Length); Marshal.FreeHGlobal(buffer); }
#else
            return false;
#endif
        }
        public static string Read(string key)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (!CredRead(key, 1, 0, out var pointer)) return null;
            try
            {
                var credential = Marshal.PtrToStructure<Credential>(pointer);
                if (credential.size == 0 || credential.size > 2560) return null;
                byte[] bytes = new byte[(int)credential.size];
                try { Marshal.Copy(credential.blob, bytes, 0, bytes.Length); return Encoding.UTF8.GetString(bytes); }
                finally { Array.Clear(bytes, 0, bytes.Length); }
            }
            finally { CredFree(pointer); }
#else
            return null;
#endif
        }
        public static void Delete(string key)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            CredDelete(key, 1, 0);
#endif
        }
    }
}
