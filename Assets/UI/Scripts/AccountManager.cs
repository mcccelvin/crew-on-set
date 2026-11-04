using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AccountManager : MonoBehaviour
{
    const string LAST_EMAIL_KEY = "LastEmail";
    public const string WebsiteSignupUrl = "https://crew-on-set.vercel.app/signup";

    [SerializeField] TMP_Text messageText;
    [SerializeField] TMP_Text username;

    #region Register
    [Header("Register")]
    [SerializeField] TMP_InputField registerEmail;
    [SerializeField] TMP_InputField registerUsername;
    [SerializeField] TMP_InputField registerPassword;

    public void OnRegisterPressed()
    {
        OnWebsiteSignUpPressed();
    }

    public void OnWebsiteSignUpPressed()
    {
        // Registration belongs to the website; keep the game ready for the return login.
        ShowMessage("Sign up in your browser, then return here to sign in.", GameFeedback.NoticeType.Instruction);
        Application.OpenURL(WebsiteSignupUrl);
    }

    public void Register(string email, string username, string password)
    {

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            ShowMessage("Password must be at least 6 characters.", GameFeedback.NoticeType.Caution);
            return;
        }

        PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
        {
            Username = username,
            Email = email,
            DisplayName = username,
            Password = password,
            RequireBothUsernameAndEmail = false,
        },
        successfullResult => 
        {
            Login(email, password);
            ShowMessage("Registration complete. Welcome " + username + "!", GameFeedback.NoticeType.Success);
        },
        PlayfabFailure);
    }
    #endregion

    #region Login
    [Header("Login")]
    [SerializeField] TMP_InputField loginEmail;
    [SerializeField] TMP_InputField loginPassword;

    private void Awake()
    {
        if (gameObject.scene.name == "Login")
            LoginScreenPresentation.Apply(loginEmail, loginPassword, recoveryEmail, messageText);
    }

    private void Start()
    {
        RefreshProfileName();
    }

    private void RefreshProfileName()
    {
        if (username == null) return;
        string savedName = PlayerPrefs.GetString("PlayerName", "").Trim();
        username.richText = false;
        username.text = string.IsNullOrEmpty(savedName) || savedName == "Guest"
            ? "Guest Profile" : savedName;
    }

    private string SaveLoginProfile(LoginResult result, string email)
    {
        var payload = result?.InfoResultPayload;
        // Website usernames are mirrored to the PlayFab title display name. Prefer
        // that account value over a possibly stale combined-profile cache.
        string displayName = payload?.AccountInfo?.TitleInfo?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName) || displayName == "Guest") displayName = payload?.PlayerProfile?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName) || displayName == "Guest") displayName = payload?.AccountInfo?.Username;
        // A successful login is never a guest, even for a legacy unnamed account.
        // Do not reuse another account's saved name or expose the login email.
        displayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName.Trim();
        PlayerPrefs.SetString(LAST_EMAIL_KEY, email);
        PlayerPrefs.SetString("PlayerName", displayName);
        PlayerPrefs.Save();
        RefreshProfileName();
        return displayName;
    }

    public void OnLoginPressed()
    {
        Login(loginEmail.text, loginPassword.text); 
    }

    private bool loginPending;
    private float nextLoginAttempt;

    private void Login(string identifier, string password)
    {
        if (loginPending || Time.unscaledTime < nextLoginAttempt) return;
        identifier = (identifier ?? "").Trim();
        if (identifier.Length == 0 || string.IsNullOrEmpty(password))
        {
            ShowMessage("Enter your email or username and password.", GameFeedback.NoticeType.Caution);
            return;
        }
        loginPending = true;
        GameSaveManager.Ensure().BeginManualSignIn();
        var info = new GetPlayerCombinedInfoRequestParams
        {
            GetPlayerProfile = true, GetUserAccountInfo = true,
            ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
        };
        bool byEmail = identifier.Contains("@");
        void SignedIn(LoginResult result)
        {
            loginPending = false;
            string email = byEmail ? identifier : result.InfoResultPayload?.AccountInfo?.PrivateInfo?.Email ?? "";
            string displayName = SaveLoginProfile(result, email);
            GameSaveManager.Ensure().SetAccount(result.PlayFabId);
            ShowMessage("Welcome back, " + displayName + "!", GameFeedback.NoticeType.Success);
            LoadingScreenController.LoadScene("Account");
        }
        void SignInFailed(PlayFabError error)
        {
            loginPending = false;
            if (error.Error == PlayFabErrorCode.APIClientRequestRateLimitExceeded)
            {
                nextLoginAttempt = Time.unscaledTime + 10f;
                ShowMessage("Too many sign-in attempts. Wait a moment before trying again.", GameFeedback.NoticeType.Caution);
            }
            else if (!byEmail && (error.Error == PlayFabErrorCode.InvalidUsernameOrPassword || error.Error == PlayFabErrorCode.AccountNotFound || error.Error == PlayFabErrorCode.InvalidPassword))
            {
                loginPending = true;
                StartCoroutine(LoginWebsiteAlias(identifier, password, info, SignedIn));
            }
            else PlayfabFailure(error);
        }
        if (byEmail)
            PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest { Email = identifier, Password = password, InfoRequestParameters = info }, SignedIn, SignInFailed);
        else
            PlayFabClientAPI.LoginWithPlayFab(new LoginWithPlayFabRequest { Username = identifier, Password = password, InfoRequestParameters = info }, SignedIn, SignInFailed);
    }

    [System.Serializable] private class WebsiteLogin { public string email, password, scope; }
    [System.Serializable] private class WebsiteIdentity { public string email, playFabId; }
    [System.Serializable] private class WebsiteReply { public WebsiteIdentity session; public string error; }

    private System.Collections.IEnumerator LoginWebsiteAlias(string alias, string password,
        GetPlayerCombinedInfoRequestParams info, System.Action<LoginResult> signedIn)
    {
        var payload = new WebsiteLogin { email = alias, password = password, scope = "player" };
        using (var request = new UnityEngine.Networking.UnityWebRequest("https://crew-on-set.vercel.app/api/auth/login", "POST"))
        {
            request.redirectLimit = 0;
            request.timeout = 15;
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            yield return request.SendWebRequest();
            WebsiteReply reply = null;
            try { reply = JsonUtility.FromJson<WebsiteReply>(request.downloadHandler.text); }
            catch (System.ArgumentException) { }
            Debug.Log("[Sign-in] Website login HTTP " + request.responseCode + "; session returned: " + (reply?.session != null));
            var account = request.result == UnityEngine.Networking.UnityWebRequest.Result.Success ? reply?.session : null;
            if (account == null || string.IsNullOrWhiteSpace(account.email) || string.IsNullOrWhiteSpace(account.playFabId))
            {
                loginPending = false;
                string reason = (reply?.error ?? "").Trim().ToLowerInvariant();
                string message = "Sign-in unavailable. Try again.";
                if (request.responseCode == 429)
                {
                    nextLoginAttempt = Time.unscaledTime + 10f;
                    message = "Too many attempts. Try again shortly.";
                }
                else if (reason.Contains("account not found") || reason.Contains("user not found") || reason.Contains("no account") || reason == "accountnotfound")
                    message = "Account not found.";
                else if (reason.Contains("incorrect password") || reason.Contains("wrong password") || reason == "invalidpassword")
                    message = "Incorrect password.";
                else if (request.responseCode == 401 || request.responseCode == 400)
                    message = "Incorrect username or password.";
                ShowMessage(message, GameFeedback.NoticeType.Caution);
                yield break;
            }
            Debug.Log("[Sign-in] Website identity verified. Starting linked PlayFab login.");
            PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest {
                Email = account.email.Trim(), Password = password, InfoRequestParameters = info
            }, result => {
                loginPending = false;
                if (string.Equals(result.PlayFabId, account.playFabId, System.StringComparison.OrdinalIgnoreCase)) signedIn(result);
                else
                {
                    PlayFabClientAPI.ForgetAllCredentials();
                    ShowMessage("Sign-in unavailable. Try again.", GameFeedback.NoticeType.Caution);
                }
            }, error => { loginPending = false; PlayfabFailure(error); });
        }
    }

    #endregion

    #region Recovery
    [Header("Recovery")]
    [SerializeField] TMP_InputField recoveryEmail; 

    public void OnRecoveryPressed()
    {
        Recovery(recoveryEmail.text);
    }

    private void Recovery(string email)
    {
        PlayFabClientAPI.SendAccountRecoveryEmail(new SendAccountRecoveryEmailRequest
        {
            Email = email,
            TitleId = "D4EA4"
        },
        successfullResult => 
        {
            ShowMessage("Password reset instructions have been sent to your email.", GameFeedback.NoticeType.Success);
        },
        PlayfabFailure);
    }
    #endregion

    private void PlayfabFailure(PlayFabError error)
    {
        bool credentialsRejected = error != null &&
            (error.Error == PlayFabErrorCode.InvalidEmailOrPassword ||
             error.Error == PlayFabErrorCode.InvalidUsernameOrPassword);
        string message = error?.Error == PlayFabErrorCode.AccountNotFound ? "Account not found." :
            error?.Error == PlayFabErrorCode.InvalidPassword ? "Incorrect password." : credentialsRejected
            ? "Incorrect email, username or password."
            : string.IsNullOrWhiteSpace(error?.ErrorMessage)
                ? "Sign in could not be completed. Check your details and try again."
                : error.ErrorMessage;
        ShowMessage(message, GameFeedback.NoticeType.Caution);
        Debug.Log(error.Error + " : " + error.GenerateErrorReport());
    }

    private void ShowMessage(string text, GameFeedback.NoticeType type)
    {
        // The legacy label sits over the login heading. All account feedback now uses
        // the shared, high contrast notice card at the top of the screen.
        if (messageText != null) messageText.text = "";
        GameFeedback.Show(text, type);
    }

}
