using System.Security.Cryptography;
using System.Text;
using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

[Trait("Tier","Standard")]
public sealed class NationalPersonEstimateTests
{
    [Fact]
    public void EvidenceTableMatchesDbAndKeepsExplicitBirthSeparateFromEstimates()
    {
        var catalog=NationalPersonEstimates.Catalog;
        Assert.Equal(HakutaiDb.Edition1560,catalog.SourceEdition);
        Assert.Equal(HakutaiDb.Source.Commit,catalog.SourceCommit);
        using var stream=typeof(HakutaiDb).Assembly.GetManifestResourceStream("HakutaiDb1560V2")!;
        Assert.Equal(Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(),catalog.SourceSha256);
        var db=HakutaiDb.Houses.SelectMany(h=>HakutaiDb.Members(h.Id)).ToDictionary(p=>p.Id,StringComparer.Ordinal);
        Assert.Equal(db.Count,catalog.People.Length);
        Assert.Contains(catalog.People,p=>p.BirthKind.StartsWith("estimated-",StringComparison.Ordinal));
        foreach(var p in catalog.People)
        {
            var source=db[p.Id];
            if(source.Birth is int birth){Assert.Equal(birth,p.BirthYear);Assert.Equal("explicit",p.BirthKind);}
            else{Assert.StartsWith("estimated-",p.BirthKind,StringComparison.Ordinal);Assert.Contains("DBに",p.BirthBasis,StringComparison.Ordinal);}
            Assert.InRange(p.Abilities.Leadership,1,100);Assert.InRange(p.Abilities.Valor,1,100);
            Assert.InRange(p.Abilities.Intelligence,1,100);Assert.InRange(p.Abilities.Politics,1,100);Assert.InRange(p.Abilities.Charm,1,100);
            Assert.Equal(50,p.Abilities.Charm);
        }
    }

    [Fact]
    public void NationalStartUsesEstimatesAndHidesOtherPeoplesValuesAfterFileRestore()
    {
        var scenario=NationalStart.Scenario();
        var selected=scenario.Factions.Select(f=>f.Head).Single(h=>scenario.People.Single(p=>p.Id==h).Name.Contains("信長",StringComparison.Ordinal));
        var world=NationalStart.Create(selected,20261113);
        Assert.Equal(NationalPersonEstimates.Policy,world.NewGame!.NationalPersonDataPolicy);
        Assert.Equal(CharacterDevelopment.Policy,world.NewGame.DevelopmentPolicy);
        var historical=world.NewGame.Start.People.Where(p=>NationalStart.DbPerson(p.Id)!=null).ToArray();
        Assert.Equal(scenario.People.Count(p=>NationalStart.DbPerson(p.Id)!=null),historical.Length);
        Assert.True(historical.Length>0);
        foreach(var person in historical)
        {
            var expected=NationalPersonEstimates.ForDbPerson(NationalStart.DbPerson(person.Id)!.Id);
            Assert.Equal(expected.BirthYear,person.BirthYear);
            Assert.Equal(expected.Abilities.Charm,person.Attributes!.Charm);
        }

        var own=CharacterDevelopment.View(world,world.PlayerControl,world.PlayerControl)!;
        Assert.Contains(own.Abilities,a=>a.Name=="魅力"&&a.Value==world.NewGame.Start.People.Single(p=>p.Id==world.PlayerControl.Value).Attributes!.Charm);
        var other=world.NewGame.Start.People.First(p=>p.Id!=world.PlayerControl.Value&&NationalStart.DbPerson(p.Id)!=null);
        var privateView=CharacterDevelopment.View(world,world.PlayerControl,new(other.Id))!;
        Assert.All(privateView.Abilities,a=>Assert.Null(a.Value));

        var before=new WorldSession(world);var snapshot=before.DiagnosticSnapshot();
        var store=new GenerationSaveStore(Path.Combine(Path.GetTempPath(),"national-person-estimate-"+Guid.NewGuid().ToString("N")),snapshot.Definitions);
        Assert.True(store.Save(before,"start",snapshot.WorldId).Published);
        var host=new WorldSessionHost(new WorldSession(TravelWorld.Create()));
        Assert.Equal(ResultCode.Accepted,host.CommitLoad(store.Prepare("start"),host.Current,host.Current.Revision));
        var restored=host.Current;
        Assert.Equal(ContractJson.Write(snapshot),ContractJson.Write(restored.DiagnosticSnapshot()));
        ContractValidation.Validate(restored.DiagnosticSnapshot());

        var campaignStore=new NewCampaignStore(Path.Combine(Path.GetTempPath(),"national-person-profile-"+Guid.NewGuid().ToString("N")));
        var profile=NationalStart.New(campaignStore,scenario,selected,20261113,null,false);
        Assert.Equal(NationalPersonEstimates.Policy,profile.NationalPersonDataPolicy);
        var fromProfile=NewCampaignStore.Build(campaignStore.Read(profile.Instance));
        Assert.Equal(world.NewGame!.Start.People.Select(p=>(p.Id,p.BirthYear,p.Attributes?.Charm)),fromProfile.NewGame!.Start.People.Select(p=>(p.Id,p.BirthYear,p.Attributes?.Charm)));
    }

