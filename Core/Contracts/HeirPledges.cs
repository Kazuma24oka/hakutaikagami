using HakutaiKagami.Core.Messaging;

namespace HakutaiKagami.Core.Contracts;

public static class HeirPledges
{
    public const string RequestKind="heir-pledge-request", ReplyKind="heir-pledge-reply";
    public static string Version(HeirNomination paper)=>FormattableString.Invariant($"{paper.House.Value}/{paper.Revision}");
    static string Id(string kind,HeirNomination paper,string signer)=>InformationSchemes.Id(kind,Version(paper)+"/"+signer).Value;
    public static IEnumerable<SetupPerson> Contacts(ContractWorld w,HeirNomination paper)=>
        w.NewGame!.Start.People.Where(p=>p.Faction==paper.House.Value && p.Id!=paper.Head.Value && p.Id!=paper.Heir.Value);
    public static bool Requested(ContractWorld w,HeirNomination paper,string signer)=>
        w.Messaging.Messages.Any(m=>m.Id==Id(RequestKind,paper,signer));

    static void Dispatch(ContractWorld w,MessageEnvelope letter)
    {
        MessageFlow.Send(w.Messaging,letter,w.ElapsedSeconds);
        w.SealedMessages=[..w.SealedMessages,new(letter.Id)];
        RegionalCampaign.Bind(w);
    }

    const string OutsideOrder="家法の第一候補と異なる指名です。";
    internal static void Request(ContractWorld w,HeirNomination paper,SetupPerson signer)
    {
        var name=FamilyBirth.People(w).Single(p=>p.Id==paper.Heir.Value).Name;
        var body=name+"を後継とする指名への返答を求めます。";
        if(HeirDesignation.UsesSuccessionOrder(w,paper.House) &&
            HeirDesignation.KnownChildren(w,paper.House).FirstOrDefault() is {} first && first.Person!=paper.Heir){
            var preferred=FamilyBirth.People(w).Single(p=>p.Id==first.Person.Value);
            body+="第一候補は"+preferred.Name+"殿です。"+OutsideOrder;
        }
        Dispatch(w,new(Id(RequestKind,paper,signer.Id),Version(paper),RequestKind,paper.Head.Value,
            signer.Id,body,signer.Site,w.ElapsedSeconds));
    }

    internal static void Validate(ContractWorld w)
    {
        foreach(var letter in w.Messaging.Messages.Where(m=>m.Kind is RequestKind or ReplyKind))
        {
            var paper=w.HeirNominations?.SingleOrDefault(p=>Version(p)==letter.CorrelationId);
            if(paper==null || letter.CreatedAt<paper.At || !w.SealedMessages.Contains(new(letter.Id)))
                throw new InvalidDataException("誓紙の指名原本・日付・封書が一致しません");
            bool outbound=letter.Kind==RequestKind;
            string signer=outbound?letter.RecipientId:letter.SenderId;
            if(!Contacts(w,paper).Any(p=>p.Id==signer) ||
                (outbound?letter.SenderId:letter.RecipientId)!=paper.Head.Value ||
                letter.Id!=Id(letter.Kind,paper,signer))
                throw new InvalidDataException("誓紙の宛先・作成者が指名原本と一致しません");
            if(!outbound && MessageFlow.LearnedAt(w.Messaging,Id(RequestKind,paper,signer),signer)!=letter.CreatedAt)
                throw new InvalidDataException("家臣が受け取った指名状に対応しない返答です");
        }
    }

    internal static void Receive(ContractWorld w)
    {
        if(w.HeirNominations==null)return;
        foreach(var request in w.Messaging.Messages.Where(m=>m.Kind==RequestKind &&
            !MessageFlow.Knows(w.Messaging,m.Id,m.RecipientId)))
        {
            if(!w.Messaging.Receipts.Any(r=>r.MessageId==request.Id && r.ReceiverId==request.RecipientId))continue;
            var signer=new EntityId(request.RecipientId);
            if(Assassination.DeadAt(w,signer,w.ElapsedSeconds,w.CommitSequence))continue;
            var paper=w.HeirNominations.Single(p=>Version(p)==request.CorrelationId);
            w.Messaging.Knowledge=[..w.Messaging.Knowledge,new(request.Id,signer.Value,w.ElapsedSeconds,request.Id)];
            bool member=w.Memberships.Any(m=>m.Target==paper.House && m.Subject==signer &&
                m.From<=w.ElapsedSeconds && w.ElapsedSeconds<m.Until);
            bool outside=request.Body.EndsWith(OutsideOrder,StringComparison.Ordinal);
            bool agrees=member && w.Characters.Single(c=>c.Id==signer).Life==LifeState.Adult &&
                FamilyConsentRules.IndividualAccepts(w,signer,paper.Heir,w.ElapsedSeconds,w.CommitSequence,outside?100:80);
            var heir=FamilyBirth.People(w).Single(p=>p.Id==paper.Heir.Value);
            var address=FamilyBirth.People(w).Single(p=>p.Id==paper.Head.Value).Site;
            var body=agrees?heir.Name+"殿の後継指名を支持すると誓います。":heir.Name+"殿への誓紙は差し控えます。";
            if(outside)body="家法の第一候補を外すとのお書状を拝見しました。"+body;
            Dispatch(w,new(Id(ReplyKind,paper,signer.Value),Version(paper),ReplyKind,signer.Value,
                paper.Head.Value,body,address,w.ElapsedSeconds));
        }
    }
}

public sealed partial class WorldSession
{
    public CommandResult RequestHeirPledge(HeirNomination paper,EntityId signer,long revision)
    {
        lock(gate)
        {
            if(state.PlayerControl!=paper.Head || state.HeirNominations?.LastOrDefault(p=>p.House==paper.House)!=paper ||
                FactionStances.ResolveAuthority(state,paper.Head,paper.House,state.ElapsedSeconds,state.CommitSequence)==null)
                return new(ResultCode.NotAvailable,signer,state.CommitSequence,"現当主の有効な指名状が必要です");
            if(HeirPledges.Requested(state,paper,signer.Value))
                return new(ResultCode.AlreadyAccepted,signer,state.CommitSequence,"この指名への書状は送付済みです");
            if(revision!=state.CommitSequence)return new(ResultCode.StateChanged,signer,state.CommitSequence,"指名を再確認してください");
            var contact=HeirPledges.Contacts(state,paper).SingleOrDefault(p=>p.Id==signer.Value);
            if(!CharacterRelations.Enabled(state) || contact==null || SocialWorld.Site(state,paper.Head)==null ||
                Assassination.DeadAt(state,paper.Head,state.ElapsedSeconds,revision))
                return new(ResultCode.NotAvailable,signer,state.CommitSequence,"差出人の所在と家臣の宛先を確認してください");
            Change(w=>HeirPledges.Request(w,paper,contact));
            return new(ResultCode.Accepted,signer,state.CommitSequence,"後継指名を伝える書状を送りました");
        }
    }
}
