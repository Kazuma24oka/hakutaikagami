using HakutaiKagami.Core.Appearance;
using HakutaiKagami.Core.Contracts;
using Xunit;

namespace HakutaiKagami.Tests;

// 進行の重さの見直し 第1段（2026-11-12、v107）。
// 開始の検査の記憶は全体で4つまで。ほかの検査と同時に動くと記憶が入れ替わるので、この組は単独で動かす。
[CollectionDefinition("開始の検査の記憶",DisableParallelization=true)]public sealed class StartProjectionCollection{}
[Collection("開始の検査の記憶")]
public sealed class AdvanceCostTests
{
    static WorldSession Large(int n)
    {
        var raw=WorldNetwork.WithCastles(NewGameWorld.Scenario());var sites=CastleRoutes.Catalog.Nodes.Select(x=>"site:"+x.Id).ToArray();
        var records=NewGameWorld.Normalize(Enumerable.Range(0,n).Select(i=>new RegisteredOfficer("created:cost-"+i,null,"鏡山 宗久",1520,new(60,50,70,40),StartTemplate.Ronin,sites[i*7%sites.Length],null,null,AppearanceCatalog.Default(1))).ToArray());
        var request=new WorldSetupRequest(records,[]);var setup=NewGameSetup.Confirm(raw,request,NewGameSetup.Preview(raw,request));
        return new WorldSession(NewGameWorld.Create(setup,records,"person:nobunaga",20261112,NewGameWorld.CompactPersonalAuthority,networkPolicy:WorldNetwork.CastlePolicy));
    }

    [Fact]
    public void LargeStartIsVerifiedOnceAcrossAdvances()
    {
        var s=Large(4200);Assert.True(ContractJson.Write(s.DiagnosticSnapshot().NewGame!).Length>4*1024*1024);
        NewGameWorldTests.At(s,60);int before=NewGameWorld.StartVerifications;
        NewGameWorldTests.At(s,3600);NewGameWorldTests.At(s,86400);
        Assert.Equal(before,NewGameWorld.StartVerifications);
    }

    [Fact]
    public void TurnSnapshotIsSharedUntilTheWorldChangesAndRewritesAreCaught()
    {
        var s=Large(3);var a=s.TurnSnapshot();Assert.Same(a,s.TurnSnapshot());
        NewGameWorldTests.At(s,60);var b=s.TurnSnapshot();Assert.NotSame(a,b);Assert.Equal(60,b.ElapsedSeconds);
        b.ElapsedSeconds=1;Assert.Throws<InvalidOperationException>(()=>s.TurnSnapshot());
    }
}

// ID の判定を正規表現から文字ごとの判定にした（2026-11-12、v110）。同じ判定になることを多くの文字列で確かめる。
public sealed class EntityIdRuleTests
{
    [Fact]
    public void CharacterRuleMatchesTheOriginalRegex()
    {
        var rx=new System.Text.RegularExpressions.Regex("^[a-z][a-z0-9-]*:[A-Za-z0-9._/-]+$",System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        bool Old(string? v)=>v is {Length:>=3 and <=128}&&rx.IsMatch(v);
        var fixedCases=new[]{"a:b","ab:","a:\n","abc:d\n","abc:d\n\n","A:b","a-b:c","a_b:c","-a:b","a::b","a:b:c","a:b c","a:\u00e9","site:c-l300191","person:hdb/P-000002","x:"+new string('a',126),"x:"+new string('a',127),"a:b\r\n","1a:b","a:.","\na:b"};
        foreach(var c in fixedCases)Assert.True(Old(c)==EntityId.IsValid(c),c);
        var rnd=new Random(20261112);const string alphabet="abcxyzABZ019:-_./ \n\u00e9";
        for(int n=0;n<200000;n++){int len=rnd.Next(0,12);var chars=new char[len];for(int i=0;i<len;i++)chars[i]=alphabet[rnd.Next(alphabet.Length)];var v=new string(chars);Assert.True(Old(v)==EntityId.IsValid(v),v);}
        Assert.False(EntityId.IsValid(null));
    }
}
