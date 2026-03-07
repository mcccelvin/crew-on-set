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
        private Equipment.Equipment currentEquipment;

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
            if (inputManager.Interact && currentEquipment == null)
            {
                TryPickup();
                return; // Stop here so 'E' doesn't immediately tilt the light!
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

            // NEW: Send the inputs to the held equipment so it can react to Q and E
            if (currentEquipment != null)
            {
                currentEquipment.OnHeldUpdate(inputManager);
            }
        }

        private void TryPickup()
        {
            Ray ray = new Ray(PlayerCamera.transform.position, PlayerCamera.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, PickupRange))
            {
                Equipment.Equipment item = hit.collider.GetComponentInParent<Equipment.Equipment>();

                if (item != null)
                {
                    currentEquipment = item;
                    currentEquipment.OnPickedUp(HoldPoint);
                }
            }
        }

        private void DropEquipment()
        {
            if (currentEquipment == null) return;

            currentEquipment.OnDropped(PlayerCamera);
            currentEquipment = null;
        }
    }
}