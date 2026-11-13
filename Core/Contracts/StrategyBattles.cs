using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// 遠方の合戦の簡略計算（統合案 W4、2026-11-14）。全国の家の戦略の決着を、強さの比のくじから、実際の合戦と同じ接触の規則で計算する形に替える。
// 両軍を500人ずつの部隊に分けて横一列に並べ、向かい合う部隊どうしが接触する。数の多い側の余った部隊は、相手の部隊へ順に重なって当たる。
// 1秒ごとの損害は実際の合戦（ContactCombat）と同じ：兵は接触している敵の兵の10分の1（最低1）ずつ、士気は接触している敵の部隊の数×8ずつ減る。
// 士気が25以下になった部隊は崩れて戦わない。崩れた部隊を除いて毎秒並べ直す。どちらかの全部隊が崩れるか兵がなくなれば終わる。
// 両軍の開始の士気は出陣ごとの固定のくじで70〜100。守る側が崩れ、攻める側が残っていれば城が落ちる。同時に崩れれば落ちない。
// 一人一人の動きや描画は省く。本人が近づいたときに実際の軍勢へ引き継ぐことは残件。
public sealed record StrategyBattle(long AttackerSoldiers,long DefenderSoldiers,int AttackerMorale,int DefenderMorale,long AttackerLost,long DefenderLost,int Seconds,bool AttackerBroke,bool DefenderBroke);

public static class StrategyBattles
{
    public const long UnitSize=500;
    const int Routed=25,MaxSeconds=3600;
    public static bool Enabled(ContractWorld w)=>NationalSystems.On(w,"StrategyBattle");
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"strategy-battle-v1/{w.Seed}/{key}")))%31);
    internal static StrategyBattle Fight(ContractWorld w,EntityId campaign,long attacker,long defender)=>
        Simulate(attacker,defender,70+Draw(w,campaign.Value+"/attacker"),70+Draw(w,campaign.Value+"/defender"));

    sealed class Unit{public long Soldiers;public int Morale;public bool Active=>Soldiers>0&&Morale>Routed;}
    static Unit[] Split(long soldiers,int morale)
    {
        long count=Math.Max(1,(soldiers+UnitSize-1)/UnitSize);
        return Enumerable.Range(0,(int)count).Select(i=>new Unit{Soldiers=soldiers/count+(i<soldiers%count?1:0),Morale=morale}).ToArray();
    }
    public static StrategyBattle Simulate(long attacker,long defender,int attackerMorale,int defenderMorale)
    {
        if(attacker<1||defender<1||attackerMorale is <0 or >100||defenderMorale is <0 or >100)throw new InvalidDataException("遠方の合戦の入力が不正です");
        var a=Split(attacker,attackerMorale);var d=Split(defender,defenderMorale);int seconds=0;
        while(seconds<MaxSeconds&&a.Any(u=>u.Active)&&d.Any(u=>u.Active))
        {
            var sa=a.Where(u=>u.Active).ToArray();var sd=d.Where(u=>u.Active).ToArray();
            // 接触：多い側の i 番目の部隊は、少ない側の i mod 数 番目の部隊に当たる。
            var large=sa.Length>=sd.Length?sa:sd;var small=large==sa?sd:sa;
            var contacts=large.Select((u,i)=>(Large:u,Small:small[i%small.Length])).ToArray();
            var soldierLoss=new Dictionary<Unit,long>();var moraleLoss=new Dictionary<Unit,int>();
            foreach(var (l,s) in contacts)
            {
                soldierLoss[l]=soldierLoss.GetValueOrDefault(l)+Math.Max(1,s.Soldiers/10);moraleLoss[l]=moraleLoss.GetValueOrDefault(l)+8;
                soldierLoss[s]=soldierLoss.GetValueOrDefault(s)+Math.Max(1,l.Soldiers/10);moraleLoss[s]=moraleLoss.GetValueOrDefault(s)+8;
            }
            foreach(var (u,n) in soldierLoss)u.Soldiers-=Math.Min(u.Soldiers,n);
            foreach(var (u,n) in moraleLoss)u.Morale-=Math.Min(u.Morale,n);
            seconds++;
        }
        bool aBroke=!a.Any(u=>u.Active),dBroke=!d.Any(u=>u.Active);
        return new(attacker,defender,attackerMorale,defenderMorale,attacker-a.Sum(u=>u.Soldiers),defender-d.Sum(u=>u.Soldiers),seconds,aBroke,dBroke);
    }
    public static bool Falls(StrategyBattle b)=>b.DefenderBroke&&!b.AttackerBroke;
    internal static void Validate(ContractWorld w,StrategyOutcome o)
    {
        if(o.Battle is not {} b)return;
        if(!Enabled(w)||b!=Simulate(b.AttackerSoldiers,b.DefenderSoldiers,b.AttackerMorale,b.DefenderMorale)||b!=Fight(w,o.Campaign,b.AttackerSoldiers,b.DefenderSoldiers)||o.Captured&&!Falls(b))
            throw new InvalidDataException("遠方の合戦の計算が一致しません");
    }
}
