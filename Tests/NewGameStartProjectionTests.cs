using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class NewGameStartProjectionTests
{
    static ContractWorld Make(int count=1,ulong seed=20261024)
    {
        var records=Enumerable.Range(0,count).Select(i=>NewGameWorldTests.Officer("created:projection-"+i,site:NationalRoutes.Sites[i%NationalRoutes.Sites.Length].Id.Value)).ToArray();
        var setup=NewGameWorldTests.Setup(records);
        return NewGameWorld.Create(setup.World,setup.Registered,"person:nobunaga",seed);
    }
    [Theory][InlineData("start-name")][InlineData("registered-name")][InlineData("grant-action")][InlineData("grant-owner")][InlineData("appearance")][InlineData("office")][InlineData("location")][InlineData("seed")]
    public void PreviouslyValidNestedDataNeverHidesLaterForgery(string kind)
    {
        var w=Make();ContractValidation.Validate(w);var prior=ContractJson.Write(w);
        switch(kind){
            case "start-name":w.NewGame!.Start.People[0]=w.NewGame.Start.People[0] with{Name="改竄"};break;
            case "registered-name":w.NewGame!.Registered[0]=w.NewGame.Registered[0] with{Name="改竄"};break;
            case "grant-action":w.Grants[0].Actions[0]=ActionKind.Transfer;break;
            case "grant-owner":w.Grants[0]=w.Grants[0] with{Principal=new("person:shingen")};break;
            case "appearance":w.Characters[0]=w.Characters[0] with{AppearanceId=new("appearance:wrong")};break;
            case "office":w.Offices[0]=w.Offices[0] with{Target=new("site:odawara")};break;
            case "location":w.Locations[0]=w.Locations[0] with{Site=new("site:odawara")};break;
            case "seed":w.NewGame=w.NewGame! with{Seed=1};break;
        }
        Assert.Throws<InvalidDataException>(()=>ContractValidation.Validate(w));
        var original=ContractJson.Read<ContractWorld>(prior);ContractValidation.Validate(original);Assert.Equal(prior,ContractJson.Write(original));
    }
    [Fact]public void EvictionAndOtherWorldsNeverChangeTheOriginalWorld()
    {
        var w=Make();var bytes=ContractJson.Write(w);for(ulong seed=1;seed<=7;seed++)ContractValidation.Validate(Make(2,seed));
        ContractValidation.Validate(w);Assert.Equal(bytes,ContractJson.Write(w));
    }
    [Fact]public async Task ParallelWorldValidationNeverSharesMutablePublicState()
    {
        var worlds=Enumerable.Range(1,8).Select(i=>Make(2,(ulong)i)).ToArray();var bytes=worlds.Select(ContractJson.Write).ToArray();
        await Task.WhenAll(worlds.Select(w=>Task.Run(()=>{for(int i=0;i<3;i++)ContractValidation.Validate(w);})));
        for(int i=0;i<worlds.Length;i++)Assert.Equal(bytes[i],ContractJson.Write(worlds[i]));
    }
    [Theory][InlineData(10)][InlineData(50)][InlineData(100)]
    public void SyntheticRegisteredPopulationRestoresAndContinuesExactMovement(int count)
    {
        var s=new WorldSession(Make(count));Assert.Equal(ResultCode.Accepted,s.Submit(TravelWorld.Move(s.DiagnosticSnapshot(),"request:projection-go","fork"),s.Revision).Code);NewGameWorldTests.At(s,5);
        var copy=new WorldSession(ContractJson.Read<ContractWorld>(ContractJson.Write(s.DiagnosticSnapshot())));
        foreach(var v in new[]{s,copy})NewGameWorldTests.At(v,10);
        var world=s.DiagnosticSnapshot();Assert.Equal(count+3,world.Characters.Length);Assert.Equal(109*(count+3),world.Grants.Length);Assert.Equal(ContractJson.Write(world),ContractJson.Write(copy.DiagnosticSnapshot()));
    }
}
