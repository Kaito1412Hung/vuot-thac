using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.EditorTools.Track
{
    /// <summary>
    /// Các bước làm khớp bờ (ShorelineFinish) và khoét hang (MajesticCaverns, MountainCaveAccess)
    /// chia mỗi tam giác đất thành 4 tam giác con, lặp nhiều lần, trên cả một vùng rộng. Phần lớn
    /// tam giác con sau đó vẫn nằm phẳng đúng chỗ cũ, chỉ những tam giác ở mép nước hoặc mép hầm
    /// mới bị hạ xuống hay bị khoét.
    ///
    /// Công cụ này đi ngược lại: với mỗi tam giác gốc (trước khi chia), xem cây chia 1→4 của nó.
    /// Nhánh nào mà mọi tam giác con còn đủ, cùng vật liệu và nằm đúng trên một mặt phẳng với
    /// UV liền mạch thì gộp lại thành một tam giác. Không có đỉnh nào bị dịch chuyển, nên bề mặt
    /// sau khi gộp giống hệt trước khi gộp.
    /// </summary>
    public static class TerrainMeshCompactor
    {
        const float PositionTolerance = 0.002f; // 2 mm
        const float UvTolerance = 0.002f;
        const float BaryTolerance = 0.0005f;
        const int MaxDepth = 12;

        struct Leaf
        {
            public Vector3 a, b, c;
            public Vector2 ua, ub, uc;
            public int sub;
            public Vector2 ba, bb, bc; // toạ độ trong tam giác gốc (u, v)
            public Vector2 Centroid => (ba + bb + bc) / 3f;
        }

        struct Original
        {
            public Vector3 a, b, c;
            public Vector3 normal;
            public int axis0, axis1; // hai trục dùng để chiếu khi tính toạ độ trong tam giác
            public float d00, d01, d11, denom;
            public Vector2 e0, e1, o;
        }

        public struct Result
        {
            public int before, after, merged, untouched;
            public override string ToString() => $"{before} -> {after} tam giác";
        }

        /// <summary>Gộp lại <paramref name="mesh"/> dựa trên tam giác gốc trong <paramref name="original"/>. Sửa trực tiếp mesh.</summary>
        public static Result Compact(Mesh mesh, Mesh original)
        {
            var result = new Result();
            var origs = BuildOriginals(original);
            var grid = BuildGrid(origs, out float cell, out Vector2 gridMin);

            var verts = mesh.vertices; var uvs = mesh.uv;
            bool hasUv = uvs != null && uvs.Length == verts.Length;
            var perOriginal = new Dictionary<int, List<Leaf>>();
            var outV = new List<Vector3>(); var outUv = new List<Vector2>();
            var outSub = new List<int>[mesh.subMeshCount];
            for (int s = 0; s < outSub.Length; s++) outSub[s] = new List<int>();

            void Emit(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
            {
                int i = outV.Count;
                outV.Add(a); outV.Add(b); outV.Add(c);
                outUv.Add(ua); outUv.Add(ub); outUv.Add(uc);
                outSub[sub].Add(i); outSub[sub].Add(i + 1); outSub[sub].Add(i + 2);
            }

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tri = mesh.GetTriangles(sub);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    result.before++;
                    var leaf = new Leaf
                    {
                        a = verts[tri[i]], b = verts[tri[i + 1]], c = verts[tri[i + 2]], sub = sub,
                        ua = hasUv ? uvs[tri[i]] : Vector2.zero, ub = hasUv ? uvs[tri[i + 1]] : Vector2.zero, uc = hasUv ? uvs[tri[i + 2]] : Vector2.zero
                    };
                    int owner = FindOwner(leaf, origs, grid, cell, gridMin);
                    if (owner < 0)
                    {
                        Emit(sub, leaf.a, leaf.b, leaf.c, leaf.ua, leaf.ub, leaf.uc);
                        result.untouched++;
                        continue;
                    }
                    var o = origs[owner];
                    leaf.ba = Bary(o, leaf.a); leaf.bb = Bary(o, leaf.b); leaf.bc = Bary(o, leaf.c);
                    if (!perOriginal.TryGetValue(owner, out var list)) perOriginal[owner] = list = new List<Leaf>();
                    list.Add(leaf);
                }
            }

            foreach (var pair in perOriginal)
            {
                var root = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
                Resolve(root, pair.Value, 0, Emit, ref result);
            }

            result.after = outV.Count / 3;
            mesh.Clear();
            mesh.indexFormat = outV.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(outV);
            if (hasUv) mesh.SetUVs(0, outUv);
            mesh.subMeshCount = outSub.Length;
            for (int s = 0; s < outSub.Length; s++) mesh.SetTriangles(outSub[s], s, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return result;
        }

        /// <summary>
        /// Gộp mọi mesh đất và núi có bản gốc trong <paramref name="originals"/>. Mỗi mesh được gộp trên bản sao,
        /// rồi bắn tia dày đặc (mỗi 0,25 m) so độ cao với mesh cũ; chỉ ghi đè khi không lệch quá 2 mm.
        /// </summary>
        public static string Run(VuotThac.Track.RiverTrack track, Dictionary<string, Mesh> originals, bool apply)
        {
            var sb = new System.Text.StringBuilder();
            int totalBefore = 0, totalAfter = 0, failed = 0, done = 0;
            foreach (string group in new[] { "Terrain", "Mountains" })
            {
                var root = track.transform.Find("Generated/" + group);
                if (root == null) continue;
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !originals.TryGetValue(mesh.name, out var original)) continue;
                    if (mesh.triangles.Length / 3 <= original.triangles.Length / 3 + 8) continue;
                    var copy = Object.Instantiate(mesh);
                    var res = Compact(copy, original);
                    string check = Verify(filter.transform, mesh, copy, out bool ok);
                    totalBefore += res.before; totalAfter += ok ? res.after : res.before;
                    sb.AppendLine($"{mesh.name}: {res}{(ok ? "" : "  KHÔNG ĐẠT")}  {check}");
                    if (ok && apply)
                    {
                        mesh.Clear();
                        mesh.indexFormat = copy.indexFormat;
                        mesh.SetVertices(copy.vertices);
                        mesh.SetUVs(0, copy.uv);
                        mesh.subMeshCount = copy.subMeshCount;
                        for (int s = 0; s < copy.subMeshCount; s++) mesh.SetTriangles(copy.GetTriangles(s), s, false);
                        mesh.RecalculateNormals(); mesh.RecalculateBounds();
                        UnityEditor.EditorUtility.SetDirty(mesh);
                        var col = filter.GetComponent<MeshCollider>();
                        if (col != null) { col.sharedMesh = null; col.sharedMesh = mesh; }
                        done++;
                    }
                    if (!ok) failed++;
                    Object.DestroyImmediate(copy);
                }
            }
            sb.Insert(0, $"Tổng: {totalBefore} -> {totalAfter} tam giác, {(apply ? done + " mesh đã ghi" : "chạy thử")}, {failed} mesh không đạt\n");
            return sb.ToString();
        }

        static string Verify(Transform t, Mesh before, Mesh after, out bool ok)
        {
            var goA = new GameObject("VerifyA") { hideFlags = HideFlags.HideAndDontSave };
            var goB = new GameObject("VerifyB") { hideFlags = HideFlags.HideAndDontSave };
            foreach (var go in new[] { goA, goB }) { go.transform.SetPositionAndRotation(t.position, t.rotation); go.transform.localScale = t.lossyScale; }
            var ca = goA.AddComponent<MeshCollider>(); ca.sharedMesh = before;
            var cb = goB.AddComponent<MeshCollider>(); cb.sharedMesh = after;
            Physics.SyncTransforms();
            var bounds = ca.bounds;
            int rays = 0, mismatch = 0, edgeGrazes = 0; float maxDy = 0;
            for (float x = bounds.min.x + .05f; x <= bounds.max.x; x += .25f)
                for (float z = bounds.min.z + .05f; z <= bounds.max.z; z += .25f)
                    foreach (bool down in new[] { true, false })
                    {
                        var ray = down ? new Ray(new Vector3(x, bounds.max.y + 5, z), Vector3.down) : new Ray(new Vector3(x, bounds.min.y - 5, z), Vector3.up);
                        float len = bounds.size.y + 10;
                        bool ha = ca.Raycast(ray, out var hitA, len), hb = cb.Raycast(ray, out var hitB, len);
                        rays++;
                        if (ha != hb) { mismatch++; continue; }
                        if (!ha) continue;
                        float dy = Mathf.Abs(hitA.point.y - hitB.point.y);
                        if (dy >= PositionTolerance)
                        {
                            // Tia sượt đúng mép một bậc đất có sẵn thì mỗi mesh có thể chọn một phía.
                            // Đo lại sát bên (1 cm): nếu cả 4 điểm đều khớp thì đó chỉ là sai số đo ở mép.
                            float worst = 0;
                            foreach (var o in new[] { new Vector3(.01f, 0, 0), new Vector3(-.01f, 0, 0), new Vector3(0, 0, .01f), new Vector3(0, 0, -.01f) })
                            {
                                var r = new Ray(ray.origin + o, ray.direction);
                                bool a2 = ca.Raycast(r, out var h2a, len), b2 = cb.Raycast(r, out var h2b, len);
                                worst = Mathf.Max(worst, a2 != b2 ? float.MaxValue : a2 ? Mathf.Abs(h2a.point.y - h2b.point.y) : 0);
                            }
                            if (worst < PositionTolerance) { edgeGrazes++; continue; }
                        }
                        maxDy = Mathf.Max(maxDy, dy);
                    }
            Object.DestroyImmediate(goA); Object.DestroyImmediate(goB);
            float area(Mesh m) { var v = m.vertices; var tr = m.triangles; double s = 0; for (int i = 0; i < tr.Length; i += 3) s += Vector3.Cross(v[tr[i + 1]] - v[tr[i]], v[tr[i + 2]] - v[tr[i]]).magnitude * .5; return (float)s; }
            float areaA = area(before), areaB = area(after);
            // Tia sượt đúng mép tam giác có thể trúng ở mesh này mà trượt ở mesh kia: cho phép rất ít
            ok = maxDy < PositionTolerance && mismatch <= rays / 20000 + 2 && Mathf.Abs(areaA - areaB) <= areaA * 1e-4f;
            return $"tia {rays}, lệch trúng/trượt {mismatch}, sượt mép {edgeGrazes}, lệch cao lớn nhất {maxDy * 1000:0.00} mm, diện tích {areaA:0} / {areaB:0} m²";
        }

        /// <summary>
        /// Bịt chỗ nối chữ T: khi đỉnh của tam giác bên cạnh nằm giữa cạnh của một tam giác, card đồ hoạ
        /// có thể để lọt khe 1 điểm ảnh. Tam giác đó được chia quạt quanh tâm của nó để đi qua các đỉnh nằm
        /// trên cạnh. Tâm và các đỉnh mới nằm đúng trên mặt phẳng cũ nên bề mặt không đổi.
        /// </summary>
        public static int CloseTJunctions(Mesh mesh)
        {
            var v = mesh.vertices; var uv = mesh.uv; bool hasUv = uv != null && uv.Length == v.Length;
            const float cell = .5f, eps = .001f;
            var grid = new Dictionary<Vector3Int, List<Vector3>>();
            var seen = new HashSet<Vector3Int>();
            foreach (var p in v)
            {
                if (!seen.Add(Vector3Int.RoundToInt(p * 10000f))) continue;
                var k = Vector3Int.FloorToInt(p / cell);
                if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<Vector3>();
                l.Add(p);
            }
            var outV = new List<Vector3>(); var outUv = new List<Vector2>();
            var subs = new List<int>[mesh.subMeshCount];
            int fixedCount = 0;
            var onEdge = new List<(float t, Vector3 p)>();
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                subs[s] = new List<int>();
                var tri = mesh.GetTriangles(s);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    Vector3 a = v[tri[i]], b = v[tri[i + 1]], c = v[tri[i + 2]];
                    Vector2 ua = hasUv ? uv[tri[i]] : default, ub = hasUv ? uv[tri[i + 1]] : default, uc = hasUv ? uv[tri[i + 2]] : default;
                    var ring = new List<Vector3>();
                    bool split = false;
                    foreach (var (p, q) in new[] { (a, b), (b, c), (c, a) })
                    {
                        ring.Add(p);
                        onEdge.Clear();
                        var min = Vector3Int.FloorToInt((Vector3.Min(p, q) - Vector3.one * eps) / cell);
                        var max = Vector3Int.FloorToInt((Vector3.Max(p, q) + Vector3.one * eps) / cell);
                        var d = q - p; float len2 = d.sqrMagnitude;
                        for (int x = min.x; x <= max.x; x++) for (int y = min.y; y <= max.y; y++) for (int z = min.z; z <= max.z; z++)
                        {
                            if (!grid.TryGetValue(new Vector3Int(x, y, z), out var pts)) continue;
                            foreach (var r in pts)
                            {
                                float t = Vector3.Dot(r - p, d) / len2;
                                if (t <= 1e-4f || t >= 1 - 1e-4f) continue;
                                if ((p + d * t - r).sqrMagnitude > eps * eps) continue;
                                onEdge.Add((t, r));
                            }
                        }
                        if (onEdge.Count > 0)
                        {
                            split = true;
                            onEdge.Sort((m, n) => m.t.CompareTo(n.t));
                            foreach (var e in onEdge) ring.Add(p + d * e.t); // đặt đúng trên cạnh để bề mặt không lệch
                        }
                    }
                    if (!split)
                    {
                        int j = outV.Count; outV.Add(a); outV.Add(b); outV.Add(c); outUv.Add(ua); outUv.Add(ub); outUv.Add(uc);
                        subs[s].Add(j); subs[s].Add(j + 1); subs[s].Add(j + 2);
                        continue;
                    }
                    fixedCount++;
                    var center = (a + b + c) / 3f;
                    Vector2 UvAt(Vector3 p)
                    {
                        var n = Vector3.Cross(b - a, c - a); float area = n.sqrMagnitude;
                        float wa = Vector3.Dot(Vector3.Cross(c - b, p - b), n) / area, wb = Vector3.Dot(Vector3.Cross(a - c, p - c), n) / area;
                        return ua * wa + ub * wb + uc * (1 - wa - wb);
                    }
                    for (int k = 0; k < ring.Count; k++)
                    {
                        var p0 = ring[k]; var p1 = ring[(k + 1) % ring.Count];
                        int j = outV.Count; outV.Add(center); outV.Add(p0); outV.Add(p1);
                        outUv.Add(UvAt(center)); outUv.Add(UvAt(p0)); outUv.Add(UvAt(p1));
                        subs[s].Add(j); subs[s].Add(j + 1); subs[s].Add(j + 2);
                    }
                }
            }
            if (fixedCount == 0) return 0;
            mesh.Clear();
            mesh.indexFormat = outV.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(outV);
            if (hasUv) mesh.SetUVs(0, outUv);
            mesh.subMeshCount = subs.Length;
            for (int s = 0; s < subs.Length; s++) mesh.SetTriangles(subs[s], s, false);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return fixedCount;
        }

        /// <summary>Bịt chỗ nối chữ T cho mọi mesh đất và núi, kiểm tra độ cao như khi gộp rồi mới ghi.</summary>
        public static string CloseAll(VuotThac.Track.RiverTrack track, bool apply)
        {
            var sb = new System.Text.StringBuilder(); int before = 0, after = 0, failed = 0;
            foreach (string group in new[] { "Terrain", "Mountains" })
                foreach (var filter in track.transform.Find("Generated/" + group).GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh = filter.sharedMesh; if (mesh == null) continue;
                    var copy = Object.Instantiate(mesh);
                    int n = CloseTJunctions(copy);
                    if (n == 0) { Object.DestroyImmediate(copy); continue; }
                    string check = Verify(filter.transform, mesh, copy, out bool ok);
                    int tb = mesh.triangles.Length / 3, ta = copy.triangles.Length / 3;
                    before += tb; after += ok ? ta : tb; if (!ok) failed++;
                    sb.AppendLine($"{mesh.name}: {n} chỗ nối chữ T, {tb} -> {ta}{(ok ? "" : "  KHÔNG ĐẠT")}  {check}");
                    if (ok && apply)
                    {
                        mesh.Clear(); mesh.indexFormat = copy.indexFormat; mesh.SetVertices(copy.vertices); mesh.SetUVs(0, copy.uv);
                        mesh.subMeshCount = copy.subMeshCount; for (int s = 0; s < copy.subMeshCount; s++) mesh.SetTriangles(copy.GetTriangles(s), s, false);
                        mesh.RecalculateNormals(); mesh.RecalculateBounds(); UnityEditor.EditorUtility.SetDirty(mesh);
                        var col = filter.GetComponent<MeshCollider>(); if (col != null) { col.sharedMesh = null; col.sharedMesh = mesh; }
                    }
                    Object.DestroyImmediate(copy);
                }
            sb.Insert(0, $"Tổng: {before} -> {after} tam giác, {failed} mesh không đạt{(apply ? "" : " (chạy thử)")}\n");
            return sb.ToString();
        }

        delegate void EmitFn(int sub, Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc);

        // node: ba góc của nút trong toạ độ (u, v) của tam giác gốc, theo đúng chiều quay của tam giác gốc
        static void Resolve(Vector2[] node, List<Leaf> leaves, int depth, EmitFn emit, ref Result result)
        {
            if (leaves.Count == 0) return;
            if (leaves.Count > 1 && TryMerge(node, leaves, out var merged))
            {
                emit(merged.sub, merged.a, merged.b, merged.c, merged.ua, merged.ub, merged.uc);
                result.merged += leaves.Count - 1;
                return;
            }
            if (leaves.Count == 1 || depth >= MaxDepth)
            {
                foreach (var l in leaves) emit(l.sub, l.a, l.b, l.c, l.ua, l.ub, l.uc);
                return;
            }
            // Chia nút giống cách các bước dựng map chia: (a,ab,ca) (ab,b,bc) (ca,bc,c) (ab,bc,ca)
            Vector2 a = node[0], b = node[1], c = node[2], ab = (a + b) * .5f, bc = (b + c) * .5f, ca = (c + a) * .5f;
            var children = new[] { new[] { a, ab, ca }, new[] { ab, b, bc }, new[] { ca, bc, c }, new[] { ab, bc, ca } };
            var buckets = new[] { new List<Leaf>(), new List<Leaf>(), new List<Leaf>(), new List<Leaf>() };
            var leftovers = new List<Leaf>();
            foreach (var l in leaves)
            {
                var local = LocalBary(node, l.Centroid);
                // Tam giác lá to hơn nút con (không thuộc hẳn nút con nào) thì giữ nguyên
                if (!FitsChild(node, l)) { leftovers.Add(l); continue; }
                int k = local.x >= .5f ? 0 : local.y >= .5f ? 1 : local.z >= .5f ? 2 : 3;
                buckets[k].Add(l);
            }
            foreach (var l in leftovers) emit(l.sub, l.a, l.b, l.c, l.ua, l.ub, l.uc);
            for (int k = 0; k < 4; k++) Resolve(children[k], buckets[k], depth + 1, emit, ref result);
        }

        static bool FitsChild(Vector2[] node, Leaf l)
        {
            // Lá nằm trong một nút con khi mọi đỉnh của nó có toạ độ cục bộ cùng phía một nửa
            var la = LocalBary(node, l.ba); var lb = LocalBary(node, l.bb); var lc = LocalBary(node, l.bc);
            const float e = BaryTolerance;
            bool In(Vector3 p, int axis) => p[axis] >= .5f - e;
            for (int axis = 0; axis < 3; axis++) if (In(la, axis) && In(lb, axis) && In(lc, axis)) return true;
            // nút giữa (ab, bc, ca): mọi toạ độ ≤ 0.5
            bool Mid(Vector3 p) => p.x <= .5f + e && p.y <= .5f + e && p.z <= .5f + e;
            return Mid(la) && Mid(lb) && Mid(lc);
        }

        struct Merged { public Vector3 a, b, c; public Vector2 ua, ub, uc; public int sub; }

        static bool TryMerge(Vector2[] node, List<Leaf> leaves, out Merged merged)
        {
            merged = default;
            int sub = leaves[0].sub;
            // Tìm vị trí 3D và UV tại ba góc nút từ các đỉnh lá trùng góc
            var found = new bool[3]; var pos = new Vector3[3]; var uv = new Vector2[3];
            double area = 0;
            foreach (var l in leaves)
            {
                if (l.sub != sub) return false;
                Check(l.ba, l.a, l.ua); Check(l.bb, l.b, l.ub); Check(l.bc, l.c, l.uc);
                area += Cross(l.bb - l.ba, l.bc - l.ba);
            }
            void Check(Vector2 p, Vector3 world, Vector2 tex)
            {
                for (int k = 0; k < 3; k++)
                    if (!found[k] && (p - node[k]).sqrMagnitude < BaryTolerance * BaryTolerance) { found[k] = true; pos[k] = world; uv[k] = tex; }
            }
            if (!found[0] || !found[1] || !found[2]) return false;
            double nodeArea = Cross(node[1] - node[0], node[2] - node[0]);
            // Lá phải phủ kín nút (cùng chiều quay và tổng diện tích bằng nhau)
            if (nodeArea <= 0 || System.Math.Abs(area - nodeArea) > nodeArea * 1e-4) return false;
            var nodeNormal = Vector3.Cross(pos[1] - pos[0], pos[2] - pos[0]);
            if (nodeNormal.sqrMagnitude < 1e-12f) return false;
            foreach (var l in leaves)
            {
                if (Vector3.Dot(Vector3.Cross(l.b - l.a, l.c - l.a), nodeNormal) <= 0) return false;
                if (!OnPlane(l.ba, l.a, l.ua) || !OnPlane(l.bb, l.b, l.ub) || !OnPlane(l.bc, l.c, l.uc)) return false;
            }
            bool OnPlane(Vector2 p, Vector3 world, Vector2 tex)
            {
                var w = LocalBary(node, p);
                var expected = pos[0] * w.x + pos[1] * w.y + pos[2] * w.z;
                var expectedUv = uv[0] * w.x + uv[1] * w.y + uv[2] * w.z;
                return (expected - world).sqrMagnitude < PositionTolerance * PositionTolerance
                    && (expectedUv - tex).sqrMagnitude < UvTolerance * UvTolerance;
            }
            merged = new Merged { a = pos[0], b = pos[1], c = pos[2], ua = uv[0], ub = uv[1], uc = uv[2], sub = sub };
            return true;
        }

        static double Cross(Vector2 a, Vector2 b) => (double)a.x * b.y - (double)a.y * b.x;

        static Vector3 LocalBary(Vector2[] node, Vector2 p)
        {
            Vector2 v0 = node[1] - node[0], v1 = node[2] - node[0], v2 = p - node[0];
            double den = Cross(v0, v1);
            float y = (float)(Cross(v2, v1) / den), z = (float)(Cross(v0, v2) / den);
            return new Vector3(1 - y - z, y, z);
        }

        // ---------- Tìm tam giác gốc chứa mỗi tam giác lá ----------

        static List<Original> BuildOriginals(Mesh mesh)
        {
            var list = new List<Original>();
            var v = mesh.vertices;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var tri = mesh.GetTriangles(sub);
                for (int i = 0; i < tri.Length; i += 3)
                {
                    var o = new Original { a = v[tri[i]], b = v[tri[i + 1]], c = v[tri[i + 2]] };
                    o.normal = Vector3.Cross(o.b - o.a, o.c - o.a);
                    var n = new Vector3(Mathf.Abs(o.normal.x), Mathf.Abs(o.normal.y), Mathf.Abs(o.normal.z));
                    // Chiếu lên mặt phẳng toạ độ vuông góc với trục pháp tuyến lớn nhất.
                    // Ưu tiên chiếu xuống mặt đất (XZ) vì bước làm khớp bờ chỉ hạ đỉnh theo chiều đứng.
                    if (n.y >= n.x * .35f && n.y >= n.z * .35f) { o.axis0 = 0; o.axis1 = 2; }
                    else if (n.x >= n.z) { o.axis0 = 1; o.axis1 = 2; }
                    else { o.axis0 = 0; o.axis1 = 1; }
                    o.o = P(o, o.a); o.e0 = P(o, o.b) - o.o; o.e1 = P(o, o.c) - o.o;
                    o.denom = (float)Cross(o.e0, o.e1);
                    list.Add(o);
                }
            }
            return list;
        }

        static Vector2 P(Original o, Vector3 p) => new Vector2(p[o.axis0], p[o.axis1]);

        static Vector2 Bary(Original o, Vector3 p)
        {
            var d = P(o, p) - o.o;
            return new Vector2((float)(Cross(d, o.e1) / o.denom), (float)(Cross(o.e0, d) / o.denom));
        }

        static Dictionary<long, List<int>> BuildGrid(List<Original> origs, out float cell, out Vector2 min)
        {
            cell = 4f; min = new Vector2(float.MaxValue, float.MaxValue);
            foreach (var o in origs) { min = Vector2.Min(min, new Vector2(Mathf.Min(o.a.x, o.b.x, o.c.x), Mathf.Min(o.a.z, o.b.z, o.c.z))); }
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < origs.Count; i++)
            {
                var o = origs[i];
                int x0 = Mathf.FloorToInt((Mathf.Min(o.a.x, o.b.x, o.c.x) - min.x) / cell), x1 = Mathf.FloorToInt((Mathf.Max(o.a.x, o.b.x, o.c.x) - min.x) / cell);
                int z0 = Mathf.FloorToInt((Mathf.Min(o.a.z, o.b.z, o.c.z) - min.y) / cell), z1 = Mathf.FloorToInt((Mathf.Max(o.a.z, o.b.z, o.c.z) - min.y) / cell);
                for (int x = x0; x <= x1; x++) for (int z = z0; z <= z1; z++)
                {
                    long key = ((long)x << 32) ^ (uint)z;
                    if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<int>();
                    l.Add(i);
                }
            }
            return grid;
        }

        static int FindOwner(Leaf leaf, List<Original> origs, Dictionary<long, List<int>> grid, float cell, Vector2 min)
        {
            var centroid = (leaf.a + leaf.b + leaf.c) / 3f;
            long key = ((long)Mathf.FloorToInt((centroid.x - min.x) / cell) << 32) ^ (uint)Mathf.FloorToInt((centroid.z - min.y) / cell);
            if (!grid.TryGetValue(key, out var candidates)) return -1;
            int best = -1; float bestDistance = 4f; // bước làm khớp bờ hạ đỉnh tối đa khoảng 3,2 m
            foreach (int i in candidates)
            {
                var o = origs[i];
                if (Mathf.Abs(o.denom) < 1e-8f) continue;
                // Mọi đỉnh của lá phải nằm trong tam giác gốc
                bool inside = true;
                foreach (var p in new[] { leaf.a, leaf.b, leaf.c })
                {
                    var w = Bary(o, p);
                    if (w.x < -BaryTolerance || w.y < -BaryTolerance || w.x + w.y > 1 + BaryTolerance) { inside = false; break; }
                }
                if (!inside) continue;
                var n = o.normal.normalized;
                float distance = Mathf.Abs(Vector3.Dot(centroid - o.a, n));
                if (distance < bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }
    }
}
