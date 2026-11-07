using System;
using UnityEngine;

namespace VuotThac.Track
{
    /// <summary>
    /// Một nhánh sông rẽ ra khỏi sông chính rồi nhập lại ở phía sau, tạo thêm một đường đi cho người chơi.
    /// Đường tắt cắt thẳng qua phía trong khúc cua nên ngắn hơn. Đường vòng phình ra xa nên dài hơn.
    /// Hình dạng nhánh được tính ra từ sông chính, không cần vẽ spline riêng.
    /// Mọi tọa độ ở đây là tọa độ trước khi chèn dải mở rộng lòng sông (xem RiverTrack.BeforeRaceInsertion).
    /// </summary>
    [Serializable]
    public class RiverBranch
    {
        public string name = "Nhánh sông";
        [Tooltip("Rẽ khỏi sông chính ở mét này, tính từ đầu sông")]
        public float fromDistance = 200f;
        [Tooltip("Nhập lại sông chính ở mét này")]
        public float toDistance = 380f;
        [Tooltip("-1 là bờ trái, 1 là bờ phải khi nhìn xuôi dòng")]
        public int side = -1;
        [Min(4f)] public float width = 9f;
        [Min(0.5f)] public float depth = 1.3f;
        [Tooltip("0 là cắt thẳng qua khúc cua (đường tắt). Lớn hơn 0 là số mét nhánh phình ra xa sông chính (đường vòng)")]
        [Min(0f)] public float bulge;
        [Tooltip("Nhánh uốn lượn qua lại bao nhiêu mét")]
        public float wiggle = 5f;
        [Tooltip("Dải đất tối thiểu giữa nhánh và sông chính, tính từ chân bờ")]
        [Min(2f)] public float minLandGap = 8f;
        [Tooltip("Số tảng đá giữa dòng trên mỗi 100 m nhánh")]
        [Min(0f)] public float rocksPer100m = 6f;
        [Tooltip("Nước trong nhánh chảy nhanh gấp bao nhiêu lần sông chính. Đường tắt có ghềnh nên chảy xiết hơn")]
        [Min(0f)] public float flowMultiplier = 1f;
        [Tooltip("Màu biển báo ở ngã rẽ: 0 vàng (đường tắt), 1 xanh (đường vòng)")]
        public int signColor;

        /// <summary>Tâm nhánh lấy mẫu mỗi mét, trục y là độ cao mặt nước.</summary>
        [NonSerialized] public Vector3[] points;
        [NonSerialized] public Vector3[] tangents;
        [NonSerialized] public float[] distances;

        public float HalfWidth => width * 0.5f;
        public bool IsBuilt => points != null && points.Length > 1;
        public float Length => IsBuilt ? distances[distances.Length - 1] : 0f;
        /// <summary>Đường tắt hay đường vòng: so chiều dài nhánh với đoạn sông chính mà nó thay thế.</summary>
        public float LengthDifference => Length - (toDistance - fromDistance);

        public void Invalidate() => points = null;

        /// <summary>Điểm trên tâm nhánh gần <paramref name="worldPos"/> nhất, chỉ xét mặt phẳng ngang.</summary>
        public RiverSample GetNearest(Vector3 worldPos)
        {
            int best = 0;
            float bestT = 0f, bestSqr = float.MaxValue;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                float abx = b.x - a.x, abz = b.z - a.z;
                float apx = worldPos.x - a.x, apz = worldPos.z - a.z;
                float lenSqr = abx * abx + abz * abz;
                float t = lenSqr > 1e-8f ? Mathf.Clamp01((apx * abx + apz * abz) / lenSqr) : 0f;
                float dx = apx - abx * t, dz = apz - abz * t;
                float sqr = dx * dx + dz * dz;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                    bestT = t;
                }
            }

