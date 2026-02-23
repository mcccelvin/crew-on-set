using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    private PlayerInput playerInput;
    public PlayerInput.OnFootActions onFoot;
    public PlayerInput.PauseActions pause;

    private PlayerMotor motor;
    private PlayerLook look;
    private 
    void Awake()
    {
        playerInput = new PlayerInput();
        onFoot = playerInput.OnFoot;
        pause = playerInput.Pause;

        motor = GetComponent<PlayerMotor>();
        look = GetComponent<PlayerLook>();

        onFoot.Jump.performed += ctx => motor.Jump();
        onFoot.Sprint.performed += ctx => motor.Sprint();
    }

    void FixedUpdate()
    {
        //tell the playermotor to move using the value from movement action
        motor.ProcessMove(onFoot.Movement.ReadValue<Vector2>( ));

    }

    void LateUpdate()
    {
        //tell the playerlook to look using the value from look action
        look.ProcessLook(onFoot.Look.ReadValue<Vector2>());
    }
    private void OnEnable()
    {
        onFoot.Enable();
    }
    private void OnDisable()
    {
        onFoot.Disable();
    }
}
