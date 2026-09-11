using PlayFab;
using PlayFab.ClientModels;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AccountManager : MonoBehaviour
{
    const string LAST_EMAIL_KEY = "LastEmail";

    [SerializeField] TMP_Text messageText;
    [SerializeField] TMP_Text username;

    #region Register
    [Header("Register")]
    [SerializeField] TMP_InputField registerEmail;
    [SerializeField] TMP_InputField registerUsername;
    [SerializeField] TMP_InputField registerPassword;

    public void OnRegisterPressed()
    {
        Register(registerEmail.text, registerUsername.text, registerPassword.text);
    }

    public void Register(string email, string username, string password)
    {

        if (string.IsNullOrEmpty(password) || password.Length < 6)
        {
            messageText.text = "Password must be at least 6 characters";
            return;
        }

        PlayFabClientAPI.RegisterPlayFabUser(new RegisterPlayFabUserRequest
        {
            Email = email,
            DisplayName = username,
            Password = password,
            RequireBothUsernameAndEmail = false,
        },
        successfullResult => 
        {
            Login(email, password);
            if (messageText != null) messageText.text = "Register successful! Welcome " + username;
        },
        PlayfabFailure);
    }
    #endregion

    #region Login
    [Header("Login")]
    [SerializeField] TMP_InputField loginEmail;
    [SerializeField] TMP_InputField loginPassword;

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
        string displayName = payload?.PlayerProfile?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = payload?.AccountInfo?.TitleInfo?.DisplayName;
        if (string.IsNullOrWhiteSpace(displayName)) displayName = payload?.AccountInfo?.Username;
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

    private void Login(string email, string password)
    {
        PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest
        {
            Email = email,
            Password = password,
            InfoRequestParameters = new GetPlayerCombinedInfoRequestParams
            {
                GetPlayerProfile = true,
                GetUserAccountInfo = true,
                ProfileConstraints = new PlayerProfileViewConstraints { ShowDisplayName = true }
            }
        },
        successfulResult =>
        {
            string displayName = SaveLoginProfile(successfulResult, email);
            GameSaveManager.Ensure().SetAccount(successfulResult.PlayFabId);
            if (messageText != null) messageText.text = "Login successful! Welcome " + displayName;
            SceneManager.LoadScene("Account");
        },
        PlayfabFailure);
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
            if (messageText != null) messageText.text = "Recovery email sent!";
        },
        PlayfabFailure);
    }
    #endregion

    private void PlayfabFailure(PlayFabError error)
    {
        if (messageText != null) messageText.text = error.ErrorMessage;
        Debug.Log(error.Error + " : " + error.GenerateErrorReport());
    }

}
