using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Player.Manager;

namespace Player.PlayerController
{

    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float AnimBlendSpeed = 8.9f;
        [SerializeField] private Transform CameraRoot;
        [SerializeField] private Transform Camera;
        [SerializeField] private float UpperLimit = -40f;
        [SerializeField] private float LowerLimit = 70f;
        [SerializeField] private float MouseSensitivity = 21.9f;
        [SerializeField, Range(10, 500)] private float JumpFactor = 260f;
        [SerializeField] private float Dis2Ground = 1.2f;
        [SerializeField] private float AirResistance = 0.8f;
        [SerializeField] private LayerMask GroundCheck;

        // Pickup / film camera inspector fields
        [SerializeField] private Transform HoldPoint;
        [SerializeField] private Camera PlayerCam;
        [SerializeField] private Camera FilmCam;
        [SerializeField] private GameObject FilmUICanvas;

        private Rigidbody playerRigidbody;
        private InputManager inputManager;
        private Animator animator;
        private bool grounded = false;     
        private bool hasAnimator;
        private int xVelHash;
        private int yVelHash;
        private int zVelHash;
        private int jumpHash;
        private int groundHash;
        private int fallingHash;
        private float xRotation;

        private const float walkSpeed = 2f;
        private const float runSpeed = 6f;
        private Vector2 currentVelocity;

        // pickup & camera state
        private GameObject heldObject;
        private bool filmEquipped = false;
        private bool filmActive = false;
        private Camera originalFilmCam;
        private RenderTexture originalFilmTargetTexture;

        // cached reference to Pause UI component so we can hide the "use (E)" UI when picking up items
        private Pause pauseUI;

        private void Start()
        {   
            hasAnimator = TryGetComponent<Animator>(out animator);
            playerRigidbody = GetComponent<Rigidbody>();
            inputManager = GetComponent<InputManager>();

            // find Pause component (if present) so we can control its interactUI visibility
            pauseUI = FindObjectOfType<Pause>();

            // prevent physics from rotating the player
            if (playerRigidbody != null) playerRigidbody.freezeRotation = true;

            xVelHash = Animator.StringToHash("x_velocity");
            yVelHash = Animator.StringToHash("y_velocity");
            zVelHash = Animator.StringToHash("z_velocity");
            jumpHash = Animator.StringToHash("Jump");
            fallingHash = Animator.StringToHash("Falling");
            groundHash = Animator.StringToHash("Grounded");

            // preserve inspector-assigned filmCam and its target texture
            originalFilmCam = FilmCam;
            if (FilmCam != null) originalFilmTargetTexture = FilmCam.targetTexture;

            // Default state: PlayerCamera active, FilmCamera inactive, UI hidden
            SetPlayerCameraActive(true);
            SetFilmCameraActive(false);
        }

