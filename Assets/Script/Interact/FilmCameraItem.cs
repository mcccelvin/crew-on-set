using UnityEngine;
using UnityEngine.InputSystem;
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

        // THE FIX: We are using the new Pixel Recorder now!
        private TruePixelRecorder pixelRecorder;

        protected override void Awake()
        {
            base.Awake();

            // Find the pixel recorder when the game starts
            pixelRecorder = FindObjectOfType<TruePixelRecorder>();

            if (filmCamera != null) filmCamera.gameObject.SetActive(false);
            if (filmUICanvas != null) filmUICanvas.SetActive(false);
        }

        public override void OnUse(Camera playerCamera)
        {
            isCameraActive = !isCameraActive;

            if (filmCamera != null) filmCamera.gameObject.SetActive(isCameraActive);
            if (filmUICanvas != null) filmUICanvas.SetActive(isCameraActive);
            if (playerCamera != null) playerCamera.gameObject.SetActive(!isCameraActive);

            if (!isCameraActive && isRecording) ToggleRecording();
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (!isCameraActive) return;

            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            {
                InsertSDCard();
            }

            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (!isRecording && !isSDCardInserted)
                {
                    Debug.LogWarning("Camera Blocked: You cannot record! Press 'C' to insert a blank SD Card first.");
                    return;
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
            string generatedFileName = "";

            // Safety check to make sure it finds the recorder
            if (pixelRecorder == null) pixelRecorder = FindObjectOfType<TruePixelRecorder>();

            // THE FIX: Tell the Pixel Recorder to start taking pictures or stop and save!
            if (pixelRecorder != null)
            {
                if (isRecording)
                {
                    pixelRecorder.StartRecording();
                }
                else
                {
                    generatedFileName = pixelRecorder.StopRecording();
                }
            }

            // If we just STOPPED recording, spit out the card and pass the file name to it
            if (!isRecording)
            {
                EjectUsedSDCard(generatedFileName);
            }

            Debug.Log($"Recording is now: {(isRecording ? "ON" : "OFF")}");
        }

        private void EjectUsedSDCard(string savedFileName)
        {
            isSDCardInserted = false;

            if (sdCardPrefab != null)
            {
                Transform spawnLoc = ejectPoint != null ? ejectPoint : transform;
                GameObject ejectedCard = Instantiate(sdCardPrefab, spawnLoc.position, spawnLoc.rotation);

                SDCardItem cardScript = ejectedCard.GetComponent<SDCardItem>();
                if (cardScript != null)
                {
                    cardScript.isUsedCard = true;
                    // The card now officially holds the new .tape file!
                    cardScript.recordedFileName = savedFileName;
                }

                MeshRenderer renderer = ejectedCard.GetComponentInChildren<MeshRenderer>();
                if (renderer != null) renderer.material.color = Color.red;

                Collider col = ejectedCard.GetComponent<Collider>();
                if (col == null) col = ejectedCard.AddComponent<BoxCollider>();

                Rigidbody rb = ejectedCard.GetComponent<Rigidbody>();
                if (rb == null) rb = ejectedCard.AddComponent<Rigidbody>();

                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(transform.up * 2f + transform.forward * 1.5f, ForceMode.Impulse);
            }

            Debug.Log($"Camera: Take complete! Ejected card holding file: {savedFileName}");
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