using UnityEngine;
using UnityEngine.InputSystem;

namespace Player.Manager
{ 
    public class InputManager : MonoBehaviour
    {
        [SerializeField] private PlayerInput PlayerInput;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Run { get; private set; }
        public bool Jump { get; private set; }
        public bool JumpPressedThisFrame { get; private set; }
        public float EquipmentAdjust { get; private set; }
        public float CameraPedestal { get; private set; }

        // One-frame input flags for interaction/equipment/camera actions
        // These are set when the action is performed and automatically cleared in LateUpdate().
        public bool Interact { get; private set; }
        public bool Drop { get; private set; }
        public bool Equip { get; private set; }
        public bool Use { get; private set; }
        public bool InsertCard { get; private set; }
        public bool Record { get; private set; }
        public bool Pause { get; private set; }
        public bool Almanac { get; private set; }
        public bool ContextPanel { get; private set; }
        public bool Continue { get; private set; }
        public float LightTilt { get; private set; }
        public int HotbarSlot { get; private set; } = -1;

        private InputActionMap playerMap;
        private InputActionMap equipmentMap;
        private InputActionMap globalMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction runAction;
        private InputAction jumpAction;
        private InputAction interactAction;
        private InputAction dropAction;
        private InputAction equipAction;
        private InputAction useAction;
        private InputAction insertCardAction;
        private InputAction recordAction;
        private InputAction equipmentAdjustAction;
        private InputAction cameraPedestalAction;
        private InputAction lightTiltAction;
        private InputAction hotbar1Action;
        private InputAction hotbar2Action;
        private InputAction hotbar3Action;
        private InputAction hotbar4Action;
        private InputAction hotbar5Action;
        private InputAction pauseAction;
        private InputAction almanacAction;
        private InputAction contextPanelAction;
        private InputAction continueAction;
        private Player.PlayerController.PlayerController playerController;
        private bool jumpPressed;

        private void Awake()
        {   
            if (PlayerInput == null)
            {
                Debug.LogError("InputManager: PlayerInput reference is null on " + gameObject.name);
                return;
            }

            if (PlayerInput.actions == null)
            {
                Debug.LogError("InputManager: Input Action Asset is missing on " + gameObject.name);
                return;
            }

            playerController = GetComponent<Player.PlayerController.PlayerController>();

            playerMap = PlayerInput.actions.FindActionMap("Player");
            equipmentMap = PlayerInput.actions.FindActionMap("Equipment");
            globalMap = PlayerInput.actions.FindActionMap("Global");

            if (playerMap == null)
            {
                Debug.LogError("InputManager: Player action map could not be found on " + gameObject.name);
                return;
            }

            moveAction = playerMap.FindAction("Move");
            lookAction = playerMap.FindAction("Look");
            runAction = playerMap.FindAction("Run");
            jumpAction = playerMap.FindAction("Jump");
            interactAction = playerMap.FindAction("Interact");
            dropAction = playerMap.FindAction("Drop");
            equipAction = playerMap.FindAction("Equip");
            useAction = playerMap.FindAction("Use");

            if (equipmentMap != null)
            {
                insertCardAction = equipmentMap.FindAction("Insert Card");
                recordAction = equipmentMap.FindAction("Record");
                equipmentAdjustAction = equipmentMap.FindAction("Adjust");
                cameraPedestalAction = equipmentMap.FindAction("Camera Pedestal");
                lightTiltAction = equipmentMap.FindAction("Light Tilt");
                hotbar1Action = equipmentMap.FindAction("Hotbar 1");
                hotbar2Action = equipmentMap.FindAction("Hotbar 2");
                hotbar3Action = equipmentMap.FindAction("Hotbar 3");
                hotbar4Action = equipmentMap.FindAction("Hotbar 4");
                hotbar5Action = equipmentMap.FindAction("Hotbar 5");
            }

            if (globalMap != null)
            {
                pauseAction = globalMap.FindAction("Pause");
                almanacAction = globalMap.FindAction("Almanac");
                contextPanelAction = globalMap.FindAction("Context Panel");
                continueAction = globalMap.FindAction("Continue");
            }
        }

        private void onMove(InputAction.CallbackContext context)
        {
            Move = context.ReadValue<Vector2>();
        }

        private void onLook(InputAction.CallbackContext context)
        {
            Look = context.ReadValue<Vector2>();
        }
            
        private void onRun(InputAction.CallbackContext context)
        { 
            Run = context.ReadValueAsButton();
        }

