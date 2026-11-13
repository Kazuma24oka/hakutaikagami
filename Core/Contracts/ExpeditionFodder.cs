namespace HakutaiKagami.Core.Contracts;

// 保存された出陣・在外行軍と装備・積載から計算。別の時計や二つ目の在庫は持たない。
public static class ExpeditionFodder
{
    // 試作係数：1頭・4時間で飼料1単位。史料から採録した定量ではない。
    public const long HorseSecondsPerUnit=14400;
    public static bool Enabled(ContractWorld w)=>NationalSystems.On(w,"ExpeditionFodder")||w.NewGame?.BattlefieldPolicy==BattlefieldRules.ExpeditionPolicy;
    sealed record Event(long At,long Revision,int Order,int Horses,long Stock);
    public static long Spent(ContractWorld w,EntityId unit,long at=long.MaxValue,long revision=long.MaxValue)
    {
        if(!Enabled(w))return 0;
        long until=Math.Min(at,w.ElapsedSeconds);
        bool Before(long time,long rev)=>time<at||time==at&&rev<revision;
        var orders=BattlefieldRules.Orders(w).Where(o=>o.Input.Unit==unit&&Before(o.At,o.Revision)).ToArray();
        if(!orders.Any(o=>o.Input.Action==BattlefieldAction.Equip&&o.Input.Kind==TroopKind.Cavalry))return 0;
        var ledger=NewGameMilitary.LedgerStates(w).FirstOrDefault(l=>NewGameMilitary.LedgerIdentity(l.Terms.Owner,l.Terms.Site).Units.Contains(unit)||l.Terms.Army==w.Units.SingleOrDefault(u=>u.Id==unit)?.Army);
        if(ledger==null)return 0;
        var windows=ledger.Sorties.Where(s=>s.From<until).Select(s=>(From:s.From,Until:Math.Min(s.Until??until,until)))
            .Concat(ArmyRoadMarch.AwayWindows(w,ledger.Terms.Army).Select(s=>(s.From,Until:Math.Min(s.Until,until))))
            .Where(s=>s.From<s.Until).ToArray();
        if(windows.Length==0)return 0;
        var events=new List<Event>();
        foreach(var o in orders)
        {
            var i=o.Input;
            if(i.Action is BattlefieldAction.Equip or BattlefieldAction.ReturnGear)
                events.Add(new(o.At,o.Revision,0,i.Action==BattlefieldAction.Equip&&i.Kind==TroopKind.Cavalry?i.Amount:0,0));
            if(i.Good=="fodder"&&i.Action is BattlefieldAction.Load or BattlefieldAction.Unload)
                events.Add(new(o.At,o.Revision,1,-1,i.Action==BattlefieldAction.Load?i.Amount:-i.Amount));
        }
        foreach(var step in w.WorldClashes?.Steps??[])
            if(Before(step.At,step.Revision))foreach(var e in step.Result.Expenses??[])
                if(e.Unit==unit&&e.Good=="fodder")events.Add(new(step.At,step.Revision,2,-1,-e.Amount));
        var cuts=events.Select(e=>e.At).Concat(windows.SelectMany(s=>new[]{s.From,s.Until})).Append(0).Append(until).Where(t=>t<=until).Distinct().Order().ToArray();
        long stock=0,spent=0,fraction=0,previous=0;int horses=0;
        foreach(long time in cuts)
        {
            if(time>previous&&horses>0&&windows.Any(s=>s.From<=previous&&time<=s.Until))
            {
                long demand=checked(fraction+checked((time-previous)*horses));long needed=demand/HorseSecondsPerUnit;
                long used=Math.Min(Math.Max(0,stock),needed);spent=checked(spent+used);stock-=used;
                // 欠食の分を借金にして、後の積載から遡って食べない。
                fraction=stock>0?demand%HorseSecondsPerUnit:0;
            }
            foreach(var e in events.Where(e=>e.At==time).OrderBy(e=>e.Revision).ThenBy(e=>e.Order))
            {
                if(e.Horses>=0)horses=e.Horses;
                stock=checked(stock+e.Stock);if(stock<=0)fraction=0;
            }
            previous=time;
        }
        return spent;
    }
}
