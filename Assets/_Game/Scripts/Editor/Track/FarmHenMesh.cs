using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.EditorTools.Track
{
    static class FarmHenMesh
    {
        public static Mesh Load()
        {
            const string path = "Assets/ThirdParty/Animals/Chicken/FarmHen_v2.asset";
            var cached = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (cached != null) return cached;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Animals/Chicken/Source/chicken-light.fbx");
            var probe = Object.Instantiate(prefab); probe.hideFlags = HideFlags.HideAndDontSave;
            var filter = probe.GetComponentInChildren<MeshFilter>();
            var original = filter.sharedMesh; var p = original.vertices; var normals = original.normals; var uv = original.uv;
            var transform = filter.transform.localToWorldMatrix;
            float bottom = p.Min(v => transform.MultiplyPoint3x4(v).y);
            var vertices = new List<Vector3>(); var outN = new List<Vector3>(); var outUV = new List<Vector2>(); var counts = new List<int>();
            var map = new int[p.Length];
            var grid = new Dictionary<(int,int,int,int,int),int>();
            for (int i = 0; i < p.Length; i++)
            {
                var v = transform.MultiplyPoint3x4(p[i]); v.y -= bottom;
                var key = (Mathf.RoundToInt(v.x / 0.006f), Mathf.RoundToInt(v.y / 0.006f), Mathf.RoundToInt(v.z / 0.006f), Mathf.RoundToInt(uv[i].x / 0.035f), Mathf.RoundToInt(uv[i].y / 0.035f));
                if (!grid.TryGetValue(key, out int k))
                {
                    k = vertices.Count; grid[key] = k; vertices.Add(Vector3.zero); outN.Add(Vector3.zero); outUV.Add(Vector2.zero); counts.Add(0);
                }
                vertices[k] += v; outN[k] += transform.MultiplyVector(normals[i]); outUV[k] += uv[i]; counts[k]++; map[i] = k;
            }
            for (int i = 0; i < vertices.Count; i++) { vertices[i] /= counts[i]; outN[i] = outN[i].normalized; outUV[i] /= counts[i]; }
            var src = original.triangles; var triangles = new List<int>();
            for (int i = 0; i < src.Length; i += 3)
            {
                int a = map[src[i]], b = map[src[i+1]], c = map[src[i+2]];
                if (a == b || b == c || a == c) continue;
                triangles.AddRange(new[] { a,b,c });
            }
            float height = vertices.Max(v => v.y);
            var upper = vertices.Where(v => v.y > height * 0.87f).ToArray();
            float headZ = upper.Average(v => v.z);
            Vector3 pivot = new Vector3(0, height * 0.56f, headZ * 0.85f);
            var weights = new BoneWeight[vertices.Count];
            float sign = Mathf.Sign(headZ);
            for (int i = 0; i < weights.Length; i++)
            {
                float w = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(height * 0.53f, height * 0.79f, vertices[i].y));
                w *= Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.02f, 0.07f, vertices[i].z * sign));
                weights[i] = new BoneWeight { boneIndex0 = 0, weight0 = 1-w, boneIndex1 = 1, weight1 = w };
            }
            var mesh = new Mesh { name = "FarmHen", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetNormals(outN); mesh.SetUVs(0,outUV); mesh.SetTriangles(triangles,0);
            mesh.boneWeights = weights; mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.TRS(pivot,Quaternion.identity,Vector3.one).inverse };
            mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,path); AssetDatabase.SaveAssets(); Object.DestroyImmediate(probe); return mesh;
        }
    }
}
