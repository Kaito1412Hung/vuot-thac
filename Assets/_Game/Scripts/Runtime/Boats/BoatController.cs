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
        public string LastRecoveryReason { get; private set; }
        public bool RecoveryEnabled { get; set; }=true;
        public void ApplyRemotePresentation(float speed)
        {Speed=Mathf.Max(0,speed);if(Track!=null)water=Track.SampleWater(transform.position,ref riverHint);}

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
        readonly int[] floatHints={-1,-1,-1,-1};
        readonly Vector3[] waterPoints=new Vector3[4];
        readonly bool[] pointInWater=new bool[4];
        Vector3 waterUp=Vector3.up;
        WaterInfo water;
        int riverHint = -1;
        float stunTimer, flipTimer, stuckTimer, landTimer, lean;
        Vector3 bankContactNormal;
        float bankContactTime;
        bool resetRequested;
        BoatItemState itemState;
        Vector3 preImpactFlat;

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
            if(GetComponent<BoatInteriorWaterClip>()==null)gameObject.AddComponent<BoatInteriorWaterClip>();
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

            // Sample the bow and stern independently: one shared water height makes the boat bridge a fall horizontally.
            for(int i=0;i<floats.Length;i++)
            {
                var p=transform.TransformPoint(floats[i]);
                var sample=Track.SampleWater(p,ref floatHints[i]);
                waterPoints[i]=new Vector3(p.x,sample.height,p.z);
                // Keep the waterline continuous while a narrow hull brushes the sloping shore.
                pointInWater[i]=sample.inWater||(water.inWater&&sample.edgeDistance<.65f);
            }
            var along=(waterPoints[0]+waterPoints[1]-waterPoints[2]-waterPoints[3])*.5f;
            var across=(waterPoints[1]+waterPoints[3]-waterPoints[0]-waterPoints[2])*.5f;
            var normal=Vector3.Cross(along,across).normalized;
            if(normal.sqrMagnitude<.5f)normal=Vector3.up;
            if(normal.y<0)normal=-normal;
            normal=Vector3.RotateTowards(Vector3.up,normal,55*Mathf.Deg2Rad,0);
            waterUp=Vector3.Slerp(waterUp,normal,1-Mathf.Exp(-8*dt));
            Vector3 gradient=Vector3.zero;
            if(normal.y>.1f)gradient=new Vector3(-normal.x/normal.y,0,-normal.z/normal.y);
            float surfaceVerticalSpeed=Vector3.Dot(new Vector3(rb.linearVelocity.x,0,rb.linearVelocity.z),gradient);
            int wetPoints = 0;
            for(int i=0;i<floats.Length;i++)
            {
                Vector3 p=transform.TransformPoint(floats[i]);
                float depth=waterPoints[i].y-p.y;
                if(!pointInWater[i]||depth<=0)
                    continue;

                wetPoints++;
                // Chìm càng sâu đẩy càng mạnh, có giảm chấn để thuyền không nảy tưng tưng
                float lift = Mathf.Min(depth / FloatRestDepth, 6f) * g;
                float relativeVertical=rb.GetPointVelocity(p).y-surfaceVerticalSpeed;
                // Near-critical damping absorbs a landing without the squared-speed impulse
                // that used to throw a submerged boat back out of the water.
                float damping=relativeVertical<0?24f:14f;
                float support=Mathf.Clamp(lift-relativeVertical*damping,0,g*12);
                rb.AddForceAtPosition(Vector3.up*(support*rb.mass*.25f),p,ForceMode.Force);
            }

            Vector3 flatVel = rb.linearVelocity;
            flatVel.y = 0f;
            Speed = flatVel.magnitude;
            preImpactFlat=flatVel;
            if(itemState==null)itemState=GetComponent<BoatItemState>();

            float throttle = Input != null ? Mathf.Clamp(Input.Throttle, -1f, 1f) : 0f;
            float steer = Input != null ? Mathf.Clamp(Input.Steer, -1f, 1f) : 0f;

            // A bow touching the bank must not switch off the engine and rudder while the cockpit is afloat.
            if (wetPoints > 0 || water.inWater)
                ApplyWaterForces(Mathf.Max(wetPoints / (float)floats.Length,water.inWater?.75f:0f), throttle, steer, dt);

            ApplyShoreRecovery(throttle,dt);
            // A collision can drive the hull under even though its float points still report water.
            // Catch that downward motion in the water, rather than waiting for the stuck respawn timer.
            float immersion=water.height+FloatHeight-rb.position.y;
            if(water.inWater&&wetPoints<3&&immersion>.16f&&transform.up.y>.3f)
            {
                float downward=rb.linearVelocity.y-surfaceVerticalSpeed;
                float support=g+immersion*25-downward*8;
                rb.AddForce(Vector3.up*Mathf.Clamp(support,0,40),ForceMode.Acceleration);
            }

            UpdateTimers(throttle, dt);
        }

        void ApplyShoreRecovery(float throttle,float dt)
        {
            bankContactTime=Mathf.Max(0,bankContactTime-dt);
            if(!water.inWater&&water.edgeDistance>Mathf.Max(hullBeam,hullLength*.5f))return;
            var inward=Vector3.ProjectOnPlane(water.center-rb.position,Vector3.up);
            inward=Vector3.ProjectOnPlane(inward,water.tangent);
            inward.Normalize();
            if(bankContactTime>0&&Mathf.Abs(throttle)>.2f&&Speed<2)
            {
                var escape=Vector3.ProjectOnPlane(bankContactNormal,Vector3.up).normalized;
                if(escape.sqrMagnitude<.1f)escape=inward.sqrMagnitude>.1f?inward:-Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized*Mathf.Sign(throttle);
                var slide=Vector3.ProjectOnPlane(water.tangent,escape).normalized;
                if(slide.sqrMagnitude<.1f)slide=Vector3.Cross(Vector3.up,escape);
                escape=(escape+slide*.7f).normalized;
                // Use the real contact normal: the obstruction may be a rock on the water side.
                // Replacing that normal with 'toward the river' can pin the hull into the rock.
                rb.AddForce(escape*(6+Mathf.Abs(throttle)*8),ForceMode.Acceleration);
                // Keep deliberate reverse/steering available during a low speed bank contact.
                stunTimer=0;
            }
            if(inward.sqrMagnitude<.01f)return;
            var forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            float radius=Mathf.Abs(Vector3.Dot(forward,inward))*hullLength*.475f+
                Mathf.Abs(Vector3.Dot(transform.right,inward))*hullBeam*.475f;
            float margin=radius+.45f;
            float near=Mathf.Clamp01((water.edgeDistance+margin)/Mathf.Max(.5f,margin));
            if(near<=0)return;
            float outward=Mathf.Max(0,-Vector3.Dot(rb.linearVelocity,inward));
            rb.AddForce(inward*(near*4+outward*near*4),ForceMode.Acceleration);
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
            if(itemState!=null&&itemState.BoostRemaining>0){maxSpeed*=1.4f;accel*=1.75f;}
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
            Vector3 righting = Vector3.Cross(transform.up, waterUp);
            float restoring=definition.Stability*2.5f;
            float damping=2*Mathf.Sqrt(restoring);
            rb.AddTorque(righting*(restoring*wet)-new Vector3(rb.angularVelocity.x,0,rb.angularVelocity.z)*damping*wet,ForceMode.Acceleration);

            lean = Mathf.Lerp(lean, -steer * speedNorm * 10f, 1f - Mathf.Exp(-4f * dt));
        }

        void UpdateTimers(float throttle, float dt)
        {
            stunTimer = Mathf.Max(0f, stunTimer - dt);
            if(!RecoveryEnabled){resetRequested=false;return;}
            flipTimer = transform.up.y < 0.3f ? flipTimer + dt : 0f;
            // Turning on the spot is deliberate manoeuvring, not a stuck boat.
            bool stalled=Mathf.Abs(throttle)>.3f&&Speed<.6f&&Mathf.Abs(rb.angularVelocity.y)<.15f;
            stuckTimer = stalled ? stuckTimer+dt : Mathf.Max(0,stuckTimer-dt*2);
            landTimer = water.inWater ? 0f : landTimer + dt;

            if (resetRequested || flipTimer > flipResetTime || stuckTimer > stuckResetTime || landTimer > landResetTime)
            {
                LastRecoveryReason=resetRequested?"manual":flipTimer>flipResetTime?"capsized":stuckTimer>stuckResetTime?"stalled":"grounded";
                Respawn();
            }
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

            RememberBankContact(collision);
            Vector3 rel = collision.relativeVelocity;rel.y=0;
            float impact=0;Vector3 hitNormal=Vector3.zero;
            for(int i=0;i<collision.contactCount;i++)
            {
                var n=collision.GetContact(i).normal;if(Mathf.Abs(n.y)>.65f)continue;
                n.y=0;n.Normalize();float headOn=Mathf.Abs(Vector3.Dot(rel,n));
                if(headOn>impact){impact=headOn;hitNormal=n;}
            }
            if (impact < 2f)
                return;

            if(itemState!=null&&itemState.AbsorbImpact())
            {
                var bounced=Vector3.Dot(preImpactFlat,hitNormal)<0?Vector3.Reflect(preImpactFlat,hitNormal):preImpactFlat;
                rb.linearVelocity=new Vector3(bounced.x,rb.linearVelocity.y,bounced.z);return;
            }

            // Thuyền bền thì giữ được nhiều tốc độ hơn và chèo lại được sớm hơn
            Vector3 v = rb.linearVelocity;
            float keep = definition.CollisionKeep;
            // A glancing hit slides along the bank; preserve the velocity tangent to the wall.
            float into=Vector3.Dot(v,hitNormal);
            if(into<0)rb.linearVelocity=v-hitNormal*into*(1-keep);
            stunTimer = definition.StunTime;
            Impact?.Invoke(impact);
        }

        void OnCollisionStay(Collision collision){RememberBankContact(collision);}
        public void ApplyItemHit()
        {
            if(itemState==null)itemState=GetComponent<BoatItemState>();
            if(itemState!=null&&itemState.AbsorbImpact())return;
            var velocity=rb.linearVelocity;rb.linearVelocity=new Vector3(velocity.x*.45f,velocity.y,velocity.z*.45f);
            stunTimer=Mathf.Max(stunTimer,1.2f);Impact?.Invoke(3);
        }
        void RememberBankContact(Collision collision)
        {
            Vector3 normal=Vector3.zero;
            for(int i=0;i<collision.contactCount;i++)
            {
                var n=collision.GetContact(i).normal;
                if(Mathf.Abs(n.y)<.95f)normal+=Vector3.ProjectOnPlane(n,Vector3.up).normalized;
            }
            if(collision.contactCount>0){bankContactNormal=normal.normalized;bankContactTime=.15f;}
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
            for(int i=0;i<floatHints.Length;i++)floatHints[i]=-1;
            waterUp=Vector3.up;
            bankContactNormal=Vector3.zero;bankContactTime=0;
            stunTimer = flipTimer = stuckTimer = landTimer = lean = 0f;
            resetRequested = false;
        }
    }
}
