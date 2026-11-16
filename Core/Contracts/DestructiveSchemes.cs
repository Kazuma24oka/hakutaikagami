using System.Security.Cryptography;
using System.Text;

namespace HakutaiKagami.Core.Contracts;

public static class DestructiveSchemes
{
    public static bool Operation(SchemeTopic topic) => topic is SchemeTopic.Arson or SchemeTopic.Sabotage or SchemeTopic.SeizeRice or SchemeTopic.Agitation;
    public static string Name(SchemeTopic topic) => topic switch
    {
        SchemeTopic.Arson => "町への放火",
        SchemeTopic.Sabotage => "城門の破壊",
        SchemeTopic.SeizeRice => "蔵米の強奪",
        SchemeTopic.Agitation => "村の煽動",
        _ => throw new ArgumentOutOfRangeException(nameof(topic))
    };

    // 入力IDや再読込でくじを引き直せないよう、人物・場所・日・計略を固定する。
    static int Roll(ContractWorld world, SchemeEvent attempt, string purpose)
    {
        string key=FormattableString.Invariant($"{world.NewGame!.Seed}/{attempt.Actor.Value}/{attempt.Site.Value}/{attempt.At/86400}/{attempt.Input.Topic}/{purpose}");
        return SHA256.HashData(Encoding.UTF8.GetBytes(key))[0]*100/256;
    }

    internal static bool Accepts(ContractWorld world,SchemeInput mission,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        var agent=world.NewGame!.Start.People.Single(p=>p.Id==mission.Person.Value);
        var target=mission.Destination!.Value;
        bool ownHouse=agent.Faction!=null && world.NewGame.Start.Castles.Any(c=>c.Id==target.Value) && RegionalDomains.CurrentOwner(world,target,at,revision)?.Value==agent.Faction;
        var decision=new SchemeEvent(mission,mission.Person,target,at,revision,null,[]);
        int willingness=50+(agent.Attributes?.Intelligence??50)/3-(ownHouse?25:0);
        return world.Characters.Single(c=>c.Id==mission.Person).WillObey && Roll(world,decision,"consent")<willingness;
    }
    static bool Succeeded(ContractWorld world, SchemeEvent attempt)
    {
        var officer=world.NewGame!.Start.People.Single(p=>p.Id==attempt.Actor.Value);
        int guards=LocalSecurity.GuardsAt(world,attempt.Site,attempt.At,attempt.Revision);
        return Roll(world,attempt,"result") < Math.Clamp(40+(officer.Attributes?.Intelligence ?? 50)/3-guards*10,10,90);
    }
    public static string Outcome(ContractWorld world, SchemeEvent attempt) =>
        Name(attempt.Input.Topic)+(Succeeded(world,attempt)?"を実行した":"に失敗した");

    static IEnumerable<SchemeEvent> Successful(ContractWorld world, SchemeTopic topic,long at,long revision) =>
        InformationSchemes.Events(world).Where(e=>e.Input.Action==SchemeAction.ExecuteOperation && e.Input.Topic==topic &&
            (e.At<at || e.At==at && e.Revision<revision) && Succeeded(world,e));

    internal static SchemeClaim? Witness(ContractWorld world,SchemeEvent attempt,EntityId observer)
    {
        if (observer==attempt.Actor || !TransportWork.At(world,observer,attempt.Site,attempt.At,attempt.Revision,true)) return null;
        int guards=LocalSecurity.GuardsAt(world,attempt.Site,attempt.At,attempt.Revision);
        if (Roll(world,attempt,"witness/"+observer.Value)>=Math.Min(80,20+10*guards)) return null;
        return new(attempt.Input.Id,observer,attempt.Actor,observer,Name(attempt.Input.Topic)+"を試みたところを目撃した",attempt.At,attempt.Site,"現場での本人の目撃");
    }

    public static string Prospect(ContractWorld world,EntityId actor,EntityId site)
    {
        int skill=world.NewGame!.Start.People.Single(p=>p.Id==actor.Value).Attributes?.Intelligence??50;
        var observations = from mission in InformationSchemes.Events(world)
                           where mission.Input.Action==SchemeAction.SendScout && !Operation(mission.Input.Topic) &&
                               mission.Actor==actor && mission.Input.Destination==site &&
                               HakutaiKagami.Core.Messaging.MessageFlow.Knows(world.Messaging,InformationSchemes.ReportId(mission.Input.Id),actor.Value)
                           from capture in InformationSchemes.Events(world)
                           where capture.Input.Reference==mission.Input.Id && capture.Input.Action==SchemeAction.CaptureScout && capture.Site==site
                           orderby capture.At descending
                           select LocalSecurity.GuardsAt(world,site,capture.At,capture.Revision);
        int knownGuards=observations.FirstOrDefault();
        return skill/3-knownGuards*10>=20?"勝算はある":"危うい";
    }
    internal static IEnumerable<(long At,int Order,int Amount)> Damage(ContractWorld world,EntityId site,SchemeTopic topic,long at,long revision) =>
        Successful(world,topic,at,revision).Where(e=>e.Site==site).Select(e=>(e.At,2,20));

