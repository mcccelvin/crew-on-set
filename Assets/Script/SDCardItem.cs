using UnityEngine;

namespace Player.Equipment
{
    public class SDCardItem : Equipment
    {
        public bool isUsedCard = false;
        public string recordedFileName = "";
        public float videoDuration = 0f;

        public float videoScore = 0f; // The Total (out of 100)
        public float cameraScore = 0f; // NEW: Camera only (out of 70)
        public float lightScore = 0f;  // NEW: Light only (out of 30)

        [Header("Production Evidence")]
        public int campaignLevel = 1;
        public int shotType = 2;
        public float screenDirection = 0f;
        public string actorPose = "";
        public bool requiredSubjectsVisible = false;
        public bool usedSoftLight = false;
        public bool hasThreePointRoles = false;

        [Header("Icons")]
        [Tooltip("Drag the icon for a RECORDED SD card here")]
        public Sprite usedCardIcon;

        private bool awaitingShopPickup;

        public void PrepareShopDelivery(Vector3 position)
        {
            transform.position = position;
            // Imported mesh pivots are offset; place the visible card, not its root pivot.
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                transform.position += new Vector3(position.x - bounds.center.x,
                    position.y - bounds.min.y, position.z - bounds.center.z);
            }
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            itemRigidbody.useGravity = false;
            itemRigidbody.isKinematic = true;
            awaitingShopPickup = true;
        }

        public override void OnPickedUp(Transform holdPoint)
        {
            if (holdPoint == null) return;
            if (awaitingShopPickup)
            {
                // Let Equipment capture normal physics so dropping the card still works.
                itemRigidbody.isKinematic = false;
                itemRigidbody.useGravity = true;
                itemRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                awaitingShopPickup = false;
            }
            base.OnPickedUp(holdPoint);
        }

        public override void OnUse(Camera playerCamera)
        {
            if (isUsedCard)
            {
                // Tells you the split score when you click to inspect it!
                Debug.Log($"Card: {recordedFileName} | Cam: {cameraScore:F0}/70 | Light: {lightScore:F0}/30 | Total: {videoScore:F0}/100");
            }
            else
            {
                Debug.Log("Inspecting SD Card. It is BLANK.");
            }
        }

        // --- NEW: The Camera calls this when ejecting the tape! ---
        public void MarkAsUsed()
        {
            isUsedCard = true;

            if (usedCardIcon != null)
            {
                EquipmentIcon = usedCardIcon; // Swaps the base icon so the hotbar reads the new one!
            }
        }
    }
}
