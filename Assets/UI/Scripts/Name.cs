using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Name : MonoBehaviour
{
    [Tooltip("Assign the TextMeshProUGUI (or TMP_Text) element that should show the player's display name.")]
    [SerializeField] private TMP_Text displayNameText;

    [Tooltip("Format used to show the name. {0} will be replaced with the display name.")]
    [SerializeField] private string format = "Welcome, {0}";

    private const string UsernameKey = "Username";

    void Start()
    {
        UpdateDisplayName();
    }

    /// <summary>
    /// Reads the saved display name (set by AccountManager at login) and updates the UI text.
    /// Falls back to 'Guest' when no name is found.
    /// </summary>
    public void UpdateDisplayName()
    {
        if (displayNameText == null)
        {
            Debug.LogWarning("[Name] displayNameText is not assigned.");
            return;
        }

        var saved = PlayerPrefs.GetString(UsernameKey, string.Empty);
        var nameToShow = string.IsNullOrEmpty(saved) ? "Guest" : saved;
        displayNameText.text = string.Format(format, nameToShow);
    }

    /// <summary>
    /// Convenience method to set the displayed name manually (and optionally save it to PlayerPrefs).
    /// </summary>
    public void SetDisplayName(string newName, bool saveToPrefs = true)
    {
        if (displayNameText == null) return;
        var nameToShow = string.IsNullOrEmpty(newName) ? "Guest" : newName;
        displayNameText.text = string.Format(format, nameToShow);

        if (saveToPrefs)
            PlayerPrefs.SetString(UsernameKey, nameToShow);
    }
}