namespace HakutaiKagami.Core.Contracts;
public static partial class NewGameMilitary
{
    // 既存の軍の発令・認識済み命令・移動から読む。別の部隊や経路を作らない。
    internal static FieldUnitView SampleField(ContractWorld w,int i,long at)
    {
        if(w.NewMilitary==null)throw new InvalidDataException("引受中の軍がありません");
        var position=Position(w,i,at);var next=Position(w,i,checked(at+1));long distance=0;
        if(EffectiveOrder(w,i,at) is {} order)
            foreach(var leg in order.Routes[i])
            {
                long length=RegionalGround.Distance(leg.From,leg.To);
                distance=checked(distance+(at>=leg.EndAt?length:at<=leg.StartAt?0:length*(at-leg.StartAt)/(leg.EndAt-leg.StartAt)));
            }
        return new(UnitIdsFor(w)[i],position,next,position!=next,distance,WorldGround.KindAt(w,position));
    }
}
