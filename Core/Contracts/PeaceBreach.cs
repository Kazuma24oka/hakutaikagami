namespace HakutaiKagami.Core.Contracts;

// S04（2026-11-17）：仲裁の後始末（一覧の CentralStanding の世界だけ）。
// ・期限切れ：取次が仲裁案を送ってから60日のうちに両家の返事がそろわなければ失効。依頼した人と取次は60日目に知る（保存しない。原本から導く）。
// ・違反の訴え：取次が成立を認めた和睦の期間に、片方の家が相手の城へ出陣したら違反。攻められた家の当主（攻められて知る）が取次へ訴えると、
//   取次が生きていれば違反を認め、破った家の当主に悪名が付く。悪名は訴えた人と取次がすぐに、ほかの人は京から道のりに応じて遅れて知る。
public sealed record StrategyAppeal(string Agreement,EntityId Appellant,EntityId InjuredHouse,EntityId Breaker,EntityId BreakerHead,EntityId Broker,EntityId Campaign,long At,long Revision,bool Upheld);
public sealed record PeaceBreachCase(string Agreement,EntityId InjuredHouse,EntityId BreakingHouse,EntityId Campaign,long KnownAt,EntityId Broker);
public sealed record MediationExpiry(string Proposal,EntityId Broker,EntityId Requester,long ExpiredAt);

public static class PeaceBreach
{
    public const long ReplyDays=60;
    const long Day=86400;
    public static bool Enabled(ContractWorld w)=>CentralStanding.On(w);

    public static MediationExpiry[] Expired(ContractWorld w,EntityId viewer)
    {
        if(!Enabled(w))return [];var rows=HouseholdCourt.Events(w);var list=new List<MediationExpiry>();
        foreach(var p in rows.Where(e=>e.Input.Action==CourtAction.SendPeaceProposal))
        {
            long due=p.At+ReplyDays*Day;if(due>w.ElapsedSeconds)continue;
            int votes=rows.Count(v=>v.Input.Action is CourtAction.AcceptPeaceProposal or CourtAction.RejectPeaceProposal&&v.Input.Reference==p.Input.Id&&v.At<=due);
            if(votes>=2||rows.Any(c=>c.Input.Action==CourtAction.CertifyPeaceAgreement&&c.Input.Reference==p.Input.Id&&c.At<=due))continue;
            var request=rows.SingleOrDefault(e=>e.Input.Id==p.Input.Reference);var requester=request?.Actor??p.Input.Person;
            if(viewer!=p.Actor&&viewer!=requester)continue;
            list.Add(new(p.Input.Id.Value,p.Actor,requester,due));
        }
        return list.ToArray();
    }

