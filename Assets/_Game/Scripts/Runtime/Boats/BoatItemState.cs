using UnityEngine;

namespace VuotThac.Boats
{
    public enum RaceItem { None, Boost, Shield, Log }

    public sealed class BoatItemState : MonoBehaviour
    {
        public RaceItem Held { get; private set; }
        public float BoostRemaining { get; private set; }
        public bool ShieldActive { get; private set; }
        public int ShieldsConsumed { get; private set; }
        public int PickupsCollected {get;private set;}
        public int ItemsUsed {get;private set;}
        public int BoostUses {get;private set;}
        public int ShieldUses {get;private set;}
        public int LogUses {get;private set;}
        public RaceItems Arena { get; set; }
        LineRenderer shieldRing;
        public bool Give(RaceItem item)
        {
            if(item==RaceItem.None||Held!=RaceItem.None)return false;
            Held=item;PickupsCollected++;return true;
        }
        public bool Use()
        {
            var boat=GetComponent<BoatController>();var progress=GetComponent<RaceProgress>();
            if(Arena==null||!Arena.CanUse||Held==RaceItem.None||progress==null||progress.Finished||progress.Withdrawn)return false;
            if(Held==RaceItem.Log&&!Arena.DropLog(boat))return false;
            if(Held==RaceItem.Boost){BoostRemaining=2;BoostUses++;}
            if(Held==RaceItem.Log)LogUses++;
            if(Held==RaceItem.Shield)
            {
                if(ShieldActive)return false;
                ShieldActive=true;
                ShieldUses++;
                if(shieldRing==null)
                {
                    var go=new GameObject("ItemShield");go.transform.SetParent(transform,false);shieldRing=go.AddComponent<LineRenderer>();
                    shieldRing.useWorldSpace=false;shieldRing.loop=true;shieldRing.positionCount=40;shieldRing.widthMultiplier=.045f;
                    shieldRing.sharedMaterial=Arena.ShieldMaterial;
                    for(int i=0;i<40;i++){float a=i*Mathf.PI*2/40;shieldRing.SetPosition(i,new Vector3(Mathf.Cos(a)*(boat.hullBeam*.6f+.2f),.35f,Mathf.Sin(a)*(boat.hullLength*.6f+.2f)));}
                }
                shieldRing.enabled=true;
            }
            Held=RaceItem.None;ItemsUsed++;return true;
        }
        public bool AbsorbImpact()
        {
            if(!ShieldActive)return false;
            ShieldActive=false;ShieldsConsumed++;if(shieldRing!=null)shieldRing.enabled=false;return true;
        }
        public void Tick(float dt){BoostRemaining=Mathf.Max(0,BoostRemaining-dt);}
        public void ClearEffects(){Held=RaceItem.None;BoostRemaining=0;ShieldActive=false;if(shieldRing!=null)shieldRing.enabled=false;}
        public static string ItemName(RaceItem item)=>item==RaceItem.Boost?"TĂNG TỐC":item==RaceItem.Shield?"KHIÊN":item==RaceItem.Log?"KHÚC GỖ":"CHƯA CÓ VẬT PHẨM";
    }
}
