using UnityEngine;
using Player.Manager;

namespace Player.Interactor
{
    [RequireComponent(typeof(InputManager))]
    public class EquipmentInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera PlayerCamera;
        [SerializeField] private Transform HoldPoint;

        public HotbarUIManager hotbarUI;

        [Header("Settings")]
        [SerializeField] private float PickupRange = 3f;

        private InputManager inputManager;
        private Equipment.Equipment[] hotbar = new Equipment.Equipment[5];
        private int currentSlotIndex = 0;
        private Equipment.Equipment currentEquipment;

        private DirectorTerminal activeTerminal;
        private ComputerStation activeComputer;
        private ShopTerminal activeShop;

        private void Start()
        {
            inputManager = GetComponent<InputManager>();
            if (PlayerCamera != null) { PlayerCamera.gameObject.SetActive(true); PlayerCamera.enabled = true; }

            if (hotbarUI == null) hotbarUI = FindObjectOfType<HotbarUIManager>();
        }

        private void Update()
        {
            if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) { ClearHoverText(); return; }
            if (PauseManager.isPaused) { ClearHoverText(); return; }
            if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) { ClearHoverText(); return; }
            if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsContractUIOpen()) { ClearHoverText(); return; }

            if (activeTerminal != null && !activeTerminal.IsTerminalActive()) activeTerminal = null;
            if (activeShop != null && !activeShop.IsTerminalActive()) activeShop = null;

            if (activeTerminal != null)
            {
                if (hotbarUI != null) hotbarUI.UpdateGuideText("");
                if (inputManager.Interact)
                {
                    activeTerminal.CloseTerminal();
                    if (!activeTerminal.IsTerminalActive()) activeTerminal = null;
                }
                return;
            }

            if (activeShop != null)
            {
                if (hotbarUI != null) hotbarUI.UpdateGuideText("");
                if (inputManager.Interact)
                {
                    activeShop.CloseTerminal();
                    if (!activeShop.IsTerminalActive()) activeShop = null;
                }
                return;
            }

            if (activeComputer != null)
            {
                if (hotbarUI != null) hotbarUI.UpdateGuideText("");
                if (inputManager.Interact) { activeComputer.CloseComputerUI(); }
                return;
            }

            if (Cursor.visible || Cursor.lockState != CursorLockMode.Locked) return;

            HandleHotbarInput();
            HandleHoverText();

            Equipment.FilmCameraItem heldCamera = currentEquipment as Equipment.FilmCameraItem;
            bool isUsingCamera = heldCamera != null && heldCamera.IsCameraViewActive();

            if (inputManager.Interact && !isUsingCamera) { TryPickupOrInteract(); return; }
            if (inputManager.Drop && currentEquipment != null) { DropEquipment(); return; }

            if (inputManager.ConsumeUse())
            {
                if (CrosshairUIClicker.TryClickButton()) return;

                if (currentEquipment != null)
                {
                    currentEquipment.OnUse(PlayerCamera);
                }
            }

            if (inputManager.Equip)
            {
                TryInsertIntoComputer();
            }

            if (currentEquipment != null) currentEquipment.OnHeldUpdate(inputManager);
        }

        private void ClearHoverText()
        {
            if (hotbarUI != null) hotbarUI.UpdateGuideText("");
        }

        private bool TryInsertIntoComputer()
        {
            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, PickupRange))
            {
                ComputerStation computer = hit.collider.GetComponentInParent<ComputerStation>();
                if (computer != null)
                {
                    computer.TryInsertCard(this);
                    return true;
                }
            }
            return false;
        }

        private void HandleHoverText()
        {
            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            string targetText = "";

            if (Physics.Raycast(ray, out RaycastHit hit, PickupRange))
            {
                Equipment.Equipment item = hit.collider.GetComponentInParent<Equipment.Equipment>();
                if (item != null)
                {
                    targetText = $"[E] Pick Up {item.EquipmentName}";
                }
                else if (hit.collider.GetComponentInParent<ComputerStation>() != null)
                {
                    if (currentEquipment != null && currentEquipment.GetComponent<Equipment.SDCardItem>() != null)
                        targetText = "[F] Insert SD Card | [E] Open Menu";
                    else
                        targetText = "[E] Open Computer Menu";
                }
                else if (hit.collider.GetComponentInParent<DirectorTerminal>() != null)
                {
                    targetText = "<size=125%><color=#20E6FF><b>[ E ]</b></color></size>  <b>ENTER DIRECTOR TABLET</b>\n" +
                                 "<size=72%><color=#C5D2DE>BUILD   |   COLOR   |   PLACE</color></size>";
                }
                else if (hit.collider.GetComponentInParent<ShopTerminal>() != null)
                {
                    targetText = "[E] Shop Terminal";
                }
                else if (hit.collider.GetComponentInParent<IInteractable>() != null)
                {
                    targetText = "[E] Interact";
                }
            }

            if (targetText == "")
            {
                if (currentEquipment != null)
                {
                    if (hotbarUI != null) hotbarUI.UpdateEquipmentGuide(currentEquipment.EquipmentControls);
                    return;
                }
            }

            if (hotbarUI != null) hotbarUI.UpdateGuideText(targetText);
        }

        public void ClearActiveComputer() { activeComputer = null; }

        private void HandleHotbarInput()
        {
            if (inputManager.HotbarSlot >= 0) SwitchSlot(inputManager.HotbarSlot);
        }

        private void SwitchSlot(int newSlotIndex)
        {
            if (currentSlotIndex == newSlotIndex) return;

            if (currentEquipment != null) currentEquipment.gameObject.SetActive(false);

            currentSlotIndex = newSlotIndex;
            currentEquipment = hotbar[currentSlotIndex];

            if (currentEquipment != null) currentEquipment.gameObject.SetActive(true);

            if (hotbarUI != null) hotbarUI.HighlightSlot(currentSlotIndex);
        }

        private void TryPickupOrInteract()
        {
            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, PickupRange))
            {
                // --- THE FIX: Computer Station Security Block! ---
                ComputerStation computer = hit.collider.GetComponentInParent<ComputerStation>();
                if (computer != null)
                {
                    if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialManager.TutorialStep.InsertToComputer)
                    {
                        TutorialManager.Instance.ShowWarning("Insert the SD card first! Hold it and press [F].");
                        return;
                    }

                    activeComputer = computer;
                    activeComputer.OpenComputerUI(this);

                    // Tells the tutorial that they successfully opened it!
                    if (TutorialManager.Instance != null) TutorialManager.Instance.OnComputerOpened();
                    return;
                }


                Equipment.Equipment item = hit.collider.GetComponentInParent<Equipment.Equipment>();
                if (item != null)
                {
                    Equipment.FilmLightItem lightItem = item.GetComponent<Equipment.FilmLightItem>();
                    if (lightItem != null && TutorialManager.Instance != null && !TutorialManager.Instance.CanPickUpLight(lightItem)) return;

                    for (int i = 0; i < 5; i++)
                    {
                        if (hotbar[i] == null)
                        {
                            hotbar[i] = item;
                            if (item is Equipment.ActorMegaphoneItem megaphone)
                                megaphone.OnPickedUp(HoldPoint, PlayerCamera);
                            else
                                item.OnPickedUp(HoldPoint);

                            if (i != currentSlotIndex)
                            {
                                item.gameObject.SetActive(false);
                            }
                            else
                            {
                                currentEquipment = item;
                                currentEquipment.gameObject.SetActive(true);
                            }

                            if (hotbarUI != null) hotbarUI.UpdateSlot(i, item.EquipmentName, item.EquipmentIcon);
                            if (item is Equipment.ActorMegaphoneItem) SwitchSlot(i);

                            if (TutorialManager.Instance != null)
                            {
                                string lowerObjName = item.gameObject.name.ToLower();
                                string lowerItemName = item.EquipmentName.ToLower();

                                if (lowerObjName.Contains("light") || lowerItemName.Contains("light"))
                                {
                                    TutorialManager.Instance.OnLightPickedUp(item.GetComponent<Equipment.FilmLightItem>());
                                }
                                else if (lowerObjName.Contains("camera") || lowerItemName.Contains("camera"))
                                {
                                    TutorialManager.Instance.OnCameraPickedUp(item.EquipmentName);
                                }
                                else if (lowerObjName.Contains("sd") || lowerItemName.Contains("card"))
                                {
                                    // --- THE FIX: Check if it is the USED SD Card! ---
                                    Equipment.SDCardItem sdCard = item.GetComponent<Equipment.SDCardItem>();
                                    if (sdCard != null && sdCard.isUsedCard)
                                    {
                                        TutorialManager.Instance.OnUsedSDCardPickedUp();
                                    }
                                    else
                                    {
                                        TutorialManager.Instance.OnSDCardPickedUp();
                                    }
                                }
                            }

                            return;
                        }
                    }
                    Debug.LogWarning("Inventory is full!");
                    return;
                }

                IInteractable interactableItem = hit.collider.GetComponentInParent<IInteractable>();
                if (interactableItem != null) { interactableItem.OnInteract(gameObject); return; }

                DirectorTerminal terminal = hit.collider.GetComponentInParent<DirectorTerminal>();
                if (terminal != null)
                {
                    activeTerminal = terminal;
                    activeTerminal.OpenTerminal(PlayerCamera.gameObject, GetComponentInParent<Player.PlayerController.PlayerController>());
                    return;
                }

                ShopTerminal shop = hit.collider.GetComponentInParent<ShopTerminal>();
                if (shop != null)
                {
                    activeShop = shop;
                    activeShop.OpenTerminal(PlayerCamera.gameObject, GetComponentInParent<Player.PlayerController.PlayerController>());
                }
            }
        }

        private void DropEquipment()
        {
            if (currentEquipment == null) return;

            Equipment.FilmLightItem droppedLight = currentEquipment.GetComponent<Equipment.FilmLightItem>();

            currentEquipment.OnDropped(PlayerCamera);

            if (TutorialManager.Instance != null && droppedLight != null)
            {
                TutorialManager.Instance.OnLightDropped(droppedLight);
            }

            hotbar[currentSlotIndex] = null;
            currentEquipment = null;

            if (hotbarUI != null) hotbarUI.UpdateSlot(currentSlotIndex, "", null);
        }

        public Equipment.Equipment GetHeldItem() { return currentEquipment; }

        public void DestroyHeldItem()
        {
            if (currentEquipment != null)
            {
                Equipment.Equipment itemToDestroy = currentEquipment;
                hotbar[currentSlotIndex] = null;
                currentEquipment = null;
                Destroy(itemToDestroy.gameObject);

                if (hotbarUI != null) hotbarUI.UpdateSlot(currentSlotIndex, "", null);
            }
        }

        public bool StoreEjectedCard(Equipment.SDCardItem card)
        {
            if (card == null || HoldPoint == null) return false;
            for (int i = 0; i < hotbar.Length; i++) if (hotbar[i] == card) return true;
            int slot = System.Array.FindIndex(hotbar, item => item == null);
            if (slot < 0)
            {
                // Keep the recording safe: make room by placing an inactive item beside the player.
                for (int i = hotbar.Length - 1; i >= 0; i--)
                {
                    if (i == currentSlotIndex || hotbar[i] == null || PlayerCamera == null) continue;
                    var displaced = hotbar[i];
                    displaced.gameObject.SetActive(true);
                    displaced.OnDropped(PlayerCamera);
                    displaced.transform.position = transform.position + transform.right * .7f + Vector3.up * .2f;
                    hotbar[i] = null;
                    slot = i;
                    GameFeedback.Show("INVENTORY FULL\nPlaced " + displaced.EquipmentName + " nearby to store your recording.");
                    break;
                }
            }
            if (slot < 0) return false;
            hotbar[slot] = card;
            card.OnPickedUp(HoldPoint);
            card.gameObject.SetActive(slot == currentSlotIndex);
            if (slot == currentSlotIndex) currentEquipment = card;
            if (hotbarUI != null) hotbarUI.UpdateSlot(slot, card.EquipmentName, card.EquipmentIcon);
            GameFeedback.Show("SD CARD STORED\nRecording added to slot " + (slot + 1));
            return true;
        }

        public bool HasBlankSDCard()
        {
            for (int i = 0; i < 5; i++)
            {
                if (hotbar[i] != null)
                {
                    Equipment.SDCardItem card = hotbar[i].GetComponent<Equipment.SDCardItem>();
                    if (card != null && !card.isUsedCard) return true;
                }
            }
            return false;
        }

        public void ConsumeBlankSDCard()
        {
            for (int i = 0; i < 5; i++)
            {
                if (hotbar[i] != null)
                {
                    Equipment.SDCardItem card = hotbar[i].GetComponent<Equipment.SDCardItem>();
                    if (card != null && !card.isUsedCard)
                    {
                        Equipment.Equipment itemToDestroy = hotbar[i];
                        hotbar[i] = null;
                        if (currentEquipment == itemToDestroy)
                        {
                            currentEquipment = null;
                        }
                        Destroy(itemToDestroy.gameObject);

                        if (hotbarUI != null) hotbarUI.UpdateSlot(i, "", null);
                        return;
                    }
                }
            }
        }

        public void DropAllEquipment()
        {
            for (int i = 0; i < 5; i++)
            {
                if (hotbar[i] != null)
                {
                    hotbar[i].OnDropped(PlayerCamera);
                    hotbar[i].gameObject.SetActive(true);
                    hotbar[i] = null;

                    if (hotbarUI != null) hotbarUI.UpdateSlot(i, "", null);
                }
            }
            currentEquipment = null;
            currentSlotIndex = 0;
            if (hotbarUI != null) hotbarUI.HighlightSlot(0);
        }
    }
}
