namespace HakutaiKagami.Core.Contracts;

// S02（2026-11-17）：危篤・意思表示不能、家督争いの決着、遺産の相続、操作の引き継ぎの兄弟姉妹。
// 新規の全国開始の一覧（NationalSystems）に MortalSuccession のある世界だけが使う。今の保存の世界は変えない。
// 移すのは死んだ人の私財（銭）だけ。家の財・兵・所領・役職・主従は移さない。
public sealed record StrategyDispute(EntityId House,EntityId Previous,EntityId Winner,EntityId[] Claimants,string Basis,long At,long Revision);
public sealed record EstateTransfer(EntityId Deceased,EntityId Heir,long Amount,long At,string Basis);
public sealed record ChronicleEnd(EntityId Person,long DiedAt,string? House,EntityId? HouseHead,int Castles);

public static class MortalSuccession
{
    public const string Name=NationalSystems.MortalSuccession;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name);

    // 危篤：重さ3の傷病の峠（生死が決まる時）の前。
    public static bool Critical(ContractWorld w,EntityId person,long at)=>HealthMortality.Enabled(w)&&PersonalHealth.Active(w,person,at).Any(c=>c.Severity>=3&&at<HealthMortality.CrisisAt(c));
    // 意思表示不能：判断を要する政務ができない状態（重い病・重さ3の傷病・診療の最中）。
    public static bool Incapacitated(ContractWorld w,EntityId person,long at)=>ClinicalHealth.Enabled(w)&&!PersonalHealth.CanJudge(w,person,at);

    // 死亡の原本（自然死・傷病死・処断と、暗殺）。
    public static IEnumerable<(EntityId Person,long At,long Revision)> Deaths(ContractWorld w)=>
        (w.NaturalDeaths??[]).Select(d=>(d.Person,d.At,d.Revision))
        .Concat(InformationSchemes.Events(w).Where(e=>e.Input.Action==SchemeAction.FatalEncounter).Select(e=>(e.Input.Subject,e.At,e.Revision)));

    static int? Born(ContractWorld w,EntityId person)=>FamilyBirth.People(w).FirstOrDefault(p=>p.Id==person.Value) is {} p?p.BirthYear??NationalStart.Life(p.Id)?.Born:null;
    // その時点で成人して生きている（年齢は生年から。今の状態で過去を読み替えない）。
    static bool AdultAlive(ContractWorld w,EntityId person,long at,long rev)=>
        Born(w,person) is int born&&WorldCalendar.At(w.EpochDate,at).Year-born>=FamilyKin.ComingOfAge&&!Assassination.DeadAt(w,person,at,rev);

    // 遺産の相続人：死んだ人が最後に指名した後継、成人の実子の年長、配偶者の順。死亡の時点の事実で決める。
    public static (EntityId Heir,string Basis)? EstateHeir(ContractWorld w,EntityId dead,long at,long rev)
    {
        if(!On(w))return null;
        if((w.HeirNominations??[]).Where(n=>n.Head==dead&&n.Revision<rev).OrderBy(n=>n.Revision).LastOrDefault()?.Heir is {} named&&AdultAlive(w,named,at,rev+1))return (named,"指名の後継");
        var child=FamilyBirth.People(w).Where(p=>(p.BirthParents??[]).Contains(dead.Value)).Select(p=>new EntityId(p.Id))
            .Where(p=>AdultAlive(w,p,at,rev+1)).OrderBy(p=>Born(w,p)??int.MaxValue).ThenBy(p=>p.Value,StringComparer.Ordinal).Cast<EntityId?>().FirstOrDefault();
        if(child is {} c)return (c,"成人の実子");
        var spouse=AdultMarriage.Spouses(w,dead,at,rev).Concat(Diplomacy2.Spouses(w,dead,at)).Distinct().Where(p=>AdultAlive(w,p,at,rev+1)).OrderBy(p=>p.Value,StringComparer.Ordinal).Cast<EntityId?>().FirstOrDefault();
        return spouse is {} s?(s,"配偶者"):null;
    }
    // 私財の動き：死亡の時点の私財（正の額だけ。借りは残す）が相続人の私財へ移る。
    internal static long EstateNet(ContractWorld w,EntityId actor,long at,long rev)
    {
        if(!On(w))return 0;long n=0;
        foreach(var d in Deaths(w))
        {
            if(!(d.At<at||d.At==at&&d.Revision<rev))continue;
            if(d.Person!=actor&&!MayInherit(w,actor,d.Person))continue;
            if(EstateHeir(w,d.Person,d.At,d.Revision) is not {} heir||heir.Heir!=actor&&d.Person!=actor)continue;
            long b=TransportWork.WalletBefore(w,d.Person,d.At,d.Revision);
            if(b>0)n=checked(d.Person==actor?n-b:n+b);
        }
        return n;
    }
    // 相続人になりうる（指名・実子・配偶者）かを先に安く調べる。
    static bool MayInherit(ContractWorld w,EntityId actor,EntityId dead)=>
        (w.HeirNominations??[]).Any(x=>x.Head==dead&&x.Heir==actor)||
        (FamilyBirth.People(w).FirstOrDefault(p=>p.Id==actor.Value)?.BirthParents??[]).Contains(dead.Value)||
        AdultMarriage.Spouses(w,dead,long.MaxValue,long.MaxValue).Contains(actor)||Diplomacy2.Spouses(w,dead,long.MaxValue).Contains(actor);
    // 相続の記録（死亡の原本から導く）。本人は自分が相続人か死者の家族のものだけ見る。
    public static EstateTransfer[] Estates(ContractWorld w,EntityId viewer)
    {
        if(!On(w))return [];
        var rows=new List<EstateTransfer>();
        foreach(var d in Deaths(w).Where(d=>d.At<=w.ElapsedSeconds))
        {
            if(EstateHeir(w,d.Person,d.At,d.Revision) is not {} heir||heir.Heir!=viewer)continue;
            long b=TransportWork.WalletBefore(w,d.Person,d.At,d.Revision);
            if(b>0)rows.Add(new(d.Person,heir.Heir,b,d.At,heir.Basis));
        }
        return rows.OrderBy(r=>r.At).ToArray();
    }

    // 兄弟姉妹：同じ実の親を持つ人と、百代鏡DBの兄弟姉妹の記録（追加の版）。
    public static EntityId[] Siblings(ContractWorld w,EntityId person)
    {
        var people=FamilyBirth.People(w).ToArray();
        var parents=people.SingleOrDefault(p=>p.Id==person.Value)?.BirthParents??[];
        var byParents=parents.Length==0?[]:people.Where(p=>p.Id!=person.Value&&(p.BirthParents??[]).Intersect(parents).Any()).Select(p=>p.Id);
        // 百代鏡DBの記録は DB の識別子なので、世界の人物の識別子へ読み替える（名簿の人物だけ）。
        var db=NationalStart.DbPerson(person.Value) is {} me&&HakutaiDbSupplement.Person(me.Id)?.Siblings is {Length:>0} sib
            ?people.Where(p=>NationalStart.DbPerson(p.Id) is {} x&&sib.Contains(x.Id)).Select(p=>p.Id):[];
        return byParents.Concat(db).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).Select(x=>new EntityId(x)).ToArray();
    }

    // NPCの家の家督（NationalStrategy.Succeed から）。申立が2人以上なら決着する。
    internal static (EntityId Heir,StrategyDispute? Dispute)? Successor(ContractWorld w,EntityId house,EntityId dead,string? faction)
    {
        long at=w.ElapsedSeconds,rev=w.CommitSequence;
        bool ok(EntityId p)=>w.Characters.SingleOrDefault(c=>c.Id==p)?.Life==LifeState.Adult;
        var named=(w.HeirNominations??[]).Where(n=>n.Head==dead&&n.House==house).OrderBy(n=>n.Revision).LastOrDefault()?.Heir;
        var claims=(w.HouseClaims??[]).Where(c=>c.House==house&&c.Deceased==dead&&ok(c.Claimant)).ToArray();
        if(claims.Length>=2)
        {
            var home=Guardianship.Openings(w).FirstOrDefault(o=>o.House==house&&o.Deceased==dead)?.Home;
            bool law=named is {} nm&&claims.Any(c=>c.Claimant==nm)&&home is {} h&&HouseReforms.SuccessionLawAt(w,house,h,at,rev+1);
            EntityId winner;string basis;
            if(law){winner=named!.Value;basis="家法の後継順位";}
            else
            {
                var score=claims.Select(c=>(c.Claimant,Votes:(w.ClaimOpinions??[]).Where(o=>o.ClaimRevision==c.Revision).Sum(o=>o.Supports?1:-1)))
                    .OrderByDescending(x=>x.Votes).ThenBy(x=>x.Claimant==named?0:1).ThenBy(x=>Born(w,x.Claimant)??int.MaxValue).ThenBy(x=>x.Claimant.Value,StringComparer.Ordinal).ToArray();
                winner=score[0].Claimant;
                basis=score.Length>1&&score[0].Votes>score[1].Votes?"家臣の意向":winner==named?"指名の後継":"年長";
            }
            return (winner,new(house,dead,winner,claims.Select(c=>c.Claimant).OrderBy(x=>x.Value,StringComparer.Ordinal).ToArray(),basis,at,rev));
        }
        if(claims.Length==1)return (claims[0].Claimant,null);
        if(named is {} n0&&ok(n0))return (n0,null);
        // 強い家から迎えた養子（一覧の Politics2）。
        if(Politics2.AdoptedHeir(w,house) is {} adopted&&ok(adopted))return (adopted,null);
        var people=FamilyBirth.People(w).ToArray();
        var child=people.Where(p=>(p.BirthParents??[]).Contains(dead.Value)&&ok(new(p.Id))).OrderBy(p=>p.BirthYear??NationalStart.Life(p.Id)?.Born??int.MaxValue).ThenBy(p=>p.Id,StringComparer.Ordinal).FirstOrDefault();
        if(child!=null)return (new(child.Id),null);
        // 成人の実子がいなければ、同じ家の成人の兄弟姉妹（百代鏡DBの記録を含む）。
        var sibling=Siblings(w,dead).Where(s=>ok(s)&&people.SingleOrDefault(p=>p.Id==s.Value)?.Faction==faction).OrderBy(s=>Born(w,s)??int.MaxValue).ThenBy(s=>s.Value,StringComparer.Ordinal).Cast<EntityId?>().FirstOrDefault();
        if(sibling is {} sb)return (sb,null);
        return null;
    }

    // 年代記の終わり：操作を継ぐ人がいないとき、亡くなった本人の没日と家の今の状態。
    public static ChronicleEnd? End(ContractWorld w)
    {
        if(!On(w)||!ControlSuccession.Ended(w))return null;
        var person=w.PlayerControl;var died=Deaths(w).Where(d=>d.Person==person).Select(d=>(long?)d.At).Min()??w.ElapsedSeconds;
        var faction=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==person.Value)?.Faction;
        var head=w.NewGame.Start.Factions.Any(f=>f.Id==faction)?HouseSuccession.Head(w,new(faction!)):(EntityId?)null;
        int castles=faction==null?0:w.Strategy==null?w.NewGame.Start.Castles.Count(c=>c.Faction==faction):NationalStrategy.CastleCount(w,faction);
        return new(person,died,faction,head,castles);
    }
}
