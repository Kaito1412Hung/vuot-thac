using UnityEngine;
using UnityEngine.Rendering;

namespace VuotThac.Track
{
    /// <summary>
    /// Gửi các vùng hang lên shader và báo cho shader biết camera đang ở trong hang nào.
    /// Phép thử "camera ở trong hang" làm một lần cho mỗi camera trên CPU, shader chỉ đọc kết quả.
    /// </summary>
    [ExecuteAlways]
    public class CaveInteriorVisibility : MonoBehaviour
    {
        public Vector4[] centers = new Vector4[0], axes = new Vector4[0];
        readonly Vector4[] gpuCenters = new Vector4[256], gpuAxes = new Vector4[256];
        static readonly int CountId = Shader.PropertyToID("_CaveInteriorCount");
        static readonly int CentersId = Shader.PropertyToID("_CaveStableCenters");
        static readonly int AxesId = Shader.PropertyToID("_CaveStableAxes");
        static readonly int OwnerId = Shader.PropertyToID("_CaveCameraOwner");
        static readonly int OwnerCountId = Shader.PropertyToID("_CaveRoomCount");
        static readonly int OwnerCentersId = Shader.PropertyToID("_CaveRoomCenters");
        static readonly int OwnerAxesId = Shader.PropertyToID("_CaveRoomAxes");
        const int MaxOwnerVolumes = 96;
        readonly Vector4[] ownerCenters = new Vector4[MaxOwnerVolumes], ownerAxes = new Vector4[MaxOwnerVolumes];
        int uploadedCount = -1;
        float uploadedOwner = -1f;

        public void Upload()
        {
            int count = Mathf.Min(256, Mathf.Min(centers.Length, axes.Length));
            uploadedCount = count;
            Shader.SetGlobalInt(CountId, count);
            if (count == 0)
                return;
            System.Array.Copy(centers, gpuCenters, count);
            System.Array.Copy(axes, gpuAxes, count);
            Shader.SetGlobalVectorArray(CentersId, gpuCenters);
            Shader.SetGlobalVectorArray(AxesId, gpuAxes);
        }

        /// <summary>Giống hệt phép thử cũ trong shader: trả về chủ của vùng hang chứa điểm, 0 nếu ở ngoài.</summary>
        public float OwnerAt(Vector3 p)
        {
            int count = Mathf.Min(256, Mathf.Min(centers.Length, axes.Length));
            for (int i = count - 1; i >= 0; i--)
            {
                Vector4 c = centers[i], a = axes[i];
                float dx = p.x - c.x, dy = p.y - c.y, dz = p.z - c.z;
                float owner = Mathf.Floor(a.w / 100f), halfLength = a.w - owner * 100f;
                if (dy < -0.4f || dy > a.z + 1f || Mathf.Abs(-dx * a.y + dz * a.x) > halfLength)
                    continue;
                float x = (dx * a.x + dz * a.y) / c.w, y = (dy + 1.8f) / (a.z + 1.8f);
                if (x * x + y * y < 1.04f)
                    return Mathf.Max(owner, 1f);
            }
            return 0f;
        }

        void BeginCamera(ScriptableRenderContext context, Camera cam)
        {
            // Mảng vùng hang chỉ gửi lại khi số vùng đổi (ví dụ vừa dựng lại map), không gửi mỗi khung hình
            if (uploadedCount != Mathf.Min(256, Mathf.Min(centers.Length, axes.Length)))
            {
                Upload();
                uploadedOwner = -1f;
            }
            Vector3 pos = cam.transform.position;
            float owner = OwnerAt(pos);
            Shader.SetGlobalFloat(OwnerId, owner);
            // Chỉ gửi lại danh sách vùng khi đổi hang hoặc camera đã đi được một quãng
            if (owner != uploadedOwner || (owner > 0.5f && (pos - uploadedAt).sqrMagnitude > RefreshMove * RefreshMove))
                UploadOwner(owner, pos);
        }

        const float NearRadius = 70f;   // chỉ cắt núi trong phạm vi nhìn thấy được trong lòng hang
        const int NearLimit = 24;        // mỗi điểm ảnh xét tối đa bấy nhiêu vùng
        const float RefreshMove = 6f;
        Vector3 uploadedAt;
        readonly float[] nearDist = new float[MaxOwnerVolumes];
        readonly int[] nearIndex = new int[MaxOwnerVolumes];

        /// <summary>
        /// Lọc các vùng của hang camera đang ở và nằm gần camera, gần nhất trước. Shader chạy vòng lặp này cho từng
        /// điểm ảnh của đất và núi (kể cả phần bị vách hang che), nên càng ít vùng càng nhẹ.
        /// </summary>
        void UploadOwner(float owner, Vector3 pos)
        {
            uploadedOwner = owner;
            uploadedAt = pos;
            int n = 0;
            if (owner > 0.5f)
            {
                int count = Mathf.Min(256, Mathf.Min(centers.Length, axes.Length));
                int found = 0;
                for (int i = 0; i < count && found < MaxOwnerVolumes; i++)
                {
                    float o = Mathf.Max(Mathf.Floor(axes[i].w / 100f), 1f);
                    if (Mathf.Abs(o - owner) > 0.1f)
                        continue;
                    float halfLength = axes[i].w - Mathf.Floor(axes[i].w / 100f) * 100f;
                    float dist = Vector3.Distance(pos, centers[i]) - halfLength - centers[i].w;
                    if (dist > NearRadius)
                        continue;
                    // Chèn vào danh sách đã xếp theo khoảng cách
                    int j = found++;
                    while (j > 0 && nearDist[j - 1] > dist)
                    {
                        nearDist[j] = nearDist[j - 1];
                        nearIndex[j] = nearIndex[j - 1];
                        j--;
                    }
                    nearDist[j] = dist;
                    nearIndex[j] = i;
                }
                for (int k = 0; k < found && n < NearLimit; k++)
                {
                    ownerCenters[n] = centers[nearIndex[k]];
                    ownerAxes[n] = axes[nearIndex[k]];
                    n++;
                }
            }
            Shader.SetGlobalInt(OwnerCountId, n);
            if (n > 0)
            {
                Shader.SetGlobalVectorArray(OwnerCentersId, ownerCenters);
                Shader.SetGlobalVectorArray(OwnerAxesId, ownerAxes);
            }
        }

        void OnEnable()
        {
            Upload();
            RenderPipelineManager.beginCameraRendering += BeginCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            Shader.SetGlobalInt(CountId, 0);
            Shader.SetGlobalFloat(OwnerId, 0f);
            Shader.SetGlobalInt(OwnerCountId, 0);
            uploadedOwner = -1f;
            uploadedAt = Vector3.zero;
        }

        void OnValidate()
        {
            uploadedCount = -1;
            uploadedOwner = -1f;
        }
    }
}
