using UnityEngine;

namespace VuotThac.World
{
    public class FarmHen : MonoBehaviour
    {
        public Transform neck;
        public float phase;
        void Update()
        {
            float t = Mathf.Repeat(Time.time + phase, 5.3f);
            float peck = t < 0.3f ? Mathf.SmoothStep(0, 1, t / 0.3f) : t < 0.65f ? 1 - Mathf.SmoothStep(0, 1, (t - 0.3f) / 0.35f) : 0;
            neck.localRotation = Quaternion.Euler(peck * 34 * Mathf.Sign(neck.localPosition.z), Mathf.Sin(Time.time * 0.7f + phase) * 8 * (1 - peck), 0);
        }
    }
}
