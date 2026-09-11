using UnityEngine;
using Player.Manager;

namespace Player.Equipment
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract class Equipment : MonoBehaviour
    {
        [Header("Equipment Info")]
        public string EquipmentName = "New Equipment";
        public Sprite EquipmentIcon;

        // --- NEW: The instructions for this specific item! ---
        [TextArea(2, 5)]
        public string EquipmentControls = "[LMB] Use  |  [G] Drop";

        [Header("Transform Adjustments")]
        public Vector3 HoldPositionOffset = Vector3.zero;
        public Vector3 HoldRotationOffset = Vector3.zero;

        protected Rigidbody itemRigidbody;
        protected Collider[] itemColliders;
        protected Rigidbody[] allRigidbodies;
        private bool[] colliderStates;
        private bool[] kinematicStates;
        private bool[] gravityStates;
        private bool[] collisionStates;
        private Vector3 worldScaleBeforePickup;
        private bool hasPickupState;

        protected virtual void Awake()
        {
            itemRigidbody = GetComponent<Rigidbody>();
            itemColliders = GetComponentsInChildren<Collider>(true);
        }

        public virtual void OnPickedUp(Transform holdPoint)
        {
            if (holdPoint == null || hasPickupState) return;
            worldScaleBeforePickup = transform.lossyScale;
            hasPickupState = true;
            itemColliders = GetComponentsInChildren<Collider>(true);
            allRigidbodies = GetComponentsInChildren<Rigidbody>(true);
            colliderStates = new bool[itemColliders.Length];
            kinematicStates = new bool[allRigidbodies.Length];
            gravityStates = new bool[allRigidbodies.Length];
            collisionStates = new bool[allRigidbodies.Length];

            for (int i = 0; i < allRigidbodies.Length; i++)
            {
                Rigidbody rb = allRigidbodies[i];
                if (rb != null)
                {
                    kinematicStates[i] = rb.isKinematic;
                    gravityStates[i] = rb.useGravity;
                    collisionStates[i] = rb.detectCollisions;
                    if (!rb.isKinematic)
                    {
                        rb.velocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.detectCollisions = false;
                }
            }

            for (int i = 0; i < itemColliders.Length; i++)
            {
                Collider col = itemColliders[i];
                if (col == null) continue;
                colliderStates[i] = col.enabled;
                col.enabled = false;
            }

            transform.SetParent(holdPoint);
            transform.localPosition = HoldPositionOffset;
            transform.localEulerAngles = HoldRotationOffset;
        }

        public virtual void OnDropped(Camera playerCamera)
        {
            transform.SetParent(null, true);
            if (!hasPickupState) return;
            transform.localScale = worldScaleBeforePickup;
            hasPickupState = false;
            if (allRigidbodies != null)
            {
                for (int i = 0; i < allRigidbodies.Length; i++)
                {
                    Rigidbody rb = allRigidbodies[i];
                    if (rb != null)
                    {
                        rb.isKinematic = kinematicStates[i];
                        rb.useGravity = gravityStates[i];
                        rb.detectCollisions = collisionStates[i];
                    }
                }
            }

            for (int i = 0; i < itemColliders.Length; i++)
            {
                if (itemColliders[i] != null) itemColliders[i].enabled = colliderStates[i];
            }
        }

        public abstract void OnUse(Camera playerCamera);
        public virtual void OnHeldUpdate(InputManager input) { }
    }
}
