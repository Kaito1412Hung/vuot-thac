using UnityEngine;

namespace VuotThac.Boats
{
    [DefaultExecutionOrder(100)]
    public class BoatRower : MonoBehaviour
    {
        public enum Stroke { None, Single, Double, Pole, Oar }
        public Animator rig;
        public Transform paddle,eye;
        public Transform steeringWheel;
        float steeringAngle;
        public float SteeringAngle => steeringAngle;
        public Stroke stroke;
        public bool standing;
        public float side=1,phaseOffset;
        public float previewEffort=0.55f;
        public Renderer[] bodyRenderers;
        public SkinnedMeshRenderer firstPersonArms;
        [SerializeField,HideInInspector] Transform[] bones;
        [SerializeField,HideInInspector] Quaternion[] rest;
        BoatController boat;
        Transform hips,spine,chest,head;
        float clock,effort,strokeDrive;
        UnityEngine.Rendering.ShadowCastingMode[] bodyShadows;
        bool firstPersonView;
        public float GripError { get; private set; }
        [SerializeField,HideInInspector] Vector3 hipsRest;
        [SerializeField,HideInInspector] bool capturedHip;
        Quaternion leftFootRest,rightFootRest;
        Transform[] armBones;
        float[] armReach;
        float shaftLength=1.6f;
        public float ShaftLength => shaftLength;
        public bool AlternatesSides { get; private set; }

        public void Capture()
        {
            bool rebuild=bones==null||rest==null||bones.Length!=rest.Length||bones.Length==0;
            if(!rebuild)for(int i=0;i<bones.Length;i++)if(bones[i]==null){rebuild=true;break;}
            if(rebuild)
            {
                bones=rig.GetComponentsInChildren<Transform>();rest=new Quaternion[bones.Length];
                for(int i=0;i<bones.Length;i++)rest[i]=bones[i].localRotation;
            }
            hips=Bone(HumanBodyBones.Hips);spine=Bone(HumanBodyBones.Spine);chest=Bone(HumanBodyBones.Chest);head=Bone(HumanBodyBones.Head);
            for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
            if(!capturedHip){hipsRest=hips.localPosition;capturedHip=true;}
            hips.localPosition=hipsRest;
            leftFootRest=Quaternion.Inverse(transform.rotation)*Bone(HumanBodyBones.LeftFoot).rotation;
            rightFootRest=Quaternion.Inverse(transform.rotation)*Bone(HumanBodyBones.RightFoot).rotation;
            armBones=new[]{Bone(HumanBodyBones.LeftUpperArm),Bone(HumanBodyBones.LeftLowerArm),Bone(HumanBodyBones.LeftHand),Bone(HumanBodyBones.RightUpperArm),Bone(HumanBodyBones.RightLowerArm),Bone(HumanBodyBones.RightHand)};
            armReach=new float[2];
            for(int i=0;i<2;i++)armReach[i]=Vector3.Distance(armBones[i*3].position,armBones[i*3+1].position)+Vector3.Distance(armBones[i*3+1].position,armBones[i*3+2].position)-0.008f;
            if(paddle!=null)
            {
                var filter=paddle.GetComponent<MeshFilter>();
                if(filter!=null&&filter.sharedMesh!=null)
                    shaftLength=filter.sharedMesh.bounds.size.y-(stroke==Stroke.Double?0.44f:stroke==Stroke.Pole?0:0.22f);
                paddle.localScale=Vector3.one;
            }
            if(bodyRenderers!=null){bodyShadows=new UnityEngine.Rendering.ShadowCastingMode[bodyRenderers.Length];for(int i=0;i<bodyRenderers.Length;i++)bodyShadows[i]=bodyRenderers[i].shadowCastingMode;}
            boat=GetComponentInParent<BoatController>();rig.enabled=false;
            AlternatesSides=boat!=null&&stroke!=Stroke.None&&stroke!=Stroke.Double&&boat.GetComponentsInChildren<BoatRower>().Length==1;
        }
        void Awake(){Capture();}
        void LateUpdate()
        {
            float steer=boat!=null&&boat.Input!=null?boat.Input.Steer:0;
            steeringAngle=Mathf.MoveTowards(steeringAngle,-Mathf.Clamp(steer,-1,1)*55,Time.deltaTime*120);
            bool preview=boat!=null&&boat.Input==null&&boat.GetComponent<Rigidbody>().isKinematic;
            float throttle=preview?previewEffort:boat!=null&&boat.Input!=null?boat.Input.Throttle:0;
            strokeDrive=Mathf.MoveTowards(strokeDrive,throttle,Time.deltaTime*2.5f);
            effort=Mathf.MoveTowards(effort,Mathf.Abs(strokeDrive),Time.deltaTime*5);
            clock+=Time.deltaTime*0.8f*strokeDrive;
            Pose(clock+phaseOffset,effort);
            UpdateBladeWater();
        }

