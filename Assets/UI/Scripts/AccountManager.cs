using PlayFab;
using PlayFab.ClientModels;
using System.Collections;
using System.Collections.Generic;
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
                GetPlayerProfile = true
            }
        },
        successfulResult =>
        {
            var displayName = successfulResult?.InfoResultPayload?.PlayerProfile?.DisplayName;
            if (string.IsNullOrEmpty(displayName))
                displayName = "Guest";
            PlayerPrefs.SetString(LAST_EMAIL_KEY, email);
            PlayerPrefs.SetString("PlayerName", displayName);
            if (messageText != null) messageText.text = "Login successful! Welcome " + displayName;
            if (username != null) username.text = displayName;

            Debug.Log("Login successful! Welcome " + PlayerPrefs.GetString("PlayerName"));

            SceneManager.LoadScene(3);
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
