using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    /// <summary>Dựng các nhánh sông: mặt nước, đá, sậy, lá súng và biển báo ở ngã rẽ.</summary>
    public static partial class RiverTrackBuilder
    {
        /// <summary>
        /// Tính hình dạng các nhánh và cấm đặt đồ dọc theo chúng. Phải chạy trước khi dựng địa hình
        /// và trước mọi công trình, để nhà cửa và cây cối không mọc giữa lòng nhánh.
        /// </summary>
        static void PrepareBranches(BuildContext ctx)
        {
            var track = ctx.Track;
            var branches = track.Branches;
            if (branches == null || branches.Count == 0)
                return;

            for (int bi = 0; bi < branches.Count; bi++)
            {
                var b = branches[bi];
                if (b == null || !b.IsBuilt)
                    continue;

                float corridor = b.HalfWidth + track.bankWidth + 1.5f;
                for (float d = 0f; d <= b.Length; d += 4f)
                    ctx.Exclude(b.EvaluateAtDistance(d).center, corridor);
                // Cửa rẽ và cửa nhập: chừa trống cả một khoảng trên sông chính
                ctx.Exclude(b.points[0], 12f);
                ctx.Exclude(b.points[b.points.Length - 1], 12f);

                // Phần mở rộng lòng sông chia map làm hai theo trục x. Nhánh phải nằm trọn một bên,
                // nếu không mặt nước nhánh sẽ bị xé ra khi mở rộng.
                int left = 0, right = 0;
                foreach (var p in b.points)
                {
                    foreach (float o in new[] { -1f, 1f })
                    {
                        var s = b.GetNearest(p);
                        Vector3 q = p + s.right * o * (b.HalfWidth + track.waterSurfaceExtraWidth);
                        if (q.x < track.CenterXAtMapZ(q.z)) left++;
                        else right++;
                    }
                }
                if (left > 0 && right > 0)
                    Debug.LogWarning($"[Vượt Thác] Nhánh '{b.name}' có {Mathf.Min(left, right)} điểm nằm lệch sang bên kia sông chính. " +
                                     "Khi mở rộng lòng sông, đoạn đó có thể bị xé. Nên đẩy nhánh ra xa hơn.", track);

                Debug.Log($"[Vượt Thác] Nhánh '{b.name}': dài {b.Length:0} m, đoạn sông chính tương ứng dài {b.toDistance - b.fromDistance:0} m " +
                          $"({(b.LengthDifference < 0 ? "ngắn hơn" : "dài hơn")} {Mathf.Abs(b.LengthDifference):0} m).", track);
            }
        }

        /// <summary>
        /// Rút ngắn suối từ thác núi nếu trên đường chảy về sông chính nó gặp một nhánh sông.
        /// Khi đó suối đổ vào nhánh, không chạy tiếp băng qua mặt đảo nữa.
        /// </summary>
        static void EndCreekAtFirstBranch(BuildContext ctx, MountainWaterfallLandform profile)
        {
            var branches = ctx.Track.Branches;
            if (branches == null || branches.Count == 0)
                return;

            // Hình dạng suối phụ thuộc vào chiều dài của nó, nên tính lại vài lần cho khớp
            for (int pass = 0; pass < 3; pass++)
            {
                bool found = false;
                for (float z = -4f; z >= -profile.riverRun; z -= 0.5f)
                {
                    Vector3 p = profile.World(profile.CreekCenter(z), 0f, z);
                    foreach (var b in branches)
                    {
                        if (b == null || !b.IsBuilt)
                            continue;
                        var s = b.GetNearest(p);
                        if (s.horizontalDistance > b.HalfWidth + 0.5f)
                            continue;
                        // Kéo suối vào sâu trong nhánh thêm 2 m để hai mặt nước nối liền
                        profile.riverRun = -z + 2f;
                        profile.riverHeight = s.WaterHeight;
                        profile.poolHeight = s.WaterHeight;
                        found = true;
                        break;
                    }
                    if (found)
                        break;
                }
                if (!found)
                    return;
            }
        }

        /// <summary>Độ cao mặt nước gần nhất, tính cả sông chính lẫn các nhánh.</summary>
        static float NearestWaterHeight(BuildContext ctx, Vector3 p)
        {
            var track = ctx.Track;
            var main = track.GetNearest(p);
            float best = main.horizontalDistance - track.HalfWidth;
            float height = main.WaterHeight;
            var branches = track.Branches;
            if (branches != null)
                foreach (var b in branches)
                {
                    if (b == null || !b.IsBuilt)
                        continue;
                    var s = b.GetNearest(p);
                    float edge = s.horizontalDistance - b.HalfWidth;
                    if (edge < best)
                    {
                        best = edge;
                        height = s.WaterHeight;
                    }
                }
            return height;
        }

        static void BuildBranchWater(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var branches = track.Branches;
            if (branches == null)
                return;

            for (int bi = 0; bi < branches.Count; bi++)
            {
                var b = branches[bi];
                if (b == null || !b.IsBuilt)
                    continue;

                float extent = b.HalfWidth + track.waterSurfaceExtraWidth;
                int rows = Mathf.CeilToInt(b.Length / WaterStep) + 1;
                int cols = WaterColumns + 1;
                var verts = new List<Vector3>(rows * cols);
                var normals = new List<Vector3>(rows * cols);
                var uvs = new List<Vector2>(rows * cols);
                var inside = new List<bool>(rows * cols);
                var tris = new List<int>();

                // Đường chia đôi mặt nước của sông chính, phần nhánh nằm trong đó thì bỏ để hai lớp nước không chồng lên nhau
                float mainWaterEdge = track.HalfWidth + track.waterSurfaceExtraWidth - 0.4f;

                for (int r = 0; r < rows; r++)
                {
                    float d = Mathf.Min(r * WaterStep, b.Length);
                    var s = b.EvaluateAtDistance(d);
                    for (int c = 0; c < cols; c++)
                    {
                        float u = c / (float)WaterColumns;
                        Vector3 v = s.center + s.right * Mathf.Lerp(-extent, extent, u);
                        verts.Add(v);
                        normals.Add(Vector3.up);
                        uvs.Add(new Vector2(u, d / (2f * extent)));
                        inside.Add(track.GetNearest(v).horizontalDistance < mainWaterEdge);
                    }
                }

                for (int r = 0; r < rows - 1; r++)
                {
                    for (int c = 0; c < WaterColumns; c++)
                    {
                        int a = r * cols + c, bb = a + cols, cc = bb + 1, dd = a + 1;
                        if (inside[a] && inside[bb] && inside[cc] && inside[dd])
                            continue;
                        tris.Add(a); tris.Add(bb); tris.Add(cc);
                        tris.Add(a); tris.Add(cc); tris.Add(dd);
                    }
                }

                var mesh = new Mesh { name = $"{track.name}_BranchWater_{bi}" };
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                ctx.Meshes.Add(mesh);

                var go = CreateMeshObject($"BranchWater_{bi}", mesh, new[] { ctx.Mats.Water }, parent);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void BuildBranchDetails(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var branches = track.Branches;
            if (branches == null)
                return;

            int rocks = 0, reeds = 0, pads = 0, signs = 0;
            for (int bi = 0; bi < branches.Count; bi++)
            {
                var b = branches[bi];
                if (b == null || !b.IsBuilt)
                    continue;

                var group = Group(parent, $"Branch_{bi}_{b.name}");
                // Hạt giống riêng cho mỗi nhánh, để thêm hay bớt nhánh không làm xáo trộn chỗ cây cối khác
                var rng = new System.Random(track.seed + 4241 + bi * 97);

                rocks += BranchRocks(ctx, group, b, rng);
                reeds += BranchReeds(ctx, group, b, rng);
                if (b.bulge > 0f)
                    pads += BranchLilyPads(ctx, group, b, rng);
                if (ForkSign(ctx, group, b))
                    signs++;
            }

            ctx.Count("đá trong nhánh", rocks);
            ctx.Count("sậy ven nhánh", reeds);
            ctx.Count("lá súng trong nhánh", pads);
            ctx.Count("biển ngã rẽ", signs);
        }

        static int BranchRocks(BuildContext ctx, Transform parent, RiverBranch b, System.Random rng)
        {
            float from = 15f, to = b.Length - 15f;
            if (to <= from)
                return 0;

            int target = Mathf.RoundToInt((to - from) * b.rocksPer100m / 100f);
            float limit = Mathf.Max(0.5f, b.HalfWidth - 1.6f);
            var placed = new List<Vector3>();
            for (int attempt = 0; attempt < target * 30 && placed.Count < target; attempt++)
            {
                var s = b.EvaluateAtDistance(Range(rng, from, to));
                Vector3 p = s.center + s.right * Range(rng, -limit, limit);
                // Nhánh hẹp nên đá thưa hơn sông chính một chút để luôn còn khe đi qua
                if (TooClose(placed, p, 7f))
                    continue;
                placed.Add(p);

                float size = Range(rng, 0.8f, 1.4f);
                var mesh = Pick(rng, ctx.Props.Rocks);
                var go = CreateMeshObject($"BranchRock_{placed.Count:00}", mesh, new[] { ctx.Mats.Rock }, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, s.WaterHeight - 0.35f * size, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = new Vector3(size * Range(rng, 0.9f, 1.3f), size * Range(rng, 1.2f, 1.6f), size * Range(rng, 0.9f, 1.3f));
                var col = go.AddComponent<MeshCollider>();
                col.sharedMesh = mesh;
                col.convex = true;
            }
            return placed.Count;
        }

        static int BranchReeds(BuildContext ctx, Transform parent, RiverBranch b, System.Random rng)
        {
            int target = Mathf.RoundToInt(b.Length * 0.18f);
            int count = 0;
            for (int i = 0; i < target; i++)
            {
                var s = b.EvaluateAtDistance(Range(rng, 12f, b.Length - 12f));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 p = s.center + s.right * side * (b.HalfWidth + Range(rng, -0.6f, 0.6f));
                var go = CreateMeshObject($"BranchReeds_{count:000}", ctx.Props["Reeds"], new[] { ctx.Mats.Reed, ctx.Mats.Trunk }, parent);
                go.transform.SetPositionAndRotation(new Vector3(p.x, s.WaterHeight, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                go.transform.localScale = Vector3.one * Range(rng, 0.8f, 1.2f);
                count++;
            }
            return count;
        }

        static int BranchLilyPads(BuildContext ctx, Transform parent, RiverBranch b, System.Random rng)
        {
            int clusters = Mathf.RoundToInt(b.Length / 25f);
            int pads = 0;
            for (int i = 0; i < clusters; i++)
            {
                var s = b.EvaluateAtDistance(Range(rng, 15f, b.Length - 15f));
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Vector3 c = s.center + s.right * side * (b.HalfWidth - Range(rng, 0.8f, 2f));
                int n = rng.Next(3, 6);
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = c + new Vector3(Range(rng, -1.2f, 1.2f), 0f, Range(rng, -1.2f, 1.2f));
                    bool flower = rng.NextDouble() < 0.3;
                    var mats = flower ? new[] { ctx.Mats.LilyPad, ctx.Mats.Flowers[1] } : new[] { ctx.Mats.LilyPad };
                    var go = CreateMeshObject($"BranchLilyPad_{pads:000}", ctx.Props[flower ? "LilyPadFlower" : "LilyPad"], mats, parent);
                    go.transform.SetPositionAndRotation(new Vector3(p.x, s.WaterHeight + 0.03f, p.z), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                    go.transform.localScale = Vector3.one * Range(rng, 0.7f, 1.1f);
                    go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                    pads++;
                }
            }
            return pads;
        }

        /// <summary>
        /// Biển báo cắm trên mũi đất giữa sông chính và nhánh, ngay sau ngã rẽ. Mũi tên chỉ về phía nhánh.
        /// Biển vàng là đường tắt, biển xanh là đường vòng.
        /// </summary>
        static bool ForkSign(BuildContext ctx, Transform parent, RiverBranch b)
        {
            var track = ctx.Track;
            for (float k = 14f; k <= 70f; k += 4f)
            {
                var sm = track.EvaluateAtDistance(b.fromDistance + k);
                Vector3 mainEdge = sm.center + sm.right * b.side * (track.HalfWidth + 1f);
                var bs = b.GetNearest(mainEdge);
                Vector3 branchEdge = bs.center - (bs.center - mainEdge).normalized * (b.HalfWidth + 1f);
                Vector3 p = Vector3.Lerp(mainEdge, branchEdge, 0.5f);
                float h = track.TerrainHeight(p, out float d);
                // Cần đứng hẳn trên đất khô, cách mép nước ít nhất 1,5 m
                if (d < track.HalfWidth + 1.5f || h < sm.WaterHeight + 0.3f)
                    continue;

                var board = b.signColor == 0 ? ctx.Mats.Flowers[0] : ctx.Mats.GateStart;
                var go = CreateMeshObject("ForkSign", ctx.Props["Sign"], new[] { ctx.Mats.Wood, board, ctx.Mats.GateFinish }, parent);
                // Mặt bảng là -z, trục +z xuôi dòng thì thuyền đang tới nhìn thấy mặt bảng
                go.transform.SetPositionAndRotation(new Vector3(p.x, h, p.z), Quaternion.LookRotation(sm.tangent, Vector3.up));
                // Mũi tên mặc định chỉ sang phải; nhánh bên trái thì lật lại
                go.transform.localScale = new Vector3(b.side > 0 ? 1.6f : -1.6f, 1.6f, 1.6f);
                ctx.Exclude(p, 2.5f);
                return true;
            }
            return false;
        }
    }
}