        private void Update()
        {
            // wire pickup/drop and camera toggle to InputManager actions
            HandlePickupDrop();
            HandleCameraSwitch();
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

            float targetSpeed = inputManager.Run ? runSpeed : walkSpeed;
            if(inputManager.Move == Vector2.zero) targetSpeed = 0f;

            if (grounded)
            {

                currentVelocity.x = Mathf.Lerp(currentVelocity.x, inputManager.Move.x * targetSpeed, AnimBlendSpeed * Time.fixedDeltaTime);
                currentVelocity.y = Mathf.Lerp(currentVelocity.y, inputManager.Move.y * targetSpeed, AnimBlendSpeed * Time.fixedDeltaTime);

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
            if (!hasAnimator) return;

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

            Debug.DrawRay(transform.position, Vector3.down * rayLength, Color.green);

            grounded = Physics.Raycast(transform.position, Vector3.down, rayLength);

            animator.SetFloat(zVelHash, playerRigidbody.velocity.y);
            animator.SetBool(fallingHash, !grounded && playerRigidbody.velocity.y < -0.1f);
            animator.SetBool(groundHash, grounded);
            Debug.DrawRay(playerRigidbody.worldCenterOfMass, Vector3.down * (Dis2Ground + 0.1f), Color.red);
        }

        private void SetAnimationGrounding()
        {
            animator.SetBool(fallingHash, !grounded);
            animator.SetBool(groundHash, grounded);
        }

        // ---------------- Camera Switching ----------------
        private void HandleCameraSwitch()
        {
            // Use InputManager.Equip (mapped to 'F' in your Input Actions)
            if (!inputManager.Equip) return;
            if (!filmEquipped) return;

            // toggle state
            filmActive = !filmActive;

            if (filmActive)
            {
                // Disable PlayerCamera
                if (PlayerCam != null)
                {
                    PlayerCam.enabled = false;
                    PlayerCam.gameObject.SetActive(false);
                }

                // Enable FilmCamera
                if (FilmCam != null)
                {
                    FilmCam.targetTexture = null;
                    FilmCam.gameObject.SetActive(true);
                    FilmCam.enabled = true;

                    if (HoldPoint != null)
                        FilmCam.transform.position = HoldPoint.position;
                    FilmCam.transform.rotation = PlayerCam != null ? PlayerCam.transform.rotation : Camera.rotation;
                }
                else
                {
                    Debug.LogWarning("FilmCamera reference is null when trying to activate it.");
                }

                if (FilmUICanvas != null) FilmUICanvas.SetActive(true);
                Debug.Log("Switched to FilmCamera via input action");
            }
            else
            {
                // Enable PlayerCamera
                if (PlayerCam != null)
                {
                    PlayerCam.gameObject.SetActive(true);
                    PlayerCam.enabled = true;
                }

                // Disable FilmCamera
                if (FilmCam != null)
                {
                    FilmCam.enabled = false;
                    FilmCam.gameObject.SetActive(false);

                    // restore the camera's original target texture (if any)
                    FilmCam.targetTexture = originalFilmTargetTexture;
                }

                if (FilmUICanvas != null) FilmUICanvas.SetActive(false);
                Debug.Log("Switched back to PlayerCamera via input action");
            }
        }

        private void SetPlayerCameraActive(bool active)
        {
            if (PlayerCam != null)
            {
                PlayerCam.enabled = active;
                PlayerCam.gameObject.SetActive(active);
            }
        }

        private void SetFilmCameraActive(bool active)
        {
            if (FilmCam != null)
            {
                FilmCam.enabled = active;
                FilmCam.gameObject.SetActive(active);

                if (active)
                    FilmCam.targetTexture = null;
                else
                    FilmCam.targetTexture = originalFilmTargetTexture;
            }
            if (FilmUICanvas != null) FilmUICanvas.SetActive(active);
        }

        // ---------------- Pickup / Drop ----------------
        private void HandlePickupDrop()
        {
            // Interact (E) to pickup, Drop (G) to drop - both are wired in InputManager
            if (inputManager.Interact)
            {
                if (heldObject == null)
                    TryPickup();
            }

            if (inputManager.Drop)
            {
                if (heldObject != null)
                    DropObject();
            }
        }

        private void TryPickup()
        {

            Ray ray = new Ray(Camera.position, Camera.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 3f))
            {
                Pickup pickup = hit.collider.GetComponent<Pickup>();
                if (pickup != null)
                {
                    heldObject = hit.collider.gameObject;
                    Rigidbody objRb = heldObject.GetComponent<Rigidbody>();
                    Collider objCol = heldObject.GetComponent<Collider>();

                    if (objRb != null) objRb.isKinematic = true;
                    if (objCol != null) objCol.enabled = false;

                    if (HoldPoint != null)
                    {
                        heldObject.transform.SetParent(HoldPoint);
                        heldObject.transform.localPosition = Vector3.zero;
                        heldObject.transform.localRotation = Quaternion.identity;
                    }

                    // If the picked object contains a Camera, use that as the FilmCam while held.
                    Camera pickedCam = heldObject.GetComponent<Camera>();
                    if (pickedCam == null)
                        pickedCam = heldObject.GetComponentInChildren<Camera>();

                    if (pickedCam != null)
                    {
                        // store this camera's target texture so we can restore it later
                        originalFilmTargetTexture = pickedCam.targetTexture;

                        FilmCam = pickedCam;
                        // ensure the camera is initially disabled until the player toggles it
                        FilmCam.enabled = false;
                        FilmCam.gameObject.SetActive(false);
                    }

                    filmEquipped = true; // mark FilmCamera as held

                    // Hide the interact UI if Pause component exists (prevents the "use (E) / use (F)" UI from staying visible)
                    if (pauseUI != null && pauseUI.interactUI != null)
                    {
                        pauseUI.interactUI.SetActive(false);
                    }

                    Debug.Log("Picked up FilmCamera (use camera action to toggle view)");
                }
            }
        }

        private void DropObject()
        {
            if (heldObject == null) return;

            Rigidbody objRb = heldObject.GetComponent<Rigidbody>();
            Collider objCol = heldObject.GetComponent<Collider>();

            if (objRb != null) objRb.isKinematic = false;
            if (objCol != null) objCol.enabled = true;

            // If the FilmCam belongs to the held object, disable it and restore original reference
            if (FilmCam != null && FilmCam.gameObject == heldObject)
            {
                FilmCam.enabled = false;
                FilmCam.gameObject.SetActive(false);

                // restore target texture to what camera had before being used for screen rendering
                FilmCam.targetTexture = originalFilmTargetTexture;

                FilmCam = originalFilmCam;
                if (FilmCam != null) FilmCam.targetTexture = originalFilmTargetTexture;
            }

            heldObject.transform.SetParent(null);
            heldObject = null;

            filmEquipped = false;
            filmActive = false;

            // Reset to PlayerCamera
            SetPlayerCameraActive(true);
            SetFilmCameraActive(false);

            Debug.Log("Dropped FilmCamera, back to PlayerCamera");
        }

    }
}