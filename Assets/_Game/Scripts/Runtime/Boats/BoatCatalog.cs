using UnityEngine;

namespace VuotThac.Boats
{
    /// <summary>Danh sách thuyền hiện trên màn chọn thuyền, theo đúng thứ tự.</summary>
    [CreateAssetMenu(fileName = "BoatCatalog", menuName = "Vượt Thác/Danh sách thuyền")]
    public class BoatCatalog : ScriptableObject
    {
        public BoatDefinition[] boats;
    }
}
