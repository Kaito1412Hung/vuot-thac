using UnityEngine;

namespace VuotThac.World
{
    // Humanoid skinned character. Locomotion uses the supplied rigged walk;
    // two-bone reaching poses the real arm bones for the farming tools.
    public class RealisticVillager : MonoBehaviour
    {
        public RuralVillager.Work work;
        public string displayName;
        public bool isWoman;
        public Animator animator;
        public Transform model, tool, chest, head;
        public Transform upperL, lowerL, handL, upperR, lowerR, handR;
        public Transform thighL, shinL, footL, thighR, shinR, footR;
        public Transform splitLogL, splitLogR;
        public LineRenderer wateringStream;
        public Vector3 routeA, routeB;
        public float phase, choppingSurfaceHeight = 0.89f, ankleHeight = 0.23f;
        public Transform[] bones;
        public Quaternion[] restRotations;
        public Vector3[] restPositions;
        public Quaternion footRestL, footRestR;
        float clock, routeTime;
        bool walking;

        public void CaptureRig()
        {
            chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            upperL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm); lowerL = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm); handL = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            upperR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm); lowerR = animator.GetBoneTransform(HumanBodyBones.RightLowerArm); handR = animator.GetBoneTransform(HumanBodyBones.RightHand);
            thighL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg); shinL = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg); footL = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            thighR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg); shinR = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg); footR = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            bones = model.GetComponentsInChildren<Transform>(); restRotations = new Quaternion[bones.Length]; restPositions = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++) { restRotations[i] = bones[i].localRotation; restPositions[i] = bones[i].localPosition; }
            footRestL = Quaternion.Inverse(transform.rotation) * footL.rotation;
            footRestR = Quaternion.Inverse(transform.rotation) * footR.rotation;
            ankleHeight = ((footL.position.y + footR.position.y) * 0.5f) - transform.position.y;
        }

        void Start()
        {
            clock = phase;
            if (bones == null || bones.Length == 0) CaptureRig();
            animator.applyRootMotion = false;
            animator.enabled = work == RuralVillager.Work.CarryFirewood;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void Update()
        {
            clock += Time.deltaTime;
            if (work != RuralVillager.Work.CarryFirewood) return;
            float duration = Mathf.Max(1, Vector3.Distance(routeA, routeB)) / 0.66f;
            routeTime += Time.deltaTime;
            float t = Mathf.Repeat(routeTime + phase, duration * 2 + 4);
            bool back = t > duration + 2;
            float leg = back ? t - duration - 2 : t;
            walking = leg < duration;
            Vector3 p = back ? Vector3.Lerp(routeB, routeA, Mathf.Clamp01(leg / duration)) : Vector3.Lerp(routeA, routeB, Mathf.Clamp01(leg / duration));
            if (Physics.Raycast(p + Vector3.up * 2, Vector3.down, out var hit, 6, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            transform.position = p;
            Vector3 dir = back ? routeA - routeB : routeB - routeA; dir.y = 0;
            if (walking && dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 4);
            animator.speed = walking ? 0.76f : 0;
        }

        void LateUpdate() => Pose(clock);

        public void PreviewPose(float time) => Pose(time);

        void Restore()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] == null) continue;
                bones[i].localRotation = restRotations[i]; bones[i].localPosition = restPositions[i];
            }
        }

        void Pose(float time)
        {
            if (head == null) return;
            if (work != RuralVillager.Work.CarryFirewood || !Application.isPlaying) Restore();
            if (work != RuralVillager.Work.CarryFirewood || !walking)
            {
                StandLeg(thighL, shinL, footL, -1, footRestL);
                StandLeg(thighR, shinR, footR, 1, footRestR);
            }
            if (work == RuralVillager.Work.CarryFirewood)
            {
                tool.localPosition = new Vector3(0, 1.07f, 0.31f);
                if (!Application.isPlaying)
                {
                    Reach(upperL, lowerL, handL, transform.TransformPoint(new Vector3(-0.18f, 1.04f, 0.31f)), -1);
                    Reach(upperR, lowerR, handR, transform.TransformPoint(new Vector3(0.18f, 1.04f, 0.31f)), 1);
                }
            }
            else if (work == RuralVillager.Work.ChopWood)
            {
                float t = Mathf.Repeat(time, 3.7f);
                float swing = t < 1.5f ? 1 - Mathf.SmoothStep(0, 1, t / 1.5f) : t < 1.85f ? Mathf.SmoothStep(0, 1, (t - 1.5f) / 0.35f) : 1;
                chest.rotation = Quaternion.AngleAxis(Mathf.Lerp(-5, 13, swing), transform.right) * chest.rotation;
                head.rotation = Quaternion.AngleAxis(Mathf.Lerp(-2, 14, swing), transform.right) * head.rotation;
                tool.localPosition = Vector3.Lerp(new Vector3(0.02f, 1.69f, 0.12f), new Vector3(0.02f, choppingSurfaceHeight + 0.40f, 0.32f), swing);
                tool.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(new Vector3(0, 0.98f, -0.12f), new Vector3(0, -0.65f, 0.76f), swing));
                Reach(upperR, lowerR, handR, tool.position, 1);
                Reach(upperL, lowerL, handL, tool.TransformPoint(Vector3.up * 0.10f), -1);
                float split = t > 1.8f ? Mathf.SmoothStep(0, 1, (t - 1.8f) * 4) : 0;
                if (splitLogL != null) splitLogL.localRotation = Quaternion.Euler(0, 0, split * 16);
                if (splitLogR != null) splitLogR.localRotation = Quaternion.Euler(0, 0, -split * 16);
            }
            else
            {
                chest.rotation = Quaternion.AngleAxis(9 + Mathf.Sin(time) * 2, transform.right) * chest.rotation;
                head.rotation = Quaternion.AngleAxis(15, transform.right) * head.rotation;
                tool.localPosition = new Vector3(0.12f, 1.03f, 0.40f);
                tool.localRotation = Quaternion.Euler(0, 0, 18 + (Mathf.Sin(time * 0.8f) + 1) * 12);
                Reach(upperR, lowerR, handR, tool.TransformPoint(new Vector3(0.13f, 0.16f, 0)), 1);
                Reach(upperL, lowerL, handL, tool.TransformPoint(new Vector3(-0.10f, 0.13f, 0)), -1);
                if (wateringStream != null)
                {
                    wateringStream.enabled = Mathf.Sin(time * 0.8f) > -0.2f;
                    Vector3 nozzle = tool.TransformPoint(new Vector3(-0.38f, 0.04f, 0));
                    wateringStream.SetPosition(0, nozzle); wateringStream.SetPosition(1, nozzle + Vector3.down * 0.65f);
                }
            }
        }

        public void ApplyAnimatorIK()
        {
            if (work != RuralVillager.Work.CarryFirewood) return;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1);
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, transform.TransformPoint(new Vector3(-0.18f, 1.04f, 0.31f)));
            animator.SetIKPosition(AvatarIKGoal.RightHand, transform.TransformPoint(new Vector3(0.18f, 1.04f, 0.31f)));
            animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0.75f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0.75f);
            animator.SetIKHintPosition(AvatarIKHint.LeftElbow, transform.TransformPoint(new Vector3(-0.40f, 1.15f, 0.08f)));
            animator.SetIKHintPosition(AvatarIKHint.RightElbow, transform.TransformPoint(new Vector3(0.40f, 1.15f, 0.08f)));
        }

        void StandLeg(Transform upper, Transform lower, Transform foot, float side, Quaternion rest)
        {
            Vector3 goal = transform.TransformPoint(new Vector3(side * 0.105f, ankleHeight, 0.02f));
            if (Physics.Raycast(goal + Vector3.up, Vector3.down, out var hit, 3, ~0, QueryTriggerInteraction.Ignore)) goal.y = hit.point.y + ankleHeight;
            Solve(upper, lower, foot, goal, transform.forward);
            foot.rotation = transform.rotation * rest;
        }

        void Reach(Transform upper, Transform lower, Transform hand, Vector3 grip, float side)
        {
            Vector3 goal = grip - (grip - upper.position).normalized * 0.055f;
            Solve(upper, lower, hand, goal, transform.TransformDirection(new Vector3(side * 0.5f, -0.7f, -0.2f)));
        }

        static void Solve(Transform upper, Transform lower, Transform end, Vector3 goal, Vector3 guide)
        {
            float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, end.position);
            Vector3 delta = goal - upper.position;
            float d = Mathf.Clamp(delta.magnitude, 0.06f, a + b - 0.002f);
            Vector3 dir = delta.normalized, bend = Vector3.ProjectOnPlane(guide, dir).normalized;
            float along = (a * a - b * b + d * d) / (2 * d);
            Vector3 elbow = upper.position + dir * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, goal - lower.position) * lower.rotation;
        }
    }
}
