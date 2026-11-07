using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    /// <summary>Dựng thác nước và hang động xuyên núi trên sông chính.</summary>
    public static partial class RiverTrackBuilder
    {
        static bool FallNear(RiverTrack track, float distance)
        {
            if (track.falls == null)
                return false;
            foreach (var f in track.falls)
                if (f != null && Mathf.Abs(distance - f.distance) < f.length * 0.5f + 4f)
                    return true;
            return false;
        }

        /// <summary>
        /// Chừa trống lòng sông và hai bờ quanh thác và hang, để nhà cửa, đá giữa dòng, sậy không mọc chắn
        /// đường thuyền hay chui vào vách núi. Chạy trước khi đặt bất cứ công trình nào.
        /// </summary>
        static void PrepareFeatures(BuildContext ctx)
        {
            var track = ctx.Track;
            void ExcludeStretch(float from, float to, float maxLateral)
            {
                for (float d = from; d <= to; d += 6f)
                {
                    var s = track.EvaluateAtDistance(d);
                    for (float lat = 0f; lat <= maxLateral; lat += 8f)
                        foreach (float side in new[] { -1f, 1f })
                            ctx.Exclude(s.center + s.right * side * lat, 8f);
                }
            }

            if (track.falls != null)
                foreach (var f in track.falls)
                    if (f != null)
                        ExcludeStretch(f.distance - f.length - 15f, f.distance + f.length + 15f, track.HalfWidth + track.bankWidth + 10f);

            if (track.tunnels != null)
                foreach (var t in track.tunnels)
                    if (t != null)
                        // Cả quãng chân núi gần sông: không xây nhà trên vách núi dựng đứng
                        ExcludeStretch(t.distance - t.length * 0.5f - 40f, t.distance + t.length * 0.5f + 40f, track.HalfWidth + track.bankWidth + 40f);
        }

        // ---------- Hang động xuyên núi ----------

        /// <summary>Một mặt cắt ngang của hang, dùng lại để trang trí bên trong.</summary>
        class CaveRing
        {
            public Vector3 mid, across;
            public float water, innerHalf;
            public Vector3[] inner;
            public Vector3[] outer;
        }

        // Submesh của vỏ hang
        const int CaveInner = 0, CaveCliff = 1, CaveGrass = 2;

        /// <summary>
        /// Dựng hang sau khi đã mở rộng lòng sông, nên hang ôm đúng mép nước thật.
        /// Lòng hang là nửa vòm méo mó tự nhiên. Mái hang bên ngoài chính là sườn núi, cao bằng địa hình hai bên
        /// nên nhìn từ ngoài vào thấy sông chui thẳng vào lòng núi.
        /// </summary>
        static void BuildTunnels(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            if (track.tunnels == null || track.tunnels.Count == 0)
                return;

            var caveRock = AccentMaterial("CaveRock", new Color(0.33f, 0.3f, 0.28f), ctx.Mats.Rock, false);
            int built = 0;
            for (int ti = 0; ti < track.tunnels.Count; ti++)
            {
                var t = track.tunnels[ti];
                if (t == null)
                    continue;
                var root = new GameObject($"Cave_{ti}").transform;
                root.SetParent(parent, false);

                // Khung mặt cắt dọc sông chính, theo mép nước thật sau khi mở rộng lòng sông
                var frames = new List<CaveFrame>();
                float half = t.length * 0.5f;
                int sections = Mathf.Max(6, Mathf.CeilToInt(t.length / 2.5f));
                for (int k = 0; k <= sections; k++)
                {
                    float d = Mathf.Lerp(t.distance - half, t.distance + half, k / (float)sections);
                    var s = track.EvaluateAtDistance(d);
                    frames.Add(MakeFrame(track, s.center, s.right, track.HalfWidth, s.WaterHeight, d));
                }

                var mesh = BuildCaveMesh($"{track.name}_Cave_{ti}", frames, t.ceilingHeight, ti, track, false, out var rings);
                ctx.Meshes.Add(mesh);
                var shell = CreateMeshObject("CaveShell", mesh, new[] { caveRock, ctx.Mats.Cliff, ctx.Mats.GrassDark }, root);
                shell.AddComponent<MeshCollider>().sharedMesh = mesh;

                DecorateCave(ctx, root, t.ceilingHeight, ti, rings, caveRock, true);
                built++;
            }
            ctx.Count("hang động", built);
        }

        /// <summary>Một mặt cắt ngang dòng nước, đã đổi sang vị trí thật sau khi mở rộng lòng sông.</summary>
        class CaveFrame
        {
            public Vector3 mid, across;
            public float water, span, d;
            /// <summary>Khúc vỏ hang nằm ngoài lòng núi (phần cửa hang): mái chỉ là một vành đá ôm vòm, ăn khớp với sườn thung lũng.</summary>
            public bool open;
        }

        /// <summary>Độ cao mặt đất đã dựng (địa hình hoặc núi) ngay dưới một điểm, bỏ qua cây, đá và các vật khác.</summary>
        static float BuiltGround(Vector3 world, float fallback)
        {
            var hits = Physics.RaycastAll(new Vector3(world.x, 400f, world.z), Vector3.down, 800f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            foreach (var h in hits)
            {
                string n = h.collider.name;
                if ((n.StartsWith("Terrain") || n.StartsWith("Mountains")) && h.point.y > best)
                    best = h.point.y;
            }
            return best > float.MinValue ? best : fallback;
        }

        static CaveFrame MakeFrame(RiverTrack track, Vector3 mapCenter, Vector3 mapRight, float halfWidth, float water, float d)
        {
            Vector3 left = track.AfterRaceInsertion(mapCenter - mapRight * halfWidth);
            Vector3 right = track.AfterRaceInsertion(mapCenter + mapRight * halfWidth);
            Vector3 across = right - left;
            across.y = 0f;
            // Tâm lấy mẫu từ sông đã mang sẵn độ cao mặt nước. Bỏ trục y đi vì khi dựng vòm sẽ cộng mặt nước vào sau
            Vector3 mid = (left + right) * 0.5f;
            mid.y = 0f;
            var f = new CaveFrame { mid = mid, water = water, span = across.magnitude * 0.5f, d = d };
            f.across = across.normalized;
            return f;
        }

        /// <summary>
        /// Độ cao mặt đất hoặc núi tại một điểm (tọa độ trước khi mở rộng lòng sông), chưa đào nhánh sông nào.
        /// Dùng để biết chỗ nào nhánh đã chui sâu vào núi và để dựng mái hang khớp với sườn núi.
        /// </summary>
        static float SurfaceRaw(RiverTrack track, Vector3 map)
        {
            var s = track.GetNearest(map);
            // TerrainHeightAt tự đổi từ tọa độ sau khi mở rộng lòng sông về tọa độ gốc,
            // nên khi đã mở rộng phải đưa vào tọa độ sau mở rộng, kẻo bị đổi hai lần và lấy lệch chỗ
            Vector3 q = track.appliedRaceStripWidth > 0f ? track.AfterRaceInsertion(map) : map;
            float h = s.horizontalDistance >= track.terrainMargin - MountainOverlap
                ? MountainHeightRaw(track, map, s)
                : track.TerrainHeightAt(q, s.horizontalDistance, s.WaterHeight);
            return Mathf.Max(h, Mathf.Max(track.RidgeHeight(map, s), track.BranchRidgeHeight(map)));
        }

        static Mesh BuildCaveMesh(string name, List<CaveFrame> frames, float ceiling, int index, RiverTrack track, bool buried, out List<CaveRing> rings)
        {
            const int arc = 16;       // số điểm trên cung vòm
            const float sink = 2.2f;  // chân vách cắm xuống dưới mặt nước
            int sections = frames.Count - 1;
            rings = new List<CaveRing>();
            var t = new { ceilingHeight = ceiling };

            for (int k = 0; k <= sections; k++)
            {
                var fr = frames[k];
                float d = fr.d;
                Vector3 mid = fr.mid;
                Vector3 across = fr.across;
                float water = fr.water;
                float innerHalf = fr.span + track.waterSurfaceExtraWidth + 0.8f;
                // Hang ngầm: rãnh khoét trong địa hình rộng innerHalf + 6 m, mép rãnh còn dốc xuống thêm một ô lưới.
                // Mái hang phủ rộng hơn rãnh để mặt núi phía trên không lộ một khe tối chạy dọc theo hang.
                float outerHalf = innerHalf + 6f + (buried && !fr.open ? track.gridSize * 2.4f : 0f);
                // Cửa hang loe rộng và cao hơn lòng hang một chút cho giống miệng hang thật
                bool portal = k == 0 || k == sections;
                float flare = portal ? 1.18f : k == 1 || k == sections - 1 ? 1.07f : 1f;

                // Lòng hang: hai chân vách thẳng ở mặt nước, phía trên là cung vòm bị bóp méo bằng nhiễu
                var inner2 = new List<Vector2> { new(-innerHalf, -sink), new(-innerHalf, 1.2f) };
                for (int i = 1; i < arc; i++)
                {
                    float ang = Mathf.PI - Mathf.PI * i / arc;
                    float wob = Mathf.PerlinNoise(d * 0.11f + i * 0.43f + index * 9f, i * 0.31f + d * 0.05f) - 0.5f;
                    float rx = innerHalf * (1f + wob * 0.12f) * flare;
                    float ry = t.ceilingHeight * (1f + wob * 0.55f) * flare;
                    inner2.Add(new Vector2(Mathf.Cos(ang) * rx, 1.2f + Mathf.Sin(ang) * (ry - 1.2f)));
                }
                inner2.Add(new Vector2(innerHalf, 1.2f));
                inner2.Add(new Vector2(innerHalf, -sink));

                // Vỏ ngoài: mỗi điểm trong có một điểm ngoài nằm trên sườn núi ngay phía trên
                Vector3 W(float x, float y) => mid + across * x + Vector3.up * (water + y);
                float Roof(float x)
                {
                    Vector3 world = mid + across * x;
                    Vector3 map = track.BeforeRaceInsertion(world);
                    float h = SurfaceRaw(track, map) - water;
                    // Hang trên sông chính: mái hai bên nằm dưới mặt núi một chút, tránh hai mặt chồng nhau nhấp nháy
                    if (buried || Mathf.Abs(x) > innerHalf + 1f)
                    {
                        if (buried && fr.open)
                            h -= 0.7f;
                        else if (buried)
                        {
                            // Hang ngầm: mái nhô nhẹ trên mặt núi để che kín mép rãnh khoét trong địa hình
                            // (nếu thấp hơn thì vách rãnh lộ thành một vệt tối chạy dọc theo hang),
                            // ra quá mép rãnh một ô lưới thì cụp hẳn xuống dưới mặt đất
                            float edge = innerHalf + 6f;
                            float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge + track.gridSize, outerHalf, Mathf.Abs(x)));
                            h += Mathf.Lerp(0.3f, -3.5f, f);
                        }
                        else
                            h -= 0.8f;
                    }
                    return Mathf.Max(h, t.ceilingHeight + 3f);
                }

                // Phần cửa hang lộ ra ngoài núi: mái là một vành đá dày vài mét ôm sát vòm, gồ ghề theo nhiễu,
                // hai bên tựa vào sườn thung lũng thật. Nhờ vậy cửa hang không còn là một bức tường phẳng vuông vức.
                float RimRoof(float x, float archY, int i)
                {
                    float ground = BuiltGround(mid + across * x, water) - water - 0.5f;
                    float n = Mathf.PerlinNoise(d * 0.21f + i * 0.57f + index * 3.1f, i * 0.73f);
                    float rim = archY + 2.2f + n * 3.2f;
                    return Mathf.Min(Mathf.Max(ground, rim), Roof(x));
                }

                Vector3 along = sections > 0 ? frames[Mathf.Min(k + 1, sections)].mid - frames[Mathf.Max(k - 1, 0)].mid : Vector3.zero;
                along.y = 0f;
                along = along.sqrMagnitude > 1e-4f ? along.normalized : Vector3.zero;

                var ring = new CaveRing { mid = mid, across = across, water = water, innerHalf = innerHalf };
                ring.inner = new Vector3[inner2.Count];
                ring.outer = new Vector3[inner2.Count];
                for (int i = 0; i < inner2.Count; i++)
                {
                    Vector2 p = inner2[i];
                    ring.inner[i] = W(p.x, p.y);
                    if (i == 0 || i == inner2.Count - 1)
                        ring.outer[i] = W(Mathf.Sign(p.x) * outerHalf, -sink);
                    else
                    {
                        float x = p.x * outerHalf / innerHalf;
                        ring.outer[i] = W(x, fr.open ? RimRoof(x, p.y, i) : Roof(x));
                    }
                    // Mép cửa hang lồi lõm theo chiều dọc để mặt vách không phẳng
                    if (portal && i > 0 && i < inner2.Count - 1)
                    {
                        float j1 = Mathf.PerlinNoise(i * 0.61f + index * 5.3f, d * 0.17f) - 0.5f;
                        float j2 = Mathf.PerlinNoise(i * 0.47f + 11f, d * 0.13f + index) - 0.5f;
                        float dir = k == 0 ? -1f : 1f;
                        ring.inner[i] += along * dir * j1 * 1.6f;
                        ring.outer[i] += along * dir * j2 * 3.5f;
                    }
                }
                rings.Add(ring);
            }

            var fb = new FlatMeshBuilder(3);
            int n = rings[0].inner.Length;
            for (int k = 0; k < sections; k++)
            {
                var a = rings[k];
                var b = rings[k + 1];
                Vector3 axis = (a.mid + b.mid) * 0.5f + Vector3.up * ((a.water + b.water) * 0.5f + t.ceilingHeight * 0.4f);
                for (int i = 0; i < n - 1; i++)
                {
                    TriToward(fb, CaveInner, a.inner[i], b.inner[i], b.inner[i + 1], axis);
                    TriToward(fb, CaveInner, a.inner[i], b.inner[i + 1], a.inner[i + 1], axis);
                    RoofTri(fb, a.outer[i], b.outer[i], b.outer[i + 1], axis);
                    RoofTri(fb, a.outer[i], b.outer[i + 1], a.outer[i + 1], axis);
                }
            }

            // Hai cửa hang: vách núi nối miệng hang với sườn núi
            foreach (int k in new[] { 0, sections })
            {
                var r = rings[k];
                var other = rings[k == 0 ? 1 : sections - 1];
                Vector3 front = r.mid + (r.mid - other.mid).normalized * 80f + Vector3.up * r.water;
                for (int i = 0; i < n - 1; i++)
                {
                    TriToward(fb, CaveCliff, r.inner[i], r.outer[i], r.outer[i + 1], front);
                    TriToward(fb, CaveCliff, r.inner[i], r.outer[i + 1], r.inner[i + 1], front);
                }
            }

            return fb.ToMesh(name);
        }

        /// <summary>Mặt ngoài: chỗ thoải phủ cỏ như sườn núi, chỗ dốc lộ đá.</summary>
        static void RoofTri(FlatMeshBuilder fb, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f)
            {
                (b, c) = (c, b);
                normal = -normal;
            }
            fb.Tri(normal.normalized.y > 0.7f ? CaveGrass : CaveCliff, a, b, c);
        }

        /// <summary>Thạch nhũ, tinh thể phát sáng, dây leo ở cửa hang, đá tảng và cây trên mái hang.</summary>
        static void DecorateCave(BuildContext ctx, Transform root, float ceiling, int index, List<CaveRing> rings, Material caveRock, bool roofTrees)
        {
            var t = new { ceilingHeight = ceiling };
            var rng = new System.Random(ctx.Track.seed + 911 + index * 37);
            var blue = AccentMaterial("CaveCrystalBlue", new Color(0.35f, 0.85f, 1f), ctx.Mats.Rock, true);
            var violet = AccentMaterial("CaveCrystalViolet", new Color(0.75f, 0.45f, 1f), ctx.Mats.Rock, true);
            var fb = new FlatMeshBuilder(4); // 0 thạch nhũ, 1 tinh thể xanh, 2 tinh thể tím, 3 dây leo
            int n = rings[0].inner.Length;

            for (int k = 1; k < rings.Count - 1; k++)
            {
                var r = rings[k];
                Vector3 axis = r.mid + Vector3.up * (r.water + t.ceilingHeight * 0.4f);

                // Thạch nhũ rủ xuống từ trần
                for (int j = 0; j < 3; j++)
                {
                    int i = rng.Next(4, n - 4);
                    Vector3 top = r.inner[i];
                    float len = Range(rng, 0.8f, 3f);
                    Spike(fb, 0, top + Vector3.up * 0.3f, top - Vector3.up * len, Range(rng, 0.25f, 0.6f), 6);
                }

                // Cụm tinh thể mọc trên vách, gần mặt nước để thuyền đi qua thấy rõ
                if (k % 2 == 0)
                {
                    foreach (int side in new[] { 0, 1 })
                    {
                        if (rng.NextDouble() < 0.35)
                            continue;
                        int i = side == 0 ? rng.Next(2, 5) : rng.Next(n - 5, n - 2);
                        Vector3 baseP = r.inner[i];
                        Vector3 inward = (axis - baseP);
                        inward.y = 0f;
                        inward.Normalize();
                        int sub = rng.NextDouble() < 0.6 ? 1 : 2;
                        int count = rng.Next(3, 6);
                        for (int c = 0; c < count; c++)
                        {
                            Vector3 dir = (inward * Range(rng, 0.3f, 0.9f) + Vector3.up * Range(rng, 0.6f, 1.2f)
                                + r.across * Range(rng, -0.4f, 0.4f)).normalized;
                            Vector3 p = baseP + inward * 0.2f + r.across * Range(rng, -0.6f, 0.6f) + Vector3.up * Range(rng, 0f, 0.8f);
                            float len = Range(rng, 0.6f, 1.8f);
                            Spike(fb, sub, p - dir * 0.15f, p + dir * len, Range(rng, 0.12f, 0.28f), 5);
                            Spike(fb, sub, p + dir * 0.05f, p - dir * 0.3f, Range(rng, 0.12f, 0.28f), 5);
                        }
                    }
                }
            }

            // Dây leo rủ xuống trước hai cửa hang
            foreach (int k in new[] { 0, rings.Count - 1 })
            {
                var r = rings[k];
                for (int i = 3; i < n - 3; i++)
                {
                    if (rng.NextDouble() < 0.45)
                        continue;
                    Vector3 top = r.inner[i];
                    float len = Range(rng, 1.5f, 5f);
                    Vector3 w = r.across * Range(rng, 0.25f, 0.5f);
                    Vector3 lean = Vector3.Cross(Vector3.up, r.across) * Range(rng, -0.3f, 0.3f);
                    var a = top - w * 0.5f;
                    var b = top + w * 0.5f;
                    var c = top + w * 0.2f - Vector3.up * len + lean;
                    var d = top - w * 0.2f - Vector3.up * len + lean;
                    fb.Tri(3, a, b, c); fb.Tri(3, a, c, d);
                    fb.Tri(3, a, c, b); fb.Tri(3, a, d, c);
                }

                // Đá tảng chặn hai bên cửa hang
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int j = 0; j < 3; j++)
                    {
                        var rock = CreateMeshObject("CaveMouthBoulder", Pick(rng, ctx.Props.Rocks), new[] { caveRock }, root);
                        Vector3 p = r.mid + r.across * side * (r.innerHalf + Range(rng, -0.5f, 2.5f));
                        Vector3 outward = (r.mid - rings[k == 0 ? 1 : rings.Count - 2].mid).normalized;
                        p += outward * Range(rng, 0f, 4f);
                        p.y = r.water - 0.5f;
                        float size = Range(rng, 1.8f, 3.5f);
                        rock.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                        rock.transform.localScale = new Vector3(size, size * Range(rng, 0.8f, 1.4f), size);
                        var col = rock.AddComponent<MeshCollider>();
                        col.sharedMesh = rock.GetComponent<MeshFilter>().sharedMesh;
                        col.convex = true;
                    }
                }
            }

            var mesh = fb.ToMesh($"{ctx.Track.name}_CaveDetail_{index}");
            ctx.Meshes.Add(mesh);
            var go = CreateMeshObject("CaveDetail", mesh, new[] { caveRock, blue, violet, ctx.Mats.Leaves }, root);
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // Vài cây thông trên mái hang cho mái hang liền với rừng trên sườn núi
            var trunk = new[] { ctx.Mats.Trunk, ctx.Mats.LeavesDark };
            for (int j = 0; j < (roofTrees ? 8 : 0); j++)
            {
                var r = rings[rng.Next(1, rings.Count - 1)];
                int i = rng.Next(3, n - 3);
                Vector3 p = r.outer[i];
                var tree = CreateMeshObject("RoofPine", ctx.Props.Tree(rng.NextDouble() < 0.5 ? PropLibrary.TreeKind.Pine : PropLibrary.TreeKind.PineTall), trunk, root);
                tree.transform.SetPositionAndRotation(p - Vector3.up * 0.4f, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                tree.transform.localScale = Vector3.one * Range(rng, 1.1f, 1.7f);
            }
        }

        /// <summary>Gai nhọn: đáy tròn nhiều cạnh ở <paramref name="baseCenter"/>, chóp ở <paramref name="tip"/>.</summary>
        static void Spike(FlatMeshBuilder fb, int sub, Vector3 baseCenter, Vector3 tip, float radius, int sides)
        {
            Vector3 axis = (tip - baseCenter).normalized;
            Vector3 helper = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, helper).normalized;
            Vector3 v = Vector3.Cross(axis, u);
            Vector3 inside = Vector3.Lerp(baseCenter, tip, 0.25f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f, a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = baseCenter + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius;
                Vector3 p1 = baseCenter + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius;
                fb.TriOutward(sub, p0, tip, p1, inside);
                fb.TriOutward(sub, p0, p1, baseCenter, inside);
            }
        }

        static void TriToward(FlatMeshBuilder fb, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 toward)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(normal, toward - (a + b + c) / 3f) < 0f) fb.Tri(sub, a, c, b);
            else fb.Tri(sub, a, b, c);
        }

        // ---------- Nhánh sông chui vào núi ----------

        /// <summary>
        /// Đánh dấu đoạn nào của nhánh đã chui sâu vào núi: mặt đất phía trên cao hơn trần hang vài mét.
        /// Phải chạy trước mọi bước khác, vì đào địa hình và dựng núi đều cần biết đoạn nào đi ngầm.
        /// </summary>
        static void PrepareBranchUnderground(BuildContext ctx)
        {
            var track = ctx.Track;
            var branches = track.Branches;
            if (branches == null)
                return;

            foreach (var b in branches)
            {
                if (b == null || !b.IsBuilt)
                    continue;
                int n = b.points.Length;
                var flags = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    // Chừa hai đầu nhánh lộ thiên để còn nhìn thấy cửa vào
                    if (b.distances[i] < 25f || b.distances[i] > b.Length - 25f)
                        continue;
                    flags[i] = SurfaceRaw(track, b.points[i]) - b.points[i].y > b.caveCeiling + 5f;
                }

                // Lấp các khe hở trời ngắn hơn 60 m để hang liền một mạch, bỏ các đoạn ngầm ngắn hơn 20 m.
                // Đổi mét sang số điểm vì khoảng cách giữa các điểm của nhánh không phải 1 m.
                float step = b.Length / Mathf.Max(1, n - 1);
                FillRuns(flags, false, Mathf.CeilToInt(60f / step));
                FillRuns(flags, true, Mathf.CeilToInt(20f / step));
                b.underground = flags;
                b.MarkUndergroundChanged();

                int count = 0;
                foreach (bool f in flags)
                    if (f) count++;
                if (count > 0)
                    Debug.Log($"[Vượt Thác] Nhánh '{b.name}' chui vào lòng núi khoảng {count * step:0} m.", track);
            }
        }

        /// <summary>Đổi các đoạn liên tiếp mang giá trị <paramref name="value"/> ngắn hơn <paramref name="minLength"/> sang giá trị ngược lại.</summary>
        static void FillRuns(bool[] flags, bool value, int minLength)
        {
            int i = 0;
            while (i < flags.Length)
            {
                if (flags[i] != value)
                {
                    i++;
                    continue;
                }
                int start = i;
                while (i < flags.Length && flags[i] == value)
                    i++;
                bool touchesEdge = start == 0 || i == flags.Length;
                // Khe lộ thiên ở hai đầu nhánh là cửa vào thật, không lấp
                if (i - start < minLength && !(value == false && touchesEdge))
                    for (int k = start; k < i; k++)
                        flags[k] = !value;
            }
        }

        /// <summary>Hang trong rừng do Codex dựng mà đè lên đường hang ngầm thì tắt đi, kẻo hai hang chọc thủng nhau.</summary>
        static void DisableCavesOverBranches(BuildContext ctx)
        {
            var track = ctx.Track;
            var branches = track.Branches;
            if (branches == null)
                return;
            foreach (var cave in track.GetComponents<ForestCaveLandform>())
            {
                if (!cave.configured)
                    continue;
                foreach (var b in branches)
                {
                    if (b == null || b.underground == null)
                        continue;
                    bool hit = false;
                    for (int i = 0; i < b.points.Length; i += 3)
                    {
                        Vector3 d = b.points[i] - cave.origin;
                        d.y = 0f;
                        if (d.magnitude < 55f)
                        {
                            hit = true;
                            break;
                        }
                    }
                    if (hit)
                    {
                        cave.configured = false;
                        EditorUtility.SetDirty(cave);
                        break;
                    }
                }
            }
        }

        /// <summary>Dựng hang dọc theo các đoạn nhánh đi ngầm trong núi, kèm thạch nhũ và tinh thể phát sáng.</summary>
        static void BuildBranchCaves(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            var branches = track.Branches;
            if (branches == null)
                return;

            var caveRock = AccentMaterial("CaveRock", new Color(0.33f, 0.3f, 0.28f), ctx.Mats.Rock, false);
            Physics.SyncTransforms(); // vành cửa hang dò độ cao địa hình thật bằng tia
            int built = 0;
            for (int bi = 0; bi < branches.Count; bi++)
            {
                var b = branches[bi];
                if (b == null || b.underground == null)
                    continue;

                int n = b.points.Length;
                int i = 0;
                while (i < n)
                {
                    if (!b.underground[i])
                    {
                        i++;
                        continue;
                    }
                    int start = i;
                    while (i < n && b.underground[i])
                        i++;
                    // Kéo dài thêm vài mét ra ngoài để miệng hang lộ ra trước vách núi
                    float from = Mathf.Max(0f, b.distances[start] - 6f);
                    float to = Mathf.Min(b.Length, b.distances[i - 1] + 6f);

                    var frames = new List<CaveFrame>();
                    int sections = Mathf.Max(6, Mathf.CeilToInt((to - from) / 2.5f));
                    for (int k = 0; k <= sections; k++)
                    {
                        float d = Mathf.Lerp(from, to, k / (float)sections);
                        var s = b.EvaluateAtDistance(d);
                        var frame = MakeFrame(track, s.center, s.right, b.HalfWidth, s.WaterHeight, d + 1000f * (bi + 1));
                        frame.open = !b.IsUndergroundAt(d);
                        frames.Add(frame);
                    }

                    int id = 100 + bi * 10 + built;
                    var root = new GameObject($"BranchCave_{bi}_{built}").transform;
                    root.SetParent(parent, false);
                    var mesh = BuildCaveMesh($"{track.name}_BranchCave_{bi}_{built}", frames, b.caveCeiling, id, track, true, out var rings);
                    ctx.Meshes.Add(mesh);
                    var shell = CreateMeshObject("CaveShell", mesh, new[] { caveRock, ctx.Mats.Cliff, ctx.Mats.GrassDark }, root);
                    shell.AddComponent<MeshCollider>().sharedMesh = mesh;
                    DecorateCave(ctx, root, b.caveCeiling, id, rings, caveRock, false);
                    built++;
                }
            }
            ctx.Count("hang trong núi", built);
        }

        // ---------- Rào chắn sông chính ----------

        /// <summary>
        /// Hàng khúc gỗ lớn xếp chồng cùng đá tảng chắn ngang sông chính, buộc người chơi rẽ vào một trong các nhánh.
        /// Có thêm một bức tường vô hình cao để thuyền không thể vượt qua.
        /// </summary>
        static void BuildBarriers(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            if (track.barriers == null || track.barriers.Count == 0)
                return;

            var rng = new System.Random(track.seed + 3301);
            int bi = 0;
            foreach (float d in track.barriers)
            {
                var s = track.EvaluateAtDistance(d);
                float extent = track.HalfWidth + track.waterSurfaceExtraWidth + 1f;
                Vector3 left = track.AfterRaceInsertion(s.center - s.right * track.HalfWidth) - s.right * (extent - track.HalfWidth);
                Vector3 right = track.AfterRaceInsertion(s.center + s.right * track.HalfWidth) + s.right * (extent - track.HalfWidth);
                left.y = right.y = s.WaterHeight;
                Vector3 span = right - left;
                Vector3 dir = span.normalized;
                float width = span.magnitude;

                var root = new GameObject($"Barrier_{bi}").transform;
                root.SetParent(parent, false);
                root.SetPositionAndRotation((left + right) * 0.5f, Quaternion.LookRotation(s.tangent, Vector3.up));

                // Hai lớp khúc gỗ nằm ngang, lớp trên lệch nửa khúc so với lớp dưới
                const float logLength = 12f;
                for (int layer = 0; layer < 2; layer++)
                {
                    float offset = layer == 0 ? 0f : logLength * 0.5f;
                    for (float x = -offset; x < width; x += logLength * 0.92f)
                    {
                        float mid = Mathf.Clamp(x + logLength * 0.5f, logLength * 0.4f, width - logLength * 0.4f);
                        Vector3 p = left + dir * mid + Vector3.up * (0.25f + layer * 1.15f) - s.tangent * (layer * 0.3f);
                        var log = CreateMeshObject("BarrierLog", ctx.Props["Log"], new[] { ctx.Mats.Trunk, ctx.Mats.Wood }, root);
                        log.transform.SetPositionAndRotation(p, Quaternion.FromToRotation(Vector3.right, dir) * Quaternion.Euler(Range(rng, -4f, 4f), Range(rng, -6f, 6f), 0f));
                        log.transform.localScale = new Vector3(logLength / 4f, 2.4f, 2.4f);
                        log.AddComponent<CapsuleCollider>().direction = 0;
                    }
                }

                // Đá tảng chồng ở hai đầu và vài tảng phía trước
                for (int r = 0; r < 9; r++)
                {
                    float along = r < 4 ? Range(rng, 0f, 4f) : r < 8 ? width - Range(rng, 0f, 4f) : Range(rng, 0.3f, 0.7f) * width;
                    Vector3 p = left + dir * along - s.tangent * Range(rng, 0.5f, 3f);
                    p.y = s.WaterHeight - 0.3f;
                    var rock = CreateMeshObject("BarrierRock", Pick(rng, ctx.Props.Rocks), new[] { ctx.Mats.Rock }, root);
                    float size = Range(rng, 1.5f, 2.8f);
                    rock.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                    rock.transform.localScale = new Vector3(size, size * Range(rng, 0.9f, 1.4f), size);
                    var col = rock.AddComponent<MeshCollider>();
                    col.sharedMesh = rock.GetComponent<MeshFilter>().sharedMesh;
                    col.convex = true;
                }

                // Tường vô hình cao 8 m chắn trọn bề ngang sông
                var wall = root.gameObject.AddComponent<BoxCollider>();
                wall.center = new Vector3(0f, 2.5f, 0f);
                wall.size = new Vector3(width + 4f, 9f, 2f);
                bi++;
            }
            ctx.Count("rào chắn", bi);
        }

        // ---------- Thác nước ----------

        /// <summary>Bụi nước, bọt trắng ở chân thác và đá lớn hai bên mép thác. Dựng sau khi mở rộng lòng sông.</summary>
        static void BuildFallDetails(BuildContext ctx, Transform parent)
        {
            var track = ctx.Track;
            if (track.falls == null || track.falls.Count == 0)
                return;

            var rng = new System.Random(track.seed + 7717);
            for (int fi = 0; fi < track.falls.Count; fi++)
            {
                var f = track.falls[fi];
                if (f == null)
                    continue;
                var root = new GameObject($"Fall_{fi}").transform;
                root.SetParent(parent, false);

                var bottom = track.EvaluateAtDistance(f.distance + f.length * 0.5f + 1f);
                var lip = track.EvaluateAtDistance(f.distance - f.length * 0.5f);
                Vector3 left = track.AfterRaceInsertion(bottom.center - bottom.right * track.HalfWidth);
                Vector3 right = track.AfterRaceInsertion(bottom.center + bottom.right * track.HalfWidth);

                // Bụi nước rải đều theo bề ngang chân thác
                for (int i = 0; i < 4; i++)
                {
                    Vector3 p = Vector3.Lerp(left, right, (i + 0.5f) / 4f);
                    p.y = bottom.WaterHeight + 0.2f;
                    var spot = new GameObject($"Spray_{i}").transform;
                    spot.SetParent(root, false);
                    spot.position = p;
                    Spray(spot, "Mist", Vector3.zero, false);
                    Spray(spot, "Splash", Vector3.zero, true);
                }

                // Đá lớn hai bên mép thác cho thác trông như chảy qua khe đá
                Vector3 lipLeft = track.AfterRaceInsertion(lip.center - lip.right * track.HalfWidth);
                Vector3 lipRight = track.AfterRaceInsertion(lip.center + lip.right * track.HalfWidth);
                foreach (var (edge, outward) in new[] { (lipLeft, -lip.right), (lipRight, lip.right) })
                {
                    for (int r = 0; r < 3; r++)
                    {
                        var rock = CreateMeshObject($"LipRock_{r}", Pick(rng, ctx.Props.Rocks), new[] { ctx.Mats.Rock }, root);
                        Vector3 p = edge + outward * Range(rng, 0.5f, 3f) + lip.tangent * Range(rng, -2f, 4f);
                        p.y = lip.WaterHeight - 0.4f;
                        float size = Range(rng, 1.4f, 2.6f);
                        rock.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f));
                        rock.transform.localScale = new Vector3(size, size * Range(rng, 1f, 1.5f), size);
                        var col = rock.AddComponent<MeshCollider>();
                        col.sharedMesh = rock.GetComponent<MeshFilter>().sharedMesh;
                        col.convex = true;
                    }
                }
            }
            ctx.Count("thác nước", track.falls.Count);
        }
    }
}
