using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VuotThac.Boats
{
    public partial class BoatSelectScreen
    {
        readonly CupSeries cup=new CupSeries();
        bool cupSelected,roundCommitted;
        int cupRound;
        int[] cupBoatRoster;
        float finishWait;
        Text itemText,modeText;
        ItemIconGraphic itemIcon;
        Button itemButton,nextRoundButton,retryButton;
        void BuildPhaseFourUI(Transform panel)
        {
            var mode=Button(panel,"RaceMode","CHẾ ĐỘ: ĐUA 1 LƯỢT >",new Color(.26f,.25f,.38f),()=>
            {cupSelected=!cupSelected;modeText.text=cupSelected?"CHẾ ĐỘ: CÚP 3 LƯỢT >":"CHẾ ĐỘ: ĐUA 1 LƯỢT >";});
            Place(mode,new Vector2(.5f,0),new Vector2(0,282),new Vector2(580,56));modeText=mode.GetComponentInChildren<Text>();modeText.fontSize=27;
            var use=Button(hudRoot.transform,"UseItem","",new Color(.1f,.36f,.48f,.95f),UseHeldItem);
            Place(use,new Vector2(.5f,0),new Vector2(0,95),new Vector2(470,104));itemButton=use.GetComponent<Button>();itemText=use.GetComponentInChildren<Text>();itemText.fontSize=25;itemText.alignment=TextAnchor.MiddleLeft;
            itemText.rectTransform.offsetMin=new Vector2(112,8);itemText.rectTransform.offsetMax=new Vector2(-12,-8);
            var iconRect=Rect(use,"SkillIcon",Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
            Place(iconRect,new Vector2(0,.5f),new Vector2(56,0),new Vector2(90,90));itemIcon=iconRect.gameObject.AddComponent<ItemIconGraphic>();itemIcon.raycastTarget=false;
            use.GetComponent<Image>().color=new Color(.035f,.10f,.14f,.96f);
            var retry=Button(finishRoot.transform,"Retry","CHƠI LẠI",new Color(.15f,.4f,.58f),RestartPhaseFour);
            retryButton=retry.GetComponent<Button>();
            Place(retry,new Vector2(.5f,.12f),new Vector2(-210,0),new Vector2(390,80));retry.GetComponentInChildren<Text>().fontSize=30;
            var next=Button(finishRoot.transform,"NextCupRound","LƯỢT TIẾP",new Color(.58f,.39f,.1f),ContinueCup);
            Place(next,new Vector2(.5f,.12f),new Vector2(210,0),new Vector2(390,80));nextRoundButton=next.GetComponent<Button>();next.GetComponentInChildren<Text>().fontSize=30;next.gameObject.SetActive(false);
        }
        void PreparePhaseFourRound()
        {
            if(cupSelected&&!cup.Active)
            {
                cup.Start();cupBoatRoster=BoatRace.DrawOpponents(catalog,System.Guid.NewGuid().GetHashCode());
            }
            if(!cup.Active)cupBoatRoster=null;
            cupRound=cup.Active?cup.CompletedRounds+1:1;roundCommitted=false;finishWait=0;
        }
        void UseHeldItem()
        {
            if(state!=State.Playing||onlinePlaying||player==null)return;
            player.GetComponent<BoatItemState>()?.Use();
        }
        void TickPhaseFour(float dt)
        {
            if(onlinePlaying){itemButton.gameObject.SetActive(false);nextRoundButton.gameObject.SetActive(false);retryButton.gameObject.SetActive(false);return;}
            retryButton.gameObject.SetActive(true);
            itemButton.gameObject.SetActive(state==State.Playing);
            if(player!=null)
            {
                var held=player.GetComponent<BoatItemState>();
                if(held!=null)
                {
                    var visibleItem=held.Held!=RaceItem.None?held.Held:held.BoostRemaining>0?RaceItem.Boost:held.ShieldActive?RaceItem.Shield:RaceItem.None;
                    itemIcon.Show(visibleItem,held.Held==RaceItem.None&&held.BoostRemaining>0?held.BoostRemaining/2:0);
                    itemText.text=held.Held!=RaceItem.None?BoatItemState.ItemName(held.Held)+"\n[E / X] DÙNG":held.BoostRemaining>0?$"TĂNG TỐC\nCòn {held.BoostRemaining:0.0} giây":held.ShieldActive?"KHIÊN\nĐang bảo vệ":"CHƯA CÓ VẬT PHẨM\nNhặt biểu tượng trên nước";
                    itemButton.interactable=race.CountdownRemaining<=0&&held.Held!=RaceItem.None;
                }
            }
            if(state==State.Playing&&((Keyboard.current!=null&&Keyboard.current.eKey.wasPressedThisFrame)||(Gamepad.current!=null&&Gamepad.current.buttonWest.wasPressedThisFrame)))UseHeldItem();
            if(race.Elapsed>=420)
            {
                foreach(var racer in race.Racers)if(!racer.Finished&&!racer.Withdrawn)racer.MarkTimedOut();
                if(state==State.Playing)FinishRace();
            }
            if(state!=State.Finished)return;
            finishWait+=dt;
            if(!race.Settled&&finishWait>=45)
                foreach(var racer in race.Racers)if(!racer.Finished&&!racer.Withdrawn)racer.MarkTimedOut();
            if(race.Settled)
            {
                race.Conclude();
                if(cup.Active&&!roundCommitted)
                {
                    roundCommitted=cup.RecordRound(race,cupRound);
                    if(cup.Complete&&cup.Points[0]>0)
                    {
                        int best=PlayerPrefs.GetInt("VuotThac.CupBestRank",7);
                        if(cup.PlayerRank<best)PlayerPrefs.SetInt("VuotThac.CupBestRank",cup.PlayerRank);
                        PlayerPrefs.Save();
                    }
                }
            }
            nextRoundButton.gameObject.SetActive(cup.Active&&!cup.Complete);
            nextRoundButton.interactable=roundCommitted;
            retryButton.GetComponent<RectTransform>().anchoredPosition=new Vector2(cup.Active&&!cup.Complete?-210:0,0);
        }
        void UpdateFinishResults()
        {
            string title=race.Player.TimedOut?"HẾT GIỜ — CHƯA HOÀN THÀNH":race.PlayerRank==1?"CHIẾN THẮNG!":"HOÀN THÀNH";
            string time=race.Player.Finished?$"{Mathf.FloorToInt(raceTime/60):00}:{raceTime%60:00.0}":"Không có thời gian hợp lệ";
            string text=title+(race.Player.TimedOut?"":" — HẠNG "+race.PlayerRank+"/"+race.Racers.Count)+"\n"+time+"\n\n"+race.Results();
            if(!onlinePlaying&&race.Player.Finished)
            {
                float best=PlayerPrefs.GetFloat("VuotThac.BestTime",raceTime);
                text+=$"\nTHÀNH TÍCH CÁ NHÂN: {Mathf.FloorToInt(best/60):00}:{best%60:00.0}";
            }
            if(cup.Active)
            {
                text="CÚP SUỐI RỪNG · LƯỢT "+cupRound+"/3\n"+text;
                text+=roundCommitted?"\n"+cup.Standings():"\nChờ các thuyền còn lại (tối đa 45 giây)…";
                if(cup.Complete)
                {
                    string award=cup.Points[0]==0?"CHƯA HOÀN THÀNH CÚP":cup.PlayerRank==1?"CÚP VÀNG!":cup.PlayerRank==2?"CÚP BẠC!":cup.PlayerRank==3?"CÚP ĐỒNG!":"HOÀN THÀNH CÚP";
                    text=award+" — HẠNG "+cup.PlayerRank+"/6\n\n"+cup.Standings();
                }
            }
            SetResultText(text);
        }
        void ContinueCup()
        {
            if(!cup.Active||cup.Complete||!roundCommitted)return;
            race.Clear();if(playerBoat!=null){playerBoat.SetActive(false);Destroy(playerBoat);}finishRoot.SetActive(false);StartRace();
        }
        void RestartPhaseFour()
        {
            if(onlinePlaying){EnterSelect();return;}
            cup.Cancel();race.Clear();if(playerBoat!=null){playerBoat.SetActive(false);Destroy(playerBoat);}finishRoot.SetActive(false);StartRace();
        }
    }
}
