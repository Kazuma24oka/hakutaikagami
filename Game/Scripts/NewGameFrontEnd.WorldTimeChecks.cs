using HakutaiKagami.Core.Contracts;
using System.IO;
public partial class NewGameFrontEnd
{
    internal (NewCampaignStore Store,NewCampaignProfile Profile) CreateWorldTimeReview(string root)
    {
        if(TestKey!=null)throw new InvalidDataException("通常開始の確認には試験用の旧方針を使いません");
        confirmed=NewGameWorld.Scenario(HistoricalStartCatalog.Current);chosen=[];campaigns=new NewCampaignStore(root);
        return (campaigns,CreateSelectedCampaign("person:nobunaga"));
    }
}
