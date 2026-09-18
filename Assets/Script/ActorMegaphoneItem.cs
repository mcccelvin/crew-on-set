using UnityEngine;
using UnityEngine.InputSystem;
using Player.Manager;

namespace Player.Equipment
{
    // Handheld actor cue tool. It targets the actor under the crosshair and
    // forwards the same commands available from the Director Tablet.
    public sealed class ActorMegaphoneItem : Equipment
    {
        private ActorBot selectedActor;
        private Camera playerCamera;
        private int commandCount;

        public bool HasSelectedActor => selectedActor != null;
        public int CommandCount => commandCount;
        public ActorBot SelectedActor => selectedActor;

        public static ActorMegaphoneItem ConfigureSpawnedItem(GameObject item)
        {
            if (item == null) return null;
            var megaphone = item.GetComponent<ActorMegaphoneItem>();
            if (megaphone == null) megaphone = item.AddComponent<ActorMegaphoneItem>();

            megaphone.EquipmentName = "DIRECTOR MEGAPHONE";
            megaphone.EquipmentControls = "[LMB] Select Actor | [Z] Neutral [X] Wave [C] Action\n[ARROWS] Move | [R] Turn | [B/N] Marks | [K] Rehearse | [J] Reset | [H] Clear | [G] Drop";
            megaphone.HoldPositionOffset = new Vector3(.45f, -.35f, 1.0f);
            megaphone.HoldRotationOffset = new Vector3(-90f, 0f, 0f);

            if (item.GetComponent<Rigidbody>() == null) item.AddComponent<Rigidbody>();
            var body = item.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            if (item.GetComponentInChildren<Collider>() == null)
            {
                var box = item.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, .15f, 0f);
                box.size = new Vector3(1.1f, .8f, 1.6f);
            }
            foreach (var mesh in item.GetComponentsInChildren<MeshCollider>(true)) mesh.convex = true;
            return megaphone;
        }

        protected override void Awake()
        {
            base.Awake();
            if (string.IsNullOrEmpty(EquipmentName) || EquipmentName == "New Equipment")
                ConfigureSpawnedItem(gameObject);
        }

        public void OnPickedUp(Transform holdPoint, Camera camera)
        {
            playerCamera = camera;
            OnPickedUp(holdPoint);
        }

        public override void OnPickedUp(Transform holdPoint)
        {
            if (holdPoint == null) return;
            base.OnPickedUp(holdPoint);
            selectedActor = null;
            commandCount = 0;
            if (playerCamera == null)
            {
                foreach (var candidate in holdPoint.root.GetComponentsInChildren<Camera>(true))
                {
                    if (!candidate.enabled || candidate.targetTexture != null) continue;
                    playerCamera = candidate;
                    break;
                }
            }
            if (playerCamera == null) playerCamera = Camera.main;
            // Follow camera movement and look rotation, including while switching slots.
            if (playerCamera != null) transform.SetParent(playerCamera.transform, true);
            FitHeldModelInView();
            EquipmentControls = "[LMB] Select Actor | [Z] Neutral [X] Wave [C] Action\n[ARROWS] Move | [R] Turn | [B/N] Marks | [K] Rehearse | [J] Reset | [H] Clear | [G] Drop";
            GameFeedback.Show("MEGAPHONE READY\nAim at an Actor and press [LMB] to select them.");
        }

        private void FitHeldModelInView()
        {
            if (playerCamera == null) return;
            var visuals = GetComponentsInChildren<Renderer>();
            if (visuals.Length == 0) return;

            // The imported mesh pivot is offset from the horn. Position its
            // visible bounds, not that pivot, inside the first-person view.
            transform.rotation = playerCamera.transform.rotation * Quaternion.Euler(HoldRotationOffset);
            Bounds bounds = visuals[0].bounds;
            foreach (var visual in visuals) bounds.Encapsulate(visual.bounds);
            float longest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (longest > .001f) transform.localScale *= .42f / longest;

            bounds = visuals[0].bounds;
            foreach (var visual in visuals) bounds.Encapsulate(visual.bounds);
            float depth = Mathf.Max(.85f, playerCamera.nearClipPlane + .4f);
            Vector3 center = playerCamera.ViewportToWorldPoint(new Vector3(.72f, .25f, depth));
            transform.position += center - bounds.center;
        }

        public override void OnUse(Camera camera)
        {
            playerCamera = camera != null ? camera : playerCamera != null ? playerCamera : Camera.main;
            if (playerCamera == null) return;

            ActorBot target = null;
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<ActorBot>();

            if (target == null)
            {
                float nearestDistance = 4f;
                foreach (var candidate in Object.FindObjectsOfType<ActorBot>())
                {
                    float distance = Vector3.Distance(playerCamera.transform.position, candidate.transform.position);
                    if (distance < nearestDistance) { nearestDistance = distance; target = candidate; }
                }
            }

            if (target == null)
            {
                GameFeedback.Show("AIM AT AN ACTOR\nMove closer or point the megaphone at the actor.");
                return;
            }

            selectedActor = target;
            GameFeedback.Show("ACTOR SELECTED\nUse [Z] Neutral, [X] Wave, [C] Action, or the movement keys.");
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (selectedActor == null || Keyboard.current == null) return;
            var keys = Keyboard.current;

            if (keys.zKey.wasPressedThisFrame) CuePose(0, "NEUTRAL");
            if (keys.xKey.wasPressedThisFrame) CuePose(1, "WAVE");
            if (keys.cKey.wasPressedThisFrame) CuePose(2, "ACTION");
            if (keys.rKey.wasPressedThisFrame) { selectedActor.transform.Rotate(0f, 15f, 0f, Space.World); commandCount++; }
            if (keys.bKey.wasPressedThisFrame) { selectedActor.SetStartMark(); commandCount++; GameFeedback.Show("START MARK SAVED"); }
            if (keys.nKey.wasPressedThisFrame) { selectedActor.SetEndMark(); commandCount++; }
            if (keys.kKey.wasPressedThisFrame) { selectedActor.RehearseWalk(); commandCount++; }
            if (keys.jKey.wasPressedThisFrame) { selectedActor.ReturnToStartMark(); commandCount++; }
            if (keys.hKey.wasPressedThisFrame) { selectedActor.ClearWalk(); commandCount++; }

            Vector3 nudge = Vector3.zero;
            if (keys.upArrowKey.wasPressedThisFrame) nudge += Vector3.forward;
            if (keys.downArrowKey.wasPressedThisFrame) nudge += Vector3.back;
            if (keys.leftArrowKey.wasPressedThisFrame) nudge += Vector3.left;
            if (keys.rightArrowKey.wasPressedThisFrame) nudge += Vector3.right;
            if (nudge != Vector3.zero) { selectedActor.MoveBy(nudge * .25f); commandCount++; }
        }

        private void CuePose(int pose, string label)
        {
            var cube = selectedActor != null ? selectedActor.GetComponent<CubeActor>() : null;
            if (cube == null) return;
            cube.SetPose(pose);
            commandCount++;
            GameFeedback.Show("ACTOR CUE: " + label);
        }
    }
}
