using UnityEngine;
using UnityEngine.InputSystem; // Needed to read 'C' and 'R' cleanly
using Player.Manager;

namespace Player.Equipment
{
    public class FilmCameraItem : Equipment
    {
        [Header("Film Camera Settings")]
        [SerializeField] private Camera filmCamera;
        [SerializeField] private GameObject filmUICanvas;

        [Header("SD Card System")]
        [Tooltip("Drag your blue SD Card Prefab here!")]
        public GameObject sdCardPrefab;
        public Transform ejectPoint;

        private bool isCameraActive = false;
        private bool isRecording = false;
        private bool isSDCardInserted = false;

        private ReplayManager replayManager;

        protected override void Awake()
        {
            base.Awake();
            replayManager = FindObjectOfType<ReplayManager>();

            if (filmCamera != null) filmCamera.gameObject.SetActive(false);
            if (filmUICanvas != null) filmUICanvas.SetActive(false);
        }

        // Toggles looking through the lens
        public override void OnUse(Camera playerCamera)
        {
            isCameraActive = !isCameraActive;

            if (filmCamera != null) filmCamera.gameObject.SetActive(isCameraActive);
            if (filmUICanvas != null) filmUICanvas.SetActive(isCameraActive);
            if (playerCamera != null) playerCamera.gameObject.SetActive(!isCameraActive);

            if (!isCameraActive && isRecording) ToggleRecording();
        }

        // NEW: This runs every frame while you are holding the camera
        public override void OnHeldUpdate(InputManager input)
        {
            if (!isCameraActive) return;

            // PRESS 'C' - Insert SD Card
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                InsertSDCard();
            }

            // PRESS 'R' - Toggle Recording
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                // THE LOCK: If we aren't recording yet, and there is no SD card, BLOCK IT!
                if (!isRecording && !isSDCardInserted)
                {
                    Debug.LogWarning("Camera Blocked: You cannot record! Press 'C' to insert a blank SD Card first.");
                    return; // The 'return' stops the code right here so it never hits ToggleRecording!
                }

                ToggleRecording();
            }
        }

        private void InsertSDCard()
        {
            if (isSDCardInserted)
            {
                Debug.Log("Camera: An SD Card is already inserted!");
                return;
            }

            Player.Interactor.EquipmentInteractor hotbar = GetComponentInParent<Player.Interactor.EquipmentInteractor>();

            if (hotbar != null && hotbar.HasBlankSDCard())
            {
                hotbar.ConsumeBlankSDCard();
                isSDCardInserted = true;
                Debug.Log("Camera: Blank SD Card taken from hotbar and inserted.");
            }
            else
            {
                Debug.LogWarning("Camera: You have no blank SD cards in your hotbar!");
            }
        }

        private void ToggleRecording()
        {
            isRecording = !isRecording;

            if (replayManager != null) replayManager.SetRecordingState(isRecording);

            // If we just STOPPED recording, spit out the card
            if (!isRecording)
            {
                EjectUsedSDCard();
            }
        }

        private void EjectUsedSDCard()
        {
            isSDCardInserted = false; // Reset the camera so it's empty again

            if (sdCardPrefab != null)
            {
                Transform spawnLoc = ejectPoint != null ? ejectPoint : transform;
                GameObject ejectedCard = Instantiate(sdCardPrefab, spawnLoc.position, spawnLoc.rotation);

                // 1. Mark it as used
                SDCardItem cardScript = ejectedCard.GetComponent<SDCardItem>();
                if (cardScript != null) cardScript.isUsedCard = true;

                // 2. Paint it red
                MeshRenderer renderer = ejectedCard.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.material.color = Color.red;

                // 3. SAFETY NET: Guarantee it has a collider to be picked up
                Collider col = ejectedCard.GetComponent<Collider>();
                if (col == null)
                {
                    col = ejectedCard.AddComponent<BoxCollider>();
                }

                // 4. SAFETY NET: Guarantee it has a Rigidbody to fall to the floor
                Rigidbody rb = ejectedCard.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = ejectedCard.AddComponent<Rigidbody>();
                }

                // Make sure physics are turned on, then pop it out!
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(transform.up * 2f + transform.forward * 1.5f, ForceMode.Impulse);
            }

            Debug.Log($"Camera: Take complete! USED SD Card physically ejected.");
        }

        public override void OnDropped(Camera playerCamera)
        {
            if (isRecording) ToggleRecording();

            if (isCameraActive)
            {
                isCameraActive = false;
                if (filmCamera != null) filmCamera.gameObject.SetActive(false);
                if (filmUICanvas != null) filmUICanvas.SetActive(false);
                if (playerCamera != null) playerCamera.gameObject.SetActive(true);
            }

            base.OnDropped(playerCamera);
        }
    }
}