using PlayFab;
using PlayFab.ClientModels;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AccountManager : MonoBehaviour
{
    const string LAST_EMAIL_KEY = "LastEmail",
                 LAST_PASSWORD_KEY = "LastPassword";

    [SerializeField] TMP_Text messageText;

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

        if (registerPassword.text.Length < 6)
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
            if (messageText != null) messageText.text = "Register successful! Welcome " + PlayerPrefs.GetString("Username");
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
            if (messageText != null) messageText.text = "Login successful! Welcome " + PlayerPrefs.GetString("Username");
            PlayerPrefs.SetString(LAST_EMAIL_KEY, email);
            PlayerPrefs.SetString(LAST_PASSWORD_KEY, password);
            PlayerPrefs.SetString("Username", successfulResult.InfoResultPayload.PlayerProfile.DisplayName);
            Debug.Log("Login successful! Welcome " + PlayerPrefs.GetString("Username"));
            SceneManager.LoadScene(1);
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
            Recovery(email);
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