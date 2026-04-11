using UnityEngine;
using TMPro;
using System.Collections;
using Player.PlayerController;

public class CinematicTitleManager : MonoBehaviour
{
    public GameObject titleCardPanel;
    public TextMeshProUGUI titleText;

    public void ShowTitleCard(string message, float duration)
    {
        StartCoroutine(TitleSequence(message, duration));
    }

    private IEnumerator TitleSequence(string message, float duration)
    {
        // 1. Find the player
        PlayerController player = FindObjectOfType<PlayerController>();

        // 2. Freeze movement and camera
        if (player != null)
        {
            player.canMove = false;
            player.canLook = false;
        }

        // 3. Show the UI
        titleText.text = message;
        titleCardPanel.SetActive(true);

        // 4. Wait
        yield return new WaitForSeconds(duration);

        // 5. Hide UI
        titleCardPanel.SetActive(false);

        // 6. Unfreeze the player
        if (player != null)
        {
            player.canMove = true;
            player.canLook = true;
        }
    }
}