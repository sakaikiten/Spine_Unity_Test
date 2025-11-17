
using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤー → 今まで通り OnXXX 経由で値が入る
// AI → 別スクリプトから inputs.Move = ...;, inputs.RightPunchPressed = true; できる

public class FighterInputs : MonoBehaviour {
    public Vector2 Move { get; set; }

    // 4技を1フレーム・フラグで受ける
    public bool RightPunchPressed { get; set; }
    public bool LeftPunchPressed  { get; set; }
    public bool RightKickPressed  { get; set; }
    public bool LeftKickPressed   { get; set; }
    
    public bool GuardPressed { get; set; }  //押した瞬間
    public bool GuardHeld    { get; set; }  //押している間

    // ---- Input System (Invoke Unity Events) から呼ばれるメソッド名 ----
    public void OnMove(InputAction.CallbackContext ctx) {
        Move = ctx.ReadValue<Vector2>();
    }
    public void OnRightPunch(InputAction.CallbackContext ctx) {
        if (ctx.performed) RightPunchPressed = true;
    }
    public void OnLeftPunch(InputAction.CallbackContext ctx) {
        if (ctx.performed) LeftPunchPressed = true;
    }
    public void OnRightKick(InputAction.CallbackContext ctx) {
        if (ctx.performed) RightKickPressed = true;
    }
    public void OnLeftKick(InputAction.CallbackContext ctx) {
        if (ctx.performed) LeftKickPressed = true;
    }

    public void OnGuard(InputAction.CallbackContext ctx) {
        if (ctx.performed) {
            // ボタンが「押された瞬間」
            GuardPressed = true;   // 1フレームだけ使う
            GuardHeld    = true;   // 押している間は true
        }
        else if (ctx.canceled) {
            // ボタンが「離された瞬間」
            GuardHeld = false;
        }
    }

    // 毎フレームの終わりで呼んでフラグを消す（1フレーム消費）
    public void ConsumeFrameButtons() {
        RightPunchPressed = false;
        LeftPunchPressed  = false;
        RightKickPressed  = false;
        LeftKickPressed   = false;

        // ★ GuardPressed も 1フレームだけ使うのでここでリセット
        GuardPressed = false;
    }
}