        private void onJump(InputAction.CallbackContext context)
        {
            Jump = context.ReadValueAsButton();
            if (context.performed && CanReadGameplayAction() && playerController != null && playerController.enabled && playerController.canMove)
            {
                jumpPressed = true;
                JumpPressedThisFrame = true;
            }
        }

        private void onInteract(InputAction.CallbackContext context) { Interact = true; }
        private void onDrop(InputAction.CallbackContext context) { if (CanReadGameplayAction()) Drop = true; }
        private void onEquip(InputAction.CallbackContext context) { if (CanReadGameplayAction()) Equip = true; }
        private void onUse(InputAction.CallbackContext context) { if (CanReadGameplayAction()) Use = true; }
        private void onInsertCard(InputAction.CallbackContext context) { if (CanReadGameplayAction()) InsertCard = true; }
        private void onRecord(InputAction.CallbackContext context) { if (CanReadGameplayAction()) Record = true; }
        private void onPause(InputAction.CallbackContext context) { Pause = true; }
        private void onAlmanac(InputAction.CallbackContext context) { Almanac = true; }
        private void onContextPanel(InputAction.CallbackContext context) { ContextPanel = true; }
        private void onContinue(InputAction.CallbackContext context) { Continue = true; }
        private void onEquipmentAdjust(InputAction.CallbackContext context) { EquipmentAdjust = CanReadGameplayAction() ? context.ReadValue<float>() : 0f; }
        private void onCameraPedestal(InputAction.CallbackContext context) { CameraPedestal = CanReadGameplayAction() ? context.ReadValue<float>() : 0f; }
        private void onLightTilt(InputAction.CallbackContext context) { LightTilt = CanReadGameplayAction() ? context.ReadValue<float>() : 0f; }
        private void onHotbar1(InputAction.CallbackContext context) { if (CanReadGameplayAction()) HotbarSlot = 0; }
        private void onHotbar2(InputAction.CallbackContext context) { if (CanReadGameplayAction()) HotbarSlot = 1; }
        private void onHotbar3(InputAction.CallbackContext context) { if (CanReadGameplayAction()) HotbarSlot = 2; }
        private void onHotbar4(InputAction.CallbackContext context) { if (CanReadGameplayAction()) HotbarSlot = 3; }
        private void onHotbar5(InputAction.CallbackContext context) { if (CanReadGameplayAction()) HotbarSlot = 4; }

        private bool CanReadGameplayAction()
        {
            if (PauseManager.isPaused) return false;
            if (Cursor.visible || Cursor.lockState != CursorLockMode.Locked) return false;
            if (AlmanacManager.Instance != null && AlmanacManager.Instance.IsOpen()) return false;
            if (TutorialUIManager.Instance != null && TutorialUIManager.Instance.IsBossDialogueOpen()) return false;
            if (ContractUIManager.Instance != null && ContractUIManager.Instance.IsContractUIOpen()) return false;
            return true;
        }

        public bool ConsumeJump()
        {
            if (!jumpPressed) return false;

            jumpPressed = false;
            return true;
        }

        public bool ConsumeUse()
        {
            if (!Use) return false;

            Use = false;
            return true;
        }

        public bool ConsumePause()
        {
            if (!Pause) return false;

            Pause = false;
            return true;
        }

        public bool ConsumeAlmanac()
        {
            if (!Almanac) return false;

            Almanac = false;
            return true;
        }

        private void Update()
        {
            if (playerMap == null) return;

            if (CanReadGameplayAction())
            {
                if (moveAction != null) Move = moveAction.ReadValue<Vector2>();
                if (lookAction != null) Look = lookAction.ReadValue<Vector2>();
                if (runAction != null) Run = runAction.IsPressed();
            }
            else
            {
                Move = Vector2.zero;
                Look = Vector2.zero;
                Run = false;
                jumpPressed = false;
                EquipmentAdjust = 0f;
                CameraPedestal = 0f;
                LightTilt = 0f;
            }
        }

        // Clear one-frame flags here so other scripts can read them during Update()
        private void LateUpdate()
        {
            Interact = false;
            Drop = false;
            Equip = false;
            Use = false;
            InsertCard = false;
            Record = false;
            Pause = false;
            Almanac = false;
            ContextPanel = false;
            Continue = false;
            JumpPressedThisFrame = false;
            LightTilt = 0f;
            HotbarSlot = -1;
        }

