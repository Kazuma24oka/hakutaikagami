using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// 史実の事件の知らせ（2026-11-17、S03）：史実の出陣の決着と史実の同盟は、その場所から人づてに広がる噂として届く。
// 噂は、事件の時に本人がいた所から事件の場所までの道のりに応じて遅れて届き（1日＋30kmごとに1日）、15%は誤報（決着が逆）。
// 噂は保存しない（事件の記録・本人の居場所・固定の種から、いつでも同じに求まる）。一覧に HistoryEvents2 のある世界だけ。
public sealed record HistoricalRumor(string Event,string Name,string Text,long OccurredAt,long LearnedAt,bool Misreport,string[] Houses);

public static class HistoricalNews
{
    const long Day=86400,StepMm=30_000_000;
    public const int MisreportPerMille=150;
    public static bool Enabled(ContractWorld w)=>NationalSystems.On(w,NationalStrategy.Events2)&&NationalStrategy.Enabled(w);

    static RoutePoint? SitePoint(ContractWorld w,EntityId site)=>BranchRoutes.GraphNodes(w).FirstOrDefault(n=>"site:"+n.Id==site.Value)?.Position;
    // その場所から本人のところへ噂が届くまでの時間（1日＋30kmごとに1日）。
    public static long DelayFrom(ContractWorld w,EntityId viewer,EntityId site,long at)=>SitePoint(w,site) is {} where?Delay(w,viewer,where,at):Day;
    static long Delay(ContractWorld w,EntityId viewer,RoutePoint where,long at)
    {
        var p=RegionalCampaign.ActorPointAt(w,viewer,at);long de=p.EastMm-where.EastMm,dn=p.NorthMm-where.NorthMm;
        long mm=(long)Math.Sqrt((double)de*de+(double)dn*dn);
        return Day+mm/StepMm*Day;
    }
    static bool Wrong(ContractWorld w,string evt,EntityId viewer)=>
        BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes($"history-rumor-v1/{w.NewGame!.Seed}/{evt}/{viewer.Value}")),0)%1000<MisreportPerMille;
    static string HouseName(ContractWorld w,EntityId house)=>w.Definitions.Organizations.SingleOrDefault(o=>o.Id==house)?.Name??house.Value;
    static string CastleName(ContractWorld w,EntityId site)=>w.Definitions.Sites.SingleOrDefault(s=>s.Id==site)?.Name??site.Value;

    // 本人がその時までに聞いた史実の事件の噂。
    public static HistoricalRumor[] Heard(ContractWorld w,EntityId viewer,long at)
    {
        if(!Enabled(w)||w.Strategy is not {} s)return [];
        var rows=new List<HistoricalRumor>();
        foreach(var o in s.Outcomes.Where(o=>o.At<=at))
        {
            var c=s.Campaigns.Single(c=>c.Id==o.Campaign);
            if(!c.Basis.StartsWith("history:",StringComparison.Ordinal)||NationalStrategy.HistoryFor(w).FirstOrDefault(e=>"history:"+e.Id==c.Basis) is not {} e)continue;
            if(SitePoint(w,c.Target) is not {} where)continue;
            long learned=o.At+Delay(w,viewer,where,o.At);if(learned>at)continue;
            bool wrong=Wrong(w,e.Id,viewer);bool captured=o.Captured!=wrong;
            string castle=CastleName(w,c.Target),attacker=HouseName(w,c.Attacker);
            rows.Add(new(e.Id,e.Name,captured?$"{e.Name}　{castle}は{attacker}の手に落ちたという":$"{e.Name}　{attacker}は{castle}を落とせずに退いたという",o.At,learned,wrong,[c.Attacker.Value,c.Defender.Value]));
        }
        foreach(var h in (s.History??[]).Where(h=>h.Result=="fired"&&h.At<=at))
        {
            if(NationalStrategy.Alliances.FirstOrDefault(a=>a.Id==h.Event) is not {} a)continue;
            var pact=(s.Pacts??[]).FirstOrDefault(p=>p.Basis=="history:"+a.Id);if(pact==null)continue;
            var home=w.NewGame!.Start.Factions.FirstOrDefault(f=>f.Id==pact.A.Value)?.Home;if(home==null||SitePoint(w,new(home)) is not {} where)continue;
            long learned=h.At+Delay(w,viewer,where,h.At);if(learned>at)continue;
            bool wrong=Wrong(w,a.Id,viewer);
            rows.Add(new(a.Id,a.Name,wrong?$"{HouseName(w,pact.A)}と{HouseName(w,pact.B)}の縁組は破談になったという":$"{a.Name}　{HouseName(w,pact.A)}と{HouseName(w,pact.B)}が結んだという",h.At,learned,wrong,[pact.A.Value,pact.B.Value]));
        }
        return rows.OrderBy(r=>r.LearnedAt).ThenBy(r=>r.Event,StringComparer.Ordinal).ToArray();
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Heard(w,viewer,w.ElapsedSeconds).Select(r=>new PersonalChronicleRow("rumor:"+r.Event,"噂",r.Text,r.OccurredAt,r.LearnedAt,"噂",r.Houses[0]){Houses=r.Houses});
}
