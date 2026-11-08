using UnityEngine;
using UnityEngine.InputSystem;

namespace VuotThac.Boats
{
    /// <summary>
    /// Bàn phím, tay cầm, cảm ứng và nghiêng máy đã cân bằng tư thế cầm.
    /// W/S hoặc mũi tên lên/xuống để chèo tới/lùi, A/D hoặc mũi tên trái/phải để bẻ lái, R để về giữa dòng.
    /// </summary>
    public class PlayerBoatInput : MonoBehaviour, IBoatInput
    {
        InputAction steer, throttle, reset;
        bool resetPending;

        public bool TiltEnabled { get; private set; }
        public bool TiltAvailable => Accelerometer.current != null;
        public float TouchSteer { get; set; }
        public bool TouchSteeringActive { get; set; }
        ScreenOrientation lastOrientation;
        public float TouchThrottle { get; set; }
        float neutral, tilt;
        bool calibratePending;
        public float Steer => Mathf.Clamp(steer.ReadValue<float>() + (TouchSteeringActive || Mathf.Abs(TouchSteer) > 0.01f ? TouchSteer : (TiltEnabled ? tilt : 0)), -1, 1);
        public float Throttle => Mathf.Clamp(throttle.ReadValue<float>() + TouchThrottle, -1, 1);
        public static float TiltAxis(float acceleration, float center)
        {
            float v = Mathf.Clamp((acceleration - center) / 0.45f, -1, 1);
            return Mathf.Abs(v) <= 0.08f ? 0 : Mathf.Sign(v) * (Mathf.Abs(v) - 0.08f) / 0.92f;
        }
        public void RequestReset() => resetPending = true;
        public void Calibrate() { calibratePending = true; tilt = 0; }
        public void SetTilt(bool enabled)
        {
            TiltEnabled = enabled && TiltAvailable;
            if (TiltEnabled) { InputSystem.EnableDevice(Accelerometer.current); Calibrate(); }
            else tilt = 0;
            PlayerPrefs.SetInt("VuotThac.Tilt", TiltEnabled ? 1 : 0);
        }
        public void ReleaseTouch() { TouchSteer = TouchThrottle = tilt = 0; TouchSteeringActive = false; }

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
            SetTilt(Application.isMobilePlatform && PlayerPrefs.GetInt("VuotThac.Tilt", 1) != 0);
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
            ReleaseTouch();
            tilt = 0;
        }

        void OnDestroy()
        {
            steer.Dispose();
            throttle.Dispose();
            reset.Dispose();
        }

        void Update()
        {
            if (lastOrientation != Screen.orientation) { lastOrientation = Screen.orientation; Calibrate(); }
            if (TiltEnabled && Accelerometer.current != null)
            {
                var gravity = Accelerometer.current.acceleration.ReadValue();
                if (gravity.sqrMagnitude > 0.1f)
                {
                    if (calibratePending) { neutral = gravity.x; calibratePending = false; }
                    tilt = Mathf.Lerp(tilt, TiltAxis(gravity.x, neutral), 1 - Mathf.Exp(-10 * Time.unscaledDeltaTime));
                }
            }
            if (reset.WasPressedThisFrame())
                resetPending = true;
        }
        void OnApplicationFocus(bool focused) { if (!focused) ReleaseTouch(); else Calibrate(); }
        void OnApplicationPause(bool paused) { ReleaseTouch(); if (!paused) Calibrate(); }
    }
}
