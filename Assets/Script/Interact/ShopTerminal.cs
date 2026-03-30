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

    [Header("UI Elements & Focus")]
    public Canvas shopCanvas;
    public TextMeshProUGUI totalCostText;

    [Header("Camera Sold Out UI")]
    public UnityEngine.UI.Button cameraCartButton;
    public TextMeshProUGUI cameraCartText;
    private bool cameraSoldOut = false;

    [Header("Spawning")]
    public Transform deliveryZone;

    // Cart Tracking
    private List<ShopItem> shoppingCart = new List<ShopItem>();
    private int currentTotalCost = 0;

    private void Start()
    {
        UpdateTotalUI();
    }

    public void MarkCameraSoldOut()
    {
        cameraSoldOut = true;
        if (cameraCartButton != null) cameraCartButton.interactable = false;
        if (cameraCartText != null) cameraCartText.text = "SOLD OUT";
    }

    public void AddItemToCartByIndex(int itemIndex)
    {
        if (itemIndex == 0 && cameraSoldOut) return;

        if (itemIndex >= 0 && itemIndex < availableItems.Count)
        {
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanBuyItem(itemIndex)) return;

            // --- Restrict cart to exactly 1 item during the "Buy Light" tutorial task! ---
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.BuyLights)
            {
                if (shoppingCart.Count >= 1)
                {
                    TutorialManager.Instance.ShowWarning("You only need one light! Click Confirm.");
                    return;
                }
            }
            // -----------------------------------------------------------------------------

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
            // --- Send the EXACT number of items in the cart to the Boss! ---
            TutorialManager.Instance.OnEquipmentBought(shoppingCart.Count);
        }

        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();
    }

    private void UpdateTotalUI()
    {
        if (totalCostText != null)
        {
            totalCostText.text = "Total: B " + currentTotalCost;
        }
    }

    // --- LEFT BLANK TO PREVENT ERRORS FOR CROSSHAIR CLICKER ---
    public void OpenTerminal(GameObject pCam, PlayerController pController) { }
    public void CloseTerminal() { }
}