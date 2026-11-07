using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using G = VuotThac.EditorTools.Track.BotanicalFlowerMesh.Geometry;

namespace VuotThac.EditorTools.Track
{
    static class VillagerAppearance
    {
        const string Folder = "Assets/ThirdParty/People/Grab3D/Appearance";
        public sealed class Template
        {
            public Mesh body, garment, leftShoe, rightShoe;
            public float bottom, top;
        }

        public static Template Prepare(GameObject source, string key)
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/ThirdParty/People/Grab3D", "Appearance");
            var probe = Object.Instantiate(source); probe.hideFlags = HideFlags.HideAndDontSave;
            var animator = probe.GetComponent<Animator>();
            var r = probe.GetComponentInChildren<SkinnedMeshRenderer>();
            var original = r.sharedMesh;
            var baked = new Mesh(); r.BakeMesh(baked);
            var p = baked.vertices.Select(v => probe.transform.InverseTransformPoint(r.transform.TransformPoint(v))).ToArray();
            var template = new Template { bottom = p.Min(v => v.y), top = p.Max(v => v.y) };
            string path = Folder + "/" + key + "_v2.asset";
            var saved = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(m => m.name);
            if (saved.ContainsKey("IdentityBody"))
            {
                template.body = saved["IdentityBody"]; template.garment = saved["WorkVest"];
                template.leftShoe = saved["LeftWorkBoot"]; template.rightShoe = saved["RightWorkBoot"];
            }
            else
            {
                int left = System.Array.IndexOf(r.bones, animator.GetBoneTransform(HumanBodyBones.LeftFoot));
                int right = System.Array.IndexOf(r.bones, animator.GetBoneTransform(HumanBodyBones.RightFoot));
                int leftToe = System.Array.IndexOf(r.bones, animator.GetBoneTransform(HumanBodyBones.LeftToes));
                int rightToe = System.Array.IndexOf(r.bones, animator.GetBoneTransform(HumanBodyBones.RightToes));
                var weights = original.boneWeights;
                bool[] foot = new bool[p.Length];
                for (int i = 0; i < p.Length; i++) foot[i] = p[i].y < template.bottom + 0.18f
                    && Influence(weights[i], left, right, leftToe, rightToe) > 0.2f;
                template.body = Object.Instantiate(original); template.body.name = "IdentityBody";
                for (int sub = 0; sub < original.subMeshCount; sub++)
                {
                    var src = original.GetTriangles(sub); var indices = new List<int>();
                    for (int i = 0; i < src.Length; i += 3)
                        if (!(foot[src[i]] && foot[src[i + 1]] && foot[src[i + 2]])) indices.AddRange(new[] { src[i], src[i + 1], src[i + 2] });
                    template.body.SetTriangles(indices, sub);
                }
                AddIdentities(template.body, p, r, probe.transform, template.top);
                template.leftShoe = Shoe("LeftWorkBoot", p, weights, left, leftToe, animator.GetBoneTransform(HumanBodyBones.LeftFoot), probe.transform, template.bottom);
                template.rightShoe = Shoe("RightWorkBoot", p, weights, right, rightToe, animator.GetBoneTransform(HumanBodyBones.RightFoot), probe.transform, template.bottom);
                template.garment = Garment(original, p, r, probe.transform, animator);
                AssetDatabase.CreateAsset(new Mesh { name = key + "Appearance" }, path);
                foreach (var mesh in new[] { template.body, template.garment, template.leftShoe, template.rightShoe }) AssetDatabase.AddObjectToAsset(mesh, path);
                AssetDatabase.SaveAssets();
            }
            Object.DestroyImmediate(baked); Object.DestroyImmediate(probe);
            return template;
        }

        static float Influence(BoneWeight w, params int[] bones)
        {
            float sum = 0;
            if (bones.Contains(w.boneIndex0)) sum += w.weight0;
            if (bones.Contains(w.boneIndex1)) sum += w.weight1;
            if (bones.Contains(w.boneIndex2)) sum += w.weight2;
            if (bones.Contains(w.boneIndex3)) sum += w.weight3;
            return sum;
        }

