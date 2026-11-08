using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace VuotThac.Boats
{
    [Serializable] public class RoomMember
    {
        public ulong id;
        public string name;
        public int boat;
        public bool ready;
        public bool connected=true;
    }
    [Serializable] public class BoatNetworkState
    {
        public ulong id;
        public Vector3 position;
        public Quaternion rotation;
        public float speed,throttle,steer,distance,finishTime;
        public int checkpoints,finishOrder;
        public bool finished,withdrawn;
    }
    [Serializable] public class RoomPacket
    {
        public string type,name,version;
        public int boat;
        public bool ready,reset;
        public float throttle,steer,time,countdown;
        public uint sequence;
        public RoomMember[] members;
        public BoatNetworkState[] boats;
    }

    // UGS handles discovery/Relay; NGO custom messages carry host-authoritative race state.
    public sealed class OnlineBoatRoom : MonoBehaviour
    {
        public const int ServicePlayerLimit=150;
        public const string Protocol="SuoiRung-1";
        const string Channel="VuotThac.Room.v1";
        public ISession Session { get; private set; }
        public NetworkManager Network { get; private set; }
        public readonly List<RoomMember> Members=new List<RoomMember>();
        public bool Busy { get; private set; }
        public bool Racing { get; private set; }
        public bool IsHost=>Network!=null&&Network.IsHost;
        public ulong LocalId=>Network!=null?Network.LocalClientId:0;
        public string Code=>Session!=null?Session.Code:"";
        public string Status { get; private set; }="Tạo phòng hoặc nhập mã để chơi cùng bạn bè.";
        public event Action Changed;
        public event Action<RoomMember[]> RaceStarted;
        public event Action<RoomPacket> FrameReceived;
        public event Action<ulong,RoomPacket> InputReceived;
        public event Action<ulong> PeerLeft;
        public event Action Closed;
        public bool CanStart=>IsHost&&!Racing&&Members.Count>=2&&Members.Count==Network.ConnectedClientsIds.Count&&
            (Session==null||Session.PlayerCount==Members.Count)&&Members.All(m=>m.ready&&m.connected);
        bool handlers,helloSent,leaving,localReady;
        string playerName;
        int selectedBoat;
        CancellationTokenSource operation;
        float sendCooldown;

        public async Task ConnectAsync(bool create,string code,string displayName,int boat)
        {
            if(Busy||leaving||Session!=null)return;
            Busy=true;leaving=false;Status="Đang kết nối dịch vụ phòng…";Changed?.Invoke();
            playerName=string.IsNullOrWhiteSpace(displayName)?"Người chơi":displayName.Trim();
            if(playerName.Length>24)playerName=playerName.Substring(0,24);
            selectedBoat=boat;localReady=false;
            operation=new CancellationTokenSource(TimeSpan.FromSeconds(35));
            try
            {
                if(UnityServices.State!=ServicesInitializationState.Initialized)
                {
                    var options=new InitializationOptions();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    string profile=OnlineSmokeClient.Argument("-online-test-profile");
                    if(!string.IsNullOrEmpty(profile))options.SetProfile(profile);
#endif
                    await UnityServices.InitializeAsync(options);
                }
                if(!AuthenticationService.Instance.IsSignedIn)await AuthenticationService.Instance.SignInAnonymouslyAsync();
                operation.Token.ThrowIfCancellationRequested();
                PrepareNetwork();
                if(create)
                    Session=await MultiplayerService.Instance.CreateSessionAsync(new SessionOptions
                    {Name="Suối Rừng",MaxPlayers=ServicePlayerLimit,IsPrivate=true}.WithRelayNetwork(),operation.Token);
                else
                    Session=await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant(),new JoinSessionOptions(),operation.Token);
                if(leaving||operation.IsCancellationRequested){await Session.LeaveAsync();Session=null;return;}
                Session.RemovedFromSession+=SessionLost;Session.Deleted+=SessionLost;
                Session.Changed+=SessionChanged;Session.SessionHostChanged+=HostChanged;
                Status="Mã phòng: "+Code;PlayerPrefs.SetString("VuotThac.PlayerName",playerName);
                EnsureHandlers();SendHello();
            }
            catch(Exception e)
            {
                string error=e is OperationCanceledException?"Kết nối đã hủy hoặc quá thời gian.":"Chưa kết nối được phòng: "+e.Message;
                await LeaveAsync();Status=error.Length>190?error.Substring(0,190):error;
            }
            finally{Busy=false;Changed?.Invoke();}
        }
        void PrepareNetwork()
        {
            if(Network!=null)return;
            var go=new GameObject("OnlineNetwork");
            var transport=go.AddComponent<UnityTransport>();transport.MaxPayloadSize=64*1024;transport.MaxSendQueueSize=1024*1024;
            Network=go.AddComponent<NetworkManager>();
            Network.NetworkConfig=new NetworkConfig();
            Network.NetworkConfig.NetworkTransport=transport;
            Network.NetworkConfig.EnableSceneManagement=false;Network.NetworkConfig.ForceSamePrefabs=false;
            Network.NetworkConfig.TickRate=30;
            Network.OnClientConnectedCallback+=Connected;Network.OnClientDisconnectCallback+=Disconnected;
        }
        void EnsureHandlers()
        {
            if(handlers||Network==null||!Network.IsListening||Network.CustomMessagingManager==null)return;
            Network.CustomMessagingManager.RegisterNamedMessageHandler(Channel,Receive);handlers=true;
        }
        void Connected(ulong id)
        {
            EnsureHandlers();
            if(!Network.IsHost&&id==Network.LocalClientId){helloSent=false;SendHello();}
        }
        void Update()
        {
            EnsureHandlers();
            if(Session!=null&&!helloSent&&Network!=null&&(Network.IsHost||Network.IsConnectedClient))SendHello();
            sendCooldown=Mathf.Max(0,sendCooldown-Time.unscaledDeltaTime);
        }
        void SendHello()
        {
            if(!handlers)return;
            var packet=new RoomPacket{type="hello",name=playerName,boat=selectedBoat,ready=localReady,version=Protocol};
            if(IsHost)AcceptHello(LocalId,packet);else Send(NetworkManager.ServerClientId,packet);
            helloSent=true;
        }
        void AcceptHello(ulong sender,RoomPacket packet)
        {
            if(Racing||packet.version!=Protocol||packet.boat<0||packet.boat>=10)
            {if(sender!=LocalId)Network.DisconnectClient(sender);return;}
            var member=Members.Find(m=>m.id==sender);
            if(member==null){member=new RoomMember{id=sender};Members.Add(member);}
            member.name=string.IsNullOrWhiteSpace(packet.name)?"Người chơi":packet.name.Trim();
            member.name=member.name.Replace("<","").Replace(">","");
            if(member.name.Length>24)member.name=member.name.Substring(0,24);
            member.boat=packet.boat;member.ready=packet.ready;
            BroadcastLobby();
        }
        public void ToggleReady()
        {
            if(Session==null||Racing)return;
            localReady=!localReady;
            var packet=new RoomPacket{type="ready",ready=localReady};
            if(IsHost){var own=Members.Find(m=>m.id==LocalId);if(own!=null)own.ready=localReady;BroadcastLobby();}
            else Send(NetworkManager.ServerClientId,packet);
        }
        public async Task StartRaceAsync()
        {
            if(!CanStart||Busy)return;
            Busy=true;Changed?.Invoke();
            try
            {
                Session.AsHost().IsLocked=true;await Session.AsHost().SavePropertiesAsync();
                if(!CanStart)return;
                Racing=true;var packet=new RoomPacket{type="start",members=Members.ToArray(),version=Protocol};
                Broadcast(packet);RaceStarted?.Invoke(packet.members);
            }
            catch(Exception e){Status="Không thể bắt đầu: "+e.Message;}
            finally{Busy=false;Changed?.Invoke();}
        }
        void Receive(ulong sender,FastBufferReader reader)
        {
            try
            {
                if(reader.Length>256*1024)return;
                reader.ReadValueSafe(out string json);var packet=JsonUtility.FromJson<RoomPacket>(json);
                if(packet==null)return;
                if(IsHost)
                {
                    if(!Network.ConnectedClientsIds.Contains(sender))return;
                    if(packet.type=="hello")AcceptHello(sender,packet);
                    else if(packet.type=="ready"&&!Racing)
                    {var member=Members.Find(m=>m.id==sender);if(member!=null){member.ready=packet.ready;BroadcastLobby();}}
                    else if(packet.type=="input"&&Racing&&Finite(packet.throttle)&&Finite(packet.steer))InputReceived?.Invoke(sender,packet);
                    return;
                }
                if(sender!=NetworkManager.ServerClientId)return;
                if(packet.type=="lobby")
                {Members.Clear();Members.AddRange(packet.members??Array.Empty<RoomMember>());Status="Mã phòng: "+Code;Changed?.Invoke();}
                else if(packet.type=="start"&&!Racing&&packet.version==Protocol)
                {Racing=true;Members.Clear();Members.AddRange(packet.members);RaceStarted?.Invoke(packet.members);Changed?.Invoke();}
                else if(packet.type=="frame"&&Racing)FrameReceived?.Invoke(packet);
            }
            catch(Exception){Status="Bỏ qua gói mạng không hợp lệ.";}
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        void BroadcastLobby(){Status="Mã phòng: "+Code;Broadcast(new RoomPacket{type="lobby",members=Members.ToArray()});Changed?.Invoke();}
        void Send(ulong id,RoomPacket packet)
        {
            if(Network==null||!Network.IsListening||!handlers)return;
            string json=JsonUtility.ToJson(packet);
            using(var writer=new FastBufferWriter(json.Length*2+32,Allocator.Temp))
            {writer.WriteValueSafe(json);Network.CustomMessagingManager.SendNamedMessage(Channel,id,writer,NetworkDelivery.ReliableFragmentedSequenced);}
        }
        void Broadcast(RoomPacket packet)
        {foreach(var id in Network.ConnectedClientsIds)if(id!=LocalId)Send(id,packet);}
        public void SendFrame(RoomPacket frame){if(IsHost&&Racing)Broadcast(frame);}
        public void SendInput(float throttle,float steer,bool reset)
        {
            if(!Racing||IsHost||sendCooldown>0&&!reset)return;
            sendCooldown=.05f;Send(NetworkManager.ServerClientId,new RoomPacket{type="input",throttle=throttle,steer=steer,reset=reset});
        }
        void Disconnected(ulong id)
        {
            if(leaving)return;
            if(IsHost)
            {
                var member=Members.Find(m=>m.id==id);
                if(member!=null){if(Racing){member.connected=false;PeerLeft?.Invoke(id);}else Members.Remove(member);BroadcastLobby();}
            }
            else if(id==LocalId||id==NetworkManager.ServerClientId)SessionLost();
        }
        void SessionChanged(){Changed?.Invoke();}
        void HostChanged(string id){SessionLost();}
        void SessionLost(){if(!leaving){Status="Phòng đã đóng hoặc mất kết nối với chủ phòng.";_ = LeaveAsync();}}
        public async Task LeaveAsync()
        {
            if(leaving)return;leaving=true;Busy=true;Changed?.Invoke();operation?.Cancel();
            var session=Session;Session=null;
            if(session!=null)
            {
                session.RemovedFromSession-=SessionLost;session.Deleted-=SessionLost;
                session.Changed-=SessionChanged;session.SessionHostChanged-=HostChanged;
                try{await session.LeaveAsync();}catch(Exception){}
            }
            if(Network!=null)
            {
                Network.OnClientConnectedCallback-=Connected;Network.OnClientDisconnectCallback-=Disconnected;
                if(handlers&&Network.CustomMessagingManager!=null)Network.CustomMessagingManager.UnregisterNamedMessageHandler(Channel);
                Network.Shutdown();Destroy(Network.gameObject);Network=null;
            }
            Members.Clear();Racing=false;handlers=helloSent=false;localReady=false;Busy=false;
            if(Status.StartsWith("Mã phòng:"))Status="Đã rời phòng. Bạn có thể tạo hoặc vào phòng khác.";
            Closed?.Invoke();Changed?.Invoke();leaving=false;
        }
        void OnDestroy(){operation?.Cancel();if(Network!=null){Network.Shutdown();Destroy(Network.gameObject);}}
    }
}
