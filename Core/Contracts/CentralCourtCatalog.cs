namespace HakutaiKagami.Core.Contracts;

// 既存の開始版は保存時の名簿から再構築する。中央の窓口役に城や指揮権を付与しない。
public static class CentralCourtCatalog
{
    public const string Id="historical-start-central-v4";
    public const string ShogunateEdition="historical-start-central-v5";
    public const string TempleEdition="historical-start-central-v6";
    public const string RegentEdition="historical-start-central-v7";
    public static readonly EntityId Konoe=new("person:konoe-sakihisa");
    public static bool HasShogunate(string? edition)=>edition is ShogunateEdition or TempleEdition or RegentEdition;
    internal static SetupScenario RegentScenario(){
        var prior=TempleScenario();
        var next=prior with{People=[..prior.People,new SetupPerson(Konoe.Value,"近衛前久",Kyoto.Value,null,null,false,false,1536)]};
        NewGameSetup.Validate(next);return next;
    }
    public static bool HasCourt(string? edition)=>edition==Id || HasShogunate(edition);
    public static readonly EntityId TempleBroker=new("person:temple-representative/site/sakai");
    internal static SetupScenario TempleScenario(){
        var prior=ShogunateScenario();
        var monk=new SetupPerson(TempleBroker.Value,"堺の寺社代表（架空）",TempleMilitary.Site.Value,null,null,false,false);
        var next=prior with{People=[..prior.People,monk]};NewGameSetup.Validate(next);return next;
    }
    public static readonly EntityId Shogun=new("person:ashikaga-yoshiteru");
    public static readonly EntityId ShogunBroker=new("person:hosokawa-fujitaka");
    public static readonly EntityId CourtBroker=new("person:yamashina-tokitsugu");
    public static readonly EntityId Kyoto=RegionalCampaign.Site("kyoto");

    internal static SetupScenario Scenario()
    {
        var prior=HistoricalHouseholdCatalog.Scenario();
        var broker=new SetupPerson(CourtBroker.Value,"山科言継",Kyoto.Value,null,null,false,false,1507);
        var next=prior with{People=[..prior.People,broker]};
        NewGameSetup.Validate(next);
        return next;
    }
    internal static SetupScenario ShogunateScenario()
    {
        var prior=Scenario();
        var shogun=new SetupPerson(Shogun.Value,"足利義輝",Kyoto.Value,null,null,false,false);
        var attendant=new SetupPerson(ShogunBroker.Value,"細川藤孝",Kyoto.Value,null,Shogun.Value,false,true,1534);
        var next=prior with{People=[..prior.People,shogun,attendant]};
        NewGameSetup.Validate(next);
        return next;
    }
}
