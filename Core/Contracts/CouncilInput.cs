namespace HakutaiKagami.Core.Contracts;

// Only composes a known order. Delivery, rights, obedience, routing and reports belong to WorldSession.
public static class CouncilInput
{
    public static RegionalRequest Line(ContractWorld w,string id,EntityId recipient,FieldPoint a,FieldPoint b)
    {
        if(!MissionEntry.Enabled(w)||!RegionalBattle.Active(w)||!EntityId.IsValid(id)||
            recipient!=w.PlayerControl&&recipient!=ContractFixture.Clerk||a==null||b==null||
            new[]{a.X,a.Y,b.X,b.Y}.Any(v=>v < -4000000000L || v >4000000000L)||RegionalGround.Distance(a,b)<10000)
            throw new InvalidDataException("担当者と10m以上の作戦線を確認してください");
        return new(new(id),w.PlayerControl,recipient,RegionalAction.Move,a,b);
    }
}
