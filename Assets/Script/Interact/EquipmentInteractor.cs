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

        private InputManager inputManager;

        // --- NEW: HOTBAR SYSTEM ---
        private Equipment.Equipment[] hotbar = new Equipment.Equipment[9]; // Memory for 9 items
        private int currentSlotIndex = 0; // Which slot number we are currently using
        private Equipment.Equipment currentEquipment; // The physical item in our hands right now

        private DirectorTerminal activeTerminal;

        private void Start()
        {
            inputManager = GetComponent<InputManager>();

            if (PlayerCamera != null)
            {
                PlayerCamera.gameObject.SetActive(true);
                PlayerCamera.enabled = true;
            }
        }

        private void Update()
        {
            if (activeTerminal != null)
            {
                if (inputManager.Interact)
                {
                    activeTerminal.CloseTerminal();
                    activeTerminal = null;
                }
                return;
            }

            // NEW: Listen for the 1 through 9 keys every frame
            HandleHotbarInput();

            if (inputManager.Interact)
            {
                TryPickupOrInteract();
                return;
            }

            if (inputManager.Drop && currentEquipment != null)
            {
                DropEquipment();
                return;
            }

            if (inputManager.Equip && currentEquipment != null)
            {
                currentEquipment.OnUse(PlayerCamera);
            }

            if (currentEquipment != null)
            {
                currentEquipment.OnHeldUpdate(inputManager);
            }
        }

        // NEW: Checks if the player presses numbers 1-9
        private void HandleHotbarInput()
        {
            for (int i = 0; i < 9; i++)
            {
                // In Unity, KeyCode.Alpha1 is 49, Alpha2 is 50, etc. 
                // So adding 'i' lets us check all 9 keys easily!
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SwitchSlot(i);
                    break;
                }
            }
        }

        // NEW: Puts the old item in your backpack and brings out the new one
        private void SwitchSlot(int newSlotIndex)
        {
            // Don't do anything if they press the button for the slot they are already holding
            if (currentSlotIndex == newSlotIndex) return;

            // 1. Hide the item we are currently holding
            if (currentEquipment != null)
            {
                currentEquipment.gameObject.SetActive(false);
            }

            // 2. Switch our memory to the new slot
            currentSlotIndex = newSlotIndex;
            currentEquipment = hotbar[currentSlotIndex];

            // 3. Show the new item, if we actually have one in this slot!
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
                // 1. Try picking up equipment first
                Equipment.Equipment item = hit.collider.GetComponentInParent<Equipment.Equipment>();
                if (item != null)
                {
                    // NEW: Search our hotbar for the first empty slot to put this item in
                    for (int i = 0; i < 9; i++)
                    {
                        if (hotbar[i] == null)
                        {
                            hotbar[i] = item; // Add it to the list
                            item.OnPickedUp(HoldPoint); // Physically attach it to the player

                            // If this isn't our currently active slot, hide it immediately
                            if (i != currentSlotIndex)
                            {
                                item.gameObject.SetActive(false);
                            }
                            else
                            {
                                currentEquipment = item; // Put it directly in our hands!
                            }

                            Debug.Log($"Picked up {item.gameObject.name} into Slot {i + 1}");
                            return;
                        }
                    }

                    Debug.LogWarning("Inventory is full! Cannot pick up more equipment.");
                    return;
                }

                // 2. Check for ANY object that uses the IInteractable interface (like your SD Card)
                IInteractable interactableItem = hit.collider.GetComponentInParent<IInteractable>();
                if (interactableItem != null)
                {
                    interactableItem.OnInteract(gameObject);
                    return;
                }

                // 3. Try checking the director terminal
                DirectorTerminal terminal = hit.collider.GetComponentInParent<DirectorTerminal>();
                if (terminal != null)
                {
                    activeTerminal = terminal;
                    activeTerminal.OpenTerminal(PlayerCamera.gameObject, GetComponentInParent<Player.PlayerController.PlayerController>());
                }
            }
        }

        private void DropEquipment()
        {
            if (currentEquipment == null) return;

            currentEquipment.OnDropped(PlayerCamera);
            hotbar[currentSlotIndex] = null; // NEW: Erase it from our hotbar memory so the slot is empty again
            currentEquipment = null;
        }

        // --- SD CARD HELPER METHODS ---

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
                        Equipment.Equipment itemToDestroy = hotbar[i]; // Remember what we are destroying

                        // 1. Erase it from the Hotbar memory so the number key stops working!
                        hotbar[i] = null;

                        // 2. Just in case they are somehow holding it, empty their hands
                        if (currentEquipment == itemToDestroy)
                        {
                            currentEquipment = null;
                        }

                        // 3. Nuke the actual physical object from the game entirely
                        Destroy(itemToDestroy.gameObject);

                        Debug.Log($"Hotbar: Consumed SD Card from Slot {i + 1}");
                        return;
                    }
                }
            }
        }
    }
}