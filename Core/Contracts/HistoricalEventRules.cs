namespace HakutaiKagami.Core.Contracts;

// 前提の真実と、行為者が知っている前提を別々に入力する。事件は行動を実行しない。
public sealed record HistoricalEventPremise(string Id,string Candidate,string Source,bool PhysicallyPossible,bool KnownByActor);
public static class HistoricalEventRules
{
    public const string OwariEdition="owari-road-observation-1560-v1";
    public static HistoricalEventPremise? OwariScouting(ContractWorld world,EntityId actor,long at,long revision)
    {
        if(world.NewGame?.Judgment?.EventEdition!=OwariEdition || actor.Value!="person:nobunaga" ||
            world.EpochDate!="1560-01-01" || at<0 || at>=366L*86400)return null;
        var castle=RegionalEconomy.Site;
        if(!TransportWork.At(world,actor,castle,at,revision,true) || Assassination.DeadAt(world,actor,at,revision) ||
            HouseSuccession.Head(world,new("org:oda"),at,revision)!=actor || !LocalWorkDemand.Enabled(world))return null;
        var need=LocalWorkDemand.At(world,actor,castle,at,revision);
        if(!need.Needed || need.MissingEdges.Length+need.StaleEdges.Length==0)return null;
        return new("owari-road-observation","inspect","kiyosu-city",true,true);
    }
    public static bool IsOwariOrder(ContractWorld w,EntityId request)
    {
        if(w.NewGame?.Judgment?.EventEdition!=OwariEdition)return false;
        return w.NpcWork?.Decisions.Any(d=>d.Request==request && d.Actor.Value=="person:nobunaga" &&
            d.Inputs.Routine?.Judgment?.ChoiceInput.Candidates.Any(c=>c.Id=="inspect" && c.MatchesHistoricalGoal)==true)==true;
    }
    public static NpcChoiceInput Apply(NpcChoiceInput input,HistoricalEventPremise[] premises,bool playerDecision,bool continuedAfterHistory)
    {
        if(premises==null||premises.Any(p=>p==null||string.IsNullOrWhiteSpace(p.Id)||string.IsNullOrWhiteSpace(p.Candidate)||string.IsNullOrWhiteSpace(p.Source))||premises.Select(p=>p.Id).Distinct(StringComparer.Ordinal).Count()!=premises.Length)throw new InvalidDataException("事件の前提と出典が不正です");
        if(playerDecision)return input;
        var eligible=premises.Where(p=>p.PhysicallyPossible&&p.KnownByActor).Select(p=>p.Candidate).ToHashSet(StringComparer.Ordinal);
        return input with{History=continuedAfterHistory?HistoryMode.Fictional:input.History,Candidates=input.Candidates.Select(c=>c with{MatchesHistoricalGoal=c.MatchesHistoricalGoal||eligible.Contains(c.Id)}).ToArray()};
    }
}
public sealed record PersonalChronicleRow(string Id,string Kind,string Text,long? OccurredAt,long LearnedAt,string Source,string? House=null);
public sealed partial class WorldSession
{
    public PersonalChronicleRow[] PersonalChronicle(EntityId actor,EntityId? house=null)
    {
        lock(gate)
        {
            var view=GetActorView(actor);
            var rows=state.Knowledge.Where(k=>k.Character==actor&&k.LearnedAt<=state.ElapsedSeconds).Select(k=>{
                var e=state.Evidence.Single(e=>e.Id==k.Evidence);
                return new PersonalChronicleRow(e.Id.Value,"認識した記録",e.Claim,e.ObservedAt,k.LearnedAt,e.Message?.Value??"目撃・本人の記録",state.Definitions.Organizations.Any(o=>o.Id==e.Subject)?e.Subject.Value:null);
            }).Concat(state.Chronicle.Where(c=>c.Actor==actor).Select(c=>new PersonalChronicleRow(c.Id.Value,"本人の経緯",c.Text,null,c.At,c.Id.Value)))
              .Concat(view.Messages.Where(m=>m.KnownBody!=null).Select(m=>{
                var msg=state.Messaging.Messages.Single(x=>x.Id==m.Id.Value);
                return new PersonalChronicleRow(msg.Id,"開封済みの書状","書状を開封（本文は受信箱で確認）",null,HakutaiKagami.Core.Messaging.MessageFlow.LearnedAt(state.Messaging,msg.Id,actor.Value)!.Value,"書状:"+msg.Id);
              }));
            rows=rows.Concat(SocialWorld.ChronicleRows(state,actor));
            return rows.Where(x=>house==null||x.House==house.Value.Value).GroupBy(x=>x.Id,StringComparer.Ordinal).Select(g=>g.First()).OrderBy(x=>x.LearnedAt).ThenBy(x=>x.Id,StringComparer.Ordinal).ToArray();
        }
    }
}
