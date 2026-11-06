using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static partial class CoalitionPacts
{
    static void CheckWithdrawal(ContractWorld w,CourtInput input,EntityId actor,long at,long rev){
        var rows=HouseholdCourt.Events(w).Where(e=>e.Revision<rev && e.At<=at).ToArray();
        var cert=rows.SingleOrDefault(e=>e.Input.Id==input.Reference && e.Input.Action==CourtAction.ConfirmCoalition);
        if(cert==null || input.Person!=cert.Actor || input.Owner==cert.Input.Owner)throw new InvalidDataException("自家宛の成立通知を指定してください");
        var notice=Certificates(w,cert).SingleOrDefault(m=>m.RecipientId==actor.Value);
        if(notice==null || MessageFlow.LearnedAt(w.Messaging,notice.Id,actor.Value) is not {} opened || opened>at ||
           !cert.Input.CoalitionSignatures!.Any(id=>rows.Any(e=>e.Input.Id==id && e.Input.Owner==input.Owner && e.Actor==actor)) ||
           rows.Any(e=>e.Input.Action==CourtAction.LeaveCoalition && e.Input.Owner==input.Owner &&
                rows.Any(c=>c.Input.Id==e.Input.Reference && c.Input.Reference==cert.Input.Reference)))
            throw new InvalidDataException("成立を知った署名家が同じ盟約から一度だけ離脱できます");
    }
    public static IEnumerable<CourtInput> Withdrawals(ContractWorld w,EntityId actor){
        var rows=HouseholdCourt.Events(w);var home=SocialWorld.Site(w,actor);if(home==null)yield break;
        var seen=new HashSet<EntityId>();
        foreach(var cert in rows.Reverse().Where(e=>e.Input.Action==CourtAction.ConfirmCoalition)){
            if(seen.Contains(cert.Input.Reference!.Value))continue;
            var sign=cert.Input.CoalitionSignatures!.Select(id=>rows.Single(e=>e.Input.Id==id)).SingleOrDefault(e=>e.Actor==actor);
            if(sign==null)continue;
            var input=new CourtInput(HouseholdCourt.Id("leave-coalition",cert.Input.Id.Value+"/"+actor.Value),CourtAction.LeaveCoalition,cert.Actor,sign.Input.Owner,home.Value,Reference:cert.Input.Id);
            try{Check(w,input,actor,home.Value,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
            seen.Add(cert.Input.Reference.Value);yield return input;
        }
    }
    static IEnumerable<MessageEnvelope> WithdrawalLetters(ContractWorld w,CourtEvent departure){
        var rows=HouseholdCourt.Events(w);var cert=rows.Single(e=>e.Input.Id==departure.Input.Reference);
        var founder=rows.Single(e=>e.Input.Id==cert.Input.Reference);
        var others=cert.Input.CoalitionSignatures!.Select(id=>rows.Single(e=>e.Input.Id==id)).Where(e=>e.Input.Owner!=departure.Input.Owner).Prepend(founder);
        var name=w.Definitions.Organizations.Single(o=>o.Id==departure.Input.Owner).Name;
        foreach(var recipient in others){
            var id=HouseholdCourt.Id("coalition-leave-notice",departure.Input.Id.Value+"/"+recipient.Input.Owner.Value);
            yield return new(id.Value,founder.Input.Id.Value,Withdrawal,departure.Actor.Value,recipient.Actor.Value,
                name+"はこの盟約から離脱する。",recipient.Place.Value,departure.At){DeliverySite=recipient.Place.Value};
        }
    }
}
