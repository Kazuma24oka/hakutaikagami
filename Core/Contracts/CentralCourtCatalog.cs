namespace HakutaiKagami.Core.Contracts;

// 既存の開始版は保存時の名簿から再構築する。中央の窓口役に城や指揮権を付与しない。
public static class CentralCourtCatalog
{
    public const string Id="historical-start-central-v4";
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
}
