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
        private bool MovementAllowed => canMove && inputManager != null && inputManager.isActiveAndEnabled && inputManager.CanReadGameplayAction();

        private const float walkSpeed = 5f;
        private const float runSpeed = 8f;
        private Vector2 currentVelocity;
        private Player.Interactor.EquipmentInteractor equipmentInteractor;
        private bool precisionWasActive;
        private bool CameraPrecisionActive => MovementAllowed && inputManager.CameraPrecisionHeld &&
            equipmentInteractor != null && equipmentInteractor.GetHeldItem() is Equipment.FilmCameraItem;

        private void OnEnable()
        {
            hasAnimator = TryGetComponent<Animator>(out animator);
            // Physics owns locomotion; animation must never overwrite body motion.
            if (hasAnimator) animator.applyRootMotion = false;
            playerRigidbody = GetComponent<Rigidbody>();
            inputManager = GetComponent<InputManager>();
            bodyCollider = GetComponent<Collider>();
            equipmentInteractor = GetComponent<Player.Interactor.EquipmentInteractor>();
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

        private void FixedUpdate()
        {
            if (PauseManager.isPaused || playerRigidbody == null) return;
            if (lookInitialized && canLook && playerRigidbody != null)
                playerRigidbody.MoveRotation(Quaternion.Euler(0f, cameraYaw, 0f));
            SampleGround();
            HandleJump();
            Move();
        }

        private void Update()
        {
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
        }

        private void Move()
        {
            bool allowed = MovementAllowed;
            Vector2 currentInput = allowed ? Vector2.ClampMagnitude(inputManager.Move, 1f) : Vector2.zero;
            bool precision = CameraPrecisionActive;
            // Ctrl takes priority over sprint while a camera is equipped.
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
