namespace HakutaiKagami.Core.Contracts;

// A bounded input adapter. The common task, authority, deadline and report machinery remains authoritative.
public static class MissionEntry
{
    public const string Ruleset = "officer-mission-entry-v1";
    public const string Contract = "inspect-transfer-own-or-clerk-quantity1to100-deadline1to3600-main4-report1-v1";
    public static bool Enabled(ContractWorld w) => w.Definitions.Ruleset == Ruleset;
    public static ContractWorld Create(bool refuses=false)
    {
        var w=OfficerLoop.Create(refuses);
        w.WorldId=new("world:officer-mission-entry-v1");
        w.Definitions=w.Definitions with{Id=new("scenario:officer-mission-entry-v1"),Ruleset=Ruleset};
        w.Definitions=w.Definitions with{Version=RegionalBattle.Version(w)};
        ContractValidation.Validate(w);return w;
    }
    public static Intent Build(ContractWorld w,string id,ActionKind purpose,EntityId executor,long quantity,int dueInSeconds)
    {
        if(!Enabled(w)||purpose is not(ActionKind.Inspect or ActionKind.Transfer)||
            executor!=ContractFixture.Officer&&executor!=ContractFixture.Clerk||
            dueInSeconds is <1 or >3600||purpose==ActionKind.Transfer&&(quantity<1||quantity>100)||purpose==ActionKind.Inspect&&quantity!=0)
            throw new InvalidDataException("任務の内容・担当者・数量・期限を確認してください");
        var deadline=checked(w.ElapsedSeconds+dueInSeconds);
        return new(new(id),w.PlayerControl,executor,ContractFixture.Castle,purpose,
            purpose==ActionKind.Transfer?ContractFixture.Source:null,purpose==ActionKind.Transfer?ContractFixture.Treasury:null,
            quantity,null,new(DeadlineCriterion.Completion,deadline,false,null,deadline),w.ElapsedSeconds,1,4,1,false,TaskKind.BindingOrder);
    }
    internal static void ValidateOrder(ContractWorld w,Intent i)
    {
        var due=i.Deadline.At??throw new InvalidDataException("Completion deadline required");
        if(due-w.ElapsedSeconds is <1 or >3600||i!=Build(w,i.RequestId.Value,i.Action,i.Executor,i.Amount,checked((int)(due-w.ElapsedSeconds))))
            throw new InvalidDataException("Unsupported mission fields");
    }
}
