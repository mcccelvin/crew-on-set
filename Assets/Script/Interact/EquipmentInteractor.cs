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

        [Header("Settings")]
        [SerializeField] private float PickupRange = 3f;

        // --- YOUR MIXED VARIABLES ---
        private InputManager inputManager;
        private Equipment.Equipment[] hotbar = new Equipment.Equipment[9];
        private int currentSlotIndex = 0;
        private Equipment.Equipment currentEquipment;

        private DirectorTerminal activeTerminal;
        private ComputerStation activeComputer;
        private ShopTerminal activeShop; // --- NEW: Tracks if we are looking at the shop screen ---

        private void Start()
        {
            inputManager = GetComponent<InputManager>();
            if (PlayerCamera != null) { PlayerCamera.gameObject.SetActive(true); PlayerCamera.enabled = true; }
        }

        private void Update()
        {
            if (activeTerminal != null)
            {
                if (inputManager.Interact) { activeTerminal.CloseTerminal(); activeTerminal = null; }
                return;
            }

            // --- NEW: If we are in the shop, press 'E' to step away ---
            if (activeShop != null)
            {
                if (inputManager.Interact) { activeShop.CloseShop(); activeShop = null; }
                return;
            }

            if (activeComputer != null)
            {
                if (inputManager.Interact)
                {
                    activeComputer.CloseComputerUI();
                }
                return;
            }

            HandleHotbarInput();

            // PRESS 'E' - Pick up items, insert SD cards, or open terminals
            if (inputManager.Interact) { TryPickupOrInteract(); return; }

            // PRESS 'G' - Drop items
            if (inputManager.Drop && currentEquipment != null) { DropEquipment(); return; }

            // PRESS 'F' - Look through Camera OR Use Computer Screen
            if (inputManager.Equip)
            {
                if (currentEquipment != null)
                {
                    currentEquipment.OnUse(PlayerCamera);
                }
                else
                {
                    TryOpenComputer();
                }
            }

            if (currentEquipment != null) currentEquipment.OnHeldUpdate(inputManager);
        }

        private void TryOpenComputer()
        {
            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, PickupRange))
            {
                ComputerStation computer = hit.collider.GetComponentInParent<ComputerStation>();
                if (computer != null)
                {
                    activeComputer = computer;
                    activeComputer.OpenComputerUI(this);
                }
            }
        }

        public void ClearActiveComputer() { activeComputer = null; }

        private void HandleHotbarInput()
        {
            for (int i = 0; i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SwitchSlot(i);
                    break;
                }
            }
        }

        private void SwitchSlot(int newSlotIndex)
        {
            if (currentSlotIndex == newSlotIndex) return;

            if (currentEquipment != null)
            {
                currentEquipment.gameObject.SetActive(false);
            }

            currentSlotIndex = newSlotIndex;
            currentEquipment = hotbar[currentSlotIndex];

            if (currentEquipment != null)
            {
                currentEquipment.gameObject.SetActive(true);
                Debug.Log($"Hotbar: Switched to Slot {currentSlotIndex + 1} ({currentEquipment.gameObject.name})");
            }
            else
            {
                Debug.Log($"Hotbar: Switched to Slot {currentSlotIndex + 1} (Empty)");
            }
        }

        private void TryPickupOrInteract()
        {
            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, PickupRange))
            {
                // A. Try picking up equipment first
                Equipment.Equipment item = hit.collider.GetComponentInParent<Equipment.Equipment>();
                if (item != null)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        if (hotbar[i] == null)
                        {
                            hotbar[i] = item;
                            item.OnPickedUp(HoldPoint);

                            if (i != currentSlotIndex)
                            {
                                item.gameObject.SetActive(false);
                            }
                            else
                            {
                                currentEquipment = item;
                            }

                            Debug.Log($"Picked up {item.gameObject.name} into Slot {i + 1}");
                            return;
                        }
                    }

                    Debug.LogWarning("Inventory is full! Cannot pick up more equipment.");
                    return;
                }

                // B. Check for ANY object that uses the IInteractable interface
                IInteractable interactableItem = hit.collider.GetComponentInParent<IInteractable>();
                if (interactableItem != null)
                {
                    interactableItem.OnInteract(gameObject);
                    return;
                }

                // C. Try checking the director terminal
                DirectorTerminal terminal = hit.collider.GetComponentInParent<DirectorTerminal>();
                if (terminal != null)
                {
                    activeTerminal = terminal;
                    activeTerminal.OpenTerminal(PlayerCamera.gameObject, GetComponentInParent<Player.PlayerController.PlayerController>());
                    return; // Added return to prevent it from checking the shop if it already found a terminal!
                }

                // D. --- NEW: Try checking the Shop Terminal ---
                ShopTerminal shop = hit.collider.GetComponentInParent<ShopTerminal>();
                if (shop != null)
                {
                    activeShop = shop;
                    activeShop.OpenShop(GetComponentInParent<Player.PlayerController.PlayerController>());
                }
            }
        }

        private void DropEquipment()
        {
            if (currentEquipment == null) return;

            currentEquipment.OnDropped(PlayerCamera);
            hotbar[currentSlotIndex] = null;
            currentEquipment = null;
        }

        public Equipment.Equipment GetHeldItem()
        {
            return currentEquipment;
        }

        public void DestroyHeldItem()
        {
            if (currentEquipment != null)
            {
                Equipment.Equipment itemToDestroy = currentEquipment;
                hotbar[currentSlotIndex] = null;
                currentEquipment = null;
                Destroy(itemToDestroy.gameObject);
            }
        }

        public bool HasBlankSDCard()
        {
            for (int i = 0; i < 9; i++)
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
            for (int i = 0; i < 9; i++)
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
                        Debug.Log($"Hotbar: Consumed SD Card from Slot {i + 1}");
                        return;
                    }
                }
            }
        }


        public void DropAllEquipment()
        {
            for (int i = 0; i < 9; i++)
            {
                if (hotbar[i] != null)
                {
                    // Force the item to drop into the physical world
                    hotbar[i].OnDropped(PlayerCamera);
                    hotbar[i] = null;
                }
            }
            currentEquipment = null;
            currentSlotIndex = 0;
            Debug.Log("Inventory safely dropped!");
        }
    }
}