            var s = Sample(best, bestT);
            Vector3 offset = worldPos - s.center;
            offset.y = 0f;
            s.lateral = Vector3.Dot(offset, s.right);
            s.horizontalDistance = Mathf.Sqrt(bestSqr);
            return s;
        }

        /// <summary>Điểm trên tâm nhánh cách chỗ rẽ <paramref name="distance"/> mét.</summary>
        public RiverSample EvaluateAtDistance(float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int lo = 0, hi = distances.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (distances[mid] <= distance) lo = mid;
                else hi = mid;
            }
            float seg = distances[hi] - distances[lo];
            return Sample(lo, seg > 1e-6f ? (distance - distances[lo]) / seg : 0f);
        }

        RiverSample Sample(int index, float t)
        {
            int next = Mathf.Min(index + 1, points.Length - 1);
            Vector3 tangent = Vector3.Slerp(tangents[index], tangents[next], t).normalized;
            return new RiverSample
            {
                center = Vector3.Lerp(points[index], points[next], t),
                tangent = tangent,
                right = Vector3.Cross(Vector3.up, tangent).normalized,
                distance = Mathf.Lerp(distances[index], distances[next], t),
            };
        }

        /// <summary>
        /// Tính hình dạng nhánh từ sông chính. Nếu nhánh đi quá sát sông chính thì đẩy dần ra xa
        /// cho tới khi giữa hai dòng còn đủ <see cref="minLandGap"/> mét đất.
        /// </summary>
        public void Build(RiverTrack main)
        {
            var s0 = main.EvaluateAtDistance(fromDistance);
            var s1 = main.EvaluateAtDistance(toDistance);
            // Hai đầu nhánh nằm hẳn trong lòng sông chính, sát bờ, để nước hai dòng nối liền
            float edge = main.HalfWidth - 0.5f;
            Vector3 p0 = s0.center + s0.right * side * edge;
            Vector3 p1 = s1.center + s1.right * side * edge;

            // Hướng "ra xa sông chính" cho đường tắt: từ đoạn giữa sông chính về phía giữa đường nối hai đầu
            Vector3 chordMid = (p0 + p1) * 0.5f;
            Vector3 arcMid = main.EvaluateAtDistance((fromDistance + toDistance) * 0.5f).center;
            Vector3 away = chordMid - arcMid;
            away.y = 0f;
            if (away.sqrMagnitude < 1f)
                away = main.EvaluateAtDistance((fromDistance + toDistance) * 0.5f).right * side;
            away.Normalize();

            float span = toDistance - fromDistance;
            int count = Mathf.Max(30, Mathf.CeilToInt((span + bulge * 2f) / 1f));
            float required = main.HalfWidth + main.bankWidth + minLandGap + HalfWidth + main.bankWidth;

            Vector3[] pts = null;
            for (float extra = 0f; extra <= 120f; extra += 3f)
            {
                pts = Shape(main, p0, p1, away, s0.WaterHeight, s1.WaterHeight, extra, count);
                float minDist = float.MaxValue;
                for (int i = count / 5; i <= count * 4 / 5; i++)
                    minDist = Mathf.Min(minDist, main.GetNearest(pts[i]).horizontalDistance);
                if (minDist >= required)
                    break;
            }

            points = pts;
            tangents = new Vector3[count];
            distances = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector3 a = points[Mathf.Max(0, i - 1)], b = points[Mathf.Min(count - 1, i + 1)];
                Vector3 tan = b - a;
                tan.y = 0f;
                tangents[i] = tan.sqrMagnitude > 1e-6f ? tan.normalized : Vector3.forward;
                Vector3 step = i == 0 ? Vector3.zero : points[i] - points[i - 1];
                step.y = 0f;
                distances[i] = i == 0 ? 0f : distances[i - 1] + step.magnitude;
            }
        }

        Vector3[] Shape(RiverTrack main, Vector3 p0, Vector3 p1, Vector3 away, float water0, float water1, float extra, int count)
        {
            var pts = new Vector3[count];
            float edge = main.HalfWidth - 0.5f;
            for (int i = 0; i < count; i++)
            {
                float u = i / (float)(count - 1);
                float arch = Mathf.Sin(u * Mathf.PI);
                float sway = Mathf.Sin(u * Mathf.PI * 3f) * arch * wiggle;
                Vector3 p;
                if (bulge <= 0f)
                {
                    // Đường tắt: đi thẳng từ chỗ rẽ tới chỗ nhập, cong nhẹ ra xa sông chính nếu cần
                    Vector3 dir = p1 - p0;
                    dir.y = 0f;
                    Vector3 across = Vector3.Cross(Vector3.up, dir.normalized);
                    p = Vector3.Lerp(p0, p1, u) + away * (extra * arch) + across * sway;
                }
                else
                {
                    // Đường vòng: bám theo dáng sông chính nhưng phình ra xa ở giữa
                    var s = main.EvaluateAtDistance(Mathf.Lerp(fromDistance, toDistance, u));
                    float lateral = edge + (bulge + extra) * arch + sway;
                    p = s.center + s.right * side * lateral;
                }
                // Mặt nước nhánh thấp hơn sông chính một chút để hai mặt nước chồng nhau không bị nhấp nháy
                p.y = Mathf.Lerp(water0, water1, u) - 0.03f;
                pts[i] = p;
            }
            return pts;
        }
    }
}
