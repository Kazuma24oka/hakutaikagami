using Godot;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
 internal (NewCampaignStore Store,NewCampaignProfile Profile) CreateWeatherReview(string root){if(TestKey!=null)throw new System.InvalidOperationException("通常入口で確認してください");Conditions();historyMode=HistoryMode.Fictional;npcAttention=NpcAttention.Beginner;chosen=NewGameWorld.Normalize([new("created:weather-lord",null,"確認用の地域領主",1520,new(60,50,70,60),StartTemplate.Daimyo,"site:nejo",null,"地域の家",HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1))]);var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);var setup=new WorldSetupRequest(chosen,[new("created:weather-lord","created:weather-house",["site:nejo"],false)]);confirmed=NewGameSetup.Confirm(raw,setup,NewGameSetup.Preview(raw,setup));campaigns=new NewCampaignStore(root);return(campaigns,CreateSelectedCampaign("created:weather-lord"));}
}
