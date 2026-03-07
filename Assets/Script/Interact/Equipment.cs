using UnityEngine;
using Player.Manager; // Added this!

namespace Player.Equipment
{
    [RequireComponent(typeof(Rigidbody))]
    public abstract class Equipment : MonoBehaviour
    {
        [Header("Equipment Info")]
        public string EquipmentName = "New Equipment";

        [Header("Transform Adjustments")]
        public Vector3 HoldPositionOffset = Vector3.zero;
        public Vector3 HoldRotationOffset = Vector3.zero;

        protected Rigidbody itemRigidbody;
        protected Collider[] itemColliders;

        protected virtual void Awake()
        {
            itemRigidbody = GetComponent<Rigidbody>();
            itemColliders = GetComponentsInChildren<Collider>();
        }

        public virtual void OnPickedUp(Transform holdPoint)
        {
            if (itemRigidbody != null) itemRigidbody.isKinematic = true;

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
            if (itemRigidbody != null) itemRigidbody.isKinematic = false;

            foreach (Collider col in itemColliders)
            {
                if (col != null) col.enabled = true;
            }

            transform.SetParent(null);
        }

        public abstract void OnUse(Camera playerCamera);

        // NEW: Allows specific items to read inputs every frame while held!
        public virtual void OnHeldUpdate(InputManager input) { }
    }
}