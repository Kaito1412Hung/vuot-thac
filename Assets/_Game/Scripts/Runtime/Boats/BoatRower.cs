using UnityEngine;

namespace VuotThac.Boats
{
    [DefaultExecutionOrder(100)]
    public class BoatRower : MonoBehaviour
    {
        public enum Stroke { None, Single, Double, Pole, Oar }
        public Animator rig;
        public Transform paddle,eye;
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
        float clock,effort;
        UnityEngine.Rendering.ShadowCastingMode[] bodyShadows;
        bool firstPersonView;
        public float GripError { get; private set; }

        public void Capture()
        {
            if(bones==null||rest==null||bones.Length!=rest.Length||bones.Length==0)
            {
                bones=rig.GetComponentsInChildren<Transform>();rest=new Quaternion[bones.Length];
                for(int i=0;i<bones.Length;i++)rest[i]=bones[i].localRotation;
            }
            hips=Bone(HumanBodyBones.Hips);spine=Bone(HumanBodyBones.Spine);chest=Bone(HumanBodyBones.Chest);head=Bone(HumanBodyBones.Head);
            if(bodyRenderers!=null){bodyShadows=new UnityEngine.Rendering.ShadowCastingMode[bodyRenderers.Length];for(int i=0;i<bodyRenderers.Length;i++)bodyShadows[i]=bodyRenderers[i].shadowCastingMode;}
            boat=GetComponentInParent<BoatController>();rig.enabled=false;
        }
        void Awake(){Capture();}
        void LateUpdate()
        {
            bool preview=boat!=null&&boat.GetComponent<Rigidbody>().isKinematic;
            float throttle=preview?previewEffort:boat!=null&&boat.Input!=null?boat.Input.Throttle:0;
            effort=Mathf.MoveTowards(effort,Mathf.Abs(throttle),Time.deltaTime*3);
            clock+=Time.deltaTime*Mathf.Lerp(0.3f,0.8f,effort)*(throttle<0?-1:1);
            Pose(clock+phaseOffset,effort);
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
            if(eye!=null)eye.position=head.position+transform.forward*(firstPersonView?-0.18f:0.08f)+transform.up*0.035f;
        }
        public void Pose(float phase,float amount)
        {
            if(bones==null)Capture();
            for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
            float cycle=Mathf.Repeat(phase,1);
            float activeSide=stroke==Stroke.Double?(cycle<0.5f?-1:1):side;
            float t=stroke==Stroke.Double?Mathf.Repeat(cycle*2,1):cycle;
            bool power=t<0.64f;
            float progress=power?Mathf.SmoothStep(0,1,t/0.64f):Mathf.SmoothStep(0,1,(t-0.64f)/0.36f);
            float reach=power?Mathf.Lerp(0.58f,-0.48f,progress):Mathf.Lerp(-0.48f,0.58f,progress);
            float recovery=(power?0:Mathf.Sin(progress*Mathf.PI))*amount;
            reach*=amount;
            float twist=activeSide*reach*20;
            if(spine!=null)spine.localRotation*=Quaternion.Euler((standing?8:12)*amount,twist,0);
            if(chest!=null)chest.localRotation*=Quaternion.Euler(reach*8,twist*0.35f,0);
            if(head!=null)head.localRotation*=Quaternion.Euler(-reach*5,-twist*0.25f,0);
            var hip=transform.InverseTransformPoint(hips.position);
            foreach(float legSide in new[]{-1f,1f})
            {
                bool left=legSide<0;
                var upper=Bone(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var lower=Bone(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=Bone(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var footGoal=standing?new Vector3(legSide*0.15f,0.08f,0.08f):hip+new Vector3(legSide*0.10f,-0.06f,0.62f);
                Solve(upper,lower,foot,transform.TransformPoint(footGoal),transform.up+transform.forward*0.25f);
            }
            Vector3 leftGrip,rightGrip;
            if(stroke==Stroke.None)
            {
                leftGrip=hip+new Vector3(-0.22f,0.3f,0.35f);rightGrip=hip+new Vector3(0.22f,0.3f,0.35f);
            }
            else if(stroke==Stroke.Double)
            {
                var center=hip+new Vector3(0,0.40f+recovery*0.13f,0.23f+reach*0.35f);
                var axis=new Vector3(1,-activeSide*0.86f*amount,reach*0.12f).normalized;
                paddle.localPosition=center;paddle.localRotation=Quaternion.FromToRotation(Vector3.up,axis);
                leftGrip=center-axis*0.34f;rightGrip=center+axis*0.34f;
            }
            else
            {
                float beam=boat!=null?boat.hullBeam:1;
                var top=hip+new Vector3(activeSide*0.08f,(stroke==Stroke.Pole?0.70f:0.64f)+recovery*0.06f,0.18f+reach*0.20f);
                var tip=hip+new Vector3(activeSide*Mathf.Max(0.72f,beam*0.5f+0.25f),-0.63f+recovery*0.65f,0.25f+reach*0.75f);
                if(stroke==Stroke.Pole)tip=hip+new Vector3(activeSide*0.75f,-2,reach);
                paddle.localPosition=(top+tip)*0.5f;paddle.localRotation=Quaternion.FromToRotation(Vector3.up,top-tip);
                paddle.localScale=new Vector3(1,(top-tip).magnitude/(stroke==Stroke.Pole?3.2f:1.6f),1);
                var high=Vector3.Lerp(top,tip,0.04f);var low=Vector3.Lerp(top,tip,stroke==Stroke.Pole?0.20f:0.28f);
                leftGrip=activeSide>0?high:low;rightGrip=activeSide>0?low:high;
            }
            GripError=0;
            Grip(true,transform.TransformPoint(leftGrip));Grip(false,transform.TransformPoint(rightGrip));
            UpdateEye();
        }
        void Grip(bool left,Vector3 point)
        {
            var upper=Bone(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=Bone(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            const float palmLength=0.075f;
            var shaft=paddle!=null?paddle.up:transform.right;
            var forward=Vector3.ProjectOnPlane(transform.forward,shaft).normalized;
            var normal=Vector3.Cross(forward,-shaft).normalized;
            Solve(upper,lower,hand,point-forward*palmLength,transform.right*(left?-1:1)-transform.up*0.55f);
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
