namespace HakutaiKagami.Core.Contracts;

// Evidence-filtered presentation over the existing sealed letter and assent protocol.
public static class IncomingMission
{
    public const string Ruleset="officer-incoming-mission-v1";
    public const string Contract="sealed-lord-binding-clerk-voluntary-inspect-delivery1-work4-report1-v1";
    public static bool Enabled(ContractWorld w)=>w.Definitions.Ruleset==Ruleset;
    public static ContractWorld Create()
    {
        var w=OfficerLoop.Create();w.WorldId=new("world:"+Ruleset);
        w.Definitions=w.Definitions with{Id=new("scenario:"+Ruleset),Ruleset=Ruleset};
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};
        ContractValidation.Validate(w);return w;
    }
    public static Intent Build(ContractWorld w,string id,bool subordinate,int due=60)
    {
        if(!Enabled(w)||!EntityId.IsValid(id)||due is <1 or >3600)throw new InvalidDataException("Unsupported incoming mission");
        long end=checked(w.ElapsedSeconds+due);
        return new(new(id),subordinate?ContractFixture.Clerk:ContractFixture.Lord,w.PlayerControl,ContractFixture.Castle,
            ActionKind.Inspect,null,null,0,null,new(DeadlineCriterion.Completion,end,false,null,end),w.ElapsedSeconds,1,4,1,true,
            subordinate?TaskKind.VoluntaryRequest:TaskKind.BindingOrder);
    }
    internal static void ValidateOrder(ContractWorld w,Intent i)
    {
        long due=(i.Deadline.At??-1)-w.ElapsedSeconds;
        if(due is <1 or >3600||i!=Build(w,i.RequestId.Value,i.Actor==ContractFixture.Clerk,(int)due))
            throw new InvalidDataException("Unsupported incoming mission fields");
        if(i.Actor==ContractFixture.Lord&&!w.ServiceBonds.Any(b=>b.Subject==i.Executor&&b.Target==i.Actor&&b.From<=w.ElapsedSeconds&&w.ElapsedSeconds<b.Until))
            throw new InvalidDataException("No lord service bond");
        if(i.Actor==ContractFixture.Clerk&&!w.ServiceBonds.Any(b=>b.Subject==i.Actor&&b.Target==i.Executor&&b.From<=w.ElapsedSeconds&&w.ElapsedSeconds<b.Until))
            throw new InvalidDataException("No subordinate service bond");
    }
    public sealed record Card(EntityId Message,EntityId? Task,string Text,bool CanOpen,bool CanDecide);
    public static Card[] View(WorldSession session,EntityId actor)
    {
        var v=session.GetActorView(actor);var w=session.DiagnosticSnapshot();
        return v.Messages.Where(m=>w.Messaging.Messages.Single(x=>x.Id==m.Id.Value).Kind=="contract-order").Select(m=>{
            if(m.KnownBody==null)return new Card(m.Id,null,"書状到着・未開封（内容未確認）",m.Sealed,false);
            var t=w.Tasks.Single(t=>t.Id.Value==m.KnownBody);var i=w.Requests.Single(r=>r.Id==t.RequestId).Intent;
            bool decide=t.Executor==actor&&t.Agreement==AgreementPhase.AwaitingAssent;
            string reason=t.Executor!=actor?"判断は受令者が行います":t.Agreement==AgreementPhase.Declined?"本人が任意依頼を辞退":t.Agreement==AgreementPhase.Expired?"期限を超過":decide?"任意依頼のため本人の受諾が必要":i.Kind==TaskKind.BindingOrder?"主従関係に基づく命令・開封後に履行判定":"本人が任意依頼を受諾";
            string status=v.Tasks.Single(x=>x.Id==t.Id).KnownStatus;
            return new Card(m.Id,t.Id,(i.Actor==ContractFixture.Lord?"主君の命令":"書役からの依頼")+$"：在所の点検 / 期限{i.Deadline.At}秒\n"+reason+" / "+status,false,decide);
        }).ToArray();
    }
}
