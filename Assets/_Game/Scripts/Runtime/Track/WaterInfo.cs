using UnityEngine;

namespace VuotThac.Track
{
    /// <summary>Kết quả tra cứu mặt nước tại một vị trí, xem <see cref="RiverTrack.SampleWater"/>.</summary>
    public struct WaterInfo
    {
        /// <summary>Vị trí nằm trong lòng sông hoặc nhánh, không phải trên bờ.</summary>
        public bool inWater;
        /// <summary>Độ cao mặt nước.</summary>
        public float height;
        /// <summary>Vận tốc nước chảy, mét mỗi giây.</summary>
        public Vector3 flow;
        /// <summary>Điểm giữa dòng gần nhất, theo tọa độ thật trong scene. Dùng để đưa thuyền về khi bị kẹt.</summary>
        public Vector3 center;
        /// <summary>Hướng xuôi dòng.</summary>
        public Vector3 tangent;
        /// <summary>-1 là sông chính, từ 0 trở lên là số thứ tự nhánh.</summary>
        public int channel;
        /// <summary>Khoảng cách tới mép nước. Âm là đang ở trong lòng sông.</summary>
        public float edgeDistance;
        /// <summary>Quãng đường dọc theo dòng (sông chính hoặc nhánh) tính từ đầu dòng đó.</summary>
        public float distanceAlong;
    }
}
