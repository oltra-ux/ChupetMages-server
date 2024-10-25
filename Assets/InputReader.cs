using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReader", menuName = "Assets/Input Reader")]
public class InputReader : ScriptableObject, PlayerInputActions.IPlayerActions {

    public Vector2 Move => inputActions.Player.Move.ReadValue<Vector2>();
    public bool IsJumping => inputActions.Player.Jump.ReadValue<float>() > 0;

    PlayerInputActions inputActions;
    
    void OnEnable(){
        if(inputActions == null) {
            inputActions = new PlayerInputActions();
            inputActions.Player.SetCallbacks(this);
        }
        inputActions.Enable();
    }

    public void Enable(){
        inputActions.Enable();
    }
    public void OnMove(InputAction.CallbackContext context){
        //noop
    }
    public void OnLook(InputAction.CallbackContext context){
        //noop
    }
    public void OnFire(InputAction.CallbackContext context){
        //noop
    }
    public void OnJump(InputAction.CallbackContext context){
        //noop
    }
    public void OnAltFire(InputAction.CallbackContext context){
        //noop
    }
    public void OnWeapon1(InputAction.CallbackContext context){
        //noop
    }
    public void OnWeapon2(InputAction.CallbackContext context){
        //noop
    }
    public void OnWeapon3(InputAction.CallbackContext context){
        //noop
    }
    public void OnWeapon4(InputAction.CallbackContext context){
        //noop
    }

}

