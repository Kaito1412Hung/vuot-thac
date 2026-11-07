using System.Collections.Generic;
using UnityEngine;

namespace VuotThac.Boats
{
    // Runtime visibility budget only: no scenery is moved or removed from the saved map.
    public class MobileSceneryBudget : MonoBehaviour
    {
        CullingGroup group;
        Renderer[] renderers;
        int[] distanceLimits;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Application.isMobilePlatform && Camera.main != null)
                Camera.main.gameObject.AddComponent<MobileSceneryBudget>();
        }
        void Start()
        {
            var camera = GetComponent<Camera>();
            camera.farClipPlane = Mathf.Min(camera.farClipPlane, 650);
            var track = FindAnyObjectByType<VuotThac.Track.RiverTrack>();
            if (track == null) return;
            var generated = track.transform.Find("Generated");
            if (generated == null) return;
            var targets = new List<Renderer>();
            var spheres = new List<BoundingSphere>();
            var limits = new List<int>();
            foreach (var r in generated.GetComponentsInChildren<Renderer>())
            {
                float radius = r.bounds.extents.magnitude;
                // Large terrain, river and cave meshes retain normal camera clipping.
                if (radius > 20 || r is SkinnedMeshRenderer) continue;
                targets.Add(r); spheres.Add(new BoundingSphere(r.bounds.center, radius));
                limits.Add(radius < 0.7f ? 1 : radius < 3 ? 2 : 3);
            }
            renderers = targets.ToArray(); distanceLimits = limits.ToArray();
            group = new CullingGroup { targetCamera = camera };
            group.SetBoundingSpheres(spheres.ToArray());
            group.SetBoundingSphereCount(renderers.Length);
            group.SetDistanceReferencePoint(camera.transform);
            group.SetBoundingDistances(new[] { 65f, 150f, 320f });
            group.onStateChanged = e => {
                var r = renderers[e.index];
                if (r != null) r.forceRenderingOff = e.currentDistance >= distanceLimits[e.index];
            };
        }
        void OnDestroy()
        {
            group?.Dispose();
            if (renderers == null) return;
            foreach (var r in renderers) if (r != null) r.forceRenderingOff = false;
        }
    }
}
