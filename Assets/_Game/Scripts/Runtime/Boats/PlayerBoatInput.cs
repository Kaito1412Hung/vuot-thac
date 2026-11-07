using UnityEngine;
using UnityEngine.InputSystem;

namespace VuotThac.Boats
{
    /// <summary>
    /// Điều khiển thuyền bằng bàn phím hoặc tay cầm. Nghiêng máy và nút cảm ứng trên điện thoại sẽ thêm sau.
    /// W/S hoặc mũi tên lên/xuống để chèo tới/lùi, A/D hoặc mũi tên trái/phải để bẻ lái, R để về giữa dòng.
    /// </summary>
    public class PlayerBoatInput : MonoBehaviour, IBoatInput
    {
        InputAction steer, throttle, reset;
        bool resetPending;

        public float Steer => steer.ReadValue<float>();
        public float Throttle => throttle.ReadValue<float>();

        public bool ConsumeReset()
        {
            bool r = resetPending;
            resetPending = false;
            return r;
        }

        void Awake()
        {
            steer = new InputAction("Steer", InputActionType.Value);
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            steer.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            steer.AddBinding("<Gamepad>/leftStick/x");

            throttle = new InputAction("Throttle", InputActionType.Value);
            throttle.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/s").With("Positive", "<Keyboard>/w");
            throttle.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            throttle.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/leftTrigger").With("Positive", "<Gamepad>/rightTrigger");

            reset = new InputAction("Reset", InputActionType.Button, "<Keyboard>/r");
            reset.AddBinding("<Gamepad>/buttonNorth");
        }

        void OnEnable()
        {
            steer.Enable();
            throttle.Enable();
            reset.Enable();
        }

        void OnDisable()
        {
            steer.Disable();
            throttle.Disable();
            reset.Disable();
        }

        void OnDestroy()
        {
            steer.Dispose();
            throttle.Dispose();
            reset.Dispose();
        }

        void Update()
        {
            if (reset.WasPressedThisFrame())
                resetPending = true;
        }
    }
}
