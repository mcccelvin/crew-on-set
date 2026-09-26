using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Manager;

namespace Player.PlayerController
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement & Camera Settings")]
        [SerializeField] private float AnimBlendSpeed = 8.9f;
        [SerializeField] private Transform CameraRoot;
        [SerializeField] private Transform Camera;
        [Header("Character Appearance")]
        [SerializeField] private GameObject CharacterModel;
        [SerializeField] private Transform CharacterHoldPoint;
        [SerializeField, Min(0.5f)] private float CharacterHeight = ProductModelCatalog.StandardCharacterHeight;
        private Animator characterAnimator;
        private PlayerCameraPose cameraHoldingPose;
        private UnityEngine.Camera heldViewCamera;
        private float heldViewNearClip;

        private void RestoreHeldViewClip()
        {
            if (heldViewCamera == null) return;
            heldViewCamera.nearClipPlane = heldViewNearClip;
            heldViewCamera = null;
        }
        private bool appearanceInitialized;
        [SerializeField] private float UpperLimit = -40f;
        [SerializeField] private float LowerLimit = 70f;
        [SerializeField] private float MouseSensitivity = 21.9f;
        [SerializeField, Min(0.1f)] private float JumpHeight = 1.2f;
        [SerializeField] private float JumpBufferTime = 0.15f;
        [SerializeField] private float CoyoteTime = 0.1f;
        [SerializeField] private float AirResistance = 0.8f;
        [SerializeField] private LayerMask GroundCheck;

        // --- NEW SWITCH: Stops the camera from spinning! ---
        public bool canLook = true;

        // --- THE FIX: NEW SWITCH: Stops the player from walking! ---
        public bool canMove = true;

        private Rigidbody playerRigidbody;
        private InputManager inputManager;
        private Animator animator;
        private bool grounded = false;
        private float nextFootstep;
        private bool audioGroundSampled;
        private bool hasAnimator;
        private int xVelHash, yVelHash, zVelHash, jumpHash, groundHash, fallingHash;
        private float xRotation;
        private float targetYaw;
        private float cameraYaw;
        private float cameraPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private bool lookInitialized;
        private float jumpBufferCounter;
        private float coyoteCounter;
        private Collider bodyCollider;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        public bool StationaryAction { get; set; }
        private bool MovementAllowed => !StationaryAction && canMove && inputManager != null && inputManager.isActiveAndEnabled && inputManager.CanReadGameplayAction();

        public void SyncLookToCamera()
        {
            if (Camera == null) return;
            targetYaw = cameraYaw = Camera.eulerAngles.y;
            xRotation = cameraPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, Camera.eulerAngles.x), UpperLimit, LowerLimit);
            yawVelocity = pitchVelocity = 0f;
            lookInitialized = true;
        }

        private const float walkSpeed = 5f;
        private const float runSpeed = 8f;
        private Vector2 currentVelocity;
        private Player.Interactor.EquipmentInteractor equipmentInteractor;
        private bool precisionWasActive;
        private bool SlowWalkActive => MovementAllowed && inputManager.SlowWalkHeld;
        private bool CameraPrecisionActive => MovementAllowed && inputManager.CameraPrecisionHeld &&
            equipmentInteractor != null && equipmentInteractor.GetHeldItem() is Equipment.FilmCameraItem;

        private void OnEnable()
        {
            animator = appearanceInitialized ? characterAnimator : GetComponent<Animator>();
            hasAnimator = animator != null && animator.runtimeAnimatorController != null;
            // Physics owns locomotion; animation must never overwrite body motion.
            if (hasAnimator) animator.applyRootMotion = false;
            playerRigidbody = GetComponent<Rigidbody>();
            inputManager = GetComponent<InputManager>();
            bodyCollider = GetComponent<Collider>();
            equipmentInteractor = GetComponent<Player.Interactor.EquipmentInteractor>();
            InitializeAppearance();
            precisionWasActive = false;

            if (playerRigidbody != null)
            {
                playerRigidbody.freezeRotation = true;
                playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            xVelHash = Animator.StringToHash("x_velocity");
            yVelHash = Animator.StringToHash("y_velocity");
            zVelHash = Animator.StringToHash("z_velocity");
            jumpHash = Animator.StringToHash("Jump");
            fallingHash = Animator.StringToHash("Falling");
            groundHash = Animator.StringToHash("Grounded");
        }

        private void InitializeAppearance()
        {
            if (appearanceInitialized || CharacterModel == null) return;
            var oldRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var visual = Instantiate(CharacterModel, transform, false);
            visual.name = "Player Character Visual";
            var rig = visual.GetComponentInChildren<Animator>();
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError("Player character needs a visible mesh. Keeping the original player.", this);
                visual.SetActive(false);
                Destroy(visual);
                return;
            }

            // Fit the complete imported character, including clothing, without scaling physics.
            Bounds bounds = renderers[0].bounds;
            foreach (var part in renderers) bounds.Encapsulate(part.bounds);
            visual.transform.localScale *= CharacterHeight * Mathf.Abs(transform.lossyScale.y) / Mathf.Max(.001f, bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (var part in renderers) bounds.Encapsulate(part.bounds);
            var capsule = bodyCollider as CapsuleCollider;
            float feet = capsule != null ? capsule.center.y - capsule.height * .5f : 0f;
            Vector3 bottom = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
            visual.transform.localPosition += new Vector3(-bottom.x, feet - bottom.y, -bottom.z);
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var camera in visual.GetComponentsInChildren<UnityEngine.Camera>(true)) camera.enabled = false;
            foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;

            // Mesh-only models can replace the appearance. Retarget locomotion only
            // when the imported model actually supplies a valid Humanoid avatar.
            bool canAnimate = rig != null && rig.avatar != null && rig.avatar.isValid && rig.avatar.isHuman;
            if (canAnimate)
            {
                rig.enabled = true;
                rig.runtimeAnimatorController = animator != null ? animator.runtimeAnimatorController : null;
                rig.applyRootMotion = false;
                rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            else if (rig != null) rig.enabled = false;
            if (animator != null && animator != rig) animator.enabled = false;
            foreach (var part in renderers)
            {
                part.gameObject.layer = oldRenderers.Length > 0 ? oldRenderers[0].gameObject.layer : gameObject.layer;
                if (oldRenderers.Length > 0) part.shadowCastingMode = oldRenderers[0].shadowCastingMode;
            }
            foreach (var part in oldRenderers) part.enabled = false;
            characterAnimator = animator = canAnimate ? rig : null;
            hasAnimator = canAnimate && rig.runtimeAnimatorController != null;
            if (canAnimate) rig.Rebind();
            appearanceInitialized = true;

            if (capsule != null)
            {
                capsule.height = CharacterHeight;
                capsule.center = new Vector3(0f, feet + CharacterHeight * .5f, 0f);
            }
            // Keep the view stable during animation, just ahead of the face.
            Vector3 eye = transform.TransformPoint(new Vector3(0f, feet + CharacterHeight * .92f, .3f));
            if (CameraRoot != null) CameraRoot.position = eye;
            if (Camera != null) Camera.position = eye;
            if (CharacterHoldPoint != null)
                CharacterHoldPoint.position = transform.TransformPoint(new Vector3(0f, feet + CharacterHeight * .7f, .636f));
        }

        private void FixedUpdate()
        {
            if (PauseManager.isPaused || playerRigidbody == null) return;
            // A seated action owns the body and physics; LateUpdate still handles looking.
            if (StationaryAction) return;
            if (lookInitialized && canLook && playerRigidbody != null)
                playerRigidbody.MoveRotation(Quaternion.Euler(0f, cameraYaw, 0f));
            bool wasGrounded = grounded;
            SampleGround();
            if (audioGroundSampled && !wasGrounded && grounded) GameplayAudioManager.Play("Player_Land");
            audioGroundSampled = true;
            float groundSpeed = Vector3.ProjectOnPlane(playerRigidbody.velocity, Vector3.up).magnitude;
            if (grounded && MovementAllowed && inputManager.Run && !SlowWalkActive && inputManager.Move.sqrMagnitude > .01f && groundSpeed > .2f && Time.time >= nextFootstep)
            {
                GameplayAudioManager.Play("Player_Footstep_" + Random.Range(1, 11).ToString("00"));
                nextFootstep = Time.time + Mathf.Clamp(.9f / groundSpeed, .25f, .55f);
            }
            HandleJump();
            Move();
        }

        private void Update()
        {
            if (!PauseManager.isPaused) cameraHoldingPose?.RestoreAnimation();
            if (inputManager == null) return;
            if (!MovementAllowed)
            {
                jumpBufferCounter = 0f;
                inputManager.ConsumeJump();
                return;
            }

            if (inputManager.ConsumeJump())
            {
                jumpBufferCounter = Mathf.Max(Time.fixedDeltaTime, JumpBufferTime);
            }
        }

        private void LateUpdate()
        {
            if (PauseManager.isPaused) return;
            CamMovement();
            var held = equipmentInteractor != null ? equipmentInteractor.GetHeldItem() as Equipment.FilmCameraItem : null;
            if (StationaryAction || held == null || !held.transform.IsChildOf(transform))
            {
                cameraHoldingPose?.Reset();
                RestoreHeldViewClip();
                return;
            }
            if (Camera == null) return;
            held.UpdateHandheldPose(Camera);
            if (heldViewCamera == null)
            {
                heldViewCamera = Camera.GetComponent<UnityEngine.Camera>();
                if (heldViewCamera != null)
                {
                    heldViewNearClip = heldViewCamera.nearClipPlane;
                    heldViewCamera.nearClipPlane = Mathf.Min(heldViewNearClip, .025f);
                }
            }
            if (animator != null && animator.isHuman && animator.avatar != null)
            {
                if (cameraHoldingPose == null) cameraHoldingPose = new PlayerCameraPose(animator);
                cameraHoldingPose.Apply(held.RightHandGrip, held.LeftHandGrip, Camera.rotation);
            }
        }

        private void Move()
        {
            bool allowed = MovementAllowed;
            Vector2 currentInput = allowed ? Vector2.ClampMagnitude(inputManager.Move, 1f) : Vector2.zero;
            bool precision = SlowWalkActive;
            // Quiet walking takes priority over sprint with any equipment or empty hands.
            float targetSpeed = precision ? 1.25f : (allowed && inputManager.Run ? runSpeed : walkSpeed);
            Vector3 desiredVelocity = playerRigidbody.rotation * new Vector3(currentInput.x * targetSpeed, 0f, currentInput.y * targetSpeed);
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(playerRigidbody.velocity, Vector3.up);
            float response = Mathf.Max(0f, AnimBlendSpeed) * (grounded ? 1f : Mathf.Clamp01(AirResistance));
            bool easing = precision || (precisionWasActive && (horizontalVelocity - desiredVelocity).sqrMagnitude > .0001f);
            horizontalVelocity = allowed
                ? (easing ? Vector3.Lerp(horizontalVelocity, desiredVelocity, 1f - Mathf.Exp(-8f * Time.fixedDeltaTime))
                    : (grounded ? desiredVelocity : Vector3.Lerp(horizontalVelocity, desiredVelocity, 1f - Mathf.Exp(-response * Time.fixedDeltaTime))))
                : Vector3.zero;
            precisionWasActive = allowed && easing;
            playerRigidbody.velocity = horizontalVelocity + Vector3.up * playerRigidbody.velocity.y;
            Vector3 localVelocity = Quaternion.Inverse(playerRigidbody.rotation) * horizontalVelocity;
            currentVelocity = new Vector2(localVelocity.x, localVelocity.z);
            if (hasAnimator)
            {
                animator.SetFloat(xVelHash, currentVelocity.x);
                animator.SetFloat(yVelHash, currentVelocity.y);
            }
        }

        private void CamMovement()
        {
            if (inputManager == null) inputManager = GetComponent<InputManager>();
            if (playerRigidbody == null) playerRigidbody = GetComponent<Rigidbody>();
            bool isTutorialRecordingLocked = TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialRecordingLookLocked();
            if (inputManager == null || playerRigidbody == null || Camera == null || CameraRoot == null) return;
            if (!canLook || isTutorialRecordingLocked)
            {
                if (isTutorialRecordingLocked &&
                    (inputManager.Look.sqrMagnitude > 0.01f || inputManager.Move.sqrMagnitude > 0.01f))
                    TutorialManager.Instance.WarnTutorialRecordingMovement();
                lookInitialized = false;
                yawVelocity = pitchVelocity = 0f;
                return;
            }
            if (!lookInitialized)
            {
                targetYaw = cameraYaw = transform.eulerAngles.y;
                xRotation = cameraPitch = Mathf.Clamp(Mathf.DeltaAngle(0f, Camera.eulerAngles.x), UpperLimit, LowerLimit);
                lookInitialized = true;
            }

            var MouseX = inputManager.Look.x;
            var MouseY = inputManager.Look.y;
            Camera.position = CameraRoot.position;

            // Mouse delta already measures movement per frame; only sticks need delta time.
            float lookScale = MouseSensitivity * (inputManager.IsPointerLook ? GameOptions.MouseSensitivityMultiplier / 60f : Time.deltaTime);
            bool precision = CameraPrecisionActive;
            if (precision) lookScale *= .35f;
            xRotation -= MouseY * lookScale;
            xRotation = Mathf.Clamp(xRotation, UpperLimit, LowerLimit);
            targetYaw += MouseX * lookScale;

            cameraYaw = Mathf.SmoothDampAngle(cameraYaw, targetYaw, ref yawVelocity, precision ? .12f : .045f);
            cameraPitch = Mathf.SmoothDampAngle(cameraPitch, xRotation, ref pitchVelocity, precision ? .12f : .045f);
            // Render the view independently of the body's fixed-step rotation.
            Camera.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        }

        private void HandleJump()
        {
            if (grounded) coyoteCounter = CoyoteTime;
            else coyoteCounter = Mathf.Max(0f, coyoteCounter - Time.fixedDeltaTime);

            if (!MovementAllowed) { jumpBufferCounter = 0f; return; }
            if (jumpBufferCounter <= 0f || (!grounded && coyoteCounter <= 0f))
            {
                jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - Time.fixedDeltaTime);
                return;
            }

            float jumpSpeed = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * Mathf.Max(0.1f, JumpHeight));
            Vector3 velocity = playerRigidbody.velocity;
            velocity.y = jumpSpeed;
            playerRigidbody.velocity = velocity;
            if (hasAnimator) animator.SetTrigger(jumpHash);

            grounded = false;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
        }

        public void JumpAddForce()
        {
            // The jump force is applied immediately in FixedUpdate().
            // This animation event now only cleans up the visual trigger.
            if (hasAnimator) animator.ResetTrigger(jumpHash);
        }

        private void SampleGround()
        {
            int groundMask = GroundCheck.value == 0 ? Physics.DefaultRaycastLayers : GroundCheck.value;
            Bounds bounds = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position + Vector3.up, new Vector3(0.5f, 2f, 0.5f));
            float radius = Mathf.Max(0.02f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f);
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + radius + 0.05f, bounds.center.z);
            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundHits, 0.15f, groundMask, QueryTriggerInteraction.Ignore);
            grounded = false;
            for (int i = 0; i < count && playerRigidbody.velocity.y <= 0.1f; i++)
            {
                if (groundHits[i].collider.attachedRigidbody == playerRigidbody) continue;
                if (Vector3.Dot(groundHits[i].normal, Vector3.up) < 0.65f) continue;
                grounded = true;
                break;
            }
            if (hasAnimator)
            {
                animator.SetFloat(zVelHash, playerRigidbody.velocity.y);
                animator.SetBool(fallingHash, !grounded && playerRigidbody.velocity.y < -0.1f);
                animator.SetBool(groundHash, grounded);
            }
        }

        private void OnDisable()
        {
            RestoreHeldViewClip();
            cameraHoldingPose?.RestoreAnimation();
            jumpBufferCounter = coyoteCounter = 0f;
            currentVelocity = Vector2.zero;
            if (playerRigidbody != null)
                playerRigidbody.velocity = Vector3.up * playerRigidbody.velocity.y;
        }

        private void SetAnimationGrounding()
        {
            animator.SetBool(fallingHash, !grounded);
            animator.SetBool(groundHash, grounded);
        }
    }
}
