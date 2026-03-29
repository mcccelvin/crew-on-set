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
    public GameObject firstButtonToFocus;
    public TextMeshProUGUI totalCostText;

    [Header("Spawning")]
    public Transform deliveryZone;

    private PlayerController playerController;
    private bool isTerminalActive = false;

    // Cart Tracking
    private List<ShopItem> shoppingCart = new List<ShopItem>();
    private int currentTotalCost = 0;

    private void Start()
    {
        UpdateTotalUI();
    }

    private void Update()
    {
        if (isTerminalActive && Input.GetKeyDown(KeyCode.Escape)) CancelPurchase();
    }

    // --- BUTTON LINKS ---

    public void AddItemToCartByIndex(int itemIndex)
    {
        if (itemIndex >= 0 && itemIndex < availableItems.Count)
        {
            // Ask the Tutorial Boss for permission before adding!
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanBuyItem(itemIndex))
            {
                return;
            }

            ShopItem itemToAdd = availableItems[itemIndex];

            // --- THE NEW FIX: Check if the cart ALREADY has this exact item! ---
            if (shoppingCart.Contains(itemToAdd))
            {
                Debug.LogWarning("You already added " + itemToAdd.itemName + " to your cart!");
                return; // Stop the code here so it doesn't add a duplicate!
            }

            // If it's not in the cart yet, add it safely
            shoppingCart.Add(itemToAdd);
            currentTotalCost += itemToAdd.price;

            UpdateTotalUI();
            Debug.Log($"Added {itemToAdd.itemName}. Total: {currentTotalCost}");
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
                Debug.LogWarning("Not enough money!");
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
        CloseTerminal();
    }

    // --- HELPER METHODS ---
    private void SpawnItemsAndFinish()
    {
        foreach (ShopItem item in shoppingCart)
        {
            if (item.prefabToSpawn != null && deliveryZone != null)
            {
                Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, Random.Range(-0.2f, 0.2f));
                Instantiate(item.prefabToSpawn, deliveryZone.position + randomOffset, deliveryZone.rotation);

                if (TutorialManager.Instance != null) TutorialManager.Instance.OnEquipmentBought();
            }
        }

        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();
        CloseTerminal();
    }

    private void UpdateTotalUI()
    {
        if (totalCostText != null)
        {
            totalCostText.text = "Total: B " + currentTotalCost;
        }
    }

    // --- TERMINAL CONTROLS ---
    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        isTerminalActive = true;
        playerController = pController;
        if (playerController != null) playerController.enabled = false;

        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();

        if (shopCanvas != null && pCam != null)
        {
            shopCanvas.worldCamera = pCam.GetComponent<Camera>();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (firstButtonToFocus != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstButtonToFocus);
        }
    }

    public void CloseTerminal()
    {
        isTerminalActive = false;
        if (playerController != null) playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        EventSystem.current.SetSelectedGameObject(null);
    }
}