using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

// S05（2026-11-17）：合戦・攻城・捕縛・恩賞の残り。一覧（NationalSystems）に Warfare2 のある新規の全国開始だけ。
// ・天候と地形：遠方の合戦の日の天候（BattlefieldRules.Weather）が雨・雪なら攻め手の兵の働きは8割。城の種類（百代鏡DBの追加の版）で守りは山城1.5倍・平山城1.25倍。
//   合戦の記録（StrategyBattle）には働きを掛けた後の兵を残すので、保存の値から同じに計算し直せる。
// ・開城：援軍がなく攻め手が守りの3倍以上なら、守る側（操作する人物の家でない）は合戦の前に城兵の助命と退去を条件に開城する。
// ・負傷兵：遠方の合戦の損害の半分は負傷兵として60日で戻り、残りの戦死は前のとおり180日かけて補う。
// ・逃亡：NPCに捕らえられた人は、月の初めに3%（捕らえた家の当主がその城にいなければ6%）で逃げ出す。拘束はそこで終わる。
// ・勲功と知行：城を落とした出陣の大将（攻めた家の当主に従う成人の筆頭、いなければ当主）と、加勢した家の当主に勲功。落とした城はその大将の知行。
public sealed record StrategyEscape(EntityId Person,EntityId CaptorOwner,EntityId Site,long At,long Revision);
public sealed record StrategyMerit(EntityId Campaign,EntityId Person,EntityId House,string Kind,long At,long Revision);
public sealed record StrategyFief(EntityId Site,EntityId Holder,EntityId House,EntityId Campaign,long At,long Revision);

