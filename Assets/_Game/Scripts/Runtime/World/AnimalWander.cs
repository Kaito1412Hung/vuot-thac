using UnityEngine;

namespace VuotThac.World
{
    /// <summary>
    /// AI đơn giản cho thú rừng bằng máy trạng thái: Đứng yên → Đi dạo quanh chỗ ở → Bỏ chạy.
    /// Thú bỏ chạy khi camera tới gần. Khi có thuyền, camera bám theo thuyền nên thú sẽ chạy khi người chơi lướt qua.
    /// </summary>
    public class AnimalWander : MonoBehaviour
    {
        public enum Gait { Walk, Hop, Swim }
        enum State { Idle, Wander, Flee }

        public Gait gait = Gait.Walk;
        [Tooltip("Đi dạo trong vòng tròn bán kính này quanh chỗ ở")]
        [Min(0.5f)] public float wanderRadius = 6f;
        public float walkSpeed = 1.2f;
        public float fleeSpeed = 4.5f;
        [Tooltip("Camera tới gần hơn khoảng này thì bỏ chạy")]
        public float fleeDistance = 14f;
        public Vector2 idleTime = new(2f, 6f);

        [Header("Hoạt cảnh")]
        [Tooltip("Phần thân, dùng để nhún khi nhảy hoặc dập dềnh khi bơi")]
        public Transform body;
        [Tooltip("Thứ tự: trước trái, trước phải, sau trái, sau phải")]
        public Transform[] legs;
        public float legSwing = 28f;
        public float stepsPerMeter = 1.6f;
        public float hopHeight = 0.25f;
        [Tooltip("Độ cao mặt nước, chỉ dùng khi bơi")]
        public float waterHeight;

        State state;
        Vector3 home, target;
        float timer, stride;
        Quaternion[] legRest;
        Vector3 bodyRest, bodyScale;
        Quaternion bodyRotation;
        Transform threat;

        void Start()
        {
            home = transform.position;
            if (body != null)
            {
                bodyRest = body.localPosition;
                bodyScale = body.localScale;
                bodyRotation = body.localRotation;
            }
            if (legs != null)
            {
                legRest = new Quaternion[legs.Length];
                for (int i = 0; i < legs.Length; i++)
                    legRest[i] = legs[i].localRotation;
            }

            stride = Random.value * 10f;
            EnterIdle();
            timer *= Random.value;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (threat == null && Camera.main != null)
                threat = Camera.main.transform;

            if (threat != null && state != State.Flee)
            {
                Vector3 away = transform.position - threat.position;
                away.y = 0f;
                if (away.sqrMagnitude < fleeDistance * fleeDistance)
                    EnterFlee(away);
            }

            if (state == State.Idle)
            {
                timer -= dt;
                if (timer <= 0f)
                    EnterWander();
            }
            else
            {
                Move(dt);
            }

            Animate();
        }

        void EnterIdle()
        {
            state = State.Idle;
            timer = Random.Range(idleTime.x, idleTime.y);
        }

        void EnterWander()
        {
            Vector2 r = Random.insideUnitCircle * wanderRadius;
            target = home + new Vector3(r.x, 0f, r.y);
            state = State.Wander;
            timer = 15f;
        }

        void EnterFlee(Vector3 away)
        {
            away = away.sqrMagnitude > 0.01f ? away.normalized : transform.forward;
            target = transform.position + away * wanderRadius * 1.5f;

            // Không chạy quá xa chỗ ở, để thú không lao xuống sông
            Vector3 offset = target - home;
            offset.y = 0f;
            float max = wanderRadius * 1.5f;
            if (offset.magnitude > max)
                target = home + offset.normalized * max;

            state = State.Flee;
            timer = 4f;
        }

        void Move(float dt)
        {
            float speed = state == State.Flee ? fleeSpeed : walkSpeed;
            Vector3 to = target - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            timer -= dt;

            if (dist < 0.3f || timer <= 0f)
            {
                EnterIdle();
                return;
            }

            Vector3 dir = to / dist;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), dt * 6f);

            float step = Mathf.Min(dist, speed * dt);
            Vector3 p = transform.position + dir * step;
            p.y = GroundHeight(p);
            transform.position = p;
            stride += step * stepsPerMeter;
        }

        float GroundHeight(Vector3 p)
        {
            if (gait == Gait.Swim)
                return waterHeight;

            if (Physics.Raycast(p + Vector3.up * 4f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return transform.position.y;
        }

        void Animate()
        {
            bool moving = state != State.Idle;
            float swing = moving ? Mathf.Sin(stride * Mathf.PI * 2f) : 0f;

            if (legs != null && legRest != null)
            {
                for (int i = 0; i < legs.Length; i++)
                {
                    // Chân chéo nhau bước cùng nhịp: trước trái với sau phải, trước phải với sau trái
                    float sign = (i == 0 || i == 3) ? 1f : -1f;
                    legs[i].localRotation = legRest[i] * Quaternion.Euler(sign * swing * legSwing, 0f, 0f);
                }
            }

            if (body == null)
                return;

            Vector3 offset = Vector3.zero;
            if (gait == Gait.Hop && moving)
                offset.y = Mathf.Abs(Mathf.Sin(stride * Mathf.PI)) * hopHeight;
            else if (gait == Gait.Swim)
                offset.y = Mathf.Sin(Time.time * 2f + stride) * 0.03f;
            else if (gait == Gait.Walk && moving)
                offset.y = Mathf.Abs(swing) * 0.012f;
            body.localPosition = bodyRest + offset;
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1, 1 + Mathf.Sin(Time.time * 2.1f + home.x) * 0.006f, 1));
            body.localRotation = bodyRotation * Quaternion.Euler(0, 0, gait == Gait.Swim ? Mathf.Sin(Time.time * 1.4f + home.z) * 2 : 0);
        }
    }
}
