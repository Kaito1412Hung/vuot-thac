using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VuotThac.Boats
{
    public partial class BoatSelectScreen
    {
        OnlineBoatRoom onlineRoom;
        OnlineRaceDriver onlineDriver;
        bool onlinePlaying;
        GameObject onlinePanel;
        InputField onlineName,onlineCode;
        Text onlineStatus,onlineRoster,readyLabel;
        Button createRoomButton,joinRoomButton,readyRoomButton,startRoomButton;
        void BuildOnlineUI(Transform root)
        {
            onlineRoom=gameObject.AddComponent<OnlineBoatRoom>();
            onlineRoom.Changed+=RefreshRoomUI;onlineRoom.RaceStarted+=StartOnlineRace;onlineRoom.Closed+=RoomClosed;
            var open=Button(selectRoot.transform,"Friends","CHƠI VỚI BẠN BÈ",new Color(.12f,.36f,.6f),()=>
            {selectRoot.SetActive(false);onlinePanel.SetActive(true);RefreshRoomUI();});
            Place(open,new Vector2(.3f,0),new Vector2(0,290),new Vector2(600,82));
            open.GetComponentInChildren<Text>().fontSize=32;
            onlinePanel=Rect(root,"OnlineRoom",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject;
            onlinePanel.AddComponent<Image>().color=new Color(.02f,.06f,.09f,.96f);
            var title=Label(onlinePanel.transform,"Title","ĐUA CÙNG BẠN BÈ",56,FontStyle.Bold,TextAnchor.MiddleCenter);
            Place(title.rectTransform,new Vector2(.5f,1),new Vector2(0,-65),new Vector2(1300,90));
            onlineName=RoomInput("Name","Tên của bạn",new Vector2(-270,-165));
            onlineName.text=PlayerPrefs.GetString("VuotThac.PlayerName","Người chơi");onlineName.characterLimit=24;
            onlineCode=RoomInput("Code","Nhập mã phòng",new Vector2(270,-165));onlineCode.characterLimit=12;
            var create=Button(onlinePanel.transform,"Create","TẠO PHÒNG",new Color(.15f,.45f,.65f),async()=>
                await onlineRoom.ConnectAsync(true,"",onlineName.text,index));
            Place(create,new Vector2(.5f,1),new Vector2(-270,-260),new Vector2(500,80));createRoomButton=create.GetComponent<Button>();
            var join=Button(onlinePanel.transform,"Join","VÀO PHÒNG",new Color(.15f,.45f,.65f),async()=>
                await onlineRoom.ConnectAsync(false,onlineCode.text,onlineName.text,index));
            Place(join,new Vector2(.5f,1),new Vector2(270,-260),new Vector2(500,80));joinRoomButton=join.GetComponent<Button>();
            onlineStatus=Label(onlinePanel.transform,"Status","",32,FontStyle.Normal,TextAnchor.MiddleCenter);onlineStatus.supportRichText=false;
            Place(onlineStatus.rectTransform,new Vector2(.5f,1),new Vector2(0,-360),new Vector2(1400,95));
            var viewport=Rect(onlinePanel.transform,"RosterViewport",Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
            Place(viewport,new Vector2(.5f,1),new Vector2(0,-440),new Vector2(1100,320),new Vector2(.5f,1));
            viewport.gameObject.AddComponent<Image>().color=new Color(1,1,1,.04f);viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.horizontal=false;
            onlineRoster=Label(viewport,"Roster","",32,FontStyle.Normal,TextAnchor.UpperLeft);onlineRoster.supportRichText=false;
            var content=onlineRoster.rectTransform;content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.anchoredPosition=Vector2.zero;content.sizeDelta=new Vector2(-35,320);scroll.content=content;
            var ready=Button(onlinePanel.transform,"Ready","SẴN SÀNG",new Color(.2f,.55f,.35f),()=>onlineRoom.ToggleReady());
            Place(ready,new Vector2(.5f,0),new Vector2(-270,190),new Vector2(500,85));readyRoomButton=ready.GetComponent<Button>();readyLabel=ready.GetComponentInChildren<Text>();
            var start=Button(onlinePanel.transform,"Race","BẮT ĐẦU ĐUA",new Color(.2f,.62f,.3f),async()=>await onlineRoom.StartRaceAsync());
            Place(start,new Vector2(.5f,0),new Vector2(270,190),new Vector2(500,85));startRoomButton=start.GetComponent<Button>();
            var leave=Button(onlinePanel.transform,"Leave","QUAY LẠI / RỜI PHÒNG",new Color(.3f,.3f,.35f),CloseOnlinePanel);
            Place(leave,new Vector2(.5f,0),new Vector2(0,65),new Vector2(740,80));leave.GetComponentInChildren<Text>().fontSize=32;
            onlinePanel.SetActive(false);
        }
        InputField RoomInput(string name,string hint,Vector2 position)
        {
            var box=Rect(onlinePanel.transform,name,Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
            Place(box,new Vector2(.5f,1),position,new Vector2(500,75));box.gameObject.AddComponent<Image>().color=new Color(.13f,.2f,.25f);
            var input=box.gameObject.AddComponent<InputField>();
            var text=Label(box,"Value","",32,FontStyle.Normal,TextAnchor.MiddleLeft);text.supportRichText=false;text.rectTransform.offsetMin=new Vector2(18,0);text.rectTransform.offsetMax=new Vector2(-18,0);
            var placeholder=Label(box,"Placeholder",hint,32,FontStyle.Italic,TextAnchor.MiddleLeft);placeholder.color=new Color(1,1,1,.5f);placeholder.rectTransform.offsetMin=new Vector2(18,0);
            input.textComponent=text;input.placeholder=placeholder;return input;
        }
        void RefreshRoomUI()
        {
            if(onlinePanel==null)return;
            bool joined=onlineRoom.Session!=null;
            onlineStatus.text=onlineRoom.Status;
            onlineName.interactable=onlineCode.interactable=!joined&&!onlineRoom.Busy;
            createRoomButton.interactable=joinRoomButton.interactable=!joined&&!onlineRoom.Busy;
            readyRoomButton.interactable=joined&&!onlineRoom.Busy;
            startRoomButton.interactable=onlineRoom.CanStart&&!onlineRoom.Busy;
            var own=onlineRoom.Members.Find(m=>m.id==onlineRoom.LocalId);readyLabel.text=own!=null&&own.ready?"HỦY SẴN SÀNG":"SẴN SÀNG";
            onlineRoster.text=joined?"NGƯỜI CHƠI: "+onlineRoom.Members.Count+"\n"+string.Join("\n",onlineRoom.Members.Select(m=>m.name+(m.id==onlineRoom.LocalId?" (bạn)":"")+" — "+(m.ready?"Sẵn sàng":"Đang chờ"))):
                "Chọn thuyền trước khi vào phòng.\nCó ít nhất 2 người và tất cả sẵn sàng thì chủ phòng bắt đầu.\nChỉ người trong phòng tham gia đua, không thêm máy.\nCúp và vật phẩm áp dụng cho đấu máy.";
            onlineRoster.rectTransform.sizeDelta=new Vector2(-35,Mathf.Max(320,(onlineRoom.Members.Count+2)*43));
        }
        async void CloseOnlinePanel()
        {
            await onlineRoom.LeaveAsync();if(onlinePanel!=null)onlinePanel.SetActive(false);
            if(state==State.Selecting&&selectRoot!=null)selectRoot.SetActive(true);
        }
        void RoomClosed()
        {
            if(onlinePlaying)EnterSelect();
            if(onlinePanel!=null)RefreshRoomUI();
        }
        void StartOnlineRace(RoomMember[] members)
        {
            cup.Cancel();
            int local=Array.FindIndex(members,m=>m.id==onlineRoom.LocalId);
            if(local<0)return;
            onlinePlaying=true;onlinePanel.SetActive(false);
            if(preview!=null){preview.SetActive(false);Destroy(preview);}
            var boats=new List<BoatController>();var names=new List<string>();
            for(int i=0;i<members.Length;i++)
            {
                var member=members[i];var def=catalog.boats[Mathf.Clamp(member.boat,0,catalog.boats.Length-1)];
                var go=Instantiate(def.prefab);go.name="Online_"+member.id;
                var boat=go.GetComponent<BoatController>();boat.Track=track;
                if(i==local)
                {
                    player=boat;playerBoat=go;boat.Input=go.AddComponent<PlayerBoatInput>();
                    if(Application.isMobilePlatform)go.AddComponent<MobileBoatControls>().Initialize((PlayerBoatInput)boat.Input,boatCamera,this);
                }
                else boat.Input=go.AddComponent<RemoteBoatInput>();
                if(!onlineRoom.IsHost)boat.enabled=false;
                boats.Add(boat);names.Add(member.name);
            }
            if(race==null)race=gameObject.AddComponent<BoatRace>();race.BeginFriends(boats,names,local,track);
            onlineDriver=gameObject.AddComponent<OnlineRaceDriver>();onlineDriver.Initialize(onlineRoom,race,members,player.GetComponent<PlayerBoatInput>());
            boatCamera.Follow(player.transform,player.hullLength);raceTime=0;
            selectRoot.SetActive(false);finishRoot.SetActive(false);hudRoot.SetActive(true);raceNotice.text="3";state=State.Playing;
        }
    }
}