    // 成立した和睦の期間に出陣した家（攻められた家の当主が、攻められたときに知る違反）。
    public static PeaceBreachCase[] Cases(ContractWorld w,EntityId viewer)
    {
        if(!Enabled(w)||w.Strategy is not {} s)return [];var list=new List<PeaceBreachCase>();
        foreach(var a in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.CertifyPeaceAgreement&&e.Input.OtherHouse!=null))
        {
            long until=a.At+a.Input.Amount*Day;EntityId x=a.Input.Owner,y=a.Input.OtherHouse!.Value;
            foreach(var c in s.Campaigns.Where(c=>c.At>=a.At&&c.At<until&&(c.Attacker==x&&c.Defender==y||c.Attacker==y&&c.Defender==x)))
            {
                if(HouseSuccession.Head(w,c.Defender,c.At,c.Revision)!=viewer)continue;
                list.Add(new(a.Input.Id.Value,c.Defender,c.Attacker,c.Id,c.At,a.Actor));
            }
        }
        return list.Where(c=>!(s.Appeals??[]).Any(p=>p.Campaign==c.Campaign)).ToArray();
    }
    internal static StrategyAppeal Decide(ContractWorld w,PeaceBreachCase c,EntityId appellant)
    {
        bool upheld=!Assassination.DeadAt(w,c.Broker,w.ElapsedSeconds,w.CommitSequence);
        return new(c.Agreement,appellant,c.InjuredHouse,c.BreakingHouse,HouseSuccession.Head(w,c.BreakingHouse,w.ElapsedSeconds,w.CommitSequence),c.Broker,c.Campaign,w.ElapsedSeconds,w.CommitSequence,upheld);
    }
    // 攻められた NPC の当主は、月の初めに知っている違反を訴える。操作する人物は自分で選ぶ（WorldSession.AppealPeaceBreach）。
    internal static StrategyState Month(ContractWorld w,StrategyState s)
    {
        if(!Enabled(w))return s;var rows=(s.Appeals??[]).ToList();w.Strategy=s;
        foreach(var f in w.NewGame!.Start.Factions.OrderBy(f=>f.Id,StringComparer.Ordinal))
        {
            var head=HouseSuccession.Head(w,new(f.Id));if(head==w.PlayerControl||w.Characters.SingleOrDefault(c=>c.Id==head)?.Life!=LifeState.Adult)continue;
            foreach(var c in Cases(w,head))if(!rows.Any(r=>r.Campaign==c.Campaign))rows.Add(Decide(w,c,head));
        }
        return rows.Count==(s.Appeals?.Length??0)?s:s with{Appeals=rows.ToArray()};
    }
    internal static void Validate(ContractWorld w,StrategyState s,StrategyState? prior)
    {
        var rows=s.Appeals??[];
        if(!Enabled(w)){if(rows.Length>0)throw new InvalidDataException("この保存では和睦の違反の訴えを記録しません");return;}
        if(prior?.Appeals is {} old&&!rows.Take(old.Length).Select(r=>r with{}).SequenceEqual(old))throw new InvalidDataException("過去の訴えが変更されています");
        if(rows.Select(r=>r.Campaign).Distinct().Count()!=rows.Length||rows.Any(r=>r.At>w.ElapsedSeconds||r.Revision>w.CommitSequence||!s.Campaigns.Any(c=>c.Id==r.Campaign&&c.Attacker==r.Breaker&&c.Defender==r.InjuredHouse&&c.At<=r.At)))
            throw new InvalidDataException("訴えが出陣の記録と合いません");
    }
    // 悪名：違反を認めた訴えの、破った家の当主。
    internal static IEnumerable<RenownFact> KnownInfamy(ContractWorld w,EntityId observer)
    {
        if(!Enabled(w))yield break;var kyoto=CentralCourtCatalog.KyotoIn(w);
        foreach(var a in w.Strategy?.Appeals??[])
        {
            if(!a.Upheld)continue;
            long learned=observer==a.Appellant||observer==a.Broker||observer==a.BreakerHead?a.At:a.At+HistoricalNews.DelayFrom(w,observer,kyoto,a.At);
            if(learned>w.ElapsedSeconds)continue;
            yield return new("peace-breach:"+a.Campaign.Value,observer,a.BreakerHead,RenownKind.Infamy,a.At,learned,kyoto,"和睦を破ったと取次が認めた",true);
        }
    }
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)=>
        Expired(w,viewer).Select(x=>new PersonalChronicleRow("mediation-expired:"+x.Proposal,"仲裁","仲裁案の返事がそろわず失効",null,x.ExpiredAt,"仲裁"));
}

public sealed partial class WorldSession
{
    // 和睦を破られた家の当主が、取次へ違反を訴える（一覧の CentralStanding の世界だけ）。
    public CommandResult AppealPeaceBreach(EntityId campaign,long revision)
    {
        lock(gate)
        {
            var id=new EntityId("request:peace-appeal/"+ContractJson.Hash(System.Text.Encoding.UTF8.GetBytes(campaign.Value))[..16]);
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,id,state.CommitSequence,"表示を更新してください");
            var c=PeaceBreach.Cases(state,state.PlayerControl).FirstOrDefault(x=>x.Campaign==campaign);
            if(c==null)return new(ResultCode.NotAvailable,id,state.CommitSequence,"訴えられる違反はありません");
            Change(w=>{var a=PeaceBreach.Decide(w,c,w.PlayerControl);w.Strategy=w.Strategy! with{Appeals=[..w.Strategy.Appeals??[],a with{Revision=w.CommitSequence}]};});
            return new(ResultCode.Accepted,id,state.CommitSequence,"取次へ訴えた");
        }
    }
}
