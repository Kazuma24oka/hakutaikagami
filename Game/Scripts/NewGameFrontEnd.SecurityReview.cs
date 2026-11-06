using System.Linq;
using HakutaiKagami.Core.Contracts;
public partial class NewGameFrontEnd
{
 internal (NewCampaignStore Store,NewCampaignProfile Profile) CreateSecurityReview(string root){Conditions();historyMode=HistoryMode.Fictional;npcAttention=NpcAttention.Beginner;chosen=NewGameWorld.Normalize(new[]{"leader","one","two","three"}.Select(id=>new RegisteredOfficer("created:security-"+id,null,id=="leader"?"確認用の領主":"警固役 "+id,1520,new(60,60,80,80),id=="leader"?StartTemplate.Daimyo:StartTemplate.Retainer,"site:castle",id=="leader"?null:"created:security-leader",id=="leader"?"警固の家":null,HakutaiKagami.Core.Appearance.AppearanceCatalog.Default(1))).ToArray());var raw=NewGameWorld.Scenario(HistoricalStartCatalog.Current);var setup=new WorldSetupRequest(chosen,[new("created:security-leader","created:security-house",["site:castle"],false)]);confirmed=NewGameSetup.Confirm(raw,setup,NewGameSetup.Preview(raw,setup));campaigns=new NewCampaignStore(root);return(campaigns,CreateSelectedCampaign("created:security-leader"));}
}
