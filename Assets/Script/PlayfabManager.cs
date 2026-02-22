using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine.UI;
using TMPro;

public class PlayfabManager : MonoBehaviour
{
    public TMP_Text messageText;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;

    public void RegisterButton()
    {
        if (passwordInput.text.Length < 6)
        {
            messageText.text = "Password must be at least 6 characters";
            return;
        }

        var request = new RegisterPlayFabUserRequest
        {
            Email = emailInput.text,
            Password = passwordInput.text,
            RequireBothUsernameAndEmail = false
        };
        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnError);
    }


    void OnError(PlayFabError error)
    {
        if (messageText != null) messageText.text = error.ErrorMessage;
        Debug.Log(error.GenerateErrorReport());
    }

    void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        if (messageText != null) messageText.text = "Registered and Login";
        Debug.Log("Register  successful!");
    }


    public void LoginButton()
    {
        var request = new LoginWithEmailAddressRequest
        {
            Email = emailInput.text,
            Password = passwordInput.text
        };
        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnError);
    }

    void OnLoginSuccess(LoginResult result)
    {
        if (messageText != null) messageText.text = "Logged In!";
        Debug.Log("Login successful!");
    }

    public void ForgotPasswordButton()
    {
        var request = new SendAccountRecoveryEmailRequest
        {
            Email = emailInput.text,
            TitleId ="D4EA4"
        };
        PlayFabClientAPI.SendAccountRecoveryEmail(request, OnPasswordResetSuccess, OnError);
    }

    void OnPasswordResetSuccess(SendAccountRecoveryEmailResult result)
    {
        if (messageText != null) messageText.text = "Password reset email sent!";
        Debug.Log("Password reset email sent!");
    }
}


