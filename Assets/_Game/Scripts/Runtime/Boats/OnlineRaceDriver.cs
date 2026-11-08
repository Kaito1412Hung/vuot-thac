using System.Collections.Generic;
using UnityEngine;

namespace VuotThac.Boats
{
    public sealed class RemoteBoatInput : MonoBehaviour,IBoatInput
    {
        float throttle,steer,lastInput;
        bool reset;
        public float Throttle=>Time.unscaledTime-lastInput<.75f?throttle:0;
        public float Steer=>Time.unscaledTime-lastInput<.75f?steer:0;
        public void Set(float power,float turn,bool restart=false)
        {throttle=Mathf.Clamp(power,-1,1);steer=Mathf.Clamp(turn,-1,1);lastInput=Time.unscaledTime;reset|=restart;}
        public bool ConsumeReset(){bool value=reset;reset=false;return value;}
    }
    public sealed class OnlineRaceDriver : MonoBehaviour
    {
        OnlineBoatRoom room;
        BoatRace race;
        readonly Dictionary<ulong,RaceProgress> racers=new Dictionary<ulong,RaceProgress>();
        readonly Dictionary<ulong,BoatNetworkState> targets=new Dictionary<ulong,BoatNetworkState>();
        PlayerBoatInput input;
        float timer,lastFrame;
        uint sequence,receivedSequence;
        public void Initialize(OnlineBoatRoom lobby,BoatRace controller,RoomMember[] members,PlayerBoatInput localInput)
        {
            room=lobby;race=controller;input=localInput;
            for(int i=0;i<members.Length;i++)racers.Add(members[i].id,race.Racers[i]);
            room.InputReceived+=ReceiveInput;room.FrameReceived+=ReceiveFrame;room.PeerLeft+=PeerLeft;
            lastFrame=Time.unscaledTime;
        }
        void Update()
        {
            if(room==null||!room.Racing||race==null||!race.Running)return;
            if(room.IsHost)
            {
                race.Tick(Time.deltaTime);timer-=Time.unscaledDeltaTime;
                if(timer<=0)
                {
                    timer=.1f;var states=new List<BoatNetworkState>(racers.Count);
                    foreach(var pair in racers)
                    {
                        var r=pair.Value;var boat=r.Boat;
                        if(r.Finished){boat.RecoveryEnabled=false;boat.GetComponent<Rigidbody>().isKinematic=true;foreach(var c in boat.GetComponentsInChildren<Collider>())c.enabled=false;}
                        states.Add(new BoatNetworkState{id=pair.Key,position=boat.transform.position,rotation=boat.transform.rotation,
                            speed=boat.Speed,throttle=boat.Input?.Throttle??0,steer=boat.Input?.Steer??0,distance=r.Distance,
                            checkpoints=r.Checkpoints,finished=r.Finished,finishTime=r.FinishTime,finishOrder=r.FinishOrder,withdrawn=r.Withdrawn});
                    }
                    room.SendFrame(new RoomPacket{type="frame",sequence=++sequence,time=race.Elapsed,countdown=race.CountdownRemaining,boats=states.ToArray()});
                }
            }
            else
            {
                if(input!=null)room.SendInput(input.Throttle,input.Steer,input.ConsumeReset());
                float blend=1-Mathf.Exp(-20*Time.unscaledDeltaTime);
                foreach(var pair in targets)
                {
                    if(!racers.TryGetValue(pair.Key,out var r))continue;
                    var state=pair.Value;var rb=r.GetComponent<Rigidbody>();
                    var position=Vector3.Distance(rb.position,state.position)>5?state.position:Vector3.Lerp(rb.position,state.position,blend);
                    rb.position=position;rb.rotation=Quaternion.Slerp(rb.rotation,state.rotation,blend);
                    r.Boat.ApplyRemotePresentation(state.speed);
                }
                // Stop sending stale actions when the host disappears; transport reports the disconnect.
                if(Time.unscaledTime-lastFrame>5)input?.ReleaseTouch();
            }
        }
        void ReceiveInput(ulong sender,RoomPacket packet)
        {
            if(!room.IsHost||!racers.TryGetValue(sender,out var r)||r.Finished||r.Withdrawn)return;
            r.GetComponent<RemoteBoatInput>()?.Set(packet.throttle,packet.steer,packet.reset);
        }
        void ReceiveFrame(RoomPacket packet)
        {
            if(room.IsHost||packet.sequence<=receivedSequence||packet.boats==null)return;
            receivedSequence=packet.sequence;lastFrame=Time.unscaledTime;race.ApplyOnlineFrame(packet);
            foreach(var state in packet.boats)
            {
                if(!racers.TryGetValue(state.id,out var r))continue;
                r.ApplyNetwork(state);targets[state.id]=state;
                // Local actions animate the local paddler; the host still decides the resulting pose.
                if(state.id!=room.LocalId)r.GetComponent<RemoteBoatInput>()?.Set(state.throttle,state.steer);
            }
        }
        void PeerLeft(ulong id){if(racers.TryGetValue(id,out var racer))racer.Withdraw();}
        void OnDestroy()
        {
            if(room!=null){room.InputReceived-=ReceiveInput;room.FrameReceived-=ReceiveFrame;room.PeerLeft-=PeerLeft;}
        }
    }
}
