using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VuotThac.Track
{
    /// <summary>Kết quả tra cứu điểm gần nhất trên tâm dòng sông.</summary>
    public struct RiverSample
    {
        /// <summary>Điểm trên tâm sông, có độ cao đúng bằng mặt nước tại đó.</summary>
        public Vector3 center;
        /// <summary>Hướng nước chảy (đã chuẩn hóa).</summary>
        public Vector3 tangent;
        /// <summary>Hướng sang bờ phải khi nhìn xuôi dòng (nằm ngang).</summary>
        public Vector3 right;
        /// <summary>Quãng đường tính từ đầu sông đến điểm này, đơn vị mét.</summary>
        public float distance;
        /// <summary>Độ lệch khỏi tâm sông theo phương ngang. Dương là lệch sang phải.</summary>
        public float lateral;
        /// <summary>Khoảng cách theo mặt phẳng ngang tới tâm sông, luôn không âm.</summary>
        public float horizontalDistance;

        public float WaterHeight => center.y;
    }

    /// <summary>
    /// Dòng sông của một đường đua. Tâm sông là spline trên cùng GameObject.
    /// Component này giữ thông số lòng sông và trả lời các câu hỏi như
    /// "điểm này cách tâm sông bao xa" hay "mặt nước ở đây cao bao nhiêu".
    /// Địa hình, mặt nước và đồ trang trí được dựng từ đây bằng nút trong Inspector.
    /// </summary>
    [RequireComponent(typeof(SplineContainer))]
    public class RiverTrack : MonoBehaviour
    {
        [Header("Lòng sông")]
        [Tooltip("Bề rộng mặt nước, đơn vị mét")]
        [Min(4f)] public float width = 14f;
        [Tooltip("Phần mesh nước mở rộng mỗi bên; không thay đổi lòng sông hay địa hình")]
        [Min(0f)] public float waterSurfaceExtraWidth = 1.5f;
        [Tooltip("Dải diện tích mới chèn giữa hai bờ theo trục X; mỗi bên được tịnh tiến nguyên khối")]
        [Min(0f)] public float insertedRaceStripWidth;
        [HideInInspector] public float appliedRaceStripWidth;
        [Tooltip("Độ sâu ở giữa dòng")]
        [Min(0.5f)] public float depth = 1.6f;
        [Tooltip("Bề rộng dải bờ dốc từ mép nước lên mặt đất")]
        [Min(1f)] public float bankWidth = 4f;
        [Tooltip("Mặt đất ở mép bờ cao hơn mặt nước bao nhiêu")]
        [Min(0f)] public float bankHeight = 1.2f;

        [Header("Địa hình hai bên")]
        [Tooltip("Dựng địa hình tới cách tâm sông bao xa")]
        [Min(10f)] public float terrainMargin = 140f;
        [Tooltip("Kích thước một ô lưới địa hình. Nhỏ thì mịn hơn nhưng nặng máy hơn")]
        [Range(1f, 5f)] public float gridSize = 2.5f;
        [Min(0f)] public float hillHeight = 5f;
        [Min(0.001f)] public float hillNoiseScale = 0.025f;
        [Tooltip("Mỗi mét xa bờ thì đất cao thêm bao nhiêu, tạo cảm giác thung lũng")]
        [Min(0f)] public float outerRise = 0.12f;

        [Header("Đường đua")]
        [Tooltip("Đoạn sông trước vạch xuất phát, để người chơi không nhìn thấy đầu sông")]
        [Min(0f)] public float startOffset = 60f;
        [Tooltip("Đoạn sông sau vạch đích")]
        [Min(0f)] public float finishOffset = 60f;

        [Header("Dòng chảy")]
        [Tooltip("Tốc độ nước chảy ở giữa sông chính, mét mỗi giây. Sát bờ chảy chậm hơn")]
        [Min(0f)] public float flowSpeed = 2.5f;

        [Header("Nhánh sông")]
        [Tooltip("Các nhánh rẽ khỏi sông chính rồi nhập lại, tạo đường tắt hoặc đường vòng")]
        public System.Collections.Generic.List<RiverBranch> branches = new();

        [Header("Trang trí")]
        public int seed = 1412;
        [Tooltip("Số tảng đá giữa dòng trên mỗi 100 m đường đua")]
        [Min(0f)] public float rocksPer100m = 5f;
        [Tooltip("Số tảng đá trên bờ trên mỗi 100 m sông")]
        [Min(0f)] public float bankRocksPer100m = 6f;
        [Tooltip("Số cây trên mỗi 100 m sông, tính cả hai bờ")]
        [Min(0f)] public float treesPer100m = 45f;
        [Tooltip("Số cây rừng rậm trên mỗi 100 m sông, mọc thành từng mảng dày ở vùng đất xa bờ")]
        [Min(0f)] public float forestTreesPer100m = 180f;
        [Min(0f)] public float bushesPer100m = 35f;
        [Tooltip("Số khóm hoa trên mỗi 100 m sông")]
        [Min(0f)] public float flowersPer100m = 22f;
        [Tooltip("Số bụi sậy mọc ở mép nước trên mỗi 100 m sông")]
        [Min(0f)] public float reedsPer100m = 14f;
        [Tooltip("Số cụm lá súng sát bờ trên mỗi 100 m sông")]
        [Min(0f)] public float lilyPadsPer100m = 5f;
        [Min(0f)] public float logsPer100m = 3f;
        [Min(0f)] public float cloudsPer100m = 1.5f;

        [Header("Công trình và cảnh nền")]
        [Tooltip("Dãy núi bao quanh thung lũng, che chỗ địa hình kết thúc")]
        public bool mountains = true;
        [Tooltip("Núi cao nhất bao nhiêu mét so với mặt nước")]
        [Min(10f)] public float mountainHeight = 75f;
        [Tooltip("Dãy núi rộng bao nhiêu mét, tính từ mép địa hình ra ngoài")]
        [Min(30f)] public float mountainDepth = 150f;
        [Tooltip("Số cây trên sườn núi")]
        [Min(0)] public int mountainTrees = 350;
        [Tooltip("Làng nhỏ có cối xay nước ở quãng cuối đường đua và vài nhà gần đích")]
        public bool village = true;
        [Tooltip("Hươu, thỏ, cáo, gấu đi lại trong rừng, vịt bơi sát bờ và chim bay trên trời")]
        public bool animals = true;
        [Tooltip("Cầu gỗ bắc qua sông ở đoạn thẳng nhất quãng giữa đường đua")]
        public bool bridge = true;
        [Tooltip("Nhà gỗ và bến thuyền ngay sau vạch xuất phát")]
        public bool cabin = true;
        [Tooltip("Biển báo mũi tên đặt trước các khúc cua gắt")]
        public bool turnSigns = true;

        const float SampleStep = 1f;

        SplineContainer container;
        Vector3[] points;
        Vector3[] tangents;
        float[] distances;

        public SplineContainer Container =>
            container != null ? container : (container = GetComponent<SplineContainer>());

        public float HalfWidth => width * 0.5f;

        public float CenterXAtMapZ(float z)
        {
            EnsureCache();
            if(z<=points[0].z)return points[0].x;
            if(z>=points[points.Length-1].z)return points[points.Length-1].x;
            int low=0,high=points.Length-1;
            while(high-low>1){int mid=(low+high)/2;if(points[mid].z<z)low=mid;else high=mid;}
            return Mathf.Lerp(points[low].x,points[high].x,Mathf.InverseLerp(points[low].z,points[high].z,z));
        }

        public Vector3 BeforeRaceInsertion(Vector3 world)
        {
            if(appliedRaceStripWidth<=0)return world;
            float center=CenterXAtMapZ(world.z),delta=world.x-center;
            world.x=center+Mathf.Sign(delta)*Mathf.Max(0,Mathf.Abs(delta)-appliedRaceStripWidth*0.5f);
            return world;
        }

        /// <summary>
        /// Ngược lại với <see cref="BeforeRaceInsertion"/>: đổi một điểm tính theo spline gốc sang vị trí
        /// thật trong scene sau khi lòng sông đã được mở rộng.
        /// </summary>
        public Vector3 AfterRaceInsertion(Vector3 map)
        {
            if (appliedRaceStripWidth <= 0) return map;
            float delta = map.x - CenterXAtMapZ(map.z);
            if (Mathf.Abs(delta) < 0.001f) return map;
            map.x += Mathf.Sign(delta) * appliedRaceStripWidth * 0.5f;
            return map;
        }

        /// <summary>Tổng chiều dài sông, tính cả đoạn trước vạch xuất phát và sau vạch đích.</summary>
        public float Length
        {
            get
            {
                EnsureCache();
                return distances[distances.Length - 1];
            }
        }

        public float StartDistance => startOffset;
        public float FinishDistance => Mathf.Max(startOffset, Length - finishOffset);
        public float RaceLength => FinishDistance - StartDistance;

        void OnEnable() => Spline.Changed += OnSplineChanged;
        void OnDisable() => Spline.Changed -= OnSplineChanged;
        void OnValidate() => InvalidateCache();

        void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
        {
            if (Container != null && Container.Spline == spline)
                InvalidateCache();
        }

        /// <summary>Bỏ dữ liệu đã lấy mẫu, lần tra cứu sau sẽ lấy mẫu lại từ spline.</summary>
        public void InvalidateCache()
        {
            points = null;
            if (branches != null)
                foreach (var b in branches)
                    b?.Invalidate();
        }

        /// <summary>Các nhánh sông đã được tính hình dạng, sẵn sàng tra cứu.</summary>
        public System.Collections.Generic.IReadOnlyList<RiverBranch> Branches
        {
            get
            {
                EnsureBranches();
                return branches;
            }
        }

        void EnsureBranches()
        {
            EnsureCache();
            if (branches == null)
                return;
            foreach (var b in branches)
                if (b != null && !b.IsBuilt)
                    b.Build(this);
        }

        void EnsureCache()
        {
            if (points != null)
                return;

            var c = Container;
            float approxLength = Mathf.Max(1f, c.CalculateLength());
            int count = Mathf.Max(2, Mathf.CeilToInt(approxLength / SampleStep) + 1);

            points = new Vector3[count];
            tangents = new Vector3[count];
            distances = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                c.Evaluate(t, out float3 pos, out float3 tan, out _);
                points[i] = pos;

                Vector3 tangent = tan;
                tangent.y = 0f;
                tangents[i] = tangent.sqrMagnitude > 1e-6f
                    ? tangent.normalized
                    : (i > 0 ? tangents[i - 1] : Vector3.forward);

                distances[i] = i == 0 ? 0f : distances[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }
        }

        /// <summary>Tìm điểm trên tâm sông gần <paramref name="worldPos"/> nhất, chỉ xét mặt phẳng ngang.</summary>
        public RiverSample GetNearest(Vector3 worldPos)
        {
            EnsureCache();
            Scan(worldPos, 0, points.Length - 1, out int best, out float bestT, out float bestSqr);
            return BuildSample(best, bestT, worldPos, Mathf.Sqrt(bestSqr));
        }

        /// <summary>
        /// Giống <see cref="GetNearest(Vector3)"/> nhưng chỉ tìm quanh lần trước (<paramref name="hint"/>),
        /// nhanh hơn nhiều khi gọi liên tục mỗi khung hình cho một chiếc thuyền.
        /// </summary>
        public RiverSample GetNearest(Vector3 worldPos, ref int hint)
        {
            EnsureCache();
            int last = points.Length - 1;
            if (hint < 0 || hint >= last)
            {
                Scan(worldPos, 0, last, out hint, out float t0, out float s0);
                return BuildSample(hint, t0, worldPos, Mathf.Sqrt(s0));
            }

            const int window = 40;
            int lo = Mathf.Max(0, hint - window), hi = Mathf.Min(last, hint + window);
            Scan(worldPos, lo, hi, out int best, out float bestT, out float bestSqr);
            // Kết quả nằm sát mép cửa sổ nghĩa là thuyền đã đi xa, tìm lại toàn bộ cho chắc
            if ((best == lo && lo > 0) || (best == hi - 1 && hi < last))
                Scan(worldPos, 0, last, out best, out bestT, out bestSqr);
            hint = best;
            return BuildSample(best, bestT, worldPos, Mathf.Sqrt(bestSqr));
        }

        void Scan(Vector3 worldPos, int from, int to, out int best, out float bestT, out float bestSqr)
        {
            best = from;
            bestT = 0f;
            bestSqr = float.MaxValue;

            for (int i = from; i < to; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
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
        }

        /// <summary>
        /// Tra cứu mặt nước tại một vị trí thật trong scene: có đang ở trên nước không, mặt nước cao bao nhiêu,
        /// nước chảy hướng nào và nhanh bao nhiêu. Xét cả sông chính lẫn các nhánh.
        /// </summary>
        public WaterInfo SampleWater(Vector3 worldPos, ref int mainHint)
        {
            EnsureBranches();
            Vector3 p = BeforeRaceInsertion(worldPos);
            var s = GetNearest(p, ref mainHint);
            float edge = s.horizontalDistance - HalfWidth;
            var info = MakeWater(s, HalfWidth, flowSpeed, -1, edge);

            if (branches != null)
            {
                for (int i = 0; i < branches.Count; i++)
                {
                    var b = branches[i];
                    if (b == null || !b.IsBuilt)
                        continue;
                    var bs = b.GetNearest(p);
                    float be = bs.horizontalDistance - b.HalfWidth;
                    if (be < edge)
                    {
                        edge = be;
                        info = MakeWater(bs, b.HalfWidth, flowSpeed * b.flowMultiplier, i, be);
                    }
                }
            }
            return info;
        }

        WaterInfo MakeWater(RiverSample s, float half, float speed, int channel, float edge)
        {
            // Giữa dòng chảy nhanh, sát bờ chỉ còn 40%
            float lateral = Mathf.Clamp01(s.horizontalDistance / Mathf.Max(0.1f, half));
            bool inWater = edge < 0.3f;
            return new WaterInfo
            {
                inWater = inWater,
                height = s.WaterHeight,
                flow = inWater ? s.tangent * speed * (1f - 0.6f * lateral * lateral) : Vector3.zero,
                center = AfterRaceInsertion(s.center),
                tangent = s.tangent,
                channel = channel,
                edgeDistance = edge,
                distanceAlong = s.distance,
            };
        }

        /// <summary>Lấy điểm trên tâm sông cách đầu sông <paramref name="distance"/> mét.</summary>
        public RiverSample EvaluateAtDistance(float distance)
        {
            EnsureCache();
            distance = Mathf.Clamp(distance, 0f, distances[distances.Length - 1]);

            int lo = 0, hi = distances.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (distances[mid] <= distance) lo = mid;
                else hi = mid;
            }

            float seg = distances[hi] - distances[lo];
            float t = seg > 1e-6f ? (distance - distances[lo]) / seg : 0f;
            var s = BuildSample(lo, t, Vector3.zero, 0f);
            s.lateral = 0f;
            s.horizontalDistance = 0f;
            return s;
        }

        RiverSample BuildSample(int index, float t, Vector3 worldPos, float horizontalDistance)
        {
            int next = Mathf.Min(index + 1, points.Length - 1);
            Vector3 center = Vector3.Lerp(points[index], points[next], t);
            Vector3 tangent = Vector3.Slerp(tangents[index], tangents[next], t).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

            Vector3 offset = worldPos - center;
            offset.y = 0f;

            return new RiverSample
            {
                center = center,
                tangent = tangent,
                right = right,
                distance = Mathf.Lerp(distances[index], distances[next], t),
                lateral = Vector3.Dot(offset, right),
                horizontalDistance = horizontalDistance,
            };
        }

        /// <summary>
        /// Độ cao mặt đất tại vị trí ngang (x, z). Giữa dòng là lòng sông, ra xa là bờ dốc rồi đồi.
        /// </summary>
        public float TerrainHeight(Vector3 worldPos, out float distanceFromCenter)
        {
            var s = GetNearest(worldPos);
            distanceFromCenter = s.horizontalDistance;
            float height = TerrainHeightAt(worldPos, s.horizontalDistance, s.WaterHeight);
            return CarveBranches(worldPos, height, ref distanceFromCenter);
        }

        /// <summary>
        /// Đào lòng các nhánh sông vào địa hình. <paramref name="distanceFromCenter"/> được quy đổi để
        /// so được với <see cref="HalfWidth"/> của sông chính: giá trị bằng HalfWidth + x nghĩa là cách
        /// mép nước gần nhất (sông chính hoặc nhánh) x mét. Nhờ vậy mọi chỗ đặt đồ đang tránh sông chính
        /// cũng tự tránh luôn các nhánh.
        /// </summary>
        public float CarveBranches(Vector3 worldPos, float height, ref float distanceFromCenter)
        {
            if (branches == null || branches.Count == 0)
                return height;

            EnsureBranches();
            Vector3 p = BeforeRaceInsertion(worldPos);
            foreach (var b in branches)
            {
                if (b == null || !b.IsBuilt)
                    continue;

                var bs = b.GetNearest(p);
                float equivalent = bs.horizontalDistance - b.HalfWidth + HalfWidth;
                if (equivalent < distanceFromCenter)
                    distanceFromCenter = equivalent;

                // Ngoài vùng ảnh hưởng thì giữ nguyên địa hình, trong vùng thì hạ dần xuống thành thung lũng nhỏ
                float outer = b.HalfWidth + bankWidth + 30f;
                if (bs.horizontalDistance >= outer)
                    continue;
                float channel = ChannelProfile(p, bs.horizontalDistance, bs.WaterHeight, b.HalfWidth, b.depth);
                float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(outer, b.HalfWidth + bankWidth + 8f, bs.horizontalDistance));
                height = Mathf.Lerp(height, Mathf.Min(height, channel), w);
            }
            return height;
        }

        /// <summary>Độ cao mặt đất khi đã biết khoảng cách tới tâm sông và độ cao mặt nước gần nhất.</summary>
        MountainWaterfallLandform[] waterfallLandforms;
        public void RefreshWaterfallLandforms() => waterfallLandforms = GetComponents<MountainWaterfallLandform>();
        public float TerrainHeightAt(Vector3 worldPos, float d, float water)
        {
            if(appliedRaceStripWidth>0)
            {
                worldPos=BeforeRaceInsertion(worldPos);
                var originalSample=GetNearest(worldPos);d=originalSample.horizontalDistance;water=originalSample.WaterHeight;
            }
            float natural = UnmodifiedTerrainHeightAt(worldPos,d,water);
            if (waterfallLandforms == null) RefreshWaterfallLandforms();
            foreach(var profile in waterfallLandforms)
                if(profile != null) natural = profile.ShapeSurface(worldPos,natural);
            foreach(var cave in GetComponents<ForestCaveLandform>())
                natural=cave.ShapeSurface(worldPos,natural);
            return natural;
        }

        public float UnmodifiedTerrainHeightAt(Vector3 worldPos, float d, float water) =>
            ChannelProfile(worldPos, d, water, HalfWidth, depth);

        /// <summary>Mặt cắt ngang của một dòng nước: lòng sông, bờ dốc rồi đồi. Dùng chung cho sông chính và nhánh.</summary>
        public float ChannelProfile(Vector3 worldPos, float d, float water, float half, float depth)
        {
            if (d <= half)
            {
                float k = d / half;
                return water - depth * (1f - 0.35f * k * k);
            }

            float bankEnd = half + bankWidth;
            if (d <= bankEnd)
            {
                float f = (d - half) / bankWidth;
                f = f * f * (3f - 2f * f);
                return Mathf.Lerp(water - depth * 0.65f, water + bankHeight, f);
            }

            float e = d - bankEnd;
            float ramp = Mathf.Clamp01(e / 20f);
            float seedOffset = (seed % 1000) * 1.37f;
            // Hai lớp nhiễu: đồi lớn thoai thoải và gò nhỏ lấm tấm
            float big = Mathf.PerlinNoise(worldPos.x * hillNoiseScale + seedOffset, worldPos.z * hillNoiseScale + seedOffset);
            float small = Mathf.PerlinNoise(worldPos.x * hillNoiseScale * 3.1f + seedOffset * 2f, worldPos.z * hillNoiseScale * 3.1f);
            float noise = big * 0.75f + small * 0.25f;
            return water + bankHeight + ramp * (noise * hillHeight + e * outerRise);
        }

        /// <summary>Khung bao theo mặt phẳng ngang của tâm sông.</summary>
        public Bounds GetCenterlineBounds()
        {
            EnsureCache();
            var b = new Bounds(points[0], Vector3.zero);
            foreach (var p in points)
                b.Encapsulate(p);
            return b;
        }

        void OnDrawGizmosSelected()
        {
            DrawLine(StartDistance, Color.green);
            DrawLine(FinishDistance, Color.red);

            if (branches == null)
                return;
            EnsureBranches();
            Gizmos.color = Color.cyan;
            foreach (var b in branches)
            {
                if (b == null || !b.IsBuilt)
                    continue;
                // Nhánh được tính theo spline gốc, phải đổi sang vị trí thật sau khi lòng sông được mở rộng
                for (int i = 0; i < b.points.Length - 1; i += 2)
                    Gizmos.DrawLine(AfterRaceInsertion(b.points[i]) + Vector3.up * 0.5f,
                        AfterRaceInsertion(b.points[Mathf.Min(i + 2, b.points.Length - 1)]) + Vector3.up * 0.5f);
            }
        }

        void DrawLine(float distance, Color color)
        {
            var s = EvaluateAtDistance(distance);
            Gizmos.color = color;
            Vector3 lift = Vector3.up * 0.5f;
            Gizmos.DrawLine(s.center - s.right * HalfWidth + lift, s.center + s.right * HalfWidth + lift);
        }
    }
}
