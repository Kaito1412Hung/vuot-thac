using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.EditorTools.Boats
{
    /// <summary>
    /// Gom tam giác low-poly theo vật liệu: mỗi vật liệu thành một submesh.
    /// Kèm các khối hình cơ bản để dựng thuyền: hộp, ống, nón, cầu, thân thuyền, thúng.
    /// Quy ước: mũi thuyền hướng +z, trục y hướng lên, y = 0 là mực nước mong muốn.
    /// </summary>
    public class BoatMesh
    {
        readonly List<Vector3> verts = new();
        readonly List<List<int>> subs = new();
        readonly List<Material> mats = new();

        int Sub(Material m)
        {
            int i = mats.IndexOf(m);
            if (i >= 0)
                return i;
            mats.Add(m);
            subs.Add(new List<int>());
            return mats.Count - 1;
        }

        public void Tri(Material m, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-10f)
                return;
            int s = Sub(m);
            int i = verts.Count;
            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            subs[s].Add(i);
            subs[s].Add(i + 1);
            subs[s].Add(i + 2);
        }

        /// <summary>Tam giác có mặt trước quay về phía điểm <paramref name="toward"/>.</summary>
        public void TriToward(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 toward)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, toward - (a + b + c) / 3f) < 0f) Tri(m, a, c, b);
            else Tri(m, a, b, c);
        }

        /// <summary>Tam giác có mặt trước quay ra xa điểm <paramref name="inside"/>.</summary>
        public void TriAway(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f) Tri(m, a, c, b);
            else Tri(m, a, b, c);
        }

        public void QuadAway(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 inside)
        {
            TriAway(m, a, b, c, inside);
            TriAway(m, a, c, d, inside);
        }

        public void QuadToward(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 toward)
        {
            TriToward(m, a, b, c, toward);
            TriToward(m, a, c, d, toward);
        }

        /// <summary>Hai mặt, nhìn từ phía nào cũng thấy (buồm, mái che).</summary>
        public void DoubleQuad(Material m, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Tri(m, a, b, c);
            Tri(m, a, c, d);
            Tri(m, a, c, b);
            Tri(m, a, d, c);
        }

        public void DoubleTri(Material m, Vector3 a, Vector3 b, Vector3 c)
        {
            Tri(m, a, b, c);
            Tri(m, a, c, b);
        }

        public void Box(Material m, Vector3 center, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(int x, int y, int z) => center + rotation * new Vector3(x * h.x, y * h.y, z * h.z);
            QuadAway(m, P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), center);
            QuadAway(m, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), center);
            QuadAway(m, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), center);
            QuadAway(m, P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), center);
            QuadAway(m, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), center);
            QuadAway(m, P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), center);
        }

        public void Box(Material m, Vector3 center, Vector3 size) => Box(m, center, size, Quaternion.identity);

        /// <summary>Hộp nối từ điểm a tới điểm b.</summary>
        public void Beam(Material m, Vector3 a, Vector3 b, float width, float thickness)
        {
            Vector3 dir = b - a;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            Box(m, (a + b) * 0.5f, new Vector3(width, thickness, dir.magnitude), Quaternion.LookRotation(dir, up));
        }

        static void Basis(Vector3 axis, out Vector3 u, out Vector3 v)
        {
            Vector3 helper = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
            u = Vector3.Cross(axis, helper).normalized;
            v = Vector3.Cross(axis, u).normalized;
        }

        /// <summary>Ống tròn (nhiều cạnh) từ điểm a tới điểm b.</summary>
        public void Cylinder(Material m, Vector3 a, Vector3 b, float radius, int sides = 7, bool caps = true)
        {
            Vector3 axis = (b - a).normalized;
            Basis(axis, out var u, out var v);
            Vector3 mid = (a + b) * 0.5f;
            for (int i = 0; i < sides; i++)
            {
                float t0 = i / (float)sides * Mathf.PI * 2f, t1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 o0 = (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * radius;
                Vector3 o1 = (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * radius;
                QuadAway(m, a + o0, b + o0, b + o1, a + o1, mid);
                if (caps)
                {
                    TriAway(m, a, a + o0, a + o1, mid);
                    TriAway(m, b, b + o0, b + o1, mid);
                }
            }
        }

        public void Cone(Material m, Vector3 baseCenter, Vector3 apex, float radius, int sides = 8, bool cap = true)
        {
            Vector3 axis = (apex - baseCenter).normalized;
            Basis(axis, out var u, out var v);
            Vector3 inside = Vector3.Lerp(baseCenter, apex, 0.3f);
            for (int i = 0; i < sides; i++)
            {
                float t0 = i / (float)sides * Mathf.PI * 2f, t1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                Vector3 p0 = baseCenter + (u * Mathf.Cos(t0) + v * Mathf.Sin(t0)) * radius;
                Vector3 p1 = baseCenter + (u * Mathf.Cos(t1) + v * Mathf.Sin(t1)) * radius;
                TriAway(m, p0, apex, p1, inside);
                if (cap)
                    TriAway(m, p0, p1, baseCenter, inside);
            }
        }

        /// <summary>Khối cầu thô (ít mặt), dùng cho đầu người, cục u.</summary>
        public void Sphere(Material m, Vector3 center, Vector3 radius, int rings = 4, int segments = 6)
        {
            Vector3 P(int r, int s)
            {
                float lat = Mathf.PI * r / rings - Mathf.PI * 0.5f;
                float lon = Mathf.PI * 2f * s / segments;
                return center + Vector3.Scale(new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon)), radius);
            }
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < segments; s++)
                    QuadAway(m, P(r, s), P(r + 1, s), P(r + 1, s + 1), P(r, s + 1), center);
        }

        /// <summary>Thông số thân thuyền dựng bằng các mặt cắt ngang xếp dọc theo chiều dài.</summary>
        public struct HullShape
        {
            public float length, beam, depth, freeboard;
            /// <summary>Mũi và đuôi: 1.5 là nhọn, 5 trở lên là bầu và tù.</summary>
            public float bowFullness, sternFullness;
            /// <summary>Hai đầu thuyền vểnh lên bao nhiêu mét.</summary>
            public float rocker;
            /// <summary>Cắt bằng đuôi (0 là đuôi nhọn, 0.2 là đuôi vuông kiểu ca nô).</summary>
            public float sternCut;
            /// <summary>Có sàn đậy kín từ vị trí này tới mũi (-1 là đuôi, 1 là mũi). Lớn hơn 1 là không đậy.</summary>
            public float deckFrom;
        }

        public void Hull(HullShape h, Material outer, Material inner, Material rim, Material deck = null)
        {
            const int stations = 18;
            float tStart = -(1f - h.sternCut);
            var outerRing = new Vector3[stations + 1][];
            var innerRing = new Vector3[stations + 1][];
            var tops = new float[stations + 1];
            var zs = new float[stations + 1];

            for (int i = 0; i <= stations; i++)
            {
                float t = Mathf.Lerp(tStart, 1f, i / (float)stations);
                float a = Mathf.Abs(t);
                float full = t >= 0f ? h.bowFullness : h.sternFullness;
                float w = h.beam * 0.5f * Mathf.Max(0f, 1f - Mathf.Pow(a, full));
                float top = h.freeboard + h.rocker * t * t;
                float bottom = -h.depth * (1f - 0.4f * t * t) + h.rocker * 0.5f * t * t;
                float z = t * h.length * 0.5f;
                tops[i] = top;
                zs[i] = z;

                float[] xs = { -1f, -0.95f, -0.6f, 0f, 0.6f, 0.95f, 1f };
                float[] ys = { 0f, 0.5f, 0.92f, 1f, 0.92f, 0.5f, 0f };
                outerRing[i] = new Vector3[7];
                innerRing[i] = new Vector3[7];
                for (int j = 0; j < 7; j++)
                {
                    float y = Mathf.Lerp(top, bottom, ys[j]);
                    outerRing[i][j] = new Vector3(xs[j] * w, y, z);
                    float yi = j == 0 || j == 6 ? top : y + 0.07f;
                    innerRing[i][j] = new Vector3(xs[j] * w * 0.86f, yi, z);
                }
            }

            for (int i = 0; i < stations; i++)
            {
                float zMid = (zs[i] + zs[i + 1]) * 0.5f;
                float top = (tops[i] + tops[i + 1]) * 0.5f;
                Vector3 axis = new(0f, top - h.depth * 0.5f, zMid);
                Vector3 above = new(0f, top + 2f, zMid);
                for (int j = 0; j < 6; j++)
                {
                    QuadAway(outer, outerRing[i][j], outerRing[i + 1][j], outerRing[i + 1][j + 1], outerRing[i][j + 1], axis);
                    QuadToward(inner, innerRing[i][j], innerRing[i + 1][j], innerRing[i + 1][j + 1], innerRing[i][j + 1], above);
                }
                foreach (int j in new[] { 0, 6 })
                    QuadToward(rim, outerRing[i][j], outerRing[i + 1][j], innerRing[i + 1][j], innerRing[i][j], outerRing[i][j] + Vector3.up * 3f);

                float tMid = Mathf.Lerp(tStart, 1f, (i + 0.5f) / stations);
                if (deck != null && tMid >= h.deckFrom)
                    QuadToward(deck, outerRing[i][0], outerRing[i + 1][0], outerRing[i + 1][6], outerRing[i][6], above + Vector3.up * 5f);
            }

            // Đuôi vuông: bịt mặt sau
            if (h.sternCut > 0f)
            {
                Vector3 c = new(0f, (tops[0] - h.depth) * 0.5f, zs[0]);
                for (int j = 0; j < 6; j++)
                    TriToward(outer, c, outerRing[0][j], outerRing[0][j + 1], c - Vector3.forward * 5f);
            }
        }

        /// <summary>Thúng chai tròn: lòng chảo, có vành.</summary>
        public void Bowl(float radius, float depth, float rimHeight, Material outer, Material inner, Material rim)
        {
            const int seg = 18;
            float[] rs = { 0f, 0.45f, 0.75f, 0.93f, 1f };
            float[] ys = { -depth, -depth * 0.9f, -depth * 0.6f, -depth * 0.12f, rimHeight };
            Vector3 P(int k, int s, float scale, float lift)
            {
                float a = s / (float)seg * Mathf.PI * 2f;
                float r = rs[k] * radius * scale;
                return new Vector3(Mathf.Cos(a) * r, ys[k] + lift, Mathf.Sin(a) * r);
            }
            var center = new Vector3(0f, -depth * 0.3f, 0f);
            var above = new Vector3(0f, rimHeight + 2f, 0f);
            for (int k = 0; k < rs.Length - 1; k++)
                for (int s = 0; s < seg; s++)
                {
                    QuadAway(outer, P(k, s, 1f, 0f), P(k + 1, s, 1f, 0f), P(k + 1, s + 1, 1f, 0f), P(k, s + 1, 1f, 0f), center);
                    float lift = k + 1 == rs.Length - 1 ? 0f : 0.05f;
                    QuadToward(inner, P(k, s, 0.92f, 0.05f), P(k + 1, s, 0.92f, lift), P(k + 1, s + 1, 0.92f, lift), P(k, s + 1, 0.92f, 0.05f), above);
                }
            int last = rs.Length - 1;
            for (int s = 0; s < seg; s++)
                QuadToward(rim, P(last, s, 1f, 0f), P(last, s + 1, 1f, 0f), P(last, s + 1, 0.92f, 0f), P(last, s, 0.92f, 0f), above + Vector3.up * 5f);
        }

        /// <summary>Ống tròn chạy theo một đường khép kín (phao cao su, vành trang trí).</summary>
        public void Tube(Material m, IList<Vector3> path, float radius, int sides = 8)
        {
            for (int i = 0; i < path.Count; i++)
                Cylinder(m, path[i], path[(i + 1) % path.Count], radius, sides, false);
            // Bịt khớp nối bằng khối cầu nhỏ cho liền
            foreach (var p in path)
                Sphere(m, p, Vector3.one * radius, 3, sides);
        }

        public Mesh Build(string name, out Material[] materials)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++)
                mesh.SetTriangles(subs[i], i, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            materials = mats.ToArray();
            return mesh;
        }
    }
}