        static void AddIdentities(Mesh mesh, Vector3[] p, SkinnedMeshRenderer r, Transform root, float top)
        {
            string[] names = { "FaceWidth", "Jawline", "NoseBridge", "CheeksAndChin" };
            Matrix4x4 toMesh = r.transform.worldToLocalMatrix * root.localToWorldMatrix;
            for (int shape = 0; shape < names.Length; shape++)
            {
                var delta = new Vector3[p.Length];
                for (int i = 0; i < p.Length; i++)
                {
                    var v = p[i];
                    float gate = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(top - 0.36f, top - 0.28f, v.y));
                    if (gate <= 0) continue;
                    float lower = Mathf.Exp(-Mathf.Pow((v.y - (top - 0.24f)) / 0.075f, 2));
                    var d = Vector3.zero;
                    if (shape == 0) d.x = v.x * 0.08f;
                    if (shape == 1) { d.x = v.x * lower * 0.17f; d.y = -lower * 0.005f; }
                    if (shape == 2) d.z = 0.009f * Mathf.Exp(-Mathf.Pow(v.x / 0.025f, 2) - Mathf.Pow((v.y - (top - 0.165f)) / 0.04f, 2)) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.02f, 0.09f, v.z));
                    if (shape == 3) { d.x = Mathf.Sign(v.x) * 0.006f * Mathf.Exp(-Mathf.Pow((Mathf.Abs(v.x) - 0.055f) / 0.035f, 2)) * lower; d.y = lower * 0.006f; }
                    delta[i] = toMesh.MultiplyVector(d * gate);
                }
                mesh.AddBlendShapeFrame(names[shape], 100, delta, null, null);
            }
        }

        static Mesh Shoe(string name, Vector3[] p, BoneWeight[] weights, int foot, int toe, Transform bone, Transform root, float bottom)
        {
            var points = p.Where((v, i) => v.y < bottom + 0.22f && Influence(weights[i], foot, toe) > 0.25f).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero); foreach (var v in points) bounds.Encapsulate(v);
            var g = new G();
            float width = Mathf.Clamp(bounds.extents.x + 0.008f, 0.05f, 0.073f);
            float length = Mathf.Clamp(bounds.extents.z + 0.014f, 0.12f, 0.16f);
            var ankle = root.InverseTransformPoint(bone.position);
            float[] heights = { -0.016f, 0.005f, 0.025f, 0.060f, 0.105f, 0.140f, 0.190f, 0.235f };
            float[] widths = { 1f, 1f, 0.98f, 0.96f, 0.90f, 0.75f, 0.70f, 0.70f };
            float[] lengths = { 1f, 1f, 0.99f, 0.96f, 0.85f, 0.58f, 0.44f, 0.44f };
            const int sides = 40;
            for (int row = 0; row < heights.Length; row++)
                for (int i = 0; i <= sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    float blend = Mathf.InverseLerp(0.08f, 0.19f, heights[row]);
                    float centerZ = Mathf.Lerp(bounds.center.z, ankle.z, blend);
                    float heel = Mathf.Sin(a) < 0 ? 0.88f : 1;
                    g.vertices.Add(new Vector3(bounds.center.x + Mathf.Cos(a) * width * widths[row] * heel,
                        bounds.min.y + heights[row], centerZ + Mathf.Sin(a) * length * lengths[row]));
                    g.normals.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                    g.uv.Add(new Vector2(i / (float)sides, row / 7f));
                }
            for (int row = 0; row < heights.Length - 1; row++)
                for (int i = 0; i < sides; i++)
                {
                    int a = row * (sides + 1) + i, b = a + sides + 1;
                    int mat = row < 2 ? 1 : 0;
                    g.triangles[mat].AddRange(new[] { a, b, b + 1, a, b + 1, a + 1 });
                }
            for (int lace = 0; lace < 4; lace++)
                g.Ellipsoid(new Vector3(bounds.center.x, bounds.min.y + 0.136f - lace * 0.008f, bounds.center.z - 0.018f + lace * 0.023f), new Vector3(width * 0.45f, 0.003f, 0.003f), Quaternion.identity, 2, 12, 5);
            Matrix4x4 toBone = bone.worldToLocalMatrix * root.localToWorldMatrix;
            for (int i = 0; i < g.vertices.Count; i++) { g.vertices[i] = toBone.MultiplyPoint3x4(g.vertices[i]); g.normals[i] = toBone.MultiplyVector(g.normals[i]).normalized; }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(g.vertices); mesh.SetNormals(g.normals); mesh.SetUVs(0, g.uv); mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(g.triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh Garment(Mesh original, Vector3[] p, SkinnedMeshRenderer r, Transform root, Animator animator)
        {
            float hip = root.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position).y;
            float shoulder = root.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position).y;
            var selected = new List<int>();
            foreach (int sub in Enumerable.Range(0, original.subMeshCount))
            {
                var tris = original.GetTriangles(sub);
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var v = (p[tris[i]] + p[tris[i + 1]] + p[tris[i + 2]]) / 3;
                    if (v.y > hip - 0.10f && v.y < shoulder - 0.04f && Mathf.Abs(v.x) < 0.23f) selected.AddRange(new[] { tris[i], tris[i + 1], tris[i + 2] });
                }
            }
            var used = selected.Distinct().ToArray(); var map = new Dictionary<int, int>();
            var vertices = original.vertices; var normals = original.normals; var uv = original.uv; var weights = original.boneWeights;
            Matrix4x4 toMesh = r.transform.worldToLocalMatrix * root.localToWorldMatrix;
            var outV = new Vector3[used.Length]; var outN = new Vector3[used.Length]; var outUV = new Vector2[used.Length]; var outW = new BoneWeight[used.Length];
            for (int i = 0; i < used.Length; i++)
            {
                int j = used[i]; map[j] = i;
                var offset = new Vector3(p[j].x * 0.07f + Mathf.Sign(p[j].x) * 0.006f, 0, p[j].z * 0.08f + Mathf.Sign(p[j].z) * 0.012f);
                outV[i] = vertices[j] + toMesh.MultiplyVector(offset); outN[i] = normals[j]; outUV[i] = uv[j]; outW[i] = weights[j];
            }
            var mesh = new Mesh { name = "WorkVest", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = outV; mesh.normals = outN; mesh.uv = outUV; mesh.boneWeights = outW; mesh.bindposes = original.bindposes;
            mesh.triangles = selected.Select(j => map[j]).ToArray(); mesh.RecalculateBounds(); return mesh;
        }

        public static void Apply(Template template, GameObject model, Animator animator, int identity, Material outfit, Material leather, Material sole, Material laces)
        {
            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(); renderer.sharedMesh = template.body;
            float[] values = { -70, -42, -14, 18, 46, 75 };
            for (int shape = 0; shape < 4; shape++) renderer.SetBlendShapeWeight(shape, values[(identity * (shape == 2 ? 5 : 1) + shape * 2) % values.Length]);
            var garment = new GameObject("WorkVest"); garment.transform.SetParent(renderer.transform, false);
            var cloth = garment.AddComponent<SkinnedMeshRenderer>(); cloth.sharedMesh = template.garment; cloth.bones = renderer.bones; cloth.rootBone = renderer.rootBone;
            cloth.localBounds = renderer.localBounds; cloth.sharedMaterial = outfit; cloth.updateWhenOffscreen = true;
            foreach (bool left in new[] { true, false })
            {
                var go = new GameObject(left ? "LeftWorkBoot" : "RightWorkBoot"); go.transform.SetParent(animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot), false);
                go.AddComponent<MeshFilter>().sharedMesh = left ? template.leftShoe : template.rightShoe;
                go.AddComponent<MeshRenderer>().sharedMaterials = new[] { leather, sole, laces };
            }
        }
    }
}
