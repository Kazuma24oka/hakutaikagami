namespace HakutaiKagami.Core.Contracts;
public sealed record KnownMilitaryTarget(EntityId Unit,long ObservedAt,FieldPoint Position,string Evidence);
public static partial class NewGameMilitary
{
    public static KnownMilitaryTarget? SelectKnownTarget(ContractWorld w,EntityId unit)
    {
        var row=KnownMilitary(w).SingleOrDefault(r=>r.Unit==unit);
        return row==null?null:new(row.Unit,row.ObservedAt,row.Position,row.Evidence);
    }
    static void ValidateKnownTarget(ContractWorld w,NewMilitaryInput input)
    {
        if(input.KnownTarget is not {} target)return;
        Need(input.Action==NewMilitaryAction.PersonalMove&&RecognitionEnabled(w),"記録位置への移動は本人の観測情報で指定します");
        Need(SelectKnownTarget(w,target.Unit)==target&&input.A==target.Position,"選択した部隊と確認時点の位置・根拠が一致しません");
        // Existing PersonalMove checks detachment/custody/range and plans the actual route.
        // Never replace the immutable input.A with Position(world, targetUnit, laterTime).
    }
}
