using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VuotThac.Track;

namespace VuotThac.EditorTools.Track
{
    /// <summary>
    /// Giới hạn số đèn thật trong map. Các bước trang trí hang đặt hàng trăm đèn điểm và đèn rọi đổ bóng,
    /// mỗi đèn bắt máy tính thêm ánh sáng (và vẽ lại cả cảnh nếu đổ bóng) mỗi khung hình nên rất lag,
    /// trên điện thoại càng không chịu nổi. Mỗi hang chỉ giữ vài đèn rải đều, không đổ bóng, sáng hơn một chút để bù.
    /// Ánh sáng còn lại trong hang dựa vào vật liệu tự phát sáng (tinh thể, đèn lồng) nên gần như không tốn gì.
    /// </summary>
    public static class CaveLightBudget
    {
        const int MaxPerCave = 3;
        const float MaxRange = 24f;
        const float Boost = 1.4f;

        [MenuItem("Vượt Thác/Giảm đèn trong hang (chống lag)")]
        static void ApplyToOpenScene()
        {
            var track = Object.FindAnyObjectByType<RiverTrack>();
            if (track == null)
            {
                Debug.LogWarning("[Vượt Thác] Scene đang mở không có RiverTrack nào.");
                return;
            }
            Apply(track);
            EditorSceneManager.MarkSceneDirty(track.gameObject.scene);
            EditorSceneManager.SaveScene(track.gameObject.scene);
        }

        public static void Apply(RiverTrack track)
        {
            var generated = track.transform.Find("Generated");
            if (generated == null)
                return;

            // Gom đèn theo từng hang: Generated/<nhóm>/<hang>/...
            var groups = new Dictionary<Transform, List<Light>>();
            int before = 0;
            foreach (var light in generated.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional)
                    continue;
                before++;
                var key = light.transform;
                while (key.parent != null && key.parent.parent != generated && key.parent != generated)
                    key = key.parent;
                if (!groups.TryGetValue(key, out var list))
                    groups[key] = list = new List<Light>();
                list.Add(light);
            }

            int kept = 0, removed = 0;
            foreach (var pair in groups)
            {
                var keep = PickSpread(pair.Value, MaxPerCave);
                foreach (var light in pair.Value)
                {
                    if (keep.Contains(light))
                    {
                        light.shadows = LightShadows.None;
                        light.range = Mathf.Min(light.range * 1.25f, MaxRange);
                        light.intensity *= Boost;
                        EditorUtility.SetDirty(light);
                        kept++;
                        continue;
                    }
                    var go = light.gameObject;
                    // URP gắn kèm mỗi đèn một UniversalAdditionalLightData bắt buộc phải có Light đi cùng,
                    // nên không gỡ riêng Light được. Vật chỉ chứa đèn thì xóa cả vật, còn không thì gỡ phần phụ trước rồi mới gỡ đèn.
                    bool onlyLight = go.transform.childCount == 0;
                    foreach (var c in go.GetComponents<Component>())
                        if (!(c is Transform) && !(c is Light) && !IsLightExtra(c))
                            onlyLight = false;
                    if (onlyLight)
                        Object.DestroyImmediate(go);
                    else
                    {
                        foreach (var c in go.GetComponents<Component>())
                            if (IsLightExtra(c))
                                Object.DestroyImmediate(c);
                        Object.DestroyImmediate(light);
                    }
                    removed++;
                }
            }
            int left = generated.GetComponentsInChildren<Light>(true).Length;
            Debug.Log($"[Vượt Thác] Đèn trong map: {before} → {left} (bỏ {removed}, không đèn nào đổ bóng).", track);
        }

        static bool IsLightExtra(Component c) => c != null && c.GetType().Name == "UniversalAdditionalLightData";

        /// <summary>Lấy đèn sáng nhất rồi lần lượt lấy đèn xa các đèn đã chọn nhất, cho ánh sáng rải đều trong hang.</summary>
        static HashSet<Light> PickSpread(List<Light> lights, int count)
        {
            var chosen = new HashSet<Light>();
            if (lights.Count == 0)
                return chosen;
            Light first = lights[0];
            foreach (var l in lights)
                if (l.intensity * l.range > first.intensity * first.range)
                    first = l;
            chosen.Add(first);
            while (chosen.Count < Mathf.Min(count, lights.Count))
            {
                Light best = null;
                float bestDist = -1f;
                foreach (var l in lights)
                {
                    if (chosen.Contains(l))
                        continue;
                    float near = float.MaxValue;
                    foreach (var c in chosen)
                        near = Mathf.Min(near, (l.transform.position - c.transform.position).sqrMagnitude);
                    if (near > bestDist)
                    {
                        bestDist = near;
                        best = l;
                    }
                }
                chosen.Add(best);
            }
            return chosen;
        }
    }
}
