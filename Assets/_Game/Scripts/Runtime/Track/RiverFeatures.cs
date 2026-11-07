using System;
using UnityEngine;

namespace VuotThac.Track
{
    /// <summary>
    /// Một thác nước trên sông chính: mặt nước tụt xuống <see cref="height"/> mét trong khoảng <see cref="length"/> mét.
    /// Phía trên thác cả dòng sông được nâng lên, nên bờ sông hai bên cũng thành vách đá dựng theo.
    /// </summary>
    [Serializable]
    public class RiverFall
    {
        [Tooltip("Vị trí thác, tính bằng mét từ đầu sông")]
        public float distance = 300f;
        [Tooltip("Thác cao bao nhiêu mét")]
        [Min(0.5f)] public float height = 4f;
        [Tooltip("Đoạn nước đổ dài bao nhiêu mét. Ngắn thì dốc đứng hơn")]
        [Min(2f)] public float length = 9f;
    }

    /// <summary>
    /// Một hầm xuyên núi: một dãy núi chắn ngang thung lũng, sông chui qua đường hầm đào xuyên dãy núi đó.
    /// </summary>
    [Serializable]
    public class RiverTunnel
    {
        [Tooltip("Giữa hầm nằm ở mét này, tính từ đầu sông")]
        public float distance = 500f;
        [Tooltip("Hầm dài bao nhiêu mét")]
        [Min(10f)] public float length = 40f;
        [Tooltip("Dãy núi chắn ngang cao bao nhiêu mét so với mặt nước")]
        [Min(8f)] public float ridgeHeight = 24f;
        [Tooltip("Vòm hầm cao bao nhiêu mét so với mặt nước")]
        [Min(4f)] public float ceilingHeight = 11f;
    }
}
