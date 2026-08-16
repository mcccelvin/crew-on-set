using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

    [Header("Level 3 Equipment")]
    public GameObject level3LightPrefab;

    // Cart Tracking
    private List<ShopItem> shoppingCart = new List<ShopItem>();
    private int currentTotalCost = 0;
    private bool cameraSoldOut = false;
    private bool level2CameraSoldOut = false;
    private int level2CameraItemIndex = -1;
    private List<GameObject> level2CameraCards = new List<GameObject>();
    private bool level3LightSoldOut = false;
    private int level3LightItemIndex = -1;
    private List<GameObject> level3LightCards = new List<GameObject>();
    private bool useLevel3LightPlaceholder = false;

    // Player & Component Tracking
    private PlayerController playerController;
    private GameObject mainPlayerUI;
    private CrosshairUIClicker crosshairClicker;
    private bool isTerminalActive = false;

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

        crosshairClicker = FindObjectOfType<CrosshairUIClicker>();
        UpdateTotalUI();
    }

    public void MarkCameraSoldOut()
    {
        cameraSoldOut = true;

        MarkOriginalCameraSoldOut(worldSpaceCanvas);
        MarkOriginalCameraSoldOut(screenSpaceCanvas);

        foreach (var btn in cameraCartButtons)
            if (btn != null) btn.interactable = false;

        foreach (var txt in cameraCartTexts)
            if (txt != null) txt.text = "SOLD OUT";
    }

    public int SetupLevel2Camera(GameObject cameraPrefab)
    {
        if (cameraPrefab == null || availableItems.Count == 0) return -1;

        level2CameraItemIndex = availableItems.FindIndex(item => item.itemName == "LEVEL 2 CAMERA");
        if (level2CameraItemIndex == -1)
        {
            ShopItem level2Camera = new ShopItem();
            level2Camera.itemName = "LEVEL 2 CAMERA";
            level2Camera.price = 10000;
            level2Camera.prefabToSpawn = cameraPrefab;
            availableItems.Add(level2Camera);
            level2CameraItemIndex = availableItems.Count - 1;
        }
        else
        {
            availableItems[level2CameraItemIndex].price = 10000;
            availableItems[level2CameraItemIndex].prefabToSpawn = cameraPrefab;
        }

        CreateLevel2CameraShopCard(worldSpaceCanvas);
        CreateLevel2CameraShopCard(screenSpaceCanvas);
        MarkCameraSoldOut();

        return level2CameraItemIndex;
    }

    public void RestoreLevel2Camera(GameObject cameraPrefab)
    {
        if (cameraPrefab == null) return;

        SetupLevel2Camera(cameraPrefab);
        MarkLevel2CameraSoldOut();

        PlayerPrefs.SetInt("Level2CameraPurchased", 1);
        PlayerPrefs.Save();

        Player.Equipment.FilmCameraItem[] existingCameras = FindObjectsOfType<Player.Equipment.FilmCameraItem>(true);
        foreach (Player.Equipment.FilmCameraItem existingCamera in existingCameras)
        {
            if (existingCamera.EquipmentName == "Level 2 Camera") return;
        }

        if (deliveryZone == null) return;

        Vector3 cameraPosition = deliveryZone.position + new Vector3(-0.35f, 0.5f, 0f);
        GameObject restoredCamera = Instantiate(cameraPrefab, cameraPosition, deliveryZone.rotation);
        restoredCamera.name = "Level 2 Camera";
    }

    public int SetupLevel3Light(GameObject lightPrefab, bool usePlaceholder)
    {
        if (lightPrefab == null || availableItems.Count < 2) return -1;

        useLevel3LightPlaceholder = usePlaceholder;

        level3LightItemIndex = availableItems.FindIndex(item => item.itemName == "LEVEL 3 SOFT LIGHT");
        if (level3LightItemIndex == -1)
        {
            ShopItem level3Light = new ShopItem();
            level3Light.itemName = "LEVEL 3 SOFT LIGHT";
            level3Light.price = 5000;
            level3Light.prefabToSpawn = lightPrefab;
            availableItems.Add(level3Light);
            level3LightItemIndex = availableItems.Count - 1;
        }
        else
        {
            availableItems[level3LightItemIndex].price = 5000;
            availableItems[level3LightItemIndex].prefabToSpawn = lightPrefab;
        }

        CreateLevel3LightShopCard(worldSpaceCanvas);
        CreateLevel3LightShopCard(screenSpaceCanvas);

        return level3LightItemIndex;
    }

    public void RestoreLevel3Light(GameObject lightPrefab, bool usePlaceholder)
    {
        if (lightPrefab == null) return;

        SetupLevel3Light(lightPrefab, usePlaceholder);
        MarkLevel3LightSoldOut();

        PlayerPrefs.SetInt("Level3LightPurchased", 1);
        PlayerPrefs.Save();

        Player.Equipment.FilmLightItem[] existingLights = FindObjectsOfType<Player.Equipment.FilmLightItem>(true);
        foreach (Player.Equipment.FilmLightItem existingLight in existingLights)
        {
            if (existingLight.EquipmentName == "Level 3 Soft Light") return;
        }

        if (deliveryZone == null) return;

        Vector3 lightPosition = deliveryZone.position + new Vector3(0.35f, 0.5f, 0f);
        GameObject restoredLight = Instantiate(lightPrefab, lightPosition, deliveryZone.rotation);
        ConfigureLevel3Light(restoredLight);
    }

    private void CreateLevel3LightShopCard(Canvas shopCanvas)
    {
        if (shopCanvas == null || FindShopText(shopCanvas, "LEVEL 3 SOFT LIGHT") != null) return;

        TextMeshProUGUI originalLightText = FindShopText(shopCanvas, "160 LED PANEL");
        Transform originalLightCard = FindShopItemCard(originalLightText, shopCanvas);
        if (originalLightCard == null) return;

        GameObject level3LightCard = Instantiate(originalLightCard.gameObject, originalLightCard.parent);
        level3LightCard.name = "Level 3 Soft Light";
        PositionLevel3LightCard(level3LightCard.transform as RectTransform, originalLightCard as RectTransform);

        TextMeshProUGUI[] shopTexts = level3LightCard.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI shopText in shopTexts)
        {
            if (shopText.text == "160 LED PANEL") shopText.text = "LEVEL 3 SOFT LIGHT";
            else if (shopText.text == "100") shopText.text = "5,000";
            else if (shopText.text == "SOLD OUT") shopText.text = "ADD TO CART";
        }

        Button[] cardButtons = level3LightCard.GetComponentsInChildren<Button>(true);
        foreach (Button cardButton in cardButtons)
        {
            if (!IsCartButton(cardButton)) continue;

            cardButton.onClick = new Button.ButtonClickedEvent();
            cardButton.onClick.AddListener(() => AddItemToCartByIndex(level3LightItemIndex));
            cardButton.enabled = true;
            cardButton.interactable = true;
        }

        level3LightCards.Add(level3LightCard);
    }

    private void PositionLevel3LightCard(RectTransform level3LightCard, RectTransform originalLightCard)
    {
        if (level3LightCard == null || originalLightCard == null) return;

        level3LightCard.anchoredPosition = originalLightCard.anchoredPosition + new Vector2(0f, -originalLightCard.rect.height - 15f);
        level3LightCard.SetAsLastSibling();
    }

    private void CreateLevel2CameraShopCard(Canvas shopCanvas)
    {
        if (shopCanvas == null || FindShopText(shopCanvas, "LEVEL 2 CAMERA") != null) return;

        TextMeshProUGUI originalCameraText = FindShopText(shopCanvas, "NONY FX");
        Transform originalCameraCard = FindShopItemCard(originalCameraText, shopCanvas);
        if (originalCameraCard == null) return;

        GameObject level2CameraCard = Instantiate(originalCameraCard.gameObject, originalCameraCard.parent);
        level2CameraCard.name = "Level 2 Camera";
        PositionLevel2CameraCard(level2CameraCard.transform as RectTransform, originalCameraCard as RectTransform);

        TextMeshProUGUI[] shopTexts = level2CameraCard.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI shopText in shopTexts)
        {
            if (shopText.text == "NONY FX") shopText.text = "LEVEL 2 CAMERA";
            else if (shopText.text.Contains("Low End Camera")) shopText.text = "Level 2 Camera\n\nProfessional camera for Level 2.";
            else if (shopText.text == "4,000" || shopText.text == "4000") shopText.text = "10,000";
            else if (shopText.text == "SOLD OUT") shopText.text = "ADD TO CART";
        }

        Button[] cardButtons = level2CameraCard.GetComponentsInChildren<Button>(true);
        foreach (Button cardButton in cardButtons)
        {
            if (!IsCartButton(cardButton)) continue;

            cardButton.onClick = new Button.ButtonClickedEvent();
            cardButton.onClick.AddListener(() => AddItemToCartByIndex(level2CameraItemIndex));
            cardButton.enabled = true;
            cardButton.interactable = true;
        }

        level2CameraCards.Add(level2CameraCard);
    }

    private void PositionLevel2CameraCard(RectTransform level2CameraCard, RectTransform originalCameraCard)
    {
        if (level2CameraCard == null || originalCameraCard == null) return;

        level2CameraCard.anchoredPosition = originalCameraCard.anchoredPosition + new Vector2(0f, -originalCameraCard.rect.height - 15f);
        level2CameraCard.SetAsLastSibling();
    }

    private void MarkOriginalCameraSoldOut(Canvas shopCanvas)
    {
        TextMeshProUGUI originalCameraText = FindShopText(shopCanvas, "NONY FX");
        Transform originalCameraCard = FindShopItemCard(originalCameraText, shopCanvas);
        if (originalCameraCard == null) return;

        Button[] cardButtons = originalCameraCard.GetComponentsInChildren<Button>(true);
        foreach (Button cardButton in cardButtons)
        {
            if (IsCartButton(cardButton)) cardButton.interactable = false;
        }

        TextMeshProUGUI[] cardTexts = originalCameraCard.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI cardText in cardTexts)
        {
            if (cardText.text.Contains("ADD TO CART")) cardText.text = "SOLD OUT";
        }
    }

    private void MarkLevel2CameraSoldOut()
    {
        level2CameraSoldOut = true;

        foreach (GameObject level2CameraCard in level2CameraCards)
        {
            if (level2CameraCard == null) continue;

            Button[] cardButtons = level2CameraCard.GetComponentsInChildren<Button>(true);
            foreach (Button cardButton in cardButtons)
            {
                cardButton.interactable = false;
            }

            TextMeshProUGUI[] cardTexts = level2CameraCard.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI cardText in cardTexts)
            {
                if (cardText.text.Contains("ADD TO CART")) cardText.text = "SOLD OUT";
            }
        }
    }

    private void MarkLevel3LightSoldOut()
    {
        level3LightSoldOut = true;

        foreach (GameObject level3LightCard in level3LightCards)
        {
            if (level3LightCard == null) continue;

            Button[] cardButtons = level3LightCard.GetComponentsInChildren<Button>(true);
            foreach (Button cardButton in cardButtons)
            {
                cardButton.interactable = false;
            }

            TextMeshProUGUI[] cardTexts = level3LightCard.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (TextMeshProUGUI cardText in cardTexts)
            {
                if (cardText.text.Contains("ADD TO CART")) cardText.text = "SOLD OUT";
            }
        }
    }

    private TextMeshProUGUI FindShopText(Canvas shopCanvas, string textToFind)
    {
        if (shopCanvas == null) return null;

        TextMeshProUGUI[] shopTexts = shopCanvas.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI shopText in shopTexts)
        {
            if (shopText.text == textToFind) return shopText;
        }

        return null;
    }

    private Transform FindShopItemCard(TextMeshProUGUI itemNameText, Canvas shopCanvas)
    {
        if (itemNameText == null || shopCanvas == null) return null;

        Transform currentTransform = itemNameText.transform.parent;
        while (currentTransform != null && currentTransform != shopCanvas.transform)
        {
            Button[] cardButtons = currentTransform.GetComponentsInChildren<Button>(true);
            foreach (Button cardButton in cardButtons)
            {
                if (IsCartButton(cardButton)) return currentTransform;
            }

            currentTransform = currentTransform.parent;
        }

        return null;
    }

    private bool IsCartButton(Button button)
    {
        if (button == null) return false;

        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentMethodName(i) == "AddItemToCartByIndex") return true;
        }

        return false;
    }

    public void AddItemToCartByIndex(int itemIndex)
    {
        if (itemIndex == 0 && cameraSoldOut) return;
        if (itemIndex == level2CameraItemIndex && level2CameraSoldOut) return;
        if (itemIndex == level3LightItemIndex && level3LightSoldOut) return;

        if (itemIndex >= 0 && itemIndex < availableItems.Count)
        {
            // Ask the Tutorial Bouncer if we are allowed to buy this item yet
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanBuyItem(itemIndex)) return;

            // Talk to the TutorialManager to complete the step!
            if (TutorialManager.Instance != null)
            {
                // During the Light Step
                if (TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.BuyLight_AddToCart ||
                    TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.BuyLight_Checkout)
                {
                    if (shoppingCart.Count >= 1)
                    {
                        TutorialManager.Instance.ShowWarning("You only need one light! Click Checkout.");
                        return;
                    }
                    TutorialManager.Instance.OnLightAddedToCart();
                }
                // --- NEW: During the Camera Step ---
                else if (TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.BuyCamera_AddToCart)
                {
                    TutorialManager.Instance.OnCameraAddedToCart();
                }
                // --- NEW: During the SD Card Step ---
                else if (TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.BuySDCard_AddToCart)
                {
                    TutorialManager.Instance.OnSDCardAddedToCart();
                }
            }

            ShopItem itemToAdd = availableItems[itemIndex];

            if (itemToAdd.itemName.Contains("CAMERA") && shoppingCart.Contains(itemToAdd))
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
        if (GokeLevelManager.Instance != null &&
            GokeLevelManager.Instance.IsEquipmentIntroductionActive() &&
            !GokeLevelManager.Instance.CanConfirmPurchase()) return;

        if (CareerManager.Instance != null && CareerManager.Instance.TrySpendMoney(currentTotalCost))
        {
            SpawnItemsAndFinish();
        }
        else if (CareerManager.Instance == null)
        {
            SpawnItemsAndFinish();
        }
        else if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.ShowWarning("Not enough B coins!");
        }
    }

    public void CancelPurchase()
    {
        if (GokeLevelManager.Instance != null &&
            GokeLevelManager.Instance.IsEquipmentIntroductionActive() &&
            !GokeLevelManager.Instance.CanCancelPurchase()) return;

        // Prevents emptying the cart while mid-tutorial!
        if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep < TutorialManager.TutorialStep.OfferLevel1)
        {
            if (shoppingCart.Count > 0)
            {
                TutorialManager.Instance.ShowWarning("Don't cancel! Complete your purchase to continue.");
                return;
            }
        }

        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();
    }

    private void SpawnItemsAndFinish()
    {
        bool boughtCameraThisTrip = false;
        bool boughtLevel2CameraThisTrip = false;
        bool boughtLevel3LightThisTrip = false;

        foreach (ShopItem item in shoppingCart)
        {
            if (item == availableItems[0]) boughtCameraThisTrip = true;
            if (level2CameraItemIndex >= 0 && item == availableItems[level2CameraItemIndex]) boughtLevel2CameraThisTrip = true;
            if (level3LightItemIndex >= 0 && item == availableItems[level3LightItemIndex]) boughtLevel3LightThisTrip = true;

            if (item.prefabToSpawn != null && deliveryZone != null)
            {
                Vector3 randomOffset = new Vector3(Random.Range(-0.2f, 0.2f), 0.5f, Random.Range(-0.2f, 0.2f));
                GameObject spawnedItem = Instantiate(item.prefabToSpawn, deliveryZone.position + randomOffset, deliveryZone.rotation);
                if (level3LightItemIndex >= 0 && item == availableItems[level3LightItemIndex]) ConfigureLevel3Light(spawnedItem);
            }
        }

        if (boughtCameraThisTrip) MarkCameraSoldOut();
        if (boughtLevel2CameraThisTrip)
        {
            MarkLevel2CameraSoldOut();
            PlayerPrefs.SetInt("Level2CameraPurchased", 1);
            PlayerPrefs.Save();
        }
        if (boughtLevel3LightThisTrip)
        {
            MarkLevel3LightSoldOut();
            PlayerPrefs.SetInt("Level3LightPurchased", 1);
            PlayerPrefs.Save();
        }

        // Tell the Tutorial Manager that we successfully checked out!
        if (shoppingCart.Count > 0 && TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnEquipmentBought(shoppingCart.Count);
        }

        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();
    }

    private void ConfigureLevel3Light(GameObject spawnedItem)
    {
        if (spawnedItem == null) return;

        spawnedItem.name = "Level 3 Soft Light";

        Player.Equipment.Equipment equipment = spawnedItem.GetComponent<Player.Equipment.Equipment>();
        if (equipment != null)
        {
            equipment.EquipmentName = "Level 3 Soft Light";
            equipment.EquipmentControls = "[LMB] Power  |  [SCROLL] Intensity  |  [UP/DOWN] Tilt  |  [G] Drop";
        }

        Player.Equipment.FilmLightItem filmLight = spawnedItem.GetComponent<Player.Equipment.FilmLightItem>();
        if (filmLight != null)
        {
            filmLight.maxLux = 40f;
            filmLight.isFixedKelvin = false;
            filmLight.forcesHardLight = false;
            if (filmLight.spotlight != null) filmLight.spotlight.shadows = LightShadows.Soft;
        }

        if (useLevel3LightPlaceholder) CreateLevel3LightPlaceholder(spawnedItem);
    }

    private void CreateLevel3LightPlaceholder(GameObject spawnedItem)
    {
        Renderer[] itemRenderers = spawnedItem.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer itemRenderer in itemRenderers)
        {
            itemRenderer.enabled = false;
        }

        GameObject lightBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightBlock.name = "Level 3 Light Block";
        lightBlock.layer = spawnedItem.layer;
        lightBlock.transform.SetParent(spawnedItem.transform);
        lightBlock.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        lightBlock.transform.localRotation = Quaternion.identity;
        lightBlock.transform.localScale = new Vector3(0.7f, 2.5f, 0.7f);

        Renderer blockRenderer = lightBlock.GetComponent<Renderer>();
        if (blockRenderer != null) blockRenderer.material.color = new Color(0.12f, 0.14f, 0.18f, 1f);
    }

    private void UpdateTotalUI()
    {
        foreach (var txt in totalCostTexts)
        {
            if (txt != null) txt.text = "Total: B " + currentTotalCost;
        }
    }

    public void OpenTerminal(GameObject pCam, PlayerController pController)
    {
        isTerminalActive = true;
        playerController = pController;
        if (playerController != null) playerController.enabled = false;

        if (mainPlayerUI != null) mainPlayerUI.SetActive(false);

        if (crosshairClicker != null) crosshairClicker.enabled = false;

        if (screenSpaceCanvas != null) screenSpaceCanvas.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnShopOpened();
    }

    public void CloseTerminal()
    {
        isTerminalActive = false;
        if (playerController != null) playerController.enabled = true;

        if (mainPlayerUI != null) mainPlayerUI.SetActive(true);

        if (crosshairClicker != null) crosshairClicker.enabled = true;

        if (screenSpaceCanvas != null) screenSpaceCanvas.gameObject.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnShopClosed();

        // Force the cart to clear when you completely exit the terminal UI
        shoppingCart.Clear();
        currentTotalCost = 0;
        UpdateTotalUI();
    }

    public bool IsTerminalActive()
    {
        return isTerminalActive;
    }
}
