using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;

public static class HouseScaleNotices
{
    public const string Kind="house-scale-declaration";
    public static string Name(CourtScale scale)=>scale switch
    {
        CourtScale.Local=>"国人衆",CourtScale.Daimyo=>"大名",CourtScale.Great=>"大大名",CourtScale.Hegemon=>"覇者",
        _=>throw new ArgumentOutOfRangeException(nameof(scale))
    };
    public static string Letter(EntityId declaration,EntityId recipient)=>HouseholdCourt.Id("scale-notice",declaration.Value+"/"+recipient.Value).Value;

    internal static void Dispatch(ContractWorld w,CourtInput input,EntityId head)
    {
        var members=w.Memberships.Where(r=>r.Target==input.Owner && r.From<=w.ElapsedSeconds && w.ElapsedSeconds<r.Until)
            .Select(r=>r.Subject).Distinct();
        foreach(var person in members)
        {
            if(person==head || Assassination.DeadAt(w,person,w.ElapsedSeconds,w.CommitSequence))continue;
            var address=FamilyBirth.People(w).Single(p=>p.Id==person.Value).Site;
            var letter=new MessageEnvelope(Letter(input.Id,person),input.Id.Value,Kind,head.Value,person.Value,
                "当主は「"+Name(input.Scale)+"」の規模で家中を整えると宣言した。",address,w.ElapsedSeconds);
            MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);
            w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
        }
        RegionalCampaign.Bind(w);
    }

    internal static void Receive(ContractWorld w)
    {
        if(w.Court==null)return;
        foreach(var letter in w.Messaging.Messages.Where(m=>m.Kind==Kind && m.RecipientId!=w.PlayerControl.Value))
        {
            if(MessageFlow.Knows(w.Messaging,letter.Id,letter.RecipientId))continue;
            var receipt=w.Messaging.Receipts.FirstOrDefault(r=>r.MessageId==letter.Id && r.ReceiverId==letter.RecipientId);
            if(receipt!=null && !Assassination.DeadAt(w,new(letter.RecipientId),w.ElapsedSeconds,w.CommitSequence))
                w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(letter.Id,letter.RecipientId,w.ElapsedSeconds,letter.Id)];
        }
    }

    public static CourtScale Known(ContractWorld w,EntityId observer,EntityId owner,long at=long.MaxValue,long rev=long.MaxValue)
    {using var _ccRev=ControlContext.At(rev);
        var scale=CourtScale.Local;
        foreach(var e in HouseholdCourt.Events(w).Where(e=>e.Input.Action==CourtAction.DeclareScale && e.Input.Owner==owner &&
            e.At<=at && e.Revision<=rev))
        {
            var id=Letter(e.Input.Id,observer);
            if(e.Actor==observer || MessageFlow.LearnedAt(w.Messaging,id,observer.Value) is {} learned && learned<=at)
                scale=e.Input.Scale;
        }
        return scale;
    }
}
