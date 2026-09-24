using UnityEngine;
using Photon.Pun;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class MultiplayerPlayerController : MonoBehaviourPun
{
    [Header("Setup")]
    public Camera playerCamera;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float runSpeed = 8f;
    public float jumpHeight = 1.2f;
    public float jumpBufferTime = 0.15f;
    public float coyoteTime = 0.1f;
    public float gravity = -9.81f;

    [Header("Looking")]
    public float lookSensitivity = 2f;
    public float maxLookAngle = 80f;

    private CharacterController cc;
    private float verticalRotation = 0f;
    private Vector3 velocity;
    private float jumpBuffer;
    private float coyoteCounter;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        // The network prefab must never run the legacy career interactor.
        foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
            if (component != this && component.GetType().Namespace != "Photon.Pun" &&
                (component.GetType().Name.Contains("Interact") || component.GetType().Name == "CrosshairUIClicker" || component.GetType().Name == "TruePixelPlayer")) component.enabled = false;

        // If this is my friend's clone, turn off their camera!
        if (!photonView.IsMine)
        {
            cc.enabled = false;
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);

            AudioListener listener = GetComponentInChildren<AudioListener>();
            if (listener != null) listener.enabled = false;
        }
        else
        {
            var duplicateCollider = GetComponent<CapsuleCollider>();
            if (duplicateCollider != null) duplicateCollider.enabled = false;
            var crew = gameObject.AddComponent<MultiplayerCrewController>();
            crew.View = playerCamera;
            // If it IS me, lock the mouse to the screen
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        // Ignore everything if this isn't my character
        if (!photonView.IsMine) return;
        bool controlsAllowed = Application.isFocused && !RoleSelectionUI.Open && Cursor.lockState == CursorLockMode.Locked;
        if (!controlsAllowed) jumpBuffer = 0f;

        // --- 1. LOOK AROUND (MOUSE) ---
        Mouse mouse = Mouse.current;
        Vector2 mouseDelta = controlsAllowed && mouse != null ? mouse.delta.ReadValue() * 0.1f : Vector2.zero;
        float mouseX = mouseDelta.x * lookSensitivity * GameOptions.MouseSensitivityMultiplier;
        float mouseY = mouseDelta.y * lookSensitivity * GameOptions.MouseSensitivityMultiplier;

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

        if (controlsAllowed && keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y += 1f;
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        bool running = controlsAllowed && keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        bool slowWalk = controlsAllowed && keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        float speed = Mathf.Max(0f, slowWalk ? 1.25f : running ? runSpeed : walkSpeed);
        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y) * speed;

        // --- 3. GRAVITY ---
        bool grounded = cc.isGrounded && velocity.y <= 0f;
        coyoteCounter = grounded ? Mathf.Max(0f, coyoteTime) : Mathf.Max(0f, coyoteCounter - Time.deltaTime);
        if (controlsAllowed && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            jumpBuffer = Mathf.Max(Time.deltaTime, jumpBufferTime);
        float downwardGravity = -Mathf.Max(0.01f, Mathf.Abs(gravity));
        if (grounded)
        {
            velocity.y = -2f; // Keep us snapped to the floor
        }
        if (controlsAllowed && jumpBuffer > 0f && (grounded || coyoteCounter > 0f))
        {
            velocity.y = Mathf.Sqrt(-2f * downwardGravity * Mathf.Max(0.1f, jumpHeight));
            jumpBuffer = coyoteCounter = 0f;
        }
        else jumpBuffer = Mathf.Max(0f, jumpBuffer - Time.deltaTime);
        velocity.y += downwardGravity * Time.deltaTime;
        CollisionFlags collisions = cc.Move((move + velocity) * Time.deltaTime);
        if ((collisions & CollisionFlags.Above) != 0 && velocity.y > 0f) velocity.y = 0f;
    }

    private void OnDisable()
    {
        jumpBuffer = coyoteCounter = 0f;
    }
}
