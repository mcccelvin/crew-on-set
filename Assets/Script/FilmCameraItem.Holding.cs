using UnityEngine;

namespace Player.Equipment
{
    public partial class FilmCameraItem
    {
        [Header("Handheld Camera Pose (view space, metres)")]
        public Vector3 heldCameraOffset = new Vector3(.20f, -.22f, .30f);
        public Vector3 raisedCameraOffset = new Vector3(.16f, -.16f, .30f);
        [Header("Camera grip positions (model local space)")]
        public Vector3 rightGripLocalPosition = new Vector3(-.01f, .19f, .55f);
        public Vector3 leftGripLocalPosition = new Vector3(-.22f, .055f, .40f);
        public Vector3 rightGripAdjustment;
        public Vector3 leftGripAdjustment;
        public Vector3 RightHandGrip { get; private set; }
        public Vector3 LeftHandGrip { get; private set; }
        private Quaternion modelLensRotation;
        private bool holdingPoseReady;
        private float holdingRaise;

        public void UpdateHandheldPose(Transform view)
        {
            if (view == null || filmCamera == null) return;
            if (!holdingPoseReady)
            {
                modelLensRotation = Quaternion.Inverse(transform.rotation) * filmCamera.transform.rotation;
                holdingPoseReady = true;
            }
            Quaternion aim = view.rotation;
            transform.rotation = aim * Quaternion.Inverse(modelLensRotation);
            // Anchor the handle rather than the centre of the mesh bounds.
            Vector3 wrist = view.position + aim * Vector3.Lerp(heldCameraOffset, raisedCameraOffset, holdingRaise);
            transform.position += wrist - transform.TransformPoint(rightGripLocalPosition);
            RightHandGrip = transform.TransformPoint(rightGripLocalPosition) + aim * rightGripAdjustment;
            LeftHandGrip = transform.TransformPoint(leftGripLocalPosition) + aim * leftGripAdjustment;
        }
    }
}
