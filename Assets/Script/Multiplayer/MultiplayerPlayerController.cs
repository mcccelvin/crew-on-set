using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class SimpleMultiplayerPlayer : MonoBehaviourPun
{
    [Header("Setup")]
    public Camera playerCamera;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Looking")]
    public float lookSensitivity = 2f;
    public float maxLookAngle = 80f;

    private CharacterController cc;
    private float verticalRotation = 0f;
    private Vector3 velocity;

    void Start()
    {
        cc = GetComponent<CharacterController>();

        // If this is my friend's clone, turn off their camera!
        if (!photonView.IsMine)
        {
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);

            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
        else
        {
            // If it IS me, lock the mouse to the screen
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // Ignore everything if this isn't my character
        if (!photonView.IsMine) return;

        // --- 1. LOOK AROUND (MOUSE) ---
        Mouse mouse = Mouse.current;
        Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() * 0.1f : Vector2.zero;
        float mouseX = mouseDelta.x * lookSensitivity;
        float mouseY = mouseDelta.y * lookSensitivity;

        // Turn body left/right
        transform.Rotate(Vector3.up * mouseX);

        // Tilt camera up/down
        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -maxLookAngle, maxLookAngle);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
        }

        // --- 2. WALK (WASD) ---
        Keyboard keyboard = Keyboard.current;
        Vector2 moveInput = Vector2.zero;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y += 1f;
        }

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
        cc.Move(move * walkSpeed * Time.deltaTime);

        // --- 3. GRAVITY ---
        if (cc.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f; // Keep us snapped to the floor
        }
        velocity.y += gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }
}