    [Theory]
    [InlineData(NationalStart.Edition)]
    [InlineData(NationalStart.CourtEdition)]
    public void PriorNationalEditionDoesNotReceivePersonDataPolicyOrEstimates(string edition)
    {
        var scenario=NationalStart.Scenario(edition);
        var selected=scenario.Factions.Select(f=>f.Head).First();
        var world=NewGameWorld.Create(scenario,[],selected,20261113,NewGameWorld.CompactPersonalAuthority,
            historicalCatalog:edition,networkPolicy:WorldNetwork.CastlePolicy);
        var bytes=ContractJson.Write(world);
        var restored=ContractJson.Read<ContractWorld>(bytes);
        ContractValidation.Validate(restored);
        Assert.Equal(bytes,ContractJson.Write(restored));
        Assert.Null(world.NewGame!.NationalPersonDataPolicy);
        Assert.All(world.NewGame.Start.People.Where(p=>NationalStart.DbPerson(p.Id)!=null),p=>
        {
            Assert.Null(p.BirthYear);
            Assert.Null(p.Attributes);
        });
    }

    [Fact]
    public void PersonEnabledCourtEditionRetainsItsSavedStartDefinition()
    {
        var scenario=NationalStart.Scenario();
        var selected=scenario.Factions.First().Head;
        var world=NewGameWorld.Create(scenario,[],selected,20261113,NewGameWorld.CompactPersonalAuthority,
            historicalCatalog:NationalStart.CourtEdition,networkPolicy:WorldNetwork.CastlePolicy,
            districtControlPolicy:NationalDistrictControl.Policy,developmentPolicy:CharacterDevelopment.Policy,
            nationalPersonDataPolicy:NationalPersonEstimates.Policy);
        var bytes=ContractJson.Write(world);
        var restored=ContractJson.Read<ContractWorld>(bytes);
        ContractValidation.Validate(restored);
        Assert.Equal(bytes,ContractJson.Write(restored));
        Assert.Equal(NationalStart.CourtEdition,restored.NewGame!.HistoricalCatalog);
    }

    [Fact]
    public void OldAttributesWithoutCharmRoundTripByteForByte()
    {
        const string legacy="{\"Leadership\":51,\"Valor\":52,\"Intelligence\":53,\"Politics\":54,\"Negotiation\":55}";
        var bytes=Encoding.UTF8.GetBytes(legacy);
        var parsed=ContractJson.Read<OfficerAttributes>(bytes);
        Assert.Null(parsed.Charm);
        Assert.Equal(bytes,ContractJson.Write(parsed));
    }
}
