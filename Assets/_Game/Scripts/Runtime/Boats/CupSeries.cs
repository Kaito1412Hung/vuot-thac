using System;
using System.Collections.Generic;
using System.Text;

namespace VuotThac.Boats
{
    public sealed class CupSeries
    {
        public const int Rounds=3;
        static readonly int[] Award={10,8,6,4,2,1};
        public readonly int[] Points=new int[6],Wins=new int[6],LastRank=new int[6];
        public bool Active {get;private set;}
        public int CompletedRounds {get;private set;}
        public bool Complete=>Active&&CompletedRounds==Rounds;
        public void Start(){Array.Clear(Points,0,6);Array.Clear(Wins,0,6);Array.Clear(LastRank,0,6);CompletedRounds=0;Active=true;}
        public void Cancel(){Active=false;}
        public bool RecordRound(BoatRace race,int expectedRound)
        {
            if(!Active||Complete||expectedRound!=CompletedRounds+1||race.Racers.Count!=6)return false;
            foreach(var racer in race.Racers)if(!racer.Finished&&!racer.Withdrawn)return false;
            for(int i=0;i<6;i++)
            {
                int rank=race.RankOf(race.Racers[i]);LastRank[i]=rank;
                if(!race.Racers[i].Finished)continue;
                Points[i]+=Award[Math.Min(5,rank-1)];if(rank==1)Wins[i]++;
            }
            CompletedRounds++;return true;
        }
        int Compare(int a,int b)
        {
            int v=Points[b].CompareTo(Points[a]);if(v!=0)return v;
            v=Wins[b].CompareTo(Wins[a]);if(v!=0)return v;
            v=LastRank[a].CompareTo(LastRank[b]);return v!=0?v:a.CompareTo(b);
        }
        public int PlayerRank {get{if(Points[0]==0)return 6;int rank=1;for(int i=1;i<6;i++)if(Compare(i,0)<0)rank++;return rank;}}
        public string Standings()
        {
            var order=new List<int>{0,1,2,3,4,5};order.Sort(Compare);var text=new StringBuilder("BẢNG ĐIỂM CÚP\n");
            for(int i=0;i<6;i++){int id=order[i];text.AppendLine((Points[id]>0?(i+1)+".":"—")+" "+(id==0?"Bạn":"Đối thủ "+id)+" — "+Points[id]+" điểm"+(Points[id]==0?" (chưa về đích)":""));}
            return text.ToString();
        }
    }
}