    internal static IEnumerable<(long At,long Revision,long Amount)> RiceDebits(ContractWorld world,EntityId site,long at,long revision) =>
        Successful(world,SchemeTopic.SeizeRice,at,revision).Where(e=>e.Site==site).Select(e=>(e.At,e.Revision,-5L));

    internal static long PrivateRice(ContractWorld world,EntityId person,long at,long revision) =>
        5L*Successful(world,SchemeTopic.SeizeRice,at,revision).Count(e=>e.Actor==person);

    internal static bool Agitated(ContractWorld world,EntityId site,long at,long revision) =>
        Successful(world,SchemeTopic.Agitation,at,revision).Any(e=>e.Site==site && at-e.At<3*86400);

    internal static EntityId Observe(ContractWorld world,SchemeEvent mission,EntityId site,bool arrived)
    {
        var agent=mission.Input.Person;
        var source=InformationSchemes.Id("operation-observation",mission.Input.Id.Value);
        var claim="実行場所に到着できず、計略を断念した";
        if (arrived)
        {
            var input=new SchemeInput(InformationSchemes.Id("operation",mission.Input.Id.Value),SchemeAction.ExecuteOperation,agent,site,mission.Input.Topic,mission.Input.Id);
            try
            {
                Check(world,input,agent,site,world.ElapsedSeconds,world.CommitSequence);
                InformationSchemes.Add(world,input,agent,site);
                claim=Outcome(world,world.Schemes!.Events[^1]);
            }
            catch(InvalidDataException)
            {
                claim="現地の条件が整わず、計略を断念した";
            }
        }
        world.Evidence=[..world.Evidence,new(source,agent,site,claim,world.ElapsedSeconds,world.ElapsedSeconds,null,null)];
        world.Knowledge=[..world.Knowledge,new(agent,source,world.ElapsedSeconds)];
        return source;
    }
    internal static void ValidateObservation(ContractWorld world, SchemeEvent mission, SchemeEvent capture)
    {
        var source=InformationSchemes.Id("operation-observation",mission.Input.Id.Value);
        var attempt=InformationSchemes.Events(world).SingleOrDefault(e=>e.Input.Reference==mission.Input.Id && e.Input.Action==SchemeAction.ExecuteOperation);
        var entry=world.Evidence.SingleOrDefault(e=>e.Id==source);
        if (!capture.Sources.SequenceEqual(new[]{source}) || entry==null || entry.Observer!=mission.Input.Person ||
            entry.Subject!=capture.Site || entry.Message!=null || entry.ObservedAt!=capture.At || entry.RecordedAt!=capture.At)
            throw new InvalidDataException("現地の実行者と返報の観測原本が一致しません");
        if (attempt==null)
        {
            if (entry.Claim!="実行場所に到着できず、計略を断念した" && entry.Claim!="現地の条件が整わず、計略を断念した")
                throw new InvalidDataException("実行記録のない計略の結果を報告できません");
            return;
        }
        if (attempt.Site!=mission.Input.Destination || attempt.Actor!=mission.Input.Person || attempt.At!=capture.At ||
            attempt.Input.Topic!=mission.Input.Topic || entry.Claim!=Outcome(world,attempt))
            throw new InvalidDataException("計略の返報が現地の結果と一致しません");
    }
    internal static void Check(ContractWorld world, SchemeInput input,EntityId actor,EntityId site,long at,long revision)
    {using var _ccRev=ControlContext.At(revision);
        if (!CastleSiege.Enabled(world) || !Operation(input.Topic) || input.Person!=actor || input.Subject!=site ||
            !MaterialMarkets.Sites(world).Contains(site) || !TransportWork.At(world,actor,site,at,revision,true))
            throw new InvalidDataException("実行者が対象の拠点に到着する必要があります");
        if (InformationSchemes.Events(world).Any(e=>e.Input.Action==SchemeAction.ExecuteOperation && e.Input.Topic==input.Topic &&
                e.Actor==actor && e.Site==site && e.At/86400==at/86400 && e.Revision<revision))
            throw new InvalidDataException("今日はこの場所で同じ計略を試みています");
        if (input.Topic==SchemeTopic.SeizeRice && CommunityEconomy.AccountAt(world,site,at,revision).HouseRice-HouseSalary.Reserved(world,site,at,revision)<5)
            throw new InvalidDataException("引当済みの米を除くと、持ち出せる蔵米が足りません");
    }
}