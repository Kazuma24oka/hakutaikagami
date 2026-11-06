using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public static partial class CoalitionPacts
{
    public const string Certificate="coalition-certificate";
    static string AnswerLetter(CourtEvent answer)=>HouseholdCourt.Id("coalition-answer",answer.Input.Id.Value).Value;
    static CourtEvent[] Confirmed(ContractWorld w,EntityId offer,long rev)=>HouseholdCourt.Events(w)
        .Where(e=>e.Input.Action==CourtAction.ConfirmCoalition && e.Input.Reference==offer && e.Revision<rev).ToArray();
    static void CheckConfirmation(ContractWorld w,CourtInput i,EntityId actor,long at,long rev){
        var offer=HouseholdCourt.Events(w).SingleOrDefault(e=>e.Input.Action==CourtAction.ProposeCoalition && e.Input.Id==i.Reference && e.At<=at && e.Revision<rev);
        if(offer==null || offer.Actor!=actor || i.Person!=actor || i.Owner!=offer.Input.Owner || i.Site!=offer.Place ||
            offer.Input.Coalition!.AssemblyAt<=at || i.CoalitionSignatures is not {Length:>=2} signatures ||
            !signatures.SequenceEqual(signatures.Distinct().OrderBy(k=>k.Value,StringComparer.Ordinal)))
            throw new InvalidDataException("呼びかけ人が期限内に、異なる二家以上の加盟返書を確認してください");
        foreach(var id in signatures){
            var answer=HouseholdCourt.Events(w).SingleOrDefault(e=>e.Input.Id==id && e.Input.Reference==i.Reference && e.Input.Action==CourtAction.JoinCoalition && e.Revision<rev);
            if(answer==null || MessageFlow.LearnedAt(w.Messaging,AnswerLetter(answer),actor.Value) is not {} opened || opened>at)
                throw new InvalidDataException("本人が開いた同一原本の加盟返書だけを取りまとめられます");
        }
        var old=Confirmed(w,offer.Input.Id,rev).LastOrDefault();
        if(old!=null && (old.Input.CoalitionSignatures!.Except(signatures).Any() || signatures.Length<=old.Input.CoalitionSignatures!.Length))
            throw new InvalidDataException("追加の返書を得たときだけ、署名した家を追記できます");
    }
    static IEnumerable<MessageEnvelope> Certificates(ContractWorld w,CourtEvent confirmation){
        var answers=confirmation.Input.CoalitionSignatures!.Select(id=>HouseholdCourt.Events(w).Single(e=>e.Input.Id==id)).ToArray();
        var names=answers.Select(a=>a.Input.Owner).Prepend(confirmation.Input.Owner)
            .Select(h=>w.Definitions.Organizations.Single(o=>o.Id==h).Name);
        var body="包囲網の盟約が成立。署名した家："+string.Join("、",names)+"。";
        foreach(var answer in answers){
            var id=HouseholdCourt.Id("coalition-certificate",confirmation.Input.Id.Value+"/"+answer.Input.Owner.Value);
            yield return new(id.Value,confirmation.Input.Reference!.Value.Value,Certificate,confirmation.Actor.Value,answer.Actor.Value,body,
                answer.Place.Value,confirmation.At){DeliverySite=answer.Place.Value};
        }
    }
    public static IEnumerable<CourtInput> Confirmations(ContractWorld w,EntityId actor){
        var rows=HouseholdCourt.Events(w);
        foreach(var offer in rows.Where(e=>e.Input.Action==CourtAction.ProposeCoalition && e.Actor==actor)){
            var selected=rows.Where(e=>e.Input.Action==CourtAction.JoinCoalition && e.Input.Reference==offer.Input.Id &&
                MessageFlow.Knows(w.Messaging,AnswerLetter(e),actor.Value)).Select(e=>e.Input.Id).OrderBy(k=>k.Value,StringComparer.Ordinal).ToArray();
            if(selected.Length<2)continue;
            var id=HouseholdCourt.Id("confirm-coalition",offer.Input.Id.Value+"/"+string.Join("/",selected.Select(k=>k.Value)));
            var i=new CourtInput(id,CourtAction.ConfirmCoalition,actor,offer.Input.Owner,offer.Place,Reference:offer.Input.Id){CoalitionSignatures=selected};
            try{Check(w,i,actor,offer.Place,w.ElapsedSeconds,w.CommitSequence+1);}catch(InvalidDataException){continue;}
            yield return i;
        }
    }
    internal static IEnumerable<(CourtInput Input,EntityId Actor)> NpcConfirmations(ContractWorld w){
        foreach(var actor in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.ProposeCoalition && e.Actor!=w.PlayerControl).Select(e=>e.Actor).Distinct()){
            foreach(var m in w.Messaging.Messages.Where(m=>m.Kind==Reply && m.RecipientId==actor.Value &&
                m.DeliverySite==SocialWorld.Site(w,actor)?.Value && w.Messaging.Receipts.Any(r=>r.MessageId==m.Id && r.ReceiverId==actor.Value) && !MessageFlow.Knows(w.Messaging,m.Id,actor.Value)))
                w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(m.Id,actor.Value,w.ElapsedSeconds,m.Id)];
            foreach(var input in Confirmations(w,actor))yield return(input,actor);
        }
    }
    public sealed record KnownPact(EntityId Original,EntityId Target,CoalitionPurpose Purpose,long AssemblyAt,EntityId[] Houses,long ObservedAt);
    public static KnownPact[] Known(ContractWorld w,EntityId observer){
        var rows=HouseholdCourt.Events(w);
        var records=new Dictionary<EntityId,KnownPact>();
        foreach(var e in rows.Where(x=>x.Input.Action==CourtAction.ConfirmCoalition)){
            long observed=e.At;
            if(e.Actor!=observer){
                var addressed=Certificates(w,e).SingleOrDefault(m=>m.RecipientId==observer.Value);
                if(addressed==null || MessageFlow.LearnedAt(w.Messaging,addressed.Id,observer.Value) is not {} learned)continue;
                observed=learned;
            }
            var original=rows.Single(x=>x.Input.Id==e.Input.Reference);
            var terms=original.Input.Coalition!;
            var houses=e.Input.CoalitionSignatures!.Select(id=>rows.Single(x=>x.Input.Id==id).Input.Owner).Prepend(e.Input.Owner).ToArray();
            records[original.Input.Id]=new(original.Input.Id,terms.Target,terms.Purpose,terms.AssemblyAt,houses,observed);
        }
        foreach(var departure in rows.Where(e=>e.Input.Action==CourtAction.LeaveCoalition)){
            var signed=rows.Single(e=>e.Input.Id==departure.Input.Reference);
            var original=signed.Input.Reference!.Value;
            if(!records.TryGetValue(original,out var pact))continue;
            long observed=departure.At;
            if(departure.Actor!=observer){
                var notice=WithdrawalLetters(w,departure).SingleOrDefault(m=>m.RecipientId==observer.Value);
                if(notice==null || MessageFlow.LearnedAt(w.Messaging,notice.Id,observer.Value) is not {} learned)continue;
                observed=learned;
            }
            records[original]=pact with{Houses=pact.Houses.Where(h=>h!=departure.Input.Owner).ToArray(),ObservedAt=Math.Max(pact.ObservedAt,observed)};
        }
        return records.Values.ToArray();
    }
    internal static void ReadMemberCopies(ContractWorld w){
        foreach(var m in w.Messaging.Messages.Where(m=>(m.Kind==Certificate || m.Kind==Warning || m.Kind==Withdrawal || m.Kind==ActivityReport) && m.RecipientId!=w.PlayerControl.Value)){
            var actor=new EntityId(m.RecipientId);
            if(!MessageFlow.Knows(w.Messaging,m.Id,m.RecipientId) && w.Messaging.Receipts.Any(r=>r.MessageId==m.Id && r.ReceiverId==m.RecipientId) &&
                SocialWorld.Site(w,actor)?.Value==m.DeliverySite && !Assassination.DeadAt(w,actor,w.ElapsedSeconds,w.CommitSequence))
                w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(m.Id,m.RecipientId,w.ElapsedSeconds,m.Id)];
        }
    }
    public static IEnumerable<(string Id,string? Body)> DeliveredPapers(ContractWorld w,EntityId actor){
        foreach(var m in w.Messaging.Messages.Where(m=>(m.Kind==Reply || m.Kind==Certificate || m.Kind==Warning || m.Kind==Withdrawal || m.Kind==ActivityReport) && m.RecipientId==actor.Value &&
            w.Messaging.Receipts.Any(r=>r.MessageId==m.Id && r.ReceiverId==actor.Value)))
            yield return(m.Id,MessageFlow.Knows(w.Messaging,m.Id,actor.Value)?m.Body:null);
    }
}
