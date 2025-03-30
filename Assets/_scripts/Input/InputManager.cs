using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static PlayerInput PlayerInput;
    public static Vector2 Movement;
    public static bool JumpWasPressed;
    public static bool JumpIsHeld;
    public static bool JumpWasReleased;
    public static bool DashWasPressed;

    private InputAction _moveAction;
    private InputAction _jumpAction;
    private InputAction _dashAction;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        PlayerInput = GetComponent<PlayerInput>();
        _moveAction = PlayerInput.actions["Move"];
        _jumpAction = PlayerInput.actions["Jump"];
        _dashAction = PlayerInput.actions["Dash"];
    }

    // Update is called once per frame
    void Update()
    {
        Movement = _moveAction.ReadValue<Vector2>();
        JumpWasPressed = _jumpAction.WasPressedThisFrame();
        JumpIsHeld = _jumpAction.IsPressed();
        JumpWasReleased = _jumpAction.WasReleasedThisFrame();
        DashWasPressed = _dashAction.WasPressedThisFrame();
    }
}
