using UnityEngine;

namespace Player.Equipment
{
    public class FilmCameraItem : Equipment
    {
        [Header("Film Camera Settings")]
        [SerializeField] private Camera filmCamera;
        [SerializeField] private GameObject filmUICanvas;

        private bool isCameraActive = false;

        protected override void Awake()
        {
            base.Awake();

            if (filmCamera != null) filmCamera.gameObject.SetActive(false);
            if (filmUICanvas != null) filmUICanvas.SetActive(false);
        }

        public override void OnUse(Camera playerCamera)
        {
            isCameraActive = !isCameraActive;

            if (filmCamera != null) filmCamera.gameObject.SetActive(isCameraActive);
            if (filmUICanvas != null) filmUICanvas.SetActive(isCameraActive);
            if (playerCamera != null) playerCamera.gameObject.SetActive(!isCameraActive);

            Debug.Log($"Film Camera Active: {isCameraActive}");
        }

        public override void OnDropped(Camera playerCamera)
        {
            // If the camera is active when we drop it, turn it off and restore the player camera
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