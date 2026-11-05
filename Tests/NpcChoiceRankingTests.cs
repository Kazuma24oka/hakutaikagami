using System.Globalization;
using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;

public sealed class NpcChoiceRankingTests
{
    static readonly NpcPersonality Neutral=new(50,50,50,50,50,50), Zero=new(0,0,0,0,0,0);
    static NpcChoiceCandidate C(string id,int utility=0,bool known=true,bool allowed=true,bool historic=false,bool player=false,bool aspiration=false)=>new(id,known,allowed,utility,Zero,aspiration,historic,player);
    static NpcChoiceInput Input(params NpcChoiceCandidate[] candidates)=>new(new("person:test"),42,0,Neutral,NpcAspiration.Unspecified,HistoryMode.SemiHistorical,NpcAttention.Equal,candidates);

    [Fact] public void UnknownAndForbiddenOptionsNeverWinEvenWithAllBonuses()
    {
        var input=Input(C("known",-100),C("secret",1000,known:false,historic:true,player:true,aspiration:true),C("forbidden",1000,allowed:false));
        Assert.Equal("known",NpcChoiceRanking.Choose(input));
        Assert.Null(NpcChoiceRanking.Choose(Input(C("secret",known:false))));
    }
    [Theory]
    [InlineData(HistoryMode.Historical,"history")]
    [InlineData(HistoryMode.SemiHistorical,"useful")]
    [InlineData(HistoryMode.Fictional,"useful")]
    public void HistoricalPreferenceIsAWeightNotAnOrder(HistoryMode mode,string expected)
    {
        Assert.Equal(expected,NpcChoiceRanking.Choose(Input(C("history",historic:true),C("useful",20)) with{History=mode}));
    }
    [Fact] public void FictionalModeRetainsAspirationAndPersonality()
    {
        var input=Input(C("plain",10),C("aspiration",aspiration:true)) with{History=HistoryMode.Fictional,Aspiration=NpcAspiration.PreserveHouse};
        Assert.Equal("aspiration",NpcChoiceRanking.Choose(input));
        var trait=C("duty") with{Weights=new(0,10,0,0,0,0)};
        Assert.Equal("duty",NpcChoiceRanking.Choose(Input(C("plain",9),trait) with{History=HistoryMode.Fictional,Personality=new(50,100,50,50,50,50)}));
    }
    [Theory]
    [InlineData(NpcAttention.Beginner,"player")]
    [InlineData(NpcAttention.Standard,"other")]
    [InlineData(NpcAttention.Equal,"other")]
    public void AttentionChangesRecipientPreferenceOnly(NpcAttention attention,string expected)=>
        Assert.Equal(expected,NpcChoiceRanking.Choose(Input(C("player",player:true),C("other",10)) with{Attention=attention}));

    [Fact] public void TieDoesNotDependOnEnumerationOrCurrentCulture()
    {
        var input=Input(C("a"),C("b"),C("c"));var expected=NpcChoiceRanking.Choose(input);var before=CultureInfo.CurrentCulture;
        try{CultureInfo.CurrentCulture=new("ar-SA");Assert.Equal(expected,NpcChoiceRanking.Choose(input with{Candidates=input.Candidates.Reverse().ToArray()}));}
        finally{CultureInfo.CurrentCulture=before;}
    }
    [Fact] public void InvalidCandidatesAndSettingsAreRejected()
    {
        Assert.Throws<InvalidDataException>(()=>NpcChoiceRanking.Choose(Input(C("same"),C("same"))));
        Assert.Throws<InvalidDataException>(()=>NpcChoiceRanking.Choose(Input(C("a")) with{Attention=(NpcAttention)99}));
        Assert.Throws<InvalidDataException>(()=>NpcChoiceRanking.Choose(Input(C("a")) with{Personality=new(101,50,50,50,50,50)}));
    }
    [Theory]
    [InlineData(NpcCadence.Daily,86400)]
    [InlineData(NpcCadence.Weekly,604800)]
    [InlineData(NpcCadence.Monthly,2505600)]
    public void ReviewUsesSharedClockAndLeapMonth(NpcCadence cadence,long expected)=>
        Assert.Equal(expected,NpcChoiceRanking.NextScheduledReview("1560-02-01",0,cadence));
    [Fact] public void ReviewAlwaysAdvancesBeyondCurrentBoundary()
    {
        Assert.Equal(172800,NpcChoiceRanking.NextScheduledReview("1560-01-01",86400,NpcCadence.Daily));
        Assert.Equal(121*86400L,NpcChoiceRanking.NextScheduledReview("1560-01-01",91*86400L,NpcCadence.Monthly));
    }
}
