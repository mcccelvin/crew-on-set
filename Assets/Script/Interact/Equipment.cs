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

        [Header("Transform Adjustments")]
        public Vector3 HoldPositionOffset = Vector3.zero;
        public Vector3 HoldRotationOffset = Vector3.zero;

        protected Rigidbody itemRigidbody;
        protected Collider[] itemColliders;

        // --- THE FIX: An array to catch rogue rigidbodies! ---
        protected Rigidbody[] allRigidbodies;

        protected virtual void Awake()
        {
            itemRigidbody = GetComponent<Rigidbody>();
            itemColliders = GetComponentsInChildren<Collider>(true);
        }

        public virtual void OnPickedUp(Transform holdPoint)
        {
            // 1. Scan for absolutely EVERY collider and rigidbody (even hidden ones!)
            itemColliders = GetComponentsInChildren<Collider>(true);
            allRigidbodies = GetComponentsInChildren<Rigidbody>(true);

            // 2. THE SILVER BULLET: Turn off all physics interactions completely
            foreach (Rigidbody rb in allRigidbodies)
            {
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.detectCollisions = false; // <--- This completely blinds the physics engine to the object!
                }
            }

            // 3. Force colliders off just to be safe
            foreach (Collider col in itemColliders)
            {
                if (col != null) col.enabled = false;
            }

            // 4. Snap to hand
            transform.SetParent(holdPoint);
            transform.localPosition = HoldPositionOffset;
            transform.localEulerAngles = HoldRotationOffset;
        }

        public virtual void OnDropped(Camera playerCamera)
        {
            // Turn all the physics back on so it hits the floor!
            if (allRigidbodies != null)
            {
                foreach (Rigidbody rb in allRigidbodies)
                {
                    if (rb != null)
                    {
                        rb.isKinematic = false;
                        rb.useGravity = true;
                        rb.detectCollisions = true;
                    }
                }
            }

            foreach (Collider col in itemColliders)
            {
                if (col != null) col.enabled = true;
            }

            transform.SetParent(null);
        }

        public abstract void OnUse(Camera playerCamera);

        public virtual void OnHeldUpdate(InputManager input) { }
    }
}