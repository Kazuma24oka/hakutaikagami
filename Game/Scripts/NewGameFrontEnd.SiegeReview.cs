using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Appearance;
public partial class NewGameFrontEnd
{
    internal (NewCampaignStore Store, NewCampaignProfile Profile) CreateSiegeReview(string directory)
    {
        Conditions();
        historyMode=HistoryMode.Fictional;
        npcAttention=NpcAttention.Beginner;
        RegisteredOfficer Lord(string id,string name,string site) => new(id,null,name,1520,new(60,60,70,60),StartTemplate.Daimyo,site,null,name+"の家",AppearanceCatalog.Default(1));
        var attacker=Lord("created:siege-lord","攻め手","site:kyoto");
        var defender=Lord("created:keep-lord","守り手","site:akutagawa");
        chosen=NewGameWorld.Normalize([attacker,defender]);
        var scenario=NewGameWorld.Scenario(HistoricalStartCatalog.Current);
        var placement=new WorldSetupRequest(chosen,[new(attacker.Id,"created:siege-house",[attacker.Site],false),new(defender.Id,"created:keep-house",[defender.Site],false)]);
        confirmed=NewGameSetup.Confirm(scenario,placement,NewGameSetup.Preview(scenario,placement));
        campaigns=new NewCampaignStore(directory);
        return (campaigns,CreateSelectedCampaign(attacker.Id));
    }
}