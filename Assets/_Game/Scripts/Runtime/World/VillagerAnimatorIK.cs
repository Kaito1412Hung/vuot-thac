using UnityEngine;

namespace VuotThac.World
{
    [RequireComponent(typeof(Animator))]
    public class VillagerAnimatorIK : MonoBehaviour
    {
        public RealisticVillager owner;
        void OnAnimatorIK(int layerIndex)
        {
            if (owner != null) owner.ApplyAnimatorIK();
        }
    }
}
