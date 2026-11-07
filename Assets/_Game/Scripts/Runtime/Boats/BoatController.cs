using System;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.Boats
{
    /// <summary>
    /// Vật lý thuyền kiểu arcade nhưng dựa trên lực thật:
    /// bốn điểm phao đẩy thuyền nổi lên, nước chảy kéo thuyền xuôi dòng, chèo đẩy thuyền tới,
    /// nước cản theo chiều dọc và chiều ngang (chiều ngang cản mạnh nên thuyền ít trượt khi cua).
    /// Thuyền bị lật, kẹt hoặc lên bờ quá lâu thì tự đưa về giữa dòng gần nhất.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BoatController : MonoBehaviour
    {
        public BoatDefinition definition;

        [Header("Thân thuyền")]
        public float hullLength = 4f;
        public float hullBeam = 1f;
        public float hullDepth = 0.5f;
        [Tooltip("Phần hình thuyền, nghiêng nhẹ khi bẻ lái cho có cảm giác")]
        public Transform model;

        [Header("Gỡ kẹt")]
        public float flipResetTime = 2f;
        public float stuckResetTime = 3.5f;
        public float landResetTime = 2.5f;

        /// <summary>Gọi khi thuyền đâm vào thứ gì đó, kèm độ mạnh cú đâm (m/s).</summary>
        public event Action<float> Impact;
        /// <summary>Gọi khi thuyền được đưa về giữa dòng.</summary>
        public event Action Respawned;

        public RiverTrack Track { get; set; }
        public IBoatInput Input { get; set; }
        public WaterInfo Water => water;
        /// <summary>Tốc độ theo mặt phẳng ngang, mét mỗi giây.</summary>
        public float Speed { get; private set; }

        public float FloatHeight
        {
            get
            {
                // Gốc toạ độ của thân thuyền là đường nước thiết kế, đáy nằm ở -hullDepth.
                // Thuyền thật ăn nước khoảng 55–60% phần thân dưới gốc; lòng thuyền không bị nước tràn
                // vì có nắp che nước vô hình ở mép mạn (DryInteriorWaterMask).
                string id=definition!=null?definition.name:string.Empty;
                if(id=="BeTre")return 0.03f;      // cây tre chìm gần nửa
                if(id=="PhaoCaoSu")return 0.04f;  // ống phao chìm khoảng một phần ba
                if(id=="ThuyenThung")return hullDepth*0.45f;
                return hullDepth*0.4f;
            }
        }

        const float FloatRestDepth = 0.18f;

        Rigidbody rb;
        Vector3[] floats;
        WaterInfo water;
        int riverHint = -1;
        float stunTimer, flipTimer, stuckTimer, landTimer, lean;
        bool resetRequested;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = new Vector3(0f, -hullDepth * 0.4f, 0f);
            if (Input == null)
                Input = GetComponent<IBoatInput>();
            if (Track == null)
                Track = FindAnyObjectByType<RiverTrack>();

            // Bốn điểm phao ở bốn góc thân thuyền, ngang mực nước mong muốn
            float x = hullBeam * 0.35f, z = hullLength * 0.35f;
            float floatY=-(FloatRestDepth+FloatHeight);
            floats = new[]
            {
                new Vector3(-x, floatY, z), new Vector3(x, floatY, z),
                new Vector3(-x, floatY, -z), new Vector3(x, floatY, -z),
            };
        }

        void Update()
        {
            if (Input != null && Input.ConsumeReset())
                resetRequested = true;
        }

        void FixedUpdate()
        {
            if (rb.isKinematic || Track == null || definition == null)
                return;

            float dt = Time.fixedDeltaTime;
            water = Track.SampleWater(rb.worldCenterOfMass, ref riverHint);
            float g = -Physics.gravity.y;

            int wetPoints = 0;
            foreach (var local in floats)
            {
                Vector3 p = transform.TransformPoint(local);
                float depth = water.height - p.y;
                if (!water.inWater || depth <= 0f)
                    continue;

                wetPoints++;
                // Chìm càng sâu đẩy càng mạnh, có giảm chấn để thuyền không nảy tưng tưng
                float lift = Mathf.Min(depth / FloatRestDepth, 3f) * g;
                float damping = rb.GetPointVelocity(p).y * 6f;
                rb.AddForceAtPosition(Vector3.up * ((lift - damping) * rb.mass * 0.25f), p, ForceMode.Force);
            }

            Vector3 flatVel = rb.linearVelocity;
            flatVel.y = 0f;
            Speed = flatVel.magnitude;

            float throttle = Input != null ? Mathf.Clamp(Input.Throttle, -1f, 1f) : 0f;
            float steer = Input != null ? Mathf.Clamp(Input.Steer, -1f, 1f) : 0f;

            if (wetPoints > 0)
                ApplyWaterForces(wetPoints / (float)floats.Length, throttle, steer, dt);

            UpdateTimers(throttle, dt);
        }

        void ApplyWaterForces(float wet, float throttle, float steer, float dt)
        {
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f)
                return;
            fwd.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            // Tính theo vận tốc so với dòng nước, nên thả tay thì thuyền trôi theo nước
            Vector3 rel = rb.linearVelocity - water.flow;
            rel.y = 0f;
            float fwdSpeed = Vector3.Dot(rel, fwd);
            float sideSpeed = Vector3.Dot(rel, right);

            if (stunTimer > 0f)
                throttle *= 0.2f;

            float maxSpeed = definition.MaxSpeed;
            float accel = definition.Acceleration;
            float push = throttle >= 0f ? throttle * accel : throttle * accel * 0.6f;
            // Lực cản tỉ lệ với tốc độ, chọn sao cho chèo hết sức thì dừng ở đúng tốc độ tối đa
            float drag = accel / maxSpeed;
            Vector3 force = fwd * (push - fwdSpeed * drag) - right * (sideSpeed * definition.Grip);
            rb.AddForce(force * wet, ForceMode.Acceleration);

            // Bẻ lái: đặt tốc độ quay mong muốn rồi kéo dần về đó. Đi chậm vẫn quay được, đi lùi thì đảo chiều lái
            float speedNorm = Mathf.Clamp01(Mathf.Abs(fwdSpeed) / maxSpeed);
            float direction = fwdSpeed < -0.5f ? -1f : 1f;
            float targetYaw = steer * definition.TurnRate * Mathf.Deg2Rad * Mathf.Lerp(0.55f, 1f, speedNorm) * direction;
            rb.AddTorque(Vector3.up * ((targetYaw - rb.angularVelocity.y) * 5f * wet), ForceMode.Acceleration);

            // Tự dựng thẳng lại khi bị nghiêng
            Vector3 righting = Vector3.Cross(transform.up, Vector3.up);
            rb.AddTorque(righting * (definition.Stability * wet), ForceMode.Acceleration);

            lean = Mathf.Lerp(lean, -steer * speedNorm * 10f, 1f - Mathf.Exp(-4f * dt));
        }

        void UpdateTimers(float throttle, float dt)
        {
            stunTimer = Mathf.Max(0f, stunTimer - dt);
            flipTimer = transform.up.y < 0.3f ? flipTimer + dt : 0f;
            stuckTimer = Mathf.Abs(throttle) > 0.3f && Speed < 0.6f ? stuckTimer + dt : 0f;
            landTimer = water.inWater ? 0f : landTimer + dt;

            if (resetRequested || flipTimer > flipResetTime || stuckTimer > stuckResetTime || landTimer > landResetTime)
                Respawn();
        }

        void LateUpdate()
        {
            if (model == null)
                return;
            // Dập dềnh nhẹ theo sóng: vài dao động chậm lệch pha nhau để không đều như máy.
            // Chỉ xoay phần hình, vật lý vẫn tính trên thân cứng.
            float t = Time.time + bobSeed;
            float k = 1f + Mathf.Clamp01(Speed / 8f) * 0.8f;
            float pitch = (Mathf.Sin(t * 1.13f) * 0.55f + Mathf.Sin(t * 2.31f + 1.7f) * 0.25f) * k;
            float roll = (Mathf.Sin(t * 1.37f + 0.6f) * 0.8f + Mathf.Sin(t * 2.9f + 2.2f) * 0.3f) * k;
            float heave = (Mathf.Sin(t * 1.21f + 0.3f) * 0.012f + Mathf.Sin(t * 2.6f) * 0.005f) * k;
            model.localRotation = Quaternion.Euler(pitch, 0f, lean + roll);
            model.localPosition = new Vector3(0f, heave, 0f);
        }

        float bobSeed;
        void OnEnable() { bobSeed = UnityEngine.Random.Range(0f, 100f); }

        void OnCollisionEnter(Collision collision)
        {
            if (definition == null || rb.isKinematic)
                return;

            Vector3 rel = collision.relativeVelocity;
            rel.y = 0f;
            float impact = rel.magnitude;
            if (impact < 2f)
                return;

            // Thuyền bền thì giữ được nhiều tốc độ hơn và chèo lại được sớm hơn
            Vector3 v = rb.linearVelocity;
            float keep = definition.CollisionKeep;
            rb.linearVelocity = new Vector3(v.x * keep, v.y, v.z * keep);
            stunTimer = definition.StunTime;
            Impact?.Invoke(impact);
        }

        /// <summary>Đưa thuyền về giữa dòng gần nhất, quay mũi xuôi dòng.</summary>
        public void Respawn()
        {
            if (Track == null)
                return;
            var w = Track.SampleWater(transform.position, ref riverHint);
            Vector3 pos = w.center;
            pos.y = w.height + FloatHeight + 0.03f;
            Place(pos, Quaternion.LookRotation(w.tangent, Vector3.up));
            Respawned?.Invoke();
        }

        /// <summary>Đặt thuyền đứng yên tại một vị trí.</summary>
        public void Place(Vector3 position, Quaternion rotation)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = position;
            rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            riverHint = -1;
            stunTimer = flipTimer = stuckTimer = landTimer = lean = 0f;
            resetRequested = false;
        }
    }
}
