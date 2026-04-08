using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using Player.PlayerController;

[System.Serializable]
public class ShopItem
{
    public string itemName;
    public int price;
    public GameObject prefabToSpawn;
}

public class ShopTerminal : MonoBehaviour
{
    [Header("Shop Database")]
    [Tooltip("0 = Camera, 1 = Light, 2 = SD Card")]
    public List<ShopItem> availableItems = new List<ShopItem>();

    [Header("Dual UI Canvases")]
    public Canvas worldSpaceCanvas;
    public Canvas screenSpaceCanvas;

    [Header("UI Elements (Assign from BOTH Canvases)")]
    public TextMeshProUGUI[] totalCostTexts;
    public UnityEngine.UI.Button[] cameraCartButtons;
    public TextMeshProUGUI[] cameraCartTexts;

    [Header("Spawning")]
    public Transform deliveryZone;

    // Cart Tracking
    private List<ShopItem> shoppingCart = new List<ShopItem>();
    private int currentTotalCost = 0;
    private bool cameraSoldOut = false;

    // Player & Component Tracking
    private PlayerController playerController;
    private GameObject mainPlayerUI;
    private CrosshairUIClicker crosshairClicker; // THE FIX: Track your custom clicker!

    private void Start()
    {
        if (worldSpaceCanvas != null) worldSpaceCanvas.gameObject.SetActive(true);
        if (screenSpaceCanvas != null) screenSpaceCanvas.gameObject.SetActive(false);

        // Find the Player UI
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas canvas in allCanvases)
        {
            Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name == "Player UI" || child.name == "PlayerUI" || child.name == "Main UI")
                    mainPlayerUI = child.gameObject;
            }
        }

        // THE FIX: Find your crosshair clicker script in the scene
        crosshairClicker = FindObjectOfType<CrosshairUIClicker>();

        UpdateTotalUI();
    }

    public void MarkCameraSoldOut()
    {
        cameraSoldOut = true;

        foreach (var btn in cameraCartButtons)
            if (btn != null) btn.interactable = false;

        foreach (var txt in cameraCartTexts)
            if (txt != null) txt.text = "SOLD OUT";
    }

    public void AddItemToCartByIndex(int itemIndex)
    {
        if (itemIndex == 0 && cameraSoldOut) return;

        if (itemIndex >= 0 && itemIndex < availableItems.Count)
        {
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanBuyItem(itemIndex)) return;

            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.BuyLights)
            {
                if (shoppingCart.Count >= 1)
                {
                    TutorialManager.Instance.ShowWarning("You only need one light! Click Confirm.");
                    return;
                }
            }

            ShopItem itemToAdd = availableItems[itemIndex];

            if (itemIndex == 0 && shoppingCart.Contains(itemToAdd))
            {
                Debug.LogWarning("You can only buy ONE camera!");
                return;
            }

            shoppingCart.Add(itemToAdd);
            currentTotalCost += itemToAdd.price;

            UpdateTotalUI();
        }
    }

    public void ConfirmPurchase()
    {
        if (shoppingCart.Count == 0) return;

        if (CareerManager.Instance != null)
        {
            if (CareerManager.Instance.playerMoney >= currentTotalCost)
            {
                CareerManager.Instance.playerMoney -= currentTotalCost;
                CareerManager.Instance.UpdateMoneyUI();
                SpawnItemsAndFinish();
            }
            else
            {
                if (TutorialManager.Instance != null) TutorialManager.Instance.ShowWarning("Not enough B coins!");
            }
        }
        else
        {
            SpawnItemsAndFinish();
        }
    }

    public void CancelPurchase()
    {
        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();
    }

    private void SpawnItemsAndFinish()
    {
        bool boughtCameraThisTrip = false;

        foreach (ShopItem item in shoppingCart)
        {
            if (item == availableItems[0]) boughtCameraThisTrip = true;

            if (item.prefabToSpawn != null && deliveryZone != null)
            {
                Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, Random.Range(-0.2f, 0.2f));
                Instantiate(item.prefabToSpawn, deliveryZone.position + randomOffset, deliveryZone.rotation);
            }
        }

        if (boughtCameraThisTrip) MarkCameraSoldOut();

        if (shoppingCart.Count > 0 && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnEquipmentBought(shoppingCart.Count);
        }

        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();
    }

    private void UpdateTotalUI()
    {
        foreach (var txt in totalCostTexts)
        {
            if (txt != null) txt.text = "Total: B " + currentTotalCost;
        }
    }

    // ==========================================
    // TERMINAL INTERACTION (SCREEN SPACE UI)
    // ==========================================
    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        playerController = pController;
        if (playerController != null) playerController.enabled = false;

        if (mainPlayerUI != null) mainPlayerUI.SetActive(false);

        // THE FIX: Turn off the crosshair clicker so it doesn't steal your mouse clicks!
        if (crosshairClicker != null) crosshairClicker.enabled = false;

        if (screenSpaceCanvas != null) screenSpaceCanvas.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseTerminal()
    {
        if (playerController != null) playerController.enabled = true;

        if (mainPlayerUI != null) mainPlayerUI.SetActive(true);

        // THE FIX: Turn the crosshair clicker back on when you leave the shop!
        if (crosshairClicker != null) crosshairClicker.enabled = true;

        if (screenSpaceCanvas != null) screenSpaceCanvas.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        CancelPurchase();
    }
}