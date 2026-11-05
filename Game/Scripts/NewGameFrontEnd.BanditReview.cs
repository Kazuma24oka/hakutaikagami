using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
 internal (NewCampaignStore Store,NewCampaignProfile Profile) CreateBanditReview(string root){Conditions();historyMode=HistoryMode.Fictional;npcAttention=NpcAttention.Beginner;chosen=NewGameWorld.Normalize([new("created:bandit-review",null,"確認用の家臣",1520,new(60,60,80,80){Negotiation=80},StartTemplate.Retainer,"site:castle","person:nobunaga",null,HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1))]);var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);var setup=new WorldSetupRequest(chosen,[]);confirmed=NewGameSetup.Confirm(raw,setup,NewGameSetup.Preview(raw,setup));campaigns=new NewCampaignStore(root);return(campaigns,CreateSelectedCampaign("created:bandit-review"));}
}