        private void OnEnable()
        {
            if (playerMap == null) return;

            if (moveAction != null) { moveAction.performed += onMove; moveAction.canceled += onMove; }
            if (lookAction != null) { lookAction.performed += onLook; lookAction.canceled += onLook; }
            if (runAction != null) { runAction.performed += onRun; runAction.canceled += onRun; }
            if (jumpAction != null) { jumpAction.performed += onJump; jumpAction.canceled += onJump; }

            if (interactAction != null) interactAction.performed += onInteract;
            if (dropAction != null) dropAction.performed += onDrop;
            if (equipAction != null) equipAction.performed += onEquip;
            if (useAction != null) useAction.performed += onUse;
            if (insertCardAction != null) insertCardAction.performed += onInsertCard;
            if (recordAction != null) recordAction.performed += onRecord;
            if (equipmentAdjustAction != null) { equipmentAdjustAction.performed += onEquipmentAdjust; equipmentAdjustAction.canceled += onEquipmentAdjust; }
            if (cameraPedestalAction != null) { cameraPedestalAction.performed += onCameraPedestal; cameraPedestalAction.canceled += onCameraPedestal; }
            if (lightTiltAction != null) { lightTiltAction.performed += onLightTilt; lightTiltAction.canceled += onLightTilt; }
            if (hotbar1Action != null) hotbar1Action.performed += onHotbar1;
            if (hotbar2Action != null) hotbar2Action.performed += onHotbar2;
            if (hotbar3Action != null) hotbar3Action.performed += onHotbar3;
            if (hotbar4Action != null) hotbar4Action.performed += onHotbar4;
            if (hotbar5Action != null) hotbar5Action.performed += onHotbar5;
            if (pauseAction != null) pauseAction.performed += onPause;
            if (almanacAction != null) almanacAction.performed += onAlmanac;
            if (contextPanelAction != null) contextPanelAction.performed += onContextPanel;
            if (continueAction != null) continueAction.performed += onContinue;

            playerMap.Enable();
            if (equipmentMap != null) equipmentMap.Enable();
            if (globalMap != null) globalMap.Enable();
        }

        private void OnDisable()
        {
            if (moveAction != null) { moveAction.performed -= onMove; moveAction.canceled -= onMove; }
            if (lookAction != null) { lookAction.performed -= onLook; lookAction.canceled -= onLook; }
            if (runAction != null) { runAction.performed -= onRun; runAction.canceled -= onRun; }
            if (jumpAction != null) { jumpAction.performed -= onJump; jumpAction.canceled -= onJump; }

            if (interactAction != null) interactAction.performed -= onInteract;
            if (dropAction != null) dropAction.performed -= onDrop;
            if (equipAction != null) equipAction.performed -= onEquip;
            if (useAction != null) useAction.performed -= onUse;
            if (insertCardAction != null) insertCardAction.performed -= onInsertCard;
            if (recordAction != null) recordAction.performed -= onRecord;
            if (equipmentAdjustAction != null) { equipmentAdjustAction.performed -= onEquipmentAdjust; equipmentAdjustAction.canceled -= onEquipmentAdjust; }
            if (cameraPedestalAction != null) { cameraPedestalAction.performed -= onCameraPedestal; cameraPedestalAction.canceled -= onCameraPedestal; }
            if (lightTiltAction != null) { lightTiltAction.performed -= onLightTilt; lightTiltAction.canceled -= onLightTilt; }
            if (hotbar1Action != null) hotbar1Action.performed -= onHotbar1;
            if (hotbar2Action != null) hotbar2Action.performed -= onHotbar2;
            if (hotbar3Action != null) hotbar3Action.performed -= onHotbar3;
            if (hotbar4Action != null) hotbar4Action.performed -= onHotbar4;
            if (hotbar5Action != null) hotbar5Action.performed -= onHotbar5;
            if (pauseAction != null) pauseAction.performed -= onPause;
            if (almanacAction != null) almanacAction.performed -= onAlmanac;
            if (contextPanelAction != null) contextPanelAction.performed -= onContextPanel;
            if (continueAction != null) continueAction.performed -= onContinue;

            if (playerMap != null) playerMap.Disable();
            if (equipmentMap != null) equipmentMap.Disable();
            if (globalMap != null) globalMap.Disable();

            Move = Vector2.zero;
            Look = Vector2.zero;
            Run = false;
            Jump = false;
            JumpPressedThisFrame = false;
            Interact = false;
            Drop = false;
            Equip = false;
            Use = false;
            InsertCard = false;
            Record = false;
            Pause = false;
            Almanac = false;
            ContextPanel = false;
            Continue = false;
            EquipmentAdjust = 0f;
            CameraPedestal = 0f;
            LightTilt = 0f;
            HotbarSlot = -1;
            jumpPressed = false;
        }
    }
}
