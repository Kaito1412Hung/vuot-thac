using UnityEngine;

namespace VuotThac.Boats
{
    /// <summary>
    /// Camera bám sau thuyền, hơi trễ một nhịp cho có cảm giác tốc độ, nhìn về phía trước mũi thuyền.
    /// Khi không có mục tiêu thì camera đứng yên để màn khác (ví dụ màn chọn thuyền) điều khiển.
    /// </summary>
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

        /// <summary>Gắn camera vào thuyền, khoảng cách tự chỉnh theo chiều dài thuyền.</summary>
        public void Follow(Transform boat, float boatLength)
        {
            target = boat;
            distance = Mathf.Max(7f, boatLength * 1.5f + 3f);
            height = 2.8f + boatLength * 0.2f;
            smoothedForward = Flat(boat.forward);
            transform.position = Desired();
            transform.LookAt(LookPoint());
        }

        void LateUpdate()
        {
            if (target == null)
                return;

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
