using UnityEngine;
using UnityEngine.InputSystem;

namespace VuotThac.Boats
{
    /// <summary>
    /// Camera bám sau thuyền, hơi trễ một nhịp cho có cảm giác tốc độ, nhìn về phía trước mũi thuyền.
    /// Khi không có mục tiêu thì camera đứng yên để màn khác (ví dụ màn chọn thuyền) điều khiển.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class BoatCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 9f;
        public float height = 3.6f;
        public float lookAhead = 6f;
        [Tooltip("Càng lớn camera bám càng sát, càng nhỏ càng trễ")]
        public float followSharpness = 5f;
        public float turnSharpness = 3f;

        Vector3 smoothedForward = Vector3.forward;
        public bool firstPerson;
        BoatRower rower;
        float yaw,pitch;
        Camera viewCamera;
        float followFov;
        float followNear = 0.3f;
        const float FirstPersonNear = 0.04f;
        public void ToggleView(){firstPerson=!firstPerson;yaw=0;pitch=firstPerson?13:0;}

        /// <summary>Gắn camera vào thuyền, khoảng cách tự chỉnh theo chiều dài thuyền.</summary>
        public void Follow(Transform boat, float boatLength)
        {
            target = boat;
            rower=boat.GetComponentInChildren<BoatRower>();yaw=0;pitch=firstPerson?13:0;
            if(viewCamera==null){viewCamera=GetComponent<Camera>();if(viewCamera!=null){followFov=viewCamera.fieldOfView;followNear=viewCamera.nearClipPlane;}}
            distance = Mathf.Max(7f, boatLength * 1.5f + 3f);
            height = 2.8f + boatLength * 0.2f;
            smoothedForward = Flat(boat.forward);
            transform.position = Desired();
            transform.LookAt(LookPoint());
        }

        void LateUpdate()
        {
            if (target == null)
            {
                if(viewCamera!=null){viewCamera.fieldOfView=followFov;viewCamera.nearClipPlane=followNear;}
                return;
            }

            // Phím V đổi giữa góc nhìn sau lưng và góc nhìn thứ nhất
            if(Keyboard.current!=null&&Keyboard.current.vKey.wasPressedThisFrame)ToggleView();
            if(Gamepad.current!=null&&Gamepad.current.rightStickButton.wasPressedThisFrame)ToggleView();
            if(firstPerson&&rower!=null&&rower.eye!=null)
            {
                rower.SetFirstPerson(true);
                // Góc nhìn thứ nhất cần mặt phẳng cắt gần rất sát để không cắt mất tay và mái chèo
                if(viewCamera!=null){viewCamera.fieldOfView=82;viewCamera.nearClipPlane=FirstPersonNear;}
                Vector2 look=Vector2.zero;
                if(Mouse.current!=null&&Mouse.current.rightButton.isPressed)look=Mouse.current.delta.ReadValue()*0.10f;
                if(Gamepad.current!=null)look+=Gamepad.current.rightStick.ReadValue()*Time.deltaTime*90;
                yaw=Mathf.Clamp(yaw+look.x,-110,110);pitch=Mathf.Clamp(pitch-look.y,-55,55);
                transform.position=rower.eye.position;
                var forward=Flat(target.forward);
                transform.rotation=Quaternion.LookRotation(forward,Vector3.up)*Quaternion.Euler(pitch,yaw,0);
                return;
            }

            if(rower!=null)rower.SetFirstPerson(false);
            // Góc nhìn sau lưng trả lại mặt phẳng cắt gần như cũ, tránh mặt nước và mặt đất ở xa bị nhấp nháy
            if(viewCamera!=null){viewCamera.fieldOfView=followFov;viewCamera.nearClipPlane=followNear;}

            float dt = Time.deltaTime;
            smoothedForward = Vector3.Slerp(smoothedForward, Flat(target.forward), 1f - Mathf.Exp(-turnSharpness * dt));
            transform.position = Vector3.Lerp(transform.position, Desired(), 1f - Mathf.Exp(-followSharpness * dt));
            transform.LookAt(LookPoint());
        }

        Vector3 Desired() => target.position - smoothedForward * distance + Vector3.up * height;
        Vector3 LookPoint() => target.position + smoothedForward * lookAhead + Vector3.up;

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }
    }
}
