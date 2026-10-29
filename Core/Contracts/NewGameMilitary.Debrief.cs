using HakutaiKagami.Core.Messaging;
namespace HakutaiKagami.Core.Contracts;
public sealed record MilitaryDebrief(EntityId Case,EntityId Principal,EntityId Notice,string Report,long At,long Revision,int Started,int Returned,int Losses,long Used,bool Deployed,string Next);
public static partial class NewGameMilitary
{
    static MilitaryDebrief[] LedgerDebriefs(ContractWorld w,bool knownOnly=true,long? at=null,long? revision=null)
    {
        if(!WorkFollowup.Enabled(w)||w.NewMilitary is not {} s)return [];
        long t=at??w.ElapsedSeconds,r=revision??w.CommitSequence;var result=new List<MilitaryDebrief>();
        foreach(var c in Cases(w).Where(c=>Before(c.At,c.Revision,t,r))){
            var end=s.Events.SingleOrDefault(e=>e.Effect==MilitaryEffect.Returned&&EventCase(w,e)==c.Id&&Before(e.At,e.Revision,t,r));if(end==null)continue;
            var notice=s.Events.Where(e=>EventCase(w,e)==c.Id&&Before(e.At,e.Revision,t,r)&&Known(w,e,t,r)).Where(e=>e.Effect==MilitaryEffect.Returned||e.Effect==MilitaryEffect.StatusSent&&Read(w,e).Effect==MilitaryEffect.Returned&&Read(w,e).EffectiveAt==end.At).OrderBy(e=>e.Revision).FirstOrDefault();if(knownOnly&&notice==null)continue;
            var next=Cases(w).Where(x=>x.Revision>c.Revision).Select(x=>x.Revision).DefaultIfEmpty(long.MaxValue).Min();var requests=s.Requests.Where(q=>q.Revision>=c.Revision&&q.Revision<next).ToArray();var pickup=requests.SingleOrDefault(q=>q.Input.Action==NewMilitaryAction.Pickup);if(pickup==null)continue;
            var report=s.Reports.Where(p=>p.Revision>=c.Revision&&p.Revision<end.Revision&&p.Carry==0&&MessageFlow.LearnedAt(w.Messaging,p.Message,CasePrincipal(w,c.At,c.Revision).Value) is {} received&&received<=end.At).OrderByDescending(p=>p.Revision).FirstOrDefault();if(report==null)continue;
            int start=TotalStrength(w,pickup.At),loss=Math.Max(0,start-report.Soldiers);var ids=requests.Select(q=>q.Input.Id).ToHashSet();long used=s.Moves.Where(m=>ids.Contains(m.Id)).Sum(m=>m.Loading?m.Amount:-m.Amount);
            result.Add(new(c.Id,CasePrincipal(w,c.At,c.Revision),new(notice?.Message??end.Message),report.Message,end.At,end.Revision,start,report.Soldiers,loss,used,requests.Any(q=>q.Input.Action==NewMilitaryAction.Deploy),loss>0?"近場で隊列を確かめ、兵を立て直す軍務":"次の布陣の軍務"));
        }
        return result.OrderBy(x=>x.Revision).ToArray();
    }
    public static MilitaryDebrief[] Debriefs(ContractWorld w,bool knownOnly=true,long? at=null,long? revision=null)=>LedgerStates(w).SelectMany(s=>LedgerDebriefs(LedgerContext(w,s),knownOnly,at,revision)).OrderBy(x=>x.Revision).ToArray();
    public static MilitaryDebrief? FollowupBasis(ContractWorld w)
    {
        var c=Cases(w).LastOrDefault();return c==null?null:LedgerDebriefs(w,at:c.At,revision:c.Revision-1).LastOrDefault();
    }
    public static FieldPoint[] FollowupLine(ContractWorld w)
    {
        var basis=FollowupBasis(w);if(basis==null)return [];var a=Anchor(w);long offset=basis.Losses>0?10000:25000,width=basis.Losses>0?8000:12000;return [new(a.X+offset,a.Y-width),new(a.X+offset,a.Y+width)];
    }
}
