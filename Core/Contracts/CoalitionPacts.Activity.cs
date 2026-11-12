using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public static partial class CoalitionPacts
{
    public const string ActivityReport="coalition-activity-report";
    internal static bool IsActivity(CourtAction a)=>a is CourtAction.ReportCoalitionSortie or CourtAction.ReportSeparatePeace;
    // この家の当事者が確認できる実行原本と、個別に認識済みの成立原本に限定する。
    static IEnumerable<(EntityId Id,long At,long Revision)> Acts(ContractWorld w,CourtAction action,EntityId actor,EntityId owner,EntityId target,long at,long rev){using var _ccRev=ControlContext.At(rev);
        if(action==CourtAction.ReportSeparatePeace){
            foreach(var f in TruceNegotiations.Known(w,actor,at,rev))
                if(f.Terms.A==owner && f.Terms.B==target || f.Terms.A==target && f.Terms.B==owner)yield return(f.Acceptance,f.At,f.Revision);
            foreach(var f in TreatyNegotiations.Known(w,actor,at,rev))
                if(f.Terms.Clauses.HasFlag(TreatyClause.NonAggression) && (f.Terms.A==owner && f.Terms.B==target || f.Terms.A==target && f.Terms.B==owner))
                    yield return(f.Acceptance,f.At,f.Revision);
            yield break;
        }
        foreach(var march in w.NpcArmies?.Marches??[]){
            var muster=w.NpcArmies!.Musters.Single(m=>m.Id==march.Muster);
            if(muster.Owner!=owner || muster.Commander!=actor || muster.Objective?.TargetOwner!=target || march.Input.DestinationSite!=muster.Objective.TargetSite)continue;
            var task=w.Tasks.SingleOrDefault(t=>t.RequestId==march.Input.RequestId);
            if(task?.StartedAt is {} start && start<=at && march.Revision<=rev)yield return(march.Input.RequestId,start,march.Revision);
        }
        foreach(var e in w.ArmyRoad?.Entries??[]){
            if(e.Terms is not {} terms || terms.Owner!=owner || terms.Intent.Actor!=actor || e.Revision>rev || e.Input.Destination is not {} site)continue;
            if(!w.NewGame!.Start.Castles.Any(c=>c.Id==site.Value && c.Faction!=null))continue;
            var task=w.Tasks.SingleOrDefault(t=>t.RequestId==terms.Intent.RequestId);
            if(task?.StartedAt is {} start && start<=at && CastleAdministration.Owner(w,site,start,e.Revision)==target)yield return(e.Input.Id,start,e.Revision);
        }
    }
    static void CheckActivity(ContractWorld w,CourtInput i,EntityId actor,long at,long rev){using var _ccRev=ControlContext.At(rev);
        var rows=HouseholdCourt.Events(w).Where(e=>e.At<=at && e.Revision<rev).ToArray();
        var offer=rows.SingleOrDefault(e=>e.Input.Id==i.Reference && e.Input.Action==CourtAction.ProposeCoalition);
        if(offer==null || i.Person!=offer.Actor || i.ActivityEvidence==null)throw new InvalidDataException("盟約と行動原本を指定してください");
        var terms=offer.Input.Coalition!;
        if(i.Action==CourtAction.ReportCoalitionSortie && terms.Purpose==CoalitionPurpose.ForbidSeparatePeace ||
           i.Action==CourtAction.ReportSeparatePeace && terms.Purpose==CoalitionPurpose.CoordinateSorties)
            throw new InvalidDataException("その条項はこの盟約にありません");
        var signed=rows.Where(e=>e.Input.Action==CourtAction.ConfirmCoalition && e.Input.Reference==offer.Input.Id &&
            (e.Actor==actor && e.Input.Owner==i.Owner || e.Input.CoalitionSignatures!.Any(id=>rows.Any(x=>x.Input.Id==id && x.Input.Owner==i.Owner && x.Actor==actor)))).FirstOrDefault();
        if(signed==null || signed.Actor!=actor && !Certificates(w,signed).Any(m=>m.RecipientId==actor.Value && MessageFlow.LearnedAt(w.Messaging,m.Id,actor.Value) is {} learned && learned<=at))
            throw new InvalidDataException("本人が開いた成立通知が必要です");
        var departure=rows.FirstOrDefault(e=>e.Input.Action==CourtAction.LeaveCoalition && e.Input.Owner==i.Owner &&
            rows.Any(s=>s.Input.Id==e.Input.Reference && s.Input.Reference==offer.Input.Id));
        if(!Acts(w,i.Action,actor,i.Owner,terms.Target,at,rev-1).Any(a=>a.Id==i.ActivityEvidence && (a.At,a.Revision).CompareTo((signed.At,signed.Revision))>0 &&
            (departure==null || (a.At,a.Revision).CompareTo((departure.At,departure.Revision))<0)) ||
            rows.Any(e=>e.Input.Action==i.Action && e.Input.Reference==i.Reference && e.Input.ActivityEvidence==i.ActivityEvidence))
            throw new InvalidDataException("加盟中の実行原本を一度だけ報告できます");
    }
    public static IEnumerable<CourtInput> ActivityReports(ContractWorld w,EntityId actor){
        var home=SocialWorld.Site(w,actor);if(home==null)yield break;
        foreach(var pact in Known(w,actor)){
            var offer=HouseholdCourt.Events(w).Single(e=>e.Input.Id==pact.Original);
            foreach(var owner in pact.Houses.Where(h=>HouseSuccession.Head(w,h)==actor))
                foreach(var action in new[]{CourtAction.ReportCoalitionSortie,CourtAction.ReportSeparatePeace})
                    foreach(var a in Acts(w,action,actor,owner,pact.Target,w.ElapsedSeconds,w.CommitSequence)){
                        var id=HouseholdCourt.Id(action.ToString(),pact.Original.Value+"/"+a.Id.Value);
                        var input=new CourtInput(id,action,offer.Actor,owner,home.Value,Reference:pact.Original){ActivityEvidence=a.Id};
                        try{Check(w,input,actor,home.Value,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
                        yield return input;
                    }
        }
    }
    internal static IEnumerable<(CourtInput Input,EntityId Actor)> NpcActivity(ContractWorld w){
        var rows=HouseholdCourt.Events(w);
        foreach(var founder in rows.Where(e=>e.Input.Action==CourtAction.ProposeCoalition)){
            var certificate=Confirmed(w,founder.Input.Id,w.CommitSequence+1).LastOrDefault();
            if(certificate==null)continue;
            var actors=certificate.Input.CoalitionSignatures!.Select(id=>rows.Single(e=>e.Input.Id==id).Actor).Prepend(founder.Actor);
            foreach(var actor in actors.Where(who=>who!=w.PlayerControl)){
                foreach(var report in ActivityReports(w,actor).Where(i=>i.Reference==founder.Input.Id))yield return(report,actor);
                if(!HouseholdCourt.Events(w).Any(e=>e.Input.Action==CourtAction.ReportSeparatePeace && e.Actor==actor && e.Input.Reference==founder.Input.Id))continue;
                var leave=Withdrawals(w,actor).FirstOrDefault(i=>rows.Any(c=>c.Input.Id==i.Reference && c.Input.Reference==founder.Input.Id));
                if(leave!=null)yield return(leave,actor);
            }
        }
    }
    static IEnumerable<MessageEnvelope> ReportLetters(ContractWorld w,CourtEvent e){
        var offer=HouseholdCourt.Events(w).Single(x=>x.Input.Id==e.Input.Reference);
        var fact=Acts(w,e.Input.Action,e.Actor,e.Input.Owner,offer.Input.Coalition!.Target,e.At,e.Revision-1).First(a=>a.Id==e.Input.ActivityEvidence);
        var body=e.Input.Action==CourtAction.ReportSeparatePeace?"対象家との単独和睦が成立した。":fact.At<=offer.Input.Coalition.AssemblyAt?"約定期日までに対象家への行軍を開始した。":"約定期日を過ぎて対象家への行軍を開始した。";
        var recipients=new List<(EntityId Actor,EntityId Home)>();
        if(offer.Actor!=e.Actor)recipients.Add((offer.Actor,offer.Place));
        else{
            var signed=Confirmed(w,offer.Input.Id,e.Revision).Last();
            foreach(var id in signed.Input.CoalitionSignatures!){
                var signer=HouseholdCourt.Events(w).Single(x=>x.Input.Id==id);
                recipients.Add((signer.Actor,signer.Place));
            }
        }
        foreach(var recipient in recipients)
            yield return new(HouseholdCourt.Id("coalition-report",e.Input.Id.Value+"/"+recipient.Actor.Value).Value,offer.Input.Id.Value,
                ActivityReport,e.Actor.Value,recipient.Actor.Value,body+" 時点 "+fact.At,recipient.Home.Value,e.At){DeliverySite=recipient.Home.Value};
    }
}
