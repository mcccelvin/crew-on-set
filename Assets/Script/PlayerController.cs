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
        [SerializeField, Range(10, 500)] private float JumpFactor = 260f;
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

        private const float walkSpeed = 2f;
        private const float runSpeed = 6f;
        private Vector2 currentVelocity;

        private void Start()
        {
            hasAnimator = TryGetComponent<Animator>(out animator);
            playerRigidbody = GetComponent<Rigidbody>();
            inputManager = GetComponent<InputManager>();

            if (playerRigidbody != null) playerRigidbody.freezeRotation = true;

            xVelHash = Animator.StringToHash("x_velocity");
            yVelHash = Animator.StringToHash("y_velocity");
            zVelHash = Animator.StringToHash("z_velocity");
            jumpHash = Animator.StringToHash("Jump");
            fallingHash = Animator.StringToHash("Falling");
            groundHash = Animator.StringToHash("Grounded");
        }

        private void FixedUpdate()
        {
            Move();
            HandleJump();
            SampleGround();
        }

        private void LateUpdate()
        {
            CamMovement();
        }

        private void Move()
        {
            if (!hasAnimator) return;

            // --- THE FIX: If we can't move, force input to zero so we smoothly stop ---
            Vector2 currentInput = canMove ? inputManager.Move : Vector2.zero;

            float targetSpeed = inputManager.Run ? runSpeed : walkSpeed;
            if (currentInput == Vector2.zero) targetSpeed = 0f;

            if (grounded)
            {
                currentVelocity.x = Mathf.Lerp(currentVelocity.x, currentInput.x * targetSpeed, AnimBlendSpeed * Time.fixedDeltaTime);
                currentVelocity.y = Mathf.Lerp(currentVelocity.y, currentInput.y * targetSpeed, AnimBlendSpeed * Time.fixedDeltaTime);

                var xVelDifference = currentVelocity.x - playerRigidbody.velocity.x;
                var zVelDifference = currentVelocity.y - playerRigidbody.velocity.z;

                playerRigidbody.AddForce(transform.TransformVector(new Vector3(xVelDifference, 0, zVelDifference)), ForceMode.VelocityChange);
            }
            else
            {
                playerRigidbody.AddForce(transform.TransformVector(new Vector3(currentVelocity.x * AirResistance, 0, currentVelocity.y * AirResistance)), ForceMode.VelocityChange);
            }

            animator.SetFloat(xVelHash, currentVelocity.x);
            animator.SetFloat(yVelHash, currentVelocity.y);
        }

        private void CamMovement()
        {
            if (!hasAnimator || !canLook) return;

            var MouseX = inputManager.Look.x;
            var MouseY = inputManager.Look.y;
            Camera.position = CameraRoot.position;

            xRotation -= MouseY * MouseSensitivity * Time.smoothDeltaTime;
            xRotation = Mathf.Clamp(xRotation, UpperLimit, LowerLimit);

            Camera.localRotation = Quaternion.Euler(xRotation, 0, 0);
            playerRigidbody.MoveRotation(playerRigidbody.rotation * Quaternion.Euler(0, MouseX * MouseSensitivity * Time.smoothDeltaTime, 0));
        }

        private void HandleJump()
        {
            if (!hasAnimator) return;
            if (!canMove) return; // --- THE FIX: Prevent jumping when frozen! ---
            if (!inputManager.Jump) return;
            if (!grounded) return;
            animator.SetTrigger(jumpHash);
        }

        public void JumpAddForce()
        {
            playerRigidbody.AddForce(-playerRigidbody.velocity.y * Vector3.up, ForceMode.VelocityChange);
            playerRigidbody.AddForce(Vector3.up * JumpFactor, ForceMode.Impulse);
            animator.ResetTrigger(jumpHash);
        }

        private void SampleGround()
        {
            if (!hasAnimator) return;

            float rayLength = 1.2f;
            grounded = Physics.Raycast(transform.position, Vector3.down, rayLength);

            animator.SetFloat(zVelHash, playerRigidbody.velocity.y);
            animator.SetBool(fallingHash, !grounded && playerRigidbody.velocity.y < -0.1f);
            animator.SetBool(groundHash, grounded);
        }

        private void SetAnimationGrounding()
        {
            animator.SetBool(fallingHash, !grounded);
            animator.SetBool(groundHash, grounded);
        }
    }
}