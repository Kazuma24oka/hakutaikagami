using HakutaiKagami.Core.Contracts;
using Xunit;
namespace HakutaiKagami.Tests;
public sealed class HistoricalEventRulesTests
{
 static NpcChoiceInput Input(HistoryMode mode)=>new(new("person:test"),1,0,new(50,50,50,50,50,50),NpcAspiration.Unspecified,mode,NpcAttention.Equal,[new("ordinary",true,true,20,new(0,0,0,0,0,0),false,false,false),new("event",true,true,0,new(0,0,0,0,0,0),false,false,false)]);
 [Theory][InlineData(false,false)][InlineData(false,true)][InlineData(true,false)]
 public void ImpossibleOrUnknownCannotBiasChoice(bool physical,bool known){var i=Input(HistoryMode.Historical);var x=HistoricalEventRules.Apply(i,[new("test","event","検証用の架空出典",physical,known)],false,false);Assert.Equal("ordinary",NpcChoiceRanking.Choose(x));}
 [Theory][InlineData(HistoryMode.Historical,"event")][InlineData(HistoryMode.SemiHistorical,"ordinary")][InlineData(HistoryMode.Fictional,"ordinary")]
 public void ModeChangesWeightOnly(HistoryMode mode,string expected){var i=Input(mode);Assert.Equal(expected,NpcChoiceRanking.Choose(HistoricalEventRules.Apply(i,[new("test","event","検証用",true,true)],false,false)));}
 [Fact]public void PlayerDecisionAndContinuationAreRespected(){var i=Input(HistoryMode.Historical);var p=new[]{new HistoricalEventPremise("test","event","検証用",true,true)};Assert.Same(i,HistoricalEventRules.Apply(i,p,true,false));Assert.Equal("ordinary",NpcChoiceRanking.Choose(HistoricalEventRules.Apply(i,p,false,true)));}
 [Fact]public void UnknownOrForbiddenCandidateCannotBecomeLegal(){var i=Input(HistoryMode.Historical);i=i with{Candidates=i.Candidates.Select(c=>c.Id=="event"?c with{Permitted=false}:c).ToArray()};Assert.Equal("ordinary",NpcChoiceRanking.Choose(HistoricalEventRules.Apply(i,[new("test","event","検証用",true,true)],false,false)));}
 [Fact]public void ChronicleFiltersOthersAndSurvivesRestoreWithoutMutation(){var w=ContractFixture.Create();w.Chronicle=[new(new("chronicle:1"),ContractFixture.Officer,"本人の記録",0),new(new("chronicle:2"),ContractFixture.Lord,"秘密",0)];w.IdCounters["chronicle"]=2;var s=new WorldSession(w);var before=ContractJson.Write(s.DiagnosticSnapshot());Assert.Single(s.PersonalChronicle(ContractFixture.Officer));Assert.DoesNotContain(s.PersonalChronicle(ContractFixture.Officer),x=>x.Text=="秘密");var restored=new WorldSession(ContractJson.Read<ContractWorld>(before));Assert.Equal(s.PersonalChronicle(ContractFixture.Officer),restored.PersonalChronicle(ContractFixture.Officer));Assert.Equal(before,ContractJson.Write(s.DiagnosticSnapshot()));Assert.Equal(before,ContractJson.Write(restored.DiagnosticSnapshot()));}
}
