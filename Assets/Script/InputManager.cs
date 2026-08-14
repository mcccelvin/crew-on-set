using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

namespace Player.Manager
{ 
    public class InputManager : MonoBehaviour
    {
        [SerializeField] private PlayerInput PlayerInput;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Run { get; private set; }
        public bool Jump { get; private set; }

        // One-frame input flags for interaction/equipment/camera actions
        // These are set when the action is performed and automatically cleared in LateUpdate().
        public bool Interact { get; private set; }
        public bool Drop { get; private set; }
        public bool Equip { get; private set; }

        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction runAction;
        private InputAction jumpAction;
        private InputAction interactAction;
        private InputAction dropAction;
        private InputAction equipAction;

        private void Awake()
        {   
            if (PlayerInput == null)
            {
                Debug.LogError("InputManager: PlayerInput reference is null on " + gameObject.name);
                return;
            }

            playerMap = PlayerInput.currentActionMap;
            if (playerMap == null && PlayerInput.actions != null)
                playerMap = PlayerInput.actions.FindActionMap(PlayerInput.defaultActionMap);

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
            equipAction = PlayerInput.actions.FindAction("Equip");
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
        }

        private void onInteract(InputAction.CallbackContext context) { Interact = true; }
        private void onDrop(InputAction.CallbackContext context) { Drop = true; }
        private void onEquip(InputAction.CallbackContext context) { Equip = true; }

        // Clear one-frame flags here so other scripts can read them during Update()
        private void LateUpdate()
        {
            Interact = false;
            Drop = false;
            Equip = false;
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

            playerMap.Enable();
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

            if (playerMap != null) playerMap.Disable();

            Move = Vector2.zero;
            Look = Vector2.zero;
            Run = false;
            Jump = false;
            Interact = false;
            Drop = false;
            Equip = false;
        }
    }
}
