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

        protected virtual void Awake()
        {
            itemRigidbody = GetComponent<Rigidbody>();
            itemColliders = GetComponentsInChildren<Collider>(true);
        }

        public virtual void OnPickedUp(Transform holdPoint)
        {
            itemColliders = GetComponentsInChildren<Collider>(true);
            allRigidbodies = GetComponentsInChildren<Rigidbody>(true);

            foreach (Rigidbody rb in allRigidbodies)
            {
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.detectCollisions = false;
                }
            }

            foreach (Collider col in itemColliders)
            {
                if (col != null) col.enabled = false;
            }

            transform.SetParent(holdPoint);
            transform.localPosition = HoldPositionOffset;
            transform.localEulerAngles = HoldRotationOffset;
        }

        public virtual void OnDropped(Camera playerCamera)
        {
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