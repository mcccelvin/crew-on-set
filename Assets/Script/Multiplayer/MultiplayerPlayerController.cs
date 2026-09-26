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
    private Animator characterAnimator;
    private Vector3 lastAnimationPosition;
    private bool animationGrounded = true;
    private readonly System.Collections.Generic.HashSet<string> animationParameters = new System.Collections.Generic.HashSet<string>();

    void Start()
    {
        cc = GetComponent<CharacterController>();
        SetupCharacter();
        // The network prefab must never run the legacy career interactor.
        foreach (var component in GetComponentsInChildren<MonoBehaviour>(true))
            if (component != this && component.GetType().Namespace != "Photon.Pun" &&
                (component.GetType().Name.Contains("Interact") || component.GetType().Name == "CrosshairUIClicker" || component.GetType().Name == "TruePixelPlayer")) component.enabled = false;

        // If this is my friend's clone, turn off their camera!
        if (!photonView.IsMine)
        {
            cc.enabled = false;
            foreach (var camera in GetComponentsInChildren<Camera>(true)) camera.enabled = false;
            if (playerCamera != null) playerCamera.gameObject.SetActive(false);

            foreach (var listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        }
        else
        {
            var duplicateCollider = GetComponent<CapsuleCollider>();
            if (duplicateCollider != null) duplicateCollider.enabled = false;
            ActivateOwnedView();
            var crew = GetComponent<MultiplayerCrewController>() ?? gameObject.AddComponent<MultiplayerCrewController>();
            crew.View = playerCamera;
            Cursor.lockState = RoleSelectionUI.Open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = RoleSelectionUI.Open;
        }
    }

    public bool ActivateOwnedView()
    {
        if (!photonView.IsMine) return false;
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera == null) return false;
        playerCamera.gameObject.SetActive(true);
        playerCamera.enabled = true;
        playerCamera.tag = "MainCamera";
        var ownedListener = playerCamera.GetComponent<AudioListener>() ?? playerCamera.gameObject.AddComponent<AudioListener>();
        foreach (var listener in GetComponentsInChildren<AudioListener>(true)) listener.enabled = listener == ownedListener;
        return true;
    }

    private void SetupCharacter()
    {
        var source = Resources.Load<GameObject>("CrewUI/Studio")?.GetComponent<MultiplayerUIReferences>();
        if (source == null || source.playerModel == null) return;
        var oldRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        var visual = Instantiate(source.playerModel, transform, false);
        visual.name = "Player Character Visual";
        visual.SetActive(true);
        var renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { Destroy(visual); return; }
        foreach (var script in visual.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
        foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
        foreach (var body in visual.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        foreach (var light in visual.GetComponentsInChildren<Light>(true)) light.enabled = false;
        float height = Mathf.Max(.5f, source.playerHeight);
        Bounds bounds = renderers[0].bounds;
        foreach (var part in renderers) bounds.Encapsulate(part.bounds);
        visual.transform.localScale *= height * Mathf.Abs(transform.lossyScale.y) / Mathf.Max(.001f, bounds.size.y);
        bounds = renderers[0].bounds;
        foreach (var part in renderers) bounds.Encapsulate(part.bounds);
        float feet = cc.center.y - cc.height * .5f;
        var bottom = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
        visual.transform.localPosition += new Vector3(-bottom.x, feet - bottom.y, -bottom.z);
        foreach (var part in oldRenderers) part.enabled = false;
        foreach (var animator in GetComponentsInChildren<Animator>(true)) animator.enabled = false;
        characterAnimator = visual.GetComponentInChildren<Animator>(true);
        if (characterAnimator != null && characterAnimator.avatar != null && characterAnimator.avatar.isValid && characterAnimator.avatar.isHuman)
        {
            characterAnimator.runtimeAnimatorController = source.playerAnimatorController;
            characterAnimator.applyRootMotion = false;
            characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            characterAnimator.enabled = source.playerAnimatorController != null;
            if (characterAnimator.enabled)
            {
                characterAnimator.Rebind();
                foreach (var parameter in characterAnimator.parameters) animationParameters.Add(parameter.name);
            }
        }
        else characterAnimator = null;
        foreach (var part in renderers)
        {
            part.gameObject.layer = gameObject.layer;
            part.shadowCastingMode = photonView.IsMine ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
        cc.height = height;
        cc.center = new Vector3(cc.center.x, feet + height * .5f, cc.center.z);
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>(true);
        if (playerCamera != null) playerCamera.transform.position = transform.TransformPoint(new Vector3(0, feet + height * .92f, .3f));
        lastAnimationPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (characterAnimator == null || !characterAnimator.enabled) return;
        // Photon already replicates movement. Drive the same singleplayer blend tree on each peer.
        Vector3 movement = (transform.position - lastAnimationPosition) / Mathf.Max(Time.deltaTime, .001f);
        lastAnimationPosition = transform.position;
        Vector3 local = transform.InverseTransformDirection(movement);
        bool grounded = photonView.IsMine ? cc.isGrounded : false;
        if (!photonView.IsMine)
        {
            Vector3 feet = transform.TransformPoint(cc.center - Vector3.up * cc.height * .5f);
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * .15f, Vector3.down, .3f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.GetComponentInParent<PhotonView>() == null && hit.normal.y > .5f) { grounded = true; break; }
        }
        SetAnimationFloat("x_velocity", Mathf.Clamp(local.x, -runSpeed, runSpeed));
        SetAnimationFloat("y_velocity", Mathf.Clamp(local.z, -runSpeed, runSpeed));
        SetAnimationFloat("z_velocity", movement.y);
        if (animationParameters.Contains("Grounded")) characterAnimator.SetBool("Grounded", grounded);
        if (animationParameters.Contains("Falling")) characterAnimator.SetBool("Falling", !grounded && movement.y < -.1f);
        if (animationGrounded && !grounded && movement.y > .5f && animationParameters.Contains("Jump")) characterAnimator.SetTrigger("Jump");
        animationGrounded = grounded;
    }

    private void SetAnimationFloat(string name, float value)
    {
        if (animationParameters.Contains(name)) characterAnimator.SetFloat(name, value, .1f, Time.deltaTime);
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