        // ---------- Mái chèo chạm nước ----------

        BoatWaterEffects fx;
        float bladeLow,bladeHigh,swirlTimer;
        bool bladeReady;
        readonly bool[] under=new bool[2];
        readonly Vector3[] lastEnd=new Vector3[2];
        readonly Vector3[] bladeEnds=new Vector3[2];

        void UpdateBladeWater()
        {
            if(paddle==null||stroke==Stroke.None)return;
            if(fx==null){fx=GetComponentInParent<BoatWaterEffects>();if(fx==null)return;}
            if(!bladeReady)
            {
                var mf=paddle.GetComponent<MeshFilter>();if(mf==null||mf.sharedMesh==null)return;
                bladeLow=mf.sharedMesh.bounds.min.y;bladeHigh=mf.sharedMesh.bounds.max.y;
            }
            float water=fx.WaterHeight,dt=Mathf.Max(Time.deltaTime,1e-4f);
            var ends=bladeEnds; ends[0]=paddle.TransformPoint(0,bladeLow,0); ends[1]=paddle.TransformPoint(0,bladeHigh,0);
            bool anyUnder=false;
            for(int k=0;k<2;k++)
            {
                bool now=ends[k].y<water;
                if(bladeReady&&now!=under[k])
                {
                    var other=ends[1-k];float u=Mathf.Approximately(other.y,ends[k].y)?1:Mathf.Clamp01((water-other.y)/(ends[k].y-other.y));
                    var point=Vector3.Lerp(other,ends[k],u);var velocity=(ends[k]-lastEnd[k])/dt;
                    // Lưỡi chèo cắm xuống thì bắn mạnh theo sức chèo, rút lên thì chỉ nhỏ giọt
                    fx.Splash(point,velocity,now?Mathf.Lerp(0.35f,1f,effort):0.25f);
                }
                under[k]=now;lastEnd[k]=ends[k];anyUnder|=now;
            }
            bladeReady=true;
            if(!anyUnder)return;
            swirlTimer-=dt;
            if(swirlTimer>0)return;
            swirlTimer=stroke==Stroke.Pole?0.3f:0.1f;
            int low=ends[0].y<ends[1].y?0:1;var top=ends[1-low];var tip=ends[low];
            float t=Mathf.Approximately(top.y,tip.y)?1:Mathf.Clamp01((water-top.y)/(tip.y-top.y));
            fx.Swirl(Vector3.Lerp(top,tip,t),Mathf.Max(0.2f,effort));
        }
        Transform Bone(HumanBodyBones bone)=>rig.GetBoneTransform(bone);
        public void SetFirstPerson(bool enabled)
        {
            firstPersonView=enabled;
            if(bodyRenderers!=null&&bodyShadows!=null)for(int i=0;i<bodyRenderers.Length;i++)
            {
                bodyRenderers[i].enabled=!enabled;
                bodyRenderers[i].shadowCastingMode=bodyShadows[i];
            }
            if(firstPersonArms!=null)firstPersonArms.enabled=enabled;
            UpdateEye();
        }
        void UpdateEye()
        {
            // Đặt đúng vị trí đôi mắt (trước tâm đầu khoảng 9 cm, cao hơn 7 cm). Trước đây mắt lùi ra sau gáy
            // để thấy nhiều tay hơn, nhưng làm tay và mái chèo ở gần phình to, méo.
            if(eye!=null)eye.position=head.position+transform.up*0.07f+transform.forward*0.09f;
        }
        public void Pose(float phase,float amount)
        {
            if(bones==null||armBones==null||hips==null)Capture();
            amount=Mathf.Clamp01(amount);
            if(steeringWheel!=null)amount=0;
            for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
            hips.localPosition=hipsRest;
            float cycle=Mathf.Repeat(phase,1);
            float activeSide=stroke==Stroke.Double?(cycle<0.5f?-1:1):AlternatesSides?
                ((Mathf.FloorToInt(phase)&1)==0?-1:1):side;
            float t=stroke==Stroke.Double?Mathf.Repeat(cycle*2,1):cycle;
            // Catch, pull, extraction, then an airborne return. Smooth joins prevent a snap at the loop.
            float reach,lift;
            if(t<0.12f){reach=1;lift=1-Mathf.SmoothStep(0,1,t/0.12f);}
            else if(t<0.60f){reach=Mathf.Lerp(1,-1,Mathf.SmoothStep(0,1,(t-0.12f)/0.48f));lift=0;}
            else if(t<0.74f){reach=-1;lift=Mathf.SmoothStep(0,1,(t-0.60f)/0.14f);}
            else {reach=Mathf.Lerp(-1,1,Mathf.SmoothStep(0,1,(t-0.74f)/0.26f));lift=1;}
            reach*=amount;
            lift=Mathf.Lerp(1,lift,amount);
            float wave=Mathf.Sin(cycle*Mathf.PI*2);
            float twist=stroke==Stroke.Double?-Mathf.Cos(cycle*Mathf.PI*2)*13*amount:activeSide*reach*11;
            if(AlternatesSides&&t>=.74f)twist=-activeSide*11*amount;
            if(spine!=null)spine.localRotation*=Quaternion.Euler((standing?3:5)*amount+reach*3,twist,0);
            if(chest!=null)chest.localRotation*=Quaternion.Euler(reach*2,twist*0.3f,0);
            if(head!=null)head.localRotation*=Quaternion.Euler(-reach*2,-twist*0.65f,0);
            if(steeringWheel!=null&&spine!=null)spine.localRotation*=Quaternion.Euler(3+Mathf.Abs(steeringAngle)/55*4,0,0);
            var hip=transform.InverseTransformPoint(hips.position);
            for(int legIndex=0;legIndex<2;legIndex++)
            {
                float legSide=legIndex==0?-1f:1f;
                bool left=legSide<0;
                var upper=Bone(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var lower=Bone(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=Bone(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                bool kayak=stroke==Stroke.Double;
                float floor=boat!=null?-boat.hullDepth+0.13f-transform.localPosition.y:-0.18f;
                string boatId=boat!=null&&boat.definition!=null?boat.definition.name:"";
                if(boatId=="ThuyenThung")floor=-0.16f-transform.localPosition.y;
                if(boatId=="PhaoCaoSu")floor=-0.04f-transform.localPosition.y;
                var footGoal=standing?new Vector3(legSide*0.15f,0.08f,0.10f):
                    new Vector3(hip.x+legSide*0.12f,Mathf.Max(floor,hip.y-(kayak?0.10f:0.23f)),hip.z+(kayak?0.68f:0.52f));
                Solve(upper,lower,foot,transform.TransformPoint(footGoal),transform.up*0.6f+transform.forward);
                foot.rotation=transform.rotation*(left?leftFootRest:rightFootRest);
            }
            Vector3 leftGrip,rightGrip;
            if(steeringWheel!=null)
            {
                steeringWheel.localRotation=Quaternion.AngleAxis(steeringAngle,Vector3.forward);
                leftGrip=transform.InverseTransformPoint(steeringWheel.TransformPoint(-.23f,0,0));
                rightGrip=transform.InverseTransformPoint(steeringWheel.TransformPoint(.23f,0,0));
            }
            else if(stroke==Stroke.None)
            {
                leftGrip=hip+new Vector3(-0.21f,0.32f,0.53f);rightGrip=hip+new Vector3(0.21f,0.32f,0.53f);
            }
            else if(stroke==Stroke.Double)
            {
                // Continuous roll AND yaw: the left and right blades trace alternating strokes, without flipping sides.
                float angle=cycle*Mathf.PI*2;
                var center=hip+new Vector3(-wave*0.16f*amount,0.39f,0.42f);
                var axis=new Vector3(1,wave*0.90f*amount,-Mathf.Cos(angle)*0.48f*amount).normalized;
                paddle.localScale=Vector3.one;
                paddle.localPosition=center;paddle.localRotation=Quaternion.FromToRotation(Vector3.up,axis);
                leftGrip=center-axis*0.32f;rightGrip=center+axis*0.32f;
            }
            else
            {
                SinglePose(hip,activeSide,reach,lift,out var top,out var direction);
                float transfer=0;
                if(AlternatesSides&&t>=.74f)
                {
                    // Lift first, then sweep the blade across in front of the paddler.
                    // A direct left/right flip would drag the shaft through the cockpit.
                    transfer=Mathf.SmoothStep(0,1,(t-.74f)/.26f);
                    SinglePose(hip,activeSide,-amount,1,out top,out direction);
                    SinglePose(hip,-activeSide,amount,1,out var nextTop,out var nextDirection);
                    float arc=Mathf.Sin(transfer*Mathf.PI);
                    top=Vector3.Lerp(top,nextTop,transfer);
                    direction=(Vector3.Lerp(direction,nextDirection,transfer)+Vector3.forward*(arc*1.4f)+Vector3.up*(arc*.7f)).normalized;
                }
                var tip=top+direction*shaftLength;
                paddle.localPosition=(top+tip)*0.5f;
                paddle.localRotation=Quaternion.FromToRotation(Vector3.up,-direction)*Quaternion.AngleAxis(lift*65,Vector3.up);
                paddle.localScale=Vector3.one;
                var high=top+direction*0.10f;var low=top+direction*(standing?0.50f:0.45f);
                leftGrip=activeSide>0?high:low;rightGrip=activeSide>0?low:high;
                if(transfer>0)
                {
                    var nextLeft=activeSide>0?low:high;var nextRight=activeSide>0?high:low;
                    float handArc=Mathf.Sin(transfer*Mathf.PI)*.08f;
                    leftGrip=Vector3.Lerp(leftGrip,nextLeft,transfer)-Vector3.right*handArc;
                    rightGrip=Vector3.Lerp(rightGrip,nextRight,transfer)+Vector3.right*handArc;
                }
            }
            if(paddle!=null&&stroke!=Stroke.None)FitReach(ref leftGrip,ref rightGrip);
            GripError=0;
            Grip(true,transform.TransformPoint(leftGrip));Grip(false,transform.TransformPoint(rightGrip));
            UpdateEye();
        }
        void SinglePose(Vector3 hip,float strokeSide,float reach,float lift,out Vector3 top,out Vector3 direction)
        {
            top=hip+new Vector3(strokeSide*.08f,(standing?.46f:.55f)+lift*.05f,(standing?.28f:.34f)+reach*.06f);
            float railX=(boat!=null?boat.hullBeam*.5f:.5f)-strokeSide*transform.localPosition.x;
            float railY=RailHeight()-transform.localPosition.y;
            float clearance=boat!=null&&boat.definition!=null&&boat.definition.name=="ThuyenThung"?.23f:.12f;
            float outward=Mathf.Max(standing?.44f:.95f,(railX+clearance-strokeSide*top.x)/Mathf.Max(.14f,top.y-railY));
            outward+=lift*(standing?1.5f:1.10f);
            direction=new Vector3(strokeSide*outward,-1,reach*.38f).normalized;
        }
        float RailHeight()
        {
            string id=boat!=null&&boat.definition!=null?boat.definition.name:"";
            switch(id){case "ThuyenThung":return 0.18f;case "PhaoCaoSu":return 0.26f;case "BeTre":return 0.16f;
                case "TamBan":return 0.32f;case "DocMoc":return 0.20f;case "ThuyenRong":return 0.25f;default:return 0.22f;}
        }
        void FitReach(ref Vector3 leftGrip,ref Vector3 rightGrip)
        {
            var forward=Vector3.ProjectOnPlane(transform.forward,paddle.up).normalized;
            for(int iteration=0;iteration<6;iteration++)for(int index=0;index<2;index++)
            {
                var upper=armBones[index*3];
                var target=transform.TransformPoint(index==0?leftGrip:rightGrip)-forward*0.075f;
                float maximum=armReach[index];
                var delta=target-upper.position;if(delta.magnitude<=maximum)continue;
                var shift=transform.InverseTransformVector(-delta.normalized*(delta.magnitude-maximum));
                leftGrip+=shift;rightGrip+=shift;paddle.localPosition+=shift;
            }
        }
        void Grip(bool left,Vector3 point)
        {
            int index=left?0:3;
            var upper=armBones[index];var lower=armBones[index+1];var hand=armBones[index+2];
            const float palmLength=0.075f;
            var shaft=steeringWheel!=null?steeringWheel.up:paddle!=null?paddle.up:transform.right;
            var forward=Vector3.ProjectOnPlane(transform.forward,shaft).normalized;
            var normal=Vector3.Cross(forward,-shaft).normalized;
            Solve(upper,lower,hand,point-forward*palmLength,transform.right*(left?-0.75f:0.75f)+transform.up*(stroke==Stroke.Double?0.2f:-0.2f)-transform.forward*0.2f);
            hand.rotation=Quaternion.LookRotation(forward,normal);
            GripError=Mathf.Max(GripError,Vector3.Distance(hand.position+forward*palmLength,point));

        }
        static void Solve(Transform upper,Transform lower,Transform end,Vector3 goal,Vector3 guide)
        {
            float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,end.position);
            var delta=goal-upper.position;float distance=Mathf.Clamp(delta.magnitude,0.04f,a+b-0.001f);
            var direction=delta.normalized;var bend=Vector3.ProjectOnPlane(guide,direction).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var joint=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,joint-upper.position)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(end.position-lower.position,goal-lower.position)*lower.rotation;
        }
    }
}
