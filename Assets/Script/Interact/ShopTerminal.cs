using UnityEngine;
using Player.PlayerController; // We need this to pause the player, just like the Director Tablet!

public class ShopTerminal : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Drag your Store Menu Canvas here")]
    public GameObject shopUI;

    private PlayerController playerController;

    private void Start()
    {
        // Make sure the shop menu is closed when the game starts
        if (shopUI != null) shopUI.SetActive(false);
    }

    // --- CALL THIS FROM YOUR EXISTING INTERACT SCRIPT WHEN YOU PRESS 'E' ---
    public void OpenShop(PlayerController pController)
    {
        playerController = pController;

        // 1. Disable player controls so they can't walk away while shopping
        if (playerController != null) playerController.enabled = false;

        // 2. Enable the Store UI
        if (shopUI != null) shopUI.SetActive(true);

        // 3. Unlock the mouse so you can click the "Buy" buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("Entered the Shop!");
    }

    // --- LINK THIS TO A "CLOSE" BUTTON ON YOUR SHOP UI ---
    public void CloseShop()
    {
        // 1. Re-enable player controls
        if (playerController != null) playerController.enabled = true;

        // 2. Disable the Store UI
        if (shopUI != null) shopUI.SetActive(false);

        // 3. Lock the mouse back to the game
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Exited the Shop!");
    }
}   