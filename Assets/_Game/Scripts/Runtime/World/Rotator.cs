using UnityEngine;

namespace VuotThac.World
{
    /// <summary>Quay đều quanh trục của chính nó, dùng cho bánh xe cối xay nước.</summary>
    public class Rotator : MonoBehaviour
    {
        public Vector3 degreesPerSecond = new(30f, 0f, 0f);

        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
