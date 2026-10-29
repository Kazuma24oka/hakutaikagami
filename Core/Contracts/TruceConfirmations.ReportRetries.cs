namespace HakutaiKagami.Core.Contracts;
public static partial class TruceConfirmations
{
    // A copy of an already signed dated observation never renews political authority or terms.
    static TruceConfirmationPaper? NextReportRetry(ContractWorld w,TruceConfirmationPaper[] ps,long at,long rev)
    {
        foreach(var p in ps.Where(p=>(p.Kind is TruceConfirmationKind.Receipt or TruceConfirmationKind.FailureNotice)&&p.Sender!=w.PlayerControl)){
            var family=ps.Where(q=>q.Kind==p.Kind&&q.Root==p.Root&&q.Related==p.Related&&q.Evidence==p.Evidence&&q.ObservedAt==p.ObservedAt).ToArray();
            if(family.Length>=3||family[^1].Id!=p.Id||Failure(w,p,at,rev-1)==null)continue;
            var id=AutoId(w,"report-retry",p.Id);return p with{Id=MessageId(id),Request=id,At=at,Revision=rev,CopyOf=p.Id};
        }return null;
    }
}
