using UnityEngine;

namespace VuotThac.Boats
{
    /// <summary>
    /// Thông tin một mẫu thuyền người chơi chọn được. Bốn chỉ số từ 1 đến 10 hiện trên màn chọn thuyền,
    /// còn các thông số vật lý thật được tính ra từ bốn chỉ số đó để mọi thuyền cân bằng theo cùng một công thức.
    /// </summary>
    [CreateAssetMenu(fileName = "Boat", menuName = "Vượt Thác/Thuyền")]
    public class BoatDefinition : ScriptableObject
    {
        public string displayName = "Thuyền";
        [TextArea(2, 4)] public string description;
        public GameObject prefab;

        [Header("Chỉ số (1 đến 10)")]
        [Range(1, 10)] public int speed = 6;
        [Range(1, 10)] public int acceleration = 6;
        [Range(1, 10)] public int handling = 6;
        [Range(1, 10)] public int toughness = 6;

        /// <summary>Tốc độ tối đa so với mặt nước, mét mỗi giây. Nước chảy cộng thêm vào.</summary>
        public float MaxSpeed => 7f + speed * 0.8f;
        /// <summary>Gia tốc khi chèo hết sức, mét mỗi giây bình phương.</summary>
        public float Acceleration => 2f + acceleration * 0.6f;
        /// <summary>Tốc độ quay đầu tối đa, độ mỗi giây.</summary>
        public float TurnRate => 40f + handling * 9f;
        /// <summary>Độ bám nước theo chiều ngang. Cao thì ít bị trượt ngang khi cua.</summary>
        public float Grip => 1.5f + handling * 0.25f;
        /// <summary>Phần tốc độ còn giữ lại sau khi đâm vào đá hay bờ.</summary>
        public float CollisionKeep => 0.35f + toughness * 0.055f;
        /// <summary>Thời gian chèo yếu đi sau khi đâm, giây.</summary>
        public float StunTime => Mathf.Max(0.1f, 0.6f - toughness * 0.045f);
        /// <summary>Lực tự dựng thẳng lại khi bị nghiêng.</summary>
        public float Stability => 6f + toughness * 0.6f;
        public float Mass => 120f + toughness * 40f;
    }
}
