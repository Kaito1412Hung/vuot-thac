using UnityEngine;

namespace VuotThac.World
{
    public class RuralVillager : MonoBehaviour
    {
        public enum Work { ChopWood, CarryFirewood, TendGarden }
        public Work work;
        public Transform torso, head;
        public Transform upperArmL, lowerArmL, upperArmR, lowerArmR;
        public Transform thighL, shinL, thighR, shinR, bootL, bootR;
        public Transform tool, splitLogL, splitLogR;
        public Vector3 routeA, routeB;
        public float phase;
        public float choppingSurfaceHeight = 0.89f;
        public LineRenderer wateringStream;
        Vector3 torsoRest;
        float clock, routeTime;

        void Start()
        {
            torsoRest = torso.localPosition;
            clock = phase;
        }

        public void PreviewPose(float time)
        {
            torsoRest = torso.localPosition;
            clock = time;
            if (work == Work.ChopWood) Chop();
            else if (work == Work.TendGarden) Garden();
            else Carry();
        }

        void Update()
        {
            clock += Time.deltaTime;
            if (work == Work.CarryFirewood) Carry();
            else if (work == Work.ChopWood) Chop();
            else Garden();
        }

        void Carry()
        {
            float length = Mathf.Max(1, Vector3.Distance(routeA, routeB));
            float duration = length / 0.7f;
            routeTime += Time.deltaTime;
            float time = Mathf.Repeat(routeTime + phase, duration * 2 + 4);
            bool reverse = time > duration + 2;
            float leg = reverse ? time - duration - 2 : time;
            bool walking = leg < duration;
            float t = Mathf.Clamp01(leg / duration);
            var p = reverse ? Vector3.Lerp(routeB, routeA, t) : Vector3.Lerp(routeA, routeB, t);
            if (Physics.Raycast(p + Vector3.up * 2, Vector3.down, out var hit, 6, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            transform.position = p;
            var direction = reverse ? routeA - routeB : routeB - routeA;
            direction.y = 0;
            if (walking && direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 4);
            float stride = walking ? Mathf.Sin(leg * 6.5f) : 0;
            float lift = walking ? Mathf.Cos(leg * 6.5f) : 0;
            PlaceFoot(thighL, shinL, bootL, -1, stride * 0.14f, Mathf.Max(0, lift) * 0.075f);
            PlaceFoot(thighR, shinR, bootR, 1, -stride * 0.14f, Mathf.Max(0, -lift) * 0.075f);
            torso.localPosition = torsoRest + Vector3.up * (walking ? Mathf.Abs(stride) * 0.018f : 0);
            torso.localRotation = Quaternion.Euler(4, 0, walking ? stride * 1.5f : 0);
            Reach(upperArmL, lowerArmL, transform.TransformPoint(new Vector3(-0.17f, 1.08f, 0.30f)), -1);
            Reach(upperArmR, lowerArmR, transform.TransformPoint(new Vector3(0.17f, 1.08f, 0.30f)), 1);
            head.localRotation = Quaternion.Euler(-3, Mathf.Sin(clock * 0.4f) * 8, 0);
        }

        void PlaceFoot(Transform thigh, Transform shin, Transform boot, float side, float stride, float lift)
        {
            var target = transform.TransformPoint(new Vector3(side * 0.105f, 0.08f + lift, stride));
            if (Physics.Raycast(target + Vector3.up, Vector3.down, out var hit, 3, ~0, QueryTriggerInteraction.Ignore))
                target.y = hit.point.y + 0.065f + lift;
            var delta = target - thigh.position;
            const float a = 0.39f, b = 0.37f;
            float distance = Mathf.Clamp(delta.magnitude, 0.15f, a + b - 0.003f);
            var direction = delta.normalized;
            var bend = Vector3.ProjectOnPlane(transform.forward, direction).normalized;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            var knee = thigh.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            thigh.rotation = Quaternion.FromToRotation(Vector3.down, knee - thigh.position);
            shin.rotation = Quaternion.FromToRotation(Vector3.down, target - shin.position);
            if (boot != null) boot.rotation = transform.rotation * Quaternion.Euler(-lift * 100, 0, 0);
        }

        void Chop()
        {
            float t = Mathf.Repeat(clock, 3.7f);
            float swing;
            if (t < 1.5f) swing = 1 - Mathf.SmoothStep(0, 1, t / 1.5f);
            else if (t < 1.85f) swing = Mathf.SmoothStep(0, 1, (t - 1.5f) / 0.35f);
            else swing = 1;
            torso.localRotation = Quaternion.Euler(Mathf.Lerp(-4, 12, swing), 0, 0);
            head.localRotation = Quaternion.Euler(Mathf.Lerp(-5, 18, swing), 0, 0);
            var hand = Vector3.Lerp(new Vector3(0.02f, 1.73f, 0.1f), new Vector3(0.02f, choppingSurfaceHeight + 0.40f, 0.32f), swing);
            tool.localPosition = hand;
            tool.localRotation = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(new Vector3(0, 0.98f, -0.12f), new Vector3(0, -0.65f, 0.76f), swing));
            Reach(upperArmR, lowerArmR, tool.position, 1);
            Reach(upperArmL, lowerArmL, tool.TransformPoint(Vector3.up * 0.12f), -1);
            float split = t > 1.8f ? Mathf.SmoothStep(0, 1, (t - 1.8f) * 4) : 0;
            if (splitLogL != null) splitLogL.localRotation = Quaternion.Euler(0, 0, split * 16);
            if (splitLogR != null) splitLogR.localRotation = Quaternion.Euler(0, 0, -split * 16);
        }

        void Garden()
        {
            torso.localRotation = Quaternion.Euler(10 + Mathf.Sin(clock) * 2, 0, 0);
            head.localRotation = Quaternion.Euler(20, Mathf.Sin(clock * 0.6f) * 5, 0);
            tool.localPosition = new Vector3(0.12f, 1.03f, 0.40f);
            tool.localRotation = Quaternion.Euler(0, 0, 18 + (Mathf.Sin(clock * 0.8f) + 1) * 12);
            Reach(upperArmR, lowerArmR, tool.TransformPoint(new Vector3(0.13f, 0.16f, 0)), 1);
            Reach(upperArmL, lowerArmL, tool.TransformPoint(new Vector3(-0.10f, 0.13f, 0)), -1);
            if (wateringStream != null)
            {
                wateringStream.enabled = Mathf.Sin(clock * 0.8f) > -0.2f;
                var nozzle = tool.TransformPoint(new Vector3(-0.38f, 0.04f, 0));
                wateringStream.SetPosition(0, nozzle);
                wateringStream.SetPosition(1, nozzle + Vector3.down * 0.65f + transform.right * Mathf.Sin(clock * 9) * 0.008f);
            }
        }

        void Reach(Transform upper, Transform lower, Vector3 target, float side)
        {
            const float a = 0.30f, b = 0.27f;
            Vector3 from = upper.position;
            Vector3 delta = target - from;
            target -= delta.normalized * 0.055f; // Target the wrist, leaving room for the palm and curled fingers.
            delta = target - from;
            float length = Mathf.Clamp(delta.magnitude, 0.07f, a + b - 0.002f);
            Vector3 direction = delta.normalized;
            Vector3 guide = transform.TransformDirection(new Vector3(side, -0.15f, -0.1f));
            Vector3 bend = Vector3.ProjectOnPlane(guide, direction).normalized;
            float along = (a * a - b * b + length * length) / (2 * length);
            Vector3 elbow = from + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(Vector3.down, elbow - from);
            lower.rotation = Quaternion.FromToRotation(Vector3.down, target - lower.position);
        }
    }
}
