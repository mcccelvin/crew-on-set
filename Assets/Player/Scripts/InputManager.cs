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
            moveAction = playerMap.FindAction("Move");
            lookAction = playerMap.FindAction("Look");
            runAction = playerMap.FindAction("Run");
            jumpAction = playerMap.FindAction("Jump");
            interactAction = playerMap.FindAction("Interact");
            dropAction = playerMap.FindAction("Drop");
            equipAction = PlayerInput.actions.FindAction("Equip");


            moveAction.performed += onMove;
            lookAction.performed += onLook;
            runAction.performed += onRun;
            jumpAction.performed += onJump;

            moveAction.canceled += onMove;
            lookAction.canceled += onLook;
            runAction.canceled += onRun;
            jumpAction.canceled += onJump;

            // Set one-frame flags on 'performed' only. They are cleared in LateUpdate().
            if (interactAction != null)
            {
                interactAction.performed += ctx => Interact = true;
            }
            else
            {
                Interact = false;
            }

            if (dropAction != null)
            {
                dropAction.performed += ctx => Drop = true;
            }
            else
            {
                Drop = false;
            }

            if (equipAction != null)
            {
                equipAction.performed += ctx => Equip = true;
            }
            else
            {
                Equip = false;
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
        }

        // Clear one-frame flags here so other scripts can read them during Update()
        private void LateUpdate()
        {
            Interact = false;
            Drop = false;
            Equip = false;
        }

        private void onEnable()
        {
            if (playerMap != null) playerMap.Enable();
        }

        private void onDisable()
        {
            if (playerMap != null) playerMap.Disable();
        }
    }
}
