using PlayerPrefs = GameSavePrefs;
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
    private int deliveredCardCount;
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

    private void Awake()
    {
        foreach (var item in availableItems)
            if (item != null) item.price = ProductionEconomy.EquipmentPrice(item.itemName, item.price);
    }

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

        ProductionKitShop.Setup(this);
        RestoreOwnedEquipment();
        crosshairClicker = FindObjectOfType<CrosshairUIClicker>();
        UpdateTotalUI();
    }

    public static int OwnedPanelLights => PlayerPrefs.GetInt("OwnedEquipment.160 LED PANEL",
        CampaignProgression.GetCurrentLevel() >= 2 ? 1 : 0);

    public void RestoreOwnedEquipment()
    {
        if (deliveryZone == null) return;
        // Migrate completed first-contract careers created before equipment ownership was saved.
        if (CampaignProgression.GetCurrentLevel() >= 2)
        {
            if (!PlayerPrefs.HasKey("OwnedEquipment.160 LED PANEL")) PlayerPrefs.SetInt("OwnedEquipment.160 LED PANEL", 1);
            if (availableItems.Count > 0 && !PlayerPrefs.HasKey("OwnedEquipment." + availableItems[0].itemName))
                PlayerPrefs.SetInt("OwnedEquipment." + availableItems[0].itemName, 1);
        }
        foreach (ShopItem item in availableItems)
        {
            if (item == null || item.prefabToSpawn == null || item.itemName.ToUpperInvariant().Contains("SD")) continue;
            if(item.prefabToSpawn.GetComponent<ProductionKit>() != null &&
                !ProductionKitShop.HasEquipmentLesson(CampaignProgression.GetCurrentLevel())) continue;
            if (item.itemName == "LEVEL 2 CAMERA" || item.itemName.ToUpperInvariant().Contains("SOFT LIGHT")) continue; // Existing upgrade restoration owns these.
            int owned = PlayerPrefs.GetInt("OwnedEquipment." + item.itemName, 0);
            int present = 0;
            foreach (Player.Equipment.Equipment equipment in FindObjectsOfType<Player.Equipment.Equipment>(true))
                if (equipment.name == item.prefabToSpawn.name + "(Clone)") present++;
            for (int i = present; i < owned; i++)
            {
                var restored = Instantiate(item.prefabToSpawn, deliveryZone.position + new Vector3((i % 3 - 1) * .45f, .5f + (i / 3) * .25f, .25f), deliveryZone.rotation);
                if (restored.TryGetComponent<ProductionKit>(out var kit)) kit.ActivateDelivery();
            }
        }
        PlayerPrefs.Save();
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
            level2Camera.price = ProductionEconomy.AdvancedCamera;
            level2Camera.prefabToSpawn = cameraPrefab;
            availableItems.Add(level2Camera);
            level2CameraItemIndex = availableItems.Count - 1;
        }
        else
        {
            availableItems[level2CameraItemIndex].price = ProductionEconomy.AdvancedCamera;
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
            level3Light.price = ProductionEconomy.SoftLight;
            level3Light.prefabToSpawn = lightPrefab;
            availableItems.Add(level3Light);
            level3LightItemIndex = availableItems.Count - 1;
        }
        else
        {
            availableItems[level3LightItemIndex].price = ProductionEconomy.SoftLight;
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
            else if (shopText.text == "100" || shopText.text == "1,200") shopText.text = ProductionEconomy.SoftLight.ToString("N0");
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

    public void CreateStripShopCard(Canvas canvas, int index)
    {
        if(canvas==null || FindShopText(canvas,"LIGHT STRIP")!=null)return;
        var original=FindShopItemCard(FindShopText(canvas,"160 LED PANEL"),canvas) as RectTransform;
        var sd=FindShopItemCard(FindShopText(canvas,"SD CARD"),canvas) as RectTransform;
        if(original==null)return;
        var card=Instantiate(original.gameObject,original.parent);
        card.name="LIGHT STRIP";
        var rect=card.GetComponent<RectTransform>();
        rect.anchoredPosition=(sd!=null?sd.anchoredPosition:original.anchoredPosition+new Vector2(original.rect.width+15,0))+new Vector2(0,-original.rect.height-15);
        foreach(var text in card.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if(text.text=="160 LED PANEL")text.text="LIGHT STRIP";
            else if(text.text.Replace(",","").Trim()=="1200"||text.text.Trim()=="100")text.text=availableItems[index].price.ToString("N0");
            else if(text.text.Contains("SOLD OUT"))text.text="+ ADD TO CART";
        }
        foreach(var button in card.GetComponentsInChildren<Button>(true))
        {
            if(!IsCartButton(button))continue;
            button.onClick=new Button.ButtonClickedEvent();
            button.onClick.AddListener(()=>AddItemToCartByIndex(index));button.interactable=true;
        }
        // Reuse the panel-light illustration as a temporary strip icon.
        var icon=card.GetComponentsInChildren<Image>(true);
        foreach(var image in icon)if(image.sprite!=null && image.GetComponent<Button>()==null && image.transform!=card.transform)
        {
            if(image.rectTransform.rect.height>40 && image.rectTransform.rect.width>40)
                availableItems[index].prefabToSpawn.GetComponent<ProductionKit>().EquipmentIcon=image.sprite;
        }
        card.SetActive(true);
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
            else if (shopText.text == "4,000" || shopText.text == "4000") shopText.text = ProductionEconomy.AdvancedCamera.ToString("N0");
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
        return FindShopTextInternal(shopCanvas, textToFind);
    }

    public RectTransform GetTutorialCartTarget(string itemName)
    {
        Canvas canvas = screenSpaceCanvas != null && screenSpaceCanvas.gameObject.activeInHierarchy ? screenSpaceCanvas : worldSpaceCanvas;
        TextMeshProUGUI title = FindShopText(canvas, itemName);
        if (title == null) return null;
        Transform node = title.transform.parent;
        while (node != null && node != canvas.transform)
        {
            foreach (Button button in node.GetComponentsInChildren<Button>(true))
                if (button.gameObject.activeInHierarchy && button.interactable) return button.transform as RectTransform;
            node = node.parent;
        }
        return null;
    }

    private TextMeshProUGUI FindShopTextInternal(Canvas shopCanvas, string textToFind)
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
            ShopItem itemToAdd = availableItems[itemIndex];

            if (itemToAdd == null) return;
            if ((itemIndex == 0 || itemIndex == level2CameraItemIndex || itemToAdd.itemName.Contains("CAMERA")) && shoppingCart.Contains(itemToAdd))
            {
                Debug.LogWarning("You can only buy ONE camera!");
                return;
            }

            // Later-level purchase gates also advance their lesson, so validate the item first.
            if (TutorialManager.Instance != null && !TutorialManager.Instance.CanBuyItem(itemIndex)) return;

            shoppingCart.Add(itemToAdd);
            currentTotalCost += itemToAdd.price;

            UpdateTotalUI();
            // Advance only after the requested item really entered the cart.
            if (TutorialManager.Instance != null)
            {
                if (itemIndex == 1) TutorialManager.Instance.OnLightAddedToCart();
                else if (itemIndex == 0) TutorialManager.Instance.OnCameraAddedToCart();
                else if (itemIndex == 2) TutorialManager.Instance.OnSDCardAddedToCart();
            }
        }
    }

    private bool CartContainsItem(int index)
    {
        return index >= 0 && index < availableItems.Count && availableItems[index] != null &&
            shoppingCart.Contains(availableItems[index]);
    }

    public void ConfirmPurchase()
    {
        if (GokeLevelManager.Instance != null && GokeLevelManager.Instance.IsEquipmentIntroductionActive())
            GokeLevelManager.EnsureEquipmentAdvance();
        if (TutorialManager.Instance != null &&
            !TutorialManager.Instance.CanCheckoutTutorialCart(CartContainsItem(0), CartContainsItem(1), CartContainsItem(2))) return;
        if (shoppingCart.Count == 0) return;
        if (deliveryZone == null || CareerManager.Instance == null)
        {
            Debug.LogWarning("Shop unavailable: missing delivery zone or budget manager.");
            return;
        }
        long verifiedTotal = 0;
        foreach (ShopItem item in shoppingCart)
        {
            if (item == null || item.prefabToSpawn == null || item.price < 0) return;
            verifiedTotal += item.price;
        }
        if (verifiedTotal > int.MaxValue) return;
        currentTotalCost = (int)verifiedTotal;
        if (GokeLevelManager.Instance != null &&
            GokeLevelManager.Instance.IsEquipmentIntroductionActive() &&
            !GokeLevelManager.Instance.CanConfirmPurchase()) return;
        if (Level3Manager.Instance != null &&
            Level3Manager.Instance.IsEquipmentIntroductionActive() &&
            !Level3Manager.Instance.CanConfirmPurchase()) return;

        if (CareerManager.Instance != null && CareerManager.Instance.TrySpendMoney(currentTotalCost))
        {
            SpawnItemsAndFinish();
        }
        // TrySpendMoney displays a notification even when tutorials are disabled.
    }

    public void CancelPurchase()
    {
        if (GokeLevelManager.Instance != null &&
            GokeLevelManager.Instance.IsEquipmentIntroductionActive() &&
            !GokeLevelManager.Instance.CanCancelPurchase()) return;
        if (Level3Manager.Instance != null &&
            Level3Manager.Instance.IsEquipmentIntroductionActive() &&
            !Level3Manager.Instance.CanCancelPurchase()) return;

        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanCloseUI("ShopTerminal")) return;

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
                if (spawnedItem.TryGetComponent<Player.Equipment.SDCardItem>(out var card))
                {
                    // Keep tiny cards visible and separated instead of dropping them among equipment.
                    int slot = deliveredCardCount++;
                    Vector3 position = deliveryZone.position + deliveryZone.right * ((slot % 3 - 1) * 0.14f)
                        + deliveryZone.forward * (((slot / 3) % 3 - 1) * 0.14f);
                    float surfaceY = position.y;
                    float closest = float.PositiveInfinity;
                    foreach (RaycastHit hit in Physics.RaycastAll(position + Vector3.up * 0.5f,
                        Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.GetComponentInParent<Player.Equipment.Equipment>() != null) continue;
                        if (hit.distance >= closest) continue;
                        closest = hit.distance;
                        surfaceY = hit.point.y;
                    }
                    position.y = surfaceY + 0.01f + (slot / 9) * 0.012f;
                    card.PrepareShopDelivery(position);
                }
                if (spawnedItem.TryGetComponent<ProductionKit>(out var kit)) kit.ActivateDelivery();
                if (!item.itemName.ToUpperInvariant().Contains("SD"))
                    PlayerPrefs.SetInt("OwnedEquipment." + item.itemName, PlayerPrefs.GetInt("OwnedEquipment." + item.itemName, 0) + 1);
                if (level3LightItemIndex >= 0 && item == availableItems[level3LightItemIndex]) ConfigureLevel3Light(spawnedItem);
            }
        }

        PlayerPrefs.Save();
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
            equipment.EquipmentControls = "[LMB] Power  |  [SCROLL] Intensity  |  [UP/DOWN] Tilt  |  [Z/K] Kelvin  |  [V/B] Diffusion  |  [G] Drop";
            equipment.HoldPositionOffset = new Vector3(0.45f, -0.35f, 1.05f);
            equipment.HoldRotationOffset = new Vector3(0f, -90f, 0f);
        }

        Player.Equipment.FilmLightItem filmLight = spawnedItem.GetComponent<Player.Equipment.FilmLightItem>();
        if (filmLight != null)
        {
            filmLight.maxLux = 6f;
            filmLight.isFixedKelvin = false;
            filmLight.forcesHardLight = false;
            filmLight.colorTemperature = 4300f;
            filmLight.diffusionPercent = 50f;
            if (filmLight.spotlight != null) filmLight.spotlight.shadows = LightShadows.Soft;
            filmLight.RefreshAdvancedFeatures();
        }

        // Reuse the real LED head and stand. Do not hide them behind primitive blocks.
        foreach (Renderer part in spawnedItem.GetComponentsInChildren<Renderer>(true))
        {
            if (part.gameObject.name != "Cube.001") continue;
            MaterialPropertyBlock housingProperties = new MaterialPropertyBlock();
            part.GetPropertyBlock(housingProperties);
            housingProperties.SetColor("_Color", new Color(0.075f, 0.09f, 0.105f));
            part.SetPropertyBlock(housingProperties);
        }
        if (filmLight != null) filmLight.RefreshAdvancedFeatures();
    }

    private void CreateLevel3LightPlaceholder(GameObject spawnedItem)
    {
        Renderer[] itemRenderers = spawnedItem.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer itemRenderer in itemRenderers)
        {
            itemRenderer.enabled = false;
        }

        Collider[] itemColliders = spawnedItem.GetComponentsInChildren<Collider>(true);
        foreach (Collider itemCollider in itemColliders)
        {
            if (itemCollider != null) Destroy(itemCollider);
        }

        Color frameColor = new Color(0.08f, 0.1f, 0.13f, 1f);
        Color panelColor = new Color(0.22f, 0.72f, 0.85f, 1f);

        CreateLevel3LightPart(spawnedItem, "Thin Light Panel", new Vector3(0f, 1.15f, 0f), new Vector3(0.12f, 0.42f, 0.82f), panelColor);
        CreateLevel3LightPart(spawnedItem, "Thin Light Stand", new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 0.95f, 0.08f), frameColor);
        CreateLevel3LightPart(spawnedItem, "Thin Light Base", new Vector3(0f, 0.06f, 0f), new Vector3(0.32f, 0.1f, 0.52f), frameColor);
    }

    private void CreateLevel3LightPart(GameObject lightRoot, string partName, Vector3 localPosition, Vector3 localScale, Color partColor)
    {
        GameObject lightPart = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightPart.name = partName;
        lightPart.layer = lightRoot.layer;
        lightPart.transform.SetParent(lightRoot.transform);
        lightPart.transform.localPosition = localPosition;
        lightPart.transform.localRotation = Quaternion.identity;
        lightPart.transform.localScale = localScale;

        Renderer partRenderer = lightPart.GetComponent<Renderer>();
        if (partRenderer != null)
        {
            partRenderer.material.color = partColor;
            partRenderer.material.EnableKeyword("_EMISSION");
            partRenderer.material.SetColor("_EmissionColor", partColor * 0.2f);
        }
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
        ProductionKitShop.Setup(this);
        isTerminalActive = true;
        playerController = pController;
        if (playerController != null) playerController.enabled = false;

        if (mainPlayerUI != null) mainPlayerUI.SetActive(false);

        if (crosshairClicker != null) crosshairClicker.enabled = false;

        if (screenSpaceCanvas != null) screenSpaceCanvas.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.RecoverTutorialCart(CartContainsItem(0), CartContainsItem(1), CartContainsItem(2));
            TutorialManager.Instance.OnShopOpened();
        }
    }

    public void CloseTerminal()
    {
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanCloseUI("ShopTerminal")) return;
        if (GokeLevelManager.Instance != null &&
            GokeLevelManager.Instance.IsEquipmentIntroductionActive() &&
            !GokeLevelManager.Instance.CanCancelPurchase()) return;
        if (Level3Manager.Instance != null &&
            Level3Manager.Instance.IsEquipmentIntroductionActive() &&
            !Level3Manager.Instance.CanCancelPurchase()) return;

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

