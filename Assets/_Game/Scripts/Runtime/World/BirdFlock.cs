using UnityEngine;

namespace VuotThac.World
{
    /// <summary>
    /// Đàn chim bay vòng tròn quanh một điểm và vỗ cánh. Mỗi con chim là một con trực tiếp của GameObject này,
    /// có hai con tên "WingL" và "WingR".
    /// </summary>
    public class BirdFlock : MonoBehaviour
    {
        public float radius = 25f;
        [Tooltip("Tốc độ bay vòng, radian mỗi giây. Âm là bay ngược chiều")]
        public float angularSpeed = 0.25f;
        public float heightWave = 3f;
        public float flapSpeed = 9f;
        public float flapAngle = 35f;

        Transform[] birds;
        Transform[] wingsL;
        Transform[] wingsR;

        void Start()
        {
            int n = transform.childCount;
            birds = new Transform[n];
            wingsL = new Transform[n];
            wingsR = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                birds[i] = transform.GetChild(i);
                wingsL[i] = birds[i].Find("WingL");
                wingsR[i] = birds[i].Find("WingR");
            }
        }

        void Update()
        {
            float t = Time.time;
            float dirSign = Mathf.Sign(angularSpeed);
            for (int i = 0; i < birds.Length; i++)
            {
                float a = t * angularSpeed + i * 0.45f;
                float r = radius * (0.8f + 0.2f * Mathf.Sin(i * 1.7f + t * 0.3f));
                birds[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(t * 0.7f + i) * heightWave + (i % 3) * 1.5f, Mathf.Sin(a) * r);
                var forward = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * dirSign;
                birds[i].localRotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 0f, -12f * dirSign);

                // Thỉnh thoảng lượn, không vỗ cánh
                float glide = Mathf.Clamp01(Mathf.Sin(t * 0.5f + i * 2.1f) * 3f);
                float flap = Mathf.Sin(t * flapSpeed + i) * flapAngle * glide;
                if (wingsL[i] != null) wingsL[i].localRotation = Quaternion.Euler(0f, 0f, -flap);
                if (wingsR[i] != null) wingsR[i].localRotation = Quaternion.Euler(0f, 0f, flap);
            }
        }
    }
}
