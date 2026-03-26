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
            if (activeTerminal != null)
            {
                if (inputManager.Interact) { activeTerminal.CloseTerminal(); activeTerminal = null; }
                return;
            }

            if (activeShop != null)
            {
                if (inputManager.Interact) { activeShop.CloseShop(); activeShop = null; }
                return;
            }

            if (activeComputer != null)
            {
                if (inputManager.Interact) { activeComputer.CloseComputerUI(); }
                return;
            }

            HandleHotbarInput();

            if (inputManager.Interact) { TryPickupOrInteract(); return; }
            if (inputManager.Drop && currentEquipment != null) { DropEquipment(); return; }

            if (inputManager.Equip)
            {
                if (currentEquipment != null) currentEquipment.OnUse(PlayerCamera);
                else TryOpenComputer();
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
            if (UnityEngine.InputSystem.Keyboard.current == null) return;

            if (UnityEngine.InputSystem.Keyboard.current.digit1Key.wasPressedThisFrame) SwitchSlot(0);
            else if (UnityEngine.InputSystem.Keyboard.current.digit2Key.wasPressedThisFrame) SwitchSlot(1);
            else if (UnityEngine.InputSystem.Keyboard.current.digit3Key.wasPressedThisFrame) SwitchSlot(2);
            else if (UnityEngine.InputSystem.Keyboard.current.digit4Key.wasPressedThisFrame) SwitchSlot(3);
            else if (UnityEngine.InputSystem.Keyboard.current.digit5Key.wasPressedThisFrame) SwitchSlot(4);
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
                Equipment.Equipment item = hit.collider.GetComponentInParent<Equipment.Equipment>();
                if (item != null)
                {
                    for (int i = 0; i < 5; i++)
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
                                currentEquipment.gameObject.SetActive(true);
                            }

                            // --- CHANGED: Passing the Name AND the Icon! ---
                            if (hotbarUI != null) hotbarUI.UpdateSlot(i, item.EquipmentName, item.EquipmentIcon);
                            Debug.Log($"Picked up {item.gameObject.name} into Slot {i + 1}");
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

            // --- CHANGED: Passing null to clear the box! ---
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

                // --- CHANGED: Passing null to clear the box! ---
                if (hotbarUI != null) hotbarUI.UpdateSlot(currentSlotIndex, "", null);
            }
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
                        if (currentEquipment == itemToDestroy) currentEquipment = null;
                        Destroy(itemToDestroy.gameObject);

                        // --- CHANGED: Passing null to clear the box! ---
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
                    hotbar[i] = null;

                    // --- CHANGED: Passing null to clear the box! ---
                    if (hotbarUI != null) hotbarUI.UpdateSlot(i, "", null);
                }
            }
            currentEquipment = null;
            currentSlotIndex = 0;
            if (hotbarUI != null) hotbarUI.HighlightSlot(0);
        }
    }
}