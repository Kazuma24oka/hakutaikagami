namespace HakutaiKagami.Core.Contracts;
public sealed record RepresentativeContactCandidate(EntityId Owner,EntityId Acceptance,EntityId Signer,RepresentativeObservation Target,long Until);
public static partial class TruceConfirmations
{
    static bool IsRequest(TruceConfirmationKind k)=>k is TruceConfirmationKind.Confirmation or TruceConfirmationKind.RepresentativeInquiry;
    static void InquirySigner(ContractWorld w,EntityId owner,EntityId acceptance,EntityId signer,EntityId requester,EntityId home,long at,long rev){
        if(signer!=requester){LocalSigner(w,owner,acceptance,signer,requester,home,at,rev);return;}
        Need(Local(w,signer,home,at,rev)&&!PersonalLife.Resting(w,at,rev)&&!CastleAdministration.Blocked(w,home,at,rev)&&TruceNegotiations.Known(w,signer,at,rev).Any(f=>f.Acceptance==acceptance),"本人が現地で確認した不戦原本が必要です");
    }
    static TruceConfirmationPaper Inquiry(ContractWorld w,TruceConfirmationInput i,EntityId actor,long at,long rev){
        long before=rev-1;var signer=HouseSuccession.Head(w,i.Owner,at,before);var auth=FactionStances.ResolveAuthority(w,signer,i.Owner,at,before);Need(auth!=null,"現当主の署名権限が必要です");
        var f=TruceNegotiations.Known(w,signer,at,before).SingleOrDefault(f=>f.Acceptance==i.Acceptance);Need(f!=null&&at<f.Terms.Until&&(f.Terms.A==i.Owner||f.Terms.B==i.Owner),"期限内の既知の成立原本が必要です");
        InquirySigner(w,i.Owner,i.Acceptance,signer,actor,auth.Home,at,before);
        var owner=f.Terms.A==i.Owner?f.Terms.B:f.Terms.A;var target=RepresentativeKnowledge.Known(w,signer,owner,at,before);Need(target!=null,"相手代表者の認識がありません");
        Need(i.Original==null&&!Papers(w,at,before).Any(p=>p.Kind==TruceConfirmationKind.RepresentativeInquiry&&p.Owner==i.Owner&&p.Acceptance==i.Acceptance&&p.Sender==signer&&p.Recipient==target.Person),"同じ代表者同士の照会は発送済みです。不達は元の照会を再送してください");
        string id=MessageId(i.Id);return new(id,i.Id,TruceConfirmationKind.RepresentativeInquiry,i.Owner,i.Acceptance,f.Terms,signer,target.Person,target.Home,auth.Home,actor,at,rev,auth,id,null,null,target.ObservedAt,target.Source);
    }
    static TruceConfirmationPaper Reconfirm(ContractWorld w,TruceConfirmationPaper p,EntityId id,long at,long rev){
        var root=Root(w,p);Need(p.Kind==TruceConfirmationKind.RepresentativeInquiry&&Opened(w,p.Id,p.Recipient,at,rev-1)&&at<p.Terms.Until&&!Papers(w,at,rev-1).Any(q=>q.Root==root.Id&&q.Kind==TruceConfirmationKind.RepresentativeConfirmation),"期限内に開封した未回答の照会が必要です");
        var auth=FactionStances.ResolveAuthority(w,p.Recipient,Other(p),at,rev-1);Need(auth!=null&&TruceNegotiations.Known(w,p.Recipient,at,rev-1).Any(f=>f.Acceptance==p.Acceptance),"現代表者本人が確認した原本が必要です");
        var o=State(w).Openings.Single(o=>o.Message==p.Id&&o.Actor==p.Recipient);return new(MessageId(id),id,TruceConfirmationKind.RepresentativeConfirmation,Other(p),p.Acceptance,p.Terms,p.Recipient,p.Sender,p.Authority!.Home,auth.Home,root.Requester,at,rev,auth,root.Id,p.Id,null,o.At,p.Id);
    }
    static TruceConfirmationPaper? NextRepresentative(ContractWorld w,TruceConfirmationPaper[] ps,long at,long rev){
        foreach(var p in ps.Where(p=>p.Kind==TruceConfirmationKind.RepresentativeInquiry&&p.Recipient!=w.PlayerControl&&at<p.Terms.Until&&Opened(w,p.Id,p.Recipient,at,rev-1)&&!ps.Any(q=>q.Root==p.Root&&q.Kind==TruceConfirmationKind.RepresentativeConfirmation)))
            if(FactionStances.ResolveAuthority(w,p.Recipient,Other(p),at,rev-1)!=null)return Reconfirm(w,p,AutoId(w,"representative",p.Root),at,rev);
        foreach(var p in ps.Where(p=>p.Kind==TruceConfirmationKind.RepresentativeConfirmation&&p.Recipient!=w.PlayerControl&&Opened(w,p.Id,p.Recipient,at,rev-1)&&!ps.Any(q=>q.Root==p.Root&&q.Kind==TruceConfirmationKind.Receipt)))
            if(FactionStances.ResolveAuthority(w,p.Recipient,Other(p),at,rev-1)!=null)return Receipt(w,p,AutoId(w,"representative-receipt",p.Root),at,rev);
        return null;
    }
    public static RepresentativeContactCandidate[] RepresentativeCandidates(ContractWorld w){
        var result=new List<RepresentativeContactCandidate>();if(!Enabled(w))return [];
        foreach(var owner in w.NewGame!.Start.Factions)foreach(var f in w.Truces?.Formations??[]){try{var p=Make(w,new(new("request:representative-preview"),TruceConfirmationAction.InquireRepresentative,new(owner.Id),f.Acceptance),w.PlayerControl,w.ElapsedSeconds,w.CommitSequence+1);result.Add(new(p.Owner,p.Acceptance,p.Sender,RepresentativeKnowledge.Known(w,p.Sender,Other(p))!,p.Terms.Until));}catch(InvalidDataException){}}
        return result.ToArray();
    }
}
