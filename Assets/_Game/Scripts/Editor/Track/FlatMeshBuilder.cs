using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.EditorTools.Track
{
    /// <summary>
    /// Gom tam giác thành mesh kiểu low-poly: mỗi tam giác có đỉnh riêng nên mặt phẳng không bị làm mượt.
    /// Hỗ trợ nhiều submesh để mỗi phần dùng một vật liệu.
    /// </summary>
    public class FlatMeshBuilder
    {
        readonly List<Vector3> vertices = new();
        readonly List<int>[] submeshes;

        public FlatMeshBuilder(int submeshCount = 1)
        {
            submeshes = new List<int>[submeshCount];
            for (int i = 0; i < submeshCount; i++)
                submeshes[i] = new List<int>();
        }

        public int VertexCount => vertices.Count;
        public bool IsEmpty => vertices.Count == 0;

        /// <summary>Thêm tam giác, mặt trước là mặt nhìn thấy a, b, c theo chiều kim đồng hồ.</summary>
        public void Tri(int submesh, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            var list = submeshes[submesh];
            list.Add(i);
            list.Add(i + 1);
            list.Add(i + 2);
        }

        /// <summary>Thêm tam giác và tự đảo chiều để mặt trước hướng ra xa điểm <paramref name="inside"/>.</summary>
        public void TriOutward(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 centroid = (a + b + c) / 3f;
            if (Vector3.Dot(normal, centroid - inside) < 0f)
                Tri(submesh, a, c, b);
            else
                Tri(submesh, a, b, c);
        }

        /// <summary>Thêm tam giác và tự đảo chiều để mặt trước hướng lên trên.</summary>
        public void TriUp(int submesh, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f)
                Tri(submesh, a, c, b);
            else
                Tri(submesh, a, b, c);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65535)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(vertices);
            mesh.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++)
                mesh.SetTriangles(submeshes[i], i, false);

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            NaturalSurfaceMaterials.EnsureUV(mesh);
            return mesh;
        }
    }
}
