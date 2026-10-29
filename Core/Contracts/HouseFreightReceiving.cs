namespace HakutaiKagami.Core.Contracts;
public sealed record HouseFreightReceipt(EntityId Operation,EntityId Job,EntityId Home,EntityId Owner,EntityId Receiver,EntityId? Grant,long At,long Revision,long Cash,long Cargo);
public static class HouseFreightReceiving
{
    public const string Policy="house-physical-procurement-explicit-local-proxy-receipt-v11";
    public static bool Enabled(ContractWorld w)=>w.NewGame?.EconomyPolicy is Policy or MilitaryEconomy.Policy or NpcFreight.Policy or FreightOperations.Policy or FieldConvoy.Policy or ArmyRoadMarch.Policy or RoadConvoy.Policy or PersonalLife.Policy or CastleHandover.Policy;
    static void Need([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)]bool ok,string why){if(!ok)throw new InvalidDataException(why);}
    static bool Local(ContractWorld w,EntityId person,EntityId home,long at,long rev)=>w.Characters.Any(c=>c.Id==person&&c.Life==LifeState.Adult)&&TransportWork.At(w,person,home,at,rev,stableCompletion:true);
    public static HouseFreightReceipt Prepare(ContractWorld w,HouseFreightInput input,HouseFreightJob job,long at,long rev)
    {
        Need(Enabled(w),"この旧契約は代理受領を含みません");var t=job.Terms;var grant=CommunityStewardship.GrantAt(w,t.Home,at,rev);EntityId receiver=input.Receiver??(Local(w,t.Principal,t.Home,at,rev)?t.Principal:grant?.Agent??throw new InvalidDataException("当主または合意済み代理官の現地受領を待っています"));EntityId? basis=null;
        if(receiver!=t.Principal){Need(grant!=null&&grant.Agent==receiver&&grant.Principal==t.Principal&&grant.Owner==t.Owner&&CommunityStewardship.Authority(w,receiver,t.Owner,t.Home,at,rev)!=null,"この現地代理官に有効な受領権限がありません");basis=grant.Id;}
        Need(Local(w,receiver,t.Home,at,rev),"受領者の現地到着を待っています");return new(input.Id,t.Id,t.Home,t.Owner,receiver,basis,at,rev,job.Cash,job.Cargo);
    }
}
