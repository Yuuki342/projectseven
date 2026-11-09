using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AnimeOpenWorld
{
    /// <summary>
    /// 入力の窓口。新 Input System / 旧 Input Manager のどちらの設定でも動く。
    /// キーボード + マウスとゲームパッドに対応。
    /// </summary>
    public static class GameInput
    {
        /// <summary>移動入力 (長さ 1 以下)</summary>
        public static Vector2 Move
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 v = Vector2.zero;
                var kb = Keyboard.current;
                if (kb != null)
                {
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v.y += 1f;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v.y -= 1f;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) v.x += 1f;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) v.x -= 1f;
                }
                var pad = Gamepad.current;
                if (pad != null) v += pad.leftStick.ReadValue();
                return Vector2.ClampMagnitude(v, 1f);
#else
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                return Vector2.ClampMagnitude(v, 1f);
#endif
            }
        }

        /// <summary>カメラ回転入力 (1 フレーム分)</summary>
        public static Vector2 Look
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                Vector2 v = Vector2.zero;
                var mouse = Mouse.current;
                if (mouse != null) v += mouse.delta.ReadValue() * 0.1f;
                var pad = Gamepad.current;
                if (pad != null) v += pad.rightStick.ReadValue() * (60f * Time.deltaTime);
                return v;
#else
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
            }
        }

        /// <summary>ズーム入力 (ホイール 1 ノッチ ≒ 1)</summary>
        public static float Zoom
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                float z = 0f;
                var mouse = Mouse.current;
                if (mouse != null) z += mouse.scroll.ReadValue().y / 120f;
                var pad = Gamepad.current;
                if (pad != null)
                {
                    if (pad.dpad.up.isPressed) z += 4f * Time.deltaTime;
                    if (pad.dpad.down.isPressed) z -= 4f * Time.deltaTime;
                }
                return z;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        public static bool JumpPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                    || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
#else
                return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0);
#endif
            }
        }

        public static bool Sprint
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
                    || (Gamepad.current != null && Gamepad.current.buttonEast.isPressed);
#else
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton1);
#endif
            }
        }

        /// <summary>カーソル解放 (Esc)</summary>
        public static bool ReleaseCursorPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        /// <summary>カーソル再ロック (左クリック)</summary>
        public static bool LockCursorPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }

        /// <summary>操作説明の表示切り替え (F1)</summary>
        public static bool HelpPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.F1);
#endif
            }
        }
    }
}
