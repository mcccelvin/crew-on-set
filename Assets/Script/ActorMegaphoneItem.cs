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
        private ActorBot previewActor;
        private Vector3 previewPosition;
        private bool previewValid;
        public bool IsRepositioning { get; private set; }
        private const string Controls = "[LMB] Select Actor / Aim at chair, machine or product | [O] Stop / Return product\n[Z/X/C] Neutral/Wave/Action | [ARROWS] Move | [R] Turn | [B/N] Marks | [K] Walk [J] Reset [H] Clear | [G] Drop";

        public bool HasSelectedActor => selectedActor != null;
        public int CommandCount => commandCount;
        public ActorBot SelectedActor => selectedActor;

        public static ActorMegaphoneItem ConfigureSpawnedItem(GameObject item)
        {
            if (item == null) return null;
            var megaphone = item.GetComponent<ActorMegaphoneItem>();
            if (megaphone == null) megaphone = item.AddComponent<ActorMegaphoneItem>();

            megaphone.EquipmentName = "DIRECTOR MEGAPHONE";
            megaphone.EquipmentControls = Controls + "\n[T] Reposition: aim at floor, [LMB] place";
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
            CancelReposition();
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
            EquipmentControls = Controls;
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
            if (IsRepositioning)
            {
                var lesson = CampaignLevelManager.Instance;
                if (lesson != null && !lesson.CanUseContract4PracticeAction("megaphone.place")) return;
                UpdatePositionPreview(camera != null ? camera : playerCamera);
                if (previewValid && selectedActor != null)
                {
                    selectedActor.MoveBy(Vector3.zero);
                    selectedActor.transform.position = previewPosition;
                    CancelReposition();
                    commandCount++;
                    GameFeedback.Show("ACTOR REPOSITIONED");
                    return;
                }
                GameFeedback.Show("Aim at a clear floor spot near the actor's current floor height.");
                return;
            }
            var practice = CampaignLevelManager.Instance;
            if (practice != null && practice.IsContract4PracticeActive &&
                !practice.CanUseContract4PracticeAction("megaphone.select") &&
                !practice.CanUseContract4PracticeAction("megaphone.seat")) return;
            playerCamera = camera != null ? camera : playerCamera != null ? playerCamera : Camera.main;
            if (playerCamera == null) return;

            ActorBot target = null;
            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<ActorBot>();

            if (target == null && selectedActor != null)
            {
                var furniture = Contract4Interactable.FindCommandTarget(ray, 10f, transform.root);
                if (furniture != null)
                {
                    if (practice != null && practice.IsContract4PracticeActive &&
                        (furniture.action != Contract4Interactable.Action.Sit ||
                         !practice.CanUseContract4PracticeAction("megaphone.seat"))) return;
                    if (selectedActor.PerformFurnitureAction(furniture)) commandCount++;
                    return;
                }
            }

            if (target == null)
            {
                GameFeedback.Show(selectedActor == null ? "AIM AT AN ACTOR\nClick their body to select them." : "Aim at a chair, coffee machine or product, then click to command the selected actor.");
                return;
            }

            if (practice != null && practice.IsContract4PracticeActive &&
                !practice.CanUseContract4PracticeAction("megaphone.select")) return;

            selectedActor = target;
            GameFeedback.Show("ACTOR SELECTED\nAim at a chair, machine or product and click. [Z/X/C] Poses | [O] Stop / Return product.");
        }

        public string GetAimPrompt(Camera camera)
        {
            if (IsRepositioning) return "Aim at clear floor: [LMB] place actor | [T] cancel";
            if (camera == null) return Controls;
            var ray = new Ray(camera.transform.position, camera.transform.forward);
            if (Physics.Raycast(ray, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore) && hit.collider.GetComponentInParent<ActorBot>() != null)
                return "[LMB] Select this actor";
            var target = Contract4Interactable.FindCommandTarget(ray, 10f, transform.root);
            if (target != null)
                return selectedActor == null ? "Select an actor first: aim at them and click [LMB]" :
                    "[LMB] " + (target.action == Contract4Interactable.Action.Product ? "Selected actor: hold this product" : target.action == Contract4Interactable.Action.Sit ? "Seat selected actor here" : "Selected actor: use this machine") + " | [O] Stop / Return product";
            return Controls + "\n[T] Reposition selected actor";
        }

        public override void OnHeldUpdate(InputManager input)
        {
            if (selectedActor == null) { CancelReposition(); return; }
            if (Keyboard.current == null) return;
            var practice = CampaignLevelManager.Instance;
            bool gated = practice != null && practice.IsContract4PracticeActive;
            var keys = Keyboard.current;
            if (keys.tKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.reposition") || practice.CanUseContract4PracticeAction("megaphone.place")))
            {
                if (IsRepositioning) CancelReposition();
                else { IsRepositioning = true; previewActor = selectedActor; }
                GameFeedback.Show(IsRepositioning ? "REPOSITION ACTOR\nAim to move the transparent preview. [LMB] place, [T] cancel." : "Reposition cancelled.");
            }
            if (IsRepositioning) { UpdatePositionPreview(playerCamera); return; }
            if (keys.oKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.stop"))) { selectedActor.ReleaseProduct(); selectedActor.StopFurnitureAction(); commandCount++; }

            if (keys.zKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.pose.z"))) CuePose(0, "NEUTRAL");
            if (keys.xKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.pose.x"))) CuePose(1, "WAVE");
            if (keys.cKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.pose.c"))) CuePose(2, "ACTION");
            if (keys.rKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.turn"))) { selectedActor.transform.Rotate(0f, 15f, 0f, Space.World); commandCount++; }
            if (keys.bKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.mark.start"))) { selectedActor.SetStartMark(); commandCount++; GameFeedback.Show("START MARK SAVED"); }
            if (keys.nKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.mark.end"))) { selectedActor.SetEndMark(); commandCount++; }
            if (keys.kKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.walk.rehearse"))) { selectedActor.RehearseWalk(); commandCount++; }
            if (keys.jKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.walk.return"))) { selectedActor.ReturnToStartMark(); commandCount++; }
            if (keys.hKey.wasPressedThisFrame && (!gated || practice.CanUseContract4PracticeAction("megaphone.walk.clear"))) { selectedActor.ClearWalk(); commandCount++; }

            Vector3 nudge = Vector3.zero;
            if (keys.upArrowKey.wasPressedThisFrame) nudge += Vector3.forward;
            if (keys.downArrowKey.wasPressedThisFrame) nudge += Vector3.back;
            if (keys.leftArrowKey.wasPressedThisFrame) nudge += Vector3.left;
            if (keys.rightArrowKey.wasPressedThisFrame) nudge += Vector3.right;
            if (nudge != Vector3.zero && (!gated || practice.CanUseContract4PracticeAction("megaphone.move"))) { selectedActor.MoveBy(nudge * .25f); commandCount++; }
        }

        private void CuePose(int pose, string label)
        {
            var cube = selectedActor != null ? selectedActor.GetComponent<CubeActor>() : null;
            if (cube == null) return;
            cube.SetPose(pose);
            commandCount++;
            GameFeedback.Show("ACTOR CUE: " + label);
        }

        private void UpdatePositionPreview(Camera view)
        {
            previewValid = false;
            if (view != null && selectedActor != null)
            {
                var hits = Physics.RaycastAll(view.transform.position, view.transform.forward, 20f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (hit.collider.transform.IsChildOf(transform.root) || hit.collider.GetComponentInParent<ActorBot>() != null) continue;
                    if (hit.normal.y < .7f || Mathf.Abs(hit.point.y - selectedActor.transform.position.y) > .5f) break;
                    previewPosition = new Vector3(hit.point.x, selectedActor.transform.position.y, hit.point.z);
                    previewValid = true;
                    previewActor = selectedActor;
                    previewActor.ShowPlacementPreview(previewPosition);
                    break;
                }
            }
            if (!previewValid && previewActor != null) previewActor.HidePlacementPreview();
        }

        private void CancelReposition()
        {
            if (previewActor != null) previewActor.ClearPlacementPreview();
            previewActor = null;
            previewValid = false;
            IsRepositioning = false;
        }

        public override void OnDropped(Camera camera)
        {
            CancelReposition();
            base.OnDropped(camera);
        }

        private void OnDisable() { CancelReposition(); }
        private void OnDestroy() { CancelReposition(); }
    }
}
