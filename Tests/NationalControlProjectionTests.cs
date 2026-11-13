using HakutaiKagami.Core.Contracts;
using HakutaiKagami.Core.Messaging;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NationalControlProjectionTests
{
    [Fact]
    public void DeliveredButSealedCaptureChangesColorOnlyAfterOpeningAndRestoresIdentically()
    {
        var start=NationalStart.Scenario(NationalStart.PersonEdition);
        var selected=start.Factions.Select(f=>f.Head).Single(h=>start.People.Single(p=>p.Id==h).Name.Contains("信長"));
        var world=NewGameWorld.Create(start,[],selected,117,NewGameWorld.CompactPersonalAuthority,
            historicalCatalog:NationalStart.PersonEdition,networkPolicy:WorldNetwork.CastlePolicy,
            districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy,
            nationalPersonDataPolicy:NationalPersonEstimates.Policy,strategyPolicy:NationalStrategy.Policy);
        // 戦略判断そのものは別工程。表示の受入では有効な落城結果と同所の封書を準備する。
        var defender=start.Factions.First(f=>f.Head!=selected&&start.Castles.Any(c=>c.Faction==f.Id&&c.Id==start.People.Single(p=>p.Id==f.Head).Site)&&
            start.People.Count(p=>p.Site==start.People.Single(h=>h.Id==f.Head).Site&&world.Characters.Any(c=>c.Id.Value==p.Id&&c.Life==LifeState.Adult))>=2);
        string site=start.People.Single(p=>p.Id==defender.Head).Site;
        var sender=start.People.First(p=>p.Site==site&&p.Id!=defender.Head&&world.Characters.Any(c=>c.Id.Value==p.Id&&c.Life==LifeState.Adult));
        var attacker=start.Factions.First(f=>f.Id!=defender.Id);
        long feb=(new DateOnly(1560,2,1).DayNumber-new DateOnly(1560,1,1).DayNumber)*86400L;
        long march=(new DateOnly(1560,3,1).DayNumber-new DateOnly(1560,1,1).DayNumber)*86400L;
        var campaign=new StrategyCampaign(new("campaign:color/one"),new(attacker.Id),new(defender.Id),new(site),feb,1,NationalStrategy.Border);
        world.ElapsedSeconds=march;world.CommitSequence=2;
        world.Strategy=new(1,NationalStrategy.Policy,[campaign],[new(campaign.Id,march,2,true)]);
        var letter=new MessageEnvelope("message:color/one",campaign.Id.Value,NationalStrategy.LetterKind,sender.Id,defender.Head,"落城",site,march);
        MessageFlow.Send(world.Messaging,letter,march,1);world.SealedMessages=[..world.SealedMessages,new(letter.Id)];
        RegionalCampaign.Bind(world);
        world.ElapsedSeconds=world.Messaging.Attempts.Single(a=>a.MessageId==letter.Id).DueAt;MessageFlow.Advance(world.Messaging,world.ElapsedSeconds);
        world.Messaging.Knowledge=world.Messaging.Knowledge.Where(k=>k.MessageId!=letter.Id).ToArray();
        var session=new WorldSession(world);session.SetPaused(true);
        var before=session.DiagnosticSnapshot();
        Assert.Contains(before.Messaging.Attempts,a=>a.MessageId==letter.Id&&a.Status==AttemptStatus.Delivered);
        Assert.False(MessageFlow.Knows(before.Messaging,letter.Id,letter.RecipientId));
        var observer=new EntityId(letter.RecipientId);
        var initial=ContractJson.Write(before.DistrictControl);
        var sealedProjection=ContractJson.Write(NationalControlProjection.For(before,observer));
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"national-color-"+Guid.NewGuid().ToString("N")),before.Definitions);
        Assert.True(store.Save(session,"sealed",before.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("sealed"),host.Current,host.Current.Revision));
        Assert.Equal(sealedProjection,ContractJson.Write(NationalControlProjection.For(host.Current.DiagnosticSnapshot(),observer)));
        foreach(var s in new[]{session,host.Current})Assert.Equal(ResultCode.Accepted,s.Open(observer,new(letter.Id),s.Revision).Code);
        var after=session.DiagnosticSnapshot();
        Assert.NotEqual(sealedProjection,ContractJson.Write(NationalControlProjection.For(after,observer)));
        Assert.Equal(initial,ContractJson.Write(after.DistrictControl));
        Assert.Equal(ContractJson.Write(after),ContractJson.Write(host.Current.DiagnosticSnapshot()));
        Assert.Equal(ContractJson.Write(NationalControlProjection.For(after,observer)),ContractJson.Write(NationalControlProjection.For(host.Current.DiagnosticSnapshot(),observer)));
        // 表示専用の入力：同じ刻・版の後発落城が未認識でも、既知の報告を上書きしない。
        var hidden=ContractJson.Copy(after);
        var third=start.Factions.First(f=>f.Id!=attacker.Id&&f.Id!=defender.Id);
        var hiddenCampaign=new StrategyCampaign(new("campaign:color/hidden"),new(third.Id),campaign.Defender,campaign.Target,feb,1,NationalStrategy.Border);
        hidden.Strategy=hidden.Strategy! with{Campaigns=[..hidden.Strategy.Campaigns,hiddenCampaign],Outcomes=[..hidden.Strategy.Outcomes,new(hiddenCampaign.Id,march,2,true)]};
        Assert.Equal(new EntityId(third.Id),NationalStrategy.Owner(hidden,campaign.Target,march,2));
        Assert.Equal(attacker.Id,NationalControlProjection.CastleOwnersFor(hidden,observer)[site]);
        Assert.Equal(ContractJson.Write(NationalControlProjection.For(after,observer)),ContractJson.Write(NationalControlProjection.For(hidden,observer)));
        var output=Environment.GetEnvironmentVariable("HAKUTAI_COLOR_QA_DIR");
        if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"sealed.json"),ContractJson.Write(before));File.WriteAllBytes(Path.Combine(output,"opened.json"),ContractJson.Write(after));File.WriteAllText(Path.Combine(output,"observer.txt"),observer.Value);}
    }
    [Fact]
    public void PublicMapProvinceAndSplitDistrictNamesResolveToHistoricalDistrict()
    {
        var district=NationalDistrictControl.Resolve("map:198","羽前","東置賜郡");
        Assert.NotNull(district);Assert.Equal("出羽国::置賜郡",district.SourceDistrictId);Assert.True(district.NameInferred);
    }
    [Fact]
    public void NonNationalWorldDoesNotAcquireControlData()=>Assert.Null(NationalControlProjection.For(TravelWorld.Create(),new("person:unknown")));
}