public static class Warfare2
{
    public const string Name=NationalSystems.Warfare2;
    public static bool On(ContractWorld w)=>NationalSystems.On(w,Name)&&NationalStrategy.Enabled(w);
    const long Day=86400;public const long WoundedRecovery=60*Day;
    static int Draw(ContractWorld w,string key)=>(int)(BinaryPrimitives.ReadUInt32LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"warfare2-v1/{w.Seed}/{key}")))%1000);

    static readonly Lazy<Dictionary<string,string>> castleTypes=new(()=>HakutaiDbSupplement.Current.CastleTypes.ToDictionary(c=>c.Id,c=>c.Type,StringComparer.Ordinal));
    static readonly Lazy<Dictionary<string,string>> castleOfSite=new(()=>CastleRoutes.Catalog.Nodes.ToDictionary(n=>"site:"+n.Id,n=>n.Castle,StringComparer.Ordinal));
    public static string? CastleType(EntityId site)=>castleOfSite.Value.TryGetValue(site.Value,out var c)?castleTypes.Value.GetValueOrDefault(c):null;
    // 守りの働き（百分率）と攻めの働き（百分率）。
    public static int DefensePercent(EntityId site)=>CastleType(site) switch{"山城"=>150,"平山城"=>125,_=>100};
    public static int AttackPercent(ContractWorld w,EntityId site,long at)=>BattlefieldRules.Weather(w,site,at) is BattleWeather.Rain or BattleWeather.Snow?80:100;
    internal static (long Attack,long Defense) Effective(ContractWorld w,StrategyCampaign c,long a,long d)=>
        (Math.Max(1,a*AttackPercent(w,c.Target,w.ElapsedSeconds)/100),Math.Max(1,d*DefensePercent(c.Target)/100));
    internal static bool Surrenders(ContractWorld w,StrategyCampaign c,long a,long d,int allies,bool playerDefends)=>
        !playerDefends&&allies==0&&a>=3*d;

    // 損害の残り：戦死の分は180日、負傷兵の分は60日で戻る。
    internal static long Left(long lost,long elapsed,long recovery)
    {
        long wounded=lost/2,dead=lost-wounded;
        long d=elapsed>=recovery?0:dead*(recovery-elapsed)/recovery;
        long h=elapsed>=WoundedRecovery?0:wounded*(WoundedRecovery-elapsed)/WoundedRecovery;
        return d+h;
    }

    // 落城の勲功と知行（NationalStrategy.Settle の後、月の初めに）。
    internal static StrategyState Rewards(ContractWorld w,StrategyState s,StrategyOutcome o)
    {
        if(!On(w)||!o.Captured)return s;
        var c=s.Campaigns.Single(x=>x.Id==o.Campaign);
        var head=HouseSuccession.Head(w,c.Attacker);
        var leader=FamilyBirth.People(w).Where(p=>p.Faction==c.Attacker.Value&&p.Lord==head.Value&&w.Characters.SingleOrDefault(x=>x.Id.Value==p.Id)?.Life==LifeState.Adult)
            .Select(p=>new EntityId(p.Id)).Where(p=>!BattleCaptivity.HeldAt(w,p,w.ElapsedSeconds,w.CommitSequence+1)).Cast<EntityId?>().FirstOrDefault()??head;
        var merits=(s.Merits??[]).ToList();var fiefs=(s.Fiefs??[]).ToList();
        merits.Add(new(c.Id,leader,c.Attacker,"落城",o.At,o.Revision));
        foreach(var j in o.Joined??[])merits.Add(new(c.Id,HouseSuccession.Head(w,j),j,"加勢",o.At,o.Revision));
        fiefs.Add(new(c.Target,leader,c.Attacker,c.Id,o.At,o.Revision));
        return s with{Merits=merits.ToArray(),Fiefs=fiefs.ToArray()};
    }
    // 勲功を知る人：本人と、その家の当主（主君）。
    public static StrategyMerit[] KnownMerits(ContractWorld w,EntityId viewer)=>!On(w)?[]:
        (w.Strategy?.Merits??[]).Where(m=>m.Person==viewer||HouseSuccession.Head(w,m.House,m.At,long.MaxValue)==viewer).ToArray();
    // 知行：その城の最後の知行の記録で、家がまだその城を持っていれば。
    public static StrategyFief? FiefOf(ContractWorld w,EntityId site)=>!On(w)?null:
        (w.Strategy?.Fiefs??[]).LastOrDefault(f=>f.Site==site) is {} f&&NationalStrategy.Owner(w,site,w.ElapsedSeconds,w.CommitSequence)==f.House?f:null;

    // 逃亡（月の初め）：今 NPC に捕らえられている人。
    internal static StrategyState Escapes(ContractWorld w,StrategyState s)
    {
        if(!On(w))return s;long now=w.ElapsedSeconds,rev=w.CommitSequence;var rows=(s.Escapes??[]).ToList();
        foreach(var person in BattleCaptivity.AllCaptures(w).Select(c=>c.Person).Distinct().Where(p=>w.Characters.Any(c=>c.Id==p&&c.Life==LifeState.Adult)).OrderBy(x=>x.Value,StringComparer.Ordinal))
        {
            if(BattleCaptivity.CaptivityAt(w,person,now,rev+1) is not {} cap||HouseSuccession.Head(w,cap.CaptorOwner)==w.PlayerControl)continue;
            bool away=SocialWorld.Site(w,cap.Holder)!=cap.Site;
            if(Draw(w,$"escape/{person.Value}/{now}")>=(away?60:30))continue;
            rows.Add(new(person,cap.CaptorOwner,cap.Site,now,rev));
        }
        return rows.Count==(s.Escapes?.Length??0)?s:s with{Escapes=rows.ToArray()};
    }
    internal static bool Escaped(ContractWorld w,EntityId person,long capturedRevision,long at,long rev)=>
        On(w)&&(w.Strategy?.Escapes??[]).Any(e=>e.Person==person&&e.Revision>=capturedRevision&&(e.At<at||e.At==at&&e.Revision<rev));
    // 逃亡を知る人：本人、捕らえた家の当主、本人の家の当主（すぐに知る）。
    internal static IEnumerable<PersonalChronicleRow> ChronicleRows(ContractWorld w,EntityId viewer)
    {
        if(!On(w))yield break;
        foreach(var e in w.Strategy?.Escapes??[])
        {
            var home=w.NewGame!.Start.People.SingleOrDefault(p=>p.Id==e.Person.Value)?.Faction;
            if(e.At>w.ElapsedSeconds||!(viewer==e.Person||HouseSuccession.Head(w,e.CaptorOwner)==viewer||home!=null&&HouseSuccession.Head(w,new(home))==viewer))continue;
            string who=w.Definitions.Characters.SingleOrDefault(c=>c.Id==e.Person)?.Name??e.Person.Value;
            yield return new("escape:"+e.Person.Value+"/"+e.At,"逃亡",$"{who}が捕らわれの身から逃れた",e.At,e.At,"知らせ",e.CaptorOwner.Value);
        }
        foreach(var m in KnownMerits(w,viewer))
        {
            string who=w.Definitions.Characters.SingleOrDefault(c=>c.Id==m.Person)?.Name??m.Person.Value;
            yield return new("merit:"+m.Campaign.Value+"/"+m.Person.Value,"勲功",$"{who}　{m.Kind}の勲功",m.At,m.At,"知らせ",m.House.Value);
        }
    }
}
